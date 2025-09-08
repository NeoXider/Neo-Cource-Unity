using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using NeoCource.Editor.Settings;

 namespace NeoCource.Editor.Util
{
    public static class AlgoNeoEditorUtils
    {
        public static string Color(this string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{text}</color>";
        }

        public static string OkMarkColored()
        {
            return "V".Color(CourseSettings.instance.okLogColor);
        }

        public static string FailMarkColored()
        {
            return "X".Color(CourseSettings.instance.failLogColor);
        }
 
        /// <summary>
        /// Открывает ассет по пути (Assets/..., Packages/... или абсолютный путь внутри проекта).
        /// Если ассет найден — выделяет и пингует в Project. Если нет — открывает системный проводник.
        /// Возвращает true при успешном выделении ассета.
        /// </summary>
        public static bool OpenAssetOrPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return false;

            string resolved = inputPath.Replace('\\', '/');

            // Конвертация абсолютного пути в относительный к Assets/
            string dataPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

            if (!resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !resolved.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                if (resolved.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = "Assets/" + resolved.Substring(dataPath.Length + 1);
                }
                else if (resolved.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    // Путь внутри проекта, но вне Assets — оставим как есть, откроем проводник ниже
                }
            }

            UnityEngine.Object asset = null;
            if (resolved.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                resolved.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(resolved);
                if (asset == null)
                {
                    // Попробуем поискать по имени файла
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
    }
}


