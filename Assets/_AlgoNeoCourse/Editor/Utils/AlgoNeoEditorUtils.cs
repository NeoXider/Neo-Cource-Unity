using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using NeoCource.Editor.Settings;

namespace NeoCource.Editor.Utils
{
    public static class AlgoNeoEditorUtils
    {
        public static string OkMark => "V";
        public static string FailMark => "X";

        public static string OkMarkColored() => OkMark.Color(CourseSettings.instance.okLogColor);
        public static string FailMarkColored() => FailMark.Color(CourseSettings.instance.failLogColor);

        public static string Color(this string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{text}</color>";
        }

        public static bool OpenAssetOrPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return false;

            string resolved = inputPath.Replace('\\', '/');

            string dataPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

            if (!resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !resolved.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                if (resolved.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = "Assets/" + resolved.Substring(dataPath.Length + 1);
                }
            }

            UnityEngine.Object asset = null;
            if (resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(resolved);
                if (asset == null)
                {
                    var fileName = Path.GetFileName(resolved);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        var guids = AssetDatabase.FindAssets(nameNoExt);
                        foreach (var guid in guids)
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            if (path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                            {
                                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                                if (asset != null) { resolved = path; break; }
                            }
                        }
                    }
                }
            }

            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                return true;
            }

            //try { EditorUtility.RevealInFinder(inputPath); } catch { }
            Debug.Log("Failed to find asset: " + resolved);
            return false;
        }

        public static string FindAssetPath(string filename)
        {
            string fileOnly = Path.GetFileName(filename);
            string nameNoExt = Path.GetFileNameWithoutExtension(fileOnly);

            var guids = AssetDatabase.FindAssets(nameNoExt + " t:MonoScript");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(p), fileOnly, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            guids = AssetDatabase.FindAssets(nameNoExt);
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(p), fileOnly, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            try
            {
                return Directory.GetFiles("Assets", fileOnly, SearchOption.AllDirectories).FirstOrDefault();
            }
            catch { /* ignore */ }

            return null;
        }

        public static string ReadAllTextWithRetries(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            try
            {
                return File.ReadAllText(fullPath, Encoding.UTF8);
            }
            catch
            {
                return File.ReadAllText(fullPath);
            }
        }

        public static System.Type FindTypeByName(string name)
        {
            if (name.Contains("Collider2D")) return typeof(Collider2D);

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException) { return Array.Empty<System.Type>(); }
                });

            System.Type exactMatch = null;
            System.Type nameMatch = null;
            System.Type endsWithMatch = null;

            foreach (var t in allTypes)
            {
                if (t == null) continue;

                if (string.Equals(t.FullName, name, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = t;
                    break;
                }

                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (nameMatch == null || t.FullName.StartsWith("UnityEngine"))
                        nameMatch = t;
                }

                if (t.FullName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                {
                    if (endsWithMatch == null || t.FullName.StartsWith("UnityEngine"))
                        endsWithMatch = t;
                }
            }

            return exactMatch ?? endsWithMatch ?? nameMatch;
        }
    }
}
