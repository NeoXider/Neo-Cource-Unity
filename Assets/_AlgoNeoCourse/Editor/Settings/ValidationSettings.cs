using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor.Settings
{
    [UnityEditor.FilePath(ValidationSettings.AssetPath, UnityEditor.FilePathAttribute.Location.ProjectFolder)]
    public class ValidationSettings : ScriptableSingleton<ValidationSettings>
    {
        // для ScriptableSingleton стабильнее использовать ProjectSettings
        public const string AssetPath = "ProjectSettings/AlgoNeoValidationSettings.asset";
        // Не создаём ассет принудительно во время загрузки редактора, чтобы избежать повторной инициализации синглтона.
        // Он будет создан автоматически Unity при первом доступе к instance и сохранён при изменении.
        public static void EnsureAssetExists() { /* no-op */ }
        [Header("Диалоги и логирование")]
        public bool EnableDialogs = true;
        public bool ShowSuccessDialog = true;
        public bool ShowFailureDialog = true;
        public bool LogVerbose = false;

        [Header("Debug отображение check-блоков в Markdown")]
        public bool DebugRenderCheckBlocks = false;

        [Header("Debug просмотр примеров из Docs/Examples")] 
        public bool DebugBrowseDocsExamples = false;

        [Header("Отключённые проверки (по ключу)")]
        public List<string> DisabledChecks = new();

        public static ValidationSettings Instance => instance;

        public bool IsCheckEnabled(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return true;
            return !DisabledChecks.Any(k => string.Equals(k?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void DisableCheck(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!DisabledChecks.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                DisabledChecks.Add(key);
                Save(true);
            }
        }

        public void EnableCheck(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
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

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("♻ Сбросить настройки", Sirenix.OdinInspector.ButtonSizes.Medium)]
        [Sirenix.OdinInspector.GUIColor(1f, 0.65f, 0.2f)]
#endif
        public void ResetToDefaults()
        {
            EnableDialogs = true;
            ShowSuccessDialog = true;
            ShowFailureDialog = true;
            LogVerbose = false;
            DebugRenderCheckBlocks = false;
            DebugBrowseDocsExamples = false;
            DisabledChecks.Clear();
            Save(true);
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Reset Validation Settings")] 
        private static void ResetViaMenu()
        {
            EnsureAssetExists();
            instance.ResetToDefaults();
            EditorUtility.DisplayDialog("AlgoNeoCourse", "Validation Settings сброшены к значениям по умолчанию.", "OK");
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider("Project/AlgoNeoCourse/Validation", SettingsScope.Project)
            {
                label = "AlgoNeo Validation",
                activateHandler = (searchContext, root) =>
                {
                    var editor = UnityEditor.Editor.CreateEditor(instance);
                    var container = new IMGUIContainer(() =>
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


