using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    [Title("Оформление логов проверок")]
#endif
    [Tooltip("Цвет для сообщений OK (консоль)")]
    public Color okLogColor = new Color(0.2f, 0.85f, 0.2f);
    [Tooltip("Цвет для сообщений FAIL (консоль)")]
    public Color failLogColor = new Color(1.0f, 0.6f, 0.2f);

#if ODIN_INSPECTOR
    [Title("Локальные тестовые настройки"), ShowIf("testMode")]
#endif
    [Tooltip("Путь к локальному course.json (используется только в режиме теста).")]
    public string localCourseJsonPath;

#if ODIN_INSPECTOR
    [Title("Загрузка и выбор уроков")]
#endif
    [Tooltip("Папка, куда сохраняются загруженные уроки (относительно корня проекта).")]
    public string downloadFolderRelative = "Assets/_AlgoNeoCourse/Downloaded";
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
    [GUIColor(0.3f, 0.6f, 1f)]
    [Button("🔗 Проверить ссылку", ButtonSizes.Medium)]
#endif
    public void CheckCourseLink()
    {
        string url = repositoryBaseUrl.TrimEnd('/') + "/" + courseJsonFile;
        _ = CheckJsonExistsAsync(url);
    }

#if ODIN_INSPECTOR
    [TableList]
#endif
    public List<LessonSelection> lessonSelections = new List<LessonSelection>();

#if ODIN_INSPECTOR
    [HorizontalGroup("sel"), GUIColor(0.3f, 0.8f, 0.4f)]
    [Button("✅ Выбрать все", ButtonSizes.Medium)]
#endif
    public void SelectAllLessons()
    {
        for (int i = 0; i < lessonSelections.Count; i++) lessonSelections[i].selected = true;
        SaveIfPossible();
        RepaintProjectWindow();
    }

#if ODIN_INSPECTOR
    [HorizontalGroup("sel"), GUIColor(0.6f, 0.6f, 0.6f)]
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
    [PropertySpace]
    [GUIColor(0.2f, 0.8f, 0.8f)]
    [Button("📥 Загрузить список уроков", ButtonSizes.Large)]
#endif
    public void LoadLessonsList()
    {
        _ = LoadLessonsListAsync();
    }

    private async Task LoadLessonsListAsync()
    {
        try
        {
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
    }

    private async Task<string> LoadCourseJsonAsync()
    {
        if (testMode && !string.IsNullOrEmpty(localCourseJsonPath))
        {
            return File.Exists(localCourseJsonPath) ? await Task.Run(() => File.ReadAllText(localCourseJsonPath)) : null;
        }

        string url = repositoryBaseUrl.TrimEnd('/') + "/" + courseJsonFile.TrimStart('/');
        using var client = new HttpClient();
        try
        {
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("AlgoNeoCourseEditor/1.0");
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"CourseSettings: не удалось загрузить course.json — {response.StatusCode} {url}");
                return null;
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("CourseSettings: ошибка загрузки course.json — " + ex.Message);
            return null;
        }
    }

#if ODIN_INSPECTOR
    [PropertySpace]
    [HorizontalGroup("dl")]
    [GUIColor(0.2f, 0.7f, 0.3f)]
    [Button("⬇️ Скачать выбранные", ButtonSizes.Large)]
#endif
    public void DownloadSelectedLessons()
    {
        _ = DownloadSelectedLessonsAsync();
    }

    private async Task DownloadSelectedLessonsAsync()
    {
        if (lessonSelections == null || lessonSelections.Count == 0)
        {
            Debug.LogWarning("CourseSettings: список уроков пуст. Сначала выполните 'Загрузить список уроков'.");
            return;
        }

        string targetFolder = downloadFolderRelative.Replace("\\", "/");
        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("AlgoNeoCourseEditor/1.0");

        try
        {
            int total = lessonSelections.Count(ls => ls.selected);
            int done = 0;
            foreach (var ls in lessonSelections.Where(s => s.selected))
            {
                string remoteRel = ls.file?.TrimStart('/');
                if (string.IsNullOrEmpty(remoteRel))
                {
                    Debug.LogWarning($"CourseSettings: у урока '{ls.title}' не задан путь к .md");
                    continue;
                }

                string url = repositoryBaseUrl.TrimEnd('/') + "/" + remoteRel;
                string localPath = Path.Combine(targetFolder, SanitizeFileName(ls.id + "-" + Path.GetFileName(remoteRel)));

                EditorUtility.DisplayProgressBar("Загрузка уроков", $"{ls.title}", done / (float)Math.Max(total,1));

                var response = await client.GetAsync(url);
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
        }
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
    [HorizontalGroup("dl")]
    [GUIColor(0.9f, 0.35f, 0.35f)]
    [Button("🗑️ Очистить загрузки", ButtonSizes.Medium)]
#endif
    public void DeleteDownloadedFiles()
    {
        string targetFolder = downloadFolderRelative.Replace("\\", "/");
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
    [Sirenix.OdinInspector.Button("♻ Сбросить настройки", Sirenix.OdinInspector.ButtonSizes.Medium)]
    [Sirenix.OdinInspector.GUIColor(0.95f, 0.5f, 0.2f)]
#endif
    public void ResetToDefaults()
    {
        repositoryBaseUrl = "https://raw.githubusercontent.com/NeoXider/AlgoNeoCource/main";
        courseJsonFile = "course.json";
        autoLoadOnStart = true;
        enableDebugLogging = false;
        testMode = false;
        localCourseJsonPath = string.Empty;
        downloadFolderRelative = "Assets/_AlgoNeoCourse/Downloaded";
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
        UnityEditor.Selection.activeObject = CourseSettings.instance;
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
    static CourseSettingsBootstrap() { }
}

}
