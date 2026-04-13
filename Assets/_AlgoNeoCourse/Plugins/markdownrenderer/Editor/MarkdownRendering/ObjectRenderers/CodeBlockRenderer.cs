using System;
using System.Collections.Generic;
using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class CodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeBlock>
    {
        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while"
        };

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

            if (block == null)
            {
                return;
            }

            if (string.Equals(firstLanguageToken, "csharp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(firstLanguageToken, "cs", StringComparison.OrdinalIgnoreCase))
            {
                RenderCodeLines(block, lines, true);
            }
            else
            {
                RenderCodeLines(block, lines, false);
            }

            renderer.FinishBlock();
        }

        private static void RenderCodeLines(VisualElement block, string[] lines, bool highlightCSharp)
        {
            foreach (string rawLine in lines)
            {
                int indentLevel = CountIndent(rawLine);
                string trimmedLine = rawLine.Length > indentLevel ? rawLine[indentLevel..] : string.Empty;

                Label lineLabel = new();
                lineLabel.enableRichText = true;
                lineLabel.style.unityTextAlign = TextAnchor.UpperLeft;
                lineLabel.style.marginLeft = indentLevel * 5;
                lineLabel.style.whiteSpace = WhiteSpace.Normal;

                string renderedText = highlightCSharp
                    ? HighlightCSharp(trimmedLine)
                    : EscapeRichText(trimmedLine);

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

            return builder.ToString();
        }

        private static string HighlightCSharp(string code)
        {
            StringBuilder builder = new();

            for (int i = 0; i < code.Length;)
            {
                if (i + 1 < code.Length && code[i] == '/' && code[i + 1] == '/')
                {
                    int start = i;
                    i += 2;
                    while (i < code.Length && code[i] != '\n')
                    {
                        i++;
                    }

                    AppendColored(builder, code[start..i], "#7A7F8A");
                    continue;
                }

                if (code[i] == '"')
                {
                    int start = i++;
                    while (i < code.Length)
                    {
                        if (code[i] == '\\' && i + 1 < code.Length)
                        {
                            i += 2;
                            continue;
                        }

                        if (code[i] == '"')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    AppendColored(builder, code[start..i], "#FF7EDB");
                    continue;
                }

                if (code[i] == '\'')
                {
                    int start = i++;
                    while (i < code.Length)
                    {
                        if (code[i] == '\\' && i + 1 < code.Length)
                        {
                            i += 2;
                            continue;
                        }

                        if (code[i] == '\'')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    AppendColored(builder, code[start..i], "#FF7EDB");
                    continue;
                }

                if (char.IsDigit(code[i]))
                {
                    int start = i++;
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '.' || code[i] == '_'))
                    {
                        i++;
                    }

                    AppendColored(builder, code[start..i], "#B388FF");
                    continue;
                }

                if (char.IsLetter(code[i]) || code[i] == '_')
                {
                    int start = i++;
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                    {
                        i++;
                    }

                    string token = code[start..i];
                    if (CSharpKeywords.Contains(token))
                    {
                        AppendColored(builder, token, "#FF9F1C");
                        continue;
                    }

                    int lookahead = i;
                    while (lookahead < code.Length && char.IsWhiteSpace(code[lookahead]) && code[lookahead] != '\n')
                    {
                        lookahead++;
                    }

                    if (lookahead < code.Length && code[lookahead] == '(')
                    {
                        AppendColored(builder, token, "#FFD166");
                        continue;
                    }

                    if (char.IsUpper(token[0]))
                    {
                        AppendColored(builder, token, "#5CC8FF");
                        continue;
                    }

                    builder.Append(EscapeRichText(token));
                    continue;
                }

                builder.Append(EscapeRichText(code[i].ToString()));
                i++;
            }

            return builder.ToString();
        }

        private static void AppendColored(StringBuilder builder, string text, string color)
        {
            builder.Append("<color=");
            builder.Append(color);
            builder.Append(">");
            builder.Append(EscapeRichText(text));
            builder.Append("</color>");
        }

        private static string EscapeRichText(string text)
        {
            return text.Replace("<", "<\u200B");
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