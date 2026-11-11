using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using NeoCource.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        private void DoRefreshLessonsAndCurrent()
        {
            RefreshLessonsList();
            if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
            {
                try
                {
                    string text = File.ReadAllText(currentLessonFilePath);
                    slides = SplitSlides(text);
                    ShowSlide(currentSlideIndex);
                    SaveLastSession();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"CourseWindow: не удалось перечитать текущий урок — {ex.Message}");
                }
            }

            EnsureDocsMenuFromSettings();
        }

        private void EnsureDocsMenuFromSettings()
        {
            if (docsMenu != null)
            {
                toolbar.Remove(docsMenu);
                docsMenu = null;
            }

            if (ValidationSettings.Instance.DebugBrowseDocsExamples)
            {
                docsMenu = new ToolbarMenu { text = "Docs" };
                docsMenu.AddToClassList("algo-course-docs-menu");
                docsMenu.tooltip = "Примеры из Docs/Examples";
                PopulateDocsMenu();
                toolbar.Add(docsMenu);
            }
        }

        private void PopulateDocsMenu()
        {
            docsMenu.menu.ClearItems();
            string root =
                AlgoNeoPackageAssetLocator.ToAbsolutePath(AlgoNeoPackageAssetLocator.DocsExamplesFolderAssetPath);
            if (!Directory.Exists(root))
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            // Регистронезависимый фильтр расширения (.md/.MD для Linux).
            string[] files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(f => string.Equals(Path.GetExtension(f), ".md", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (files.Length == 0)
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            foreach (string path in files)
            {
                string filePath = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(filePath);
                docsMenu.menu.AppendAction(name, _ => LoadDocsExample(filePath));
            }
        }

        private void LoadDocsExample(string assetPath)
        {
            try
            {
                if (mdRenderer == null)
                {
                    BuildContent();
                }

                // Сначала читаем: при ошибке чтения состояние окна остаётся прежним.
                List<string> loadedSlides = SplitSlides(File.ReadAllText(assetPath));
                currentLessonTitle = Path.GetFileNameWithoutExtension(assetPath);
                currentLessonFilePath = assetPath;
                currentSlideIndex = 0;
                slides = loadedSlides;
                SeedMarkdownContext(assetPath);
                SyncLessonDropdown();
                ShowSlide(0);
                ClearEmptyState();
            }
            catch (Exception ex)
            {
                Debug.LogError($"CourseWindow: не удалось открыть пример {assetPath}: {ex.Message}");
            }
        }

        // Цвета текста слайдов, которые до 1.8.0 были зашиты во встроенную копию рендерера.
        // Теперь копия совпадает с апстримом (там жирный без цвета, встроенный код — жирный циановый),
        // а прежний вид окна курса задаёт сам плагин.
        internal const string BoldColorHex = "#FF9F1C";
        internal const string InlineCodeColorHex = "#B388FF";

        internal void BuildContent()
        {
            contentRoot = new ScrollView(ScrollViewMode.Vertical);
            contentRoot.style.flexGrow = 1f;
            contentRoot.AddToClassList("algo-course-content");
            // Содержимое подстраивается под ширину окна: горизонтальной прокрутки нет никогда.
            contentRoot.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            // Въезд слайда анимируем на самой карточке: сдвиг внутри ScrollView раздувал бы его границы.
            contentRoot.AddToClassList("algo-slide");

            mdRenderer = CreateSlideRenderer();
            if (mdRenderer.RootElement is ScrollView slideScroll)
            {
                slideScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }

            contentRoot.Add(mdRenderer.RootElement);
            rootVisualElement.Add(contentRoot);
        }

        private UIMarkdownRenderer.UIMarkdownRenderer CreateSlideRenderer()
        {
            // Ассеты рендерера передаём явно: сам он ищет их от корня своего пакета, а встроенная копия
            // лежит внутри плагина (Assets/_AlgoNeoCourse или Packages/com.neoxider.algoneocourse).
            // Не нашли — передаём null, и рендерер ищет сам. Вложенный ScrollView не нужен:
            // слайд прокручивает только contentRoot.
            UIMarkdownRenderer.UIMarkdownRenderer renderer = new(
                (link, clickedRenderer) => OnLinkClicked(link, clickedRenderer),
                includeScrollview: false,
                defaultStylesheet: LoadPluginAsset<StyleSheet>(AlgoNeoPackageAssetLocator.MarkdownStylesheetAssetPath),
                videoPlayerStyleSheet: LoadPluginAsset<StyleSheet>(
                    AlgoNeoPackageAssetLocator.VideoPlayerStylesheetAssetPath),
                videoPlayerElementPrefab: LoadPluginAsset<VisualTreeAsset>(
                    AlgoNeoPackageAssetLocator.VideoPlayerUxmlAssetPath));

            if (ColorUtility.TryParseHtmlString(InlineCodeColorHex, out Color inlineCodeColor))
            {
                renderer.InlineCodeColor = inlineCodeColor;
            }

            // Стили квизов, кнопки «Проверить» и карточки задания — после стиля рендерера:
            // при равной специфичности побеждает таблица, добавленная позже.
            StyleSheet quizStyleSheet = LoadPluginAsset<StyleSheet>(AlgoNeoPackageAssetLocator.QuizStylesheetAssetPath);
            if (quizStyleSheet != null)
            {
                renderer.RootElement.styleSheets.Add(quizStyleSheet);
            }

            return renderer;
        }

        private static T LoadPluginAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            try
            {
                return AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
            catch
            {
                return null;
            }
        }

        private void ShowEmptyState()
        {
            ClearEmptyState();
            if (contentRoot == null)
            {
                return;
            }

            try
            {
                emptyStateBox = new VisualElement();
                emptyStateBox.AddToClassList("algo-course-empty-box");

                Label title = new("Нет загруженных уроков");
                title.AddToClassList("algo-course-empty-title");
                emptyStateBox.Add(title);

                Label desc = new("Загрузите список и скачайте уроки в Course Settings — окно подхватит их автоматически.");
                desc.AddToClassList("algo-course-empty-desc");
                emptyStateBox.Add(desc);

                ToolbarButton openSettings = new(() =>
                {
                    try
                    {
                        Selection.activeObject = CourseSettings.instance;
                    }
                    catch
                    {
                    }
                })
                { text = "Открыть Course Settings" };
                openSettings.AddToClassList("algo-course-empty-button");
                emptyStateBox.Add(openSettings);

                contentRoot.Add(emptyStateBox);
            }
            catch
            {
            }
        }

        private void ClearEmptyState()
        {
            try
            {
                if (emptyStateBox != null)
                {
                    emptyStateBox.RemoveFromHierarchy();
                    emptyStateBox = null;
                }
            }
            catch
            {
            }
        }

        private void EnsureRichTextOnAllLabels()
        {
            if (mdRenderer?.RootElement == null)
            {
                return;
            }

            try
            {
                foreach (Label label in mdRenderer.RootElement.Query<Label>().ToList())
                {
                    if (label != null)
                    {
                        label.enableRichText = true;
                    }
                }
            }
            catch
            {
            }
        }

        // Прежний вид текста слайда: жирный — оранжевый, встроенный код — без жирности.
        // Вызывается один раз сразу после SetMarkdown, пока в дереве только метки рендерера.
        private void ApplyLegacyTextColors()
        {
            if (mdRenderer?.RootElement == null)
            {
                return;
            }

            try
            {
                string inlineCodeOpenTag = "<color=#" + ColorUtility.ToHtmlStringRGB(mdRenderer.InlineCodeColor) + ">";
                foreach (Label label in mdRenderer.RootElement.Query<Label>().ToList())
                {
                    string text = label?.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    string restyled = RestyleBoldTags(text, inlineCodeOpenTag);
                    if (!ReferenceEquals(restyled, text))
                    {
                        label.text = restyled;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseWindow: цвета текста слайда не применены — " + ex.Message);
            }
        }

        // Рендерер пишет жирный как <b>…</b>, встроенный код — как <color=#код><b>…</b></color>.
        // Жирный оборачиваем в оранжевый цвет, у встроенного кода снимаем <b> (цвет остаётся).
        // Повторный вызов текст не меняет: уже окрашенный <b> узнаётся по тегу цвета перед ним.
        internal static string RestyleBoldTags(string text, string inlineCodeOpenTag)
        {
            const string open = "<b>";
            const string close = "</b>";
            const string boldOpenTag = "<color=" + BoldColorHex + ">";

            if (string.IsNullOrEmpty(text) || text.IndexOf(open, StringComparison.Ordinal) < 0)
            {
                return text;
            }

            StringBuilder result = new(text.Length + 48);
            // 0 — оставить как есть, 1 — жирный текст (закрыть цвет), 2 — встроенный код (тег снят).
            Stack<int> openTags = new();
            int i = 0;
            while (i < text.Length)
            {
                if (string.CompareOrdinal(text, i, open, 0, open.Length) == 0)
                {
                    if (!string.IsNullOrEmpty(inlineCodeOpenTag) && EndsWith(result, inlineCodeOpenTag))
                    {
                        openTags.Push(2);
                    }
                    else if (EndsWith(result, boldOpenTag))
                    {
                        openTags.Push(0);
                        result.Append(open);
                    }
                    else
                    {
                        openTags.Push(1);
                        result.Append(boldOpenTag).Append(open);
                    }

                    i += open.Length;
                    continue;
                }

                if (string.CompareOrdinal(text, i, close, 0, close.Length) == 0)
                {
                    int kind = openTags.Count > 0 ? openTags.Pop() : 0;
                    if (kind == 1)
                    {
                        result.Append(close).Append("</color>");
                    }
                    else if (kind == 0)
                    {
                        result.Append(close);
                    }

                    i += close.Length;
                    continue;
                }

                result.Append(text[i]);
                i++;
            }

            return result.ToString();
        }

        private static bool EndsWith(StringBuilder builder, string suffix)
        {
            if (builder.Length < suffix.Length)
            {
                return false;
            }

            int offset = builder.Length - suffix.Length;
            for (int i = 0; i < suffix.Length; i++)
            {
                if (builder[offset + i] != suffix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void FixBrokenMarkdownLinks()
        {
            if (mdRenderer?.RootElement == null)
            {
                return;
            }

            try
            {
                foreach (Label label in mdRenderer.RootElement.Query<Label>().ToList())
                {
                    if (label == null)
                    {
                        continue;
                    }

                    string text = label.text ?? string.Empty;
                    if (text.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string link = null;
                    Match match = Regex.Match(text, @"<link=(unity://check[^>]+)>");
                    if (match.Success)
                    {
                        link = match.Groups[1].Value;
                    }

                    if (string.IsNullOrEmpty(link))
                    {
                        int start = text.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase);
                        int end = start;
                        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '>' &&
                               text[end] != '"')
                        {
                            end++;
                        }

                        link = text.Substring(start, end - start);
                    }

                    if (label.parent == null)
                    {
                        continue;
                    }

                    VisualElement parent = label.parent;
                    int index = parent.IndexOf(label);
                    ToolbarButton button = new() { text = "Проверить" };
                    button.clicked += () =>
                    {
                        try
                        {
                            OnLinkClicked(link, mdRenderer, button);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex.Message);
                        }
                    };
                    button.AddToClassList("check-button");
                    button.userData = link;
                    parent.Insert(index, button);
                    parent.Remove(label);
                }
            }
            catch
            {
            }
        }

        // Блок check показываем не сырым YAML, а карточкой задания с пунктами и кнопкой.
        private void DecorateCheckBlocks(string slideMarkdown)
        {
            if (mdRenderer?.RootElement == null || string.IsNullOrEmpty(slideMarkdown))
            {
                return;
            }

            try
            {
                // Та же регулярка, что ставит кнопки (InjectCheckBlocksIfDebug) и считает проверки урока.
                MatchCollection raws = CheckBlockRegex.Matches(slideMarkdown);
                if (raws.Count == 0)
                {
                    return;
                }

                List<VisualElement> blocks =
                    mdRenderer.RootElement.Query<VisualElement>(className: "language-check").ToList();
                List<Button> buttons = mdRenderer.RootElement.Query<Button>(className: "check-button").ToList();
                LessonQuizState state = string.IsNullOrEmpty(currentLessonFilePath)
                    ? null
                    : QuizStateStore.GetLessonState(currentLessonFilePath, false);

                for (int i = 0; i < blocks.Count && i < raws.Count; i++)
                {
                    VisualElement block = blocks[i];
                    VisualElement parent = block.parent;
                    if (parent == null)
                    {
                        continue;
                    }

                    Button button = i < buttons.Count ? buttons[i] : null;
                    string link = button?.userData as string;
                    bool? saved = null;
                    if (link != null && state?.checkIdToPassed != null &&
                        state.checkIdToPassed.TryGetValue(QuizStateStore.CheckIdForLink(link), out bool passed))
                    {
                        saved = passed;
                    }

                    VisualElement buttonHost = button?.parent;
                    CheckCard card = new(raws[i].Groups[1].Value, link, button, saved);
                    parent.Insert(parent.IndexOf(block), card);
                    block.RemoveFromHierarchy();

                    // Строка, в которой жила кнопка, осталась пустой — убираем лишний отступ.
                    if (buttonHost != null && buttonHost != parent && buttonHost.childCount == 0)
                    {
                        buttonHost.RemoveFromHierarchy();
                    }
                }
            }
            catch (Exception ex)
            {
                // Карточка — украшение: если не собралась, остаётся прежний вид (код + кнопка).
                Debug.LogWarning("CourseWindow: карточка задания не построена — " + ex.Message);
            }
        }

        // Смена слайда: прокрутка в начало и короткий въезд содержимого со стороны движения.
        private void PlaySlideEnter()
        {
            VisualElement body = contentRoot;
            if (body == null)
            {
                return;
            }

            bool sameLesson = string.Equals(lastShownLessonPath, currentLessonFilePath, StringComparison.Ordinal);
            int direction = !sameLesson ? 1 : Math.Sign(currentSlideIndex - lastShownSlideIndex);
            lastShownLessonPath = currentLessonFilePath;
            lastShownSlideIndex = currentSlideIndex;

            // Тот же слайд перерисован (обновление, проверка) — без анимации и без прыжка прокрутки.
            if (direction == 0)
            {
                return;
            }

            contentRoot.scrollOffset = Vector2.zero;

            // Если корень рендерера — тоже ScrollView, сбрасываем и его прокрутку.
            if (mdRenderer?.RootElement is ScrollView bodyScroll)
            {
                bodyScroll.scrollOffset = Vector2.zero;
            }

            string startClass = direction > 0 ? "algo-slide--from-right" : "algo-slide--from-left";
            body.RemoveFromClassList("algo-slide--from-right");
            body.RemoveFromClassList("algo-slide--from-left");
            body.AddToClassList(startClass);
            body.schedule.Execute(() => body.RemoveFromClassList(startClass)).StartingIn(20);
        }

        private void LoadWindowStyles()
        {
            try
            {
                StyleSheet styleSheet =
                    AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        AlgoNeoPackageAssetLocator.CourseWindowStylesheetAssetPath);
                if (styleSheet != null && !rootVisualElement.styleSheets.Contains(styleSheet))
                {
                    rootVisualElement.styleSheets.Add(styleSheet);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseWindow: не удалось загрузить стиль окна — {ex.Message}");
            }
        }
    }
}