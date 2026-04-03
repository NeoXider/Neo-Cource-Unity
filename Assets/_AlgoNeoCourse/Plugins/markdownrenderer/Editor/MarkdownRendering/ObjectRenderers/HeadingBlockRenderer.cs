using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class HeadingBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, HeadingBlock>
    {
        private static readonly string[] HeadingTexts =
        {
            "h1",
            "h2",
            "h3",
            "h4",
            "h5",
            "h6"
        };

        protected override void Write(UIMarkdownRenderer renderer, HeadingBlock obj)
        {
            int index = obj.Level - 1;
            string headingText = (uint)index < (uint)HeadingTexts.Length
                ? HeadingTexts[index]
                : "h" + obj.Level.ToString(CultureInfo.InvariantCulture);

            VisualElement newBlock = renderer.StartBlock();

            HtmlAttributes attribute = obj.GetAttributes();
            List<string> classes = new() { headingText, "header" };
            if (attribute.Classes != null)
            {
                classes.AddRange(attribute.Classes);
            }

            renderer.StartNewText(classes);
            renderer.WriteLeafBlockInline(obj);

            renderer.FinishBlock();

            // When LockTextCreation is on (heading inside a list item), StartBlock returns null — do not call Q on null.
            if (newBlock != null)
            {
                Label label = newBlock.Q<Label>();
                renderer.RegisterHeader(label.text, label);
            }
            else
            {
                Label listLabel = renderer.CurrentBlockTextLabel;
                if (listLabel != null)
                {
                    string plain = GetHeadingPlainText(obj);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        renderer.RegisterHeader(plain, listLabel);
                    }
                }
            }
        }

        private static string GetHeadingPlainText(HeadingBlock obj)
        {
            if (obj.Inline?.FirstChild == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            AppendInlinesPlain(obj.Inline.FirstChild, sb);
            return sb.ToString();
        }

        private static void AppendInlinesPlain(Inline inline, StringBuilder sb)
        {
            for (; inline != null; inline = inline.NextSibling)
            {
                switch (inline)
                {
                    case LiteralInline lit:
                        sb.Append(lit.Content.ToString());
                        break;
                    case CodeInline code:
                        sb.Append(code.Content);
                        break;
                    case AutolinkInline auto:
                        sb.Append(auto.Url);
                        break;
                    case LinkInline link:
                        AppendInlinesPlain(link.FirstChild, sb);
                        break;
                    case EmphasisInline emph:
                        AppendInlinesPlain(emph.FirstChild, sb);
                        break;
                    case LineBreakInline:
                        sb.Append(' ');
                        break;
                    case HtmlEntityInline entity:
                        sb.Append(entity.Transcoded.ToString());
                        break;
                    case HtmlInline html:
                        sb.Append(html.Tag);
                        break;
                    case ContainerInline container when container.FirstChild != null:
                        AppendInlinesPlain(container.FirstChild, sb);
                        break;
                }
            }
        }
    }
}