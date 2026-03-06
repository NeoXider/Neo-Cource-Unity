using UnityEditor;
using UnityEngine;
using NeoCource.Editor.Utils;

namespace NeoCource.Editor.Settings
{
    [CustomEditor(typeof(ValidationSettings))]
    public class ValidationSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty enableDialogsProperty;
        private SerializedProperty showSuccessDialogProperty;
        private SerializedProperty showFailureDialogProperty;
        private SerializedProperty logVerboseProperty;
        private SerializedProperty debugRenderCheckBlocksProperty;
        private SerializedProperty debugBrowseDocsExamplesProperty;
        private SerializedProperty disabledChecksProperty;

        private void OnEnable()
        {
            enableDialogsProperty = serializedObject.FindProperty("EnableDialogs");
            showSuccessDialogProperty = serializedObject.FindProperty("ShowSuccessDialog");
            showFailureDialogProperty = serializedObject.FindProperty("ShowFailureDialog");
            logVerboseProperty = serializedObject.FindProperty("LogVerbose");
            debugRenderCheckBlocksProperty = serializedObject.FindProperty("DebugRenderCheckBlocks");
            debugBrowseDocsExamplesProperty = serializedObject.FindProperty("DebugBrowseDocsExamples");
            disabledChecksProperty = serializedObject.FindProperty("DisabledChecks");
        }

        public override void OnInspectorGUI()
        {
            var settings = (ValidationSettings)target;
            serializedObject.Update();

            AlgoNeoEditorGui.DrawHeader(
                "AlgoNeo Validation Settings",
                "Настройка диалогов, отладочных check-блоков и отключённых проверок в едином тёмном стиле.",
                new Color(0.98f, 0.68f, 0.24f));

            AlgoNeoEditorGui.BeginSection("Диалоги и логирование", new Color(0.98f, 0.68f, 0.24f));
            EditorGUILayout.PropertyField(enableDialogsProperty);
            EditorGUILayout.PropertyField(showSuccessDialogProperty);
            EditorGUILayout.PropertyField(showFailureDialogProperty);
            EditorGUILayout.PropertyField(logVerboseProperty);
            AlgoNeoEditorGui.EndSection();

            AlgoNeoEditorGui.BeginSection("Отладочное отображение", new Color(0.45f, 0.78f, 1f));
            EditorGUILayout.PropertyField(debugRenderCheckBlocksProperty);
            EditorGUILayout.PropertyField(debugBrowseDocsExamplesProperty);
            AlgoNeoEditorGui.EndSection();

            AlgoNeoEditorGui.BeginSection("Отключённые проверки", new Color(0.73f, 0.58f, 0.95f));
            EditorGUILayout.PropertyField(disabledChecksProperty, true);
            AlgoNeoEditorGui.DrawInfoBox("Список используется для временного отключения конкретных правил по их ключу.", MessageType.None);
            AlgoNeoEditorGui.EndSection();

            AlgoNeoEditorGui.BeginSection("Сброс", new Color(0.9f, 0.38f, 0.34f));
            if (AlgoNeoEditorGui.DrawActionButton("Сбросить настройки", new Color(0.86f, 0.42f, 0.31f)))
            {
                settings.ResetToDefaults();
                serializedObject.Update();
            }
            AlgoNeoEditorGui.EndSection();

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.Persist();
            }
        }
    }
}
