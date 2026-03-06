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

        private void Awake()
        {
            m_Renderer = new UIMarkdownRenderer(HandleLink);
        }

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

                if (Path.GetExtension(path) == ".md")
                {
                    m_Path = path;
                    Setup();
                }
            }
        }

        [OnOpenAsset(0)]
        public static bool HandleDblClick(int instanceID, int line)
        {
            string path = AssetDatabase.GetAssetPath(instanceID);

            if (Path.GetExtension(path) == ".md")
            {
                Open(path);
                return true;
            }

            return false;
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
            rootVisualElement.Clear();
            m_Renderer.OpenFile(m_Path);
            rootVisualElement.Add(m_Renderer.RootElement);
        }

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

                UIMarkdownRenderer.Command cmd = new()
                {
                    CommandName = cmdName,
                    CommandParameters = parameters
                };

                renderer.SendCommand(cmd);
            }
            else if (link.EndsWith(".md") || link.EndsWith(".txt"))
            {
                //this is a link to an external MD or text file so we open it with the viewer instead of using Application.OpenURL
                if (!Path.IsPathRooted(link))
                {
                    link = Path.Combine(Path.GetDirectoryName(renderer.FileFolder), link);
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