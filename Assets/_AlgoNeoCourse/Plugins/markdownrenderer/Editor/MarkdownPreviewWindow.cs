using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer
{
    /// <summary>
    /// Package-level editor tool: type/paste raw Markdown on the left, see it rendered through the
    /// SAME <see cref="UIMarkdownRenderer"/> pipeline consumers embed at runtime on the right, using
    /// only the package's own default stylesheet (<c>Styles/MarkdownRenderer.uss</c>) — no host
    /// project theme is involved. Use this to sanity-check every supported construct (headings,
    /// emphasis, inline code, quotes, lists, links, tables, ```python```/```csharp``` code blocks) while
    /// developing the renderer itself, independent of any consuming project's chat/UI skin.
    ///
    /// Open via <c>Window/Neoxider/Markdown Preview</c>.
    /// </summary>
    public sealed class MarkdownPreviewWindow : EditorWindow
    {
        private static readonly string DefaultSample = string.Join("\n", new[] { "# Heading 1", "## Heading 2", "### Heading 3", "", "A normal paragraph with **bold**, *italic* and `inline code`.", "", "> A blockquote.", "", "- list item one", "- list item two", "    - nested item", "", "1. numbered item", "2. second item", "", "Code sample:", "", "```python", "def area(radius):", "    return 3.14159 * radius ** 2", "", "for r in [1, 2, 3]:", "    print(f\"r={r}: area={area(r):.2f}\")", "```", "", "| Item | Status | Price |", "| :--- | :---: | ---: |", "| Sword | ready | 120 |", "| Shield with a longer name | draft | 85 |", "", "And a link: [Neoxider](https://example.com)." });

        private TextField _input;
        private VisualElement _previewHost;
        private float _previewScale = 1f;

        [MenuItem("Window/Neoxider/Markdown Preview")]
        public static void Open()
        {
            MarkdownPreviewWindow window = GetWindow<MarkdownPreviewWindow>();
            window.titleContent = new GUIContent("Markdown Preview");
            window.minSize = new Vector2(520, 420);
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            Label hint = new(
                "Neoxider Markdown Renderer package sample, rendered through UIMarkdownRenderer with " +
                "only the package's own default stylesheet (no host project theme).");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginBottom = 6;
            hint.style.flexShrink = 0;
            root.Add(hint);

            VisualElement controls = new();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 0;
            controls.style.marginBottom = 8;

            Button render = new(Render) { text = "Render" };
            controls.Add(render);

            Slider sizeSlider = new("Size", 0.4f, 1.6f) { value = _previewScale };
            sizeSlider.style.marginLeft = 12;
            sizeSlider.style.minWidth = 160;
            sizeSlider.RegisterValueChangedCallback(evt =>
            {
                _previewScale = evt.newValue;
                ApplyPreviewScale();
            });
            controls.Add(sizeSlider);

            root.Add(controls);

            VisualElement columns = new();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            columns.style.minHeight = 0;
            root.Add(columns);

            VisualElement left = new();
            left.style.flexGrow = 1;
            left.style.flexBasis = 0;
            left.style.minWidth = 0;
            left.style.minHeight = 0;
            left.style.marginRight = 8;
            columns.Add(left);

            VisualElement right = new();
            right.style.flexGrow = 1;
            right.style.flexBasis = 0;
            right.style.minWidth = 0;
            right.style.minHeight = 0;
            columns.Add(right);

            _input = new TextField { multiline = true, value = DefaultSample };
            _input.style.flexGrow = 1;
            _input.style.flexShrink = 1;
            _input.style.minHeight = 60;
            left.Add(_input);

            ScrollView scroll = new(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            right.Add(scroll);

            _previewHost = new VisualElement();
            _previewHost.style.transformOrigin = new TransformOrigin(0, 0);
            scroll.Add(_previewHost);
            ApplyPreviewScale();

            Render();
        }

        private void Render()
        {
            if (_previewHost == null)
            {
                return;
            }

            _previewHost.Clear();
            string markdown = _input != null ? _input.value : DefaultSample;

            try
            {
                UIMarkdownRenderer renderer = new(
                    (link, _) =>
                    {
                        if (!string.IsNullOrWhiteSpace(link))
                        {
                            Application.OpenURL(link);
                        }
                    },
                    false);
                renderer.SetMarkdown(markdown ?? string.Empty);
                _previewHost.Add(renderer.RootElement);
            }
            catch (System.Exception ex)
            {
                Label error = new($"Markdown render failed: {ex}");
                error.style.whiteSpace = WhiteSpace.Normal;
                error.style.color = Color.red;
                _previewHost.Add(error);
                Debug.LogWarning($"[Neoxider] Markdown Preview render failed: {ex}");
            }
        }

        private void ApplyPreviewScale()
        {
            if (_previewHost != null)
            {
                _previewHost.style.scale = new Scale(new Vector3(_previewScale, _previewScale, 1f));
            }
        }
    }
}
