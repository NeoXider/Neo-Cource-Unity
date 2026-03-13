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
                renderer.WriteText("<color=#FF9F1C><b>");
            }
            else
            {
                renderer.WriteText("<i>");
            }

            renderer.WriteChildren(obj);

            if (obj.DelimiterCount == 2)
            {
                renderer.WriteText("</b></color>");
            }
            else
            {
                renderer.WriteText("</i>");
            }
        }
    }
}