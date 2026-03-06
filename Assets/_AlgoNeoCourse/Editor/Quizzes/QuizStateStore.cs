using System;
using NeoCource.Editor.Progress;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizStateStore
    {
        public static LessonQuizState GetLessonState(string lessonPath, bool createIfMissing = true)
        {
            return CourseProgressStore.GetLessonState(lessonPath, createIfMissing);
        }

        public static void ResetInMemory()
        {
            CourseProgressStore.ResetInMemory();
        }

        public static void SaveLessonState(string lessonPath)
        {
            var state = GetLessonState(lessonPath, false);
            if (state == null) return;
            CourseProgressStore.SaveToDisk();
        }
    }
}


