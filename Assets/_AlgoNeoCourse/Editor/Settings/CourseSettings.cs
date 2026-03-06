using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using NeoCource.Editor.Infrastructure;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    public partial class CourseSettings : ScriptableObject
    {
        public enum CourseJsonPreset
        {
            Course1,
            Course2,
            Custom
        }

        public const string AssetPath = "ProjectSettings/AlgoNeoCourseSettings.asset";
        public const string DefaultCourseJsonFile = "course.json";
        public const string SecondCourseJsonFile = "course2.json";

        private static CourseSettings s_Instance;
        private static bool s_IsLoadingInstance;
        private static HttpClient s_HttpClient;

        [Tooltip("Raw-ссылка на GitHub-репозиторий или путь к папке с уроками.")]
        public string repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";

        [Tooltip("Какой JSON курса использовать: первый, второй или свой.")]
        public CourseJsonPreset courseJsonPreset;

        [Tooltip("Имя JSON-файла с описанием курса для режима Custom.")]
        public string courseJsonFile = "course.json";

        [Tooltip("Загружать курс автоматически при открытии окна?")]
        public bool autoLoadOnStart = true;

        [Tooltip("Включить подробное логирование в консоль Unity.")]
        public bool enableDebugLogging;

        [Tooltip("Режим теста — использовать локальные файлы вместо загрузки с GitHub.")]
        public bool testMode;

        [Tooltip("Максимальная длина заголовка урока в дропдауне окна курса.")]
        public int maxLessonTitleLength = 25;

        [Tooltip("Цвет для сообщений OK (консоль)")]
        public Color okLogColor = new(0.2f, 0.85f, 0.2f);

        [Tooltip("Цвет для сообщений FAIL (консоль)")]
        public Color failLogColor = new(1.0f, 0.6f, 0.2f);

        [Tooltip("Автоматически конвертировать .gif в .mp4 для корректного воспроизведения")]
        public bool autoConvertGifToMp4 = true;

        [Tooltip("Путь к ffmpeg.exe (если пусто, конвертация отключена)")]
        public string ffmpegPath = "Assets/_AlgoNeoCourse/Editor/Utils/ffmpeg.exe";

        [Tooltip("Папка кэша mp4 (в проекте)")]
        public string gifVideoCacheFolder = "Assets/_AlgoNeoCourse/VideoCache";

        [Tooltip("Ограничение FPS при конвертации GIF в MP4. Меньше значение = быстрее конвертация и меньший размер файла.")]
        public int gifConversionFps = 12;

        [Tooltip("Максимальная ширина видео после конвертации. 0 или меньше - без ограничения.")]
        public int gifConversionMaxWidth = 960;

        [Tooltip("Путь к локальному course.json (используется только в режиме теста).")]
        public string localCourseJsonPath;

        [Tooltip("Папка, куда сохраняются загруженные уроки (относительно корня проекта).")]
        public string downloadFolderRelative = "Assets/_AlgoNeoCourse/Downloaded";

        [Tooltip(
            "Папка в репозитории, где лежат .md уроки (используется как запасной вариант, если в course.json не задан путь).")]
        public string lessonsFolderInRepo = "lessons";

        public List<LessonSelection> lessonSelections;
        private CancellationTokenSource currentDownloadCts;

        public static CourseSettings instance
        {
            get
            {
                if (s_Instance != null)
                {
                    return s_Instance;
                }

                if (s_IsLoadingInstance)
                {
                    return null;
                }

                s_IsLoadingInstance = true;
                try
                {
                    UnityEngine.Object[] loadedObjects = InternalEditorUtility.LoadSerializedFileAndForget(AssetPath);
                    if (loadedObjects != null)
                    {
                        for (int i = 0; i < loadedObjects.Length; i++)
                        {
                            if (loadedObjects[i] is CourseSettings loadedSettings)
                            {
                                s_Instance = loadedSettings;
                                break;
                            }
                        }
                    }

                    if (s_Instance == null)
                    {
                        s_Instance = CreateInstance<CourseSettings>();
                        s_Instance.ResetToDefaultsWithoutSaving();
                        s_Instance.SaveAsset();
                    }

                    s_Instance.hideFlags = HideFlags.None;
                    s_Instance.EnsureRuntimeState();
                    s_Instance.ApplyDefaultValuesIfNeeded();
                    return s_Instance;
                }
                finally
                {
                    s_IsLoadingInstance = false;
                }
            }
        }

        public static void EnsureAssetExists()
        {
            CourseSettings _ = instance;
        }

        private void EnsureRuntimeState()
        {
            lessonSelections ??= new List<LessonSelection>();
        }

        private void ApplyDefaultValuesIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(repositoryBaseUrl))
            {
                repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";
            }

            if (string.IsNullOrWhiteSpace(courseJsonFile))
            {
                courseJsonFile = DefaultCourseJsonFile;
            }

            if (maxLessonTitleLength <= 0)
            {
                maxLessonTitleLength = 25;
            }

            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                ffmpegPath = "Assets/_AlgoNeoCourse/Editor/Utils/ffmpeg.exe";
            }

            if (string.IsNullOrWhiteSpace(gifVideoCacheFolder))
            {
                gifVideoCacheFolder = "Assets/_AlgoNeoCourse/VideoCache";
            }

            if (gifConversionFps <= 0)
            {
                gifConversionFps = 12;
            }

            if (gifConversionMaxWidth <= 0)
            {
                gifConversionMaxWidth = 960;
            }

            if (string.IsNullOrWhiteSpace(downloadFolderRelative))
            {
                downloadFolderRelative = "Assets/_AlgoNeoCourse/Downloaded";
            }

            if (string.IsNullOrWhiteSpace(lessonsFolderInRepo))
            {
                lessonsFolderInRepo = "lessons";
            }

            if (okLogColor.a <= 0f)
            {
                okLogColor = new Color(0.2f, 0.85f, 0.2f);
            }

            if (failLogColor.a <= 0f)
            {
                failLogColor = new Color(1.0f, 0.6f, 0.2f);
            }
        }

        private void ResetToDefaultsWithoutSaving()
        {
            repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";
            courseJsonPreset = CourseJsonPreset.Course1;
            courseJsonFile = DefaultCourseJsonFile;
            autoLoadOnStart = true;
            enableDebugLogging = false;
            testMode = false;
            localCourseJsonPath = string.Empty;
            maxLessonTitleLength = 25;
            okLogColor = new Color(0.2f, 0.85f, 0.2f);
            failLogColor = new Color(1.0f, 0.6f, 0.2f);
            autoConvertGifToMp4 = true;
            ffmpegPath = "Assets/_AlgoNeoCourse/Editor/Utils/ffmpeg.exe";
            gifVideoCacheFolder = AlgoNeoPackageAssetLocator.DefaultVideoCacheFolderAssetPath;
            gifConversionFps = 12;
            gifConversionMaxWidth = 960;
            downloadFolderRelative = AlgoNeoPackageAssetLocator.DefaultDownloadFolderAssetPath;
            lessonsFolderInRepo = "lessons";
            lessonSelections = new List<LessonSelection>();
        }

        private void SaveAsset()
        {
            EnsureRuntimeState();
            ApplyDefaultValuesIfNeeded();
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, AssetPath, true);
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new();
            try
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.TryParseAdd("AlgoNeoCourseEditor/1.0");
            }
            catch
            {
            }

            return client;
        }

        private static HttpClient GetHttpClient()
        {
            return s_HttpClient ??= CreateHttpClient();
        }

        public string GetDownloadFolderPath()
        {
            return AlgoNeoPackageAssetLocator.NormalizeWritableAssetPath(
                downloadFolderRelative,
                AlgoNeoPackageAssetLocator.DefaultDownloadFolderAssetPath);
        }

        public string GetGifVideoCacheFolderPath()
        {
            return AlgoNeoPackageAssetLocator.NormalizeWritableAssetPath(
                gifVideoCacheFolder,
                AlgoNeoPackageAssetLocator.DefaultVideoCacheFolderAssetPath);
        }

        public string GetFfmpegAssetPath()
        {
            string normalized = (ffmpegPath ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return AlgoNeoPackageAssetLocator.EmbeddedFfmpegAssetPath;
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return AlgoNeoPackageAssetLocator.AssetExists(normalized)
                    ? normalized
                    : AlgoNeoPackageAssetLocator.EmbeddedFfmpegAssetPath;
            }

            return File.Exists(normalized)
                ? normalized
                : AlgoNeoPackageAssetLocator.EmbeddedFfmpegAssetPath;
        }

        public void CheckCourseLink()
        {
            string url = repositoryBaseUrl.TrimEnd('/') + "/" + GetSelectedCourseJsonFile();
            _ = CheckJsonExistsAsync(url);
        }

        public string GetSelectedCourseJsonFile()
        {
            return courseJsonPreset switch
            {
                CourseJsonPreset.Course1 => DefaultCourseJsonFile,
                CourseJsonPreset.Course2 => SecondCourseJsonFile,
                _ => string.IsNullOrWhiteSpace(courseJsonFile) ? DefaultCourseJsonFile : courseJsonFile.Trim()
            };
        }

        public void SelectAllLessons()
        {
            EnsureRuntimeState();
            for (int i = 0; i < lessonSelections.Count; i++)
            {
                lessonSelections[i].selected = true;
            }

            SaveIfPossible();
            RepaintProjectWindow();
        }

        public void DeselectAllLessons()
        {
            EnsureRuntimeState();
            for (int i = 0; i < lessonSelections.Count; i++)
            {
                lessonSelections[i].selected = false;
            }

            SaveIfPossible();
            RepaintProjectWindow();
        }

        [Serializable]
        public class LessonInfo
        {
            public string id;
            public string title;
            public string path;
            public string md;
            public string file;
        }

        [Serializable]
        public class CourseDescriptor
        {
            public LessonInfo[] lessons;
        }

        [Serializable]
        public class LessonSelection
        {
            public bool selected;
            public string id;
            public string title;
            public string file;
        }
    }
}