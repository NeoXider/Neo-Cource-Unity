using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace UIMarkdownRenderer.ObjectRenderers
{
    /// <summary>
    /// Разделитель, который разбор оставил в дереве нераспознанным, пишется как обычный текст. С включёнными
    /// pipe-таблицами это каждый символ <c>|</c> абзаца, который таблицей не стал (нет строки-разделителя под
    /// шапкой): Markdig оставляет такие символы узлами-контейнерами, а без этого рендера сами символы пропадали.
    /// </summary>
    public class DelimiterInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, DelimiterInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, DelimiterInline obj)
        {
            renderer.WriteText(obj.ToLiteral());
            renderer.WriteChildren(obj);
        }
    }
}
