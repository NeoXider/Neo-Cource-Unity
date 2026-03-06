using System;
using System.IO;
using System.Text.RegularExpressions;
using NeoCource.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor
{
    [InitializeOnLoad]
    internal static class AlgoNeoCourseDependencyInstaller
    {
        static AlgoNeoCourseDependencyInstaller()
        {
            EditorApplication.delayCall += () => EnsureEmbeddedMarkdownRenderer(logIfMissing: false);
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Validate Embedded Markdown", priority = 10)]
        private static void ValidateEmbeddedMarkdown()
        {
            EnsureEmbeddedMarkdownRenderer(logIfMissing: true);
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Apply Embedded Markdown Fix", priority = 11)]
        private static void ApplyEmbeddedMarkdownFix()
        {
            if (EnsureMarkdownRendererPatched())
            {
                AssetDatabase.Refresh();
                Debug.Log("AlgoNeoCourse: фикс встроенного MarkdownRenderer применён.");
                return;
            }

            Debug.Log("AlgoNeoCourse: встроенный MarkdownRenderer уже в актуальном состоянии.");
        }

        private static void EnsureEmbeddedMarkdownRenderer(bool logIfMissing)
        {
            if (!AlgoNeoPackageAssetLocator.AssetExists(AlgoNeoPackageAssetLocator.MarkdownRendererRootAssetPath))
            {
                if (logIfMissing)
                {
                    Debug.LogWarning("AlgoNeoCourse: встроенный MarkdownRenderer не найден в составе пакета.");
                }

                return;
            }

            bool changed = EnsureMarkdownRendererPatched();
            if (changed)
            {
                AssetDatabase.Refresh();
            }
            else if (logIfMissing)
            {
                Debug.Log("AlgoNeoCourse: встроенный MarkdownRenderer найден и готов к работе.");
            }
        }

        private static bool EnsureMarkdownRendererPatched()
        {
            try
            {
                string target = AlgoNeoPackageAssetLocator.ToAbsolutePath(
                    AlgoNeoPackageAssetLocator.CombineFromPackageRoot("Plugins/markdownrenderer/Editor/VideoElement/VideoPlayerElement.cs"));
                if (!File.Exists(target))
                {
                    return false;
                }

                string originalText = File.ReadAllText(target);
                string updatedText = Regex.Replace(
                    originalText,
                    @"(?m)^\s*\[UxmlElement(?:Attribute)?\]\s*\r?\n",
                    string.Empty);

                if (!updatedText.Contains("class UxmlFactory : UxmlFactory<VideoPlayerElement>"))
                {
                    var classMatch = Regex.Match(
                        updatedText,
                        @"public\s+partial\s+class\s+VideoPlayerElement\s*:\s*VisualElement\s*\{",
                        RegexOptions.Multiline);
                    if (classMatch.Success)
                    {
                        int insertPos = classMatch.Index + classMatch.Length;
                        string insertText = "\n    public new class UxmlFactory : UxmlFactory<VideoPlayerElement> { }\n";
                        updatedText = updatedText.Insert(insertPos, insertText);
                    }
                }

                if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
                {
                    return false;
                }

                File.WriteAllText(target, updatedText);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AlgoNeoCourse: не удалось применить фикс встроенного MarkdownRenderer: {ex.Message}");
                return false;
            }
        }
    }
}


