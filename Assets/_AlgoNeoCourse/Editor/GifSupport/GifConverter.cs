using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Settings;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NeoCource.Editor.GifSupport
{
    public static class GifConverter
    {
        private const int ConversionTimeoutMs = 120000;
        private const string RequestUserAgent = "AlgoNeoCourseEditor/1.0";
        private static readonly Dictionary<string, DateTime> s_FailedUrlsUntil = new();

        public static string ConvertGifToMp4IfNeeded(string gifUrl)
        {
            return ConvertGifToMp4IfNeeded(gifUrl, null, out _);
        }

        public static string ConvertGifToMp4IfNeeded(string gifUrl, Func<bool> shouldCancel, out bool wasCancelled)
        {
            wasCancelled = false;
            CourseSettings settings = CourseSettings.instance;
            if (!settings.autoConvertGifToMp4 || string.IsNullOrWhiteSpace(gifUrl))
            {
                return null;
            }

            if (ShouldSkipUrl(gifUrl))
            {
                return null;
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

            string tempGif = null;
            try
            {
                string cacheDir = settings.GetGifVideoCacheFolderPath();
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                string hash = ComputeStableHash(gifUrl);
                string outName = $"gif_{hash}.mp4";
                string outPath = Path.Combine(cacheDir, outName).Replace('\\', '/');

                if (File.Exists(outPath))
                {
                    if (settings.enableDebugLogging)
                    {
                        Debug.Log($"[AlgoNeoCourse] GIF cache hit → {outPath}");
                    }

                    return outPath;
                }

                string inputGifPath = ResolveInputGifPath(gifUrl, hash, out tempGif, settings.enableDebugLogging);
                if (string.IsNullOrEmpty(inputGifPath) || !LooksLikeGif(inputGifPath))
                {
                    return null;
                }

                ProcessStartInfo psi = new()
                {
                    FileName = ffmpegExe,
                    Arguments = BuildFfmpegArguments(inputGifPath, Path.GetFullPath(outPath), settings),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                if (settings.enableDebugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] Run ffmpeg: \"{psi.FileName}\" {psi.Arguments}");
                }

                using Process proc = Process.Start(psi);
                if (proc == null)
                {
                    Debug.LogWarning($"[AlgoNeoCourse] GIF convert failed: process not started for {gifUrl}");
                    return null;
                }

                int waitedMs = 0;
                while (!proc.WaitForExit(200))
                {
                    waitedMs += 200;

                    if (shouldCancel != null && shouldCancel())
                    {
                        wasCancelled = true;
                        TryKillProcess(proc);
                        return null;
                    }

                    if (waitedMs >= ConversionTimeoutMs)
                    {
                        TryKillProcess(proc);
                        Debug.LogWarning($"[AlgoNeoCourse] GIF convert timeout: {gifUrl}");
                        return null;
                    }
                }

                if (shouldCancel != null && shouldCancel())
                {
                    wasCancelled = true;
                    TryKillProcess(proc);
                    return null;
                }

                string err = string.Empty;
                try
                {
                    err = proc.StandardError.ReadToEnd();
                }
                catch
                {
                }

                if (proc.ExitCode != 0 || !File.Exists(outPath))
                {
                    Debug.LogWarning($"[AlgoNeoCourse] GIF convert failed: {gifUrl}\nExit {proc.ExitCode}\n{err}");
                    return null;
                }

                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ClearFailedUrl(gifUrl);
                if (settings.enableDebugLogging)
                {
                    Debug.Log($"[AlgoNeoCourse] GIF converted → {outPath}");
                }

                return outPath;
            }
            catch (WebException ex)
            {
                HandleNetworkFailure(gifUrl, ex);
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AlgoNeoCourse] GIF convert exception: {ex.Message}");
                return null;
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
            }
        }

        private static string ResolveInputGifPath(string gifUrl, string hash, out string tempGif, bool debugLogging)
        {
            tempGif = null;
            if (TryGetLocalGifPath(gifUrl, out string localPath) && File.Exists(localPath))
            {
                return localPath;
            }

            tempGif = Path.Combine(Path.GetTempPath(), $"algo_gif_{hash}.gif");
            if (debugLogging)
            {
                Debug.Log($"[AlgoNeoCourse] Download image: {gifUrl}");
            }

            using WebClient wc = new();
            wc.Headers[HttpRequestHeader.UserAgent] = RequestUserAgent;
            wc.Headers[HttpRequestHeader.Accept] = "image/gif,image/*;q=0.9,*/*;q=0.8";
            wc.DownloadFile(gifUrl, tempGif);
            return tempGif;
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

        private static void ClearFailedUrl(string gifUrl)
        {
            s_FailedUrlsUntil.Remove(gifUrl);
        }

        private static void HandleNetworkFailure(string gifUrl, WebException ex)
        {
            HttpWebResponse response = ex.Response as HttpWebResponse;
            HttpStatusCode? statusCode = response?.StatusCode;
            TimeSpan cooldown = statusCode == HttpStatusCode.TooManyRequests
                ? TimeSpan.FromMinutes(2)
                : TimeSpan.FromMinutes(10);
            s_FailedUrlsUntil[gifUrl] = DateTime.UtcNow.Add(cooldown);

            string statusText = statusCode.HasValue
                ? $"{(int)statusCode.Value} {statusCode.Value}"
                : ex.Status.ToString();
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