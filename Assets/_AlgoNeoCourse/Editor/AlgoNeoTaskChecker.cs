using System.Collections.Generic;
using UnityEditor;
using NeoCource.Editor.Settings;
using NeoCource.Editor.Util;
using NeoCource.Editor.Tasks;

namespace NeoCource.Editor
{
    internal static class AlgoNeoTaskChecker
    {
        public static void Execute(Dictionary<string, string> args)
        {
            if (args == null || !args.TryGetValue("type", out var type) || string.IsNullOrEmpty(type))
            {
                EditorUtility.DisplayDialog("Проверка задания", "Параметры проверки не заданы", "Ок");
                return;
            }

            // Проверим флаг включения
            if (!ValidationSettings.Instance.IsCheckEnabled(type))
            {
                if (ValidationSettings.Instance.LogVerbose)
                    UnityEngine.Debug.Log($"[AlgoNeoCourse] Проверка '{type}' отключена настройками");
                return;
            }

            if (!TaskCheckRegistry.TryGet(type, out var checker))
            {
                // Особый случай: запускаем из блока ```check``` — попытаемся распарсить YAML и выполнить набор правил
                if (type == "from-block" && args != null && args.TryGetValue("__raw_block__", out var raw))
                {
                    var (blockOk, blockMsg) = EvaluateYamlBlock(raw);
                    EditorUtility.DisplayDialog("Проверка блока", (blockOk ? "✅ " : "❌ ") + (blockMsg ?? string.Empty), "Ок");
                    return;
                }

                EditorUtility.DisplayDialog("Проверка задания", $"Неизвестная проверка: {type}", "Ок");
                return;
            }

            var ok = checker.TryExecute(args, out var msg);
            if (ValidationSettings.Instance.EnableDialogs)
            {
                if (ok && ValidationSettings.Instance.ShowSuccessDialog || !ok && ValidationSettings.Instance.ShowFailureDialog)
                    EditorUtility.DisplayDialog("Проверка задания", (ok ? "✅ " : "❌ ") + (msg ?? string.Empty), "Ок");
            }
            else if (ValidationSettings.Instance.LogVerbose)
            {
                var prefix = "[AlgoNeoCourse] ";
                string isOk = prefix + (ok ? "OK" : "FAIL");
                string line = $" {type}: {msg}";

                var settings = CourseSettings.instance;
                if (ok)
                    UnityEngine.Debug.Log(isOk.Color(settings.okLogColor) + line);
                else
                    UnityEngine.Debug.Log(isOk.Color(settings.failLogColor) + line);
            }
        }

        public static (bool ok, string message) EvaluateYamlBlock(string raw)
        {
            // Минималистичный парсер: ищем простые правила
            // type: scene / project и т.п.; rules: - object_exists: "Player"; - component_exists: object: "Player" type: "Rigidbody"
            if (string.IsNullOrEmpty(raw))
            {
                return (false, "Пустой блок check");
            }

            var lines = raw.Split('\n');
            var messages = new System.Text.StringBuilder();
            bool allOk = true;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- object_exists:"))
                {
                    var name = trimmed.Split(':')[1].Trim().Trim('"');
                    var go = UnityEngine.GameObject.Find(name);
                    bool ok = go != null;
                    allOk &= ok;
                    messages.AppendLine((ok ? "✅" : "❌") + $" object_exists: {name}");
                }
                else if (trimmed.StartsWith("object:") || trimmed.StartsWith("type:"))
                {
                    // обработается в компонентной проверке после накопления контекста
                }
            }

            // Простая проверка компонента: находим пары object:/type:
            string objName = null; string compType = null;
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("object:")) objName = t.Split(':')[1].Trim().Trim('"');
                if (t.StartsWith("type:")) compType = t.Split(':')[1].Trim().Trim('"');
            }
            if (!string.IsNullOrEmpty(objName) && !string.IsNullOrEmpty(compType))
            {
                var go = UnityEngine.GameObject.Find(objName);
                var type = System.Type.GetType(compType) ?? System.Type.GetType("UnityEngine." + compType + ", UnityEngine");
                bool ok = go != null && type != null && go.GetComponent(type) != null;
                allOk &= ok;
                messages.AppendLine((ok ? "✅" : "❌") + $" component_exists: {objName}.{compType}");
            }

            return (allOk, messages.ToString());
        }
    }
}


