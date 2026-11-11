using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using NeoCource.Editor.UI;
using NeoCource.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        private const string NextSlideTooltip = "Следующий слайд (→)";
        private const string NextSlideBlockedTooltip = "Сначала ответьте на вопросы слайда";

        private void ShowSlide(int index)
        {
            // Закрываем откреплённое медиа: иначе оно переживает смену слайда.
            try
            {
                MediaPopoutWindow.EnsureClosed();
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath))
                {
                    QuizStateStore.SaveLessonState(currentLessonFilePath);
                }
            }
            catch
            {
            }

            if (slides == null || slides.Count == 0)
            {
                slideIndicator.text = "—/—";
                prevBtn?.SetEnabled(false);
                nextBtn?.SetEnabled(false);
                mdRenderer.SetMarkdown("# Пусто");
                EnsureRichTextOnAllLabels();
                FixBrokenMarkdownLinks();
                return;
            }

            currentSlideIndex = Mathf.Clamp(index, 0, slides.Count - 1);
            slideIndicator.text = $"{currentSlideIndex + 1}/{slides.Count}";
            prevBtn.SetEnabled(currentSlideIndex > 0);
            nextBtn.SetEnabled(currentSlideIndex < slides.Count - 1);

            // Дальняя точка для «Продолжить» — только вперёд (откат назад её не сдвигает).
            UpdateFarthestSession();

            // Обновляем состояние слайдов урока для полосы прогресса курса.
            try
            {
                LessonQuizState st = QuizStateStore.GetLessonState(currentLessonFilePath);
                if (st != null)
                {
                    st.slidesTotal = slides.Count;
                    st.lastSlideIndex = currentSlideIndex;
                    st.maxSlideReached = Math.Max(st.maxSlideReached, currentSlideIndex);
                }
            }
            catch
            {
            }

            RenderSlideContent(slides[currentSlideIndex]);
            PlaySlideEnter();
            UpdateSlideGuardState();
            SaveLastSession();
            UpdateCourseProgressUI();
            UpdateContinueButton();
            Repaint();
        }

        // Содержимое одного слайда: кнопки проверок, медиа, квизы, цвета текста, карточки заданий.
        // Навигация и сохранение прогресса остаются в ShowSlide; тесты вызывают этот метод напрямую.
        internal void RenderSlideContent(string slideMarkdown)
        {
            string markdown = InjectCheckBlocksIfDebug(slideMarkdown);
            markdown = PreprocessMediaLinks(markdown);

            List<QuizQuestion> parsedForMarkers;
            try
            {
                markdown = QuizParser.ReplaceQuizBlocksWithMarkers(markdown, out parsedForMarkers);
            }
            catch
            {
                parsedForMarkers = new List<QuizQuestion>();
            }

            // Контекст папки обычно выставлен при загрузке урока; здесь — страховка (например, после
            // перезагрузки домена, когда рендерер создан заново).
            if (mdRenderer != null && string.IsNullOrEmpty(mdRenderer.FileFolder) &&
                !string.IsNullOrEmpty(currentLessonFilePath))
            {
                SeedMarkdownContext(currentLessonFilePath);
            }

            mdRenderer.SetMarkdown(markdown);
            ApplyLegacyTextColors();
            try
            {
                currentSlideQuestions = parsedForMarkers != null && parsedForMarkers.Count > 0
                    ? parsedForMarkers
                    : QuizParser.ParseQuestions(markdown);
            }
            catch
            {
                currentSlideQuestions = new List<QuizQuestion>();
            }

            TryRenderSlideQuizzes();
            EnsureRichTextOnAllLabels();
            FixBrokenMarkdownLinks();
            DecorateCheckBlocks(slideMarkdown);
        }

        private void TryRenderSlideQuizzes()
        {
            try
            {
                if (currentSlideQuestions == null || currentSlideQuestions.Count == 0 ||
                    mdRenderer?.RootElement == null)
                {
                    return;
                }

                foreach (QuizQuestion question in currentSlideQuestions)
                {
                    string marker = "[[QUIZ:" + question.id + "]]";
                    // Заново на каждый вопрос: предыдущая замена могла разбить метку на новые
                    // (два квиза в одном абзаце) — в старом списке их нет.
                    List<Label> labels = mdRenderer.RootElement.Query<Label>().ToList();
                    Label host = labels.FirstOrDefault(l => string.Equals(l.text, marker, StringComparison.Ordinal));
                    if (host?.parent != null)
                    {
                        ReplaceLabelWithQuiz(host, question);
                        continue;
                    }

                    Label hostWithText =
                        labels.FirstOrDefault(l => !string.IsNullOrEmpty(l.text) && l.text.Contains(marker));
                    if (hostWithText?.parent == null)
                    {
                        continue;
                    }

                    VisualElement parent = hostWithText.parent;
                    int index = parent.IndexOf(hostWithText);
                    // Делим по ПЕРВОМУ вхождению метки: хвост остаётся одной меткой (в нём может быть следующий квиз).
                    string text = hostWithText.text;
                    int markerAt = text.IndexOf(marker, StringComparison.Ordinal);
                    string before = text.Substring(0, markerAt);
                    string after = text.Substring(markerAt + marker.Length);
                    parent.Remove(hostWithText);

                    if (!string.IsNullOrWhiteSpace(before))
                    {
                        parent.Insert(index++, CloneTextLabel(hostWithText, before));
                    }

                    VisualElement container = new();
                    parent.Insert(index++, container);
                    QuizRenderer.RenderQuizzes(container, currentLessonFilePath, new List<QuizQuestion> { question },
                        OnQuizStateChanged);

                    if (!string.IsNullOrWhiteSpace(after))
                    {
                        parent.Insert(index, CloneTextLabel(hostWithText, after));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseWindow: quiz render failed — " + ex.Message);
            }
        }

        // Кусок разбитой метки сохраняет классы исходной (paragraph и т.п.) — иначе текст теряет стиль темы.
        private static Label CloneTextLabel(Label source, string text)
        {
            Label label = new(text) { enableRichText = true };
            foreach (string className in source.GetClasses())
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private void ReplaceLabelWithQuiz(Label host, QuizQuestion question)
        {
            VisualElement parent = host.parent;
            if (parent == null)
            {
                return;
            }

            int index = parent.IndexOf(host);
            parent.Remove(host);
            VisualElement container = new();
            parent.Insert(index, container);
            QuizRenderer.RenderQuizzes(container, currentLessonFilePath, new List<QuizQuestion> { question },
                OnQuizStateChanged);
        }

        private void OnQuizStateChanged()
        {
            UpdateSlideGuardState();
        }

        private void UpdateSlideGuardState()
        {
            try
            {
                bool guard = QuizSettings.instance.guardSlideNavigation;
                canGoNextNow = !guard || !HasUnfinishedQuestions(currentSlideQuestions, currentLessonFilePath);

                // Guard не должен «оживлять» кнопку на последнем слайде: дальше идти некуда.
                bool hasNextSlide = slides != null && currentSlideIndex < slides.Count - 1;
                if (nextBtn != null)
                {
                    nextBtn.SetEnabled(canGoNextNow && hasNextSlide);
                    nextBtn.tooltip = !canGoNextNow && hasNextSlide ? NextSlideBlockedTooltip : NextSlideTooltip;
                }

                if (guard && QuizSettings.instance.enableDebugLogging)
                {
                    Debug.Log($"[Quiz] guard update: canNext={canGoNextNow}, slide={currentSlideIndex + 1}");
                }
            }
            catch
            {
            }
        }

        private bool HasUnfinishedQuestions(List<QuizQuestion> questions, string lessonPath)
        {
            try
            {
                if (questions == null || questions.Count == 0)
                {
                    return false;
                }

                LessonQuizState state = QuizStateStore.GetLessonState(lessonPath, false);
                if (state == null)
                {
                    // Состояния нет — вопросы не пройдены (кроме случая, когда их вовсе нет).
                    return questions.Count > 0;
                }

                foreach (QuizQuestion question in questions)
                {
                    if (!state.questionIdToState.TryGetValue(question.id, out QuizQuestionState questionState) ||
                        !questionState.isCompleted)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool CanGoNextSlide()
        {
            try
            {
                if (!QuizSettings.instance.guardSlideNavigation)
                {
                    return true;
                }

                return canGoNextNow;
            }
            catch
            {
                return true;
            }
        }

        // check-блоки — основной механизм практических проверок: кнопка «Проверить»
        // подставляется всегда (раньше — только в debug-режиме). Авторские unity://check-ссылки
        // удалены в 1.6.1, транспорт unity://check остался внутренним для этих кнопок.
        private string InjectCheckBlocksIfDebug(string markdown)
        {
            // Слайды уже с LF-переводами строк (SplitSlides); регулярка общая с CollectCheckIds и карточкой.
            return CheckBlockRegex.Replace(markdown ?? string.Empty, match =>
            {
                string button = $"[▶ Проверить]({BuildCheckLink(match.Groups[1].Value)})";
                return match.Value + "\n" + button + "\n";
            });
        }

        private static string ToProjectRelativePath(string fullPath)
        {
            try
            {
                string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string normalized = Path.GetFullPath(fullPath);
                if (normalized.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(projectPath.Length + 1).Replace('\\', '/');
                }
            }
            catch
            {
            }

            return null;
        }

        private string PreprocessMediaLinks(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown;
            }

            markdown = Regex.Replace(markdown, @"!\[\s*\]\(", "![img](");

            return Regex.Replace(markdown, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                string target = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(target))
                {
                    return match.Value;
                }

                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                if (!string.IsNullOrEmpty(currentLessonFilePath))
                {
                    try
                    {
                        string baseDir = Path.GetDirectoryName(currentLessonFilePath);
                        string combined = Path.GetFullPath(Path.Combine(baseDir ?? ".", target));
                        if (File.Exists(combined))
                        {
                            string projectRelative = ToProjectRelativePath(combined);
                            if (!string.IsNullOrEmpty(projectRelative))
                            {
                                return match.Value.Replace(target, projectRelative);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return match.Value.Replace(target, "search:" + Path.GetFileNameWithoutExtension(target));
            });
        }

        private void OnLinkClicked(string link, UIMarkdownRenderer.UIMarkdownRenderer renderer,
            VisualElement clickedElement = null)
        {
            if (!link.StartsWith("unity://", StringComparison.OrdinalIgnoreCase))
            {
                Application.OpenURL(link);
                return;
            }

            try
            {
                (string action, Dictionary<string, string> args) = ParseUnityLink(link);

                if (string.Equals(action, "check", StringComparison.OrdinalIgnoreCase))
                {
                    string resultMessage = AlgoNeoTaskChecker.Execute(args, out bool checkOk);
                    if (clickedElement != null && resultMessage != null)
                    {
                        // Кнопка внутри карточки задания — отмечаем пункты; иначе старый текстовый результат.
                        CheckCard card = clickedElement.GetFirstAncestorOfType<CheckCard>();
                        if (card != null)
                        {
                            // Тот же текст блока, что ушёл в проверку: пункты карточки и вердикт не расходятся.
                            string cardRaw = args.TryGetValue("__raw_block__", out string linkRaw) &&
                                             !string.IsNullOrEmpty(linkRaw)
                                ? linkRaw
                                : card.Raw;
                            card.ShowResults(AlgoNeoYamlChecker.EvaluateRules(cardRaw), checkOk);
                        }
                        else
                        {
                            CheckResultPresenter.Show(clickedElement, resultMessage, checkOk);
                        }
                    }

                    // Персистим результат практической проверки для прогресса урока.
                    // Только если проверка реально выполнилась (message == null — отключена/неизвестна).
                    if (resultMessage != null)
                    {
                        try
                        {
                            QuizStateStore.SaveCheckResult(
                                currentLessonFilePath, QuizStateStore.CheckIdForLink(link), checkOk);
                        }
                        catch
                        {
                        }

                        // Счётчик «проверки N/M» под полосой прогресса обновляется сразу, а не при смене слайда.
                        UpdateCourseProgressUI();
                    }

                    return;
                }

                if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetValue("path", out string path))
                    {
                        AlgoNeoEditorUtils.OpenAssetOrPath(path);
                    }
                    else
                    {
                        Debug.LogWarning($"CourseWindow: unity://open без параметра path ({link})");
                    }

                    return;
                }

                // Неизвестные unity://-действия подсвечиваем предупреждением, а не игнорируем молча.
                // Слайд-ссылки (unity://slide) удалены в 1.6.0: навигация только кнопками < > и стрелками.
                Debug.LogWarning($"CourseWindow: неизвестное unity://-действие '{action}' ({link})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"CourseWindow: ошибка обработки ссылки {link} — {ex.Message}");
            }
        }

        private static (string action, Dictionary<string, string> args) ParseUnityLink(string link)
        {
            string withoutScheme = link.Substring("unity://".Length);
            string action;
            string query = string.Empty;
            int separator = withoutScheme.IndexOf('?');
            if (separator >= 0)
            {
                action = withoutScheme.Substring(0, separator);
                query = withoutScheme.Substring(separator + 1);
            }
            else
            {
                action = withoutScheme;
            }

            Dictionary<string, string> args = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(query))
            {
                foreach (string pair in query.Split('&'))
                {
                    if (string.IsNullOrEmpty(pair))
                    {
                        continue;
                    }

                    try
                    {
                        // Делим по ПЕРВОМУ '=': значения могут содержать '=' (например base64).
                        int eq = pair.IndexOf('=');
                        string rawKey = eq < 0 ? pair : pair.Substring(0, eq);
                        string rawValue = eq < 0 ? string.Empty : pair.Substring(eq + 1);
                        string key = Uri.UnescapeDataString(rawKey);
                        string value = Uri.UnescapeDataString(rawValue);
                        args[key] = value;
                    }
                    catch
                    {
                        // Битый %-escape в одной паре не должен ронять всю ссылку.
                    }
                }
            }

            return (action, args);
        }

        private void SeedMarkdownContext(string filePath)
        {
            if (mdRenderer == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                // Только контекст папки (относительные ссылки и картинки), без рендера всего урока.
                mdRenderer.SetFileContext(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"CourseWindow: не удалось проинициализировать контекст MarkdownRenderer — {ex.Message}");
            }
        }
    }
}