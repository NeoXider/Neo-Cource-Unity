using System;
using NeoCource.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Settings
{
    public partial class CourseSettings
    {
        private static void RepaintProjectWindow()
        {
            EditorApplication.RepaintProjectWindow();
            SceneView.RepaintAll();
        }

        private void SaveIfPossible()
        {
            try
            {
                SaveAsset();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("CourseSettings: сохранение настроек пропущено — " + ex.Message);
            }
        }

        public void Persist()
        {
            SaveIfPossible();
        }

        public void ResetToDefaults()
        {
            ResetToDefaultsWithoutSaving();
            SaveAsset();
        }
    }
}