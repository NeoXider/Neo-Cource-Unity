using NUnit.Framework;
using NeoCource.Editor.Quizzes;
using System.Collections.Generic;

namespace NeoCource.Editor.Tests
{
    public class QuizEngineTests
    {
        [Test]
        public void Single_Attempts_Respects_Max()
        {
            var q = new QuizQuestion
            {
                id = "q1",
                kind = QuizKind.Single,
                text = "Q",
                answers = new List<QuizAnswer>
                {
                    new QuizAnswer{ id="a1", text="A", isCorrect=false},
                    new QuizAnswer{ id="a2", text="B", isCorrect=true}
                }
            };
            var s = new QuizQuestionState{ questionId = q.id };
            QuizEngine.ApplySingleOrTrueFalseAttempt(q, s, q.answers[0], 2);
            Assert.IsFalse(s.isCompleted);
            QuizEngine.ApplySingleOrTrueFalseAttempt(q, s, q.answers[0], 2);
            Assert.IsTrue(s.isCompleted);
            Assert.IsFalse(s.isCorrect);
        }

        [Test]
        public void Multiple_Correctness_Matches_Selected()
        {
            var q = new QuizQuestion
            {
                id = "q2",
                kind = QuizKind.Multiple,
                text = "Q",
                answers = new List<QuizAnswer>
                {
                    new QuizAnswer{ id="a1", text="A", isCorrect=true},
                    new QuizAnswer{ id="a2", text="B", isCorrect=false},
                    new QuizAnswer{ id="a3", text="C", isCorrect=true}
                }
            };
            var s = new QuizQuestionState{ questionId = q.id };
            s.selectedAnswerIds.Add("a1");
            s.selectedAnswerIds.Add("a3");
            Assert.IsTrue(QuizEngine.IsMultipleSelectionCorrect(q, s));
            s.selectedAnswerIds.Clear();
            s.selectedAnswerIds.Add("a1");
            Assert.IsFalse(QuizEngine.IsMultipleSelectionCorrect(q, s));
        }

        [Test]
        public void HasUnfinishedQuestions_Works()
        {
            var q1 = new QuizQuestion { id = "q1", kind = QuizKind.Single };
            var q2 = new QuizQuestion { id = "q2", kind = QuizKind.TrueFalse };
            var state = new LessonQuizState { lessonPath = "lesson.md" };
            state.questionIdToState["q1"] = new QuizQuestionState { questionId = "q1", isCompleted = true };
            // q2 отсутствует в состоянии => считается незавершённым
            Assert.IsTrue(QuizEngine.HasUnfinishedQuestions(new[] { q1, q2 }, state));
            state.questionIdToState["q2"] = new QuizQuestionState { questionId = "q2", isCompleted = true };
            Assert.IsFalse(QuizEngine.HasUnfinishedQuestions(new[] { q1, q2 }, state));
        }
    }
}
