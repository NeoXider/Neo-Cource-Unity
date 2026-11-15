using NUnit.Framework;

namespace NeoCource.Editor.Tests
{
    // Дальняя точка для кнопки «▶ Продолжить»: движется только вперёд, а устаревшая (урок удалён или
    // пропал из списка) заменяется текущим уроком — иначе кнопка навсегда остаётся выключенной.
    public class FarthestSessionTests
    {
        [Test]
        public void Same_Lesson_Advances_Only_Forward()
        {
            Assert.IsTrue(CourseWindow.ShouldAdvanceFarthest(3, 3, false, true, 20, 13));
            Assert.IsFalse(CourseWindow.ShouldAdvanceFarthest(3, 3, false, true, 10, 13), "откат назад точку не сдвигает");
            Assert.IsFalse(CourseWindow.ShouldAdvanceFarthest(3, 3, false, true, 13, 13));
        }

        [Test]
        public void Later_Lesson_Advances_Earlier_Lesson_Does_Not()
        {
            Assert.IsTrue(CourseWindow.ShouldAdvanceFarthest(5, 2, false, false, 0, 30));
            Assert.IsFalse(CourseWindow.ShouldAdvanceFarthest(1, 2, false, false, 40, 3), "обзор прошлого урока точку не сдвигает");
        }

        [Test]
        public void Stale_Farthest_Is_Replaced()
        {
            // Файл дальнего урока удалён (например, скачаны только модули 5–6).
            Assert.IsTrue(CourseWindow.ShouldAdvanceFarthest(0, -1, true, false, 18, 10));
            // Файл есть, но урока нет в текущем списке курса, а текущий урок — в списке.
            Assert.IsTrue(CourseWindow.ShouldAdvanceFarthest(4, -1, false, false, 2, 10));
            // Текущий урок сам вне списка (пример из Docs) — существующую точку не трогаем.
            Assert.IsFalse(CourseWindow.ShouldAdvanceFarthest(-1, 2, false, false, 5, 1));
        }
    }
}
