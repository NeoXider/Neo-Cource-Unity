using System.Collections.Generic;
using System.Globalization;
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

            // Нумерация идёт от числа, которое написал автор: список «3. …» начинается с 3, а список,
            // продолженный после абзаца или блока кода, не сбрасывается на 1.
            int orderedStart = 1;
            if (obj.IsOrdered && !int.TryParse(obj.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture,
                    out orderedStart))
            {
                orderedStart = 1;
            }

            renderer.IndentLevel++;
            for (int i = 0; i < obj.Count; i++)
            {
                string indent = new(' ', (renderer.IndentLevel - 1) * 2);
                renderer.WriteText(indent);
                renderer.WriteText(obj.IsOrdered
                    ? (orderedStart + i).ToString(CultureInfo.InvariantCulture) + ". "
                    : "\u2022 ");
                renderer.WriteChildren(obj[i] as ListItemBlock);
            }

            renderer.LockTextCreation = lockValue;

            renderer.IndentLevel--;
            renderer.FinishBlock();
        }
    }
}
