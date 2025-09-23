using System;
using System.Linq;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizEngine
    {
        public static void ApplySingleOrTrueFalseAttempt(QuizQuestion question, QuizQuestionState state, QuizAnswer chosen, int maxAttempts)
        {
            if (state.isCompleted) return;
            state.attemptsUsed = Math.Max(0, state.attemptsUsed) + 1;
            if (chosen.isCorrect)
            {
                state.isCompleted = true;
                state.isCorrect = true;
            }
            else if (state.attemptsUsed >= Math.Max(1, maxAttempts))
            {
                state.isCompleted = true;
                state.isCorrect = false;
            }
        }

        public static bool IsMultipleSelectionCorrect(QuizQuestion question, QuizQuestionState state)
        {
            var correctIds = question.answers.Where(a => a.isCorrect).Select(a => a.id).OrderBy(x => x).ToArray();
            var chosen = state.selectedAnswerIds.OrderBy(x => x).ToArray();
            if (correctIds.Length == 0) return false;
            if (chosen.Length != correctIds.Length) return false;
            for (int i = 0; i < correctIds.Length; i++)
                if (!string.Equals(correctIds[i], chosen[i], StringComparison.Ordinal)) return false;
            return true;
        }

        public static bool HasUnfinishedQuestions(System.Collections.Generic.IEnumerable<QuizQuestion> questions, LessonQuizState lessonState)
        {
            if (questions == null || lessonState == null) return false;
            foreach (var q in questions)
            {
                if (!lessonState.questionIdToState.TryGetValue(q.id, out var s) || !s.isCompleted)
                    return true;
            }
            return false;
        }
    }
}


