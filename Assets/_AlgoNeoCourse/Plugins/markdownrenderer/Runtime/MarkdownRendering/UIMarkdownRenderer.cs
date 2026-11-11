using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Markdig;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UIMarkdownRenderer.ObjectRenderers;
using UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UIMarkdownRenderer
{
    public class UIMarkdownRenderer : RendererBase
    {
        /// <summary>Цвет встроенного кода для следующего рендера; палитра блоков Python от него не зависит.</summary>
        public Color InlineCodeColor { get; set; }

        /// <summary>
        ///     Встроенный код не пишет тег цвета и берёт цвет текста от USS своего Label (остаётся жирным).
        ///     Нужен поверхностям, где весь текст одного цвета; <see cref="InlineCodeColor" /> тогда не читается.
        /// </summary>
        public bool InlineCodeInheritsTextColor { get; set; }

        /// <summary>Ссылка остаётся подчёркнутой и кликабельной, но берёт цвет текста от USS, а не синий #4C7EFF.</summary>
        public bool LinkInheritsTextColor { get; set; }

        // Общие значения по умолчанию: в редакторе — ассеты самого пакета (грузятся один раз), в сборке —
        // последний явно переданный ассет, чтобы рендерер без аргументов по-прежнему получал оформление.
        // Явно переданный в конструктор ассет в эти поля как «значение для всех» больше не попадает:
        // он хранится в экземпляре (m_DefaultStylesheet и т.д.) и действует только на свой рендерер.
        private static StyleSheet s_DefaultStylesheet;
        private static VisualTreeAsset s_VideoPlayerElementPrefab;
        private static StyleSheet s_VideoPlayerStyleSheet;

        private static StyleSheet s_LastExplicitDefaultStylesheet;
        private static VisualTreeAsset s_LastExplicitVideoPlayerElementPrefab;
        private static StyleSheet s_LastExplicitVideoPlayerStyleSheet;

        private readonly StyleSheet m_DefaultStylesheet;
        private readonly VisualTreeAsset m_VideoPlayerElementPrefab;
        private readonly StyleSheet m_VideoPlayerStyleSheet;

#if UNITY_EDITOR
        private const string EditorDefaultStylesheetPath = "Styles/MarkdownRenderer.uss";
        private const string EditorVideoPlayerUxmlPath = "Runtime/VideoElement/VideoPlayerElement.uxml";
        private const string EditorVideoPlayerStylesheetPath = "Runtime/VideoElement/VideoPlayerElement.uss";
        private static string s_EditorPackageRoot;

        /// <summary>
        /// Путь ассета внутри пакета. Корень — по самому пакету, а не строкой: установленный из git пакет лежит в
        /// <c>Packages/com.neoxider.markdownrenderer</c>, встроенный в проект — где угодно под <c>Assets</c>.
        /// </summary>
        private static string EditorPackageAssetPath(string relativePath)
        {
            if (s_EditorPackageRoot == null)
            {
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIMarkdownRenderer).Assembly);
                s_EditorPackageRoot = package != null ? package.assetPath : FindEmbeddedPackageRoot();
            }

            return s_EditorPackageRoot + "/" + relativePath;
        }

        /// <summary>Встроенная копия: корень — на три уровня выше этого скрипта (Runtime/MarkdownRendering).</summary>
        private static string FindEmbeddedPackageRoot()
        {
            foreach (string guid in AssetDatabase.FindAssets("UIMarkdownRenderer t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                const string suffix = "/Runtime/MarkdownRendering/UIMarkdownRenderer.cs";
                if (path.EndsWith(suffix, System.StringComparison.Ordinal))
                {
                    // Runtime/MarkdownRendering/UIMarkdownRenderer.cs — корень пакета тремя уровнями выше.
                    return System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(
                        System.IO.Path.GetDirectoryName(path))).Replace('\\', '/');
                }
            }

            return "Packages/com.neoxider.markdownrenderer";
        }
#endif

        // Порядок расширений важен: UseGenericAttributes подключается к уже добавленным парсерам, поэтому по
        // рекомендации Markdig стоит последним. Pipe-таблицы требуют строку-разделитель под шапкой
        // (RequireHeaderSeparator по умолчанию), так что абзац с символами `|` таблицей не становится.
        private static readonly MarkdownPipeline s_StaticPipeline =
            new MarkdownPipelineBuilder().UsePipeTables().UseYamlFrontMatter().UseGenericAttributes().Build();

        public class Command
        {
            public string CommandName;
            public string[] CommandParameters;
        }

        public delegate void CommandHandlerDelegate(Command cmd);

        /// <summary>
        ///     The root visual element. Add this to your UI tree to render the Markdown.
        /// </summary>
        public VisualElement RootElement { get; protected set; }

        /// <summary>
        ///     This is the root of the content. This will be the same as RootElement if a ScrollView wasn't asked when
        ///     creating the Renderer, otherwise this will be the ScrollView
        /// </summary>
        public VisualElement ContentRoot { get; protected set; }

        public bool LockTextCreation { get; set; } = false;
        public int IndentLevel { get; set; } = 0;

        /// <summary>
        ///     Current text <see cref="Label" /> (e.g. list line when <see cref="LockTextCreation" /> is true).
        /// </summary>
        internal Label CurrentBlockTextLabel => m_CurrentBlockText;

        public string FileFolder { get; private set; }

        private readonly bool m_IncludeScrollView;
        private readonly Stack<VisualElement> m_BlockStack = new();

        private string m_LocalFilePath;
        //useful when using relative file path

        private Label m_CurrentBlockText;
        private readonly Texture m_LoadingTexture;

        //used for jumping to header, the key is a whitespace removed version of the header
        public struct HeaderEntry
        {
            public string HeaderKey;
            public Label HeaderLabel;
        }

        private readonly List<HeaderEntry> m_HeadersToLabelMappings = new();

#if !UNITY_2022_2_OR_NEWER
        private PropertyInfo m_TextHandleFieldInfo;
        private PropertyInfo m_TextInfoFieldInfo;

        private Type m_TextElementInfoType;
        private FieldInfo m_TextElementInfoFieldInfo;

        private Type m_LinkInfoType;
        private FieldInfo m_LinkFieldInfo;
        
        class LinkData
        {
            public List<LinkInfoCopy> LinkInfo;
            public List<TextElementInfoCopy> TextElementInfo;
            public bool HoveredIconDisplayed;
        }
#endif

        private readonly Action<string, UIMarkdownRenderer> m_CurrentLinkHandler;

        private CommandHandlerDelegate m_CommandHandler;

        private readonly List<StyleSheet> m_CustomStylesheets;

        public override object Render(MarkdownObject markdownObject)
        {
            CreateNewRoot();
            Write(markdownObject);
            MarkLastRenderedElement(ContentRoot);

            return this;
        }

        //Call to handle all type of link : relative, absolute and special search
        public string ResolveLink(string link)
        {
            if (link.StartsWith("search:"))
            {
#if UNITY_EDITOR
                //this is a relative link, so find the actual link
                link = link.Replace("search:", "");

                string[] files = AssetDatabase.FindAssets(link);

                if (files.Length == 0)
                {
                    Debug.LogError($"Couldn't find image {link}");
                    return "";
                }

                link = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(files[0]));
#else
                Debug.LogWarning($"Markdown search links are only available in the editor: {link}");
                return "";
#endif
            }
            else if (link.StartsWith("package:"))
            {
#if UNITY_EDITOR
                //will search only in packages
                link = link.Replace("package:", "");

                string[] files = AssetDatabase.FindAssets($"a:packages {link}");

                if (files.Length == 0)
                {
                    Debug.LogError($"Couldn't find link : {link}");
                    return "";
                }

                link = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(files[0]));
#else
                Debug.LogWarning($"Markdown package links are only available in the editor: {link}");
                return "";
#endif
            }
            else if (link.StartsWith(".") || link.StartsWith(".."))
            {
                try
                {
                    // "./" и "../" резолвим относительно папки текущего файла в нормальный полный путь:
                    // склейка строк оставляла в пути "/./" и "/../", а без папки файла отдавала ссылку как есть.
                    link = Path.GetFullPath(Path.Combine(string.IsNullOrEmpty(FileFolder) ? "." : FileFolder, link));
                }
                catch (Exception)
                {
                    // Недопустимые символы в ссылке не должны ронять рендер документа.
                    return link;
                }
            }
            else if (link.StartsWith("Packages") || link.StartsWith("Assets"))
            {
                link = Path.GetFullPath(link);
            }

            link = link.Replace("\\", "/");

            return link;
        }

        public void RegisterCommandHandler(CommandHandlerDelegate handler) => m_CommandHandler += handler;

        public void SendCommand(Command cmd) => m_CommandHandler?.Invoke(cmd);

        private void DefaultCommandHandler(Command cmd)
        {
            switch (cmd.CommandName)
            {
                case "log":
                    if (cmd.CommandParameters.Length == 0)
                    {
                        return;
                    }

                    Debug.Log(cmd.CommandParameters[0]);
                    break;
                case "highlight":
                {
                    if (cmd.CommandParameters.Length != 2)
                    {
                        Debug.LogError("Command highlight with another number of parameters than 2");
                        return;
                    }

#if UNITY_EDITOR
                    Highlighter.Highlight(cmd.CommandParameters[0], cmd.CommandParameters[1],
                        HighlightSearchMode.Identifier);
#else
                    Debug.LogWarning("Markdown highlight commands are only available in the editor.");
#endif
                }
                    break;
            }
        }

        private void CreateNewRoot()
        {
            ContentRoot.Clear();
            m_BlockStack.Clear();
            m_BlockStack.Push(ContentRoot);
        }

        private static void MarkLastRenderedElement(VisualElement root)
        {
            if (root == null || root.childCount == 0)
            {
                return;
            }

            VisualElement lastChild = root.ElementAt(root.childCount - 1);
            lastChild.AddToClassList("markdown-last-child");
            MarkLastRenderedElement(lastChild);
        }

        public UIMarkdownRenderer(
            Action<string, UIMarkdownRenderer> LinkHandler,
            bool includeScrollview = true,
            StyleSheet defaultStylesheet = null,
            StyleSheet videoPlayerStyleSheet = null,
            VisualTreeAsset videoPlayerElementPrefab = null,
            bool includeDefaultStylesheet = true)
        {
            ColorUtility.TryParseHtmlString(VsCodePalette.InlineCode, out Color inlineCodeColor);
            InlineCodeColor = inlineCodeColor;

#if UNITY_EDITOR
            // Ассеты пакета нужны только тому, кто не передал свои: явный аргумент общий кэш не трогает.
            if (includeDefaultStylesheet && defaultStylesheet == null && s_DefaultStylesheet == null)
            {
                s_DefaultStylesheet = AssetDatabase.LoadAssetAtPath(
                    EditorPackageAssetPath(EditorDefaultStylesheetPath),
                    typeof(StyleSheet)) as StyleSheet;
                if (s_DefaultStylesheet == null)
                {
                    Debug.LogError("Couldn't load the MarkdownRenderer.uss stylesheet");
                }
            }

            if (videoPlayerElementPrefab == null && s_VideoPlayerElementPrefab == null)
            {
                s_VideoPlayerElementPrefab =
                    AssetDatabase.LoadAssetAtPath(
                        EditorPackageAssetPath(EditorVideoPlayerUxmlPath),
                        typeof(VisualTreeAsset)) as VisualTreeAsset;
            }

            if (videoPlayerStyleSheet == null && s_VideoPlayerStyleSheet == null)
            {
                s_VideoPlayerStyleSheet =
                    AssetDatabase.LoadAssetAtPath(
                        EditorPackageAssetPath(EditorVideoPlayerStylesheetPath),
                        typeof(StyleSheet)) as StyleSheet;
            }

            m_LoadingTexture = EditorGUIUtility.Load("WaitSpin00") as Texture;
#else
            m_LoadingTexture = Texture2D.grayTexture;
#endif

            // Раньше явный аргумент перезаписывал статическое поле, и его получал каждый следующий рендерер
            // в процессе. Теперь он действует на свой экземпляр; без аргумента берётся ассет пакета (редактор)
            // либо последний явно переданный (сборка, где AssetDatabase нет) — прежние вызовы не ломаются.
            m_DefaultStylesheet = FirstAlive(defaultStylesheet, s_DefaultStylesheet, s_LastExplicitDefaultStylesheet);
            m_VideoPlayerElementPrefab = FirstAlive(videoPlayerElementPrefab, s_VideoPlayerElementPrefab,
                s_LastExplicitVideoPlayerElementPrefab);
            m_VideoPlayerStyleSheet = FirstAlive(videoPlayerStyleSheet, s_VideoPlayerStyleSheet,
                s_LastExplicitVideoPlayerStyleSheet);

            if (defaultStylesheet != null)
            {
                s_LastExplicitDefaultStylesheet = defaultStylesheet;
            }

            if (videoPlayerElementPrefab != null)
            {
                s_LastExplicitVideoPlayerElementPrefab = videoPlayerElementPrefab;
            }

            if (videoPlayerStyleSheet != null)
            {
                s_LastExplicitVideoPlayerStyleSheet = videoPlayerStyleSheet;
            }

            ObjectRenderers.Add(new YamlFrontMatterHandler());

            ObjectRenderers.Add(new ParagraphBlockRenderer());
            ObjectRenderers.Add(new HeadingBlockRenderer());
            ObjectRenderers.Add(new ListBlockRenderer());
            ObjectRenderers.Add(new CodeBlockRenderer());
            ObjectRenderers.Add(new ThematiceBreakBlockRenderer());
            ObjectRenderers.Add(new QuoteBlockRenderer());
            ObjectRenderers.Add(new TableRenderer());

            ObjectRenderers.Add(new EmphasisInlineRenderer());
            ObjectRenderers.Add(new DelimiterInlineRenderer());
            ObjectRenderers.Add(new LiteralInlineRenderer());
            ObjectRenderers.Add(new LineBreakInlineRenderer());
            ObjectRenderers.Add(new CodeInlineRenderer());
            ObjectRenderers.Add(new LinkInlineRenderer());

#if UNITY_2022_2_OR_NEWER
            //In 2022.2 and after there is an event when hovering over link in a label so we can use that instead of reflection
#else
#if UNITY_2022_1_OR_NEWER
            string handleName = "uitkTextHandle";
            string textHandleTypeName = "UnityEngine.TextCore.Text.TextHandle, UnityEngine.TextCoreTextEngineModule";
            string textInfoName = "textInfo";
#else 
            string handleName = "textHandle";
            string textHandleTypeName = "UnityEngine.UIElements.TextCoreHandle, UnityEngine.UIElementsModule";
            string textInfoName = "textInfoMesh";
#endif
             m_TextHandleFieldInfo =
 typeof(TextElement).GetProperty(handleName, BindingFlags.NonPublic|BindingFlags.Instance);
            Type textCoreHandleType = Type.GetType(textHandleTypeName);
            m_TextInfoFieldInfo =
                textCoreHandleType.GetProperty(textInfoName, BindingFlags.NonPublic | BindingFlags.Instance);

            Type textInfoType =
                Type.GetType("UnityEngine.TextCore.Text.TextInfo, UnityEngine.TextCoreTextEngineModule");
            
            m_LinkFieldInfo = textInfoType.GetField("linkInfo");
            m_TextElementInfoFieldInfo = textInfoType.GetField("textElementInfo");

            m_LinkInfoType = Type.GetType("UnityEngine.TextCore.Text.LinkInfo, UnityEngine.TextCoreTextEngineModule");
            m_TextElementInfoType =
                Type.GetType("UnityEngine.TextCore.Text.TextElementInfo, UnityEngine.TextCoreTextEngineModule");
#endif
            m_CommandHandler += DefaultCommandHandler;

            m_CurrentLinkHandler = LinkHandler;
            m_CustomStylesheets = new List<StyleSheet>();
            m_HeadersToLabelMappings.Clear();

            ContentRoot = new VisualElement { name = "RendererRoot" };
            ContentRoot.AddToClassList("mainBody");

            m_IncludeScrollView = includeScrollview;
            if (includeScrollview)
            {
                RootElement = new ScrollView();
                RootElement.name = "ScrollViewRoot";
                RootElement.Add(ContentRoot);
            }
            else
            {
                RootElement = ContentRoot;
            }

            if (includeDefaultStylesheet && m_DefaultStylesheet != null)
            {
                RootElement.styleSheets.Add(m_DefaultStylesheet);
            }

            foreach (StyleSheet stylesheet in m_CustomStylesheets)
            {
                RootElement.styleSheets.Add(stylesheet);
            }
        }

        // Сравнение через оператор UnityEngine.Object: уничтоженный ассет считается отсутствующим.
        private static T FirstAlive<T>(T first, T second, T third) where T : UnityEngine.Object
        {
            if (first != null)
            {
                return first;
            }

            return second != null ? second : third;
        }

        public void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                // Пустой путь раньше падал внутри Path.GetDirectoryName / Path.GetFullPath. Контекст файла не
                // меняем, а в документе показываем тот же видимый fallback, что и для отсутствующего файла.
                SetMarkdown("# Markdown file not found\n\nThe file path is empty.");
                return;
            }

            SetFileContext(filePath);

            if (!File.Exists(filePath))
            {
                // Файл отсутствует — рендерим видимый fallback прямо в документе (строка ниже). Отдельный
                // Debug.LogError убран как избыточный: он лишь засорял консоль (в т.ч. при прогоне тестов),
                // дублируя то, что и так показано пользователю в самой панели рендера.
                SetMarkdown($"# Markdown file not found\n\n`{filePath}`");
                return;
            }

            SetMarkdown(File.ReadAllText(filePath));
        }

        /// <summary>
        ///     Задаёт папку файла (для относительных ссылок, картинок, видео и <c>uss:</c> из front matter)
        ///     так же, как <see cref="OpenFile" />, но ничего не рендерит: текст потом подаётся через
        ///     <see cref="SetMarkdown" />. Пустой путь игнорируется, файл может не существовать.
        /// </summary>
        public void SetFileContext(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string folder = Path.GetDirectoryName(filePath);
            m_LocalFilePath = folder;
            FileFolder = Path.GetFullPath(string.IsNullOrEmpty(folder) ? "." : folder);
        }

        public void SetMarkdown(string markdownText)
        {
            MarkdownDocument parsed = Markdown.Parse(markdownText, s_StaticPipeline);
            Render(parsed);
        }

        internal void WriteLeafBlockInline(LeafBlock block)
        {
            Inline inline = block.Inline;

            while (inline != null)
            {
                Write(inline);
                inline = inline.NextSibling;
            }
        }

        internal void WriteLeafRawLines(LeafBlock block)
        {
            if (block.Lines.Lines == null)
            {
                return;
            }

            StringLineGroup lines = block.Lines;
            StringLine[] slices = lines.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = slices[i].ToString();
                if (i != lines.Count - 1)
                {
                    line += "\n";
                }

                WriteText(line);
            }
        }

        public void AddCustomUSS(string path)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (path.StartsWith("."))
            {
                if (string.IsNullOrEmpty(m_LocalFilePath))
                {
                    // Текст подан через SetMarkdown без контекста файла — относительный путь резолвить не от чего.
                    Debug.LogWarning($"Markdown custom USS {path} is relative, but no file context is set");
                    return;
                }

                path = Path.Combine(m_LocalFilePath, path.Substring(1).TrimStart('/', '\\')).Replace('\\', '/');
            }

            StyleSheet stylesheet;
            try
            {
                stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Couldn't load custom USS {path}: {ex.Message}");
                return;
            }

            if (stylesheet == null)
            {
                Debug.LogError($"Couldn't find custom USS {path} defined in file {m_LocalFilePath}");
                return;
            }

            m_CustomStylesheets.Add(stylesheet);
#else
            Debug.LogWarning($"Markdown USS path loading is only available in the editor: {path}");
#endif
        }

        public void AddStyleSheet(StyleSheet stylesheet)
        {
            if (stylesheet == null)
            {
                return;
            }

            m_CustomStylesheets.Add(stylesheet);
            RootElement?.styleSheets.Add(stylesheet);
        }

        public void StartNewText(List<string> classLists)
        {
            if (LockTextCreation)
            {
                return;
            }

            m_CurrentBlockText = new Label();
            foreach (string c in classLists)
            {
                m_CurrentBlockText.AddToClassList(c);
            }

            m_BlockStack.Peek().Add(m_CurrentBlockText);
        }

        public void StartNewText(params string[] classLists) => StartNewText(classLists.ToList());

        public void WriteText(string text) => m_CurrentBlockText.text += text;

        public Image AddImage()
        {
            FinishBlock();

            Image imgElement = new();
            imgElement.AddToClassList("md-image");
            imgElement.scaleMode = ScaleMode.ScaleToFit;
            imgElement.image = m_LoadingTexture;

            m_BlockStack.Peek().Add(imgElement);
            StartBlock();

            return imgElement;
        }

        public VideoPlayerElement AddVideoPlayer()
        {
            FinishBlock();

            VideoPlayerElement newPlayer = null;
            if (m_VideoPlayerElementPrefab != null)
            {
                newPlayer = m_VideoPlayerElementPrefab.Instantiate().Q<VideoPlayerElement>();
            }

            newPlayer ??= new VideoPlayerElement();
            if (m_VideoPlayerStyleSheet != null)
            {
                newPlayer.styleSheets.Add(m_VideoPlayerStyleSheet);
            }

            m_BlockStack.Peek().Add(newPlayer);
            StartBlock();

            return newPlayer;
        }

#if !UNITY_2022_2_OR_NEWER
        public void OpenLink(string linkTarget)
        {
            if (m_CurrentBlockText.userData == null)
            {
                //this capture the current click handler, so if m_CurrentLinkHandler is changed before the link is clicked
                //we use the right one.
                var clickHandler = m_CurrentLinkHandler;
                m_CurrentBlockText.RegisterCallback<MouseMoveEvent>(MouseMoveOnLink);
                m_CurrentBlockText.RegisterCallback<ClickEvent>((evt) =>
                {
                    var lnk = CheckLinkAgainstCursor(evt.target as Label, evt.localPosition);
                    if (lnk != null)
                    {
                        string target = lnk.GetLinkId();
                        clickHandler(target);
                    }
                });
                m_CurrentBlockText.RegisterCallback<MouseLeaveEvent>(MouseLeaveOnLink);

                m_CurrentBlockText.userData = new LinkData();
            }

            m_CurrentBlockText.text += LinkOpenTags(linkTarget);
        }


        void MouseMoveOnLink(MouseMoveEvent evt)
        {
            var label = evt.target as Label;
            var linkData = label.userData as LinkData;
            var link = CheckLinkAgainstCursor(label, evt.localMousePosition);
            
            if (link == null)
            {
                if (linkData.HoveredIconDisplayed)
                {
                    linkData.HoveredIconDisplayed = false;
                    label.RemoveFromClassList("linkHovered");
                    
                    //needed to force the cursor to update
                    label.SendEvent(new MouseOverEvent());
                }
            }
            else
            {
                if (!linkData.HoveredIconDisplayed)
                {
                    linkData.HoveredIconDisplayed = true;
                    label.AddToClassList("linkHovered");
                    
                    //needed to force the cursor to update
                    label.SendEvent(new MouseOverEvent());
                }
            }
            
        }

        void MouseLeaveOnLink(MouseLeaveEvent evt)
        {
            var label = evt.target as Label;
            var linkData = label.userData as LinkData;
            
            if (linkData.HoveredIconDisplayed)
            {
                linkData.HoveredIconDisplayed = false;
                label.RemoveFromClassList("linkHovered");
            }
        }
#else

        public void OpenLink(string linkTarget)
        {
            m_CurrentBlockText.text += LinkOpenTags(linkTarget);
            m_CurrentBlockText.RegisterCallback<PointerOverLinkTagEvent>(evt => { (evt.target as Label).AddToClassList("linkHovered"); });
            m_CurrentBlockText.RegisterCallback<PointerOutLinkTagEvent>(evt => { (evt.target as Label).RemoveFromClassList("linkHovered"); });

            m_CurrentBlockText.RegisterCallback<PointerUpLinkTagEvent>(evt => { m_CurrentLinkHandler(evt.linkID, this); });
        }
#endif

        public void CloseLink() =>
            m_CurrentBlockText.text += LinkInheritsTextColor ? "</u></link>" : "</u></color></link>";

        private string LinkOpenTags(string linkTarget) =>
            LinkInheritsTextColor
                ? "<link=" + linkTarget + "><u>"
                : "<link=" + linkTarget + "><color=#4C7EFF><u>";

        public void RegisterHeader(string headerText, Label label)
        {
            //we first change all space to - but also remove any non alpha numeric character 
            string sanitizedHeader = headerText.Replace(" ", "-").ToLowerInvariant();
            sanitizedHeader = string.Concat(sanitizedHeader.Where(c => char.IsLetterOrDigit(c) || c == '-'));

            m_HeadersToLabelMappings.Add(new HeaderEntry { HeaderKey = sanitizedHeader, HeaderLabel = label });
        }

        /// <summary>
        ///     Will return the Label that correspond to the link (in the form of #header-text) passed as parameter
        /// </summary>
        /// <param name="link">An anchor link in the form #header-text</param>
        /// <returns></returns>
        public Label GetLabelFromHeaderAnchor(string link)
        {
            foreach (HeaderEntry headerEntry in m_HeadersToLabelMappings)
            {
                if (headerEntry.HeaderKey.StartsWith(link.Remove(0, 1)))
                {
                    return headerEntry.HeaderLabel;
                }
            }

            return null;
        }

        public void ScrollToHeader(string link)
        {
            if (!m_IncludeScrollView)
            {
                return;
            }

            Label label = GetLabelFromHeaderAnchor(link);
            if (label == null)
            {
                return;
            }


            ScrollView scrollView = RootElement as ScrollView;
            Rect localLabelPos = scrollView.WorldToLocal(label.worldBound);
            scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, localLabelPos.y);
        }


#if !UNITY_2022_2_OR_NEWER
        LinkInfoCopy CheckLinkAgainstCursor(Label target, Vector2 localMousePosition)
        {
            var data = target.userData as LinkData;

            if (data == null)
                return null;

            var localPosInvert = localMousePosition;
            localPosInvert.y = target.localBound.height - localPosInvert.y;
            
            foreach (var link in data.LinkInfo)
            {
                for (int i = 0; i < link.linkTextLength; i++)
                {
                    var textInfo = data.TextElementInfo[link.linkTextfirstCharacterIndex + i];

                    Rect r = new Rect(textInfo.bottomLeft,
                        new Vector2(textInfo.topRight.x - textInfo.topLeft.x, textInfo.topLeft.y - textInfo.bottomLeft.y));
                    
                    if (r.Contains(localPosInvert))
                    { //hovering that link
                        return link;
                    }
                }
            }

            return null;
        }
#endif

        internal VisualElement StartBlock(List<string> classList)
        {
            if (LockTextCreation)
            {
                return null;
            }

            VisualElement newBlock = new();
            newBlock.AddToClassList("block");
            foreach (string c in classList)
            {
                newBlock.AddToClassList(c);
            }

            m_BlockStack.Peek().Add(newBlock);

            m_BlockStack.Push(newBlock);

            return newBlock;
        }

        internal VisualElement StartBlock(params string[] classList) => StartBlock(classList.ToList());

        /// <summary>
        ///     Вложенный контейнер без класса <c>block</c> (строка и ячейка таблицы): отступы блока ему не нужны,
        ///     а содержимое должно попадать внутрь. Закрывается обычным <see cref="FinishBlock" />.
        ///     При <see cref="LockTextCreation" /> возвращает null, как и <see cref="StartBlock(string[])" />.
        /// </summary>
        internal VisualElement StartContainer(params string[] classList)
        {
            if (LockTextCreation)
            {
                return null;
            }

            VisualElement container = new();
            foreach (string c in classList)
            {
                container.AddToClassList(c);
            }

            m_BlockStack.Peek().Add(container);
            m_BlockStack.Push(container);

            return container;
        }

        internal void FinishBlock()
        {
            if (LockTextCreation)
            {
                //finishing a block when locked from creating new text mean we just jump a line (used by list)
                m_CurrentBlockText.text += "\n";
                return;
            }

#if !UNITY_2022_2_OR_NEWER
            if (m_CurrentBlockText.userData != null)
            {
                m_CurrentBlockText.generateVisualContent += OnGenerateLinkVisualContent;
            }
#endif

            m_BlockStack.Pop();
        }

#if !UNITY_2022_2_OR_NEWER
        void OnGenerateLinkVisualContent(MeshGenerationContext ctx)
        {
            Label l = ctx.visualElement as Label;

            LinkData data = l.userData as LinkData;
            
            var th = m_TextHandleFieldInfo.GetValue(ctx.visualElement);
            var textInfo = m_TextInfoFieldInfo.GetValue(th);
            
            var linkInfoArray = m_LinkFieldInfo.GetValue(textInfo) as Array;
            var textElementInfoArray = m_TextElementInfoFieldInfo.GetValue(textInfo) as Array;
            
            Debug.Log((ctx.visualElement as Label).text);
            
            data.TextElementInfo = new List<TextElementInfoCopy>();
            foreach (var obj in textElementInfoArray)
            {
                TextElementInfoCopy cpy = new TextElementInfoCopy();
                CopyFromTo(m_TextElementInfoType, obj, ref cpy);
                data.TextElementInfo.Add(cpy);
            }

            data.LinkInfo = new List<LinkInfoCopy>();
            foreach (var itm in linkInfoArray)
            {
                LinkInfoCopy cpy = new LinkInfoCopy();
                CopyFromTo(m_LinkInfoType, itm, ref cpy);
                Debug.Log("Link info copied");
                data.LinkInfo.Add(cpy);
            }
            
            Debug.Log($"Data link info length {data.LinkInfo.Count}");
            
        }

        static void CopyFromTo<T>(Type fromType, object from, ref T to)
        {
            var membersSource = fromType.GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.NonPublic);
            var membersDest = typeof(T).GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.NonPublic);
            
            foreach (var info in membersSource)
            {
                var dest = membersDest.FirstOrDefault(fieldInfo => fieldInfo.Name == info.Name);

                if (dest != null)
                {
                    var value = info.GetValue(from);
                    dest.SetValue(to, value);
                }
            }
        }
#endif

        //Those are just copy of the internal class in TextCore to easily copy the content of those by reflection as their
        //counterpart are internal. If this is exposed someday, can be removed.

#if !UNITY_2022_2_OR_NEWER
        internal class TextElementInfoCopy
        {
            public char character;
            public int index;
            public int spriteIndex;
            public Material material;
            public int materialReferenceIndex;
            public bool isUsingAlternateTypeface;
            public float pointSize;
            public int lineNumber;
            public int pageNumber;
            public int vertexIndex;
            public Vector3 topLeft;
            public Vector3 bottomLeft;
            public Vector3 topRight;
            public Vector3 bottomRight;
            public float origin;
            public float ascender;
            public float baseLine;
            public float descender;
            public float xAdvance;
            public float aspectRatio;
            public float scale;
            public Color32 color;
            public Color32 underlineColor;
            public Color32 strikethroughColor;
            public Color32 highlightColor;
            public bool isVisible;
        }

        internal class LinkInfoCopy
        {
            public int hashCode;
            public int linkIdFirstCharacterIndex;
            public int linkIdLength;
            public int linkTextfirstCharacterIndex;
            public int linkTextLength;
            char[] linkId;
    
            public string GetLinkId() => new string(this.linkId, 0, this.linkIdLength);
        }

#endif
    }
}
