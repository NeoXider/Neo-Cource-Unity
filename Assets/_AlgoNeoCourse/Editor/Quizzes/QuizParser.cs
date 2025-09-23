using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizParser
    {
        private static readonly Regex QuizBlockRegex = new Regex(
            "```quiz\\s*\\n([\\s\\S]*?)\\n```",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static List<QuizQuestion> ParseQuestions(string markdown)
        {
            var result = new List<QuizQuestion>();
            if (string.IsNullOrEmpty(markdown)) return result;

            foreach (Match m in QuizBlockRegex.Matches(markdown))
            {
                string body = m.Groups[1].Value;
                var q = ParseSingleQuestion(body);
                if (q != null) result.Add(q);
            }
            return result;
        }

        private static QuizQuestion ParseSingleQuestion(string body)
        {
            // Простейший парсер YAML‑подобного блока
            // Поддержка ключей: id, kind, text, answers: - text: .., correct: true
            string id = null;
            string kindStr = null;
            string text = null;
            var answers = new List<(string text, bool correct)>();

            var lines = body.Replace("\r\n", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i].TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

                if (StartsWithKey(line, "id:")) { id = GetValue(line); i++; continue; }
                if (StartsWithKey(line, "kind:")) { kindStr = GetValue(line); i++; continue; }
                if (StartsWithKey(line, "text:"))
                {
                    text = GetValue(line);
                    // поддержка многострочного текста: строки, начинающиеся с двух пробелов, считаем продолжением
                    int j = i + 1;
                    while (j < lines.Length && lines[j].StartsWith("  "))
                    {
                        text += "\n" + lines[j].Trim();
                        j++;
                    }
                    i = j;
                    continue;
                }
                if (StartsWithKey(line, "answers:"))
                {
                    i++;
                    while (i < lines.Length && lines[i].TrimStart().StartsWith("- "))
                    {
                        string aLine = lines[i].TrimStart().Substring(2); // после "- "
                        string aText = null; bool aCorrect = false;

                        // Возможны два формата: "- text: X" и последующие подстроки с отступом
                        if (StartsWithKey(aLine, "text:")) aText = GetValue(aLine);
                        if (StartsWithKey(aLine, "correct:")) aCorrect = IsTrue(GetValue(aLine));

                        int j = i + 1;
                        while (j < lines.Length && lines[j].StartsWith("    "))
                        {
                            string cont = lines[j].Trim();
                            if (StartsWithKey(cont, "text:")) aText = GetValue(cont);
                            else if (StartsWithKey(cont, "correct:")) aCorrect = IsTrue(GetValue(cont));
                            j++;
                        }

                        if (!string.IsNullOrEmpty(aText)) answers.Add((aText, aCorrect));
                        i = j;
                    }
                    continue;
                }

                i++;
            }

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(kindStr) || string.IsNullOrEmpty(text) || answers.Count == 0)
                return null;

            var q = new QuizQuestion
            {
                id = id,
                kind = ParseKind(kindStr),
                text = text,
                answers = answers.Select((a, idx) => new QuizAnswer { id = "a" + idx, text = a.text, isCorrect = a.correct }).ToList()
            };
            return q;
        }

        private static bool StartsWithKey(string line, string key)
        {
            return line.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetValue(string line)
        {
            int c = line.IndexOf(':');
            if (c < 0) return string.Empty;
            return line.Substring(c + 1).Trim();
        }

        private static bool IsTrue(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static QuizKind ParseKind(string kind)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "single": return QuizKind.Single;
                case "multiple": return QuizKind.Multiple;
                case "truefalse": return QuizKind.TrueFalse;
                default: return QuizKind.Single;
            }
        }

        public static string ReplaceQuizBlocksWithMarkers(string markdown, out List<QuizQuestion> questions)
        {
            questions = new List<QuizQuestion>();
            if (string.IsNullOrEmpty(markdown)) return markdown;
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string result = QuizBlockRegex.Replace(markdown, match =>
            {
                string body = match.Groups[1].Value;
                var q = ParseSingleQuestion(body);
                if (q == null || string.IsNullOrEmpty(q.id))
                {
                    return match.Value; // оставить как есть, если парс не удался
                }
                // добавить в список и вернуть маркер
                string baseId = q.id;
                string finalId = baseId;
                int dup = 1;
                while (usedIds.Contains(finalId))
                {
                    finalId = baseId + "-" + (++dup);
                }
                usedIds.Add(finalId);
                if (!string.Equals(finalId, q.id, StringComparison.Ordinal))
                {
                    q.id = finalId;
                }
                // ВАЖНО: не использовать out-параметр в лямбде — локальный список и копирование после Replace
                _localQuestions.Add(q);
                return $"[[QUIZ:{q.id}]]";
            });

            // Перенесём локальные вопросы в out-параметр
            // (объявление списка делаем наверху файла вне лямбды)
            questions.AddRange(_localQuestions);
            _localQuestions.Clear();

            return result;
        }

        // локальное хранилище на время Replace — чтобы не трогать out-параметр внутри лямбды
        [ThreadStatic]
        private static List<QuizQuestion> _localQuestions = new List<QuizQuestion>();
    }
}


