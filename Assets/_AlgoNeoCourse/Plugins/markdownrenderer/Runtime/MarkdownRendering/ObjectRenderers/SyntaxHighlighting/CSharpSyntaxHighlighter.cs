using System;
using System.Collections.Generic;
using System.Text;

namespace UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting
{
    /// <summary>
    /// C# highlighter — VS Code "Dark+" approximation: keywords, '//' comments, "..." and '...' strings,
    /// numbers, function calls, and PascalCase identifiers treated as types.
    /// </summary>
    public sealed class CSharpSyntaxHighlighter : ISyntaxHighlighter
    {
        // Loaded from Resources/SyntaxHighlighting/csharp.json (defaults if missing).
        //
        // RedoSchool customization — re-apply if this ThirdParty package is updated: the palette is an
        // INSTANCE field, not a static one. A static palette froze the colors for the whole editor
        // session, so a designer editing csharp.json saw no change until a domain reload; with an
        // instance field it is enough to build a fresh highlighter (see CodeBlockRenderer.ReloadPalettes).
        private readonly SyntaxPalette Palette;

        public CSharpSyntaxHighlighter() : this(null)
        {
        }

        public CSharpSyntaxHighlighter(SyntaxPalette palette)
        {
            Palette = palette ?? SyntaxPalette.Load("csharp");
        }

        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        public string[] Highlight(string[] dedentedLines)
        {
            string[] result = new string[dedentedLines.Length];
            bool inBlockComment = false; // carries an open /* ... */ across lines
            for (int i = 0; i < dedentedLines.Length; i++)
            {
                result[i] = HighlightLine(dedentedLines[i], ref inBlockComment);
            }

            return result;
        }

        private string HighlightLine(string code, ref bool inBlockComment)
        {
            StringBuilder builder = new();
            int i = 0;

            // Continuation of a multiline /* ... */ comment.
            if (inBlockComment)
            {
                int close = code.IndexOf("*/", StringComparison.Ordinal);
                if (close < 0)
                {
                    SyntaxHighlightText.AppendColored(builder, code, Palette.Comment);
                    return builder.ToString();
                }

                int end = close + 2;
                SyntaxHighlightText.AppendColored(builder, code[..end], Palette.Comment);
                inBlockComment = false;
                i = end;
            }

            for (; i < code.Length;)
            {
                if (i + 1 < code.Length && code[i] == '/' && code[i + 1] == '*')
                {
                    int start = i;
                    int close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        SyntaxHighlightText.AppendColored(builder, code[start..], Palette.Comment);
                        inBlockComment = true;
                        break;
                    }

                    int end = close + 2;
                    SyntaxHighlightText.AppendColored(builder, code[start..end], Palette.Comment);
                    i = end;
                    continue;
                }

                if (i + 1 < code.Length && code[i] == '/' && code[i + 1] == '/')
                {
                    int start = i;
                    i += 2;
                    while (i < code.Length && code[i] != '\n')
                    {
                        i++;
                    }

                    SyntaxHighlightText.AppendColored(builder, code[start..i], Palette.Comment);
                    continue;
                }

                if (code[i] == '"' || code[i] == '\'')
                {
                    char quote = code[i];
                    int start = i++;
                    while (i < code.Length)
                    {
                        if (code[i] == '\\' && i + 1 < code.Length)
                        {
                            i += 2;
                            continue;
                        }

                        if (code[i] == quote)
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    SyntaxHighlightText.AppendColored(builder, code[start..i], Palette.String);
                    continue;
                }

                if (char.IsDigit(code[i]))
                {
                    int start = i++;
                    while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '.' || code[i] == '_'))
                    {
                        i++;
                    }

                    SyntaxHighlightText.AppendColored(builder, code[start..i], Palette.Number);
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
                    if (Keywords.Contains(token))
                    {
                        SyntaxHighlightText.AppendColored(builder, token, Palette.Keyword);
                        continue;
                    }

                    int lookahead = i;
                    while (lookahead < code.Length && char.IsWhiteSpace(code[lookahead]) && code[lookahead] != '\n')
                    {
                        lookahead++;
                    }

                    if (lookahead < code.Length && code[lookahead] == '(')
                    {
                        SyntaxHighlightText.AppendColored(builder, token, Palette.Function);
                        continue;
                    }

                    if (char.IsUpper(token[0]))
                    {
                        SyntaxHighlightText.AppendColored(builder, token, Palette.Type);
                        continue;
                    }

                    builder.Append(SyntaxHighlightText.Escape(token));
                    continue;
                }

                builder.Append(SyntaxHighlightText.Escape(code[i].ToString()));
                i++;
            }

            return builder.ToString();
        }
    }
}
