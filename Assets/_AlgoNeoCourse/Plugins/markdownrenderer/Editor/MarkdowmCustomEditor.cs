using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer
{
    [CustomEditor(typeof(TextAsset))]
    public class MarkdownCustomEditor : Editor
    {
        private Editor m_DefaultEditor;
        private bool m_IsMDFile;
        private UIMarkdownRenderer m_Renderer;
        private string m_TargetPath;


        private void Awake()
        {
            m_Renderer = new UIMarkdownRenderer(MarkdownViewer.HandleLink);
        }

        public virtual void OnEnable()
        {
            m_TargetPath = AssetDatabase.GetAssetPath(target);

            Assembly assembly = typeof(Editor).Assembly;
            Type type = assembly.GetType("UnityEditor.TextAssetInspector");

            CreateCachedEditor(target, type, ref m_DefaultEditor);

            //TODO : handle also other extension? Potentially skip that and display every file as Markdown?
            m_IsMDFile = Path.GetExtension(AssetDatabase.GetAssetPath(target)) == ".md";
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (m_IsMDFile)
            {
                m_Renderer.OpenFile(Path.GetFullPath(m_TargetPath));
                return m_Renderer.RootElement;
            }

            IMGUIContainer elem = new(m_DefaultEditor.OnInspectorGUI);

            //by default the stylesheet seems to add some margin, so we reverse them to fit "right"
            elem.style.marginTop = 2;
            elem.style.marginLeft = -15;
            return elem;
        }
    }
}