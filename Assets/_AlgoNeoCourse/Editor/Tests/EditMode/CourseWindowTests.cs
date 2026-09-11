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
                File.WriteAllBytes(imgFull, new byte[] { 0 });
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