using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.Progress;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace NeoCource.Editor.Settings
{
    [UnityEditor.FilePath(QuizSettings.AssetPath, UnityEditor.FilePathAttribute.Location.ProjectFolder)]
    public class QuizSettings : ScriptableSingleton<QuizSettings>
    {
        public const string AssetPath = "ProjectSettings/AlgoNeoCourseQuizSettings.asset";
        public static void EnsureAssetExists() { /* no-op */ }

#if ODIN_INSPECTOR
        [Title("Поведение викторин")]
#endif
        [Tooltip("Максимум попыток на один вопрос")] public int maxAttemptsPerQuestion = 2;
        [Tooltip("Перемешивать варианты один раз при открытии курса")] public bool randomizeAnswersOnCourseOpen = true;
        [Tooltip("Блокировать переход на следующий слайд при незавершённых вопросах")] public bool guardSlideNavigation = true;
        [Tooltip("Подробное логирование событий квиза в консоль")] public bool enableDebugLogging = false;

#if ODIN_INSPECTOR
        [Title("Типы вопросов")]
#endif
        [Tooltip("Разрешить одиночный выбор (single-choice)")] public bool enableSingleChoice = true;
        [Tooltip("Разрешить множественный выбор (multiple-choice)")] public bool enableMultipleChoice = true;
        [Tooltip("Разрешить True/False вопросы")] public bool enableTrueFalse = true;

#if ODIN_INSPECTOR
        [Title("Сохранение состояния")] 
#endif
        [Tooltip("Сохранение прогресса квизов всегда включено")] public bool persistState = true;
        [Tooltip("Прогресс хранится в локальном JSON-файле")] public bool saveStateAsJson = true;
        [Tooltip("Папка для локального JSON прогресса (только в Assets, не в Packages)")] public string stateJsonFolder = AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath;

        public string GetProgressFolderAssetPath()
        {
            return AlgoNeoPackageAssetLocator.NormalizeWritableAssetPath(
                stateJsonFolder,
                AlgoNeoPackageAssetLocator.DefaultProgressFolderAssetPath);
        }

        public string GetProgressFileAssetPath()
        {
            return GetProgressFolderAssetPath().TrimEnd('/') + "/course-progress.json";
        }

#if ODIN_INSPECTOR
        [PropertySpace]
        [HorizontalGroup("actions")]
        [GUIColor(0.45f, 0.8f, 0.95f)]
        [Button("Открыть папку сохранений", ButtonSizes.Medium)]
#endif
        public void OpenStateFolder()
        {
            try
            {
                string full = AlgoNeoPackageAssetLocator.ToAbsolutePath(GetProgressFolderAssetPath());
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                EditorUtility.RevealInFinder(full);
            }
            catch (Exception ex)
            {
                Debug.LogError("QuizSettings: не удалось открыть папку — " + ex.Message);
            }
        }

#if ODIN_INSPECTOR
        [HorizontalGroup("actions")]
        [GUIColor(1.0f, 0.35f, 0.35f)]
        [Button("Очистить сохранения", ButtonSizes.Medium)]
#endif
        public void ClearState()
        {
            try
            {
                CourseProgressStore.Clear();
                if (Directory.Exists(AlgoNeoPackageAssetLocator.ToAbsolutePath(GetProgressFolderAssetPath())))
                {
                    Debug.Log("QuizSettings: сохранения очищены");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("QuizSettings: ошибка очистки — " + ex.Message);
            }
        }
    }

    public static class QuizSettingsMenu
    {
        [MenuItem("Tools/AlgoNeoCourse/Settings/Open Quiz Settings")]
        public static void Open()
        {
            Selection.activeObject = QuizSettings.instance;
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Reset Course Progress")]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("AlgoNeoCourse", "Сбросить локальный прогресс курса и все сохранения квизов?", "Сбросить", "Отмена"))
            {
                return;
            }

            QuizSettings.instance.ClearState();
        }
    }
}


