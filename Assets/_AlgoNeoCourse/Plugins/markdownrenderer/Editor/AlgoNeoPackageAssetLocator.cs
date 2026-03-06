using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Infrastructure
{
    public static class AlgoNeoPackageAssetLocator
    {
        public const string PackageName = "com.neoxider.algoneocourse";
        public const string LegacyAssetsRoot = "Assets/_AlgoNeoCourse";

        private static readonly string[] RootCandidates =
        {
            "Packages/" + PackageName,
            LegacyAssetsRoot
        };

        public static string PackageRootAssetPath => FindExistingRoot() ?? LegacyAssetsRoot;

        public static string DocsExamplesFolderAssetPath => CombineFromPackageRoot("Docs/Examples");
        public static string CourseWindowStylesheetAssetPath => CombineFromPackageRoot("Editor/Styles/AlgoNeoCourseWindow.uss");
        public static string MarkdownRendererRootAssetPath => CombineFromPackageRoot("Plugins/markdownrenderer");
        public static string MarkdownStylesheetAssetPath => CombineFromPackageRoot("Plugins/markdownrenderer/Styles/MarkdownRenderer.uss");
        public static string QuizStylesheetAssetPath => CombineFromPackageRoot("Plugins/markdownrenderer/Styles/Quiz.uss");
        public static string VideoPlayerUxmlAssetPath => CombineFromPackageRoot("Plugins/markdownrenderer/Editor/VideoElement/VideoPlayerElement.uxml");
        public static string VideoPlayerStylesheetAssetPath => CombineFromPackageRoot("Plugins/markdownrenderer/Editor/VideoElement/VideoPlayerElement.uss");
        public static string EmbeddedFfmpegAssetPath => CombineFromPackageRoot("Editor/Utils/ffmpeg.exe");

        public static string DefaultDownloadFolderAssetPath => LegacyAssetsRoot + "/Downloaded";
        public static string DefaultProgressFolderAssetPath => LegacyAssetsRoot + "/Progress";
        public static string DefaultVideoCacheFolderAssetPath => LegacyAssetsRoot + "/VideoCache";

        public static string CombineFromPackageRoot(string relativePath)
        {
            string normalized = NormalizeAssetPath(relativePath);
            foreach (var root in RootCandidates)
            {
                string candidate = root + "/" + normalized;
                if (AssetExists(candidate))
                {
                    return candidate;
                }
            }

            return PackageRootAssetPath + "/" + normalized;
        }

        public static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            string normalized = NormalizeAssetPath(assetPath);
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(normalized);
            }

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), normalized));
        }

        public static string EnsureProjectFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string absolutePath = ToAbsolutePath(normalized);
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
                AssetDatabase.Refresh();
            }

            return normalized;
        }

        public static string NormalizeWritableAssetPath(string configuredPath, string fallbackAssetPath)
        {
            string fallback = NormalizeAssetPath(fallbackAssetPath);
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return fallback;
            }

            string normalized = NormalizeAssetPath(configuredPath);
            if (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            return normalized;
        }

        public static bool AssetExists(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            string absolutePath = ToAbsolutePath(normalized);
            return File.Exists(absolutePath) || Directory.Exists(absolutePath);
        }

        public static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string FindExistingRoot()
        {
            foreach (var root in RootCandidates)
            {
                if (AssetExists(root))
                {
                    return root;
                }
            }

            return null;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }
    }
}
