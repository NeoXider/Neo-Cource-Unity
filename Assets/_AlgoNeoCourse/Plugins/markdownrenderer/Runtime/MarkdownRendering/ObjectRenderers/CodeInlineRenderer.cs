using System.Net;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using UnityEngine;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class CodeInlineRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeInline>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeInline obj)
        {
            // Decode HTML entities and escape for Unity rich text.
            string decoded = WebUtility.HtmlDecode(obj.Content);
            string escaped = decoded.Replace("&", "&\u200B").Replace("<", "<\u200B");

            // Цвет задаёт потребитель до рендера: голубой бабл и тёмная панель требуют разного контраста.
            if (renderer.InlineCodeInheritsTextColor)
            {
                renderer.WriteText("<b>");
                renderer.WriteText(escaped);
                renderer.WriteText("</b>");
                return;
            }

            renderer.WriteText($"<color=#{ColorUtility.ToHtmlStringRGB(renderer.InlineCodeColor)}><b>");
            renderer.WriteText(escaped);
            renderer.WriteText("</b></color>");
        }
    }
}
