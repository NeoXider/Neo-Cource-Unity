using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NeoCource.Editor.Progress;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        private void RefreshLessonsList()
        {
            CourseSettings settings = CourseSettings.instance;

            availableLessons.Clear();
            if (settings.lessonSelections != null)
            {
                foreach (CourseSettings.LessonSelection selection in settings.lessonSelections.Where(s => s.selected))
                {
                    string path = ResolveLocalLessonPath(settings, selection.id, selection.file);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        availableLessons.Add((selection.title, path, selection.id));
                    }
                }
            }

            try
            {
                string folder = settings.GetDownloadFolderPath();
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    HashSet<string> knownPaths = new(availableLessons.Select(l => Path.GetFullPath(l.filePath)),
                        StringComparer.OrdinalIgnoreCase);
                    foreach (string markdownFile in Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly))
                    {
                        string full = Path.GetFullPath(markdownFile);
                        if (knownPaths.Contains(full))
                        {
                            continue;
                        }

                        string title = Path.GetFileNameWithoutExtension(markdownFile);
                        availableLessons.Add((title, markdownFile, title));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseWindow: сканирование папки загрузок завершилось с ошибкой — {ex.Message}");
            }

            availableLessons.Sort((a, b) =>
            {
                (int module, int lesson) left = ExtractModuleLessonKey(a.id, a.filePath, a.title);
                (int module, int lesson) right = ExtractModuleLessonKey(b.id, b.filePath, b.title);
                int compare = left.module.CompareTo(right.module);
                if (compare != 0)
                {
                    return compare;
                }

                compare = left.lesson.CompareTo(right.lesson);
                if (compare != 0)
                {
                    return compare;
                }

                return string.Compare(a.title, b.title, StringComparison.CurrentCultureIgnoreCase);
            });

            List<(string title, string filePath, string id)> filteredList = availableLessons.ToList();
            List<string> titles = filteredList.Select(l => l.title).ToList();
            if (titles.Count == 0)
            {
                titles.Add("Нет загруженных уроков — скачайте их в CourseSettings");
            }

            CourseProgressStore.TryGetLastSession(out string savedPath, out int savedSlide);
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(savedPath))
            {
                try
                {
                    string fullSaved = Path.GetFullPath(savedPath);
                    for (int i = 0; i < filteredList.Count; i++)
                    {
                        if (string.Equals(Path.GetFullPath(filteredList[i].filePath), fullSaved,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                catch
                {
                }
            }

            lessonDropdown.choices = titles;
            lessonDropdown.index = titles.Count > 0 ? selectedIndex : -1;

            if (filteredList.Count > 0)
            {
                LoadLesson(filteredList[selectedIndex]);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    ShowSlide(Mathf.Clamp(savedSlide, 0, Math.Max(0, slides.Count - 1)));
                }
            }
            else
            {
                slides.Clear();
                currentSlideIndex = 0;
                slideIndicator.text = "—/—";
                mdRenderer.SetMarkdown("# Нет уроков\n\nСначала загрузите список и скачайте уроки в CourseSettings.");
            }
        }

        private static string ResolveLocalLessonPath(CourseSettings settings, string id, string remotePath)
        {
            string folder = settings.GetDownloadFolderPath();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(remotePath))
            {
                string fileName = Path.GetFileName(remotePath);
                string preferred = Path.Combine(folder, $"{id}-{fileName}");
                if (File.Exists(preferred))
                {
                    return preferred;
                }

                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                string alternative = Path.Combine(folder, $"{nameNoExt}-{id}{ext}");
                if (File.Exists(alternative))
                {
                    return alternative;
                }

                string any = Directory.GetFiles(folder, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (!string.IsNullOrEmpty(any))
                {
                    return any;
                }
            }

            return Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path =>
                {
                    string fileName = Path.GetFileName(path);
                    return fileName.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase)
                           || fileName.EndsWith("-" + id + Path.GetExtension(fileName),
                               StringComparison.OrdinalIgnoreCase)
                           || fileName.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0;
                });
        }

        private void LoadLesson((string title, string filePath, string id) lesson)
        {
            if (mdRenderer == null)
            {
                BuildContent();
            }

            try
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath))
                {
                    QuizStateStore.SaveLessonState(currentLessonFilePath);
                }
            }
            catch
            {
            }

            currentLessonTitle = lesson.title;
            currentLessonFilePath = lesson.filePath;
            currentSlideIndex = 0;

            string text = File.ReadAllText(currentLessonFilePath);
            slides = SplitSlides(text);
            SeedMarkdownContext(currentLessonFilePath);
            ShowSlide(0);
            SaveLastSession();
        }

        private static List<string> SplitSlides(string md)
        {
            List<string> parts = Regex.Split(md.Replace("\r\n", "\n"), @"^\n?\s*---\s*$", RegexOptions.Multiline)
                .ToList();
            return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private void SaveLastSession()
        {
            CourseProgressStore.SaveLastSession(currentLessonFilePath, currentSlideIndex);
        }

        private void RestoreLastSession()
        {
            if (mdRenderer == null)
            {
                BuildContent();
            }

            if (!CourseProgressStore.TryGetLastSession(out string lastPath, out int lastSlide))
            {
                return;
            }

            if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath))
            {
                return;
            }

            if (availableLessons == null || availableLessons.Count == 0)
            {
                try
                {
                    RefreshLessonsList();
                }
                catch
                {
                }
            }

            (string title, string filePath, string id) found = availableLessons.FirstOrDefault(l =>
                string.Equals(Path.GetFullPath(l.filePath), Path.GetFullPath(lastPath),
                    StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(found.filePath))
            {
                try
                {
                    currentLessonTitle = Path.GetFileNameWithoutExtension(lastPath);
                    currentLessonFilePath = lastPath;
                    slides = SplitSlides(File.ReadAllText(lastPath));
                    SeedMarkdownContext(lastPath);
                    ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
                }
                catch
                {
                }
            }
            else
            {
                LoadLesson(found);
                ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
            }
        }

        private void ResetProgressAndReload()
        {
            if (!EditorUtility.DisplayDialog("AlgoNeoCourse",
                    "Сбросить локальный прогресс курса и сохранения всех квизов?", "Сбросить", "Отмена"))
            {
                return;
            }

            QuizSettings.instance.ClearState();
            currentLessonFilePath = null;
            currentLessonTitle = null;
            currentSlideIndex = 0;
            slides.Clear();
            RefreshLessonsList();
        }

        private static (int module, int lesson) ExtractModuleLessonKey(string id, string path, string title)
        {
            string source = id ?? string.Empty;
            if (string.IsNullOrEmpty(source))
            {
                source = Path.GetFileNameWithoutExtension(path ?? string.Empty) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(source))
            {
                source = title ?? string.Empty;
            }

            Match match = Regex.Match(source, @"m(\d+)[^\d]*y(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out int module);
                int.TryParse(match.Groups[2].Value, out int lesson);
                return (module, lesson);
            }

            MatchCollection numbers = Regex.Matches(source, @"\d+");
            if (numbers.Count >= 2)
            {
                int.TryParse(numbers[0].Value, out int module);
                int.TryParse(numbers[1].Value, out int lesson);
                return (module, lesson);
            }

            if (numbers.Count == 1)
            {
                int.TryParse(numbers[0].Value, out int only);
                return (only, 0);
            }

            return (int.MaxValue, int.MaxValue);
        }
    }
}