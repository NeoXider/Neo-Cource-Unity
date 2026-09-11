using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NeoCource.Editor.Tests
{
    public class CourseWindowTests
    {
        private static MethodInfo GetPrivateMethod(Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        [Test]
        public void SplitSlides_SplitsByTripleDash()
        {
            Type t = typeof(CourseWindow);
            MethodInfo mi = GetPrivateMethod(t, "SplitSlides");
            Assert.IsNotNull(mi, "SplitSlides method not found");

            string md = "Slide A\n\n---\n\nSlide B\n---\nSlide C";
            List<string> slides = (List<string>)mi.Invoke(null, new object[] { md });
            Assert.AreEqual(3, slides.Count);
            Assert.AreEqual("Slide A", slides[0]);
            Assert.AreEqual("Slide B", slides[1]);
            Assert.AreEqual("Slide C", slides[2]);
        }

        [Test]
        public void SplitSlides_Ignores_TripleDash_Inside_Fence()
        {
            Type t = typeof(CourseWindow);
            MethodInfo mi = GetPrivateMethod(t, "SplitSlides");
            Assert.IsNotNull(mi, "SplitSlides method not found");

            string md = "Slide A\n```csharp\n---\n```\n---\nSlide B";
            List<string> slides = (List<string>)mi.Invoke(null, new object[] { md });
            Assert.AreEqual(2, slides.Count);
            StringAssert.Contains("---", slides[0]);
            Assert.AreEqual("Slide B", slides[1]);
        }

        [Test]
        public void GetLessonPercent_Untouched_Lesson_Is_Zero()
        {
            string path = Path.Combine(Path.GetTempPath(), "algo_percent_test.md");
            try
            {
                File.WriteAllText(path,
                    "# T\n---\n```quiz\nid: q1\nkind: single\ntext: Q?\nanswers:\n  - text: A\n    correct: true\n  - text: B\n```\n");
                int pct = CourseWindow.GetLessonPercent(path, out int done, out int total);
                Assert.AreEqual(1, total);
                Assert.AreEqual(0, done);
                Assert.AreEqual(0, pct);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void GetLessonPercent_Missing_File_Is_Zero()
        {
            int pct = CourseWindow.GetLessonPercent(
                Path.Combine(Path.GetTempPath(), "algo_no_such_percent_xyz.md"),
                out int done, out int total);
            Assert.AreEqual(0, pct);
            Assert.AreEqual(0, done);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void GetLessonPercent_Counts_Checks_Without_Touching_Disk()
        {
            string path = Path.Combine(Path.GetTempPath(), "algo_percent_checks_test.md");
            try
            {
                File.WriteAllText(path,
                    "# T\n```check\nrules:\n  - object_exists: \"Player\"\n```\n---\nSecond\n");
                int pct = CourseWindow.GetLessonPercent(path,
                    out int quizDone, out int quizTotal, out int checksDone, out int checksTotal);
                Assert.AreEqual(0, quizTotal);
                Assert.AreEqual(1, checksTotal);
                Assert.AreEqual(0, checksDone);
                Assert.AreEqual(0, pct);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        [Test]
        public void CollectCheckIds_Counts_Check_Blocks()
        {
            Type t = typeof(CourseWindow);
            MethodInfo mi = t.GetMethod("CollectCheckIds", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "CollectCheckIds method not found");

            string md = "# T\n```check\nrules:\n  - object_exists: \"Player\"\n```\n---\nNo checks here\n";
            var ids = (System.Collections.Generic.HashSet<string>)mi.Invoke(null, new object[] { md });
            Assert.AreEqual(1, ids.Count);
        }

        [Test]
        public void PreprocessMediaLinks_ResolvesRelativeToMdFolder()
        {
            // Arrange: create a temp image under Assets so that project-relative path can be built
            string assetsPath = Application.dataPath.Replace('\n', '/');
            string tempDir = Path.Combine(assetsPath, "_AlgoNeoCourse/TempTestMedia/images");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            string imgFull = Path.Combine(tempDir, "pic.png");
            if (!File.Exists(imgFull))
            {
                // Валидный PNG 1x1 (прозрачный): мусорные байты роняли бы импорт
                // при AssetDatabase.Refresh() с ошибкой "File could not be read".
                File.WriteAllBytes(imgFull, new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                    0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                    0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                    0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                    0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                    0x42, 0x60, 0x82
                });
            }

            string mdDir = Path.Combine(assetsPath, "_AlgoNeoCourse/TempTestMedia");
            string mdFull = Path.Combine(mdDir, "lesson.md");
            if (!Directory.Exists(mdDir))
            {
                Directory.CreateDirectory(mdDir);
            }

            File.WriteAllText(mdFull, "# test");

            CourseWindow wnd = ScriptableObject.CreateInstance<CourseWindow>();

            Type t = typeof(CourseWindow);
            FieldInfo field = t.GetField("currentLessonFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(wnd, mdFull);

            MethodInfo mi = GetPrivateMethod(t, "PreprocessMediaLinks");
            Assert.IsNotNull(mi, "PreprocessMediaLinks method not found");

            string input = "![img](images/pic.png)";
            string output = (string)mi.Invoke(wnd, new object[] { input });

            Assert.IsTrue(output.Contains("Assets/_AlgoNeoCourse/TempTestMedia/images/pic.png"), output);

            // Cleanup
            try
            {
                AssetDatabase.Refresh();
                File.Delete(imgFull);
                File.Delete(mdFull);
                Directory.Delete(Path.GetDirectoryName(imgFull), true);
                Directory.Delete(mdDir, true);
            }
            catch
            {
            }
        }
    }
}