using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoCource.Editor.Validation;
using NUnit.Framework;

namespace NeoCource.Editor.Tests
{
    public class LessonValidatorTests
    {
        private readonly List<string> tempFiles = new();

        [TearDown]
        public void CleanupTempFiles()
        {
            foreach (string f in tempFiles)
            {
                try
                {
                    if (File.Exists(f))
                    {
                        File.Delete(f);
                    }
                }
                catch
                {
                }
            }

            tempFiles.Clear();
        }

        private string WriteTempLesson(string name, string content)
        {
            string path = Path.Combine(Path.GetTempPath(), name);
            File.WriteAllText(path, content);
            tempFiles.Add(path);
            return path;
        }

        [Test]
        public void Valid_Lesson_Has_No_Errors()
        {
            string path = WriteTempLesson("algo_valid_test.md",
                "# Title\n---\n```quiz\nid: q1\nkind: single\ntext: Q?\nanswers:\n  - text: A\n    correct: true\n  - text: B\n```\n");
            List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "valid");
            Assert.IsFalse(issues.Any(i => i.severity == "error"),
                string.Join("; ", issues.Select(i => i.rule + ": " + i.message)));
        }

        [Test]
        public void Duplicate_Ids_Are_Errors()
        {
            string path = WriteTempLesson("algo_dup_test.md",
                "```quiz\nid: q1\nkind: single\ntext: Q1?\nanswers:\n  - text: A\n    correct: true\n```\n---\n```quiz\nid: Q1\nkind: single\ntext: Q2?\nanswers:\n  - text: B\n    correct: true\n```\n");
            List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "dup");
            Assert.IsTrue(issues.Any(i => i.rule == "quiz-duplicate-id" && i.severity == "error"));
        }

        [Test]
        public void Empty_Answers_And_Missing_Correct_Are_Errors()
        {
            string path = WriteTempLesson("algo_noans_test.md",
                "```quiz\nid: q1\nkind: single\ntext: Q?\nanswers:\n```\n---\n```quiz\nid: q2\nkind: multiple\ntext: Q?\nanswers:\n  - text: A\n  - text: B\n```\n");
            List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "noans");
            Assert.IsTrue(issues.Any(i => i.rule == "quiz-no-answers"));
            Assert.IsTrue(issues.Any(i => i.rule == "quiz-no-correct"));
        }

        [Test]
        public void Unknown_Kind_And_Removed_Slide_Link_Are_Errors()
        {
            string path = WriteTempLesson("algo_kind_test.md",
                "```quiz\nid: q1\nkind: matching\ntext: Q?\nanswers:\n  - text: A\n    correct: true\n```\n---\n[Next](unity://slide?dir=next)\n");
            List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "kind");
            Assert.IsTrue(issues.Any(i => i.rule == "quiz-unknown-kind"));
            Assert.IsTrue(issues.Any(i => i.rule == "unity-slide-removed"));
        }

        [Test]
        public void Missing_File_Returns_Issue_Without_Throwing()
        {
            string path = Path.Combine(Path.GetTempPath(), "algo_no_such_lesson_xyz.md");
            Assert.DoesNotThrow(() =>
            {
                List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "missing");
                Assert.IsTrue(issues.Count > 0);
            });
        }

        [Test]
        public void Code_Fence_Content_Is_Not_Validated_As_Media_Or_Links()
        {
            // Примеры внутри ```md-fence не должны давать ложных срабатываний
            // media/unity-проверок (там это не ссылки, а текст примеров).
            string path = WriteTempLesson("algo_fence_test.md",
                "# Doc\n```md\n![img](fake-pic.png)\n[Next](unity://slide?dir=next)\n```\n---\nReal text\n");
            List<ValidationIssue> issues = LessonValidator.ValidateLessonFile(path, "fence");
            Assert.IsFalse(issues.Any(i => i.rule == "media-missing"));
            Assert.IsFalse(issues.Any(i => i.rule == "unity-slide-removed"));
        }
    }
}
