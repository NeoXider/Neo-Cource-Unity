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
using NeoCource.Editor.Util;
using UnityEngine.UIElements.Experimental;
using UnityEditor.Callbacks;

namespace NeoCource.Editor
{
    public class CourseWindow : EditorWindow
    {
        private const string WindowTitle = "AlgoNeo Course";

        private Toolbar toolbar;
        private PopupField<string> lessonDropdown;
        private ToolbarMenu docsMenu;
        private Label slideIndicator;
        private ToolbarButton prevBtn;
        private ToolbarButton nextBtn;
        private ToolbarButton reloadBtn;
        private ToolbarButton openInExplorerBtn;

        private ScrollView contentRoot;
        private UIMarkdownRenderer.UIMarkdownRenderer mdRenderer;

        private List<(string title, string filePath, string id)> availableLessons = new();
        private string currentLessonFilePath;
        private string currentLessonTitle;
        private int currentSlideIndex;
        private List<string> slides = new();
        private bool autoReloadScheduled = false;
        private bool autoReloadInProgress = false;

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
            // После domain reload восстановим сессию во всех открытых окнах
            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<CourseWindow>();
                foreach (var w in windows)
                {
                    // Дадим окну завершить CreateGUI/RefreshLessonsList
                    EditorApplication.delayCall += () => { try { w.RestoreLastSession(); } catch { } };
                }
            };
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            BuildToolbar();
            BuildContent();

            // Defer accessing ScriptableSingleton instances until after editor load
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
            lessonDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = lessonDropdown.index;
                if (idx >= 0 && idx < availableLessons.Count)
                {
                    LoadLesson(availableLessons[idx]);
                    SaveLastSession();
                }
            });
            toolbar.Add(lessonDropdown);

            toolbar.Add(new ToolbarSpacer());

            // Settings button before back
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
            // Сделаем кнопку вперёд визуально крупнее
            nextBtn.style.minWidth = 60;
            slideIndicator = new Label("—/—") { style = { unityTextAlign = TextAnchor.MiddleCenter, minWidth = 60 } };

            toolbar.Add(prevBtn);
            toolbar.Add(slideIndicator);
            toolbar.Add(nextBtn);

            toolbar.Add(new ToolbarSpacer());

            // Кнопка обновления с иконкой редактора (надёжнее, чем эмодзи)
            var refreshTex = (Texture2D)(EditorGUIUtility.IconContent("d_Refresh").image ?? EditorGUIUtility.IconContent("Refresh").image);
            reloadBtn = CreateIconButton(refreshTex, "Обновить уроки", () =>
            {
                // Полный цикл обновления: перечитать список уроков, перечитать текущий файл и переразметить слайды
                RefreshLessonsList();
                if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                {
                    try
                    {
                        var text = System.IO.File.ReadAllText(currentLessonFilePath);
                        slides = SplitSlides(text);
                        // форсируем ререндер текущего слайда
                        ShowSlide(currentSlideIndex);
                        SaveLastSession();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"CourseWindow: не удалось перечитать текущий урок — {ex.Message}");
                    }
                }
                // Дополнительно реинициализируем меню Docs в случае изменения флага
                EnsureDocsMenuFromSettings();
            });
            reloadBtn.style.color = new StyleColor(new Color(0.2f, 0.8f, 0.8f));
            openInExplorerBtn = new ToolbarButton(() =>
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                    EditorUtility.RevealInFinder(currentLessonFilePath);
            });
            var folderTex = (Texture2D)(EditorGUIUtility.IconContent("Folder Icon").image ?? EditorGUIUtility.IconContent("d_Project").image);
            // Заменим визуал на иконку
            openInExplorerBtn.text = string.Empty;
            var img = new Image { image = folderTex, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 16; img.style.height = 16; img.style.marginTop = 2; img.style.marginBottom = 2;
            openInExplorerBtn.Add(img);
            openInExplorerBtn.tooltip = "Показать файл";

            // Docs/Examples quick access in debug — added later in deferred init

            toolbar.Add(reloadBtn);
            toolbar.Add(openInExplorerBtn);

            rootVisualElement.Add(toolbar);
        }

        private void EnsureDocsMenuFromSettings()
        {
            // Remove existing menu if any
            if (docsMenu != null)
            {
                toolbar.Remove(docsMenu);
                docsMenu = null;
            }

            // Add docs menu only if enabled in settings
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
                // seed renderer with file context so relative media resolve
                // Инициализируем контекст без предварительного полного рендера файла, сразу показываем слайд 0
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

            // Markdown renderer root
            mdRenderer = new UIMarkdownRenderer.UIMarkdownRenderer(OnLinkClicked, includeScrollview: true);
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

                    // Try to extract the target from <link=unity://check...>
                    string link = null;
                    var m = System.Text.RegularExpressions.Regex.Match(txt, @"<link=(unity://check[^>]+)>");
                    if (m.Success) link = m.Groups[1].Value;
                    if (string.IsNullOrEmpty(link))
                    {
                        // fallback: raw url in text
                        int start = txt.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase);
                        int end = start;
                        while (end < txt.Length && !char.IsWhiteSpace(txt[end]) && txt[end] != '>' && txt[end] != '"') end++;
                        link = txt.Substring(start, end - start);
                    }

                    // Replace visual text to a clean label
                    l.text = "▶ Проверить";
                    l.style.color = new StyleColor(new Color(0.30f, 0.49f, 1.0f));
                    l.AddToClassList("linkHovered");
                    l.RegisterCallback<ClickEvent>(_ =>
                    {
                        try { OnLinkClicked(link, mdRenderer); } catch (Exception ex) { Debug.LogError(ex.Message); }
                    });
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

            // Дополнительно: подхватить любые .md из папки загрузок, если они ещё не в списке
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

            var titles = availableLessons.Select(l => l.title).ToList();
            if (titles.Count == 0)
            {
                titles.Add("Нет загруженных уроков — скачайте их в CourseSettings");
            }

            // Попробуем восстановить последний открытый урок/слайд из EditorPrefs
            string savedPath = EditorPrefs.GetString(LastLessonPathKey, string.Empty);
            int savedSlide = EditorPrefs.GetInt(LastSlideIndexKey, 0);
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(savedPath))
            {
                try
                {
                    string fullSaved = Path.GetFullPath(savedPath);
                    for (int i = 0; i < availableLessons.Count; i++)
                    {
                        if (string.Equals(Path.GetFullPath(availableLessons[i].filePath), fullSaved, StringComparison.OrdinalIgnoreCase))
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

            if (availableLessons.Count > 0)
            {
                LoadLesson(availableLessons[selectedIndex]);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    // Перейдём на сохранённый слайд
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
                // мы сохраняем как <id>-<fileName>
                string preferred = Path.Combine(folder, $"{id}-{fileName}");
                if (File.Exists(preferred)) return preferred;

                // поддержка варианта <fileNameWithoutExt>-<id>.ext
                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                string alt = Path.Combine(folder, $"{nameNoExt}-{id}{ext}");
                if (File.Exists(alt)) return alt;

                // резерв: поиск просто по имени файла
                var any = Directory.GetFiles(folder, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (!string.IsNullOrEmpty(any)) return any;
            }

            // последний шанс — первый .md файл с id внутри имени
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
            currentLessonTitle = lesson.title;
            currentLessonFilePath = lesson.filePath;
            currentSlideIndex = 0;

            string text = File.ReadAllText(currentLessonFilePath);
            slides = SplitSlides(text);
            // Инициализируем контекст без предварительного полного рендера файла, сразу показываем слайд 0
            SeedMarkdownContext(currentLessonFilePath);
            ShowSlide(0);
            SaveLastSession();
        }

        private static List<string> SplitSlides(string md)
        {
            var parts = Regex.Split(md.Replace("\r\n", "\n"), @"^\n?\s*---\s*$", RegexOptions.Multiline).ToList();
            // очистим пустые
            return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private void ShowSlide(int index)
        {
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
            // Подстрахуемся: если у рендера не инициализирован FileFolder, инициализируем по текущему файлу
            if (mdRenderer != null && string.IsNullOrEmpty(mdRenderer.FileFolder) && !string.IsNullOrEmpty(currentLessonFilePath))
            {
                SeedMarkdownContext(currentLessonFilePath);
                if (string.IsNullOrEmpty(mdRenderer.FileFolder) && File.Exists(currentLessonFilePath))
                {
                    try { mdRenderer.OpenFile(currentLessonFilePath); }
                    catch (Exception) { /* ignore — цель лишь инициализировать контекст */ }
                }
            }
            mdRenderer.SetMarkdown(md);
            EnsureRichTextOnAllLabels();
            FixBrokenMarkdownLinks();
            SaveLastSession();
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

            // Найти урок в доступных и открыть его
            var found = availableLessons.FirstOrDefault(l => string.Equals(Path.GetFullPath(l.filePath), Path.GetFullPath(lastPath), StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(found.filePath))
            {
                // Если отсутствует в списке — попробуем открыть напрямую
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
                catch { /* ignore */ }
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

            // Оставляем исходный fenced-блок ```check ... ``` и добавляем только кнопку "Проверить".
            // Результаты проверки пишутся в консоль.
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

        // Converts bare filenames (e.g., sample.jpg) into search:sample.jpg so the renderer can find assets by name in the project.
        // Leaves URLs, absolute project paths, and relative paths untouched (they resolve via FileFolder seeded by OpenFile).
        private static string PreprocessMediaLinks(string md)
        {
            if (string.IsNullOrEmpty(md)) return md;
            // Гарантируем ненулевой alt-текст для изображений, чтобы рендерер не падал на FirstChild.ToString()
            md = Regex.Replace(md, @"!\[\s*\]\(", "![img](");

            return Regex.Replace(md, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                var target = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(target)) return match.Value;

                // Already a URL or special scheme
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                // Absolute project paths or relative paths with folders are fine
                if (target.StartsWith("Assets/") || target.StartsWith("Packages/") || target.Contains("/"))
                {
                    return match.Value;
                }

                // Bare filename -> use search:<nameWithoutExt> for robust project-wide lookup
                var nameOnly = Path.GetFileNameWithoutExtension(target);
                var replaced = match.Value.Replace(target, "search:" + nameOnly);
                return replaced;
            });
        }

        // Конвертирует ссылки на .gif в локальные mp4 через ffmpeg (если настроен), чтобы UI Toolkit-видео их воспроизводил
        private string ConvertGifLinksToMp4(string md)
        {
            if (string.IsNullOrEmpty(md)) return md;
            var settings = CourseSettings.instance;
            if (!settings.autoConvertGifToMp4 || string.IsNullOrEmpty(settings.ffmpegPath)) return md;

            int replacedCount = 0;
            var result = Regex.Replace(md, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                var url = match.Groups[1].Value.Trim();
                // Обрабатываем только внешние/файловые ссылки. Остальные (Assets/, search:, package:) оставляем как есть
                if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)))
                    return match.Value;

                var mp4AssetsPath = NeoCource.Editor.GifSupport.GifConverter.ConvertGifToMp4IfNeeded(url);
                if (string.IsNullOrEmpty(mp4AssetsPath)) return match.Value;
                replacedCount++;

                // Заменим на видео с тем же alt
                var alt = Regex.Match(match.Value, "!\\[([^\\]]*)\\]").Groups[1].Value;
                return $"![{alt}]({mp4AssetsPath})";
            });

            if (settings.enableDebugLogging && replacedCount == 0)
            {
                //Debug.Log("[AlgoNeoCourse] Markdown: GIF-ссылок не найдено — конвертация не выполнялась.");
            }
            // Если были замены, то через маленькую задержку перезагрузим текущий слайд один раз,
            // чтобы гарантировать, что импорт mp4 завершён и UI их увидит
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

        private void OnLinkClicked(string link, UIMarkdownRenderer.UIMarkdownRenderer renderer)
        {
            // Спец-схема unity://action?key=value
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
                        AlgoNeoTaskChecker.Execute(args);
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

            // обычные ссылки
            Application.OpenURL(link);
        }

        private static (string action, Dictionary<string, string> args) ParseUnityLink(string link)
        {
            // unity://action?key=value&key2=value2
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
    }
}


