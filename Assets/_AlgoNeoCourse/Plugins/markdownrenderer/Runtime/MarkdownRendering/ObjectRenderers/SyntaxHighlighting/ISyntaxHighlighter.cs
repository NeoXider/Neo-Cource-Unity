namespace UIMarkdownRenderer.ObjectRenderers.SyntaxHighlighting
{
    /// <summary>
    /// Per-language source highlighter. ONE implementation per language keeps languages isolated, so
    /// adding a new language is just: add a file implementing this interface + register it in
    /// <see cref="CodeBlockRenderer"/>.
    /// <para>
    /// <see cref="Highlight"/> receives ALL already-dedented code lines of one block at once (so a
    /// language can carry state across lines, e.g. a Python triple-quoted docstring or a C# block
    /// comment) and returns one Unity rich-text string per input line.
    /// </para>
    /// </summary>
    public interface ISyntaxHighlighter
    {
        public string[] Highlight(string[] dedentedLines);
    }
}
