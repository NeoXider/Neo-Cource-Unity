using System.Collections.Generic;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class ListBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, ListBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, ListBlock obj)
        {
            renderer.StartBlock();

            HtmlAttributes attribute = obj.GetAttributes();
            List<string> classes = new() { "list" };
            if (attribute.Classes != null)
            {
                classes.AddRange(attribute.Classes);
            }

            renderer.StartNewText(classes);

            bool lockValue = renderer.LockTextCreation;
            renderer.LockTextCreation = true;

            renderer.IndentLevel++;
            for (int i = 0; i < obj.Count; i++)
            {
                string indent = new(' ', (renderer.IndentLevel - 1) * 2);
                renderer.WriteText(indent);
                renderer.WriteText(obj.IsOrdered ? i + 1 + ". " : "\u2022 ");
                renderer.WriteChildren(obj[i] as ListItemBlock);
            }

            renderer.LockTextCreation = lockValue;

            renderer.IndentLevel--;
            renderer.FinishBlock();
        }
    }
}