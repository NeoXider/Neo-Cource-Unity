using System.Collections.Generic;
using System.Text.RegularExpressions;
using NeoCource.Editor.Quizzes;
using NUnit.Framework;
using MarkerKind = NeoCource.Editor.CourseWindow.ProgressMarkerKind;
using MarkerState = NeoCource.Editor.CourseWindow.ProgressMarkerState;

namespace NeoCource.Editor.Tests
{
    public class CourseProgressMarkersTests
    {
        private static string Quiz(string id)
        {
            return "```quiz\nid: " + id + "\nkind: single\ntext: Вопрос?\nanswers:\n  - text: Да\n    correct: true\n```";
        }

        private static string Check(string objectName)
        {
            return "```check\nrules:\n  - object_exists: \"" + objectName + "\"\n```";
        }

        private static string CheckId(string slide)
        {
            string normalized = slide.Replace("\r\n", "\n").Replace('\r', '\n');
            Match match = CourseWindow.CheckBlockRegex.Match(normalized);
            Assert.IsTrue(match.Success);
            return QuizStateStore.CheckIdForLink(CourseWindow.BuildCheckLink(match.Groups[1].Value));
        }

        [Test]
        public void EmptyOrPlainSlides_HaveNoCheckpoints()
        {
            Assert.IsEmpty(CourseWindow.BuildProgressMarkerDescriptors(null, null));
            Assert.IsEmpty(CourseWindow.BuildProgressMarkerDescriptors(new List<string>(), null));
            Assert.IsEmpty(CourseWindow.BuildProgressMarkerDescriptors(new List<string> { "# Теория", null }, null));
        }

        [Test]
        public void Checkpoints_KeepZeroBasedSlideIndexes_AndRussianOneBasedTooltips()
        {
            List<string> slides = new();
            for (int i = 0; i < 14; i++)
            {
                slides.Add("Теория");
            }

            slides[8] = Quiz("q9");
            slides[13] = Check("Player");
            var markers = CourseWindow.BuildProgressMarkerDescriptors(slides, null);
            Assert.AreEqual(2, markers.Count);
            Assert.AreEqual(8, markers[0].SlideIndex);
            Assert.AreEqual(MarkerKind.Quiz, markers[0].Kind);
            Assert.AreEqual(MarkerState.NotDone, markers[0].State);
            Assert.AreEqual("Квиз · слайд 9 · не пройден", markers[0].Tooltip);
            Assert.AreEqual(13, markers[1].SlideIndex);
            Assert.AreEqual(MarkerKind.Check, markers[1].Kind);
            Assert.AreEqual("Практика · слайд 14 · не выполнено", markers[1].Tooltip);
        }

        [Test]
        public void MixedSlide_HasSeparateQuizAndPracticeMarkers()
        {
            var markers = CourseWindow.BuildProgressMarkerDescriptors(
                new List<string> { Quiz("q") + "\n" + Check("Player") }, null);
            Assert.AreEqual(2, markers.Count);
            Assert.AreEqual(0, markers[0].SlideIndex);
            Assert.AreEqual(0, markers[1].SlideIndex);
            Assert.AreEqual(MarkerKind.Quiz, markers[0].Kind);
            Assert.AreEqual(MarkerKind.Check, markers[1].Kind);
        }

        [TestCase(false, false, "NotDone")]
        [TestCase(false, true, "NotDone")]
        [TestCase(true, false, "Done")]
        [TestCase(true, true, "Done")]
        public void QuizState_UsesParserIds_AndCompletionRatherThanCorrectness(
            bool completed, bool correct, string expectedState)
        {
            string slide = Quiz("question-with-id");
            string id = QuizParser.ParseQuestions(slide)[0].id;
            LessonQuizState state = new();
            state.questionIdToState[id] = new QuizQuestionState { isCompleted = completed, isCorrect = correct };
            var marker = CourseWindow.BuildProgressMarkerDescriptors(new List<string> { slide }, state)[0];
            CollectionAssert.AreEqual(new[] { id }, marker.Ids);
            Assert.AreEqual(expectedState, marker.State.ToString());
            Assert.AreEqual(completed ? "Квиз · слайд 1 · пройден" : "Квиз · слайд 1 · не пройден", marker.Tooltip);
        }

        [Test]
        public void MultipleQuizzes_OnOneSlide_RequireEveryQuestionToBeCompleted()
        {
            string slide = Quiz("first") + "\n" + Quiz("second");
            LessonQuizState state = new();
            state.questionIdToState["first"] = new QuizQuestionState { isCompleted = true };
            var markers = CourseWindow.BuildProgressMarkerDescriptors(new List<string> { slide }, state);
            Assert.AreEqual(1, markers.Count);
            Assert.AreEqual(2, markers[0].Ids.Length);
            Assert.AreEqual(MarkerState.NotDone, markers[0].State);

            state.questionIdToState["second"] = new QuizQuestionState { isCompleted = true };
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.Done, markers[0].State);
        }

        [TestCase(null, null, "NotDone", "не выполнено")]
        [TestCase(null, true, "NotDone", "не выполнено")]
        [TestCase(false, null, "Failed", "не пройдена")]
        [TestCase(false, true, "Failed", "не пройдена")]
        [TestCase(true, null, "Done", "выполнено")]
        [TestCase(true, false, "Done", "выполнено")]
        [TestCase(true, true, "Remarks", "засчитано с замечаниями")]
        public void PracticeState_DistinguishesMissingFailedPerfectAndRemarks(
            bool? passed, bool? remarks, string expectedState, string expectedText)
        {
            string slide = Check("Player");
            string id = CheckId(slide);
            LessonQuizState state = new();
            if (passed.HasValue)
            {
                state.checkIdToPassed[id] = passed.Value;
            }

            if (remarks.HasValue)
            {
                state.checkIdToRemarks[id] = remarks.Value;
            }

            var marker = CourseWindow.BuildProgressMarkerDescriptors(new List<string> { slide }, state)[0];
            Assert.AreEqual(expectedState, marker.State.ToString());
            Assert.AreEqual("Практика · слайд 1 · " + expectedText, marker.Tooltip);
        }

        [Test]
        public void CrlfSlides_UseTheSameIdsAsLfQuizParserAndCheckButtons()
        {
            string lf = Quiz("q-lf") + "\n" + Check("Игрок");
            string crlf = lf.Replace("\n", "\r\n");
            string quizId = QuizParser.ParseQuestions(lf)[0].id;
            string checkId = CheckId(lf);
            LessonQuizState state = new();
            state.questionIdToState[quizId] = new QuizQuestionState { isCompleted = true };
            state.checkIdToPassed[checkId] = true;
            var markers = CourseWindow.BuildProgressMarkerDescriptors(new List<string> { crlf }, state);
            Assert.AreEqual(2, markers.Count);
            CollectionAssert.AreEqual(new[] { quizId }, markers[0].Ids);
            CollectionAssert.AreEqual(new[] { checkId }, markers[1].Ids);
            Assert.AreEqual(MarkerState.Done, markers[0].State);
            Assert.AreEqual(MarkerState.Done, markers[1].State);
        }

        [Test]
        public void RepeatedCheckBlocks_ShareState_AndStillMarkEverySlide()
        {
            string check = Check("Player");
            LessonQuizState state = new();
            state.checkIdToPassed[CheckId(check)] = true;
            var markers = CourseWindow.BuildProgressMarkerDescriptors(
                new List<string> { check + "\n" + check, check }, state);
            Assert.AreEqual(2, markers.Count);
            Assert.AreEqual(1, markers[0].Ids.Length);
            Assert.AreEqual(0, markers[0].SlideIndex);
            Assert.AreEqual(1, markers[1].SlideIndex);
            Assert.AreEqual(MarkerState.Done, markers[0].State);
            Assert.AreEqual(MarkerState.Done, markers[1].State);
        }

        [Test]
        public void MultipleChecks_FailureWins_ThenUnfinished_ThenRemarks_ThenPerfect()
        {
            string first = Check("Player");
            string second = Check("Enemy");
            string firstId = CheckId(first);
            string secondId = CheckId(second);
            LessonQuizState state = new();
            state.checkIdToPassed[firstId] = false;
            var markers = CourseWindow.BuildProgressMarkerDescriptors(new List<string> { first + "\n" + second }, state);
            Assert.AreEqual(MarkerState.Failed, markers[0].State);

            state.checkIdToPassed[firstId] = true;
            state.checkIdToRemarks[firstId] = true;
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.NotDone, markers[0].State);

            state.checkIdToPassed[secondId] = true;
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.Remarks, markers[0].State);

            state.checkIdToRemarks.Clear();
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.Done, markers[0].State);
            Assert.AreEqual("Практика · слайд 1 · выполнено", markers[0].Tooltip);

            state.checkIdToPassed[secondId] = false;
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.Failed, markers[0].State);
        }

        [Test]
        public void MissingLegacyDictionariesAndNullQuestionState_AreUnfinished()
        {
            LessonQuizState state = new() { questionIdToState = null, checkIdToPassed = null, checkIdToRemarks = null };
            var markers = CourseWindow.BuildProgressMarkerDescriptors(
                new List<string> { Quiz("q"), Check("Player") }, state);
            Assert.AreEqual(MarkerState.NotDone, markers[0].State);
            Assert.AreEqual(MarkerState.NotDone, markers[1].State);

            state.questionIdToState = new Dictionary<string, QuizQuestionState> { ["q"] = null };
            CourseWindow.UpdateProgressMarkerDescriptors(markers, state);
            Assert.AreEqual(MarkerState.NotDone, markers[0].State);
        }

        [TestCase(43, 0, 0, 0)]
        [TestCase(43, 13, 13, 13)]
        [TestCase(43, 8, 19, 19)]
        [TestCase(43, 42, 42, 42)]
        [TestCase(43, 8, 999, 42)]
        [TestCase(43, -1, -1, 0)]
        [TestCase(43, 999, 0, 42)]
        [TestCase(1, 0, 0, 0)]
        public void SlideScale_FillEndsAfterFarthestSegment_CurrentMarkerIsAtSegmentCentre(
            int count, int current, int reached, int expectedFarthest)
        {
            CourseWindow.GetSlideProgress(count, current, reached, out int farthest, out float fill, out float marker);
            int clampedCurrent = System.Math.Max(0, System.Math.Min(current, count - 1));
            Assert.AreEqual(expectedFarthest, farthest);
            Assert.That(fill, Is.EqualTo(100f * (expectedFarthest + 1) / count).Within(0.0001f));
            Assert.That(marker, Is.EqualTo(100f * (clampedCurrent + 0.5f) / count).Within(0.0001f));
            Assert.That(marker, Is.GreaterThan(0).And.LessThan(fill));
            Assert.That(fill, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void EmptySlideScale_HasNoFillOrCurrentMarker()
        {
            CourseWindow.GetSlideProgress(0, 0, 0, out int farthest, out float fill, out float marker);
            Assert.AreEqual(-1, farthest);
            Assert.AreEqual(0, fill);
            Assert.AreEqual(0, marker);
        }

        [TestCase(0, 8, -1, true)]
        [TestCase(8, 8, -1, true)]
        [TestCase(9, 8, -1, false)]
        [TestCase(12, 8, 12, true)]
        [TestCase(13, 8, 12, false)]
        [TestCase(0, 0, 0, true)]
        [TestCase(1, 0, 0, false)]
        public void Checkpoint_Jump_Is_Limited_To_Farthest_Reached_Slide(
            int target, int current, int reached, bool allowed)
        {
            Assert.AreEqual(allowed, CourseWindow.CanJumpToCheckpoint(target, current, reached));
        }
    }
}
