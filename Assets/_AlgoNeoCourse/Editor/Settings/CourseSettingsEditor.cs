using NeoCource.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    [CustomEditor(typeof(CourseSettings))]
    public class CourseSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty autoConvertGifToMp4Property;
        private SerializedProperty autoLoadOnStartProperty;
        private SerializedProperty courseJsonPresetProperty;
        private SerializedProperty courseJsonFileProperty;
        private SerializedProperty downloadFolderRelativeProperty;
        private SerializedProperty enableDebugLoggingProperty;
        private SerializedProperty failLogColorProperty;
        private SerializedProperty ffmpegPathProperty;
        private SerializedProperty gifVideoCacheFolderProperty;
        private SerializedProperty gifConversionFpsProperty;
        private SerializedProperty gifConversionMaxWidthProperty;
        private SerializedProperty lessonSelectionsProperty;
        private SerializedProperty lessonsFolderInRepoProperty;
        private SerializedProperty localCourseJsonPathProperty;
        private SerializedProperty maxLessonTitleLengthProperty;
        private SerializedProperty okLogColorProperty;
        private SerializedProperty repositoryBaseUrlProperty;
        private SerializedProperty testModeProperty;

        private void OnEnable()
        {
            repositoryBaseUrlProperty = serializedObject.FindProperty("repositoryBaseUrl");
            courseJsonPresetProperty = serializedObject.FindProperty("courseJsonPreset");
            courseJsonFileProperty = serializedObject.FindProperty("courseJsonFile");
            autoLoadOnStartProperty = serializedObject.FindProperty("autoLoadOnStart");
            enableDebugLoggingProperty = serializedObject.FindProperty("enableDebugLogging");
            testModeProperty = serializedObject.FindProperty("testMode");
            maxLessonTitleLengthProperty = serializedObject.FindProperty("maxLessonTitleLength");
            okLogColorProperty = serializedObject.FindProperty("okLogColor");
            failLogColorProperty = serializedObject.FindProperty("failLogColor");
            autoConvertGifToMp4Property = serializedObject.FindProperty("autoConvertGifToMp4");
            ffmpegPathProperty = serializedObject.FindProperty("ffmpegPath");
            gifVideoCacheFolderProperty = serializedObject.FindProperty("gifVideoCacheFolder");
            gifConversionFpsProperty = serializedObject.FindProperty("gifConversionFps");
            gifConversionMaxWidthProperty = serializedObject.FindProperty("gifConversionMaxWidth");
            localCourseJsonPathProperty = serializedObject.FindProperty("localCourseJsonPath");
            downloadFolderRelativeProperty = serializedObject.FindProperty("downloadFolderRelative");
            lessonsFolderInRepoProperty = serializedObject.FindProperty("lessonsFolderInRepo");
            lessonSelectionsProperty = serializedObject.FindProperty("lessonSelections");
        }

        public override void OnInspectorGUI()
        {
            CourseSettings settings = (CourseSettings)target;
            serializedObject.Update();

            AlgoNeoEditorGui.DrawHeader(
                "AlgoNeo Course Settings",
                "Управление источником курса, загрузкой уроков и внешними инструментами в тёмной компактной панели.",
                new Color(0.13f, 0.71f, 0.95f));

            DrawSourceSection(settings);
            DrawBehaviorSection(settings);
            DrawLessonsSection(settings);
            DrawMediaSection(settings);
            DrawDangerSection(settings);

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.Persist();
            }
        }

        private void DrawSourceSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Источник курса", new Color(0.13f, 0.71f, 0.95f));
            EditorGUILayout.PropertyField(repositoryBaseUrlProperty);
            EditorGUILayout.PropertyField(courseJsonPresetProperty);
            if ((CourseSettings.CourseJsonPreset)courseJsonPresetProperty.enumValueIndex == CourseSettings.CourseJsonPreset.Custom)
            {
                EditorGUILayout.PropertyField(courseJsonFileProperty, new GUIContent("Custom Course Json"));
            }

            AlgoNeoEditorGui.DrawPathRow("Выбранный course json", settings.GetSelectedCourseJsonFile());
            EditorGUILayout.PropertyField(testModeProperty);
            if (testModeProperty.boolValue)
            {
                EditorGUILayout.PropertyField(localCourseJsonPathProperty);
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Проверить ссылку", new Color(0.2f, 0.55f, 0.95f)))
            {
                settings.CheckCourseLink();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Загрузить список уроков", new Color(0.13f, 0.72f, 0.78f)))
            {
                settings.LoadLessonsList();
            }

            EditorGUILayout.EndHorizontal();
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawBehaviorSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Поведение и UI", new Color(0.36f, 0.82f, 0.61f));
            EditorGUILayout.PropertyField(autoLoadOnStartProperty);
            EditorGUILayout.PropertyField(enableDebugLoggingProperty);
            EditorGUILayout.PropertyField(maxLessonTitleLengthProperty);
            EditorGUILayout.PropertyField(okLogColorProperty);
            EditorGUILayout.PropertyField(failLogColorProperty);
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawLessonsSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Уроки и загрузка", new Color(0.45f, 0.78f, 1f));
            EditorGUILayout.PropertyField(downloadFolderRelativeProperty);
            EditorGUILayout.PropertyField(lessonsFolderInRepoProperty);

            int selectedCount = 0;
            if (settings.lessonSelections != null)
            {
                foreach (CourseSettings.LessonSelection item in settings.lessonSelections)
                {
                    if (item != null && item.selected)
                    {
                        selectedCount++;
                    }
                }
            }

            AlgoNeoEditorGui.DrawInfoBox(
                $"Загружено элементов списка: {settings.lessonSelections?.Count ?? 0}. Выбрано для скачивания: {selectedCount}.",
                MessageType.None);
            AlgoNeoEditorGui.DrawPathRow("Фактическая папка загрузки", settings.GetDownloadFolderPath());

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Выбрать все", new Color(0.21f, 0.7f, 0.46f)))
            {
                settings.SelectAllLessons();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Снять выбор", new Color(0.38f, 0.42f, 0.5f)))
            {
                settings.DeselectAllLessons();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Скачать выбранные", new Color(0.17f, 0.74f, 0.53f)))
            {
                settings.DownloadSelectedLessons();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Отменить", new Color(0.93f, 0.68f, 0.21f)))
            {
                settings.CancelDownloadsButton();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Очистить загрузки", new Color(0.82f, 0.35f, 0.35f)))
            {
                settings.DeleteDownloadedFiles();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(lessonSelectionsProperty, true);
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawMediaSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Медиа и GIF", new Color(0.72f, 0.56f, 0.95f));
            EditorGUILayout.PropertyField(autoConvertGifToMp4Property);
            EditorGUILayout.PropertyField(ffmpegPathProperty);
            EditorGUILayout.PropertyField(gifVideoCacheFolderProperty);
            EditorGUILayout.PropertyField(gifConversionFpsProperty);
            EditorGUILayout.PropertyField(gifConversionMaxWidthProperty);
            AlgoNeoEditorGui.DrawPathRow("Кэш видео", settings.GetGifVideoCacheFolderPath());
            AlgoNeoEditorGui.DrawInfoBox(
                "Для максимальной скорости держите FPS в диапазоне 10-12 и ограничивайте ширину видео, если GIF большие.",
                MessageType.Info);
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawDangerSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Сброс и обслуживание", new Color(0.97f, 0.45f, 0.36f));
            AlgoNeoEditorGui.DrawInfoBox(
                "Здесь можно отдельно сбросить настройки курса или полностью очистить локальный прогресс и сохранения квизов.",
                MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Сбросить настройки", new Color(0.86f, 0.42f, 0.31f)))
            {
                settings.ResetToDefaults();
                serializedObject.Update();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Очистить сохранения", new Color(0.77f, 0.26f, 0.26f)))
            {
                if (EditorUtility.DisplayDialog(
                        "AlgoNeoCourse",
                        "Сбросить локальный прогресс курса и все сохранения квизов?",
                        "Сбросить",
                        "Отмена"))
                {
                    QuizSettings.instance.ClearState();
                }
            }

            EditorGUILayout.EndHorizontal();
            AlgoNeoEditorGui.EndSection();
        }
    }
}