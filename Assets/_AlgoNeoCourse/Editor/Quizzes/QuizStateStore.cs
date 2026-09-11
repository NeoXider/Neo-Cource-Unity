using System.Collections.Generic;
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
            LessonQuizState state = GetLessonState(lessonPath, false);
            if (state == null)
            {
                return;
            }

            CourseProgressStore.SaveToDisk();
        }

        // Результат практической проверки: last-outcome семантика (как попытки квизов).
        public static void SaveCheckResult(string lessonPath, string checkId, bool passed)
        {
            if (string.IsNullOrEmpty(checkId))
            {
                return;
            }

            LessonQuizState state = GetLessonState(lessonPath);
            if (state == null)
            {
                return;
            }

            state.checkIdToPassed ??= new Dictionary<string, bool>();
            state.checkIdToPassed[checkId] = passed;
            CourseProgressStore.SaveToDisk();
        }

        // Детерминированный id проверки по содержимому unity://check-ссылки.
        public static string CheckIdForLink(string unityLink)
        {
            return "check:" + (unityLink ?? string.Empty);
        }
    }
}