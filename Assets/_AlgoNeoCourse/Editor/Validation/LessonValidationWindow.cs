using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NeoCource.Editor.Validation
{
    // Окно проверки уроков: сканирует .md из папки загрузок и выбранных
    // lessonSelections, показывает проблемы списком. CourseWindow НЕ трогаем.
    public class LessonValidationWindow : EditorWindow
    {
        private Label m_SummaryLabel;
        private ScrollView m_IssuesView;

        [MenuItem("Tools/AlgoNeoCourse/Validate Lessons")]
        public static void Open()
        {
            LessonValidationWindow win = GetWindow<LessonValidationWindow>("Валидация уроков");
            win.Refresh();
            win.Show();
        }

        private void CreateGUI()
        {
            Button refreshButton = new(Refresh) { text = "Refresh" };
            rootVisualElement.Add(refreshButton);

            m_SummaryLabel = new Label();
            rootVisualElement.Add(m_SummaryLabel);

            m_IssuesView = new ScrollView();
            m_IssuesView.style.flexGrow = 1;
            rootVisualElement.Add(m_IssuesView);

            Refresh();
        }

        private void Refresh()
        {
            if (m_SummaryLabel == null || m_IssuesView == null)
            {
                return;
            }

            m_IssuesView.Clear();

            List<(string title, string filePath)> lessons = CollectLessons();
            if (lessons.Count == 0)
            {
                m_SummaryLabel.text = "нет уроков";
                m_IssuesView.Add(new Label("нет уроков"));
                return;
            }

            List<ValidationIssue> allIssues = new();
            foreach ((string title, string filePath) lesson in lessons)
            {
                try
                {
                    allIssues.AddRange(
                        LessonValidator.ValidateLessonFile(lesson.filePath, lesson.title));
                }
                catch (Exception ex)
                {
                    // Валидатор не бросает, но перестраховываемся.
                    Debug.LogWarning($"LessonValidationWindow: {lesson.filePath} — {ex.Message}");
                }
            }

            int errors = allIssues.Count(i => i.severity == "error");
            int warnings = allIssues.Count(i => i.severity == "warning");
            m_SummaryLabel.text =
                $"Уроков: {lessons.Count}. Проблем: {allIssues.Count} (ошибок: {errors}, предупреждений: {warnings}).";

            if (allIssues.Count == 0)
            {
                m_IssuesView.Add(new Label("Проблем не найдено."));
                return;
            }

            foreach (ValidationIssue issue in allIssues)
            {
                Label row = new(
                    $"[{issue.severity}] {issue.lessonTitle} · слайд {issue.slideIndex} · {issue.rule}: {issue.message}");
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.color = issue.severity == "error"
                    ? new Color(1f, 0.45f, 0.45f)
                    : new Color(1f, 0.85f, 0.4f);
                // Клик: выделить ассет урока и показать в Finder/Explorer.
                string capturedPath = issue.lessonPath;
                row.AddManipulator(new Clickable(() => RevealLesson(capturedPath)));
                m_IssuesView.Add(row);
            }
        }

        private static void RevealLesson(string lessonFilePath)
        {
            try
            {
                string fullPath = Path.GetFullPath(lessonFilePath);
                string assetPath = ToAssetPath(fullPath);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                    }
                }

                EditorUtility.RevealInFinder(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LessonValidationWindow: не удалось показать урок — " + ex.Message);
            }
        }

        private static string ToAssetPath(string fullPath)
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string normalizedRoot = root.Replace('\\', '/').TrimEnd('/') + "/";
                string normalized = fullPath.Replace('\\', '/');
                if (normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(normalizedRoot.Length);
                }
            }
            catch
            {
                /* не конвертируем */
            }

            return null;
        }

        // Сканирование как в CourseWindow.RefreshLessonsList (копия логики,
        // сам CourseWindow не трогаем): выбранные lessonSelections + все *.md
        // из папки загрузок.
        private static List<(string title, string filePath)> CollectLessons()
        {
            Dictionary<string, (string title, string filePath)> byPath =
                new(StringComparer.OrdinalIgnoreCase);

            CourseSettings settings = null;
            try
            {
                settings = CourseSettings.instance;
            }
            catch
            {
                return new List<(string title, string filePath)>();
            }

            if (settings == null)
            {
                return new List<(string title, string filePath)>();
            }

            string folderAbs = null;
            try
            {
                string folder = settings.GetDownloadFolderPath();
                folderAbs = string.IsNullOrWhiteSpace(folder)
                    ? null
                    : AlgoNeoPackageAssetLocator.ToAbsolutePath(folder);
            }
            catch
            {
                folderAbs = null;
            }

            try
            {
                if (settings.lessonSelections != null && folderAbs != null)
                {
                    foreach (CourseSettings.LessonSelection selection in settings.lessonSelections.Where(
                                 s => s != null && s.selected))
                    {
                        string found = ResolveLocalLessonPath(folderAbs, selection.id, selection.file);
                        if (!string.IsNullOrEmpty(found) && File.Exists(found))
                        {
                            string full = Path.GetFullPath(found);
                            byPath[full] = (selection.title ?? Path.GetFileNameWithoutExtension(full), full);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LessonValidationWindow: чтение lessonSelections — " + ex.Message);
            }

            try
            {
                if (!string.IsNullOrEmpty(folderAbs) && Directory.Exists(folderAbs))
                {
                    foreach (string md in Directory.GetFiles(folderAbs, "*", SearchOption.TopDirectoryOnly)
                                 .Where(f => string.Equals(Path.GetExtension(f), ".md",
                                     StringComparison.OrdinalIgnoreCase)))
                    {
                        string full = Path.GetFullPath(md);
                        if (!byPath.ContainsKey(full))
                        {
                            byPath[full] = (Path.GetFileNameWithoutExtension(full), full);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LessonValidationWindow: сканирование папки — " + ex.Message);
            }

            return byPath.Values.OrderBy(l => l.title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static string ResolveLocalLessonPath(string folderAbs, string id, string remotePath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderAbs) || !Directory.Exists(folderAbs))
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(remotePath))
                {
                    string fileName = Path.GetFileName(remotePath);
                    string preferred = Path.Combine(folderAbs, $"{id}-{fileName}");
                    if (File.Exists(preferred))
                    {
                        return preferred;
                    }

                    string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    string alternative = Path.Combine(folderAbs, $"{nameNoExt}-{id}{ext}");
                    if (File.Exists(alternative))
                    {
                        return alternative;
                    }

                    string any = Directory.GetFiles(folderAbs, fileName, SearchOption.TopDirectoryOnly)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(any))
                    {
                        return any;
                    }
                }

                return Directory.GetFiles(folderAbs, "*.md", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path =>
                    {
                        string fileName = Path.GetFileName(path);
                        return fileName.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase) ||
                               fileName.EndsWith("-" + id + Path.GetExtension(path),
                                   StringComparison.OrdinalIgnoreCase) ||
                               fileName.IndexOf(id ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
            }
            catch
            {
                return null;
            }
        }
    }
}
