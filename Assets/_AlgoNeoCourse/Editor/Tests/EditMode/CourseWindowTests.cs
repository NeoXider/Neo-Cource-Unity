using System;
using System.IO;
using System.Linq;
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
            var t = typeof(NeoCource.Editor.CourseWindow);
            var mi = GetPrivateMethod(t, "SplitSlides");
            Assert.IsNotNull(mi, "SplitSlides method not found");

            string md = "Slide A\n\n---\n\nSlide B\n---\nSlide C";
            var slides = (System.Collections.Generic.List<string>)mi.Invoke(null, new object[] { md });
            Assert.AreEqual(3, slides.Count);
            Assert.AreEqual("Slide A", slides[0]);
            Assert.AreEqual("Slide B", slides[1]);
            Assert.AreEqual("Slide C", slides[2]);
        }

        [Test]
        public void PreprocessMediaLinks_ResolvesRelativeToMdFolder()
        {
            // Arrange: create a temp image under Assets so that project-relative path can be built
            string assetsPath = Application.dataPath.Replace('\n', '/');
            string tempDir = Path.Combine(assetsPath, "_AlgoNeoCourse/TempTestMedia/images");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            string imgFull = Path.Combine(tempDir, "pic.png");
            if (!File.Exists(imgFull)) File.WriteAllBytes(imgFull, new byte[] { 0 });

            string mdDir = Path.Combine(assetsPath, "_AlgoNeoCourse/TempTestMedia");
            string mdFull = Path.Combine(mdDir, "lesson.md");
            if (!Directory.Exists(mdDir)) Directory.CreateDirectory(mdDir);
            File.WriteAllText(mdFull, "# test");

            var wnd = ScriptableObject.CreateInstance<NeoCource.Editor.CourseWindow>();

            var t = typeof(NeoCource.Editor.CourseWindow);
            var field = t.GetField("currentLessonFilePath", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(wnd, mdFull);

            var mi = GetPrivateMethod(t, "PreprocessMediaLinks");
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
            catch { }
        }
    }
}


