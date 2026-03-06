// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markdig.Extensions.Globalization
{
    /// <summary>
    ///     Extension to add support for RTL content.
    /// </summary>
    public class GlobalizationExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // Make sure we don't have a delegate twice
            pipeline.DocumentProcessed -= Pipeline_DocumentProcessed;
            pipeline.DocumentProcessed += Pipeline_DocumentProcessed;
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }

        private void Pipeline_DocumentProcessed(MarkdownDocument document)
        {
            foreach (MarkdownObject? node in document.Descendants())
            {
                if (node is TableRow || node is TableCell || node is ListItemBlock)
                {
                    continue;
                }

                if (ShouldBeRightToLeft(node))
                {
                    HtmlAttributes attributes = node.GetAttributes();
                    attributes.AddPropertyIfNotExist("dir", "rtl");

                    if (node is Table)
                    {
                        attributes.AddPropertyIfNotExist("align", "right");
                    }
                }
            }
        }

        private static bool ShouldBeRightToLeft(MarkdownObject item)
        {
            if (item is IEnumerable<MarkdownObject> container)
            {
                foreach (MarkdownObject? child in container)
                {
                    // TaskList items contain an "X", which will cause
                    // the function to always return false.
                    if (child is TaskList)
                    {
                        continue;
                    }

                    return ShouldBeRightToLeft(child);
                }
            }
            else if (item is LeafBlock leaf)
            {
                return ShouldBeRightToLeft(leaf.Inline!);
            }
            else if (item is LiteralInline literal)
            {
                return StartsWithRtlCharacter(literal.Content);
            }

            foreach (ParagraphBlock? paragraph in item.Descendants<ParagraphBlock>())
            {
                foreach (Inline inline in paragraph.Inline!)
                {
                    if (inline is LiteralInline literal)
                    {
                        return StartsWithRtlCharacter(literal.Content);
                    }
                }
            }

            return false;
        }

        private static bool StartsWithRtlCharacter(StringSlice slice)
        {
            for (int i = slice.Start; i <= slice.End; i++)
            {
                if (slice[i] < 128)
                {
                    continue;
                }

                int rune;
                if (CharHelper.IsHighSurrogate(slice[i]) && i < slice.End && CharHelper.IsLowSurrogate(slice[i + 1]))
                {
                    Debug.Assert(char.IsSurrogatePair(slice[i], slice[i + 1]));
                    rune = char.ConvertToUtf32(slice[i], slice[i + 1]);
                }
                else
                {
                    rune = slice[i];
                }

                if (CharHelper.IsRightToLeft(rune))
                {
                    return true;
                }

                if (CharHelper.IsLeftToRight(rune))
                {
                    return false;
                }
            }

            return false;
        }
    }
}