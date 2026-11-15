using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeoCource.Editor.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace NeoCource.Editor.Tests
{
    // Дополнительные правила (optional: true) и правило field (значение поля компонента), с 1.9.0.
    // Старые блоки без новых параметров должны разбираться ровно как раньше.
    public class OptionalAndFieldRulesTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            created.Clear();
        }

        private GameObject Create(string name)
        {
            GameObject go = new(name);
            created.Add(go);
            return go;
        }

        private static string Block(params string[] rules)
        {
            return "rules:\n" + string.Join("\n", rules.Select(r => "  " + r));
        }

        private static string Field(string objectName, string type, string member, string equals,
            bool optional = false)
        {
            string text = Block("- field:",
                "    object: \"" + objectName + "\"",
                "    type: \"" + type + "\"",
                "    name: \"" + member + "\"",
                "    equals: " + equals);
            return optional ? text + "\n    optional: true" : text;
        }

        [Test]
        public void Optional_Parameter_Is_Read_For_Every_Rule_Kind()
        {
            string raw = Block(
                "- object_exists: \"A\"",
                "  optional: true",
                "- filename: \"A.cs\"",
                "  optional: yes",
                "- contains: \"void Start()\"",
                "  optional: true",
                "- scene_name: \"Main\"",
                "  optional: false",
                "- component_exists:",
                "    object: \"A\"",
                "    type: \"BoxCollider2D\"",
                "    optional: true",
                "- component_exists: { object: \"A\", type: \"Camera\", optional: true }",
                "- object_exists: \"B\"");

            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(raw);
            Assert.AreEqual(7, rules.Count);
            Assert.IsTrue(rules.All(r => r.problem == null && r.kind != CheckRuleKind.Unknown));
            CollectionAssert.AreEqual(new[] { true, true, true, false, true, true, false },
                rules.Select(r => r.optional).ToArray());
            Assert.AreEqual("A.cs", rules[2].target, "contains по-прежнему относится к filename выше");
        }

        [Test]
        public void Old_Blocks_Parse_As_Before()
        {
            // contains с отступом под filename (без дефиса) — отдельное правило, как до 1.9.0.
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block(
                "- filename: \"A.cs\"",
                "  contains: \"x\"",
                "- object_exists: \"P\""));
            Assert.AreEqual(3, rules.Count);
            Assert.AreEqual(CheckRuleKind.Contains, rules[1].kind);
            Assert.IsTrue(rules.All(r => !r.optional && r.problem == null));

            // Заголовок "type: scene" не становится параметром правила.
            rules = AlgoNeoYamlChecker.ParseRules("type: scene\nrules:\n  - object_exists: \"P\"\ntype: combined");
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(CheckRuleKind.ObjectExists, rules[0].kind);
        }

        [Test]
        public void Bad_Optional_Value_Is_An_Error()
        {
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block("- object_exists: \"A\"", "  optional: maybe"));
            Assert.AreEqual(1, rules.Count);
            StringAssert.Contains("optional", rules[0].problem);
        }

        [Test]
        public void Verdict_Separates_Remarks_From_Errors()
        {
            Create("AlgoOpt_Present");

            string perfect = Block("- object_exists: \"AlgoOpt_Present\"",
                "- object_exists: \"AlgoOpt_Present\"", "  optional: true");
            string remarks = Block("- object_exists: \"AlgoOpt_Present\"",
                "- object_exists: \"AlgoOpt_Missing\"", "  optional: true");
            string failed = Block("- object_exists: \"AlgoOpt_Missing\"",
                "- object_exists: \"AlgoOpt_Present\"", "  optional: true");
            // Ошибка в самом дополнительном правиле — не замечание, а провал: правило надо чинить.
            string brokenOptional = Block("- object_exists: \"AlgoOpt_Present\"",
                "- field:", "    object: \"AlgoOpt_Present\"", "    type: \"Transform\"", "    optional: true");

            Assert.AreEqual(CheckVerdict.Perfect,
                AlgoNeoYamlChecker.GetVerdict(AlgoNeoYamlChecker.EvaluateRules(perfect)));
            Assert.AreEqual(CheckVerdict.PassedWithRemarks,
                AlgoNeoYamlChecker.GetVerdict(AlgoNeoYamlChecker.EvaluateRules(remarks)));
            Assert.AreEqual(CheckVerdict.Failed,
                AlgoNeoYamlChecker.GetVerdict(AlgoNeoYamlChecker.EvaluateRules(failed)));
            Assert.AreEqual(CheckVerdict.Failed,
                AlgoNeoYamlChecker.GetVerdict(AlgoNeoYamlChecker.EvaluateRules(brokenOptional)));

            (bool ok, string message) = AlgoNeoYamlChecker.Evaluate(remarks);
            Assert.IsTrue(ok, "провал только дополнительного правила задание засчитывает");
            StringAssert.Contains("ЗАМЕЧАНИЕ", message);
            StringAssert.Contains("дополнительно: 0/1", message);
            StringAssert.Contains("засчитано", message);
            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(failed).ok);
        }

        [Test]
        public void Field_Compares_Numbers_Bools_Enums_Vectors_Colors()
        {
            GameObject go = Create("AlgoField_Probe");
            go.transform.localPosition = new Vector3(0f, -4.2f, 0f);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f, 0.5f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
            sprite.sortingOrder = 5;
            sprite.color = new Color(1f, 0.5f, 0f);

            string[] passing =
            {
                Field("AlgoField_Probe", "Transform", "localPosition", "\"0, -4.2, 0\""),
                Field("AlgoField_Probe", "Transform", "localPosition", "\"0, -4.2\""),
                Field("AlgoField_Probe", "Transform", "localPosition.y", "-4.2"),
                Field("AlgoField_Probe", "BoxCollider2D", "isTrigger", "true"),
                Field("AlgoField_Probe", "BoxCollider2D", "size", "\"2, 0.5\""),
                Field("AlgoField_Probe", "Rigidbody2D", "bodyType", "Kinematic"),
                Field("AlgoField_Probe", "Rigidbody2D", "bodyType", "kinematic"),
                Field("AlgoField_Probe", "SpriteRenderer", "sortingOrder", "5"),
                Field("AlgoField_Probe", "SpriteRenderer", "color", "\"#FF8000\""),
                Field("AlgoField_Probe", "SpriteRenderer", "sprite", "none"),
                Field("AlgoField_Probe", "Transform", "name", "\"AlgoField_Probe\"")
            };
            foreach (string raw in passing)
            {
                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
                Assert.AreEqual(1, results.Count, raw);
                Assert.IsTrue(results[0].ok, raw + "\n→ " + results[0].hint);
            }

            List<CheckRuleResult> wrong =
                AlgoNeoYamlChecker.EvaluateRules(Field("AlgoField_Probe", "SpriteRenderer", "sortingOrder", "8"));
            Assert.IsFalse(wrong[0].ok);
            Assert.AreEqual("сейчас 5, в уроке 8", wrong[0].hint);

            wrong = AlgoNeoYamlChecker.EvaluateRules(Field("AlgoField_Probe", "SpriteRenderer", "sprite", "\"Slime\""));
            Assert.IsFalse(wrong[0].ok);
            StringAssert.Contains("сейчас пусто", wrong[0].hint);

            wrong = AlgoNeoYamlChecker.EvaluateRules(Field("AlgoField_Probe", "Rigidbody2D", "bodyType", "Dynamic"));
            Assert.AreEqual("сейчас Kinematic, в уроке Dynamic", wrong[0].hint);
        }

        [Test]
        public void Field_Count_Reads_Array_Length()
        {
            GameObject go = Create("AlgoField_Edge");
            EdgeCollider2D edge = go.AddComponent<EdgeCollider2D>();
            edge.points = new[] { Vector2.zero, Vector2.one, Vector2.right };

            string raw = Block("- field:", "    object: \"AlgoField_Edge\"", "    type: \"EdgeCollider2D\"",
                "    name: \"points\"", "    count: 3");
            Assert.IsTrue(AlgoNeoYamlChecker.EvaluateRules(raw)[0].ok);

            CheckRuleResult wrong = AlgoNeoYamlChecker.EvaluateRules(raw.Replace("count: 3", "count: 5"))[0];
            Assert.IsFalse(wrong.ok);
            Assert.AreEqual("сейчас элементов 3, в уроке 5", wrong.hint);
        }

        [Test]
        public void Field_Problems_Are_Explained()
        {
            Create("AlgoField_Errors").AddComponent<BoxCollider2D>();
            string head = "- field:\n    object: \"AlgoField_Errors\"\n    type: \"BoxCollider2D\"\n";

            (string raw, string expectedHint)[] cases =
            {
                ("rules:\n  " + head + "    equals: 1", "name"),
                ("rules:\n  " + head + "    name: \"isTrigger\"", "equals"),
                ("rules:\n  " + head + "    name: \"edgeRadius\"\n    equals: 1\n    count: 1", "одно"),
                ("rules:\n  " + head + "    name: \"edgeRadius\"\n    count: two", "целое"),
                ("rules:\n  " + head + "    name: \"noSuchMember\"\n    equals: 1", "нет поля"),
                ("rules:\n  " + head + "    name: \"isTrigger\"\n    equals: maybe", "true или false"),
                ("rules:\n  - field:\n    prefab: \"AlgoNoSuchPrefab_42\"\n    type: \"Transform\"\n" +
                 "    name: \"position\"\n    equals: \"0, 0, 0\"", "префаба"),
                ("rules:\n  - field:\n    object: \"AlgoField_Errors\"\n    prefab: \"X\"\n    type: \"Transform\"\n" +
                 "    name: \"position\"\n    equals: 0", "object"),
                ("rules:\n  - field: { object: \"AlgoField_Errors\", type: \"Transform\", colour: red }", "colour")
            };

            foreach ((string raw, string expectedHint) in cases)
            {
                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
                Assert.AreEqual(1, results.Count, raw);
                Assert.IsFalse(results[0].ok, raw);
                StringAssert.Contains(expectedHint, results[0].hint, raw);
            }
        }

        [Test]
        public void Card_Shows_Optional_Rows_And_Verdicts()
        {
            Create("AlgoCard_Present");
            string raw = Block("- object_exists: \"AlgoCard_Present\"",
                "- object_exists: \"AlgoCard_Missing\"", "  optional: true");

            CheckCard card = new(raw, null, null, null);
            List<VisualElement> rows = card.Query<VisualElement>(className: "check-rule").ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsFalse(rows[0].ClassListContains("check-rule--optional"));
            Assert.IsTrue(rows[1].ClassListContains("check-rule--optional"));
            Assert.AreEqual("дополнительно", rows[1].Q<Label>(className: "check-rule__tag").text);

            card.ShowResults(AlgoNeoYamlChecker.EvaluateRules(raw));
            Assert.AreEqual(CheckVerdict.PassedWithRemarks, card.Verdict);
            Assert.IsTrue(card.ClassListContains("check-card--done"));
            Assert.IsTrue(card.ClassListContains("check-card--remarks"));
            Assert.IsFalse(card.ClassListContains("check-card--perfect"));

            Create("AlgoCard_Missing");
            card.ShowResults(AlgoNeoYamlChecker.EvaluateRules(raw));
            Assert.AreEqual(CheckVerdict.Perfect, card.Verdict);
            Assert.IsTrue(card.ClassListContains("check-card--perfect"));
            Assert.IsFalse(card.ClassListContains("check-card--remarks"));
            Assert.AreEqual(1, card.Query<VisualElement>(className: "check-card__shine").ToList().Count,
                "«идеально» запускает блик");
            StringAssert.StartsWith("идеально", card.Q<Label>(className: "check-card__status").text);

            CheckCard saved = new(raw, null, null, CheckVerdict.PassedWithRemarks);
            Assert.IsTrue(saved.ClassListContains("check-card--remarks"));
            StringAssert.Contains("замечани", saved.Q<Label>(className: "check-card__status").text);
        }

        [Test]
        public void Legacy_Inline_Component_Ignores_Unknown_Keys_And_Last_Type_Wins()
        {
            Create("AlgoLegacy_Component");
            string[] mappings =
            {
                "{ object: AlgoLegacy_Component, type: Transform, extra: ignored }",
                "{ object: AlgoLegacy_Component, type: NoSuchComponent, type: Transform }",
                "{ object: AlgoLegacy_Component, type: Transform, optional: maybe, optional: true }"
            };
            foreach (string mapping in mappings)
            {
                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(Block("- component_exists: " + mapping));
                Assert.AreEqual(1, results.Count);
                Assert.IsNull(results[0].rule.problem, mapping);
                Assert.IsTrue(results[0].ok, mapping);
            }
        }

        [Test]
        public void Inline_Component_Still_Consumes_Following_Object_Parameter()
        {
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block(
                "- component_exists: { object: First, type: Transform }", "  object: Second"));
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("Second", rules[0].target);
        }

        [TestCase("equals: 1", "count: 1")]
        [TestCase("count: 1", "equals: 1")]
        public void Mixed_Inline_And_Line_Comparisons_Are_A_Rule_Problem(string inline, string line)
        {
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block(
                "- field: { object: Probe, type: Transform, name: childCount, " + inline + " }", "  " + line));
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("укажите что-то одно: equals или count", rules[0].problem);
        }

        [Test]
        public void Optional_Invalid_Bool_And_Count_On_Scalar_Fail_The_Block()
        {
            Create("AlgoOptional_Format").AddComponent<BoxCollider2D>();
            string invalidBool = Field("AlgoOptional_Format", "BoxCollider2D", "isTrigger", "maybe", true);
            string invalidCount = Field("AlgoOptional_Format", "BoxCollider2D", "isTrigger", "1", true)
                .Replace("equals: 1", "count: 1");
            foreach (string raw in new[] { invalidBool, invalidCount })
            {
                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
                Assert.IsFalse(results[0].ok);
                Assert.IsNotEmpty(results[0].rule.problem);
                Assert.AreEqual(results[0].rule.problem, results[0].hint);
                Assert.AreEqual(CheckVerdict.Failed, AlgoNeoYamlChecker.GetVerdict(results));
            }

            List<CheckRuleResult> missing = AlgoNeoYamlChecker.EvaluateRules(
                Field("AlgoOptional_Format", "BoxCollider2D", "notMadeYet", "true", true));
            Assert.IsNull(missing[0].rule.problem);
            Assert.AreEqual(CheckVerdict.PassedWithRemarks, AlgoNeoYamlChecker.GetVerdict(missing));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void NonFinite_Expected_Numbers_Are_Rejected(string expected)
        {
            Assert.IsFalse(AlgoNeoYamlChecker.TryParseNumber(expected, out _));
            foreach (object actual in new object[] { 0f, Vector3.zero, Color.black })
            {
                string value = actual is float ? expected : expected + ", 0, 0";
                Assert.IsFalse(AlgoNeoYamlChecker.TryCompare(actual, value, null, out bool equal, out _, out string problem));
                Assert.IsFalse(equal);
                Assert.IsNotEmpty(problem);
            }

            CheckRule rule = AlgoNeoYamlChecker.ParseRules(Block(
                "- field: { object: Probe, type: Transform, name: position, equals: 0, tolerance: " + expected + " }"))[0];
            Assert.IsNotEmpty(rule.problem);
        }

        [Test]
        public void NonFinite_Actual_Values_Never_Compare_Equal()
        {
            object[] values =
            {
                double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                new Vector3(0, 0, float.NaN), new Vector3(0, 0, float.PositiveInfinity),
                new Color(0, 0, 0, float.NaN), new Color(0, 0, 0, float.PositiveInfinity)
            };
            foreach (object actual in values)
            {
                string expected = actual is Color ? "#000000" : "0";
                Assert.IsTrue(AlgoNeoYamlChecker.TryCompare(actual, expected, null, out bool equal, out _, out _));
                Assert.IsFalse(equal, actual.ToString());
            }
        }

        private class BaseMember
        {
            public int Value => 1;
        }

        private sealed class AmbiguousMember : BaseMember
        {
            public new string Value => "one";
        }

        [Test]
        public void Ambiguous_Member_Lookup_Returns_Failure_Without_Throwing()
        {
            Assert.Throws<AmbiguousMatchException>(() => typeof(AmbiguousMember).GetProperty("Value"));
            bool read = true;
            string problem = null;
            Assert.DoesNotThrow(() => read = AlgoNeoYamlChecker.TryReadMember(
                new AmbiguousMember(), "Value", out _, out problem));
            Assert.IsFalse(read);
            StringAssert.Contains("не удалось прочитать", problem);
        }

        [Test]
        public void Throwing_Rule_Does_Not_Abort_Following_Rules()
        {
            var original = AlgoNeoYamlChecker.LoadedScenesProvider;
            try
            {
                AlgoNeoYamlChecker.LoadedScenesProvider = () => throw new InvalidOperationException("probe failure");
                Create("AlgoAfter_Throw");
                List<CheckRuleResult> results = null;
                Assert.DoesNotThrow(() => results = AlgoNeoYamlChecker.EvaluateRules(Block(
                    "- scene_name: Main", "- object_exists: AlgoAfter_Throw")));
                Assert.AreEqual(2, results.Count);
                Assert.IsFalse(results[0].ok);
                Assert.AreEqual("не удалось проверить: probe failure", results[0].hint);
                Assert.IsTrue(results[1].ok);
            }
            finally
            {
                AlgoNeoYamlChecker.LoadedScenesProvider = original;
            }
        }

        [Test]
        public void Evaluate_Returns_One_Fresh_Rule_Run_Per_Call()
        {
            var original = AlgoNeoYamlChecker.LoadedScenesProvider;
            try
            {
                int calls = 0;
                AlgoNeoYamlChecker.LoadedScenesProvider = () =>
                {
                    calls++;
                    return new List<(string, string)> { (calls == 1 ? "Main" : "Other", "") };
                };
                const string raw = "rules:\n  - scene_name: Main";
                Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(raw, out List<CheckRuleResult> first).ok);
                Assert.AreEqual(1, calls);
                Assert.IsTrue(first[0].ok);
                Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(raw, out List<CheckRuleResult> second).ok);
                Assert.AreEqual(2, calls);
                Assert.IsFalse(second[0].ok);
                Assert.AreNotSame(first, second);
            }
            finally
            {
                AlgoNeoYamlChecker.LoadedScenesProvider = original;
            }
        }

        [Test]
        public void Showing_New_Results_Cancels_Previous_Card_Animation()
        {
            Create("AlgoAnimation_Probe");
            const string raw = "rules:\n  - object_exists: AlgoAnimation_Probe";
            CheckCard card = new(raw, null, null, null);
            card.ShowResults(AlgoNeoYamlChecker.EvaluateRules(raw));
            Assert.IsNotNull(card.Q(className: "check-card__shine"));
            card.style.translate = new Translate(6f, 0f);
            card.ShowResults(AlgoNeoYamlChecker.EvaluateRules("rules:\n  - object_exists: AlgoAnimation_Missing"));
            Assert.IsNull(card.Q(className: "check-card__shine"));
            Assert.AreEqual(StyleKeyword.Null, card.style.translate.keyword);
        }
    }
}
