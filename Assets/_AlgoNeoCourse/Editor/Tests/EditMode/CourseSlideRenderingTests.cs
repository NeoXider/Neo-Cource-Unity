using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NeoCource.Editor.Infrastructure;
using NeoCource.Editor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor.Tests
{
    // Слайд проходит тот же путь, что в окне курса: BuildContent (рендерер и его стили) и
    // RenderSlideContent (кнопки проверок, квизы, цвета текста, карточки заданий).
    // Окно на экран не выводится, прогресс на диск не пишется (путь урока пуст).
    public class CourseSlideRenderingTests
    {
        private const string Bold = "<color=" + CourseWindow.BoldColorHex + "><b>";
        private const string BoldEnd = "</b></color>";
        private const string Code = "<color=" + CourseWindow.InlineCodeColorHex + ">";

        private CourseWindow window;

        [SetUp]
        public void SetUp()
        {
            window = ScriptableObject.CreateInstance<CourseWindow>();
            window.BuildContent();
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null)
            {
                Object.DestroyImmediate(window);
            }
        }

        private ScrollView ContentRoot => window.rootVisualElement.Q<ScrollView>(className: "algo-course-content");

        private VisualElement RendererRoot => ContentRoot.Q(className: "mainBody");

        private List<VisualElement> WithClass(string className)
        {
            return window.rootVisualElement.Query<VisualElement>(className: className).ToList();
        }

        private List<string> LabelTexts()
        {
            return RendererRoot.Query<Label>().ToList().Select(label => label.text ?? string.Empty).ToList();
        }

        // Текст без тегов rich text и без нулевых пробелов экранирования — то, что видит студент.
        private static string Plain(VisualElement element)
        {
            List<Label> labels = element.Query<Label>().ToList();
            if (element is Label self && !labels.Contains(self))
            {
                labels.Insert(0, self);
            }

            string joined = string.Join(" ", labels.Select(label => label.text));
            return Regex.Replace(joined, "</?(?:b|i|u|color|link)(?:=[^>]*)?>", string.Empty)
                .Replace("​", string.Empty).Trim();
        }

        [Test]
        public void Table_Slide_Renders_Grid_With_Rows_Cells_And_Alignment()
        {
            window.RenderSlideContent(
                "## Типы\n\n" +
                "| Тип | Пример | Байт |\n" +
                "| :--- | :---: | ---: |\n" +
                "| int | `42` | 4 |\n" +
                "| float | **3.5f** | 4 |\n" +
                "| bool | true | 1 |\n");

            List<VisualElement> tables = WithClass("md-table");
            Assert.AreEqual(1, tables.Count, "таблица должна стать одним .md-table");
            VisualElement table = tables[0];
            Assert.IsTrue(ContentRoot.Contains(table), "таблица лежит внутри карточки слайда");

            List<VisualElement> rows = table.Query<VisualElement>(className: "md-table-row").ToList();
            Assert.AreEqual(4, rows.Count, "шапка и три строки тела");
            Assert.IsTrue(rows[0].ClassListContains("md-table-header"));
            Assert.IsFalse(rows[1].ClassListContains("md-table-header"));
            Assert.IsFalse(rows[1].ClassListContains("md-table-row--alt"));
            Assert.IsTrue(rows[2].ClassListContains("md-table-row--alt"), "каждая вторая строка тела — полосатая");
            Assert.IsFalse(rows[3].ClassListContains("md-table-row--alt"));

            foreach (VisualElement row in rows)
            {
                List<VisualElement> cells = row.Query<VisualElement>(className: "md-table-cell").ToList();
                Assert.AreEqual(3, cells.Count);
                Assert.IsTrue(cells[0].ClassListContains("md-table-cell--left"));
                Assert.IsTrue(cells[1].ClassListContains("md-table-cell--center"));
                Assert.IsTrue(cells[2].ClassListContains("md-table-cell--right"));
            }

            List<VisualElement> header = rows[0].Query<VisualElement>(className: "md-table-cell").ToList();
            CollectionAssert.AreEqual(new[] { "Тип", "Пример", "Байт" }, header.Select(Plain).ToList());

            List<VisualElement> last = rows[3].Query<VisualElement>(className: "md-table-cell").ToList();
            CollectionAssert.AreEqual(new[] { "bool", "true", "1" }, last.Select(Plain).ToList());

            // Содержимое ячеек идёт через тот же конвейер: цвета плагина действуют и в таблице.
            string codeCell = rows[1].Query<VisualElement>(className: "md-table-cell").ToList()[1].Q<Label>().text;
            StringAssert.Contains(Code + "42</color>", codeCell);
            string boldCell = rows[2].Query<VisualElement>(className: "md-table-cell").ToList()[1].Q<Label>().text;
            StringAssert.Contains(Bold + "3.5f" + BoldEnd, boldCell);
        }

        [Test]
        public void Text_With_Pipes_But_No_Separator_Row_Stays_A_Paragraph()
        {
            window.RenderSlideContent("Запись a | b | c — это не таблица.");

            Assert.AreEqual(0, WithClass("md-table").Count);
            StringAssert.Contains("a | b | c", string.Join("\n", LabelTexts()));
        }

        [Test]
        public void Bold_Text_Is_Orange_In_Paragraph_List_And_Heading()
        {
            window.RenderSlideContent(
                "## Заголовок с **акцентом**\n\n" +
                "Это **важно**, а это *курсив*.\n\n" +
                "- пункт **раз**\n" +
                "- пункт два\n");

            string heading = RendererRoot.Q<Label>(className: "h2").text;
            StringAssert.Contains(Bold + "акцентом" + BoldEnd, heading);

            string paragraph = RendererRoot.Q<Label>(className: "paragraph").text;
            StringAssert.Contains(Bold + "важно" + BoldEnd, paragraph);
            StringAssert.Contains("<i>курсив</i>", paragraph);

            string list = RendererRoot.Q<Label>(className: "list").text;
            StringAssert.Contains(Bold + "раз" + BoldEnd, list);
        }

        [Test]
        public void Inline_Code_Keeps_Plugin_Color_And_Is_Not_Bold()
        {
            window.RenderSlideContent("Вызов `Debug.Log` и **жирный `код` текст**.");

            string paragraph = RendererRoot.Q<Label>(className: "paragraph").text;
            StringAssert.Contains(Code + "Debug.Log</color>", paragraph);
            StringAssert.Contains(Bold + "жирный " + Code + "код</color> текст" + BoldEnd, paragraph);
            StringAssert.DoesNotContain("48EEFF", paragraph, "цвет встроенного кода апстрима не должен протечь");
        }

        [Test]
        public void RestyleBoldTags_Is_Idempotent_And_Tolerates_Unbalanced_Tags()
        {
            const string code = "<color=#B388FF>";
            string once = CourseWindow.RestyleBoldTags("a <b>b</b> " + code + "<b>c</b></color>", code);
            Assert.AreEqual("a " + Bold + "b" + BoldEnd + " " + code + "c</color>", once);
            Assert.AreEqual(once, CourseWindow.RestyleBoldTags(once, code));

            Assert.AreEqual("x</b> y", CourseWindow.RestyleBoldTags("x</b> y", code));
            Assert.AreEqual("нет тегов", CourseWindow.RestyleBoldTags("нет тегов", code));
            Assert.IsNull(CourseWindow.RestyleBoldTags(null, code));
        }

        [Test]
        public void CSharp_Code_Block_Keeps_Lines_And_Gets_Highlighting()
        {
            window.RenderSlideContent("```csharp\npublic class Player\n{\n    int hp = 10; // здоровье\n}\n```\n");

            List<VisualElement> blocks = WithClass("codeblock");
            Assert.AreEqual(1, blocks.Count);
            Assert.IsTrue(blocks[0].ClassListContains("language-csharp"));

            List<Label> lines = blocks[0].Query<Label>().ToList();
            Assert.AreEqual(4, lines.Count);
            Assert.AreEqual("public class Player", Plain(lines[0]));
            Assert.AreEqual("int hp = 10; // здоровье", Plain(lines[2]));
            StringAssert.Contains("<color=", lines[0].text, "ключевые слова C# подсвечены");
            StringAssert.DoesNotContain("<b>", lines[0].text);
        }

        [Test]
        public void Check_Block_Becomes_Card_With_Check_Button()
        {
            const string raw = "rules:\n  - object_exists: \"Player\"\n  - filename: \"Player.cs\"";
            window.RenderSlideContent("## Практика\n\nСоздайте игрока.\n\n```check\n" + raw + "\n```\n");

            List<CheckCard> cards = window.rootVisualElement.Query<CheckCard>().ToList();
            Assert.AreEqual(1, cards.Count, "блок check должен стать карточкой задания");
            CheckCard card = cards[0];
            Assert.IsTrue(card.ClassListContains("check-card"));
            Assert.AreEqual(raw, card.Raw);
            Assert.AreEqual(0, WithClass("language-check").Count, "сырой YAML на слайде не остаётся");
            Assert.AreEqual(2, card.Query<VisualElement>(className: "check-rule").ToList().Count);

            List<Button> buttons = window.rootVisualElement.Query<Button>(className: "check-button").ToList();
            Assert.AreEqual(1, buttons.Count);
            Assert.IsTrue(card.Contains(buttons[0]), "кнопка «Проверить» переезжает в карточку");
            Assert.AreEqual("Проверить", buttons[0].text);
            Assert.AreEqual(CourseWindow.BuildCheckLink(raw), buttons[0].userData as string);
            Assert.AreEqual(CourseWindow.BuildCheckLink(raw), card.Link);

            Assert.IsFalse(LabelTexts().Any(text => text.Contains("unity://check")),
                "ссылка-транспорт заменена кнопкой");
        }

        [Test]
        public void Quiz_Marker_Is_Replaced_By_Quiz_Ui()
        {
            window.RenderSlideContent(
                "## Вопрос\n\nТекст до квиза.\n\n" +
                "```quiz\nid: render-test-q1\nkind: single\ntext: Сколько будет 2 + 2?\n" +
                "answers:\n  - text: Четыре\n    correct: true\n  - text: Пять\n```\n\n" +
                "Текст после квиза.\n");

            Assert.AreEqual(1, WithClass("quiz-block").Count);
            Assert.AreEqual("Сколько будет 2 + 2?", RendererRoot.Q<Label>(className: "quiz-title").text);
            List<Button> answers = RendererRoot.Query<Button>(className: "quiz-answer").ToList();
            CollectionAssert.AreEquivalent(new[] { "Четыре", "Пять" }, answers.Select(button => button.text).ToList());

            List<string> texts = LabelTexts();
            Assert.IsFalse(texts.Any(text => text.Contains("[[QUIZ:")), "метка квиза не должна остаться текстом");
            Assert.AreEqual(0, WithClass("language-quiz").Count, "блок quiz не показывается кодом");
            Assert.IsTrue(texts.Contains("Текст до квиза."));
            Assert.IsTrue(texts.Contains("Текст после квиза."));
        }

        [Test]
        public void Slide_Never_Scrolls_Horizontally_And_Only_The_Card_Scrolls()
        {
            ScrollView content = ContentRoot;
            Assert.IsNotNull(content);
            Assert.AreEqual(ScrollerVisibility.Hidden, content.horizontalScrollerVisibility);

            // Любой ScrollView в окне (включая корень рендерера, если он им станет) — без горизонтальной полосы.
            List<ScrollView> scrollViews = window.rootVisualElement.Query<ScrollView>().ToList();
            foreach (ScrollView scrollView in scrollViews)
            {
                Assert.AreEqual(ScrollerVisibility.Hidden, scrollView.horizontalScrollerVisibility);
            }

            Assert.AreEqual(1, scrollViews.Count, "прокручивается только карточка слайда");
            Assert.IsNotNull(RendererRoot);
            Assert.IsFalse(RendererRoot is ScrollView);
        }

        [Test]
        public void Slide_Enter_Animation_Lives_On_The_Card_Not_On_The_Renderer_Root()
        {
            Assert.IsTrue(ContentRoot.ClassListContains("algo-slide"));
            Assert.IsFalse(RendererRoot.ClassListContains("algo-slide"));

            window.RenderSlideContent("# Слайд");
            MethodInfo play = typeof(CourseWindow).GetMethod("PlaySlideEnter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(play, "PlaySlideEnter method not found");
            play.Invoke(window, null);

            Assert.IsTrue(ContentRoot.ClassListContains("algo-slide--from-right"), "стартовое состояние въезда");
            Assert.IsFalse(RendererRoot.ClassListContains("algo-slide--from-right"));
        }

        [Test]
        public void Embedded_Renderer_Finds_Its_Own_Default_Style_Without_Explicit_Assets()
        {
            // Так рендерер создают инструменты самой встроенной копии (инспектор .md, Markdown Doc Viewer):
            // без аргументов он ищет стиль от корня копии. Не нашёл бы — написал ошибку, и тест бы упал.
            UIMarkdownRenderer.UIMarkdownRenderer renderer = new((_, _) => { }, false);

            Assert.AreEqual(1, renderer.RootElement.styleSheets.count);
            Assert.AreEqual(AlgoNeoPackageAssetLocator.MarkdownStylesheetAssetPath,
                AssetDatabase.GetAssetPath(renderer.RootElement.styleSheets[0]));
        }

        [Test]
        public void Renderer_Uses_Embedded_Default_Style_Then_Plugin_Quiz_Style()
        {
            CollectionAssert.IsEmpty(AlgoNeoCourseDependencyInstaller.FindMissingRendererAssets());

            VisualElementStyleSheetSet sheets = RendererRoot.styleSheets;
            List<string> paths = new();
            for (int i = 0; i < sheets.count; i++)
            {
                paths.Add(AssetDatabase.GetAssetPath(sheets[i]));
            }

            // Порядок важен: при равной специфичности побеждает таблица, добавленная позже.
            CollectionAssert.AreEqual(new[]
            {
                AlgoNeoPackageAssetLocator.MarkdownStylesheetAssetPath,
                AlgoNeoPackageAssetLocator.QuizStylesheetAssetPath
            }, paths);
            StringAssert.EndsWith("Editor/Styles/AlgoNeoQuiz.uss", paths[1]);
            StringAssert.DoesNotContain("Plugins/markdownrenderer", paths[1],
                "стили плагина не лежат в папке встроенного рендерера");
        }
    }
}
