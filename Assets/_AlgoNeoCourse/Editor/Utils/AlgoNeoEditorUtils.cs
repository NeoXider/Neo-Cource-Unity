using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeoCource.Editor.Utils
{
    public static class AlgoNeoEditorUtils
    {
        public static string OkMark => "V";
        public static string FailMark => "X";

        public static string OkMarkColored()
        {
            return OkMark.Color(CourseSettings.instance.okLogColor);
        }

        public static string FailMarkColored()
        {
            return FailMark.Color(CourseSettings.instance.failLogColor);
        }

        public static string Color(this string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{text}</color>";
        }

        public static bool OpenAssetOrPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return false;
            }

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

            Object asset = null;
            if (resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                asset = AssetDatabase.LoadAssetAtPath<Object>(resolved);
                if (asset == null)
                {
                    string fileName = Path.GetFileName(resolved);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        string[] guids = AssetDatabase.FindAssets(nameNoExt);
                        foreach (string guid in guids)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            if (path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                            {
                                asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                                if (asset != null)
                                {
                                    resolved = path;
                                    break;
                                }
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

            string[] guids = AssetDatabase.FindAssets(nameNoExt + " t:MonoScript");
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(p), fileOnly, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            guids = AssetDatabase.FindAssets(nameNoExt);
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(p), fileOnly, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            try
            {
                return Directory.GetFiles("Assets", fileOnly, SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                /* ignore */
            }

            return null;
        }

        public static string ReadAllTextWithRetries(string assetPath)
        {
            // Guard: абсолютный путь за пределами проекта не читаем.
            if (!string.IsNullOrEmpty(assetPath) && Path.IsPathRooted(assetPath))
            {
                string projectRoot = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
                string requested = Path.GetFullPath(assetPath);
                string rootWithSep = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                     Path.DirectorySeparatorChar;
                bool inside = requested.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(requested, projectRoot, StringComparison.OrdinalIgnoreCase);
                if (!inside)
                {
                    throw new IOException("Путь вне проекта: " + assetPath);
                }
            }

            string root = Path.GetDirectoryName(Application.dataPath);
            string fullPath =
                Path.GetFullPath(Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)));

            // Три попытки с нарастающей паузой; пробрасываем последнее исключение.
            Exception lastError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    try
                    {
                        return File.ReadAllText(fullPath, Encoding.UTF8);
                    }
                    catch
                    {
                        return File.ReadAllText(fullPath);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 3)
                    {
                        Thread.Sleep(60 * attempt);
                    }
                }
            }

            throw lastError ?? new IOException("Не удалось прочитать файл: " + assetPath);
        }

        public static Type FindTypeByName(string name)
        {
            if (name.Contains("Collider2D"))
            {
                return typeof(Collider2D);
            }

            IEnumerable<Type> allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        return Array.Empty<Type>();
                    }
                });

            Type exactMatch = null;
            Type nameMatch = null;
            Type endsWithMatch = null;

            foreach (Type t in allTypes)
            {
                if (t == null)
                {
                    continue;
                }

                if (string.Equals(t.FullName, name, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = t;
                    break;
                }

                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (nameMatch == null || t.FullName.StartsWith("UnityEngine"))
                    {
                        nameMatch = t;
                    }
                }

                if (t.FullName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
                {
                    if (endsWithMatch == null || t.FullName.StartsWith("UnityEngine"))
                    {
                        endsWithMatch = t;
                    }
                }
            }

            return exactMatch ?? endsWithMatch ?? nameMatch;
        }
    }
}