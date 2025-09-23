using System;
using System.Collections.Generic;

namespace NeoCource.Editor.Quizzes
{
    public enum QuizKind
    {
        Single,
        Multiple,
        TrueFalse
    }

    [Serializable]
    public class QuizAnswer
    {
        public string id;        // локальный id внутри вопроса
        public string text;
        public bool isCorrect;
    }

    [Serializable]
    public class QuizQuestion
    {
        public string id;        // уникально в рамках урока
        public QuizKind kind;
        public string text;
        public List<QuizAnswer> answers = new List<QuizAnswer>();
    }

    [Serializable]
    public class QuizQuestionState
    {
        public string questionId;
        public int attemptsUsed;
        public bool isCompleted;
        public bool isCorrect;
        public List<int> shuffledOrder = new List<int>();
        public HashSet<string> selectedAnswerIds = new HashSet<string>();
    }

    [Serializable]
    public class LessonQuizState
    {
        public string lessonPath;
        public Dictionary<string, QuizQuestionState> questionIdToState = new Dictionary<string, QuizQuestionState>();
    }
}


