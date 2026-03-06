using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoCource.Editor.Quizzes
{
    public static class QuizEngine
    {
        public static void ApplySingleOrTrueFalseAttempt(QuizQuestion question, QuizQuestionState state,
            QuizAnswer chosen, int maxAttempts)
        {
            if (state.isCompleted)
            {
                return;
            }

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
            string[] correctIds = question.answers.Where(a => a.isCorrect).Select(a => a.id).OrderBy(x => x).ToArray();
            string[] chosen = state.selectedAnswerIds.OrderBy(x => x).ToArray();
            if (correctIds.Length == 0)
            {
                return false;
            }

            if (chosen.Length != correctIds.Length)
            {
                return false;
            }

            for (int i = 0; i < correctIds.Length; i++)
            {
                if (!string.Equals(correctIds[i], chosen[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasUnfinishedQuestions(IEnumerable<QuizQuestion> questions, LessonQuizState lessonState)
        {
            if (questions == null || lessonState == null)
            {
                return false;
            }

            foreach (QuizQuestion q in questions)
            {
                if (!lessonState.questionIdToState.TryGetValue(q.id, out QuizQuestionState s) || !s.isCompleted)
                {
                    return true;
                }
            }

            return false;
        }
    }
}