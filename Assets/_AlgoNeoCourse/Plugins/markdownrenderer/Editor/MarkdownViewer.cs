using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UIMarkdownRenderer
{
    public class MarkdownViewer : EditorWindow
    {
        private string m_Path;
        private UIMarkdownRenderer m_Renderer;

        private void Awake() => EnsureRenderer();

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                MonoBehaviour[] cmps = Selection.activeGameObject.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour cmp in cmps)
                {
                    Type t = cmp.GetType();

                    if (Attribute.GetCustomAttribute(t, typeof(MarkdownDocAttribute)) is MarkdownDocAttribute attribute)
                    {
                        string[] files = AssetDatabase.FindAssets($"{attribute.DocName} t:TextAsset");
                        if (files.Length > 0)
                        {
                            m_Path = AssetDatabase.GUIDToAssetPath(files[0]);
                            Setup();
                        }
                    }
                }
            }
            else if (Selection.activeObject is TextAsset txtAsset)
            {
                string path = AssetDatabase.GetAssetPath(txtAsset);

                if (IsMarkdownPath(path))
                {
                    m_Path = path;
                    Setup();
                }
            }
        }

        // WHY сигнатура под условной компиляцией: в Unity 6.3+ атрибут `[OnOpenAsset]` принимает
        // ТОЛЬКО перегрузки с `EntityId`, и метод со старым `int` он не находит вовсе — редактор
        // пишет в консоль «does not match any of [OnOpenAssetAttribute] expected signatures» на
        // каждом обновлении домена. Внутренний хелпер конвертацию уже учитывал, а сама сигнатура
        // осталась старой, поэтому двойной клик по .md переставал открывать просмотрщик.
#if UNITY_6000_3_OR_NEWER
        [OnOpenAsset(0)]
        public static bool HandleDblClick(EntityId instanceID, int line)
        {
            Object asset = EditorUtility.EntityIdToObject(instanceID);
#else
        [OnOpenAsset(0)]
        public static bool HandleDblClick(int instanceID, int line)
        {
            Object asset = InstanceIdToObject(instanceID);
#endif
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;

            if (IsMarkdownPath(path))
            {
                Open(path);
                return true;
            }

            return false;
        }

        // Без учёта регистра: README.MD — тоже Markdown.
        private static bool IsMarkdownPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase);

        private static Object InstanceIdToObject(int instanceID)
        {
#if UNITY_6000_3_OR_NEWER
            EntityId entityId = EntityId.FromULong(unchecked((ulong)(uint)instanceID));
            return EditorUtility.EntityIdToObject(entityId);
#else
            return EditorUtility.InstanceIDToObject(instanceID);
#endif
        }

        [MenuItem("Window/Markdown Doc Viewer")]
        public static void DisplayWindow()
        {
            MarkdownViewer win = GetWindow<MarkdownViewer>();
            win.Show();
        }

        public static void Open(string path)
        {
            MarkdownViewer win = GetWindow<MarkdownViewer>();
            win.m_Path = path;

            win.Setup();
        }

        private void Setup()
        {
            EnsureRenderer();
            rootVisualElement.Clear();
            m_Renderer.OpenFile(m_Path);
            rootVisualElement.Add(m_Renderer.RootElement);
        }

        private void EnsureRenderer() => m_Renderer ??= new UIMarkdownRenderer(HandleLink);

        public static void HandleLink(string link, UIMarkdownRenderer renderer)
        {
            if (link.StartsWith("#"))
            {
                renderer.ScrollToHeader(link);
            }
            else if (link.StartsWith("Assets") || link.StartsWith("Packages"))
            {
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(link);
                Selection.activeObject = obj;
            }
            else if (link.StartsWith("search:"))
            {
                //this is a relative link, so find the actual link
                link = link.Replace("search:", "");

                string[] files = AssetDatabase.FindAssets(link);

                if (files.Length == 0)
                {
                    Debug.LogError($"Couldn't find file {link}");
                    return;
                }

                link = AssetDatabase.GUIDToAssetPath(files[0]);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(link);
                Selection.activeObject = obj;
            }
            else if (link.StartsWith("package:"))
            {
                //will search only in packages
                link = link.Replace("package:", "");

                string[] files = AssetDatabase.FindAssets($"a:packages {link}");

                if (files.Length == 0)
                {
                    Debug.LogError($"Couldn't find link : {link}");
                    return;
                }

                link = AssetDatabase.GUIDToAssetPath(files[0]);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(link);
                Selection.activeObject = obj;
            }
            else if (link.StartsWith("cmd:"))
            {
                string cmdName;
                string[] parameters;

                link = link.Replace("cmd:", "");
                int openingParenthesis = link.IndexOf('(');
                if (openingParenthesis == -1)
                {
                    //no parameters
                    cmdName = link;
                    parameters = Array.Empty<string>();
                }
                else
                {
                    //we find the closing one
                    int closingParenthesis = link.IndexOf(')');

                    cmdName = link.Substring(0, openingParenthesis);
                    string parametersString = link.Substring(openingParenthesis + 1,
                        closingParenthesis - openingParenthesis - 1);

                    parameters = parametersString.Split(',');
                }

                UIMarkdownRenderer.Command cmd = new() { CommandName = cmdName, CommandParameters = parameters };

                renderer.SendCommand(cmd);
            }
            else if (link.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                     link.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                //this is a link to an external MD or text file so we open it with the viewer instead of using Application.OpenURL
                if (!Path.IsPathRooted(link))
                {
                    string currentFolder = string.IsNullOrEmpty(renderer.FileFolder)
                        ? Directory.GetCurrentDirectory()
                        : renderer.FileFolder;
                    link = Path.GetFullPath(Path.Combine(currentFolder, link));
                }

                Open(link);
            }
            else
            {
                //any other link is open normally
                Application.OpenURL(link);
            }
        }
    }
}
