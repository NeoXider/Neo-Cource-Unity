using System;
using System.Collections.Generic;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizUtils
    {
        public static void Shuffle<T>(IList<T> list, int seed)
        {
            var rng = new Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}


