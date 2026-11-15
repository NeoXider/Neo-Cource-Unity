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
            SaveCheckResult(lessonPath, checkId, passed, false);
        }

        // remarks — засчитано, но дополнительные пункты не выполнены.
        public static void SaveCheckResult(string lessonPath, string checkId, bool passed, bool remarks)
        {
            SaveCheckResult(lessonPath, checkId, passed, remarks, null, preserveMatchingRules: true);
        }

        public static void SaveCheckResult(string lessonPath, string checkId, bool passed, bool remarks,
            List<CheckRuleSnapshot> rules)
        {
            SaveCheckResult(lessonPath, checkId, passed, remarks, rules, preserveMatchingRules: false);
        }

        private static void SaveCheckResult(string lessonPath, string checkId, bool passed, bool remarks,
            List<CheckRuleSnapshot> rules, bool preserveMatchingRules)
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
            state.checkIdToRemarks ??= new Dictionary<string, bool>();
            // Legacy callers only update the verdict. Keep rows only while that verdict still matches.
            bool sameVerdict = state.checkIdToPassed.TryGetValue(checkId, out bool previousPassed) &&
                previousPassed == passed && (!passed ||
                    (state.checkIdToRemarks.TryGetValue(checkId, out bool previousRemarks) && previousRemarks) == remarks);
            state.checkIdToRuleResults ??= new Dictionary<string, List<CheckRuleSnapshot>>();
            if (rules != null)
            {
                state.checkIdToRuleResults[checkId] = rules;
            }
            else if (!preserveMatchingRules || !sameVerdict)
            {
                state.checkIdToRuleResults.Remove(checkId);
            }

            state.checkIdToPassed[checkId] = passed;
            if (passed && remarks)
            {
                state.checkIdToRemarks[checkId] = true;
            }
            else
            {
                state.checkIdToRemarks.Remove(checkId);
            }

            CourseProgressStore.SaveToDisk();
        }

        // Детерминированный id проверки по содержимому unity://check-ссылки.
        public static string CheckIdForLink(string unityLink)
        {
            return "check:" + (unityLink ?? string.Empty);
        }
    }
}