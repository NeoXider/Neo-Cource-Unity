using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer
{
    /// <summary>
    /// Runtime UI Toolkit control that displays Markdown text through <see cref="UIMarkdownRenderer"/>.
    /// Drop it into a UXML document (or add it in code), then set <see cref="Markdown"/> or
    /// <see cref="SourceAsset"/> to (re)render — internally wraps its own <see cref="ScrollView"/> and
    /// keeps a single <see cref="UIMarkdownRenderer"/> instance alive across re-renders.
    ///
    /// Unlike the Editor-only tools (<c>MarkdownViewer</c>, <c>MarkdownCustomEditor</c>,
    /// <c>MarkdownPreviewWindow</c>), which resolve their default stylesheet via <c>AssetDatabase</c> and
    /// therefore only work inside the Editor, this control takes its stylesheet as a plain serialized
    /// <see cref="StyleSheet"/> reference (<see cref="DefaultStyleSheet"/>) — settable from UI Builder or
    /// code — so it renders identically in the Editor, Play Mode and builds (WebGL/standalone/etc).
    ///
    /// An explicitly assigned <see cref="DefaultStyleSheet"/> applies to this control only. An instance
    /// without one gets the package stylesheet in the Editor; in a build (no <c>AssetDatabase</c>) it falls
    /// back to the stylesheet most recently passed to any renderer, or to none. For a build, either always
    /// assign one explicitly, or set <see cref="IncludeDefaultStylesheet"/> false and layer your own theme
    /// (the same approach RedoSchool's <c>ChatUI</c> feature uses for chat bubbles).
    /// </summary>
    [UxmlElement]
    public sealed partial class MarkdownDisplay : VisualElement
    {
        private readonly VisualElement _host;

        private UIMarkdownRenderer _renderer;
        private string _markdown = string.Empty;
        private TextAsset _sourceAsset;
        private StyleSheet _defaultStyleSheet;
        private bool _includeDefaultStylesheet = true;

        /// <summary>Raised when the reader clicks a link, with the raw link target.</summary>
        public event Action<string> LinkClicked;

        [UxmlAttribute("markdown")]
        public string Markdown
        {
            get => _markdown;
            set
            {
                _sourceAsset = null;
                _markdown = value ?? string.Empty;
                Render();
            }
        }

        /// <summary>
        /// Convenience alternative to <see cref="Markdown"/>: assign a <c>.md</c> <see cref="TextAsset"/>
        /// (e.g. from <c>Resources</c>) instead of an inline string. Setting this overwrites
        /// <see cref="Markdown"/> with the asset's text.
        /// </summary>
        [UxmlAttribute("source-asset")]
        public TextAsset SourceAsset
        {
            get => _sourceAsset;
            set
            {
                _sourceAsset = value;
                _markdown = value != null ? value.text : string.Empty;
                Render();
            }
        }

        /// <summary>
        /// Stylesheet applied when <see cref="IncludeDefaultStylesheet"/> is true. Unlike the package's
        /// Editor tools, this is a normal serialized reference so it also works in builds. Leave unset to
        /// fall back to the shared default (see the class summary).
        /// </summary>
        [UxmlAttribute("default-stylesheet")]
        public StyleSheet DefaultStyleSheet
        {
            get => _defaultStyleSheet;
            set
            {
                _defaultStyleSheet = value;
                _renderer = null; // stylesheet/include flags are constructor-only on UIMarkdownRenderer.
                Render();
            }
        }

        [UxmlAttribute("include-default-stylesheet")]
        public bool IncludeDefaultStylesheet
        {
            get => _includeDefaultStylesheet;
            set
            {
                _includeDefaultStylesheet = value;
                _renderer = null;
                Render();
            }
        }

        public MarkdownDisplay()
        {
            style.flexGrow = 1;

            ScrollView scrollView = new(ScrollViewMode.Vertical) { name = "markdown-display-scroll" };
            scrollView.style.flexGrow = 1;
            Add(scrollView);

            _host = new VisualElement { name = "markdown-display-host" };
            scrollView.Add(_host);
        }

        /// <summary>Equivalent to setting <see cref="Markdown"/>; provided for a fluent/explicit call site.</summary>
        public void SetMarkdown(string markdown) => Markdown = markdown;

        private void Render()
        {
            if (string.IsNullOrEmpty(_markdown))
            {
                _host.Clear();
                _renderer = null;
                return;
            }

            if (_renderer == null)
            {
                _renderer = new UIMarkdownRenderer(
                    HandleLink,
                    false,
                    _defaultStyleSheet,
                    includeDefaultStylesheet: _includeDefaultStylesheet);
                _host.Clear();
                _host.Add(_renderer.RootElement);
            }

            try
            {
                _renderer.SetMarkdown(_markdown);
            }
            catch (Exception ex)
            {
                _host.Clear();
                Label error = new($"Markdown render failed: {ex.Message}");
                error.style.whiteSpace = WhiteSpace.Normal;
                error.style.color = Color.red;
                _host.Add(error);
                Debug.LogWarning($"[Neoxider] MarkdownDisplay render failed: {ex}");
                _renderer = null;
            }
        }

        private void HandleLink(string link, UIMarkdownRenderer renderer) => LinkClicked?.Invoke(link);
    }
}
