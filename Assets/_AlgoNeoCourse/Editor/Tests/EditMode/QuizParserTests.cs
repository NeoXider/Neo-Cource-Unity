using NUnit.Framework;
using NeoCource.Editor.Quizzes;

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
            var list = QuizParser.ParseQuestions(md);
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
            var outMd = QuizParser.ReplaceQuizBlocksWithMarkers(md, out var questions);
            Assert.AreEqual(1, questions.Count);
            StringAssert.Contains("[[QUIZ:q1]]", outMd);
        }
    }
}


