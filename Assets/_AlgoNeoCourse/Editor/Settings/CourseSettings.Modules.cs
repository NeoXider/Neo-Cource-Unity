using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NeoCource.Editor.Infrastructure;
using UnityEditor;

namespace NeoCource.Editor.Settings
{
    // Уроки, сгруппированные по модулям: для инспектора настроек («скачать только m5»).
    public partial class CourseSettings
    {
        private static readonly Regex ModuleSegmentRegex = new(@"^m(\d+)$", RegexOptions.IgnoreCase);
        private static readonly Regex ModuleInIdRegex = new(@"m(\d+)", RegexOptions.IgnoreCase);

        private HashSet<string> downloadedFileNames;
        private double downloadedFileNamesTime = -1;

        public bool IsLoadingLessonsList { get; private set; }

        // Ключ модуля: сегмент пути вида «m5», иначе «mN» из id, иначе «other».
        public static string GetModuleKey(LessonSelection lesson)
        {
            if (lesson == null)
            {
                return "other";
            }

            string file = (lesson.file ?? string.Empty).Replace('\\', '/');
            foreach (string segment in file.Split('/'))
            {
                if (ModuleSegmentRegex.IsMatch(segment))
                {
                    return segment.ToLowerInvariant();
                }
            }

            Match match = ModuleInIdRegex.Match(lesson.id ?? string.Empty);
            return match.Success ? "m" + match.Groups[1].Value : "other";
        }

        // «l2 / m5 / Арбалет: объект…» -> «y1 · Арбалет: объект…»
        public static string GetShortLessonTitle(LessonSelection lesson)
        {
            if (lesson == null)
            {
                return string.Empty;
            }

            string title = lesson.title ?? string.Empty;
            int cut = title.LastIndexOf(" / ", StringComparison.Ordinal);
            if (cut >= 0 && cut + 3 < title.Length)
            {
                title = title.Substring(cut + 3);
            }

            string fileName = Path.GetFileNameWithoutExtension(lesson.file ?? string.Empty);
            return string.IsNullOrEmpty(fileName) ? title : fileName + " · " + title;
        }

        public List<LessonModule> GetLessonModules()
        {
            EnsureRuntimeState();
            List<LessonModule> modules = new();
            Dictionary<string, LessonModule> byKey = new(StringComparer.OrdinalIgnoreCase);
            foreach (LessonSelection lesson in lessonSelections)
            {
                if (lesson == null)
                {
                    continue;
                }

                string key = GetModuleKey(lesson);
                if (!byKey.TryGetValue(key, out LessonModule module))
                {
                    Match number = ModuleSegmentRegex.Match(key);
                    module = new LessonModule
                    {
                        key = key,
                        title = number.Success ? "Модуль " + number.Groups[1].Value + "  (" + key + ")" : "Без модуля"
                    };
                    byKey[key] = module;
                    modules.Add(module);
                }

                module.lessons.Add(lesson);
            }

            return modules;
        }

        // Имя файла урока в папке загрузки — то же правило, что при скачивании.
        public static string GetDownloadedFileName(LessonSelection lesson)
        {
            string remote = lesson?.file?.TrimStart('/');
            return string.IsNullOrEmpty(remote)
                ? null
                : SanitizeFileName(lesson.id + "-" + Path.GetFileName(remote));
        }

        public bool IsLessonDownloaded(LessonSelection lesson)
        {
            string fileName = GetDownloadedFileName(lesson);
            if (fileName == null)
            {
                return false;
            }

            // Список файлов перечитываем не чаще раза в секунду: инспектор перерисовывается часто.
            double now = EditorApplication.timeSinceStartup;
            if (downloadedFileNames == null || now - downloadedFileNamesTime > 1.0 || now < downloadedFileNamesTime)
            {
                downloadedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string folder = AlgoNeoPackageAssetLocator.ToAbsolutePath(GetDownloadFolderPath());
                    if (Directory.Exists(folder))
                    {
                        foreach (string path in Directory.GetFiles(folder, "*.md"))
                        {
                            downloadedFileNames.Add(Path.GetFileName(path));
                        }
                    }
                }
                catch
                {
                    // Нет папки или нет доступа — считаем, что ничего не скачано.
                }

                downloadedFileNamesTime = now;
            }

            return downloadedFileNames.Contains(fileName);
        }

        private void InvalidateDownloadedCache()
        {
            downloadedFileNames = null;
        }

        public class LessonModule
        {
            public string key;
            public string title;
            public readonly List<LessonSelection> lessons = new();
        }
    }
}
