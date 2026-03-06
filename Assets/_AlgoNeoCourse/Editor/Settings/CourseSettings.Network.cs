using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    public partial class CourseSettings
    {
        private async Task CheckJsonExistsAsync(string url)
        {
            using (HttpClient client = new())
            {
                try
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log($"Проверка ссылки: {url}");
                    }

                    HttpResponseMessage response = await client.GetAsync(url);
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
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Ошибка: {ex.Message}");
                }
            }
        }

        public void LoadLessonsList()
        {
            _ = LoadLessonsListAsync();
        }

        private async Task LoadLessonsListAsync()
        {
            EnsureRuntimeState();
            try
            {
                EditorUtility.DisplayProgressBar("AlgoNeoCourse", "Загрузка списка уроков...", 0.1f);
                string jsonText = await LoadCourseJsonAsync();
                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    Debug.LogError("CourseSettings: пустой course.json");
                    return;
                }

                CourseDescriptor descriptor = JsonUtility.FromJson<CourseDescriptor>(jsonText);
                if (descriptor?.lessons == null || descriptor.lessons.Length == 0)
                {
                    Debug.LogError("CourseSettings: не удалось распарсить список уроков (поле lessons)");
                    return;
                }

                lessonSelections = descriptor.lessons.Select((lesson, index) =>
                {
                    string id = string.IsNullOrEmpty(lesson.id) ? (index + 1).ToString() : lesson.id;
                    string title = string.IsNullOrEmpty(lesson.title) ? "Lesson " + (index + 1) : lesson.title;
                    string fileOrPath = !string.IsNullOrEmpty(lesson.file) ? lesson.file :
                        !string.IsNullOrEmpty(lesson.path) ? lesson.path : lesson.md;

                    if (string.IsNullOrWhiteSpace(fileOrPath))
                    {
                        string slug = Slugify(title);
                        string baseFolder = string.IsNullOrWhiteSpace(lessonsFolderInRepo)
                            ? string.Empty
                            : lessonsFolderInRepo.Trim('/');
                        fileOrPath = string.IsNullOrEmpty(baseFolder) ? $"{slug}.md" : $"{baseFolder}/{slug}.md";
                        if (enableDebugLogging)
                        {
                            Debug.Log(
                                $"CourseSettings: для урока '{title}' не задан путь — используем по умолчанию '{fileOrPath}'");
                        }
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
                return File.Exists(localCourseJsonPath)
                    ? await Task.Run(() => File.ReadAllText(localCourseJsonPath))
                    : null;
            }

            string url = repositoryBaseUrl.TrimEnd('/') + "/" + GetSelectedCourseJsonFile().TrimStart('/');
            const int maxAttempts = 3;
            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    HttpResponseMessage response = await GetHttpClient().GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.LogError(
                            $"CourseSettings: не удалось загрузить course.json — {response.StatusCode} {url}");
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
    }
}