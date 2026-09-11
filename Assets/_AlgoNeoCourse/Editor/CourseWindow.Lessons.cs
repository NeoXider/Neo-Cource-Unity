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
            if (CourseSettings.instance == null)
            {
                Debug.LogWarning("CourseWindow: CourseSettings недоступен — обновление списка уроков пропущено.");
                return;
            }

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
                    // Перечисляем все файлы и фильтруем регистронезависимо (.md/.MD для Linux).
                    foreach (string markdownFile in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                                 .Where(f => string.Equals(Path.GetExtension(f), ".md",
                                     StringComparison.OrdinalIgnoreCase)))
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
            // Порядок titles обязан совпадать с filteredList: префикс прогресса порядок не меняет.
            List<string> titles = filteredList.Select(l =>
            {
                int pct = GetLessonPercent(l.filePath, out int done, out int total);
                if (pct >= 100)
                {
                    return "✓ " + l.title;
                }

                return pct > 0 ? pct + "%  " + l.title : l.title;
            }).ToList();
            if (titles.Count == 0)
            {
                titles.Add("Нет загруженных уроков — скачайте их в CourseSettings");
            }

            CourseProgressStore.TryGetLastSession(out string savedPath, out int savedSlide);
            savedPath = NormalizeSessionPath(savedPath);
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

            if (lessonDropdown == null || mdRenderer == null || slideIndicator == null)
            {
                Debug.LogWarning("CourseWindow: UI окна ещё не готово — восстановление сессии отложено.");
                return;
            }

            lessonDropdown.choices = titles;
            lessonDropdown.index = titles.Count > 0 ? selectedIndex : -1;

            if (filteredList.Count > 0)
            {
                // Грузим сохранённую позицию молча: промежуточные ShowSlide(0)/сохранения
                // не должны затирать lastSlideIndex до конца восстановления.
                suppressSessionSave = true;
                try
                {
                    LoadLesson(filteredList[selectedIndex]);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        ShowSlide(Mathf.Clamp(savedSlide, 0, Math.Max(0, slides.Count - 1)));
                    }
                }
                finally
                {
                    suppressSessionSave = false;
                }

                SaveLastSession();
            }
            else
            {
                slides.Clear();
                currentSlideIndex = 0;
                slideIndicator.text = "—/—";
                mdRenderer.SetMarkdown("# Нет уроков\n\nСначала загрузите список и скачайте уроки в CourseSettings.");
            }

            // Обновляем общий прогресс курса и кнопку «Продолжить».
            UpdateCourseProgressUI();
            UpdateContinueButton();
        }

        // Процент прохождения урока: 50% слайды + 50% квизы (без квизов — 100% слайды).
        // completedQuizzes/totalQuizzes отдаём для тултипа дропдауна.
        public static int GetLessonPercent(string lessonFilePath, out int completedQuizzes, out int totalQuizzes)
        {
            completedQuizzes = 0;
            totalQuizzes = 0;
            try
            {
                if (string.IsNullOrEmpty(lessonFilePath) || !File.Exists(lessonFilePath))
                {
                    return 0;
                }

                string text = File.ReadAllText(lessonFilePath);
                int slidesTotal = SplitSlides(text).Count;

                List<QuizQuestion> questions;
                try
                {
                    questions = QuizParser.ParseQuestions(text) ?? new List<QuizQuestion>();
                }
                catch
                {
                    questions = new List<QuizQuestion>();
                }

                totalQuizzes = questions.Count;
                LessonQuizState state = QuizStateStore.GetLessonState(lessonFilePath, false);

                // Урок ни разу не открывали — прогресс слайдов равен нулю.
                float slidePart = 0f;
                if (state != null && slidesTotal > 0)
                {
                    int reached = Math.Min(state.maxSlideReached, slidesTotal - 1);
                    slidePart = (reached + 1) / (float)slidesTotal;
                }

                if (totalQuizzes == 0)
                {
                    return Mathf.Clamp(Mathf.RoundToInt(100f * slidePart), 0, 100);
                }

                if (state?.questionIdToState != null)
                {
                    foreach (QuizQuestion q in questions)
                    {
                        if (q != null && state.questionIdToState.TryGetValue(q.id, out QuizQuestionState qs) &&
                            qs != null && qs.isCompleted)
                        {
                            completedQuizzes++;
                        }
                    }
                }

                float quizPart = completedQuizzes / (float)totalQuizzes;
                return Mathf.Clamp(Mathf.RoundToInt(50f * slidePart + 50f * quizPart), 0, 100);
            }
            catch
            {
                completedQuizzes = 0;
                totalQuizzes = 0;
                return 0;
            }
        }

        private bool IsAlreadyOnSession(string lastPath, int lastSlide)
        {
            try
            {
                string currentNormalized = NormalizeSessionPath(currentLessonFilePath);
                return !string.IsNullOrEmpty(currentNormalized) && slides.Count > 0 &&
                       string.Equals(Path.GetFullPath(currentNormalized), Path.GetFullPath(lastPath),
                           StringComparison.OrdinalIgnoreCase) &&
                       currentSlideIndex == Mathf.Max(0, lastSlide);
            }
            catch
            {
                return false;
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

            // Регистронезависимый фильтр расширения (.md/.MD для Linux).
            return Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
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
            // Fence-aware нарезка: --- внутри ``` / ~~~ блоков — часть кода, а не граница слайда.
            List<string> slides = new();
            List<string> current = new();
            bool inFence = false;
            string fenceMarker = null;

            string[] lines = (md ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    string marker = trimmed.StartsWith("```") ? "```" : "~~~";
                    if (!inFence)
                    {
                        inFence = true;
                        fenceMarker = marker;
                    }
                    else if (string.Equals(marker, fenceMarker, StringComparison.Ordinal))
                    {
                        inFence = false;
                        fenceMarker = null;
                    }

                    current.Add(raw);
                    continue;
                }

                if (!inFence && Regex.IsMatch(trimmed, @"^\s*---\s*$"))
                {
                    slides.Add(string.Join("\n", current));
                    current.Clear();
                    continue;
                }

                current.Add(raw);
            }

            slides.Add(string.Join("\n", current));
            return slides.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private void SaveLastSession()
        {
            if (suppressSessionSave)
            {
                return;
            }

            CourseProgressStore.SaveLastSession(currentLessonFilePath, currentSlideIndex);
        }

        private static string NormalizeSessionPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').Trim();
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

            lastPath = NormalizeSessionPath(lastPath);
            if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath))
            {
                if (!string.IsNullOrEmpty(lastPath))
                {
                    Debug.LogWarning(
                        $"CourseWindow: сохранённый урок не найден на диске, открываю первый доступный: {lastPath}");
                }

                return;
            }

            // Уже на сохранённой позиции (повторный вызов после рекомпиляции) — ничего не делаем,
            // чтобы не дёргать LoadLesson/ShowSlide и не перезаписывать прогресс.
            if (IsAlreadyOnSession(lastPath, lastSlide))
            {
                return;
            }

            if (availableLessons == null || availableLessons.Count == 0)
            {
                try
                {
                    // RefreshLessonsList сам грузит сохранённую позицию и сохраняет её в конце.
                    RefreshLessonsList();
                }
                catch
                {
                }

                if (IsAlreadyOnSession(lastPath, lastSlide))
                {
                    return;
                }
            }

            (string title, string filePath, string id) found = availableLessons.FirstOrDefault(l =>
                string.Equals(Path.GetFullPath(l.filePath), Path.GetFullPath(lastPath),
                    StringComparison.OrdinalIgnoreCase));
            suppressSessionSave = true;
            try
            {
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
            finally
            {
                suppressSessionSave = false;
            }

            SaveLastSession();
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