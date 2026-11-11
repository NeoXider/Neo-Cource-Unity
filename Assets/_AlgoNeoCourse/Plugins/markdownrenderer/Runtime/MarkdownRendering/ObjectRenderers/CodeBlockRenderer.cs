using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class CodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeBlock>
    {
        // One highlighter per language. Adding a language = add an ISyntaxHighlighter implementation in
        // SyntaxHighlighting/ and map it here. Colors come from the shared VS Code "Dark+" palette;
        // code blocks render on a dark #1E1E1E background (ChatWindow.uss .codeblock). RedoSchool
        // customization — re-apply if this ThirdParty package is updated.
        private static readonly Dictionary<string, ISyntaxHighlighter> Highlighters =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "python", new PythonSyntaxHighlighter() },
                { "py", null }, // alias filled below
                { "csharp", new CSharpSyntaxHighlighter() },
                { "cs", null } // alias filled below
            };

        static CodeBlockRenderer()
        {
            Highlighters["py"] = Highlighters["python"];
            Highlighters["cs"] = Highlighters["csharp"];
        }

        /// <summary>
        /// Пересобирает подсветчик языка — с переданной палитрой или заново из его JSON
        /// (<c>palette = null</c>).
        /// <para>
        /// RedoSchool customization — re-apply if this ThirdParty package is updated. Подсветчики
        /// живут в статическом словаре и создаются один раз на домен, поэтому правка
        /// <c>Resources/SyntaxHighlighting/*.json</c> не была видна до перезагрузки домена: ни
        /// повторный рендер, ни новый экземпляр рендерера цвета не обновляли. Это шов для окна
        /// «Markdown Style Preview»: дизайнер двигает цвет и сразу видит результат, а источником
        /// истины остаётся JSON.
        /// </para>
        /// </summary>
        /// <returns>
        /// <c>false</c>, если у языка нет подсветчика в этой раскладке (например
        /// <c>python-light</c> — у markdown для него подсветчика нет). Вызывающий тогда
        /// честно скажет, что живого превью не будет, вместо тихого «ничего не произошло».
        /// </returns>
        public static bool ReloadPalette(string language, SyntaxPalette palette = null)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            string key = language.Trim();
            if (key.Equals("python", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("py", StringComparison.OrdinalIgnoreCase))
            {
                PythonSyntaxHighlighter python = new(palette);
                Highlighters["python"] = python;
                Highlighters["py"] = python;
                return true;
            }

            if (key.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("cs", StringComparison.OrdinalIgnoreCase))
            {
                CSharpSyntaxHighlighter csharp = new(palette);
                Highlighters["csharp"] = csharp;
                Highlighters["cs"] = csharp;
                return true;
            }

            return false;
        }

        /// <summary>Языки, для которых в этой раскладке есть подсветчик.</summary>
        public static IEnumerable<string> HighlightedLanguages => Highlighters.Keys;

        protected override void Write(UIMarkdownRenderer renderer, CodeBlock obj)
        {
            HtmlAttributes attribute = obj.GetAttributes();
            List<string> classes = new() { "codeblock" };
            string firstLanguageToken = null;
            if (attribute.Classes != null)
            {
                classes.AddRange(attribute.Classes);
            }

            // Expose fenced code language as a CSS class, e.g. language-quiz, language-truefalse
            if (obj is FencedCodeBlock fenced)
            {
                string info = fenced.Info;
                if (!string.IsNullOrWhiteSpace(info))
                {
                    // Markdig allows multiple info words; add class for the first token and the full raw info
                    string first = info.Trim().Split(' ')[0];
                    firstLanguageToken = first;
                    classes.Add($"language-{first}");
                    if (!string.Equals(first, info.Trim(), StringComparison.Ordinal))
                    {
                        classes.Add($"language-{info.Trim()}");
                    }

                    if (string.Equals(first, "quiz", StringComparison.OrdinalIgnoreCase))
                    {
                        classes.Add("quiz");
                    }
                }
            }

            VisualElement block = renderer.StartBlock(classes);

            string rawCode = GetRawCode(obj);
            string[] lines = rawCode.Replace("\r\n", "\n").Split('\n');

            ISyntaxHighlighter highlighter = ResolveHighlighter(firstLanguageToken);

            if (block == null)
            {
                // Внутри списка (LockTextCreation) отдельный блок создать нельзя: StartBlock вернул null, и раньше
                // код молча пропадал. Пишем его строки текстом в метку списка — содержимое не теряется.
                WriteCodeLinesAsText(renderer, lines, highlighter);
                return;
            }

            RenderCodeLines(block, lines, highlighter);

            renderer.FinishBlock();
        }

        private static void WriteCodeLinesAsText(UIMarkdownRenderer renderer, string[] lines,
            ISyntaxHighlighter highlighter)
        {
            if (renderer.CurrentBlockTextLabel == null)
            {
                return;
            }

            string[] expanded = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                expanded[i] = lines[i].Replace("\t", "    ");
            }

            // Подсветчик сохраняет пробелы строки, поэтому отступы кода остаются; без подсветчика код
            // получает цвет встроенного кода (или цвет текста, если потребитель так настроил рендерер).
            string[] rendered = highlighter != null ? highlighter.Highlight(expanded) : null;
            string plainColor = renderer.InlineCodeInheritsTextColor
                ? null
                : "#" + ColorUtility.ToHtmlStringRGB(renderer.InlineCodeColor);
            string indent = new(' ', renderer.IndentLevel * 2);

            for (int i = 0; i < lines.Length; i++)
            {
                string text = rendered != null ? rendered[i] : SyntaxHighlightText.Escape(expanded[i]);
                if (rendered == null && plainColor != null && text.Length > 0)
                {
                    text = "<color=" + plainColor + ">" + text + "</color>";
                }

                renderer.WriteText(indent + text + "\n");
            }
        }

        private static ISyntaxHighlighter ResolveHighlighter(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            return Highlighters.TryGetValue(language.Trim(), out ISyntaxHighlighter highlighter)
                ? highlighter
                : null;
        }

        private static void RenderCodeLines(VisualElement block, string[] lines, ISyntaxHighlighter highlighter)
        {
            // Dedent each line first (indent becomes a left margin), then highlight the whole block so the
            // highlighter can carry state across lines (Python docstrings, C# block comments).
            int[] indentLevels = new int[lines.Length];
            string[] dedented = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                int indentLevel = CountIndent(lines[i]);
                indentLevels[i] = indentLevel;
                dedented[i] = lines[i].Length > indentLevel ? lines[i][indentLevel..] : string.Empty;
            }

            string[] highlighted = highlighter != null
                ? highlighter.Highlight(dedented)
                : null;

            for (int i = 0; i < lines.Length; i++)
            {
                Label lineLabel = new();
                lineLabel.enableRichText = true;
                lineLabel.style.unityTextAlign = TextAnchor.UpperLeft;
                lineLabel.style.marginLeft = indentLevels[i] * 5;
                lineLabel.style.whiteSpace = WhiteSpace.Normal;

                string renderedText = highlighted != null
                    ? highlighted[i]
                    : SyntaxHighlightText.Escape(dedented[i]);

                lineLabel.text = string.IsNullOrEmpty(renderedText) ? " " : renderedText;
                block.Add(lineLabel);
            }
        }

        private static string GetRawCode(CodeBlock obj)
        {
            if (obj is not LeafBlock leaf || leaf.Lines.Lines == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int i = 0; i < leaf.Lines.Count; i++)
            {
                builder.Append(leaf.Lines.Lines[i].ToString());
                if (i < leaf.Lines.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            // Decode HTML entities that may have been introduced by the
            // Markdig pipeline or server transport (for example, &amp; to & and &lt; to <).
            return WebUtility.HtmlDecode(builder.ToString());
        }

        private static int CountIndent(string line)
        {
            int indent = 0;

            foreach (char symbol in line)
            {
                if (symbol == ' ')
                {
                    indent++;
                    continue;
                }

                if (symbol == '\t')
                {
                    indent += 4;
                    continue;
                }

                break;
            }

            return indent;
        }
    }
}
