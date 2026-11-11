using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NeoCource.Editor.Quizzes;
using NeoCource.Editor.Settings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow : EditorWindow
    {
        private const string WindowTitle = "AlgoNeo Course";

        private readonly List<(string title, string filePath, string id)> availableLessons = new();
        private bool canGoNextNow = true;

        private ScrollView contentRoot;
        private string currentLessonFilePath;
        private string currentLessonTitle;
        private int currentSlideIndex;
        private string lastShownLessonPath;
        private int lastShownSlideIndex = -1;

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

        // Длина названия урока в дропдауне (применяется к последнему сегменту "l2 / m2 / Название").
        private int LessonDropdownMaxLength =>
            Math.Max(8, CourseSettings.instance?.maxLessonTitleLength ?? CourseSettings.DefaultMaxLessonTitleLength);

        // Единственное определение блока ```check: по нему ставится кнопка «Проверить», строится
        // карточка задания и считаются проверки урока. Применять к тексту с LF-переводами строк
        // (слайды после SplitSlides уже такие).
        internal static readonly Regex CheckBlockRegex = new(@"```check[ \t]*\n([\s\S]*?)\n```");

        // Ссылка-транспорт кнопки «Проверить» для блока check; её текст — он же id проверки в прогрессе.
        internal static string BuildCheckLink(string rawBlock)
        {
            return "unity://check?type=from-block&dialog=auto&__raw_block__=" + Uri.EscapeDataString(rawBlock);
        }

        public void CreateGUI()
        {
            // Заголовок слетает на имя типа после перезагрузки домена —
            // выставляем при каждом создании GUI, а не только в Open().
            titleContent = new GUIContent(WindowTitle);
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.AddToClassList("algo-course-window");
            LoadWindowStyles();

            BuildToolbar();
            BuildContent();
            EnsureHotkeysHook();

            EditorApplication.delayCall += () =>
            {
                // Окно могли закрыть до отложенного вызова.
                if (this == null || toolbar == null)
                {
                    return;
                }

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

        // Единственный пункт верхнего уровня: Ctrl+Alt+L. Всё остальное живёт в подменю Settings.
        [MenuItem("Tools/AlgoNeoCourse/Open Course Window %&l", priority = 1)]
        public static void Open()
        {
            CourseWindow wnd = GetWindow<CourseWindow>(false, WindowTitle, true);
            wnd.minSize = new Vector2(640, 420);
            wnd.Focus();
            // Фокус для горячих клавиш — только при явном открытии окна (не после каждой перекомпиляции).
            EditorApplication.delayCall += () =>
            {
                if (wnd != null)
                {
                    wnd.TryFocusWindowForHotkeys();
                }
            };
        }

        // Отдельный [DidReloadScripts]-обработчик не нужен: после перезагрузки домена Unity заново вызывает
        // CreateGUI, а он сам восстанавливает сессию. Второй вызов мог прийти до построения GUI
        // и добавить ещё один ScrollView содержимого.
    }
}
