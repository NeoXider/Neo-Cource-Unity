using System;
using System.Collections.Generic;
using NeoCource.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    [CustomEditor(typeof(CourseSettings))]
    public class CourseSettingsEditor : UnityEditor.Editor
    {
        private const string FoldoutKeyPrefix = "AlgoNeoCourse.Settings.ModuleFoldout.";

        private SerializedProperty autoLoadOnStartProperty;
        private SerializedProperty courseJsonPresetProperty;
        private SerializedProperty courseJsonFileProperty;
        private SerializedProperty downloadFolderRelativeProperty;
        private SerializedProperty enableDebugLoggingProperty;
        private SerializedProperty failLogColorProperty;
        private SerializedProperty lessonsFolderInRepoProperty;
        private SerializedProperty localCourseJsonPathProperty;
        private SerializedProperty maxLessonTitleLengthProperty;
        private SerializedProperty okLogColorProperty;
        private SerializedProperty repositoryBaseUrlProperty;
        private SerializedProperty testModeProperty;

        private string lessonFilter = string.Empty;
        private GUIStyle lessonTitleStyle;
        private GUIStyle mutedStyle;
        private GUIStyle doneStyle;

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
            localCourseJsonPathProperty = serializedObject.FindProperty("localCourseJsonPath");
            downloadFolderRelativeProperty = serializedObject.FindProperty("downloadFolderRelative");
            lessonsFolderInRepoProperty = serializedObject.FindProperty("lessonsFolderInRepo");
        }

        public override void OnInspectorGUI()
        {
            CourseSettings settings = (CourseSettings)target;
            serializedObject.Update();
            EnsureStyles();

            AlgoNeoEditorGui.DrawHeader(
                "AlgoNeo Course Settings",
                "Источник курса, выбор и загрузка уроков, поведение окна.",
                new Color(0.13f, 0.71f, 0.95f));

            bool sourceChanged = DrawSourceSection(settings);
            DrawBehaviorSection();
            DrawDownloadSection(settings);

            if (serializedObject.ApplyModifiedProperties())
            {
                settings.Persist();
            }

            // Сменили курс (пресет, ссылку или имя JSON) — список уроков подтягивается сам.
            if (sourceChanged)
            {
                settings.LoadLessonsList();
            }

            DrawLessonsSection(settings);
            DrawDangerSection(settings);

            if (settings.IsLoadingLessonsList || settings.IsDownloadingLessons)
            {
                Repaint();
            }
        }

        private void EnsureStyles()
        {
            if (lessonTitleStyle != null)
            {
                return;
            }

            lessonTitleStyle = new GUIStyle(EditorStyles.label) { wordWrap = false, clipping = TextClipping.Clip };
            mutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.62f, 0.66f, 0.72f) }
            };
            doneStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.45f, 0.85f, 0.55f) }
            };
        }

        // Возвращает true, если пользователь поменял источник курса.
        private bool DrawSourceSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Источник курса", new Color(0.13f, 0.71f, 0.95f));

            EditorGUI.BeginChangeCheck();
            // Delayed: текст применяется по Enter или потере фокуса, а не на каждую букву.
            EditorGUILayout.DelayedTextField(repositoryBaseUrlProperty);
            EditorGUILayout.PropertyField(courseJsonPresetProperty);
            if ((CourseSettings.CourseJsonPreset)courseJsonPresetProperty.enumValueIndex ==
                CourseSettings.CourseJsonPreset.Custom)
            {
                EditorGUILayout.DelayedTextField(courseJsonFileProperty, new GUIContent("Custom Course Json"));
            }

            EditorGUILayout.PropertyField(testModeProperty);
            if (testModeProperty.boolValue)
            {
                EditorGUILayout.DelayedTextField(localCourseJsonPathProperty);
            }

            bool changed = EditorGUI.EndChangeCheck();

            AlgoNeoEditorGui.DrawPathRow("Выбранный course json", settings.GetSelectedCourseJsonFile());
            EditorGUILayout.LabelField(
                settings.IsLoadingLessonsList
                    ? "Загружаю список уроков…"
                    : "Список уроков обновляется сам при смене курса.",
                EditorStyles.miniLabel);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Проверить ссылку", new Color(0.2f, 0.55f, 0.95f)))
            {
                settings.CheckCourseLink();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Обновить список", new Color(0.13f, 0.72f, 0.78f)))
            {
                settings.LoadLessonsList();
            }

            EditorGUILayout.EndHorizontal();
            AlgoNeoEditorGui.EndSection();
            return changed;
        }

        private void DrawBehaviorSection()
        {
            AlgoNeoEditorGui.BeginSection("Поведение и UI", new Color(0.36f, 0.82f, 0.61f));
            EditorGUILayout.PropertyField(autoLoadOnStartProperty);
            EditorGUILayout.PropertyField(enableDebugLoggingProperty);
            EditorGUILayout.PropertyField(maxLessonTitleLengthProperty);
            EditorGUILayout.PropertyField(okLogColorProperty);
            EditorGUILayout.PropertyField(failLogColorProperty);
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawDownloadSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Куда сохранять", new Color(0.55f, 0.7f, 0.95f));
            EditorGUILayout.PropertyField(downloadFolderRelativeProperty);
            EditorGUILayout.PropertyField(lessonsFolderInRepoProperty);
            AlgoNeoEditorGui.DrawPathRow("Фактическая папка загрузки", settings.GetDownloadFolderPath());
            AlgoNeoEditorGui.EndSection();
        }

        private void DrawLessonsSection(CourseSettings settings)
        {
            AlgoNeoEditorGui.BeginSection("Уроки по модулям", new Color(0.45f, 0.78f, 1f));

            List<CourseSettings.LessonSelection> all = settings.lessonSelections;
            if (all == null || all.Count == 0)
            {
                AlgoNeoEditorGui.DrawInfoBox(
                    settings.IsLoadingLessonsList
                        ? "Загружаю список уроков…"
                        : "Список уроков пуст. Проверьте источник курса и нажмите «Обновить список».",
                    MessageType.Info);
                AlgoNeoEditorGui.EndSection();
                return;
            }

            List<CourseSettings.LessonModule> modules = settings.GetLessonModules();
            int selectedCount = 0;
            int downloadedCount = 0;
            foreach (CourseSettings.LessonSelection item in all)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.selected)
                {
                    selectedCount++;
                }

                if (settings.IsLessonDownloaded(item))
                {
                    downloadedCount++;
                }
            }

            EditorGUILayout.LabelField(
                $"Модулей: {modules.Count}  ·  уроков: {all.Count}  ·  отмечено: {selectedCount}  ·  скачано: {downloadedCount}",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (AlgoNeoEditorGui.DrawActionButton("Отметить все", new Color(0.21f, 0.7f, 0.46f)))
            {
                settings.SelectAllLessons();
            }

            if (AlgoNeoEditorGui.DrawActionButton("Снять все", new Color(0.38f, 0.42f, 0.5f)))
            {
                settings.DeselectAllLessons();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedCount == 0 || settings.IsDownloadingLessons))
            {
                if (AlgoNeoEditorGui.DrawActionButton($"Скачать отмеченные ({selectedCount})",
                        new Color(0.17f, 0.74f, 0.53f)))
                {
                    settings.DownloadSelectedLessons();
                }
            }

            using (new EditorGUI.DisabledScope(!settings.IsDownloadingLessons))
            {
                if (AlgoNeoEditorGui.DrawActionButton("Отменить", new Color(0.93f, 0.68f, 0.21f)))
                {
                    settings.CancelDownloadsButton();
                }
            }

            if (AlgoNeoEditorGui.DrawActionButton("Очистить загрузки", new Color(0.82f, 0.35f, 0.35f)))
            {
                if (EditorUtility.DisplayDialog("AlgoNeoCourse",
                        "Удалить все скачанные уроки из папки загрузки?", "Удалить", "Отмена"))
                {
                    settings.DeleteDownloadedFiles();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);
            lessonFilter = EditorGUILayout.TextField("Поиск", lessonFilter);
            string filter = (lessonFilter ?? string.Empty).Trim();
            GUILayout.Space(2f);

            bool selectionChanged = false;
            foreach (CourseSettings.LessonModule module in modules)
            {
                selectionChanged |= DrawModule(settings, module, filter);
            }

            if (selectionChanged)
            {
                settings.Persist();
            }

            AlgoNeoEditorGui.EndSection();
        }

        // Один модуль: строка-заголовок (галочка на весь модуль, счётчики, «Скачать») и уроки внутри.
        private bool DrawModule(CourseSettings settings, CourseSettings.LessonModule module, string filter)
        {
            List<CourseSettings.LessonSelection> lessons = module.lessons;
            if (filter.Length > 0)
            {
                lessons = lessons.FindAll(lesson => Matches(lesson, module, filter));
                if (lessons.Count == 0)
                {
                    return false;
                }
            }

            int selected = 0;
            int downloaded = 0;
            foreach (CourseSettings.LessonSelection lesson in module.lessons)
            {
                if (lesson.selected)
                {
                    selected++;
                }

                if (settings.IsLessonDownloaded(lesson))
                {
                    downloaded++;
                }
            }

            bool changed = false;
            string foldoutKey = FoldoutKeyPrefix + module.key;
            bool expanded = filter.Length > 0 || SessionState.GetBool(foldoutKey, false);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Галочка модуля: все уроки отмечены / часть (смешанное состояние) / ни одного.
            EditorGUI.showMixedValue = selected > 0 && selected < module.lessons.Count;
            EditorGUI.BeginChangeCheck();
            bool moduleOn = EditorGUILayout.Toggle(selected == module.lessons.Count, GUILayout.Width(18f));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (CourseSettings.LessonSelection lesson in module.lessons)
                {
                    lesson.selected = moduleOn;
                }

                changed = true;
            }

            EditorGUI.showMixedValue = false;

            Rect headerRect = GUILayoutUtility.GetRect(new GUIContent(module.title), EditorStyles.foldout,
                GUILayout.ExpandWidth(true));
            bool newExpanded = EditorGUI.Foldout(headerRect, expanded, module.title, true);
            if (newExpanded != expanded && filter.Length == 0)
            {
                SessionState.SetBool(foldoutKey, newExpanded);
            }

            GUILayout.Label($"{selected}/{module.lessons.Count} отмечено", mutedStyle, GUILayout.Width(96f));
            GUILayout.Label($"скачано {downloaded}/{module.lessons.Count}",
                downloaded == module.lessons.Count ? doneStyle : mutedStyle, GUILayout.Width(92f));

            using (new EditorGUI.DisabledScope(settings.IsDownloadingLessons))
            {
                if (GUILayout.Button(new GUIContent("Скачать", "Скачать только этот модуль"), EditorStyles.miniButton,
                        GUILayout.Width(64f)))
                {
                    settings.DownloadLessons(module.lessons);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (newExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (CourseSettings.LessonSelection lesson in lessons)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    bool value = EditorGUILayout.ToggleLeft(
                        new GUIContent(CourseSettings.GetShortLessonTitle(lesson), lesson.file), lesson.selected,
                        lessonTitleStyle, GUILayout.MinWidth(60f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        lesson.selected = value;
                        changed = true;
                    }

                    bool isDownloaded = settings.IsLessonDownloaded(lesson);
                    GUILayout.Label(isDownloaded ? "скачан" : lesson.id, isDownloaded ? doneStyle : mutedStyle,
                        GUILayout.Width(70f));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            return changed;
        }

        private static bool Matches(CourseSettings.LessonSelection lesson, CourseSettings.LessonModule module,
            string filter)
        {
            return Contains(lesson.title, filter) || Contains(lesson.id, filter) || Contains(lesson.file, filter) ||
                   Contains(module.key, filter);
        }

        private static bool Contains(string text, string filter)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
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
                settings.LoadLessonsList();
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
