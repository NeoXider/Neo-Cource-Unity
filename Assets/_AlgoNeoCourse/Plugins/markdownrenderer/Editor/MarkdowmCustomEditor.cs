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


        private void Awake() => EnsureRenderer();

        public virtual void OnEnable()
        {
            EnsureRenderer();
            if (target == null)
            {
                m_TargetPath = string.Empty;
                m_IsMDFile = false;
                return;
            }

            m_TargetPath = AssetDatabase.GetAssetPath(target);

            Assembly assembly = typeof(Editor).Assembly;
            Type type = assembly.GetType("UnityEditor.TextAssetInspector");

            CreateCachedEditor(target, type, ref m_DefaultEditor);

            //TODO : handle also other extension? Potentially skip that and display every file as Markdown?
            // Без учёта регистра: README.MD — тоже Markdown.
            m_IsMDFile = string.Equals(Path.GetExtension(m_TargetPath), ".md", StringComparison.OrdinalIgnoreCase);
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (m_IsMDFile)
            {
                EnsureRenderer();
                if (string.IsNullOrEmpty(m_TargetPath))
                {
                    return new Label("Markdown asset path is empty.");
                }

                // Файл могли удалить или занять, пока инспектор открыт — инспектор при этом не должен падать.
                try
                {
                    m_Renderer.OpenFile(Path.GetFullPath(m_TargetPath));
                }
                catch (Exception ex)
                {
                    return new Label("Couldn't open the Markdown file: " + ex.Message);
                }

                return m_Renderer.RootElement;
            }

            if (m_DefaultEditor == null)
            {
                return new Label("Text asset inspector is not available.");
            }

            IMGUIContainer elem = new(m_DefaultEditor.OnInspectorGUI);

            //by default the stylesheet seems to add some margin, so we reverse them to fit "right"
            elem.style.marginTop = 2;
            elem.style.marginLeft = -15;
            return elem;
        }

        private void EnsureRenderer() => m_Renderer ??= new UIMarkdownRenderer(MarkdownViewer.HandleLink);
    }
}
