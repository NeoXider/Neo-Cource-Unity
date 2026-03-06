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
        public Dictionary<string, LessonQuizState> lessonStates = new();
    }
}