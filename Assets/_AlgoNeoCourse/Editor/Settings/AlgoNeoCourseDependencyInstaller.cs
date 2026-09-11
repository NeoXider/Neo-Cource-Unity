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
            EditorApplication.delayCall += () => EnsureEmbeddedMarkdownRenderer(false);
        }

        [MenuItem("Tools/AlgoNeoCourse/Dependencies/Validate Embedded Markdown", priority = 10)]
        private static void ValidateEmbeddedMarkdown()
        {
            EnsureEmbeddedMarkdownRenderer(true);
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
                    AlgoNeoPackageAssetLocator.CombineFromPackageRoot(
                        "Plugins/markdownrenderer/Editor/VideoElement/VideoPlayerElement.cs"));
                if (!File.Exists(target))
                {
                    return false;
                }

                string originalText = File.ReadAllText(target);

                // Исходник уже версионно-разделён (#if UNITY_6000_0_OR_NEWER) — патчить нечего.
                if (originalText.Contains("UNITY_6000_0_OR_NEWER"))
                {
                    return false;
                }

#if UNITY_6000_0_OR_NEWER
                // Unity 6.0–6.6: нужен [UxmlElement] + partial, legacy UxmlFactory
                // deprecated и удалён в 6.6 (CS0308). Лечим копии, испорченные старым фиксом.
                string updatedText = EnsureModernUxmlElement(originalText);
#else
                string updatedText = EnsureLegacyUxmlFactory(originalText);
#endif

                if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
                {
                    return false;
                }

                File.WriteAllText(target, updatedText);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"AlgoNeoCourse: не удалось применить фикс встроенного MarkdownRenderer: {ex.Message}");
                return false;
            }
        }

#if UNITY_6000_0_OR_NEWER
        private static string EnsureModernUxmlElement(string text)
        {
            // 1. Убрать legacy-фабрику (именно она ломает компиляцию в 6.6).
            string updated = Regex.Replace(
                text,
                @"(?ms)^[ \t]*public\s+new\s+class\s+UxmlFactory\s*:\s*[^\r\n{]+\{\s*\}\s*\r?\n?",
                string.Empty);

            // 2. Класс должен быть partial для source-генератора UxmlElement.
            if (!updated.Contains("partial class VideoPlayerElement"))
            {
                updated = Regex.Replace(
                    updated,
                    @"public\s+class\s+VideoPlayerElement",
                    "public partial class VideoPlayerElement");
            }

            // 3. Вернуть атрибут [UxmlElement], если его срезал старый фикс.
            if (!Regex.IsMatch(updated, @"\[UxmlElement(?:Attribute)?\]\s*\r?\n\s*public\s+partial\s+class\s+VideoPlayerElement"))
            {
                updated = Regex.Replace(
                    updated,
                    @"public\s+partial\s+class\s+VideoPlayerElement",
                    "[UxmlElement]\npublic partial class VideoPlayerElement");
            }

            return updated;
        }
#else
        private static string EnsureLegacyUxmlFactory(string text)
        {
            string updatedText = Regex.Replace(
                text,
                @"(?m)^\s*\[UxmlElement(?:Attribute)?\]\s*\r?\n",
                string.Empty);

            if (!updatedText.Contains("class UxmlFactory"))
            {
                Match classMatch = Regex.Match(
                    updatedText,
                    @"public\s+partial\s+class\s+VideoPlayerElement\s*:\s*VisualElement\s*\{",
                    RegexOptions.Multiline);
                if (classMatch.Success)
                {
                    int insertPos = classMatch.Index + classMatch.Length;
                    string insertText =
                        "\n    public new class UxmlFactory : UnityEngine.UIElements.UxmlFactory<VideoPlayerElement> { }\n";
                    updatedText = updatedText.Insert(insertPos, insertText);
                }
            }

            return updatedText;
        }
#endif
    }
}