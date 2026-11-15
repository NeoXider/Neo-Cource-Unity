using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NeoCource.Editor.Quizzes;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeoCource.Editor
{
    public partial class CourseWindow
    {
        internal enum ProgressMarkerKind { Quiz, Check }
        internal enum ProgressMarkerState { NotDone, Done, Remarks, Failed }

        internal sealed class ProgressMarkerDescriptor
        {
            internal int SlideIndex { get; }
            internal ProgressMarkerKind Kind { get; }
            internal string[] Ids { get; }
            internal ProgressMarkerState State { get; set; }
            internal string Tooltip { get; set; }

            internal ProgressMarkerDescriptor(int slideIndex, ProgressMarkerKind kind, string[] ids)
            {
                SlideIndex = slideIndex;
                Kind = kind;
                Ids = ids;
            }
        }

        // Pure mapping: no file reads, settings, EditorWindow or progress-store access.
        // One descriptor per slide/kind; every question/check on that slide contributes to its state.
        internal static List<ProgressMarkerDescriptor> BuildProgressMarkerDescriptors(
            List<string> lessonSlides, LessonQuizState state)
        {
            List<ProgressMarkerDescriptor> markers = new();
            if (lessonSlides == null)
            {
                return markers;
            }

            for (int i = 0; i < lessonSlides.Count; i++)
            {
                string text = (lessonSlides[i] ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
                List<string> quizIds = new();
                foreach (QuizQuestion question in QuizParser.ParseQuestions(text))
                {
                    quizIds.Add(question.id);
                }

                if (quizIds.Count > 0)
                {
                    markers.Add(new ProgressMarkerDescriptor(i, ProgressMarkerKind.Quiz, quizIds.ToArray()));
                }

                HashSet<string> checkIds = new(StringComparer.Ordinal);
                foreach (Match match in CheckBlockRegex.Matches(text))
                {
                    try
                    {
                        checkIds.Add(QuizStateStore.CheckIdForLink(BuildCheckLink(match.Groups[1].Value)));
                    }
                    catch
                    {
                        // Same as CollectCheckIds: a block without a transport link has no check button.
                    }
                }

                if (checkIds.Count > 0)
                {
                    string[] ids = new string[checkIds.Count];
                    checkIds.CopyTo(ids);
                    markers.Add(new ProgressMarkerDescriptor(i, ProgressMarkerKind.Check, ids));
                }
            }

            UpdateProgressMarkerDescriptors(markers, state);
            return markers;
        }

        // Re-evaluate cached ids only. Quiz completion includes exhausted attempts, like GetLessonPercent.
        internal static void UpdateProgressMarkerDescriptors(
            List<ProgressMarkerDescriptor> markers, LessonQuizState state)
        {
            foreach (ProgressMarkerDescriptor marker in markers)
            {
                bool unfinished = false;
                bool failed = false;
                bool remarks = false;
                foreach (string id in marker.Ids)
                {
                    if (marker.Kind == ProgressMarkerKind.Quiz)
                    {
                        unfinished |= state?.questionIdToState == null ||
                                      !state.questionIdToState.TryGetValue(id, out QuizQuestionState question) ||
                                      question == null || !question.isCompleted;
                    }
                    else if (state?.checkIdToPassed == null || !state.checkIdToPassed.TryGetValue(id, out bool passed))
                    {
                        unfinished = true;
                    }
                    else if (!passed)
                    {
                        failed = true;
                    }
                    else if (state.checkIdToRemarks != null &&
                             state.checkIdToRemarks.TryGetValue(id, out bool hasRemarks) && hasRemarks)
                    {
                        remarks = true;
                    }
                }

                // A failed check remains visible even when another check on the same slide is untouched.
                marker.State = failed ? ProgressMarkerState.Failed
                    : unfinished ? ProgressMarkerState.NotDone
                    : remarks ? ProgressMarkerState.Remarks : ProgressMarkerState.Done;
                string result = marker.State == ProgressMarkerState.Remarks ? "засчитано с замечаниями"
                    : marker.State == ProgressMarkerState.Failed ? "не пройдена"
                    : marker.Kind == ProgressMarkerKind.Quiz
                        ? (unfinished ? "не пройден" : "пройден")
                        : (unfinished ? "не выполнено" : "выполнено");
                string kind = marker.Kind == ProgressMarkerKind.Quiz ? "Квиз" : "Практика";
                marker.Tooltip = $"{kind} · слайд {marker.SlideIndex + 1} · {result}";
            }
        }

        internal static void GetSlideProgress(int slideCount, int currentSlide, int maxSlideReached,
            out int farthest, out float fillPercent, out float markerPercent)
        {
            if (slideCount <= 0)
            {
                farthest = -1;
                fillPercent = markerPercent = 0;
                return;
            }

            int current = Math.Max(0, Math.Min(currentSlide, slideCount - 1));
            farthest = Math.Max(current, Math.Min(maxSlideReached, slideCount - 1));
            fillPercent = 100f * (farthest + 1) / slideCount;
            markerPercent = 100f * (current + 0.5f) / slideCount;
        }

        private void ClearProgressMarkers()
        {
            courseProgressCheckpoints?.Clear();
            progressMarkersLessonPath = null;
            progressMarkersSlides = null;
            progressMarkerDescriptors.Clear();
            progressMarkerElements.Clear();
            progressMarkerQuizIds.Clear();
            progressMarkerCheckIds.Clear();
        }

        internal static bool CanJumpToCheckpoint(int targetSlide, int currentSlide, int maxSlideReached)
        {
            return targetSlide <= Math.Max(currentSlide, maxSlideReached);
        }

        private void UpdateProgressMarkers(LessonQuizState state)
        {
            if (courseProgressCheckpoints == null)
            {
                return;
            }

            if (!string.Equals(progressMarkersLessonPath, currentLessonFilePath, StringComparison.Ordinal) ||
                !ReferenceEquals(progressMarkersSlides, slides))
            {
                ClearProgressMarkers();
                progressMarkerDescriptors = BuildProgressMarkerDescriptors(slides, state);
                progressMarkersLessonPath = currentLessonFilePath;
                progressMarkersSlides = slides;
                foreach (ProgressMarkerDescriptor descriptor in progressMarkerDescriptors)
                {
                    if (descriptor.Kind == ProgressMarkerKind.Quiz)
                    {
                        progressMarkerQuizIds.AddRange(descriptor.Ids);
                    }
                    else
                    {
                        progressMarkerCheckIds.UnionWith(descriptor.Ids);
                    }

                    VisualElement chip = new();
                    chip.AddToClassList("algo-course-progress-checkpoint");
                    chip.AddToClassList(descriptor.Kind == ProgressMarkerKind.Quiz
                        ? "algo-course-progress-checkpoint--quiz" : "algo-course-progress-checkpoint--check");
                    Label glyph = new() { pickingMode = PickingMode.Ignore };
                    glyph.AddToClassList("algo-course-progress-checkpoint-glyph");
                    chip.Add(glyph);
                    int targetSlide = descriptor.SlideIndex;
                    chip.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (evt.button != 0)
                        {
                            return;
                        }

                        evt.StopPropagation();
                        LessonQuizState currentState = QuizStateStore.GetLessonState(currentLessonFilePath, false);
                        if (targetSlide != currentSlideIndex &&
                            CanJumpToCheckpoint(targetSlide, currentSlideIndex, currentState?.maxSlideReached ?? -1))
                        {
                            ShowSlide(targetSlide);
                        }
                    });
                    courseProgressCheckpoints.Add(chip);
                    progressMarkerElements.Add(chip);
                }
            }
            else
            {
                UpdateProgressMarkerDescriptors(progressMarkerDescriptors, state);
            }

            UpdateProgressMarkerPositions();
            for (int i = 0; i < progressMarkerDescriptors.Count; i++)
            {
                ProgressMarkerDescriptor descriptor = progressMarkerDescriptors[i];
                VisualElement element = progressMarkerElements[i];
                element.EnableInClassList("algo-course-progress-checkpoint--done", descriptor.State == ProgressMarkerState.Done);
                element.EnableInClassList("algo-course-progress-checkpoint--remarks", descriptor.State == ProgressMarkerState.Remarks);
                element.EnableInClassList("algo-course-progress-checkpoint--failed", descriptor.State == ProgressMarkerState.Failed);
                element.EnableInClassList("algo-course-progress-checkpoint--current", descriptor.SlideIndex == currentSlideIndex);
                bool canJump = CanJumpToCheckpoint(descriptor.SlideIndex, currentSlideIndex, state?.maxSlideReached ?? -1);
                element.EnableInClassList("algo-course-progress-checkpoint--locked", !canJump);
                element.Q<Label>().text = descriptor.State == ProgressMarkerState.Done ? "✓"
                    : descriptor.State == ProgressMarkerState.Remarks ? "!"
                    : descriptor.State == ProgressMarkerState.Failed ? "✕"
                    : descriptor.Kind == ProgressMarkerKind.Quiz ? "?" : "▶";
                element.tooltip = descriptor.Tooltip +
                    (canJump ? string.Empty : " · сначала дойдите до этого слайда");
            }
        }

        private void UpdateProgressMarkerPositions()
        {
            if (courseProgressCheckpoints == null || slides == null || slides.Count == 0)
            {
                return;
            }

            float width = courseProgressCheckpoints.contentRect.width;
            for (int i = 0; i < progressMarkerDescriptors.Count; i++)
            {
                ProgressMarkerDescriptor descriptor = progressMarkerDescriptors[i];
                bool paired = (i > 0 && progressMarkerDescriptors[i - 1].SlideIndex == descriptor.SlideIndex) ||
                              (i + 1 < progressMarkerDescriptors.Count &&
                               progressMarkerDescriptors[i + 1].SlideIndex == descriptor.SlideIndex);
                float offset = paired ? (descriptor.Kind == ProgressMarkerKind.Quiz ? -7f : 7f) : 0f;
                float percent = 100f * (descriptor.SlideIndex + 0.5f) / slides.Count;
                if (width > 0 && !float.IsInfinity(width))
                {
                    // Clamp the shared centre, preserving the pair's +/-7px offsets. Reserve
                    // half a chip at maximum hover scale; only the track clips, never a segment.
                    float inset = Mathf.Min(width * 0.5f, 13f * 1.25f * 0.5f + Mathf.Abs(offset));
                    float centre = Mathf.Clamp(width * percent / 100f, inset, width - inset);
                    percent = 100f * centre / width;
                }

                progressMarkerElements[i].style.left = Length.Percent(percent);
                progressMarkerElements[i].style.marginLeft = offset;
            }
        }

        private int GetCachedLessonPercent(LessonQuizState state,
            out int completedQuizzes, out int totalQuizzes, out int completedChecks, out int totalChecks)
        {
            completedQuizzes = 0;
            completedChecks = 0;
            totalQuizzes = progressMarkerQuizIds.Count;
            totalChecks = progressMarkerCheckIds.Count;
            foreach (string id in progressMarkerQuizIds)
            {
                if (state?.questionIdToState != null &&
                    state.questionIdToState.TryGetValue(id, out QuizQuestionState question) &&
                    question != null && question.isCompleted)
                {
                    completedQuizzes++;
                }
            }

            foreach (string id in progressMarkerCheckIds)
            {
                if (state?.checkIdToPassed != null && state.checkIdToPassed.TryGetValue(id, out bool passed) && passed)
                {
                    completedChecks++;
                }
            }

            int slidesDone = state == null ? 0 : Mathf.Clamp(state.maxSlideReached, 0, slides.Count - 1) + 1;
            int totalItems = slides.Count + totalQuizzes + totalChecks;
            return totalItems == 0 ? 0
                : Mathf.Clamp(Mathf.RoundToInt(100f * (slidesDone + completedQuizzes + completedChecks) / totalItems), 0, 100);
        }
    }
}
