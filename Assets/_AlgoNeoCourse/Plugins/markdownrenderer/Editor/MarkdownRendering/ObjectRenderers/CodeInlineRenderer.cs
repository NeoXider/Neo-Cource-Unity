using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class CodeInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeInline obj)
        {
            // Decode HTML entities (&amp; → &, &lt; → <) and escape for Unity rich text.
            string decoded = WebUtility.HtmlDecode(obj.Content);
            string escaped = decoded.Replace("&", "&\u200B").Replace("<", "<\u200B");

            renderer.WriteText("<color=#B388FF>");
            renderer.WriteText(escaped);
            renderer.WriteText("</color>");
        }
    }
}