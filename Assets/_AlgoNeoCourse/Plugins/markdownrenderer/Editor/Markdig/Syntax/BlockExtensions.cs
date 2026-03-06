// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

namespace Markdig.Syntax
{
    /// <summary>
    ///     Extensions for <see cref="Block" />
    /// </summary>
    public static class BlockExtensions
    {
        // TODO: Add test for this code

        public static Block? FindBlockAtPosition(this Block rootBlock, int position)
        {
            bool contains = rootBlock.CompareToPosition(position) == 0;
            if (!(rootBlock is ContainerBlock blocks) || blocks.Count == 0 || !contains)
            {
                return contains ? rootBlock : null;
            }

            int lowerIndex = 0;
            int upperIndex = blocks.Count - 1;

            // binary search on lines
            Block? block = null;
            while (lowerIndex <= upperIndex)
            {
                int midIndex = (upperIndex - lowerIndex) / 2 + lowerIndex;
                block = blocks[midIndex];
                int comparison = block.CompareToPosition(position);
                if (comparison == 0)
                {
                    break;
                }

                block = null;
                if (comparison < 0)
                {
                    lowerIndex = midIndex + 1;
                }
                else
                {
                    upperIndex = midIndex - 1;
                }
            }

            if (block is null)
            {
                return rootBlock;
            }

            // Recursively go deep into the block
            return block.FindBlockAtPosition(position);
        }

        public static int FindClosestLine(this MarkdownDocument root, int line)
        {
            Block? closestBlock = root.FindClosestBlock(line);
            return closestBlock?.Line ?? 0;
        }

        public static Block? FindClosestBlock(this Block rootBlock, int line)
        {
            if (!(rootBlock is ContainerBlock blocks) || blocks.Count == 0)
            {
                return rootBlock.Line == line ? rootBlock : null;
            }

            int lowerIndex = 0;
            int upperIndex = blocks.Count - 1;

            // binary search on lines
            while (lowerIndex <= upperIndex)
            {
                int midIndex = (upperIndex - lowerIndex) / 2 + lowerIndex;
                Block block = blocks[midIndex];
                int comparison = block.Line.CompareTo(line);
                if (comparison == 0)
                {
                    return block;
                }

                if (comparison < 0)
                {
                    lowerIndex = midIndex + 1;
                }
                else
                {
                    upperIndex = midIndex - 1;
                }
            }

            // If we are between two lines, try to find the best spot
            if (lowerIndex > 0 && lowerIndex < blocks.Count)
            {
                Block prevBlock = blocks[lowerIndex - 1].FindClosestBlock(line) ?? blocks[lowerIndex - 1];
                Block nextBlock = blocks[lowerIndex].FindClosestBlock(line) ?? blocks[lowerIndex];

                if (prevBlock.Line == line)
                {
                    return prevBlock;
                }

                if (nextBlock.Line == line)
                {
                    return nextBlock;
                }

                // we calculate the position of the current line relative to the line found and previous line
                int prevLine = prevBlock.Line;
                int nextLine = nextBlock.Line;

                double middle = (line - prevLine) * 1.0 / (nextLine - prevLine);
                // If  relative position < 0.5, we select the previous line, otherwise we select the line found
                return middle < 0.5 ? prevBlock : nextBlock;
            }

            if (lowerIndex == 0)
            {
                Block prevBlock = blocks[lowerIndex].FindClosestBlock(line) ?? blocks[lowerIndex];
                return prevBlock;
            }

            if (lowerIndex == blocks.Count)
            {
                Block prevBlock = blocks[lowerIndex - 1].FindClosestBlock(line) ?? blocks[lowerIndex - 1];
                return prevBlock;
            }

            return null;
        }


        public static bool ContainsPosition(this Block block, int position)
        {
            return block.CompareToPosition(position) == 0;
        }

        public static int CompareToPosition(this Block block, int position)
        {
            return position < block.Span.Start ? 1 : position > block.Span.End + 1 ? -1 : 0;
        }
    }
}