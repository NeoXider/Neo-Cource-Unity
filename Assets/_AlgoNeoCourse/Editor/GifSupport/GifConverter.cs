using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using NeoCource.Editor.Settings;

namespace NeoCource.Editor.GifSupport
{
    public static class GifConverter
    {
        // Конвертирует gifUrl (http/file) в локальный mp4 в кэше проекта. Возвращает путь Assets/... к mp4 или null при ошибке.
        public static string ConvertGifToMp4IfNeeded(string gifUrl)
        {
            var settings = CourseSettings.instance;
            if (!settings.autoConvertGifToMp4) return null;
            // Resolve ffmpeg path (supports Assets/...)
            string ffmpegExe = settings.ffmpegPath;
            if (!string.IsNullOrEmpty(ffmpegExe) && ffmpegExe.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                ffmpegExe = Path.Combine(projectRoot ?? string.Empty, ffmpegExe).Replace('\\', '/');
            }
            if (string.IsNullOrEmpty(ffmpegExe) || !File.Exists(ffmpegExe))
            {
                if (settings.enableDebugLogging)
                    UnityEngine.Debug.Log($"[AlgoNeoCourse] Skip GIF convert: ffmpeg not found at '{settings.ffmpegPath}'");
                return null;
            }

            try
            {
                // Создадим кэш-папку
                string cacheDir = settings.gifVideoCacheFolder;
                if (string.IsNullOrEmpty(cacheDir)) cacheDir = "Assets/_AlgoNeoCourse/VideoCache";
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                // Имя файла по хэшу URL
                string hash = gifUrl.GetHashCode().ToString("X8");
                string outName = $"gif_{hash}.mp4";
                string outPath = Path.Combine(cacheDir, outName).Replace('\\', '/');

                // Если уже существует — вернуть
                if (File.Exists(outPath))
                {
                    if (settings.enableDebugLogging)
                        UnityEngine.Debug.Log($"[AlgoNeoCourse] GIF cache hit → {outPath}");
                    return outPath;
                }

                // Скачать во временный файл
                string tempGif = Path.Combine(Path.GetTempPath(), $"algo_gif_{hash}.bin");
                if (settings.enableDebugLogging)
                    UnityEngine.Debug.Log($"[AlgoNeoCourse] Download image: {gifUrl}");

                using (var wc = new System.Net.WebClient())
                {
                    wc.DownloadFile(gifUrl, tempGif);
                }

                // Мини-проверка сигнатуры GIF: 'GIF87a' или 'GIF89a'
                bool isGif = false;
                try
                {
                    using (var fs = File.OpenRead(tempGif))
                    {
                        Span<byte> header = stackalloc byte[6];
                        int read = fs.Read(header);
                        if (read >= 6)
                        {
                            isGif = (header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F');
                        }
                    }
                }
                catch { }
                if (!isGif)
                {
                    if (settings.enableDebugLogging)
                        UnityEngine.Debug.Log("[AlgoNeoCourse] Downloaded image is not GIF — skipping conversion.");
                    try { File.Delete(tempGif); } catch { }
                    return null;
                }

                // Вызов ffmpeg: ffmpeg -y -i input.gif -movflags faststart -pix_fmt yuv420p output.mp4
                // Оптимизированные параметры для скорости: -preset veryfast (быстрое кодирование), -crf 28 (ниже качество, быстрее), -vf "fps=15" (меньше кадров)
                // scale=trunc(iw/2)*2:trunc(ih/2)*2 - для совместимости с yuv420p, который требует четных размеров кадра.
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = $"-y -i \"{tempGif}\" -vf \"fps=15,scale=trunc(iw/2)*2:trunc(ih/2)*2\" -preset veryfast -crf 28 -movflags faststart -pix_fmt yuv420p \"{Path.GetFullPath(outPath)}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                if (settings.enableDebugLogging)
                    UnityEngine.Debug.Log($"[AlgoNeoCourse] Run ffmpeg: \"{psi.FileName}\" {psi.Arguments}");
                var proc = Process.Start(psi);
                proc.WaitForExit(30000);

                if (proc.ExitCode != 0 || !File.Exists(outPath))
                {
                    var err = string.Empty;
                    try { err = proc.StandardError.ReadToEnd(); } catch { }
                    UnityEngine.Debug.LogWarning($"[AlgoNeoCourse] GIF convert failed: {gifUrl}\nExit {proc.ExitCode}\n{err}");
                    return null;
                }

                AssetDatabase.ImportAsset(outPath);
                if (settings.enableDebugLogging)
                    UnityEngine.Debug.Log($"[AlgoNeoCourse] GIF converted → {outPath}");
                return outPath;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AlgoNeoCourse] GIF convert exception: {ex.Message}");
                return null;
            }
        }
    }
}
