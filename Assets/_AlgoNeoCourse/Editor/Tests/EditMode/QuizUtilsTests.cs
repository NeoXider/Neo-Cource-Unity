using System.Collections.Generic;
using NeoCource.Editor.Quizzes;
using NUnit.Framework;

namespace NeoCource.Editor.Tests
{
    public class QuizUtilsTests
    {
        [Test]
        public void StableSeed_Is_Deterministic()
        {
            Assert.AreEqual(QuizUtils.StableSeed("lesson|x"), QuizUtils.StableSeed("lesson|x"));
            Assert.AreEqual(0, QuizUtils.StableSeed(null));
            Assert.AreEqual(0, QuizUtils.StableSeed(string.Empty));
        }

        [Test]
        public void StableSeed_Differs_For_Different_Inputs()
        {
            Assert.AreNotEqual(QuizUtils.StableSeed("lesson|q1"), QuizUtils.StableSeed("lesson|q2"));
        }

        [Test]
        public void Shuffle_With_Same_Seed_Gives_Same_Order()
        {
            List<int> a = new() { 0, 1, 2, 3, 4 };
            List<int> b = new() { 0, 1, 2, 3, 4 };
            int seed = QuizUtils.StableSeed("lesson|q1");
            QuizUtils.Shuffle(a, seed);
            QuizUtils.Shuffle(b, seed);
            CollectionAssert.AreEqual(a, b);
        }
    }
}
