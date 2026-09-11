using System.IO;
using System.Reflection;
using NeoCource.Editor.Progress;
using NUnit.Framework;

namespace NeoCource.Editor.Tests
{
    public class CourseProgressIsolationTests
    {
        private static bool IsPathInsideProject(string path)
        {
            MethodInfo mi = typeof(CourseProgressStore).GetMethod(
                "IsPathInsideProject", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "IsPathInsideProject method not found");
            return (bool)mi.Invoke(null, new object[] { path });
        }

        [Test]
        public void Progress_File_Inside_Project_Is_Accepted()
        {
            string inside = CourseProgressStore.GetProgressFileAssetPath();
            Assert.IsTrue(IsPathInsideProject(inside), inside);
        }

        [Test]
        public void Foreign_Absolute_Path_Is_Rejected()
        {
            // Другой проект / чужая папка — сейв оттуда грузить нельзя.
            string foreign = Path.Combine(Path.GetTempPath(), "OtherProject",
                "Assets/_AlgoNeoCourse/Progress/course-progress.json");
            Assert.IsFalse(IsPathInsideProject(foreign), foreign);
        }

        [Test]
        public void Empty_Path_Is_Rejected()
        {
            Assert.IsFalse(IsPathInsideProject(null));
            Assert.IsFalse(IsPathInsideProject(string.Empty));
            Assert.IsFalse(IsPathInsideProject("   "));
        }
    }
}
