using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NeoCource.Editor.Validation
{
    // Одна проблема, найденная валидатором урока.
    [Serializable]
    public class ValidationIssue
    {
        public string lessonPath;
        public string lessonTitle;
        public int slideIndex; // 1-based номер слайда
        public string rule;
        public string message;
        public string severity; // "error" / "warning"
    }

    // Лёгкий валидатор .md-уроков. Свой построчный парс quiz/check-блоков,
    // чужой QuizParser НЕ трогаем. Валидатор никогда не бросает исключения.
    public static class LessonValidator
    {
        private static readonly Regex QuizBlockRegex =
            new(@"```quiz\s*\n([\s\S]*?)\n```", RegexOptions.Compiled);

        private static readonly Regex CheckBlockRegex =
            new(@"```check\s*\n([\s\S]*?)\n```", RegexOptions.Compiled);

        private static readonly Regex MediaRegex =
            new(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);

        private static readonly Regex UnityLinkRegex =
            new(@"\]\( *?(unity://[^)]+?) *?\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AnswerLineRegex =
            new(@"^\s*-\s*text\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Вырезание fenced-блоков: примеры синтаксиса внутри ```md не должны
        // давать ложных срабатываний media/unity-проверок (там это не ссылки).
        private static readonly Regex FenceStripRegex =
            new(@"```[\s\S]*?```|~~~[\s\S]*?~~~", RegexOptions.Compiled);

        private static readonly Regex CorrectLineRegex =
            new(@"^\s*correct\s*:\s*(.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<ValidationIssue> ValidateLessonFile(string lessonFilePath, string lessonTitle)
        {
            List<ValidationIssue> issues = new();
            try
            {
                if (string.IsNullOrWhiteSpace(lessonFilePath) || !File.Exists(lessonFilePath))
                {
                    issues.Add(Error(lessonFilePath, lessonTitle, 0, "read",
                        "Файл урока не найден."));
                    return issues;
                }

                string text = File.ReadAllText(lessonFilePath);
                List<string> slides = SplitSlidesFenceAware(text);

                // Дубли id отслеживаем в пределах всего урока.
                Dictionary<string, int> quizIdToSlide =
                    new(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < slides.Count; i++)
                {
                    int slideIndex = i + 1;
                    try
                    {
                        ValidateQuizBlocks(slides[i], lessonFilePath, lessonTitle, slideIndex,
                            issues, quizIdToSlide);
                        ValidateCheckBlocks(slides[i], lessonFilePath, lessonTitle, slideIndex, issues);
                        // Медиа и unity-ссылки ищем только в прозе слайда,
                        // без fenced-блоков (примеры кода — не ссылки).
                        string prose = StripFences(slides[i]);
                        ValidateMedia(prose, lessonFilePath, lessonTitle, slideIndex, issues);
                        ValidateUnityLinks(prose, lessonFilePath, lessonTitle, slideIndex, issues);
                    }
                    catch (Exception ex)
                    {
                        // Один битый слайд не должен ронять проверку остальных.
                        issues.Add(Error(lessonFilePath, lessonTitle, slideIndex, "exception",
                            "Ошибка проверки слайда: " + ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(Error(lessonFilePath, lessonTitle, 0, "exception",
                    "Ошибка валидации файла: " + ex.Message));
            }

            return issues;
        }

        // Простая fence-aware нарезка на слайды: строки ``` / ~~~ переключают
        // состояние "внутри блока", разделитель --- учитываем только снаружи.
        private static List<string> SplitSlidesFenceAware(string md)
        {
            List<string> slides = new();
            List<string> current = new();
            bool inFence = false;
            string fenceMarker = null;

            string[] lines = (md ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    string marker = trimmed.StartsWith("```") ? "```" : "~~~";
                    if (!inFence)
                    {
                        inFence = true;
                        fenceMarker = marker;
                    }
                    else if (string.Equals(marker, fenceMarker, StringComparison.Ordinal))
                    {
                        inFence = false;
                        fenceMarker = null;
                    }

                    current.Add(raw);
                    continue;
                }

                if (!inFence && string.Equals(trimmed, "---", StringComparison.Ordinal))
                {
                    slides.Add(string.Join("\n", current));
                    current.Clear();
                    continue;
                }

                current.Add(raw);
            }

            slides.Add(string.Join("\n", current));
            slides.RemoveAll(string.IsNullOrWhiteSpace);
            return slides;
        }

        private static void ValidateQuizBlocks(string slide, string lessonPath, string lessonTitle,
            int slideIndex, List<ValidationIssue> issues, Dictionary<string, int> quizIdToSlide)
        {
            foreach (Match match in QuizBlockRegex.Matches(slide))
            {
                string id = null;
                string kind = null;
                string text = null;
                int answerCount = 0;
                int correctCount = 0;
                bool inAnswers = false;
                bool lastAnswerHasCorrect = true;

                foreach (string rawLine in match.Groups[1].Value.Split('\n'))
                {
                    string line = rawLine.Trim();
                    if (!inAnswers)
                    {
                        if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                        {
                            id = Unquote(line.Substring(3).Trim());
                        }
                        else if (line.StartsWith("kind:", StringComparison.OrdinalIgnoreCase))
                        {
                            kind = Unquote(line.Substring(5).Trim());
                        }
                        else if (line.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
                        {
                            text = Unquote(line.Substring(5).Trim());
                        }
                        else if (line.StartsWith("answers:", StringComparison.OrdinalIgnoreCase))
                        {
                            inAnswers = true;
                        }

                        continue;
                    }

                    if (AnswerLineRegex.IsMatch(line))
                    {
                        answerCount++;
                        // correct может быть в той же строке: "- text: A (correct: true)".
                        lastAnswerHasCorrect = line.IndexOf("correct:", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                               IsTrueValue(ExtractInlineCorrect(line));
                        if (lastAnswerHasCorrect)
                        {
                            correctCount++;
                        }
                    }
                    else
                    {
                        Match correct = CorrectLineRegex.Match(line);
                        if (correct.Success)
                        {
                            if (IsTrueValue(correct.Groups[1].Value) && !lastAnswerHasCorrect)
                            {
                                correctCount++;
                            }

                            lastAnswerHasCorrect = true;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-id-empty",
                        "quiz-блок без id."));
                }
                else if (quizIdToSlide.TryGetValue(id, out int firstSlide))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-duplicate-id",
                        $"Дубль id '{id}' (первое вхождение — слайд {firstSlide})."));
                }
                else
                {
                    quizIdToSlide[id] = slideIndex;
                }

                if (string.IsNullOrWhiteSpace(kind))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-kind-empty",
                        $"quiz '{id ?? "?"}' без kind."));
                }
                else if (!IsKnownKind(kind))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-unknown-kind",
                        $"quiz '{id ?? "?"}': неизвестный kind '{kind}' (нужен single/multiple/truefalse)."));
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-text-empty",
                        $"quiz '{id ?? "?"}' без text."));
                }

                if (answerCount == 0)
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-no-answers",
                        $"quiz '{id ?? "?"}': нет answers."));
                }
                else if (IsSingleOrMultiple(kind) && correctCount == 0)
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "quiz-no-correct",
                        $"quiz '{id ?? "?"}' ({kind}): нет ответа с correct: true."));
                }

                if (string.Equals(kind, "truefalse", StringComparison.OrdinalIgnoreCase) &&
                    answerCount != 2)
                {
                    issues.Add(Warning(lessonPath, lessonTitle, slideIndex, "quiz-truefalse-answers",
                        $"quiz '{id ?? "?"}' (truefalse): ответов {answerCount}, обычно нужно 2."));
                }
            }
        }

        private static void ValidateCheckBlocks(string slide, string lessonPath, string lessonTitle,
            int slideIndex, List<ValidationIssue> issues)
        {
            foreach (Match match in CheckBlockRegex.Matches(slide))
            {
                foreach (string rawLine in match.Groups[1].Value.Split('\n'))
                {
                    // Правила — элементы списка: "- filename: ...".
                    string line = rawLine.Trim();
                    if (line.StartsWith("-"))
                    {
                        line = line.Substring(1).Trim();
                    }

                    if (line.StartsWith("filename:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(Unquote(line.Substring(9).Trim())))
                        {
                            issues.Add(Error(lessonPath, lessonTitle, slideIndex,
                                "check-filename-empty", "check-блок: пустой filename."));
                        }
                    }
                    else if (line.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(Unquote(line.Substring(9).Trim())))
                        {
                            issues.Add(Warning(lessonPath, lessonTitle, slideIndex,
                                "check-contains-empty", "check-блок: пустой contains."));
                        }
                    }
                    else if (line.StartsWith("object_exists:", StringComparison.OrdinalIgnoreCase) ||
                             line.StartsWith("object-exists:", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = line.IndexOf(':');
                        if (string.IsNullOrWhiteSpace(Unquote(line.Substring(colon + 1).Trim())))
                        {
                            issues.Add(Error(lessonPath, lessonTitle, slideIndex,
                                "check-object-exists-empty", "check-блок: пустой object_exists."));
                        }
                    }
                }
            }
        }

        private static void ValidateMedia(string slide, string lessonPath, string lessonTitle,
            int slideIndex, List<ValidationIssue> issues)
        {
            string lessonDir;
            try
            {
                lessonDir = Path.GetDirectoryName(Path.GetFullPath(lessonPath));
            }
            catch
            {
                return;
            }

            string projectRoot = null;
            try
            {
                projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch
            {
                /* без корня проверяем только относительно папки урока */
            }

            foreach (Match match in MediaRegex.Matches(slide))
            {
                string src = match.Groups[1].Value.Trim();
                // Отрезаем возможный title: ![](path "title").
                int space = src.IndexOfAny(new[] { ' ', '\t' });
                if (space >= 0)
                {
                    src = src.Substring(0, space);
                }

                src = Unquote(src.Trim());
                if (string.IsNullOrEmpty(src))
                {
                    continue;
                }

                // Внешние и специальные схемы пропускаем.
                if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                    src.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool exists = false;
                try
                {
                    if (Path.IsPathRooted(src))
                    {
                        exists = File.Exists(src);
                    }
                    else
                    {
                        exists = File.Exists(Path.Combine(lessonDir, src.Replace('/', Path.DirectorySeparatorChar)));
                        if (!exists && projectRoot != null &&
                            (src.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                             src.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)))
                        {
                            exists = File.Exists(Path.Combine(projectRoot,
                                src.Replace('/', Path.DirectorySeparatorChar)));
                        }
                    }
                }
                catch
                {
                    exists = false;
                }

                if (!exists)
                {
                    issues.Add(Warning(lessonPath, lessonTitle, slideIndex, "media-missing",
                        $"Медиа не найдено: '{src}'."));
                }
            }
        }

        private static void ValidateUnityLinks(string slide, string lessonPath, string lessonTitle,
            int slideIndex, List<ValidationIssue> issues)
        {
            foreach (Match match in UnityLinkRegex.Matches(slide))
            {
                string link = match.Groups[1].Value.Trim();
                string withoutScheme = link.Substring("unity://".Length);
                string action;
                string query = string.Empty;
                int separator = withoutScheme.IndexOf('?');
                if (separator >= 0)
                {
                    action = withoutScheme.Substring(0, separator).Trim();
                    query = withoutScheme.Substring(separator + 1);
                }
                else
                {
                    action = withoutScheme.Trim();
                }

                if (string.Equals(action, "slide", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "unity-slide-removed",
                        "Ссылка unity://slide удалена в 1.6.0."));
                    continue;
                }

                if (string.Equals(action, "check", StringComparison.OrdinalIgnoreCase))
                {
                    // Авторские unity://check-ссылки удалены в 1.6.1: пишите ```check-блоки,
                    // кнопка «Проверить» подставится сама. (unity://check остался лишь
                    // внутренним транспортом этих кнопок.)
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "unity-check-removed",
                        "Ссылка unity://check удалена в 1.6.1 — используйте ```check-блок."));
                    continue;
                }

                if (!string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "unity-unknown-action",
                        $"Неизвестный action unity-ссылки: '{action}' (нужен open)."));
                    continue;
                }

                if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase) &&
                    !ParseQuery(query).ContainsKey("path"))
                {
                    issues.Add(Error(lessonPath, lessonTitle, slideIndex, "unity-open-no-path",
                        "unity://open без path=."));
                }
            }
        }

        private static string StripFences(string slide)
        {
            try
            {
                return FenceStripRegex.Replace(slide ?? string.Empty, string.Empty);
            }
            catch
            {
                return slide;
            }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> args = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return args;
            }

            foreach (string pair in query.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                string[] kv = pair.Split(new[] { '=' }, 2);
                try
                {
                    string key = Uri.UnescapeDataString(kv[0]).Trim();
                    string value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                    if (!string.IsNullOrEmpty(key))
                    {
                        args[key] = value;
                    }
                }
                catch
                {
                    /* кривую пару пропускаем */
                }
            }

            return args;
        }

        private static bool IsKnownKind(string kind)
        {
            return string.Equals(kind, "single", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(kind, "multiple", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(kind, "truefalse", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSingleOrMultiple(string kind)
        {
            return string.Equals(kind, "single", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(kind, "multiple", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrueValue(string value)
        {
            return string.Equals(value?.Trim().Trim('"', '\''), "true",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractInlineCorrect(string line)
        {
            int index = line.IndexOf("correct:", StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return string.Empty;
            }

            return line.Substring(index + "correct:".Length).Trim().TrimEnd(')', ']', '"', '\'');
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = value.Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[value.Length - 1] == '"') ||
                 (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static ValidationIssue Error(string lessonPath, string lessonTitle, int slideIndex,
            string rule, string message)
        {
            return new ValidationIssue
            {
                lessonPath = lessonPath,
                lessonTitle = lessonTitle,
                slideIndex = slideIndex,
                rule = rule,
                message = message,
                severity = "error"
            };
        }

        private static ValidationIssue Warning(string lessonPath, string lessonTitle, int slideIndex,
            string rule, string message)
        {
            return new ValidationIssue
            {
                lessonPath = lessonPath,
                lessonTitle = lessonTitle,
                slideIndex = slideIndex,
                rule = rule,
                message = message,
                severity = "warning"
            };
        }
    }
}
