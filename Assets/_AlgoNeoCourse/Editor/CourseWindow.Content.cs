using System;
using System.IO;
using System.Text.RegularExpressions;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        private void DoRefreshLessonsAndCurrent()
        {
            RefreshLessonsList();
            if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
            {
                try
                {
                    string text = File.ReadAllText(currentLessonFilePath);
                    slides = SplitSlides(text);
                    ShowSlide(currentSlideIndex);
                    SaveLastSession();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"CourseWindow: не удалось перечитать текущий урок — {ex.Message}");
                }
            }

            EnsureDocsMenuFromSettings();
        }

        private void EnsureDocsMenuFromSettings()
        {
            if (docsMenu != null)
            {
                toolbar.Remove(docsMenu);
                docsMenu = null;
            }

            if (ValidationSettings.Instance.DebugBrowseDocsExamples)
            {
                docsMenu = new ToolbarMenu { text = "Docs" };
                docsMenu.AddToClassList("algo-course-docs-menu");
                docsMenu.tooltip = "Примеры из Docs/Examples";
                PopulateDocsMenu();
                toolbar.Add(docsMenu);
            }
        }

        private void PopulateDocsMenu()
        {
            docsMenu.menu.ClearItems();
            string root =
                AlgoNeoPackageAssetLocator.ToAbsolutePath(AlgoNeoPackageAssetLocator.DocsExamplesFolderAssetPath);
            if (!Directory.Exists(root))
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            string[] files = Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                docsMenu.menu.AppendAction("Нет примеров", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            foreach (string path in files)
            {
                string filePath = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(filePath);
                docsMenu.menu.AppendAction(name, _ => LoadDocsExample(filePath));
            }
        }

        private void LoadDocsExample(string assetPath)
        {
            try
            {
                if (mdRenderer == null)
                {
                    BuildContent();
                }

                currentLessonTitle = Path.GetFileNameWithoutExtension(assetPath);
                currentLessonFilePath = assetPath;
                currentSlideIndex = 0;
                string text = File.ReadAllText(assetPath);
                slides = SplitSlides(text);
                SeedMarkdownContext(assetPath);
                ShowSlide(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"CourseWindow: не удалось открыть пример {assetPath}: {ex.Message}");
            }
        }

        private void BuildContent()
        {
            contentRoot = new ScrollView(ScrollViewMode.Vertical);
            contentRoot.style.flexGrow = 1f;
            contentRoot.AddToClassList("algo-course-content");

            mdRenderer = new UIMarkdownRenderer.UIMarkdownRenderer((link, renderer) => OnLinkClicked(link, renderer));
            try
            {
                StyleSheet styleSheet =
                    AssetDatabase.LoadAssetAtPath<StyleSheet>(AlgoNeoPackageAssetLocator.QuizStylesheetAssetPath);
                if (styleSheet != null)
                {
                    mdRenderer.RootElement.styleSheets.Add(styleSheet);
                }
            }
            catch
            {
            }

            contentRoot.Add(mdRenderer.RootElement);
            rootVisualElement.Add(contentRoot);
        }

        private void EnsureRichTextOnAllLabels()
        {
            if (mdRenderer?.RootElement == null)
            {
                return;
            }

            try
            {
                foreach (Label label in mdRenderer.RootElement.Query<Label>().ToList())
                {
                    if (label != null)
                    {
                        label.enableRichText = true;
                    }
                }
            }
            catch
            {
            }
        }

        private void FixBrokenMarkdownLinks()
        {
            if (mdRenderer?.RootElement == null)
            {
                return;
            }

            try
            {
                foreach (Label label in mdRenderer.RootElement.Query<Label>().ToList())
                {
                    if (label == null)
                    {
                        continue;
                    }

                    string text = label.text ?? string.Empty;
                    if (text.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string link = null;
                    Match match = Regex.Match(text, @"<link=(unity://check[^>]+)>");
                    if (match.Success)
                    {
                        link = match.Groups[1].Value;
                    }

                    if (string.IsNullOrEmpty(link))
                    {
                        int start = text.IndexOf("unity://check", StringComparison.OrdinalIgnoreCase);
                        int end = start;
                        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '>' &&
                               text[end] != '"')
                        {
                            end++;
                        }

                        link = text.Substring(start, end - start);
                    }

                    if (label.parent == null)
                    {
                        continue;
                    }

                    VisualElement parent = label.parent;
                    int index = parent.IndexOf(label);
                    ToolbarButton button = new() { text = "Проверить" };
                    button.clicked += () =>
                    {
                        try
                        {
                            OnLinkClicked(link, mdRenderer, button);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex.Message);
                        }
                    };
                    button.AddToClassList("check-button");
                    parent.Insert(index, button);
                    parent.Remove(label);
                }
            }
            catch
            {
            }
        }

        private void LoadWindowStyles()
        {
            try
            {
                StyleSheet styleSheet =
                    AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        AlgoNeoPackageAssetLocator.CourseWindowStylesheetAssetPath);
                if (styleSheet != null && !rootVisualElement.styleSheets.Contains(styleSheet))
                {
                    rootVisualElement.styleSheets.Add(styleSheet);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseWindow: не удалось загрузить стиль окна — {ex.Message}");
            }
        }
    }
}