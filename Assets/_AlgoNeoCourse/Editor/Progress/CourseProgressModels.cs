using System;
using System.Collections.Generic;
using NeoCource.Editor.Quizzes;

namespace NeoCource.Editor.Progress
{
    [Serializable]
    public class CourseProgressData
    {
        public string lastLessonPath;
        public int lastSlideIndex;

        // Дальняя точка прогресса (монотонно, только вперёд): для кнопки «Продолжить».
        // lastSession едет за каждым ShowSlide (в т.ч. назад), farthest — только вперёд
        // по порядку уроков, поэтому «Продолжить» возвращает именно к максимуму.
        public string farthestLessonPath = string.Empty;
        public int farthestSlideIndex;

        public Dictionary<string, LessonQuizState> lessonStates = new();

        // Опциональные поля под будущий бэкенд (userId/deviceId/метки).
        // Newtonsoft толерантен к отсутствующим полям, старые сейвы читаются как раньше.
        // Поурочные поля (lessonId, lastSlideIndex, maxSlideReached, slidesTotal,
        // lastActivityAtUnixMs) — в LessonQuizState (QuizModels.cs).
        public int schemaVersion = 1;
        public string userId = "";
        public string deviceId = "";
        public long createdAtUnixMs = 0;
        public long updatedAtUnixMs = 0;
    }
}