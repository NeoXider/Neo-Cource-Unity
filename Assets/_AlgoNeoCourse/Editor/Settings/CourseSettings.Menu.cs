using System;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    public static class CourseSettingsMenu
    {
        [MenuItem("Tools/AlgoNeoCourse/Settings/Open Course Settings")]
        public static void Open()
        {
            CourseSettings instance = CourseSettings.instance;
            Selection.activeObject = instance;
            try
            {
                if (instance != null && instance.autoLoadOnStart &&
                    (instance.lessonSelections == null || instance.lessonSelections.Count == 0))
                {
                    instance.LoadLessonsList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CourseSettings: авто-загрузка при открытии не удалась — {ex.Message}");
            }
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Reset Course Settings")]
        public static void Reset()
        {
            CourseSettings.EnsureAssetExists();
            CourseSettings.instance.ResetToDefaults();
            EditorUtility.DisplayDialog("AlgoNeoCourse", "Course Settings сброшены к значениям по умолчанию.", "OK");
        }
    }

    [InitializeOnLoad]
    public static class CourseSettingsBootstrap
    {
        static CourseSettingsBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    CourseSettings instance = CourseSettings.instance;
                    if (instance != null && instance.autoLoadOnStart &&
                        (instance.lessonSelections == null || instance.lessonSelections.Count == 0))
                    {
                        instance.LoadLessonsList();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("CourseSettings: авто-загрузка при старте не удалась — " + ex.Message);
                }
            };
        }
    }
}