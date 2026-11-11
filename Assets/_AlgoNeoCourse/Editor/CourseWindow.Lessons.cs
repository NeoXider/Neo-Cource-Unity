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
        // Публичный: дёргается из CourseSettings после скачивания уроков,
        // чтобы окно презентации обновлялось само, без ручной кнопки.
        public void RefreshLessonsList()
        {
            ClearEmptyState();
            if (CourseSettings.instance == null)
            {
                Debug.LogWarning("CourseWindow: CourseSettings недоступен — обновление списка уроков пропущено.");
                return;
            }

            CourseSettings settings = CourseSettings.instance;

            availableLessons.Clear();
            if (settings.lessonSelections != null)
            {
                // Галочка «selected» решает только, что скачивать. Урок, который уже лежит на диске,
                // показываем с его названием из курса в любом случае — иначе в списке остаётся имя файла.
                foreach (CourseSettings.LessonSelection selection in settings.lessonSelections.Where(s => s != null))
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

                        // Файла нет в списке курса (например, положен вручную): название берём
                        // из его первого заголовка и раскладываем по модулям по имени файла.
                        string fileId = Path.GetFileNameWithoutExtension(markdownFile);
                        string title = BuildFallbackLessonTitle(markdownFile, fileId);
                        if (availableLessons.Any(l => string.Equals(l.title, title, StringComparison.Ordinal)))
                        {
                            title += " (" + fileId + ")";
                        }

                        availableLessons.Add((title, markdownFile, fileId));
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
            // Названия — чистые, без префиксов: PopupField группирует по "/" в названии,
            // и любой префикс плодит дубли групп при смене процента. Прогресс — в баре и тултипе.
            List<string> titles = filteredList.Select(l => l.title).ToList();
            if (titles.Count == 0)
            {
                // Коротко и без обрезки: лимит дропдауна режет длинные строки.
                titles.Add("Нет загруженных уроков");
            }

            CourseProgressStore.TryGetLastSession(out string savedPath, out int savedSlide);
            savedPath = NormalizeSessionPath(savedPath);
            int selectedIndex = 0;
            // Сохранённый урок может отсутствовать в списке (сняли галочку, удалили файл, пример из Docs).
            bool savedLessonFound = false;
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
                            savedLessonFound = true;
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

            // Без ChangeEvent: урок ниже загружается явно, повторная загрузка из колбэка не нужна.
            lessonDropdown.choices = titles;
            lessonDropdown.SetValueWithoutNotify(titles[Mathf.Clamp(selectedIndex, 0, titles.Count - 1)]);

            if (filteredList.Count > 0)
            {
                // Грузим сохранённую позицию молча: промежуточные ShowSlide(0)/сохранения
                // не должны затирать lastSlideIndex до конца восстановления.
                suppressSessionSave = true;
                try
                {
                    bool loaded = LoadLesson(filteredList[selectedIndex]);
                    // Сохранённый слайд относится только к сохранённому уроку: если его нет в списке,
                    // открыт первый урок — и чужой номер слайда записался бы в его прогресс.
                    if (loaded && savedLessonFound)
                    {
                        ShowSlide(Mathf.Clamp(savedSlide, 0, Math.Max(0, slides.Count - 1)));
                    }
                    else if (!loaded && (slides == null || slides.Count == 0))
                    {
                        ShowSlide(0);
                    }
                }
                finally
                {
                    suppressSessionSave = false;
                }

                // Сохранённый урок вне списка, но файл на месте (пример из Docs) — позицию не затираем:
                // RestoreLastSession откроет его напрямую.
                if (savedLessonFound || string.IsNullOrEmpty(savedPath) || !File.Exists(savedPath))
                {
                    SaveLastSession();
                }
            }
            else
            {
                // Пустое состояние: одна заглушка (без дублирующего текста слайда) и выключенная навигация.
                slides.Clear();
                currentSlideIndex = 0;
                slideIndicator.text = "—/—";
                prevBtn?.SetEnabled(false);
                nextBtn?.SetEnabled(false);
                mdRenderer.SetMarkdown(string.Empty);
                ShowEmptyState();
            }

            // Обновляем общий прогресс курса и кнопку «Продолжить».
            UpdateCourseProgressUI();
            UpdateContinueButton();
        }

        // Процент прохождения урока: среднее по present-частям (слайды + квизы + проверки).
        // completedQuizzes/totalQuizzes отдаём для тултипа дропдауна.
        public static int GetLessonPercent(string lessonFilePath, out int completedQuizzes, out int totalQuizzes)
        {
            return GetLessonPercent(lessonFilePath, out completedQuizzes, out totalQuizzes,
                out _, out _);
        }

        public static int GetLessonPercent(string lessonFilePath, out int completedQuizzes, out int totalQuizzes,
            out int completedChecks, out int totalChecks)
        {
            completedQuizzes = 0;
            totalQuizzes = 0;
            completedChecks = 0;
            totalChecks = 0;
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

                // Пройденных слайдов: урок ни разу не открывали — ноль.
                int slidesDone = 0;
                if (state != null && slidesTotal > 0)
                {
                    slidesDone = Math.Min(state.maxSlideReached, slidesTotal - 1) + 1;
                }

                if (totalQuizzes == 0 && slidesTotal == 0)
                {
                    return 0;
                }

                if (totalQuizzes > 0 && state?.questionIdToState != null)
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

                HashSet<string> checkIds = CollectCheckIds(text);
                totalChecks = checkIds.Count;
                if (totalChecks > 0 && state?.checkIdToPassed != null)
                {
                    foreach (string id in checkIds)
                    {
                        if (state.checkIdToPassed.TryGetValue(id, out bool ok) && ok)
                        {
                            completedChecks++;
                        }
                    }
                }

                // Доля от ВСЕХ элементов урока (слайды + квизы + проверки):
                // 25/27 даёт почти полный бар, а не 67% как при среднем частей.
                int totalItems = slidesTotal + totalQuizzes + totalChecks;
                if (totalItems <= 0)
                {
                    return 0;
                }

                int doneItems = slidesDone + completedQuizzes + completedChecks;
                return Mathf.Clamp(Mathf.RoundToInt(100f * doneItems / totalItems), 0, 100);
            }
            catch
            {
                completedQuizzes = 0;
                totalQuizzes = 0;
                completedChecks = 0;
                totalChecks = 0;
                return 0;
            }
        }

        // Все id практических проверок урока: ```check-блоки (кнопки инжектятся всегда,
        // см. InjectCheckBlocksIfDebug). unity://check-ссылки авторского написания удалены
        // в 1.6.1 и здесь не учитываются.
        private static HashSet<string> CollectCheckIds(string lessonText)
        {
            HashSet<string> ids = new(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(lessonText))
            {
                return ids;
            }

            try
            {
                // Идём по слайдам, как ShowSlide: SplitSlides убирает \r (иначе в CRLF-файлах id получали
                // "\r" и никогда не совпадали с id кнопок), а регулярка и ссылка — те же, что у кнопки.
                foreach (string slide in SplitSlides(lessonText))
                {
                    foreach (Match m in CheckBlockRegex.Matches(slide))
                    {
                        try
                        {
                            ids.Add(QuizStateStore.CheckIdForLink(BuildCheckLink(m.Groups[1].Value)));
                        }
                        catch
                        {
                            // Блок, который не кодируется в ссылку, кнопки тоже не получит.
                        }
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        // Дальняя точка прогресса: движется только вперёд по порядку уроков.
        // Откат на пару слайдов назад и обзор других уроков её не сдвигают,
        // поэтому «Продолжить» всегда ведёт к максимуму, а не к последнему автосейву.
        // Только in-memory: на диск запишет ближайший SaveLastSession/SaveToDisk.
        private void UpdateFarthestSession()
        {
            try
            {
                if (string.IsNullOrEmpty(currentLessonFilePath) || slides == null || slides.Count == 0)
                {
                    return;
                }

                // Пути — в том же виде, что и ключи прогресса (относительно проекта): абсолютный путь
                // примера из Docs и относительный путь урока сравниваются одинаково, а сохранение переносимо.
                CourseProgressData data = CourseProgressStore.GetData();
                string current = CourseProgressStore.NormalizeLessonPathKey(currentLessonFilePath);
                string far = CourseProgressStore.NormalizeLessonPathKey(data.farthestLessonPath);

                bool advance = false;
                if (string.IsNullOrEmpty(far))
                {
                    advance = true;
                }
                else
                {
                    int curIdx = IndexInAvailableLessons(current);
                    int farIdx = IndexInAvailableLessons(far);
                    if (curIdx >= 0 && farIdx >= 0)
                    {
                        advance = curIdx > farIdx ||
                                  (curIdx == farIdx && currentSlideIndex > data.farthestSlideIndex);
                    }
                    else if (string.Equals(current, far, StringComparison.OrdinalIgnoreCase))
                    {
                        advance = currentSlideIndex > data.farthestSlideIndex;
                    }
                    // farthest вне текущего списка — не трогаем.
                }

                if (advance)
                {
                    data.farthestLessonPath = current;
                    data.farthestSlideIndex = currentSlideIndex;
                }
            }
            catch
            {
            }
        }

        private int IndexInAvailableLessons(string normalizedPath)
        {
            try
            {
                string full = Path.GetFullPath(normalizedPath);
                for (int i = 0; i < availableLessons.Count; i++)
                {
                    if (string.Equals(Path.GetFullPath(availableLessons[i].filePath), full,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            catch
            {
            }

            return -1;
        }

        private bool IsAlreadyOnSession(string lastPath, int lastSlide)
        {
            try
            {
                // Сохранённый слайд зажимаем в границы урока так же, как это делает GoToSession:
                // урок могли укоротить, и «слайд 30 из 20» — это последний слайд.
                string currentNormalized = NormalizeSessionPath(currentLessonFilePath);
                return !string.IsNullOrEmpty(currentNormalized) && slides.Count > 0 &&
                       string.Equals(Path.GetFullPath(currentNormalized), Path.GetFullPath(lastPath),
                           StringComparison.OrdinalIgnoreCase) &&
                       currentSlideIndex == Mathf.Clamp(lastSlide, 0, slides.Count - 1);
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

        // Возвращает false, если файл урока не прочитался: тогда состояние окна остаётся прежним целиком.
        private bool LoadLesson((string title, string filePath, string id) lesson)
        {
            if (mdRenderer == null)
            {
                BuildContent();
            }

            // Сначала читаем в локальные переменные. Иначе при ошибке чтения окно «называлось» бы
            // новым уроком, показывая старый, и позиция старого сохранилась бы под новым.
            List<string> loadedSlides;
            try
            {
                loadedSlides = SplitSlides(File.ReadAllText(lesson.filePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"CourseWindow: не удалось открыть урок «{lesson.title}» ({lesson.filePath}) — {ex.Message}");
                SyncLessonDropdown();
                return false;
            }

            ClearEmptyState();
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
            slides = loadedSlides;

            SeedMarkdownContext(currentLessonFilePath);
            // «Продолжить» и восстановление сессии меняют урок без участия дропдауна — показываем его там.
            SyncLessonDropdown();
            ShowSlide(0);
            SaveLastSession();
            return true;
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

            GoToSession(lastPath, lastSlide);
        }

        // Кнопка «▶ Продолжить»: ведёт к дальней достигнутой точке, а не к последнему
        // автосейву (автосейв едет и за откатом назад, farthest — только вперёд).
        private void GoToFarthestSession()
        {
            if (mdRenderer == null)
            {
                BuildContent();
            }

            if (CourseProgressStore.TryGetFarthestSession(out string farPath, out int farSlide) &&
                !string.IsNullOrEmpty(NormalizeSessionPath(farPath)))
            {
                farPath = NormalizeSessionPath(farPath);
                if (File.Exists(farPath) && !IsAlreadyOnSession(farPath, farSlide))
                {
                    GoToSession(farPath, farSlide);
                    return;
                }

                if (IsAlreadyOnSession(farPath, farSlide))
                {
                    return;
                }
            }

            RestoreLastSession();
        }

        private void GoToSession(string lastPath, int lastSlide)
        {
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
                    // Урока нет в списке (например, пример из Docs) — открываем файл напрямую.
                    // Как и в LoadLesson: сначала читаем, состояние меняем только при успехе.
                    List<string> loadedSlides;
                    try
                    {
                        loadedSlides = SplitSlides(File.ReadAllText(lastPath));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"CourseWindow: не удалось открыть урок {lastPath} — {ex.Message}");
                        return;
                    }

                    ClearEmptyState();
                    currentLessonTitle = Path.GetFileNameWithoutExtension(lastPath);
                    currentLessonFilePath = lastPath;
                    slides = loadedSlides;
                    SeedMarkdownContext(lastPath);
                    SyncLessonDropdown();
                    ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
                }
                else if (LoadLesson(found))
                {
                    ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
                }
                else
                {
                    // Урок не прочитался — остаёмся где были, сохранённую позицию не трогаем.
                    return;
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

        // Название для файла, которого нет в списке курса: «l2 / m5 / <первый заголовок урока>».
        // Имя вида l2m5y1-y1 даёт группы для подменю; без него остаётся просто заголовок или имя файла.
        internal static string BuildFallbackLessonTitle(string markdownFile, string fileId)
        {
            string heading = null;
            try
            {
                foreach (string line in File.ReadLines(markdownFile))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                    {
                        heading = trimmed.Substring(2).Trim();
                        break;
                    }

                    if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        // Урок без заголовка первой строкой — дальше не ищем.
                        break;
                    }
                }
            }
            catch
            {
                // Файл занят или недоступен — обойдёмся именем.
            }

            if (!string.IsNullOrEmpty(heading))
            {
                // «Модуль 5, Урок 1: Арбалет…» -> «Арбалет…»; «/» в названии создал бы лишнее подменю.
                int colon = heading.IndexOf(": ", StringComparison.Ordinal);
                if (colon >= 0 && colon + 2 < heading.Length)
                {
                    heading = heading.Substring(colon + 2);
                }

                heading = heading.Replace("/", "·").Trim();
            }

            string name = string.IsNullOrEmpty(heading) ? fileId : heading;
            Match match = Regex.Match(fileId ?? string.Empty, @"^l(\d+)m(\d+)y(\d+)", RegexOptions.IgnoreCase);
            return match.Success
                ? $"l{match.Groups[1].Value} / m{match.Groups[2].Value} / {name}"
                : name;
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