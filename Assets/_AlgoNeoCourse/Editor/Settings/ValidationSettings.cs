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

        // По умолчанию блок check показывается только карточкой задания. В debug под карточкой
        // показывается и сам исходник ```check — удобно автору урока сверять правила.
        // (Прежнее поле DebugRenderCheckBlocks ни на что не влияло и у всех было включено,
        // поэтому новое поле названо иначе: старые сохранённые настройки его не включат.)
        [Header("Debug: исходник check-блоков")]
        public bool DebugShowCheckSource;

        [Header("Debug просмотр примеров из Docs/Examples")]
        public bool DebugBrowseDocsExamples = true;

        [Header("Отключённые проверки (по ключу)")]
        public List<string> DisabledChecks = new();

        public static ValidationSettings Instance => instance;

        // Настройки изменились и сохранены: открытое окно курса перерисовывает текущий слайд.
        public static event Action Changed;

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

            // Список мог стать null после ручной правки ассета.
            DisabledChecks ??= new List<string>();
            return !DisabledChecks.Any(k => string.Equals(k?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void DisableCheck(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            // Список мог стать null после ручной правки ассета.
            DisabledChecks ??= new List<string>();
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

            // Список мог стать null после ручной правки ассета.
            DisabledChecks ??= new List<string>();
            int idx = DisabledChecks.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                DisabledChecks.RemoveAt(idx);
                Save(true);
            }
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Open Validation Settings", priority = 102)]
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
            DebugShowCheckSource = false;
            DebugBrowseDocsExamples = true;
            // Список мог стать null после ручной правки ассета.
            DisabledChecks ??= new List<string>();
            DisabledChecks.Clear();
            Persist();
        }

        public void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }

        [MenuItem("Tools/AlgoNeoCourse/Settings/Reset/Reset Validation Settings", priority = 161)]
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