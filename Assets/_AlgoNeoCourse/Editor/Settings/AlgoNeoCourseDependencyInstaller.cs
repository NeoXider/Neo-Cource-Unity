using System.Collections.Generic;
using NeoCource.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    // До 1.8.0 этот класс при загрузке редактора патчил исходник встроенного рендерера (VideoPlayerElement
    // под разные версии Unity). Теперь встроенная копия Neoxider Markdown Renderer не меняется вовсе
    // (папка совпадает с апстримом), поэтому осталась только ручная проверка её комплектности.
    internal static class AlgoNeoCourseDependencyInstaller
    {
        [MenuItem("Tools/AlgoNeoCourse/Settings/Dependencies/Validate Embedded Markdown", priority = 180)]
        private static void ValidateEmbeddedMarkdown()
        {
            List<string> missing = FindMissingRendererAssets();
            if (missing.Count == 0)
            {
                Debug.Log("AlgoNeoCourse: встроенный MarkdownRenderer найден и готов к работе.");
                return;
            }

            Debug.LogWarning("AlgoNeoCourse: встроенный MarkdownRenderer неполон, не найдено: " +
                             string.Join(", ", missing));
        }

        // Всё, что окно курса берёт из папки рендерера и из собственных стилей плагина.
        internal static List<string> FindMissingRendererAssets()
        {
            List<string> missing = new();
            if (!AlgoNeoPackageAssetLocator.AssetExists(AlgoNeoPackageAssetLocator.MarkdownRendererRootAssetPath))
            {
                missing.Add(AlgoNeoPackageAssetLocator.MarkdownRendererRootAssetPath);
                return missing;
            }

            AddIfMissing<StyleSheet>(missing, AlgoNeoPackageAssetLocator.MarkdownStylesheetAssetPath);
            AddIfMissing<StyleSheet>(missing, AlgoNeoPackageAssetLocator.VideoPlayerStylesheetAssetPath);
            AddIfMissing<VisualTreeAsset>(missing, AlgoNeoPackageAssetLocator.VideoPlayerUxmlAssetPath);
            AddIfMissing<StyleSheet>(missing, AlgoNeoPackageAssetLocator.QuizStylesheetAssetPath);
            AddIfMissing<StyleSheet>(missing, AlgoNeoPackageAssetLocator.CourseWindowStylesheetAssetPath);
            return missing;
        }

        private static void AddIfMissing<T>(List<string> missing, string assetPath) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(assetPath) == null)
            {
                missing.Add(assetPath);
            }
        }
    }
}
