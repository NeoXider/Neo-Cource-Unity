using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIMarkdownRenderer;
using NeoCource.Editor.Settings;
using NeoCource.Editor.Utils;
using NeoCource.Editor.UI;
using NeoCource.Editor.Quizzes;
using UnityEngine.UIElements.Experimental;
using UnityEditor.Callbacks;

namespace NeoCource.Editor
{
    public class CourseWindow : EditorWindow
    {
        private const string WindowTitle = "AlgoNeo Course";

        private Toolbar toolbar;
        private PopupField<string> lessonDropdown;
        private TextField lessonSearchField;
        private ToolbarMenu docsMenu;
        private Label slideIndicator;
        private ToolbarButton prevBtn;
        private ToolbarButton nextBtn;
        private ToolbarButton reloadBtn;
        private ToolbarButton openInExplorerBtn;

        private int LessonDropdownMaxLength => Math.Max(5, ScriptableSingleton<CourseSettings>.instance?.maxLessonTitleLength ?? 20);

        private ScrollView contentRoot;
        private UIMarkdownRenderer.UIMarkdownRenderer mdRenderer;

        private List<(string title, string filePath, string id)> availableLessons = new();
        private string currentLessonFilePath;
        private string currentLessonTitle;
        private int currentSlideIndex;
        private List<string> slides = new();
        private bool autoReloadScheduled = false;
        private bool autoReloadInProgress = false;

        // Quiz state for current slide
        private List<QuizQuestion> currentSlideQuestions = new();
        private bool canGoNextNow = true;

        [MenuItem("Tools/AlgoNeoCourse/Open Course Window")]
        public static void Open()
        {
            var wnd = GetWindow<CourseWindow>(false, WindowTitle, true);
            wnd.minSize = new Vector2(640, 420);
            wnd.Focus();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<CourseWindow>();
                foreach (var w in windows)
                {
                    EditorApplication.delayCall += () => { try { w.RestoreLastSession(); } catch { } };
                }
            };
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            BuildToolbar();
            BuildContent();
            EnsureHotkeysHook();

            EditorApplication.delayCall += () =>
            {
                try
                {
                    EnsureDocsMenuFromSettings();
                    RefreshLessonsList();
                    RestoreLastSession();
                    SaveLastSession();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"CourseWindow: deferred init failed — {ex.Message}");
                }
            };
        }

        private void BuildToolbar()
        {
            toolbar = new Toolbar();

            lessonDropdown = new PopupField<string>(new List<string>(), 0)
            {
                tooltip = "Выберите урок"
            };
            lessonDropdown.formatSelectedValueCallback = (string value) =>
            {
                if (string.IsNullOrEmpty(value)) return value;
                bool needTruncate = value.Length > LessonDropdownMaxLength;
                lessonDropdown.tooltip = needTruncate ? value : "Выберите урок";
                return needTruncate ? (value.Substring(0, LessonDropdownMaxLength - 3) + "...") : value;
            };
            lessonDropdown.formatListItemCallback = (string value) =>
            {
                if (string.IsNullOrEmpty(value)) return value;
                return value.Length > LessonDropdownMaxLength ? (value.Substring(0, LessonDropdownMaxLength - 3) + "...") : value;
            };
            lessonDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = lessonDropdown.index;
                if (idx >= 0 && idx < availableLessons.Count)
                {
                    LoadLesson(availableLessons[idx]);
                    SaveLastSession();
                }
            });
            lessonSearchField = new TextField { tooltip = "Фильтр уроков", value = string.Empty };
            lessonSearchField.style.maxWidth = 200;
            lessonSearchField.RegisterValueChangedCallback(_ => RefreshLessonsList());
            toolbar.Add(lessonSearchField);
            toolbar.Add(lessonDropdown);

            toolbar.Add(new ToolbarSpacer());

            var settingsIcon = (Texture2D)(EditorGUIUtility.IconContent("d__Popup@2x").image ?? EditorGUIUtility.IconContent("SettingsIcon").image ?? EditorGUIUtility.IconContent("_Popup").image);
            var settingsBtn = CreateIconButton(settingsIcon, "Открыть Course Settings", () =>
            {
                Selection.activeObject = CourseSettings.instance;
            });
            toolbar.Add(settingsBtn);

            prevBtn = new ToolbarButton(() => ShowSlide(currentSlideIndex - 1)) { text = "◀" };
            nextBtn = new ToolbarButton(() => ShowSlide(currentSlideIndex + 1)) { text = "▶" };
            prevBtn.tooltip = "Предыдущий";
            nextBtn.tooltip = "Следующий";
            prevBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            nextBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            prevBtn.style.color = new StyleColor(new Color(0.65f, 0.75f, 1f));
            nextBtn.style.color = new StyleColor(new Color(0.6f, 1f, 0.6f));
            nextBtn.style.minWidth = 60;
            slideIndicator = new Label("—/—") { style = { unityTextAlign = TextAnchor.MiddleCenter, minWidth = 60 } };

            toolbar.Add(prevBtn);
            toolbar.Add(slideIndicator);
            toolbar.Add(nextBtn);

            toolbar.Add(new ToolbarSpacer());

            var refreshTex = (Texture2D)(EditorGUIUtility.IconContent("d_Refresh").image ?? EditorGUIUtility.IconContent("Refresh").image);
            reloadBtn = CreateIconButton(refreshTex, "Обновить уроки", () => { DoRefreshLessonsAndCurrent(); });
            reloadBtn.style.color = new StyleColor(new Color(0.2f, 0.8f, 0.8f));
            openInExplorerBtn = new ToolbarButton(() =>
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                    EditorUtility.RevealInFinder(currentLessonFilePath);
            });
            var folderTex = (Texture2D)(EditorGUIUtility.IconContent("Folder Icon").image ?? EditorGUIUtility.IconContent("d_Project").image);
            openInExplorerBtn.text = string.Empty;
            var img = new Image { image = folderTex, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 16; img.style.height = 16; img.style.marginTop = 2; img.style.marginBottom = 2;
            openInExplorerBtn.Add(img);
            openInExplorerBtn.tooltip = "Показать файл";

            toolbar.Add(reloadBtn);
            toolbar.Add(openInExplorerBtn);

            rootVisualElement.Add(toolbar);

            // Горячие клавиши
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                try
                {
                    if (evt.keyCode == KeyCode.LeftArrow)
                    {
                        ShowSlide(currentSlideIndex - 1);
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.RightArrow)
                    {
                        if (CanGoNextSlide())
                        {
                            ShowSlide(currentSlideIndex + 1);
                        }
                        else
                        {
                            // опционально мигнём индикатором/лог
                            if (NeoCource.Editor.Settings.QuizSettings.instance.enableDebugLogging)
                                Debug.Log("[Quiz] Навигация вперёд заблокирована: есть незавершённые вопросы");
                        }
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.R)
                    {
                        DoRefreshLessonsAndCurrent();
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.O)
                    {
                        if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                            EditorUtility.RevealInFinder(currentLessonFilePath);
                        evt.StopPropagation();
                    }
                }
                catch { }
            }, TrickleDown.TrickleDown);
        }

        private void EnsureHotkeysHook()
        {
            try
            {
                // Делает корень фокусируемым, чтобы он мог получать KeyDown
                rootVisualElement.focusable = true;
                rootVisualElement.pickingMode = PickingMode.Position;
                rootVisualElement.tabIndex = 0;
                // Попробуем передать фокус окну после постройки UI
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        TryFocusWindowForHotkeys();
                    }
                };
            }
            catch { }
        }

        private void TryFocusWindowForHotkeys()
        {
            try
            {
                if (rootVisualElement != null)
                {
                    rootVisualElement.Focus();
                }
                Focus();
            }
            catch { }
        }

        private void DoRefreshLessonsAndCurrent()
        {
            RefreshLessonsList();
            if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
            {
                try
                {
                    var text = System.IO.File.ReadAllText(currentLessonFilePath);
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
                docsMenu.tooltip = "Примеры из Docs/Examples";
                PopulateDocsMenu();
                toolbar.Add(docsMenu);
            }
        }

        private void PopulateDocsMenu()
        {
            docsMenu.menu.ClearItems();
            string root = "Assets/_AlgoNeoCourse/Docs/Examples";
            if (!System.IO.Directory.Exists(root))
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            var files = System.IO.Directory.GetFiles(root, "*.md", System.IO.SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            foreach (var path in files)
            {
                string filePath = path.Replace('\\', '/');
                string name = System.IO.Path.GetFileNameWithoutExtension(filePath);
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
                currentLessonTitle = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                currentLessonFilePath = assetPath;
                currentSlideIndex = 0;
                var text = System.IO.File.ReadAllText(assetPath);
                slides = SplitSlides(text);
                SeedMarkdownContext(assetPath);
                ShowSlide(0);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"CourseWindow: не удалось открыть пример {assetPath}: {ex.Message}");
            }
        }

        private void BuildContent()
        {
            contentRoot = new ScrollView(ScrollViewMode.Vertical);
            contentRoot.style.flexGrow = 1f;

            mdRenderer = new UIMarkdownRenderer.UIMarkdownRenderer((link, renderer) => OnLinkClicked(link, renderer, null), includeScrollview: true);
            try
            {
                // Подключим стили квизов, если файл есть
                var stylePath = "Assets/_AlgoNeoCourse/Plugins/markdownrenderer/Styles/Quiz.uss";
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
                if (styleSheet != null)
                {
                    mdRenderer.RootElement.styleSheets.Add(styleSheet);
                }
            }
            catch { }
            contentRoot.Add(mdRenderer.RootElement);

            rootVisualElement.Add(contentRoot);
        }

        private void EnsureRichTextOnAllLabels()
        {
            if (mdRenderer == null || mdRenderer.RootElement == null) return;
            try
            {
                var labels = mdRenderer.RootElement.Query<Label>().ToList();
                foreach (var l in labels)
                {
                    if (l == null) continue;
                    l.enableRichText = true;
                }
            }
            catch { /* ignore */ }
        }

        private void FixBrokenMarkdownLinks()
        {
            if (mdRenderer == null || mdRenderer.RootElement == null) return;
            try
            {
                var labels = mdRenderer.RootElement.Query<Label>().ToList();
                foreach (var l in labels)
                {
                    if (l == null) continue;
                    string txt = l.text ?? string.Empty;
                    int idx = txt.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;

                    string link = null;
                    var m = System.Text.RegularExpressions.Regex.Match(txt, @"<link=(unity://check[^>]+)>");
                    if (m.Success) link = m.Groups[1].Value;
                    if (string.IsNullOrEmpty(link))
                    {
                        int start = txt.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase);
                        int end = start;
                        while (end < txt.Length && !char.IsWhiteSpace(txt[end]) && txt[end] != '>' && txt[end] != '"') end++;
                        link = txt.Substring(start, end - start);
                    }

                    // Заменим Label на настоящую кнопку с классом оформления
                    if (l.parent != null)
                    {
                        var parent = l.parent;
                        int at = parent.IndexOf(l);
                        var btn = new ToolbarButton();
                        btn.text = "Проверить";
                        btn.clicked += () =>
                        {
                            try { OnLinkClicked(link, mdRenderer, btn); } catch (Exception ex) { Debug.LogError(ex.Message); }
                        };
                        btn.AddToClassList("check-button");
                        parent.Insert(at, btn);
                        parent.Remove(l);
                    }
                }
            }
            catch { /* ignore */ }
        }

        private static ToolbarButton CreateIconButton(Texture2D icon, string tooltip, Action onClick)
        {
            var btn = new ToolbarButton(onClick) { tooltip = tooltip };
            btn.text = string.Empty;
            var img = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 16; img.style.height = 16; img.style.marginTop = 2; img.style.marginBottom = 2;
            btn.Add(img);
            return btn;
        }

        private void RefreshLessonsList()
        {
            var settings = ScriptableSingleton<CourseSettings>.instance;

            availableLessons.Clear();

            if (settings.lessonSelections != null)
            {
                foreach (var sel in settings.lessonSelections.Where(s => s.selected))
                {
                    var path = ResolveLocalLessonPath(settings, sel.id, sel.file);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        availableLessons.Add((sel.title, path, sel.id));
                    }
                }
            }

            try
            {
                string folder = settings.downloadFolderRelative?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    var knownPaths = new HashSet<string>(availableLessons.Select(l => Path.GetFullPath(l.filePath)), StringComparer.OrdinalIgnoreCase);
                    foreach (var md in Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly))
                    {
                        string full = Path.GetFullPath(md);
                        if (knownPaths.Contains(full)) continue;
                        string title = Path.GetFileNameWithoutExtension(md);
                        availableLessons.Add((title, md, title));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseWindow: сканирование папки загрузок завершилось с ошибкой — {ex.Message}");
            }

            // Сортировка: по модулю и номеру урока из id/имени файла (m{X} y{Y}), затем по title
            availableLessons.Sort((a, b) =>
            {
                var ka = ExtractModuleLessonKey(a.id, a.filePath, a.title);
                var kb = ExtractModuleLessonKey(b.id, b.filePath, b.title);
                int c = ka.module.CompareTo(kb.module);
                if (c != 0) return c;
                c = ka.lesson.CompareTo(kb.lesson);
                if (c != 0) return c;
                return string.Compare(a.title, b.title, StringComparison.CurrentCultureIgnoreCase);
            });

            // Фильтр по поиску
            string filter = (lessonSearchField?.value ?? string.Empty).Trim();
            IEnumerable<(string title, string filePath, string id)> filtered = availableLessons;
            if (!string.IsNullOrEmpty(filter))
            {
                filtered = availableLessons.Where(l =>
                    (!string.IsNullOrEmpty(l.title) && l.title.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(l.id) && l.id.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0));
            }

            var filteredList = filtered.ToList();
            var titles = filteredList.Select(l => l.title).ToList();
            if (titles.Count == 0)
            {
                titles.Add("Нет загруженных уроков — скачайте их в CourseSettings");
            }

            string savedPath = EditorPrefs.GetString(LastLessonPathKey, string.Empty);
            int savedSlide = EditorPrefs.GetInt(LastSlideIndexKey, 0);
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(savedPath))
            {
                try
                {
                    string fullSaved = Path.GetFullPath(savedPath);
                    for (int i = 0; i < filteredList.Count; i++)
                    {
                        if (string.Equals(Path.GetFullPath(filteredList[i].filePath), fullSaved, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                catch { /* ignore */ }
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
            if (string.IsNullOrEmpty(settings.downloadFolderRelative)) return null;
            string folder = settings.downloadFolderRelative.Replace("\\", "/");
            if (!Directory.Exists(folder)) return null;

            if (!string.IsNullOrEmpty(remotePath))
            {
                string fileName = Path.GetFileName(remotePath);
                string preferred = Path.Combine(folder, $"{id}-{fileName}");
                if (File.Exists(preferred)) return preferred;

                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                string alt = Path.Combine(folder, $"{nameNoExt}-{id}{ext}");
                if (File.Exists(alt)) return alt;

                var any = Directory.GetFiles(folder, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (!string.IsNullOrEmpty(any)) return any;
            }

            var candidate = Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(p =>
                {
                    var n = Path.GetFileName(p);
                    return n.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase) ||
                           n.EndsWith("-" + id + Path.GetExtension(n), StringComparison.OrdinalIgnoreCase) ||
                           n.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            return candidate;
        }

        private void LoadLesson((string title, string filePath, string id) lesson)
        {
            if (mdRenderer == null)
            {
                BuildContent();
            }
            // Перед сменой урока сохраним квиз-состояние и сбросим in-memory (если не нужна персистенция)
            try
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath))
                {
                    QuizStateStore.SaveLessonState(currentLessonFilePath);
                }
                if (!NeoCource.Editor.Settings.QuizSettings.instance.persistState)
                {
                    QuizStateStore.ResetInMemory();
                }
            }
            catch { }
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
            var parts = Regex.Split(md.Replace("\r\n", "\n"), @"^\n?\s*---\s*$", RegexOptions.Multiline).ToList();
            return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private void ShowSlide(int index)
        {
            // Сохраняем состояние квизов перед сменой слайда
            try { if (!string.IsNullOrEmpty(currentLessonFilePath)) QuizStateStore.SaveLessonState(currentLessonFilePath); } catch { }
            if (slides == null || slides.Count == 0)
            {
                slideIndicator.text = "—/—";
                mdRenderer.SetMarkdown("# Пусто");
                EnsureRichTextOnAllLabels();
                FixBrokenMarkdownLinks();
                return;
            }

            currentSlideIndex = Mathf.Clamp(index, 0, slides.Count - 1);
            slideIndicator.text = $"{currentSlideIndex + 1}/{slides.Count}";
            prevBtn.SetEnabled(currentSlideIndex > 0);
            nextBtn.SetEnabled(currentSlideIndex < slides.Count - 1);

            var md = slides[currentSlideIndex];
            md = InjectCheckBlocksIfDebug(md);
            md = PreprocessMediaLinks(md);
            md = ConvertGifLinksToMp4(md);
            // Replace quiz blocks with markers for in-place rendering
            List<QuizQuestion> parsedForMarkers;
            try { md = QuizParser.ReplaceQuizBlocksWithMarkers(md, out parsedForMarkers); }
            catch { parsedForMarkers = new List<QuizQuestion>(); }
            if (mdRenderer != null && string.IsNullOrEmpty(mdRenderer.FileFolder) && !string.IsNullOrEmpty(currentLessonFilePath))
            {
                SeedMarkdownContext(currentLessonFilePath);
                if (string.IsNullOrEmpty(mdRenderer.FileFolder) && File.Exists(currentLessonFilePath))
                {
                    try { mdRenderer.OpenFile(currentLessonFilePath); }
                    catch (Exception) { }
                }
            }
            mdRenderer.SetMarkdown(md);
            // Parse quiz blocks on this slide
            try
            {
                // prefer parsedForMarkers if any; otherwise fallback to parsing after SetMarkdown
                currentSlideQuestions = parsedForMarkers != null && parsedForMarkers.Count > 0 ? parsedForMarkers : QuizParser.ParseQuestions(md);
            }
            catch { currentSlideQuestions = new List<QuizQuestion>(); }
            // Render quiz UI on top of markdown element if questions exist
            try
            {
                if (currentSlideQuestions != null && currentSlideQuestions.Count > 0 && mdRenderer?.RootElement != null)
                {
                    // Ищем маркеры [[QUIZ:<id>]] и заменяем их на визуальные блоки
                    var allLabels = mdRenderer.RootElement.Query<Label>().ToList();
                    foreach (var q in currentSlideQuestions)
                    {
                        string marker = "[[QUIZ:" + q.id + "]]";
                        // 1) точное совпадение
                        var host = allLabels.FirstOrDefault(l => string.Equals(l.text, marker, StringComparison.Ordinal));
                        if (host != null && host.parent != null)
                        {
                            ReplaceLabelWithQuiz(host, q);
                            continue;
                        }
                        // 2) маркер внутри текста
                        var hostWithText = allLabels.FirstOrDefault(l => !string.IsNullOrEmpty(l.text) && l.text.Contains(marker));
                        if (hostWithText != null && hostWithText.parent != null)
                        {
                            var parent = hostWithText.parent;
                            int idx = parent.IndexOf(hostWithText);
                            string text = hostWithText.text;
                            var parts = text.Split(new[] { marker }, StringSplitOptions.None);
                            parent.Remove(hostWithText);
                            // before
                            if (!string.IsNullOrEmpty(parts[0]))
                            {
                                var before = new Label(parts[0]);
                                parent.Insert(idx++, before);
                            }
                            // quiz container
                            var container = new VisualElement();
                            parent.Insert(idx++, container);
                            QuizRenderer.RenderQuizzes(container, currentLessonFilePath, new List<QuizQuestion> { q }, OnQuizStateChanged);
                            // after
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                            {
                                var after = new Label(parts[1]);
                                parent.Insert(idx, after);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseWindow: quiz render failed — " + ex.Message);
            }
            EnsureRichTextOnAllLabels();
            FixBrokenMarkdownLinks();
            // Guard next on initial render as well
            try
            {
                if (NeoCource.Editor.Settings.QuizSettings.instance.guardSlideNavigation)
                {
                    bool canGoNext = !HasUnfinishedQuestions(currentSlideQuestions, currentLessonFilePath);
                    canGoNextNow = canGoNext;
                    nextBtn.SetEnabled(canGoNextNow);
                    if (NeoCource.Editor.Settings.QuizSettings.instance.enableDebugLogging)
                    {
                        Debug.Log($"[Quiz] guard initial: canNext={canGoNext}, slide={currentSlideIndex+1}");
                    }
                }
            }
            catch { }
            SaveLastSession();
            Repaint();
        }

        private void ReplaceLabelWithQuiz(Label host, QuizQuestion q)
        {
            var parent = host.parent;
            if (parent == null) return;
            int idx = parent.IndexOf(host);
            parent.Remove(host);
            var container = new VisualElement();
            parent.Insert(idx, container);
            QuizRenderer.RenderQuizzes(container, currentLessonFilePath, new List<QuizQuestion> { q }, OnQuizStateChanged);
        }

        private void OnQuizStateChanged()
        {
            try
            {
                if (NeoCource.Editor.Settings.QuizSettings.instance.guardSlideNavigation)
                {
                    bool canGoNext = !HasUnfinishedQuestions(currentSlideQuestions, currentLessonFilePath);
                    canGoNextNow = canGoNext;
                    nextBtn.SetEnabled(canGoNextNow);
                    if (NeoCource.Editor.Settings.QuizSettings.instance.enableDebugLogging)
                    {
                        Debug.Log($"[Quiz] guard update: canNext={canGoNext}, slide={currentSlideIndex+1}");
                    }
                }
            }
            catch { }
        }

        private bool HasUnfinishedQuestions(List<QuizQuestion> questions, string lessonPath)
        {
            try
            {
                if (questions == null || questions.Count == 0) return false;
                var state = QuizStateStore.GetLessonState(lessonPath, false);
                if (state == null) return questions.Count == 0;
                foreach (var q in questions)
                {
                    if (!state.questionIdToState.TryGetValue(q.id, out var s) || !s.isCompleted)
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        private bool CanGoNextSlide()
        {
            try
            {
                if (!NeoCource.Editor.Settings.QuizSettings.instance.guardSlideNavigation) return true;
                return canGoNextNow;
            }
            catch { return true; }
        }

        private const string LastLessonPathKey = "AlgoNeoCourse.LastLessonPath";
        private const string LastSlideIndexKey = "AlgoNeoCourse.LastSlideIndex";

        private void SaveLastSession()
        {
            if (!string.IsNullOrEmpty(currentLessonFilePath))
                EditorPrefs.SetString(LastLessonPathKey, currentLessonFilePath);
            EditorPrefs.SetInt(LastSlideIndexKey, currentSlideIndex);
        }

        private void RestoreLastSession()
        {
            if (mdRenderer == null) BuildContent();
            string lastPath = EditorPrefs.GetString(LastLessonPathKey, string.Empty);
            int lastSlide = EditorPrefs.GetInt(LastSlideIndexKey, 0);
            if (string.IsNullOrEmpty(lastPath) || !File.Exists(lastPath)) return;

            if (availableLessons == null || availableLessons.Count == 0)
            {
                try { RefreshLessonsList(); } catch { }
            }

            var found = availableLessons.FirstOrDefault(l => string.Equals(Path.GetFullPath(l.filePath), Path.GetFullPath(lastPath), StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(found.filePath))
            {
                try
                {
                    currentLessonTitle = Path.GetFileNameWithoutExtension(lastPath);
                    currentLessonFilePath = lastPath;
                    var text = File.ReadAllText(lastPath);
                    slides = SplitSlides(text);
                    SeedMarkdownContext(lastPath);
                    ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
                    return;
                }
                catch { }
            }
            else
            {
                LoadLesson(found);
                ShowSlide(Mathf.Clamp(lastSlide, 0, Math.Max(0, slides.Count - 1)));
            }
        }

        private string InjectCheckBlocksIfDebug(string md)
        {
            if (!ValidationSettings.Instance.DebugRenderCheckBlocks) return md;

            var pattern = @"```check\n([\s\S]*?)\n```";
            var rx = new Regex(pattern, RegexOptions.Multiline);

            var result = rx.Replace(md, m =>
            {
                var raw = m.Groups[1].Value;
                var enc = Uri.EscapeDataString(raw);
                var btn = $"[▶ Проверить](unity://check?type=from-block&dialog=auto&__raw_block__={enc})";
                return m.Value + "\n" + btn + "\n";
            });

            return result;
        }

        private static string ToProjectRelativePath(string fullPath)
        {
            try
            {
                string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string full = Path.GetFullPath(fullPath);
                if (full.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = full.Substring(projectPath.Length + 1).Replace('\\', '/');
                    return rel;
                }
            }
            catch { }
            return null;
        }

        private string PreprocessMediaLinks(string md)
        {
            if (string.IsNullOrEmpty(md)) return md;
            md = Regex.Replace(md, @"!\[\s*\]\(", "![img](");

            return Regex.Replace(md, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                var target = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(target)) return match.Value;

                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                // Относительный путь относительно текущего md-файла
                if (!string.IsNullOrEmpty(currentLessonFilePath))
                {
                    try
                    {
                        string baseDir = Path.GetDirectoryName(currentLessonFilePath);
                        string combined = Path.GetFullPath(Path.Combine(baseDir ?? ".", target));
                        if (File.Exists(combined))
                        {
                            string projRel = ToProjectRelativePath(combined);
                            if (!string.IsNullOrEmpty(projRel))
                            {
                                return match.Value.Replace(target, projRel);
                            }
                        }
                    }
                    catch { }
                }

                var nameOnly = Path.GetFileNameWithoutExtension(target);
                var replaced = match.Value.Replace(target, "search:" + nameOnly);
                return replaced;
            });
        }

        private string ConvertGifLinksToMp4(string md)
        {
            if (string.IsNullOrEmpty(md)) return md;
            var settings = CourseSettings.instance;
            if (!settings.autoConvertGifToMp4 || string.IsNullOrEmpty(settings.ffmpegPath)) return md;

            int replacedCount = 0;
            var result = Regex.Replace(md, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                var url = match.Groups[1].Value.Trim();
                if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)))
                    return match.Value;

                var mp4AssetsPath = NeoCource.Editor.GifSupport.GifConverter.ConvertGifToMp4IfNeeded(url);
                if (string.IsNullOrEmpty(mp4AssetsPath)) return match.Value;
                replacedCount++;

                var alt = Regex.Match(match.Value, "!\\[([^\\]]*)\\]").Groups[1].Value;
                return $"![{alt}]({mp4AssetsPath})";
            });

            if (settings.enableDebugLogging && replacedCount == 0)
            {
            }
            if (replacedCount > 0 && !autoReloadScheduled)
            {
                autoReloadScheduled = true;
                EditorApplication.delayCall += () =>
                {
                    if (this == null) { autoReloadScheduled = false; return; }
                    autoReloadInProgress = true;
                    try { ShowSlide(currentSlideIndex); }
                    finally { autoReloadInProgress = false; autoReloadScheduled = false; }
                };
            }

            return result;
        }

        private void OnLinkClicked(string link, UIMarkdownRenderer.UIMarkdownRenderer renderer, VisualElement clickedElement = null)
        {
            if (link.StartsWith("unity://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var (action, args) = ParseUnityLink(link);
                    if (string.Equals(action, "slide", StringComparison.OrdinalIgnoreCase))
                    {
                        int dir = args.TryGetValue("dir", out var d) && d == "next" ? 1 : -1;
                        ShowSlide(currentSlideIndex + dir);
                        return;
                    }
                    if (string.Equals(action, "check", StringComparison.OrdinalIgnoreCase))
                    {
                        string resultMessage = AlgoNeoTaskChecker.Execute(args);
                        if (clickedElement != null)
                        {
                            CheckResultPresenter.Show(clickedElement, resultMessage);
                        }
                        return;
                    }
                    if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetValue("path", out var p))
                        {
                            AlgoNeoEditorUtils.OpenAssetOrPath(p);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"CourseWindow: ошибка обработки ссылки {link} — {ex.Message}");
                }
                return;
            }

            Application.OpenURL(link);
        }

        private static (string action, Dictionary<string, string> args) ParseUnityLink(string link)
        {
            string withoutScheme = link.Substring("unity://".Length);
            string action;
            string query = string.Empty;
            int q = withoutScheme.IndexOf('?');
            if (q >= 0)
            {
                action = withoutScheme.Substring(0, q);
                query = withoutScheme.Substring(q + 1);
            }
            else action = withoutScheme;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var pair in query.Split('&'))
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    var kv = pair.Split('=');
                    var k = Uri.UnescapeDataString(kv[0]);
                    var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                    dict[k] = v;
                }
            }
            return (action, dict);
        }

        private void SeedMarkdownContext(string filePath)
        {
            if (mdRenderer == null || string.IsNullOrEmpty(filePath)) return;
            try
            {
                string localDir = Path.GetDirectoryName(filePath);
                string fullDir = Path.GetFullPath(localDir ?? ".");

                var t = typeof(UIMarkdownRenderer.UIMarkdownRenderer);
                var localField = t.GetField("m_LocalFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
                var folderField = t.GetField("m_FileFolder", BindingFlags.NonPublic | BindingFlags.Instance);
                localField?.SetValue(mdRenderer, localDir);
                folderField?.SetValue(mdRenderer, fullDir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseWindow: не удалось проинициализировать контекст MarkdownRenderer — {ex.Message}");
            }
        }

        private static (int module, int lesson) ExtractModuleLessonKey(string id, string path, string title)
        {
            string src = id ?? string.Empty;
            if (string.IsNullOrEmpty(src)) src = System.IO.Path.GetFileNameWithoutExtension(path ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrEmpty(src)) src = title ?? string.Empty;
            // Ищем шаблон m{num}...y{num}
            var m = System.Text.RegularExpressions.Regex.Match(src, @"m(\d+)[^\d]*y(\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out int mod);
                int.TryParse(m.Groups[2].Value, out int les);
                return (mod, les);
            }
            // fallback: первые два числа в строке
            var nums = System.Text.RegularExpressions.Regex.Matches(src, @"\d+");
            if (nums.Count >= 2)
            {
                int.TryParse(nums[0].Value, out int mod);
                int.TryParse(nums[1].Value, out int les);
                return (mod, les);
            }
            if (nums.Count == 1)
            {
                int.TryParse(nums[0].Value, out int only);
                return (only, 0);
            }
            return (int.MaxValue, int.MaxValue);
        }
    }
}
