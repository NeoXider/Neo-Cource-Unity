using NeoCource.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    [CustomEditor(typeof(QuizSettings))]
    public class QuizSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty enableDebugLoggingProperty;
        private SerializedProperty enableMultipleChoiceProperty;
        private SerializedProperty enableSingleChoiceProperty;
        private SerializedProperty enableTrueFalseProperty;
        private SerializedProperty guardSlideNavigationProperty;
        private SerializedProperty maxAttemptsPerQuestionProperty;
        private SerializedProperty randomizeAnswersOnCourseOpenProperty;
        private SerializedProperty stateJsonFolderProperty;

        private void OnEnable()
        {
            maxAttemptsPerQuestionProperty = serializedObject.FindProperty("maxAttemptsPerQuestion");
            randomizeAnswersOnCourseOpenProperty = serializedObject.FindProperty("randomizeAnswersOnCourseOpen");
            guardSlideNavigationProperty = serializedObject.FindProperty("guardSlideNavigation");
            enableDebugLoggingProperty = serializedObject.FindProperty("enableDebugLogging");
            enableSingleChoiceProperty = serializedObject.FindProperty("enableSingleChoice");
            enableMultipleChoiceProperty = serializedObject.FindProperty("enableMultipleChoice");
            enableTrueFalseProperty = serializedObject.FindProperty("enableTrueFalse");
            stateJsonFolderProperty = serializedObject.FindProperty("stateJsonFolder");
        }

        public override void OnInspectorGUI()
        {
            QuizSettings settings = (QuizSettings)target;
            serializedObject.Update();

            AlgoNeoEditorGui.DrawHeader(
                "AlgoNeo Quiz Settings",
                "Управление попытками, навигацией по слайдам и локальным JSON-прогрессом квизов.",
                new Color(0.18f, 0.78f, 0.89f));

            AlgoNeoEditorGui.BeginSection("Поведение квизов", new Color(0.18f, 0.78f, 0.89f));
            EditorGUILayout.PropertyField(maxAttemptsPerQuestionProperty);
            EditorGUILayout.PropertyField(randomizeAnswersOnCourseOpenProperty);
            EditorGUILayout.PropertyField(guardSlideNavigationProperty);
            EditorGUILayout.PropertyField(enableDebugLoggingProperty);
            AlgoNeoEditorGui.EndSection();

            AlgoNeoEditorGui.BeginSection("Разрешённые типы вопросов", new Color(0.4f, 0.83f, 0.57f));
            EditorGUILayout.PropertyField(enableSingleChoiceProperty);
            EditorGUILayout.PropertyField(enableMultipleChoiceProperty);
            EditorGUILayout.PropertyField(enableTrueFalseProperty);
            AlgoNeoEditorGui.EndSection();

            AlgoNeoEditorGui.BeginSection("Локальное сохранение", new Color(0.75f, 0.59f, 0.95f));
            EditorGUILayout.PropertyField(stateJsonFolderProperty);
            AlgoNeoEditorGui.DrawPathRow("Папка прогресса", settings.GetProgressFolderAssetPath());
            AlgoNeoEditorGui.DrawPathRow("Файл прогресса", settings.GetProgressFileAssetPath());
            AlgoNeoEditorGui.DrawInfoBox("Состояние всех квизов сохраняется автоматически в один локальный JSON-файл.",
                MessageType.None);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Открыть папку сохранений", new Color(0.25f, 0.63f, 0.95f)))
            {
                settings.OpenStateFolder();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Очистить сохранения", new Color(0.83f, 0.34f, 0.34f)))
            {
                if (EditorUtility.DisplayDialog(
                        "AlgoNeoCourse",
                        "Сбросить локальный прогресс курса и все сохранения квизов?",
                        "Сбросить",
                        "Отмена"))
                {
                    settings.ClearState();
                }
            }

            EditorGUILayout.EndHorizontal();
            AlgoNeoEditorGui.EndSection();

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.Persist();
            }
        }
    }
}