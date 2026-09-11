using System.Collections.Generic;
using NeoCource.Editor.Quizzes;
using NUnit.Framework;

namespace NeoCource.Editor.Tests
{
    public class QuizParserTests
    {
        [Test]
        public void Parse_Single_Block()
        {
            string md = @"```quiz
id: q1
kind: single
text: Q?
answers:
  - text: A
    correct: true
  - text: B
```";
            List<QuizQuestion> list = QuizParser.ParseQuestions(md);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("q1", list[0].id);
        }

        [Test]
        public void Replace_With_Markers_Generates_Markers()
        {
            string md = @"```quiz
id: q1
kind: truefalse
text: T/F?
answers:
  - text: True
    correct: true
  - text: False
```";
            string outMd = QuizParser.ReplaceQuizBlocksWithMarkers(md, out List<QuizQuestion> questions);
            Assert.AreEqual(1, questions.Count);
            StringAssert.Contains("[[QUIZ:q1]]", outMd);
        }

        [Test]
        public void Replace_Does_Not_Leak_Questions_Between_Calls()
        {
            string md1 = @"```quiz
id: qa
kind: single
text: Q?
answers:
  - text: A
    correct: true
```";
            QuizParser.ReplaceQuizBlocksWithMarkers(md1, out List<QuizQuestion> first);
            Assert.AreEqual(1, first.Count);

            string md2 = @"```quiz
id: qb
kind: single
text: Q?
answers:
  - text: A
    correct: true
```";
            QuizParser.ReplaceQuizBlocksWithMarkers(md2, out List<QuizQuestion> second);
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual("qb", second[0].id);
        }

        [Test]
        public void Replace_Duplicate_Ids_Get_Suffixed()
        {
            string md = @"```quiz
id: q1
kind: single
text: Q1?
answers:
  - text: A
    correct: true
```
```quiz
id: q1
kind: single
text: Q2?
answers:
  - text: B
    correct: true
```";
            string outMd = QuizParser.ReplaceQuizBlocksWithMarkers(md, out List<QuizQuestion> questions);
            Assert.AreEqual(2, questions.Count);
            Assert.AreEqual("q1", questions[0].id);
            Assert.AreEqual("q1-2", questions[1].id);
            StringAssert.Contains("[[QUIZ:q1-2]]", outMd);
        }

        [Test]
        public void Replace_Broken_Block_Stays_As_Is()
        {
            string md = @"```quiz
kind: single
text: No id here
```";
            string outMd = QuizParser.ReplaceQuizBlocksWithMarkers(md, out List<QuizQuestion> questions);
            Assert.AreEqual(0, questions.Count);
            StringAssert.Contains("```quiz", outMd);
        }
    }
}