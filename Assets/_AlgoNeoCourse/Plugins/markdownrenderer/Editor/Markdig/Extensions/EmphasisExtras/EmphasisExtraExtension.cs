// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using System.Diagnostics;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace Markdig.Extensions.EmphasisExtras
{
    /// <summary>
    ///     Extension for strikethrough, subscript, superscript, inserted and marked.
    /// </summary>
    /// <seealso cref="IMarkdownExtension" />
    public class EmphasisExtraExtension : IMarkdownExtension
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="EmphasisExtraExtension" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public EmphasisExtraExtension(EmphasisExtraOptions options = EmphasisExtraOptions.Default)
        {
            Options = options;
        }

        /// <summary>
        ///     Gets the options.
        /// </summary>
        public EmphasisExtraOptions Options { get; }

        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            EmphasisInlineParser? parser = pipeline.InlineParsers.FindExact<EmphasisInlineParser>();
            if (parser != null)
            {
                bool hasTilde = false;
                bool hasSup = false;
                bool hasPlus = false;
                bool hasEqual = false;

                bool requireTilde = (Options & EmphasisExtraOptions.Strikethrough) != 0 ||
                                    (Options & EmphasisExtraOptions.Subscript) != 0;

                bool requireSup = (Options & EmphasisExtraOptions.Superscript) != 0;
                bool requirePlus = (Options & EmphasisExtraOptions.Inserted) != 0;
                bool requireEqual = (Options & EmphasisExtraOptions.Marked) != 0;

                foreach (EmphasisDescriptor? emphasis in parser.EmphasisDescriptors)
                {
                    if (requireTilde && emphasis.Character == '~')
                    {
                        hasTilde = true;
                    }

                    if (requireSup && emphasis.Character == '^')
                    {
                        hasSup = true;
                    }

                    if (requirePlus && emphasis.Character == '+')
                    {
                        hasPlus = true;
                    }

                    if (requireEqual && emphasis.Character == '=')
                    {
                        hasEqual = true;
                    }
                }

                if (requireTilde && !hasTilde)
                {
                    int minimumCount = (Options & EmphasisExtraOptions.Subscript) != 0 ? 1 : 2;
                    int maximumCount = (Options & EmphasisExtraOptions.Strikethrough) != 0 ? 2 : 1;
                    parser.EmphasisDescriptors.Add(new EmphasisDescriptor('~', minimumCount, maximumCount, true));
                }

                if (requireSup && !hasSup)
                {
                    parser.EmphasisDescriptors.Add(new EmphasisDescriptor('^', 1, 1, true));
                }

                if (requirePlus && !hasPlus)
                {
                    parser.EmphasisDescriptors.Add(new EmphasisDescriptor('+', 2, 2, true));
                }

                if (requireEqual && !hasEqual)
                {
                    parser.EmphasisDescriptors.Add(new EmphasisDescriptor('=', 2, 2, true));
                }
            }
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                // Extend the rendering here.
                EmphasisInlineRenderer? emphasisRenderer =
                    htmlRenderer.ObjectRenderers.FindExact<EmphasisInlineRenderer>();
                if (emphasisRenderer != null)
                {
                    EmphasisInlineRenderer.GetTagDelegate previousTag = emphasisRenderer.GetTag;
                    emphasisRenderer.GetTag = inline => GetTag(inline) ?? previousTag(inline);
                }
            }
        }

        private string? GetTag(EmphasisInline emphasisInline)
        {
            char c = emphasisInline.DelimiterChar;
            switch (c)
            {
                case '~':
                    Debug.Assert(emphasisInline.DelimiterCount <= 2);
                    return emphasisInline.DelimiterCount == 2 ? "del" : "sub";
                case '^':
                    return "sup";
                case '+':
                    return "ins";
                case '=':
                    return "mark";
            }

            return null;
        }
    }
}