using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class EmphasisInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, EmphasisInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, EmphasisInline obj)
        {
            if (obj.DelimiterCount == 2)
            {
                // Выделение сохраняет цвет основного текста; насыщенность задаёт шрифт.
                renderer.WriteText("<b>");
            }
            else
            {
                renderer.WriteText("<i>");
            }

            renderer.WriteChildren(obj);

            if (obj.DelimiterCount == 2)
            {
                renderer.WriteText("</b>");
            }
            else
            {
                renderer.WriteText("</i>");
            }
        }
    }
}
