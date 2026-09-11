using System;
using System.Collections.Generic;
using NeoCource.Editor.GifSupport;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow : EditorWindow
    {
        private const string WindowTitle = "AlgoNeo Course";

        private readonly List<(string title, string filePath, string id)> availableLessons = new();
        private bool autoReloadScheduled;
        private bool canGoNextNow = true;

        private ScrollView contentRoot;
        private string currentLessonFilePath;
        private string currentLessonTitle;
        private int currentSlideIndex;

        // Quiz state for current slide
        private List<QuizQuestion> currentSlideQuestions = new();
        private ToolbarMenu docsMenu;
        private readonly List<(string title, string filePath, string id)> filteredLessons = new();
        private PopupField<string> lessonDropdown;
        private PopupField<string> lessonGroupDropdown;
        private PopupField<string> lessonModuleDropdown;
        private UIMarkdownRenderer.UIMarkdownRenderer mdRenderer;
        private ToolbarButton continueBtn;
        private VisualElement courseProgressFill;
        private Label courseProgressLabel;
        private VisualElement emptyStateBox;
        private ToolbarButton nextBtn;
        private ToolbarButton openInExplorerBtn;
        private ToolbarButton prevBtn;
        private ToolbarButton reloadBtn;
        private ToolbarButton resetProgressBtn;
        private Label slideIndicator;
        private List<string> slides = new();

        private Toolbar toolbar;
        private string selectedLessonGroup;
        private string selectedLessonModule;

        // Пока идёт восстановление сессии после открытия/перезагрузки —
        // промежуточные LoadLesson/ShowSlide не должны затирать сохранённую позицию.
        private bool suppressSessionSave;

        private int LessonDropdownMaxLength =>
            Math.Max(5, CourseSettings.instance?.maxLessonTitleLength ?? 20);

        public void CreateGUI()
        {
            // Заголовок слетает на имя типа после перезагрузки домена —
            // выставляем при каждом создании GUI, а не только в Open().
            titleContent = new GUIContent(WindowTitle);
            try
            {
                // Защита от двойной подписки при повторном создании GUI.
                GifConverter.ConversionFinished -= OnGifConversionFinished;
                GifConverter.ConversionFinished += OnGifConversionFinished;
            }
            catch
            {
            }

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.AddToClassList("algo-course-window");
            LoadWindowStyles();

            BuildToolbar();
            BuildContent();
            EnsureHotkeysHook();

            EditorApplication.delayCall += () =>
            {
                try
                {
                    EnsureDocsMenuFromSettings();
                    RefreshLessonsList();
                    RestoreLastSession();
                    SaveLastSession();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"CourseWindow: deferred init failed — {ex.Message}");
                }
            };
        }

        [MenuItem("Tools/AlgoNeoCourse/Open Course Window", priority = 1)]
        public static void Open()
        {
            CourseWindow wnd = GetWindow<CourseWindow>(false, WindowTitle, true);
            wnd.minSize = new Vector2(640, 420);
            wnd.Focus();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () =>
            {
                CourseWindow[] windows = Resources.FindObjectsOfTypeAll<CourseWindow>();
                foreach (CourseWindow w in windows)
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            w.RestoreLastSession();
                        }
                        catch
                        {
                        }
                    };
                }
            };
        }

        // GIF-конвертация асинхронна: по завершении перерисовываем слайд и прогресс.
        private void OnGifConversionFinished()
        {
            try
            {
                if (this == null)
                {
                    return;
                }

                ShowSlide(currentSlideIndex);
                UpdateCourseProgressUI();
            }
            catch
            {
            }
        }

        private void OnDestroy()
        {
            try
            {
                GifConverter.ConversionFinished -= OnGifConversionFinished;
            }
            catch
            {
            }
        }
    }
}