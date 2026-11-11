using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoCource.Editor.Utils;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NeoCource.Editor.Tests
{
    // Движок правил блока check: один разбор (ParseRules), одна проверка (EvaluateRules),
    // вердикт Evaluate() строится из них же.
    public class CheckRulesTests
    {
        private const string TempFolder = "Assets/_AlgoNeoCourse/TempRuleTests";
        private const string ProbeA = "AlgoRuleProbeAlpha.txt";
        private const string ProbeB = "AlgoRuleProbeBeta.txt";

        private readonly List<GameObject> created = new();
        private Func<List<(string name, string path)>> originalScenes;

        [SetUp]
        public void SetUp()
        {
            originalScenes = AlgoNeoYamlChecker.LoadedScenesProvider;
        }

        [TearDown]
        public void TearDown()
        {
            AlgoNeoYamlChecker.LoadedScenesProvider = originalScenes;
            foreach (GameObject go in created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            created.Clear();

            // Временные файлы не импортировались (без Refresh) — убираем вместе с возможными .meta.
            try
            {
                if (Directory.Exists(TempFolder))
                {
                    Directory.Delete(TempFolder, true);
                }

                if (File.Exists(TempFolder + ".meta"))
                {
                    File.Delete(TempFolder + ".meta");
                }
            }
            catch
            {
            }
        }

        private GameObject Create(string name, Transform parent = null, bool active = true)
        {
            GameObject go = new(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.SetActive(active);
            created.Add(go);
            return go;
        }

        private static void WriteProbes()
        {
            Directory.CreateDirectory(TempFolder);
            File.WriteAllText(Path.Combine(TempFolder, ProbeA), "public class Alpha {\n    void Update() { }\n}\n");
            File.WriteAllText(Path.Combine(TempFolder, ProbeB), "public class Beta {\n    int score;\n}\n");
        }

        private static string Block(params string[] rules)
        {
            return "rules:\n" + string.Join("\n", rules.Select(r => "  " + r));
        }

        [Test]
        public void Unknown_Key_Fails_And_Is_Named()
        {
            string raw = Block("- has_component: \"Rigidbody\"");

            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(raw);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(CheckRuleKind.Unknown, rules[0].kind);
            Assert.AreEqual("has_component", rules[0].target);
            StringAssert.Contains("has_component", rules[0].Describe());

            (bool ok, string message) = AlgoNeoYamlChecker.Evaluate(raw);
            Assert.IsFalse(ok);
            StringAssert.Contains("has_component", message);
        }

        [Test]
        public void Unknown_Key_Fails_Even_When_Other_Rules_Pass()
        {
            Create("AlgoRuleTest_Known");
            string raw = Block("- object_exists: \"AlgoRuleTest_Known\"", "- object_exist: \"AlgoRuleTest_Known\"");

            List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].ok);
            Assert.IsFalse(results[1].ok);
            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(raw).ok);
        }

        [Test]
        public void Empty_Block_Fails()
        {
            foreach (string raw in new[] { "", "   ", "rules:", "rules:\n  # только комментарий\n", "type: scene\nrules:\n" })
            {
                (bool ok, string message) = AlgoNeoYamlChecker.Evaluate(raw);
                Assert.IsFalse(ok, "raw: '" + raw + "'");
                Assert.IsFalse(string.IsNullOrEmpty(message));

                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
                Assert.AreEqual(1, results.Count, "raw: '" + raw + "'");
                Assert.AreEqual(CheckRuleKind.EmptyBlock, results[0].rule.kind);
                Assert.IsFalse(results[0].ok);
            }
        }

        [Test]
        public void Verdict_Always_Matches_Rule_Results()
        {
            Create("AlgoRuleTest_Verdict");
            string[] blocks =
            {
                Block("- object_exists: \"AlgoRuleTest_Verdict\""),
                Block("- object_exists: \"AlgoRuleTest_Missing_xyz\""),
                Block("- scene_nam: \"X\""),
                "rules:"
            };
            foreach (string raw in blocks)
            {
                Assert.AreEqual(AlgoNeoYamlChecker.IsPassed(AlgoNeoYamlChecker.EvaluateRules(raw)),
                    AlgoNeoYamlChecker.Evaluate(raw).ok, raw);
            }

            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(blocks[0]).ok);
        }

        [Test]
        public void SceneName_Passes_Only_For_Loaded_Scene()
        {
            AlgoNeoYamlChecker.LoadedScenesProvider = () => new List<(string name, string path)>
            {
                ("TicTacToe", "Assets/Scenes/TicTacToe.unity")
            };

            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block("- scene_name: \"TicTacToe\""));
            Assert.AreEqual(CheckRuleKind.SceneName, rules[0].kind);
            StringAssert.Contains("Открыта сцена", rules[0].Describe());

            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(Block("- scene_name: \"TicTacToe\"")).ok);
            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(Block("- scene_name: \"Scenes/TicTacToe.unity\"")).ok);

            List<CheckRuleResult> wrong = AlgoNeoYamlChecker.EvaluateRules(Block("- scene_name: \"Menu\""));
            Assert.IsFalse(wrong[0].ok);
            StringAssert.Contains("TicTacToe", wrong[0].hint);

            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(Block("- scene_name: \"\"")).ok);
        }

        [Test]
        public void SceneName_Uses_Real_Loaded_Scenes_By_Default()
        {
            // Без подмены: сцены с таким именем точно нет.
            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(Block("- scene_name: \"AlgoRuleTest_NoSuchScene_xyz\"")).ok);
        }

        [Test]
        public void Inline_Comment_And_Single_Quotes_Are_Parsed()
        {
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(Block(
                "- object_exists: \"Player\"  # главный герой",
                "- object_exists: 'Enemy'   # одинарные кавычки",
                "- object_exists: Boss # без кавычек",
                "# строка-комментарий",
                "- filename: \"Score.cs\"",
                "- contains: \"a # b\"  # решётка внутри кавычек — часть текста",
                "- contains: \"Debug.Log(\\\"hi\\\");\"",
                "- contains: 'it''s'"));

            Assert.AreEqual(7, rules.Count);
            Assert.IsTrue(rules.All(r => r.kind != CheckRuleKind.Unknown && r.problem == null));
            Assert.AreEqual("Player", rules[0].target);
            Assert.AreEqual("Enemy", rules[1].target);
            Assert.AreEqual("Boss", rules[2].target);
            Assert.AreEqual("Score.cs", rules[3].target);
            Assert.AreEqual("a # b", rules[4].value);
            Assert.AreEqual("Debug.Log(\"hi\");", rules[5].value);
            Assert.AreEqual("it's", rules[6].value);

            // Неэкранированные кавычки внутри значения (так писали до появления экранирования) не обрезают его.
            Assert.AreEqual("text = \"Очки: \" + score",
                AlgoNeoYamlChecker.ParseScalar(" \"text = \"Очки: \" + score\"  # комментарий"));
            Assert.AreEqual(string.Empty, AlgoNeoYamlChecker.ParseScalar("  # только комментарий"));
        }

        [Test]
        public void Crlf_Block_Is_Parsed_Like_Lf()
        {
            List<CheckRule> rules =
                AlgoNeoYamlChecker.ParseRules("rules:\r\n  - object_exists: \"Player\"\r\n  - scene_name: \"Main\"\r\n");
            Assert.AreEqual(2, rules.Count);
            Assert.AreEqual("Player", rules[0].target);
            Assert.AreEqual("Main", rules[1].target);
        }

        [Test]
        public void Contains_Is_Case_Sensitive()
        {
            WriteProbes();

            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(Block(
                "- filename: \"" + ProbeA + "\"", "- contains: \"void Update()\"")).ok);

            List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(Block(
                "- filename: \"" + ProbeA + "\"", "- contains: \"void update()\""));
            Assert.IsTrue(results[0].ok, "файл должен найтись");
            Assert.IsFalse(results[1].ok, "void update() не должен засчитываться за void Update()");
        }

        [Test]
        public void Each_Contains_Applies_To_Nearest_Filename_Above()
        {
            WriteProbes();
            string raw = Block(
                "- filename: \"" + ProbeA + "\"",
                "- contains: \"void Update()\"",
                "- filename: \"" + ProbeB + "\"",
                "- contains: \"int score;\"",
                "- contains: \"void Update()\"");

            List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(ProbeA, results[1].rule.target);
            Assert.AreEqual(ProbeB, results[3].rule.target);
            Assert.AreEqual(ProbeB, results[4].rule.target);
            Assert.IsTrue(results[0].ok && results[1].ok && results[2].ok && results[3].ok);
            Assert.IsFalse(results[4].ok, "void Update() есть только в первом файле");
            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(raw).ok);
        }

        [Test]
        public void Missing_File_And_Contains_Without_Filename_Fail()
        {
            Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(Block(
                "- filename: \"AlgoRuleNoSuchFile_xyz.cs\"", "- contains: \"class\"")).ok);

            List<CheckRuleResult> orphan = AlgoNeoYamlChecker.EvaluateRules(Block("- contains: \"class\""));
            Assert.AreEqual(1, orphan.Count);
            Assert.IsFalse(orphan[0].ok);
            StringAssert.Contains("filename", orphan[0].hint);
        }

        [Test]
        public void Inactive_Object_Is_Found_By_Name_And_By_Path()
        {
            GameObject canvas = Create("AlgoRuleTest_Canvas");
            GameObject panel = Create("AlgoRuleTest_GameOverPanel", canvas.transform, false);
            Create("AlgoRuleTest_RestartButton", panel.transform);

            Assert.IsNull(GameObject.Find("AlgoRuleTest_GameOverPanel"), "предпосылка: Find не видит выключенные");

            Assert.IsNotNull(AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_GameOverPanel"));
            Assert.IsNotNull(AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_RestartButton"));
            Assert.IsNotNull(
                AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_GameOverPanel/AlgoRuleTest_RestartButton"));
            Assert.IsNotNull(AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_Canvas/AlgoRuleTest_GameOverPanel"));
            Assert.IsNull(AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_Canvas/AlgoRuleTest_RestartButton"),
                "путь — это цепочка родителей, а не «где-то внутри»");
            Assert.IsNull(AlgoNeoEditorUtils.FindSceneObject("AlgoRuleTest_NoSuchObject_xyz"));

            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(Block(
                "- object_exists: \"AlgoRuleTest_GameOverPanel\"",
                "- object_exists: \"AlgoRuleTest_GameOverPanel/AlgoRuleTest_RestartButton\"")).ok);
        }

        [Test]
        public void Component_Check_Uses_Exact_Type_Name()
        {
            GameObject player = Create("AlgoRuleTest_Player");
            player.AddComponent<BoxCollider2D>();

            Assert.AreEqual(typeof(CircleCollider2D), AlgoNeoEditorUtils.FindTypeByName("CircleCollider2D"));
            Assert.AreEqual(typeof(Collider2D), AlgoNeoEditorUtils.FindTypeByName("Collider2D"));
            Assert.AreEqual(typeof(Rigidbody2D), AlgoNeoEditorUtils.FindTypeByName("UnityEngine.Rigidbody2D"));
            Assert.IsNull(AlgoNeoEditorUtils.FindTypeByName("AlgoRuleNoSuchType_xyz"));

            Assert.IsTrue(HasComponent("AlgoRuleTest_Player", "BoxCollider2D"));
            Assert.IsTrue(HasComponent("AlgoRuleTest_Player", "Collider2D"), "наследник проходит проверку базового типа");
            Assert.IsFalse(HasComponent("AlgoRuleTest_Player", "CircleCollider2D"),
                "BoxCollider2D не должен засчитываться за CircleCollider2D");
            Assert.IsFalse(HasComponent("AlgoRuleTest_Player", "AlgoRuleNoSuchType_xyz"));
            Assert.IsFalse(HasComponent("AlgoRuleTest_NoSuchObject_xyz", "BoxCollider2D"));
        }

        private static bool HasComponent(string objectName, string typeName)
        {
            return AlgoNeoYamlChecker.Evaluate(Block(
                "- component_exists:", "    object: \"" + objectName + "\"", "    type: \"" + typeName + "\"")).ok;
        }

        [Test]
        public void Component_Check_Accepts_Inline_Mapping()
        {
            GameObject player = Create("AlgoRuleTest_Inline");
            player.AddComponent<Rigidbody2D>();

            string raw = Block("- component_exists: { object: \"AlgoRuleTest_Inline\", type: \"Rigidbody2D\" }");
            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(raw);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("AlgoRuleTest_Inline", rules[0].target);
            Assert.AreEqual("Rigidbody2D", rules[0].value);
            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(raw).ok);
        }

        [Test]
        public void Component_Without_Object_Means_Any_Object_In_Scene()
        {
            string raw = Block("- component_exists:", "    type: \"AudioReverbZone\"");

            List<CheckRule> rules = AlgoNeoYamlChecker.ParseRules(raw);
            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(CheckRuleKind.ComponentExists, rules[0].kind);
            Assert.IsNull(rules[0].target);
            Assert.IsNull(rules[0].problem);
            StringAssert.Contains("На сцене есть объект с компонентом", rules[0].Describe());

            List<CheckRuleResult> before = AlgoNeoYamlChecker.EvaluateRules(raw);
            Assert.IsFalse(before[0].ok, "компонента ещё нет ни на одном объекте");

            // Компонент на ВЫКЛЮЧЕННОМ дочернем объекте тоже считается.
            GameObject root = Create("AlgoRuleTest_AnyRoot");
            GameObject child = Create("AlgoRuleTest_AnyChild", root.transform, false);
            child.AddComponent<AudioReverbZone>();

            Assert.IsTrue(AlgoNeoYamlChecker.Evaluate(raw).ok);
        }

        [Test]
        public void Component_Without_Type_Is_An_Error()
        {
            Create("AlgoRuleTest_NoType");
            foreach (string raw in new[]
                     {
                         Block("- component_exists:", "    object: \"AlgoRuleTest_NoType\""),
                         Block("- component_exists:"),
                         Block("- component_exists: { object: \"AlgoRuleTest_NoType\" }")
                     })
            {
                List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
                Assert.AreEqual(1, results.Count, raw);
                Assert.IsFalse(results[0].ok, raw);
                StringAssert.Contains("type", results[0].hint);
                Assert.IsFalse(AlgoNeoYamlChecker.Evaluate(raw).ok, raw);
            }
        }

        [Test]
        public void Component_Rule_Does_Not_Swallow_Following_Rules()
        {
            GameObject player = Create("AlgoRuleTest_Order");
            player.AddComponent<BoxCollider2D>();
            string raw = Block(
                "- component_exists:",
                "    object: \"AlgoRuleTest_Order\"",
                "    type: \"BoxCollider2D\"",
                "- object_exists: \"AlgoRuleTest_Order\"",
                "- component_exists:",
                "    type: \"BoxCollider2D\"",
                "    object: \"AlgoRuleTest_Order\"");

            List<CheckRuleResult> results = AlgoNeoYamlChecker.EvaluateRules(raw);
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => r.ok));
        }
    }
}
