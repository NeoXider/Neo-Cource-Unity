using System;
using System.Collections.Generic;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace UIMarkdownRenderer.ObjectRenderers
{
    public class CodeBlockRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, CodeBlock>
    {
        protected override void Write(UIMarkdownRenderer renderer, CodeBlock obj)
        {
            HtmlAttributes attribute = obj.GetAttributes();
            List<string> classes = new() { "codeblock" };
            if (attribute.Classes != null)
            {
                classes.AddRange(attribute.Classes);
            }

            // Expose fenced code language as a CSS class, e.g. language-quiz, language-truefalse
            if (obj is FencedCodeBlock fenced)
            {
                string info = fenced.Info;
                if (!string.IsNullOrWhiteSpace(info))
                {
                    // Markdig allows multiple info words; add class for the first token and the full raw info
                    string first = info.Trim().Split(' ')[0];
                    classes.Add($"language-{first}");
                    if (!string.Equals(first, info.Trim(), StringComparison.Ordinal))
                    {
                        classes.Add($"language-{info.Trim()}");
                    }

                    if (string.Equals(first, "quiz", StringComparison.OrdinalIgnoreCase))
                    {
                        classes.Add("quiz");
                    }
                }
            }

            renderer.StartBlock(classes);

            renderer.StartNewText();
            renderer.WriteLeafRawLines(obj);

            renderer.FinishBlock();
        }
    }
}