using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NeoCource.Editor.GifSupport;
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
        private void ShowSlide(int index)
        {
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
                mdRenderer.SetMarkdown("# Пусто");
                EnsureRichTextOnAllLabels();
                FixBrokenMarkdownLinks();
                return;
            }

            currentSlideIndex = Mathf.Clamp(index, 0, slides.Count - 1);
            slideIndicator.text = $"{currentSlideIndex + 1}/{slides.Count}";
            prevBtn.SetEnabled(currentSlideIndex > 0);
            nextBtn.SetEnabled(currentSlideIndex < slides.Count - 1);

            string markdown = slides[currentSlideIndex];
            markdown = InjectCheckBlocksIfDebug(markdown);
            markdown = PreprocessMediaLinks(markdown);
            markdown = ConvertGifLinksToMp4(markdown);

            List<QuizQuestion> parsedForMarkers;
            try
            {
                markdown = QuizParser.ReplaceQuizBlocksWithMarkers(markdown, out parsedForMarkers);
            }
            catch
            {
                parsedForMarkers = new List<QuizQuestion>();
            }

            if (mdRenderer != null && string.IsNullOrEmpty(mdRenderer.FileFolder) &&
                !string.IsNullOrEmpty(currentLessonFilePath))
            {
                SeedMarkdownContext(currentLessonFilePath);
                if (string.IsNullOrEmpty(mdRenderer.FileFolder) && File.Exists(currentLessonFilePath))
                {
                    try
                    {
                        mdRenderer.OpenFile(currentLessonFilePath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            mdRenderer.SetMarkdown(markdown);
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
            UpdateSlideGuardState();
            SaveLastSession();
            Repaint();
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

                List<Label> labels = mdRenderer.RootElement.Query<Label>().ToList();
                foreach (QuizQuestion question in currentSlideQuestions)
                {
                    string marker = "[[QUIZ:" + question.id + "]]";
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
                    string[] parts = hostWithText.text.Split(new[] { marker }, StringSplitOptions.None);
                    parent.Remove(hostWithText);

                    if (!string.IsNullOrEmpty(parts[0]))
                    {
                        parent.Insert(index++, new Label(parts[0]));
                    }

                    VisualElement container = new();
                    parent.Insert(index++, container);
                    QuizRenderer.RenderQuizzes(container, currentLessonFilePath, new List<QuizQuestion> { question },
                        OnQuizStateChanged);

                    if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                    {
                        parent.Insert(index, new Label(parts[1]));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseWindow: quiz render failed — " + ex.Message);
            }
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
                if (!QuizSettings.instance.guardSlideNavigation)
                {
                    return;
                }

                bool canGoNext = !HasUnfinishedQuestions(currentSlideQuestions, currentLessonFilePath);
                canGoNextNow = canGoNext;
                nextBtn.SetEnabled(canGoNextNow);
                if (QuizSettings.instance.enableDebugLogging)
                {
                    Debug.Log($"[Quiz] guard update: canNext={canGoNext}, slide={currentSlideIndex + 1}");
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
                    return questions.Count == 0;
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

        private string InjectCheckBlocksIfDebug(string markdown)
        {
            if (!ValidationSettings.Instance.DebugRenderCheckBlocks)
            {
                return markdown;
            }

            string pattern = @"```check\n([\s\S]*?)\n```";
            Regex regex = new(pattern, RegexOptions.Multiline);
            return regex.Replace(markdown, match =>
            {
                string raw = match.Groups[1].Value;
                string encoded = Uri.EscapeDataString(raw);
                string button = $"[▶ Проверить](unity://check?type=from-block&dialog=auto&__raw_block__={encoded})";
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

        private string ConvertGifLinksToMp4(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown;
            }

            CourseSettings settings = CourseSettings.instance;
            if (!settings.autoConvertGifToMp4 || string.IsNullOrEmpty(settings.GetFfmpegAssetPath()))
            {
                return markdown;
            }

            int replacedCount = 0;
            string result = Regex.Replace(markdown, "!\\[[^\\]]*\\]\\(([^)]+)\\)", match =>
            {
                string url = match.Groups[1].Value.Trim();
                if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)))
                {
                    return match.Value;
                }

                if (!LooksLikeGifUrl(url))
                {
                    return match.Value;
                }

                string mp4AssetsPath = GifConverter.ConvertGifToMp4IfNeeded(url);
                if (string.IsNullOrEmpty(mp4AssetsPath))
                {
                    return match.Value;
                }

                replacedCount++;

                string alt = Regex.Match(match.Value, "!\\[([^\\]]*)\\]").Groups[1].Value;
                return $"![{alt}]({mp4AssetsPath})";
            });

            if (replacedCount > 0 && !autoReloadScheduled)
            {
                autoReloadScheduled = true;
                EditorApplication.delayCall += () =>
                {
                    if (this == null)
                    {
                        autoReloadScheduled = false;
                        return;
                    }

                    try
                    {
                        ShowSlide(currentSlideIndex);
                    }
                    finally
                    {
                        autoReloadScheduled = false;
                    }
                };
            }

            return result;
        }

        private static bool LooksLikeGifUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return string.Equals(Path.GetExtension(uri.AbsolutePath), ".gif", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(Path.GetExtension(url), ".gif", StringComparison.OrdinalIgnoreCase);
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
                if (string.Equals(action, "slide", StringComparison.OrdinalIgnoreCase))
                {
                    int direction = args.TryGetValue("dir", out string value) && value == "next" ? 1 : -1;
                    ShowSlide(currentSlideIndex + direction);
                    return;
                }

                if (string.Equals(action, "check", StringComparison.OrdinalIgnoreCase))
                {
                    string resultMessage = AlgoNeoTaskChecker.Execute(args);
                    if (clickedElement != null)
                    {
                        CheckResultPresenter.Show(clickedElement, resultMessage);
                    }

                    return;
                }

                if (string.Equals(action, "open", StringComparison.OrdinalIgnoreCase) &&
                    args.TryGetValue("path", out string path))
                {
                    AlgoNeoEditorUtils.OpenAssetOrPath(path);
                }
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

                    string[] kv = pair.Split('=');
                    string key = Uri.UnescapeDataString(kv[0]);
                    string value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                    args[key] = value;
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
                string localDir = Path.GetDirectoryName(filePath);
                string fullDir = Path.GetFullPath(localDir ?? ".");
                Type type = typeof(UIMarkdownRenderer.UIMarkdownRenderer);
                FieldInfo localField = type.GetField("m_LocalFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo folderField = type.GetField("m_FileFolder", BindingFlags.NonPublic | BindingFlags.Instance);
                localField?.SetValue(mdRenderer, localDir);
                folderField?.SetValue(mdRenderer, fullDir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"CourseWindow: не удалось проинициализировать контекст MarkdownRenderer — {ex.Message}");
            }
        }
    }
}