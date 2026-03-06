// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Markdig.Helpers;

namespace Markdig.Parsers
{
    /// <summary>
    ///     Base class for a list of parsers.
    /// </summary>
    /// <typeparam name="T">Type of the parser</typeparam>
    /// <typeparam name="TState">The type of the parser state.</typeparam>
    /// <seealso cref="OrderedList{T}" />
    public abstract class ParserList<T, TState> : OrderedList<T> where T : notnull, ParserBase<TState>
    {
        private readonly CharacterMap<T[]> charMap;

        protected ParserList(IEnumerable<T> parsersArg) : base(parsersArg)
        {
            Dictionary<char, int> charCounter = new();
            int globalCounter = 0;

            for (int i = 0; i < Count; i++)
            {
                T? parser = this[i];
                if (parser is null)
                {
                    ThrowHelper.InvalidOperationException("Unexpected null parser found");
                }

                parser.Initialize();
                parser.Index = i;
                if (parser.OpeningCharacters is { Length: > 0 })
                {
                    foreach (char openingChar in parser.OpeningCharacters)
                    {
                        if (!charCounter.ContainsKey(openingChar))
                        {
                            charCounter[openingChar] = 0;
                        }

                        charCounter[openingChar]++;
                    }
                }
                else
                {
                    globalCounter++;
                }
            }

            if (globalCounter > 0)
            {
                GlobalParsers = new T[globalCounter];
            }

            Dictionary<char, T[]> tempCharMap = new();
            foreach (T? parser in this)
            {
                if (parser.OpeningCharacters is { Length: > 0 })
                {
                    foreach (char openingChar in parser.OpeningCharacters)
                    {
                        if (!tempCharMap.TryGetValue(openingChar, out T[]? parsers))
                        {
                            parsers = new T[charCounter[openingChar]];
                            tempCharMap[openingChar] = parsers;
                        }

                        int index = parsers.Length - charCounter[openingChar];
                        parsers[index] = parser;
                        charCounter[openingChar]--;
                    }
                }
                else
                {
                    GlobalParsers![GlobalParsers.Length - globalCounter] = parser;
                    globalCounter--;
                }
            }

            charMap = new CharacterMap<T[]>(tempCharMap);
        }

        /// <summary>
        ///     Gets the list of global parsers (that don't have any opening characters defined)
        /// </summary>
        public T[]? GlobalParsers { get; }

        /// <summary>
        ///     Gets all the opening characters defined.
        /// </summary>
        public char[] OpeningCharacters => charMap.OpeningCharacters;

        /// <summary>
        ///     Gets the list of parsers valid for the specified opening character.
        /// </summary>
        /// <param name="openingChar">The opening character.</param>
        /// <returns>A list of parsers valid for the specified opening character or null if no parsers registered.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[]? GetParsersForOpeningCharacter(uint openingChar)
        {
            return charMap[openingChar];
        }

        /// <summary>
        ///     Searches for an opening character from a registered parser in the specified string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <returns>
        ///     Index position within the string of the first opening character found in the specified text; if not found,
        ///     returns -1
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfOpeningCharacter(string text, int start, int end)
        {
            return charMap.IndexOfOpeningCharacter(text, start, end);
        }
    }
}