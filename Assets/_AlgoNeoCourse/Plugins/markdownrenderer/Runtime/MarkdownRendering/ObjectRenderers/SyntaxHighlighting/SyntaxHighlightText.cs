using System.Text;

namespace UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting
{
    /// <summary>
    /// VS Code "Dark+" DEFAULT colors. These are the fallback values for every <see cref="SyntaxPalette"/>
    /// (each language loads its own JSON that may override them) and the color of inline `code`.
    /// (RedoSchool customization.)
    /// </summary>
    public static class VsCodePalette
    {
        public const string Comment = "#6A9955";
        public const string String = "#CE9178";
        public const string Number = "#B5CEA8";
        public const string Keyword = "#569CD6";
        public const string Function = "#DCDCAA";
        public const string Type = "#4EC9B0";
        public const string Variable = "#9CDCFE";

        /// <summary>Inline `code` spans inside prose (rendered as a rich-text color span, not via USS).</summary>
        // Встроенный код на тёмном фоне использует циановый акцент брендовой палитры.
        public const string InlineCode = "#48EEFF";
    }

    /// <summary>Shared rich-text helpers for highlighters.</summary>
    public static class SyntaxHighlightText
    {
        public static void AppendColored(StringBuilder builder, string text, string color)
        {
            builder.Append("<color=");
            builder.Append(color);
            builder.Append('>');
            builder.Append(Escape(text));
            builder.Append("</color>");
        }

        /// <summary>
        /// Escape '&amp;' first to stop Unity TextCore from interpreting HTML entities, then '&lt;' to
        /// stop rich-text tag interpretation. A zero-width space breaks the sequence without showing.
        /// </summary>
        public static string Escape(string text) => text.Replace("&", "&​").Replace("<", "<​");
    }
}
