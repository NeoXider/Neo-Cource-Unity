using System.Collections.Generic;
using System.IO;
using NeoCource.Editor.Progress;
using NeoCource.Editor.Quizzes;
using NUnit.Framework;
using UnityEngine;

namespace NeoCource.Editor.Tests
{
    // Ключи прогресса: путь урока относительно проекта, без учёта регистра; старые абсолютные ключи мигрируются.
    // Тесты работают только с данными в памяти — файл прогресса проекта не читают и не пишут.
    public class CourseProgressMigrationTests
    {
        private const string Relative = "Assets/_AlgoNeoCourse/Downloaded/l2m2y1-y1.md";

        private static string Root =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');

        private static LessonQuizState State(int maxSlide, string lessonPath = null)
        {
            return new LessonQuizState { lessonPath = lessonPath, maxSlideReached = maxSlide, lastSlideIndex = maxSlide };
        }

        [Test]
        public void Key_Is_Project_Relative_With_Forward_Slashes()
        {
            Assert.AreEqual(Relative, CourseProgressStore.NormalizeLessonPathKey(Root + "/" + Relative));
            Assert.AreEqual(Relative,
                CourseProgressStore.NormalizeLessonPathKey("Assets/_AlgoNeoCourse/Downloaded\\l2m2y1-y1.md"));
            Assert.AreEqual(Relative,
                CourseProgressStore.NormalizeLessonPathKey((Root + "/" + Relative).Replace('/', '\\')));
            Assert.AreEqual(string.Empty, CourseProgressStore.NormalizeLessonPathKey(null));
            Assert.AreEqual(string.Empty, CourseProgressStore.NormalizeLessonPathKey("  "));
        }

        [Test]
        public void Differently_Cased_Absolute_Keys_Collapse_Into_One_Keeping_Better_Entry()
        {
            string upperKey = Root.ToUpperInvariant() + "/" + Relative;
            string normalKey = Root + "/" + Relative;
            if (upperKey == normalKey)
            {
                upperKey = Root.ToLowerInvariant() + "/" + Relative;
            }

            LessonQuizState weak = State(0, "Assets/_AlgoNeoCourse/Downloaded\\l2m2y1-y1.md");
            weak.questionIdToState["q1"] = new QuizQuestionState { questionId = "q1", attemptsUsed = 1 };
            weak.questionIdToState["onlyWeak"] = new QuizQuestionState
                { questionId = "onlyWeak", isCompleted = true, isCorrect = true };
            weak.checkIdToPassed["check:a"] = true;
            weak.checkIdToPassed["check:b"] = false;

            LessonQuizState strong = State(19, "Assets/_AlgoNeoCourse/Downloaded\\l2m2y1-y1.md");
            strong.slidesTotal = 20;
            strong.questionIdToState["q1"] = new QuizQuestionState
                { questionId = "q1", attemptsUsed = 2, isCompleted = true, isCorrect = true };
            strong.checkIdToPassed["check:a"] = false;
            strong.checkIdToPassed["check:c"] = true;

            // Обычный (регистрозависимый) словарь — как после чтения старого файла.
            CourseProgressData data = new()
            {
                lessonStates = new Dictionary<string, LessonQuizState> { [upperKey] = weak, [normalKey] = strong },
                lastLessonPath = normalKey,
                farthestLessonPath = upperKey,
                schemaVersion = 1
            };

            bool changed = CourseProgressStore.MigrateLessonKeys(data, Root);

            Assert.IsTrue(changed);
            Assert.AreEqual(1, data.lessonStates.Count);
            Assert.IsTrue(data.lessonStates.ContainsKey(Relative));
            Assert.IsTrue(data.lessonStates.ContainsKey(Relative.ToUpperInvariant()), "ключи без учёта регистра");

            LessonQuizState merged = data.lessonStates[Relative];
            Assert.AreEqual(Relative, merged.lessonPath);
            Assert.AreEqual(19, merged.maxSlideReached);
            Assert.AreEqual(19, merged.lastSlideIndex);
            Assert.AreEqual(20, merged.slidesTotal);
            Assert.IsTrue(merged.questionIdToState["q1"].isCompleted);
            Assert.IsTrue(merged.questionIdToState["onlyWeak"].isCompleted, "вопрос из слабой записи не потерян");
            Assert.IsTrue(merged.checkIdToPassed["check:a"], "пройдено хотя бы в одной записи");
            Assert.IsFalse(merged.checkIdToPassed["check:b"]);
            Assert.IsTrue(merged.checkIdToPassed["check:c"]);

            Assert.AreEqual(Relative, data.lastLessonPath);
            Assert.AreEqual(Relative, data.farthestLessonPath);
            Assert.AreEqual(2, data.schemaVersion);

            // Повторный прогон ничего не меняет.
            Assert.IsFalse(CourseProgressStore.MigrateLessonKeys(data, Root));
            Assert.AreEqual(1, data.lessonStates.Count);
            Assert.AreEqual(19, data.lessonStates[Relative].maxSlideReached);
        }

        [Test]
        public void Better_Entry_Wins_Regardless_Of_Order()
        {
            LessonQuizState strong = State(7);
            strong.checkIdToPassed["check:x"] = true;
            LessonQuizState weak = State(2);
            weak.questionIdToState["q"] = new QuizQuestionState { questionId = "q", isCompleted = true };

            CourseProgressData data = new()
            {
                lessonStates = new Dictionary<string, LessonQuizState>
                {
                    [Root + "/Assets/L/a.md"] = strong,
                    ["Assets\\L\\A.md"] = weak
                }
            };

            Assert.IsTrue(CourseProgressStore.MigrateLessonKeys(data, Root));
            Assert.AreEqual(1, data.lessonStates.Count);
            LessonQuizState merged = data.lessonStates["Assets/L/a.md"];
            Assert.AreEqual(7, merged.maxSlideReached);
            Assert.IsTrue(merged.checkIdToPassed["check:x"]);
            Assert.IsTrue(merged.questionIdToState["q"].isCompleted);
        }

        [Test]
        public void Absolute_Key_From_Another_Machine_Becomes_Relative()
        {
            CourseProgressData data = new()
            {
                lessonStates = new Dictionary<string, LessonQuizState>
                {
                    ["C:/Users/student/OtherProject/" + Relative] =
                        State(5, "Assets/_AlgoNeoCourse/Downloaded\\l2m2y1-y1.md"),
                    ["E:/Work/Proj/Assets/Lessons/intro.md"] = State(3, "E:/Work/Proj/Assets/Lessons/intro.md")
                },
                lastLessonPath = "C:/Users/student/OtherProject/" + Relative
            };

            Assert.IsTrue(CourseProgressStore.MigrateLessonKeys(data, Root));
            Assert.AreEqual(2, data.lessonStates.Count);
            Assert.AreEqual(5, data.lessonStates[Relative].maxSlideReached);
            Assert.AreEqual(3, data.lessonStates["Assets/Lessons/intro.md"].maxSlideReached);
            Assert.AreEqual(Relative, data.lastLessonPath);
        }

        [Test]
        public void Old_Json_With_Duplicate_Lesson_Is_Loaded_Without_Loss()
        {
            string upperKey = Root.ToUpperInvariant() + "/" + Relative;
            string normalKey = Root + "/" + Relative;
            if (upperKey == normalKey)
            {
                upperKey = Root.ToLowerInvariant() + "/" + Relative;
            }

            // Формат файла до миграции: абсолютные ключи, один урок записан дважды.
            string json = "{\n" +
                          "  \"lastLessonPath\": \"" + Relative + "\",\n" +
                          "  \"lastSlideIndex\": 10,\n" +
                          "  \"farthestLessonPath\": \"" + Relative + "\",\n" +
                          "  \"farthestSlideIndex\": 10,\n" +
                          "  \"lessonStates\": {\n" +
                          "    \"" + upperKey + "\": {\n" +
                          "      \"lessonPath\": \"Assets/_AlgoNeoCourse/Downloaded\\\\l2m2y1-y1.md\",\n" +
                          "      \"questionIdToState\": { \"q1\": { \"questionId\": \"q1\", \"attemptsUsed\": 0,\n" +
                          "        \"isCompleted\": false, \"isCorrect\": false, \"shuffledOrder\": [1, 0],\n" +
                          "        \"selectedAnswerIds\": [] } },\n" +
                          "      \"checkIdToPassed\": {},\n" +
                          "      \"maxSlideReached\": 0\n" +
                          "    },\n" +
                          "    \"" + normalKey + "\": {\n" +
                          "      \"lessonPath\": \"Assets/_AlgoNeoCourse/Downloaded\\\\l2m2y1-y1.md\",\n" +
                          "      \"questionIdToState\": { \"q1\": { \"questionId\": \"q1\", \"attemptsUsed\": 1,\n" +
                          "        \"isCompleted\": true, \"isCorrect\": true, \"shuffledOrder\": [0, 1],\n" +
                          "        \"selectedAnswerIds\": [\"a1\"] } },\n" +
                          "      \"checkIdToPassed\": { \"check:one\": true, \"check:two\": false },\n" +
                          "      \"lastSlideIndex\": 19, \"maxSlideReached\": 19, \"slidesTotal\": 20\n" +
                          "    }\n" +
                          "  },\n" +
                          "  \"schemaVersion\": 1,\n" +
                          "  \"createdAtUnixMs\": 1789117630171\n" +
                          "}";

            CourseProgressData data = CourseProgressStore.ParseProgressJson(json, out bool migrated);

            Assert.IsTrue(migrated);
            Assert.AreEqual(1, data.lessonStates.Count);
            LessonQuizState state = data.lessonStates[Relative];
            Assert.AreEqual(19, state.maxSlideReached);
            Assert.AreEqual(20, state.slidesTotal);
            Assert.IsTrue(state.questionIdToState["q1"].isCompleted);
            CollectionAssert.AreEqual(new[] { 0, 1 }, state.questionIdToState["q1"].shuffledOrder);
            Assert.IsTrue(state.questionIdToState["q1"].selectedAnswerIds.Contains("a1"));
            Assert.IsTrue(state.checkIdToPassed["check:one"]);
            Assert.IsFalse(state.checkIdToPassed["check:two"]);
            Assert.AreEqual(Relative, data.lastLessonPath);
            Assert.AreEqual(10, data.lastSlideIndex);
            Assert.AreEqual(10, data.farthestSlideIndex);
            Assert.AreEqual(1789117630171L, data.createdAtUnixMs);
            Assert.AreEqual(2, data.schemaVersion);

            // Уже перенесённый файл читается без изменений.
            string again = CourseProgressStore.SerializeProgressJson(data);
            StringAssert.Contains("\"" + Relative + "\": {", again);
            StringAssert.DoesNotContain(Root + "/", again);
            CourseProgressData reloaded = CourseProgressStore.ParseProgressJson(again, out bool migratedAgain);
            Assert.IsFalse(migratedAgain);
            Assert.AreEqual(1, reloaded.lessonStates.Count);
            Assert.AreEqual(19, reloaded.lessonStates[Relative.ToLowerInvariant()].maxSlideReached);
        }

        [Test]
        public void Empty_Or_Missing_Data_Is_Safe()
        {
            Assert.IsFalse(CourseProgressStore.MigrateLessonKeys(null, Root));

            CourseProgressData empty = CourseProgressStore.ParseProgressJson("", out bool migrated);
            Assert.IsFalse(migrated);
            Assert.IsNotNull(empty.lessonStates);
            Assert.AreEqual(0, empty.lessonStates.Count);

            CourseProgressData noStates = new() { lessonStates = null };
            Assert.IsFalse(CourseProgressStore.MigrateLessonKeys(noStates, Root));
            Assert.IsNotNull(noStates.lessonStates);
        }
    }
}
