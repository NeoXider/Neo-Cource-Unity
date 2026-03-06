// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markdig.Extensions.Footnotes
{
    /// <summary>
    ///     The block parser for a <see cref="Footnote" />.
    /// </summary>
    /// <seealso cref="BlockParser" />
    public class FootnoteParser : BlockParser
    {
        /// <summary>
        ///     The key used to store at the document level the pending <see cref="FootnoteGroup" />
        /// </summary>
        private static readonly object DocumentKey = typeof(Footnote);

        public FootnoteParser()
        {
            OpeningCharacters = new[] { '[' };
        }

        public override BlockState TryOpen(BlockProcessor processor)
        {
            return TryOpen(processor, false);
        }

        private BlockState TryOpen(BlockProcessor processor, bool isContinue)
        {
            // We expect footnote to appear only at document level and not indented more than a code indent block
            ContainerBlock currentContainer = processor.GetCurrentContainerOpened();
            if (processor.IsCodeIndent || (!isContinue && currentContainer.GetType() != typeof(MarkdownDocument)) ||
                (isContinue && !(currentContainer is FootnoteGroup)))
            {
                return BlockState.None;
            }

            int saved = processor.Column;
            int start = processor.Start;
            if (!LinkHelper.TryParseLabel(ref processor.Line, false, out string? label, out SourceSpan labelSpan) ||
                !label.StartsWith("^") || processor.CurrentChar != ':')
            {
                processor.GoToColumn(saved);
                return BlockState.None;
            }

            // Advance the column
            int deltaColumn = processor.Start - start;
            processor.Column = processor.Column + deltaColumn;

            processor.NextChar(); // Skip ':'

            Footnote footnote = new(this)
            {
                Label = label,
                LabelSpan = labelSpan
            };

            // Maintain a list of all footnotes at document level
            FootnoteGroup? footnotes = processor.Document.GetData(DocumentKey) as FootnoteGroup;
            if (footnotes == null)
            {
                footnotes = new FootnoteGroup(this);
                processor.Document.Add(footnotes);
                processor.Document.SetData(DocumentKey, footnotes);
                processor.Document.ProcessInlinesEnd += Document_ProcessInlinesEnd;
            }

            footnotes.Add(footnote);

            FootnoteLinkReferenceDefinition linkRef = new(footnote)
            {
                CreateLinkInline = CreateLinkToFootnote,
                Line = processor.LineIndex,
                Span = new SourceSpan(start, processor.Start - 2), // account for ]:
                LabelSpan = labelSpan,
                Label = label
            };
            processor.Document.SetLinkReferenceDefinition(footnote.Label, linkRef, true);
            processor.NewBlocks.Push(footnote);
            return BlockState.Continue;
        }

        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            Footnote footnote = (Footnote)block;

            if (processor.CurrentBlock == null || processor.CurrentBlock.IsBreakable)
            {
                if (processor.IsBlankLine)
                {
                    footnote.IsLastLineEmpty = true;
                    return BlockState.ContinueDiscard;
                }

                if (processor.Column == 0)
                {
                    if (footnote.IsLastLineEmpty)
                    {
                        // Close the current footnote
                        processor.Close(footnote);

                        // Parse any opening footnote
                        return TryOpen(processor);
                    }

                    // Make sure that consecutive footnotes without a blanklines are parsed correctly
                    if (TryOpen(processor, true) == BlockState.Continue)
                    {
                        processor.Close(footnote);
                        return BlockState.Continue;
                    }
                }
            }

            footnote.IsLastLineEmpty = false;

            if (processor.IsCodeIndent)
            {
                processor.GoToCodeIndent();
            }

            return BlockState.Continue;
        }

        /// <summary>
        ///     Add footnotes to the end of the document
        /// </summary>
        /// <param name="state">The processor.</param>
        /// <param name="inline">The inline.</param>
        private void Document_ProcessInlinesEnd(InlineProcessor state, Inline? inline)
        {
            // Unregister
            state.Document.ProcessInlinesEnd -= Document_ProcessInlinesEnd;

            FootnoteGroup footnotes = (FootnoteGroup)state.Document.GetData(DocumentKey)!;
            // Remove the footnotes from the document and readd them at the end
            state.Document.Remove(footnotes);
            state.Document.Add(footnotes);
            state.Document.RemoveData(DocumentKey);

            footnotes.Sort((leftObj, rightObj) =>
            {
                Footnote? left = (Footnote)leftObj;
                Footnote? right = (Footnote)rightObj;

                return left.Order >= 0 && right.Order >= 0
                    ? left.Order.CompareTo(right.Order)
                    : 0;
            });

            int linkIndex = 0;
            for (int i = 0; i < footnotes.Count; i++)
            {
                Footnote footnote = (Footnote)footnotes[i];
                if (footnote.Order < 0)
                {
                    // Remove this footnote if it doesn't have any links
                    footnotes.RemoveAt(i);
                    i--;
                    continue;
                }

                // Insert all footnote backlinks
                ParagraphBlock? paragraphBlock = footnote.LastChild as ParagraphBlock;
                if (paragraphBlock is null)
                {
                    paragraphBlock = new ParagraphBlock();
                    footnote.Add(paragraphBlock);
                }

                if (paragraphBlock.Inline == null)
                {
                    paragraphBlock.Inline = new ContainerInline();
                }

                foreach (FootnoteLink? link in footnote.Links)
                {
                    linkIndex++;
                    link.Index = linkIndex;
                    FootnoteLink backLink = new(footnote)
                    {
                        Index = linkIndex,
                        IsBackLink = true
                    };
                    paragraphBlock.Inline.AppendChild(backLink);
                }
            }
        }

        private static Inline CreateLinkToFootnote(InlineProcessor state, LinkReferenceDefinition linkRef,
            Inline? child)
        {
            Footnote footnote = ((FootnoteLinkReferenceDefinition)linkRef).Footnote;
            if (footnote.Order < 0)
            {
                FootnoteGroup footnotes = (FootnoteGroup)state.Document.GetData(DocumentKey)!;
                footnotes.CurrentOrder++;
                footnote.Order = footnotes.CurrentOrder;
            }

            FootnoteLink link = new(footnote);
            footnote.Links.Add(link);

            return link;
        }
    }
}