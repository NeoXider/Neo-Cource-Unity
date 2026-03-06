using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NeoCource.Editor.Infrastructure;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace NeoCource.Editor.Settings
{
[UnityEditor.FilePath(CourseSettings.AssetPath, UnityEditor.FilePathAttribute.Location.ProjectFolder)]
public class CourseSettings : ScriptableSingleton<CourseSettings>
{
    public const string AssetPath = "ProjectSettings/AlgoNeoCourseSettings.asset";
    // Аналогично ValidationSettings — не форсируем создание ассета, чтобы не вызывать повторный конструктор
    public static void EnsureAssetExists() { /* no-op */ }
#if ODIN_INSPECTOR
    [Title("Основные настройки")]
#endif
    [Tooltip("Raw-ссылка на GitHub-репозиторий или путь к папке с уроками.")]
    public string repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";

    [Tooltip("Имя JSON-файла с описанием курса.")]
    public string courseJsonFile = "course.json";

#if ODIN_INSPECTOR
    [Title("Поведение плагина")]
#endif
    [Tooltip("Загружать курс автоматически при открытии окна?")]
    public bool autoLoadOnStart = true;

    [Tooltip("Включить подробное логирование в консоль Unity.")]
    public bool enableDebugLogging = false;

    [Tooltip("Режим теста — использовать локальные файлы вместо загрузки с GitHub.")]
    public bool testMode = false;
#if ODIN_INSPECTOR
    [Title("UI")]
#endif
    [Tooltip("Максимальная длина заголовка урока в дропдауне окна курса.")]
    public int maxLessonTitleLength = 20;

    private static readonly System.Net.Http.HttpClient s_HttpClient = CreateHttpClient();
    private static System.Net.Http.HttpClient CreateHttpClient()
    {
        var client = new System.Net.Http.HttpClient();
        try
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("AlgoNeoCourseEditor/1.0");
        }
        catch { }
        return client;
    }

    private System.Threading.CancellationTokenSource currentDownloadCts;

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
            if (AlgoNeoPackageAssetLocator.AssetExists(normalized))
            {
                return normalized;
            }

            return AlgoNeoPackageAssetLocator.EmbeddedFfmpegAssetPath;
        }

        if (File.Exists(normalized))
        {
            return normalized;
        }

        return AlgoNeoPackageAssetLocator.EmbeddedFfmpegAssetPath;
    }


#if ODIN_INSPECTOR
    [Title("Оформление логов проверок")]
#endif
    [Tooltip("Цвет для сообщений OK (консоль)")]
    public Color okLogColor = new Color(0.2f, 0.85f, 0.2f);
    [Tooltip("Цвет для сообщений FAIL (консоль)")]
    public Color failLogColor = new Color(1.0f, 0.6f, 0.2f);

#if ODIN_INSPECTOR
    [Title("GIF → MP4 конвертация (для Markdown)")]
#endif
    [Tooltip("Автоматически конвертировать .gif в .mp4 для корректного воспроизведения")] public bool autoConvertGifToMp4 = true;
    [Tooltip("Путь к ffmpeg.exe (если пусто, конвертация отключена)")] public string ffmpegPath = "Assets/_AlgoNeoCourse/Editor/Utils/ffmpeg.exe";
    [Tooltip("Папка кэша mp4 (в проекте)")] public string gifVideoCacheFolder = "Assets/_AlgoNeoCourse/VideoCache";

#if ODIN_INSPECTOR
    [Title("Локальные тестовые настройки"), ShowIf("testMode")]
#endif
    [Tooltip("Путь к локальному course.json (используется только в режиме теста).")]
    public string localCourseJsonPath;

#if ODIN_INSPECTOR
    [Title("Загрузка и выбор уроков")]
#endif
    [Tooltip("Папка, куда сохраняются загруженные уроки (относительно корня проекта).")]
    public string downloadFolderRelative = AlgoNeoPackageAssetLocator.DefaultDownloadFolderAssetPath;
    [Tooltip("Папка в репозитории, где лежат .md уроки (используется как запасной вариант, если в course.json не задан путь).")]
    public string lessonsFolderInRepo = "lessons";

    [Serializable]
    public class LessonInfo
    {
        public string id;
        public string title;
        public string path; // путь к .md относительный к корню репозитория
        public string md;   // альтернативное имя поля для совместимости
        public string file; // предпочтительное имя файла или относительный путь (из course.json)
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
        public string file; // имя файла или относительный путь в репозитории
    }
    
#if ODIN_INSPECTOR
    [PropertySpace]
    [PropertyOrder(10)]
    [HorizontalGroup("top"), GUIColor(0.6f, 0.75f, 0.95f)]
    [Button("Проверить ссылку", ButtonSizes.Small)]
#endif
    public void CheckCourseLink()
    {
        string url = repositoryBaseUrl.TrimEnd('/') + "/" + courseJsonFile;
        _ = CheckJsonExistsAsync(url);
    }

#if ODIN_INSPECTOR
    [PropertyOrder(30)]
    [TableList, Title("Lessons selections")]
#endif
    public List<LessonSelection> lessonSelections = new List<LessonSelection>();

#if ODIN_INSPECTOR
    [HorizontalGroup("select"), GUIColor(0.4f, 0.85f, 0.55f)]
    [Button("✅ Выбрать все", ButtonSizes.Medium)]
#endif
    public void SelectAllLessons()
    {
        for (int i = 0; i < lessonSelections.Count; i++) lessonSelections[i].selected = true;
        SaveIfPossible();
        RepaintProjectWindow();
    }

#if ODIN_INSPECTOR
    [HorizontalGroup("select"), GUIColor(0.8f, 0.8f, 0.8f)]
    [Button("❎ Снять выбор", ButtonSizes.Medium)]
#endif
    public void DeselectAllLessons()
    {
        for (int i = 0; i < lessonSelections.Count; i++) lessonSelections[i].selected = false;
        SaveIfPossible();
        RepaintProjectWindow();
    }

    private async Task CheckJsonExistsAsync(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                if (enableDebugLogging)
                    Debug.Log($"Проверка ссылки: {url}");

                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        Debug.Log($"✅ Файл найден и загружен: {url}");
                    }
                    else
                    {
                        Debug.LogError($"⚠ Файл пустой: {url}");
                    }
                }
                else
                {
                    Debug.LogError($"❌ Ошибка загрузки: {response.StatusCode} — {url}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Ошибка: {ex.Message}");
            }
        }
    }

#if ODIN_INSPECTOR
    [PropertyOrder(20)]
    [HorizontalGroup("actions"), GUIColor(0.45f, 0.8f, 0.95f)]
    [Button("📥 Загрузить список уроков", ButtonSizes.Large)]
#endif
    public void LoadLessonsList()
    {
        _ = LoadLessonsListAsync();
        // Разворачиваем список уроков в Odin: заголовок уже установлен, Odin покажет таблицу открытой
    }

    private async Task LoadLessonsListAsync()
    {
        try
        {
            EditorUtility.DisplayProgressBar("AlgoNeoCourse", "Загрузка списка уроков...", 0.1f);
            string jsonText = await LoadCourseJsonAsync();
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                Debug.LogError("CourseSettings: пустой course.json");
                return;
            }

            var descriptor = JsonUtility.FromJson<CourseDescriptor>(jsonText);
            if (descriptor == null || descriptor.lessons == null || descriptor.lessons.Length == 0)
            {
                Debug.LogError("CourseSettings: не удалось распарсить список уроков (поле lessons)");
                return;
            }

            lessonSelections = descriptor.lessons.Select((l, idx) =>
            {
                string id = string.IsNullOrEmpty(l.id) ? (idx + 1).ToString() : l.id;
                string title = string.IsNullOrEmpty(l.title) ? ("Lesson " + (idx + 1)) : l.title;
                // приоритет: file -> path -> md -> slug
                string fileOrPath = !string.IsNullOrEmpty(l.file) ? l.file : (!string.IsNullOrEmpty(l.path) ? l.path : l.md);

                if (string.IsNullOrWhiteSpace(fileOrPath))
                {
                    string slug = Slugify(title);
                    string baseFolder = string.IsNullOrWhiteSpace(lessonsFolderInRepo) ? string.Empty : lessonsFolderInRepo.Trim('/');
                    fileOrPath = string.IsNullOrEmpty(baseFolder) ? ($"{slug}.md") : ($"{baseFolder}/{slug}.md");
                    if (enableDebugLogging)
                        Debug.Log($"CourseSettings: для урока '{title}' не задан путь — используем по умолчанию '{fileOrPath}'");
                }

                return new LessonSelection
                {
                    selected = true,
                    id = id,
                    title = title,
                    file = fileOrPath
                };
            }).ToList();

            SaveIfPossible();
            RepaintProjectWindow();
            Debug.Log($"CourseSettings: загружено уроков — {lessonSelections.Count}");
        }
        catch (Exception ex)
        {
            Debug.LogError("CourseSettings: LoadLessonsListAsync error — " + ex.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private async Task<string> LoadCourseJsonAsync()
    {
        if (testMode && !string.IsNullOrEmpty(localCourseJsonPath))
        {
            return File.Exists(localCourseJsonPath) ? await Task.Run(() => File.ReadAllText(localCourseJsonPath)) : null;
        }

        string url = repositoryBaseUrl.TrimEnd('/') + "/" + courseJsonFile.TrimStart('/');
        const int maxAttempts = 3;
        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var response = await s_HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"CourseSettings: не удалось загрузить course.json — {response.StatusCode} {url}");
                    return null;
                }
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    Debug.LogError("CourseSettings: ошибка загрузки course.json — " + ex.Message);
                    return null;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt - 1)));
            }
        }
    }

#if ODIN_INSPECTOR
    [PropertySpace]
    [PropertyOrder(20)]
    [HorizontalGroup("actions")]
    [GUIColor(0.25f, 0.85f, 0.35f)]
    [Button("Скачать выбранные", ButtonSizes.Large)]
#endif
    public void DownloadSelectedLessons()
    {
        CancelDownloads();
        currentDownloadCts = new System.Threading.CancellationTokenSource();
        _ = DownloadSelectedLessonsAsync(currentDownloadCts.Token);
    }

    private async Task DownloadSelectedLessonsAsync(System.Threading.CancellationToken ct)
    {
        if (lessonSelections == null || lessonSelections.Count == 0)
        {
            Debug.LogWarning("CourseSettings: список уроков пуст. Сначала выполните 'Загрузить список уроков'.");
            return;
        }

        string targetFolder = GetDownloadFolderPath();
        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        var client = s_HttpClient;

        try
        {
            int total = lessonSelections.Count(ls => ls.selected);
            int done = 0;
            foreach (var ls in lessonSelections.Where(s => s.selected))
            {
                if (ct.IsCancellationRequested) break;
                string remoteRel = ls.file?.TrimStart('/');
                if (string.IsNullOrEmpty(remoteRel))
                {
                    Debug.LogWarning($"CourseSettings: у урока '{ls.title}' не задан путь к .md");
                    continue;
                }

                string url = repositoryBaseUrl.TrimEnd('/') + "/" + remoteRel;
                string localPath = Path.Combine(targetFolder, SanitizeFileName(ls.id + "-" + Path.GetFileName(remoteRel)));

                EditorUtility.DisplayProgressBar("Загрузка уроков", $"{ls.title}", done / (float)Math.Max(total,1));

                const int maxAttempts = 3;
                int attempt = 0;
                while (true)
                {
                    if (ct.IsCancellationRequested) break;
                    attempt++;
                    try
                    {
                        var response = await client.GetAsync(url, ct);
                        if (!response.IsSuccessStatusCode)
                        {
                            Debug.LogError($"CourseSettings: не удалось загрузить {ls.title} — {response.StatusCode} {url}");
                        }
                        else
                        {
                            string md = await response.Content.ReadAsStringAsync();
                            File.WriteAllText(localPath, md);
                            Debug.Log($"Сохранено: {localPath}");
                        }
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt >= maxAttempts)
                        {
                            Debug.LogError("CourseSettings: ошибка загрузки урока — " + ex.Message);
                            break;
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt - 1)), ct);
                    }
                }

                done++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("CourseSettings: ошибка загрузки уроков — " + ex.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            CancelDownloads();
        }
    }

    public void CancelDownloads()
    {
        try { currentDownloadCts?.Cancel(); }
        catch { }
        finally { currentDownloadCts = null; }
    }

#if ODIN_INSPECTOR
    [PropertyOrder(20)]
    [HorizontalGroup("actions")]
    [GUIColor(0.98f, 0.75f, 0.25f)]
    [Button("Отменить", ButtonSizes.Medium)]
#endif
    public void CancelDownloadsButton()
    {
        CancelDownloads();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "lesson";
        string lower = input.Trim().ToLowerInvariant();
        // заменим пробелы/дефисы на '-'
        lower = Regex.Replace(lower, @"[\t\s_]+", "-");
        // уберём все не буквенно-цифровые и дефисы
        lower = Regex.Replace(lower, @"[^a-z0-9-]", "");
        lower = Regex.Replace(lower, "-+", "-");
        lower = lower.Trim('-');
        if (string.IsNullOrEmpty(lower)) lower = "lesson";
        return lower;
    }

#if ODIN_INSPECTOR
    [PropertyOrder(20)]
    [HorizontalGroup("actions")]
    [GUIColor(1.0f, 0.35f, 0.35f)]
    [Button("Очистить загрузки", ButtonSizes.Medium)]
#endif
    public void DeleteDownloadedFiles()
    {
        string targetFolder = GetDownloadFolderPath();
        if (Directory.Exists(targetFolder))
        {
            try
            {
                FileUtil.DeleteFileOrDirectory(targetFolder);
                FileUtil.DeleteFileOrDirectory(targetFolder + ".meta");
                Debug.Log("CourseSettings: папка с загруженными уроками очищена.");
            }
            catch (Exception ex)
            {
                Debug.LogError("CourseSettings: не удалось удалить папку — " + ex.Message);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }
        else
        {
            Debug.Log("CourseSettings: загруженных файлов не найдено.");
        }
    }

    private static void RepaintProjectWindow()
    {
        EditorApplication.RepaintProjectWindow();
        SceneView.RepaintAll();
    }

    private void SaveIfPossible()
    {
        try
        {
            Save(true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("CourseSettings: сохранение настроек пропущено — " + ex.Message);
        }
    }

#if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("♻ Сбросить настройки", Sirenix.OdinInspector.ButtonSizes.Small)]
    [Sirenix.OdinInspector.GUIColor(0.98f, 0.7f, 0.45f)]
#endif
    public void ResetToDefaults()
    {
        repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";
        courseJsonFile = "course.json";
        autoLoadOnStart = true;
        enableDebugLogging = false;
        testMode = false;
        localCourseJsonPath = string.Empty;
        autoConvertGifToMp4 = true;
        ffmpegPath = "Assets/_AlgoNeoCourse/Editor/Utils/ffmpeg.exe";
        gifVideoCacheFolder = AlgoNeoPackageAssetLocator.DefaultVideoCacheFolderAssetPath;
        downloadFolderRelative = AlgoNeoPackageAssetLocator.DefaultDownloadFolderAssetPath;
        lessonsFolderInRepo = "lessons";
        lessonSelections.Clear();
        Save(true);
    }
}

public static class CourseSettingsMenu
{
    [UnityEditor.MenuItem("Tools/AlgoNeoCourse/Settings/Open Course Settings")]
    public static void Open()
    {
        var inst = CourseSettings.instance;
        UnityEditor.Selection.activeObject = inst;
        try
        {
            if (inst != null && inst.autoLoadOnStart && (inst.lessonSelections == null || inst.lessonSelections.Count == 0))
            {
                inst.LoadLessonsList();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"CourseSettings: авто-загрузка при открытии не удалась — {ex.Message}");
        }
    }

    [UnityEditor.MenuItem("Tools/AlgoNeoCourse/Settings/Reset Course Settings")] 
    public static void Reset()
    {
        CourseSettings.EnsureAssetExists();
        CourseSettings.instance.ResetToDefaults();
        UnityEditor.EditorUtility.DisplayDialog("AlgoNeoCourse", "Course Settings сброшены к значениям по умолчанию.", "OK");
    }
}

[InitializeOnLoad]
public static class CourseSettingsBootstrap
{
    static CourseSettingsBootstrap()
    {
        // Автозагрузка при старте редактора, если включено и список пуст
        EditorApplication.delayCall += () =>
        {
            try
            {
                var inst = CourseSettings.instance;
                if (inst != null && inst.autoLoadOnStart && (inst.lessonSelections == null || inst.lessonSelections.Count == 0))
                {
                    inst.LoadLessonsList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseSettings: авто-загрузка при старте не удалась — " + ex.Message);
            }
        };
    }
}

}
