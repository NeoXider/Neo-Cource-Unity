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
using UnityEngine.SceneManagement;
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

        // Ищет тип по имени. Точное совпадение всегда выигрывает: "CircleCollider2D" — это именно
        // CircleCollider2D, а не любой Collider2D. Проверка базового типа ("Collider2D") по-прежнему
        // проходит с наследником — это делает сам GetComponent.
        public static Type FindTypeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            name = name.Trim();
            Type best = null;
            int bestScore = 0;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types ?? Array.Empty<Type>();
                }
                catch
                {
                    continue;
                }

                foreach (Type t in types)
                {
                    if (t == null)
                    {
                        continue;
                    }

                    int score = ScoreTypeMatch(t, name);
                    if (score > bestScore)
                    {
                        best = t;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        // 0 — не подходит. Чем выше, тем точнее: полное имя > короткое имя > то же без учёта регистра.
        // При равенстве имён выигрывает компонент (UnityEngine.UI.Button, а не UIElements.Button).
        private static int ScoreTypeMatch(Type t, string name)
        {
            string fullName = t.FullName ?? t.Name;
            int score;
            if (string.Equals(fullName, name, StringComparison.Ordinal))
            {
                score = 400;
            }
            else if (string.Equals(t.Name, name, StringComparison.Ordinal) ||
                     fullName.EndsWith("." + name, StringComparison.Ordinal))
            {
                score = 300;
            }
            else if (string.Equals(fullName, name, StringComparison.OrdinalIgnoreCase))
            {
                score = 200;
            }
            else if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
                     fullName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
            {
                score = 100;
            }
            else
            {
                return 0;
            }

            if (typeof(Component).IsAssignableFrom(t))
            {
                score += 50;
            }

            if (fullName.StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                score += 20;
            }

            if (t.IsPublic)
            {
                score += 5;
            }

            return score;
        }

        // Объект сцены по имени или по пути в иерархии ("Player/ShootPoint", "Canvas/WallText").
        // В отличие от GameObject.Find находит и ВЫКЛЮЧЕННЫЕ объекты: уроки намеренно прячут
        // GameOverPanel и подобные, а потом проверяют их.
        public static GameObject FindSceneObject(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                return null;
            }

            string query = nameOrPath.Trim().Replace('\\', '/');
            try
            {
                GameObject active = GameObject.Find(query);
                if (active != null)
                {
                    return active;
                }
            }
            catch
            {
            }

            bool rooted = query.StartsWith("/", StringComparison.Ordinal);
            string[] segments = query.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            foreach (GameObject root in GetLoadedSceneRoots())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (MatchesHierarchyPath(t, segments, rooted))
                    {
                        return t.gameObject;
                    }
                }
            }

            return null;
        }

        // Есть ли в загруженных сценах (включая выключенные объекты) компонент такого типа.
        public static Component FindSceneComponent(Type componentType)
        {
            if (!IsComponentType(componentType))
            {
                return null;
            }

            foreach (GameObject root in GetLoadedSceneRoots())
            {
                Component[] found = root.GetComponentsInChildren(componentType, true);
                if (found != null && found.Length > 0)
                {
                    return found[0];
                }
            }

            return null;
        }

        // GetComponent принимает только наследников Component и интерфейсы — на остальном бросает исключение.
        public static bool IsComponentType(Type type)
        {
            return type != null && (typeof(Component).IsAssignableFrom(type) || type.IsInterface);
        }

        private static List<GameObject> GetLoadedSceneRoots()
        {
            List<GameObject> roots = new();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    roots.AddRange(scene.GetRootGameObjects());
                }
            }

            return roots;
        }

        // Совпадение по хвосту пути: последний сегмент — сам объект, предыдущие — его родители.
        private static bool MatchesHierarchyPath(Transform t, string[] segments, bool rooted)
        {
            Transform current = t;
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (current == null || !string.Equals(current.name, segments[i], StringComparison.Ordinal))
                {
                    return false;
                }

                current = current.parent;
            }

            return !rooted || current == null;
        }
    }
}