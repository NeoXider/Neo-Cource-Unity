using System;
using System.Collections.Generic;
using System.IO;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        private void BuildToolbar()
        {
            toolbar = new Toolbar();
            toolbar.AddToClassList("algo-course-toolbar");

            lessonDropdown = new PopupField<string>(new List<string>(), 0)
            {
                tooltip = "Выберите урок"
            };
            lessonDropdown.AddToClassList("algo-course-dropdown");
            lessonDropdown.formatSelectedValueCallback = value =>
            {
                if (string.IsNullOrEmpty(value))
                {
                    return value;
                }

                bool needTruncate = value.Length > LessonDropdownMaxLength;
                lessonDropdown.tooltip = needTruncate ? value : "Выберите урок";
                return needTruncate ? value.Substring(0, LessonDropdownMaxLength - 3) + "..." : value;
            };
            lessonDropdown.formatListItemCallback = value =>
            {
                if (string.IsNullOrEmpty(value))
                {
                    return value;
                }

                return value.Length > LessonDropdownMaxLength
                    ? value.Substring(0, LessonDropdownMaxLength - 3) + "..."
                    : value;
            };
            lessonDropdown.RegisterValueChangedCallback(_ =>
            {
                int idx = lessonDropdown.index;
                if (idx >= 0 && idx < availableLessons.Count)
                {
                    LoadLesson(availableLessons[idx]);
                    SaveLastSession();
                }
            });
            toolbar.Add(lessonDropdown);

            toolbar.Add(new ToolbarSpacer());

            Texture2D settingsIcon = (Texture2D)(EditorGUIUtility.IconContent("d__Popup@2x").image ??
                                                 EditorGUIUtility.IconContent("SettingsIcon").image ??
                                                 EditorGUIUtility.IconContent("_Popup").image);
            ToolbarButton settingsBtn = CreateIconButton(settingsIcon, "Открыть Course Settings",
                () => Selection.activeObject = CourseSettings.instance);
            settingsBtn.AddToClassList("settings");
            toolbar.Add(settingsBtn);

            prevBtn = new ToolbarButton(() => ShowSlide(currentSlideIndex - 1)) { text = "<" };
            nextBtn = new ToolbarButton(() => ShowSlide(currentSlideIndex + 1)) { text = ">" };
            prevBtn.AddToClassList("algo-course-nav-button");
            prevBtn.AddToClassList("prev");
            nextBtn.AddToClassList("algo-course-nav-button");
            nextBtn.AddToClassList("next");
            prevBtn.tooltip = "Предыдущий";
            nextBtn.tooltip = "Следующий";
            prevBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            nextBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            prevBtn.style.color = new StyleColor(new Color(0.65f, 0.75f, 1f));
            nextBtn.style.color = new StyleColor(new Color(0.6f, 1f, 0.6f));
            nextBtn.style.minWidth = 60;
            slideIndicator = new Label("—/—") { style = { unityTextAlign = TextAnchor.MiddleCenter, minWidth = 60 } };
            slideIndicator.AddToClassList("algo-course-slide-indicator");

            toolbar.Add(prevBtn);
            toolbar.Add(slideIndicator);
            toolbar.Add(nextBtn);

            toolbar.Add(new ToolbarSpacer());

            Texture2D refreshTex = (Texture2D)(EditorGUIUtility.IconContent("d_Refresh").image ??
                                               EditorGUIUtility.IconContent("Refresh").image);
            reloadBtn = CreateIconButton(refreshTex, "Обновить уроки", DoRefreshLessonsAndCurrent);
            reloadBtn.AddToClassList("reload");
            reloadBtn.style.color = new StyleColor(new Color(0.2f, 0.8f, 0.8f));

            Texture2D resetTex = (Texture2D)(EditorGUIUtility.IconContent("d_TreeEditor.Trash").image ??
                                             EditorGUIUtility.IconContent("TreeEditor.Trash").image);
            resetProgressBtn = CreateIconButton(resetTex, "Сбросить локальный прогресс", ResetProgressAndReload);
            resetProgressBtn.AddToClassList("reset");

            openInExplorerBtn = new ToolbarButton(() =>
            {
                if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                {
                    EditorUtility.RevealInFinder(currentLessonFilePath);
                }
            });
            Texture2D folderTex = (Texture2D)(EditorGUIUtility.IconContent("Folder Icon").image ??
                                              EditorGUIUtility.IconContent("d_Project").image);
            openInExplorerBtn.text = string.Empty;
            openInExplorerBtn.AddToClassList("algo-course-icon-button");
            Image img = new() { image = folderTex, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 16;
            img.style.height = 16;
            img.style.marginTop = 2;
            img.style.marginBottom = 2;
            openInExplorerBtn.Add(img);
            openInExplorerBtn.tooltip = "Показать файл";

            toolbar.Add(reloadBtn);
            toolbar.Add(resetProgressBtn);
            toolbar.Add(openInExplorerBtn);

            rootVisualElement.Add(toolbar);
            RegisterToolbarHotkeys();
        }

        private void RegisterToolbarHotkeys()
        {
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                try
                {
                    if (evt.keyCode == KeyCode.LeftArrow)
                    {
                        ShowSlide(currentSlideIndex - 1);
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.RightArrow)
                    {
                        if (CanGoNextSlide())
                        {
                            ShowSlide(currentSlideIndex + 1);
                        }
                        else if (QuizSettings.instance.enableDebugLogging)
                        {
                            Debug.Log("[Quiz] Навигация вперёд заблокирована: есть незавершённые вопросы");
                        }

                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.R)
                    {
                        DoRefreshLessonsAndCurrent();
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.O)
                    {
                        if (!string.IsNullOrEmpty(currentLessonFilePath) && File.Exists(currentLessonFilePath))
                        {
                            EditorUtility.RevealInFinder(currentLessonFilePath);
                        }

                        evt.StopPropagation();
                    }
                }
                catch
                {
                }
            }, TrickleDown.TrickleDown);
        }

        private void EnsureHotkeysHook()
        {
            try
            {
                rootVisualElement.focusable = true;
                rootVisualElement.pickingMode = PickingMode.Position;
                rootVisualElement.tabIndex = 0;
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        TryFocusWindowForHotkeys();
                    }
                };
            }
            catch
            {
            }
        }

        private void TryFocusWindowForHotkeys()
        {
            try
            {
                rootVisualElement?.Focus();
                Focus();
            }
            catch
            {
            }
        }

        private static ToolbarButton CreateIconButton(Texture2D icon, string tooltip, Action onClick)
        {
            ToolbarButton btn = new(onClick) { tooltip = tooltip };
            btn.text = string.Empty;
            btn.AddToClassList("algo-course-icon-button");
            Image img = new() { image = icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 16;
            img.style.height = 16;
            img.style.marginTop = 2;
            img.style.marginBottom = 2;
            btn.Add(img);
            return btn;
        }
    }
}