using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor.Settings
{
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public class ValidationSettings : ScriptableSingleton<ValidationSettings>
    {
        // для ScriptableSingleton стабильнее использовать ProjectSettings
        public const string AssetPath = "ProjectSettings/AlgoNeoValidationSettings.asset";

        [Header("Диалоги и логирование")] public bool EnableDialogs;

        public bool ShowSuccessDialog = true;
        public bool ShowFailureDialog = true;
        public bool LogVerbose = true;

        [Header("Debug отображение check-блоков в Markdown")]
        public bool DebugRenderCheckBlocks = true;

        [Header("Debug просмотр примеров из Docs/Examples")]
        public bool DebugBrowseDocsExamples = true;

        [Header("Отключённые проверки (по ключу)")]
        public List<string> DisabledChecks = new();

        public static ValidationSettings Instance => instance;

        // Не создаём ассет принудительно во время загрузки редактора, чтобы избежать повторной инициализации синглтона.
        // Он будет создан автоматически Unity при первом доступе к instance и сохранён при изменении.
        public static void EnsureAssetExists()
        {
            /* no-op */
        }

        public bool IsCheckEnabled(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            return !DisabledChecks.Any(k => string.Equals(k?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void DisableCheck(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!DisabledChecks.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                DisabledChecks.Add(key);
                Save(true);
            }
        }

        public void EnableCheck(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            int idx = DisabledChecks.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                DisabledChecks.RemoveAt(idx);
                Save(true);
            }
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Open Validation Settings")]
        private static void OpenViaMenu()
        {
            Selection.activeObject = instance;
        }

        public void ResetToDefaults()
        {
            EnableDialogs = false;
            ShowSuccessDialog = true;
            ShowFailureDialog = true;
            LogVerbose = true;
            DebugRenderCheckBlocks = true;
            DebugBrowseDocsExamples = true;
            DisabledChecks.Clear();
            Save(true);
        }

        public void Persist()
        {
            Save(true);
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Reset Validation Settings")]
        private static void ResetViaMenu()
        {
            EnsureAssetExists();
            instance.ResetToDefaults();
            EditorUtility.DisplayDialog("AlgoNeoCourse", "Validation Settings сброшены к значениям по умолчанию.",
                "OK");
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            SettingsProvider provider = new("Project/AlgoNeoCourse/Validation", SettingsScope.Project)
            {
                label = "AlgoNeo Validation",
                activateHandler = (searchContext, root) =>
                {
                    UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(instance);
                    IMGUIContainer container = new(() =>
                    {
                        if (editor != null)
                        {
                            editor.OnInspectorGUI();
                        }
                    });
                    root.Add(container);
                }
            };
            return provider;
        }
    }

    // Убираем автосоздание на загрузке, чтобы не триггерить повторные конструкторы ScriptableSingleton
}