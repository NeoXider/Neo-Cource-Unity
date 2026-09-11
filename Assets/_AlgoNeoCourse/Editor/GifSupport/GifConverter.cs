using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Settings;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NeoCource.Editor.GifSupport
{
    public static class GifConverter
    {
        private const int ConversionTimeoutMs = 120000;
        private const int DownloadTimeoutSeconds = 60;
        private const string RequestUserAgent = "AlgoNeoCourseEditor/1.0";
        // Cooldown неуспешных URL. Трогать только под lock (s_Lock): пишут и UI-, и фоновый потоки.
        private static readonly Dictionary<string, DateTime> s_FailedUrlsUntil = new();
        // URL, уже конвертирующиеся в фоне. Доступ только под lock (s_Lock).
        private static readonly HashSet<string> s_InFlight = new();
        private static readonly object s_Lock = new();

        // Есть ли незавершённые фоновые конвертации (опрашивается из UI-потока).
        public static bool HasPendingConversions
        {
            get
            {
                lock (s_Lock)
                {
                    return s_InFlight.Count > 0;
                }
            }
        }

        // Вызывается в главном потоке (через EditorApplication.delayCall) по завершении каждой фоновой задачи.
        public static event Action ConversionFinished;

        public static string ConvertGifToMp4IfNeeded(string gifUrl)
        {
            return ConvertGifToMp4IfNeeded(gifUrl, null, out _);
        }

        public static string ConvertGifToMp4IfNeeded(string gifUrl, Func<bool> shouldCancel, out bool wasCancelled)
        {
            wasCancelled = false;

            // Всё до Task.Run выполняется в главном потоке: делаем снапшот настроек в локальные переменные.
            CourseSettings settings = CourseSettings.instance;
            if (settings == null || !settings.autoConvertGifToMp4 || string.IsNullOrWhiteSpace(gifUrl))
            {
                return null;
            }

            if (ShouldSkipUrl(gifUrl))
            {
                return null;
            }

            lock (s_Lock)
            {
                if (s_InFlight.Contains(gifUrl))
                {
                    // Конвертация уже идёт в фоне — не дублируем задачу.
                    return null;
                }
            }

            string ffmpegExe = settings.GetFfmpegAssetPath();
            if (!string.IsNullOrEmpty(ffmpegExe) &&
                (ffmpegExe.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                 ffmpegExe.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)))
            {
                ffmpegExe = AlgoNeoPackageAssetLocator.ToAbsolutePath(ffmpegExe);
            }

            if (string.IsNullOrEmpty(ffmpegExe) || !File.Exists(ffmpegExe))
            {
                if (settings.enableDebugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] Skip GIF convert: ffmpeg not found at '{settings.ffmpegPath}'");
                }

                return null;
            }

            // Снапшот настроек: в фон передаём только обычные данные, без Unity-объектов.
            bool debugLogging = settings.enableDebugLogging;
            int fps = settings.gifConversionFps;
            int maxWidth = settings.gifConversionMaxWidth;
            string cacheDirAsset = settings.GetGifVideoCacheFolderPath().Replace('\\', '/');
            string cacheDirAbs = AlgoNeoPackageAssetLocator.ToAbsolutePath(cacheDirAsset);
            try
            {
                if (!Directory.Exists(cacheDirAbs))
                {
                    Directory.CreateDirectory(cacheDirAbs);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AlgoNeoCourse] GIF convert: no cache dir '{cacheDirAsset}': {ex.Message}");
                return null;
            }

            string hash = ComputeStableHash(gifUrl);
            string outName = $"gif_{hash}.mp4";
            string outAssetPath = cacheDirAsset.TrimEnd('/') + "/" + outName;
            string outAbsPath = Path.Combine(cacheDirAbs, outName);

            // Быстрый путь: mp4 уже в кэше — возвращаем asset-путь сразу, без фона.
            if (File.Exists(outAbsPath))
            {
                if (debugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] GIF cache hit → {outAssetPath}");
                }

                return outAssetPath;
            }

            lock (s_Lock)
            {
                if (!s_InFlight.Add(gifUrl))
                {
                    return null;
                }
            }

            // Медленный путь: скачивание и ffmpeg уезжают в фон, UI не блокируется.
            // shouldCancel в фоне не проверяем: делегат может обращаться к Unity API, что в фоне запрещено.
            Task.Run(() => ConvertInBackgroundAsync(gifUrl, hash, ffmpegExe, fps, maxWidth, outAssetPath, outAbsPath, debugLogging));
            return null;
        }

        // Фоновая задача: Unity API запрещены, кроме Debug.Log. Импорт ассета — только через delayCall в finally.
        private static async Task ConvertInBackgroundAsync(string gifUrl, string hash, string ffmpegExe, int fps, int maxWidth, string outAssetPath, string outAbsPath, bool debugLogging)
        {
            bool success = false;
            string tempGif = null;
            try
            {
                (string inputGifPath, string downloadedTemp) = await ResolveInputGifPathAsync(gifUrl, hash, debugLogging);
                tempGif = downloadedTemp;
                if (string.IsNullOrEmpty(inputGifPath) || !LooksLikeGif(inputGifPath))
                {
                    return;
                }

                ProcessStartInfo psi = new()
                {
                    FileName = ffmpegExe,
                    Arguments = BuildFfmpegArguments(inputGifPath, outAbsPath, fps, maxWidth),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                if (debugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] Run ffmpeg: \"{psi.FileName}\" {psi.Arguments}");
                }

                using Process proc = Process.Start(psi);
                if (proc == null)
                {
                    Debug.LogWarning($"[AlgoNeoCourse] GIF convert failed: process not started for {gifUrl}");
                    return;
                }

                int waitedMs = 0;
                while (!proc.WaitForExit(200))
                {
                    waitedMs += 200;
                    if (waitedMs >= ConversionTimeoutMs)
                    {
                        TryKillProcess(proc);
                        Debug.LogWarning($"[AlgoNeoCourse] GIF convert timeout: {gifUrl}");
                        return;
                    }
                }

                string err = string.Empty;
                try
                {
                    err = proc.StandardError.ReadToEnd();
                }
                catch
                {
                }

                if (proc.ExitCode != 0 || !File.Exists(outAbsPath))
                {
                    Debug.LogWarning($"[AlgoNeoCourse] GIF convert failed: {gifUrl}\nExit {proc.ExitCode}\n{err}");
                    return;
                }

                if (debugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] GIF converted → {outAssetPath}");
                }

                success = true;
            }
            catch (HttpRequestException ex)
            {
                HandleNetworkFailure(gifUrl, ex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AlgoNeoCourse] GIF convert exception: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempGif) && File.Exists(tempGif))
                {
                    try
                    {
                        File.Delete(tempGif);
                    }
                    catch
                    {
                    }
                }

                lock (s_Lock)
                {
                    s_InFlight.Remove(gifUrl);
                    if (success)
                    {
                        s_FailedUrlsUntil.Remove(gifUrl);
                    }
                }

                // Возврат в главный поток: импорт ассета и уведомление подписчиков.
                try
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            if (success && File.Exists(outAbsPath))
                            {
                                AssetDatabase.ImportAsset(outAssetPath);
                            }
                        }
                        catch
                        {
                        }

                        try
                        {
                            ConversionFinished?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AlgoNeoCourse] ConversionFinished handler failed: {ex.Message}");
                        }
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AlgoNeoCourse] GIF convert: delayCall failed: {ex.Message}");
                }
            }
        }

        // Скачивание GIF: свой HttpClient с таймаутом 60с (общий клиент настроек не трогаем).
        private static async Task<(string inputPath, string tempGif)> ResolveInputGifPathAsync(string gifUrl, string hash, bool debugLogging)
        {
            if (TryGetLocalGifPath(gifUrl, out string localPath) && File.Exists(localPath))
            {
                return (localPath, null);
            }

            string tempGif = Path.Combine(Path.GetTempPath(), $"algo_gif_{hash}.gif");
            if (debugLogging)
            {
                Debug.Log($"[AlgoNeoCourse] Download image: {gifUrl}");
            }

            using HttpClient http = new();
            http.Timeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds);
            http.DefaultRequestHeaders.UserAgent.TryParseAdd(RequestUserAgent);
            http.DefaultRequestHeaders.Accept.TryParseAdd("image/gif,image/*;q=0.9,*/*;q=0.8");
            using HttpResponseMessage response = await http.GetAsync(gifUrl);
            response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(tempGif, bytes);
            return (tempGif, tempGif);
        }

        private static bool TryGetLocalGifPath(string gifUrl, out string localPath)
        {
            localPath = null;
            if (Uri.TryCreate(gifUrl, UriKind.Absolute, out Uri uri) && uri.IsFile)
            {
                localPath = uri.LocalPath;
                return true;
            }

            if (File.Exists(gifUrl))
            {
                localPath = gifUrl;
                return true;
            }

            return false;
        }

        private static bool LooksLikeGif(string path)
        {
            try
            {
                using FileStream fs = File.OpenRead(path);
                Span<byte> header = stackalloc byte[6];
                int read = fs.Read(header);
                return read >= 6 &&
                       header[0] == (byte)'G' &&
                       header[1] == (byte)'I' &&
                       header[2] == (byte)'F';
            }
            catch
            {
                return false;
            }
        }

        private static string BuildFfmpegArguments(string inputGifPath, string outputMp4Path, CourseSettings settings)
        {
            int fps = Math.Clamp(settings.gifConversionFps, 1, 30);
            int maxWidth = Math.Max(0, settings.gifConversionMaxWidth);
            return BuildFfmpegArguments(inputGifPath, outputMp4Path, fps, maxWidth);
        }

        // Перегрузка для фоновой конвертации: только снапшот чисел, без обращения к CourseSettings.
        private static string BuildFfmpegArguments(string inputGifPath, string outputMp4Path, int fps, int maxWidth)
        {
            fps = Math.Clamp(fps, 1, 30);
            maxWidth = Math.Max(0, maxWidth);
            string videoFilter = BuildVideoFilter(fps, maxWidth);

            return $"-y -hide_banner -loglevel error -nostdin -threads 0 -i \"{inputGifPath}\" -an -sn -dn -vf \"{videoFilter}\" -c:v libx264 -preset ultrafast -tune fastdecode -crf 32 -movflags +faststart -pix_fmt yuv420p \"{outputMp4Path}\"";
        }

        private static string BuildVideoFilter(int fps, int maxWidth)
        {
            if (maxWidth > 0)
            {
                return $"fps={fps},scale='if(gt(iw,{maxWidth}),{maxWidth},iw)':-2:flags=fast_bilinear,scale=trunc(iw/2)*2:trunc(ih/2)*2";
            }

            return $"fps={fps},scale=trunc(iw/2)*2:trunc(ih/2)*2";
        }

        private static string ComputeStableHash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(16);
            for (int i = 0; i < 8 && i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("X2"));
            }

            return builder.ToString();
        }

        private static bool ShouldSkipUrl(string gifUrl)
        {
            lock (s_Lock)
            {
                if (s_FailedUrlsUntil.TryGetValue(gifUrl, out DateTime until))
                {
                    if (until > DateTime.UtcNow)
                    {
                        return true;
                    }

                    s_FailedUrlsUntil.Remove(gifUrl);
                }

                return false;
            }
        }

        private static void HandleNetworkFailure(string gifUrl, HttpRequestException ex)
        {
            lock (s_Lock)
            {
                s_FailedUrlsUntil[gifUrl] = DateTime.UtcNow.AddMinutes(10);
            }

            string statusText = ex.Message;
            Debug.LogWarning($"[AlgoNeoCourse] GIF download skipped for a while: {statusText} {gifUrl}");
        }

        private static void TryKillProcess(Process proc)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                }
            }
            catch
            {
            }
        }
    }
}
