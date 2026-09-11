using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizUtils
    {
        public static void Shuffle<T>(IList<T> list, int seed)
        {
            Random rng = new(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Стабильный сид из строки: string.GetHashCode() рандомизирован между запусками процесса,
        // поэтому для «стабильного» перемешивания используем SHA256 (первые 4 байта хеша).
        public static int StableSeed(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                if (hash == null || hash.Length < 4)
                {
                    return 0;
                }

                return BitConverter.ToInt32(hash, 0);
            }
        }
    }
}