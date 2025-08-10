using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
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
                    // Перекрытие диалогов, если явно указано в ссылке dialog=console|dialog|auto
                    args.TryGetValue("dialog", out var dialogMode);
                    bool useDialogs = ValidationSettings.Instance.EnableDialogs;
                    if (!string.IsNullOrEmpty(dialogMode))
                    {
                        if (dialogMode.Equals("console", System.StringComparison.OrdinalIgnoreCase)) useDialogs = false;
                        else if (dialogMode.Equals("dialog", System.StringComparison.OrdinalIgnoreCase))
                            useDialogs = true;
                    }

                    if (useDialogs)
                    {
                        EditorUtility.DisplayDialog("Проверка блока",
                            (blockOk ? "> " : "X ") + (blockMsg ?? string.Empty), "Ок");
                    }
                    else
                    {
                        var prefix = "[AlgoNeoCourse] ";
                        var settings = CourseSettings.instance;
                        var isOk = prefix + (blockOk ? "OK" : "FAIL");
                        var coloredBody = ColorizeChecks(blockMsg ?? string.Empty, blockOk, settings);
                        UnityEngine.Debug.Log(
                            (blockOk ? isOk.Color(settings.okLogColor) : isOk.Color(settings.failLogColor)) + "\n" +
                            coloredBody);
                    }

                    return;
                }

                EditorUtility.DisplayDialog("Проверка задания", $"Неизвестная проверка: {type}", "Ок");
                return;
            }

            var ok = checker.TryExecute(args, out var msg);
            if (ValidationSettings.Instance.EnableDialogs)
            {
                if (ok && ValidationSettings.Instance.ShowSuccessDialog ||
                    !ok && ValidationSettings.Instance.ShowFailureDialog)
                {
                    var mark = ok ? NeoCource.Editor.Util.AlgoNeoEditorUtils.OkMarkColored() : NeoCource.Editor.Util.AlgoNeoEditorUtils.FailMarkColored();
                    EditorUtility.DisplayDialog("Проверка задания", mark + " " + (msg ?? string.Empty), "Ок");
                }
            }
            else
            {
                var prefix = "[AlgoNeoCourse] ";
                string isOk = prefix + (ok ? "OK" : "FAIL");
                string line = $" {type}:";

                var settings = CourseSettings.instance;
                var coloredBody = ColorizeChecks(msg ?? string.Empty, ok, settings);
                if (ok)
                    UnityEngine.Debug.Log(isOk.Color(settings.okLogColor) + line + "\n" + coloredBody);
                else
                    UnityEngine.Debug.Log(isOk.Color(settings.failLogColor) + line + "\n" + coloredBody);
            }
        }

        private static string ColorizeChecks(string msg, bool overallOk, CourseSettings settings)
        {
            if (string.IsNullOrEmpty(msg)) return string.Empty;
            var okColor = settings.okLogColor;
            var failColor = settings.failLogColor;
            var sb = new StringBuilder();
            var lines = msg.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                var l = line ?? string.Empty;
                var trimmed = l.TrimStart();
                if (trimmed.StartsWith("V ") || trimmed == "V")
                {
                    int idx = l.IndexOf('V');
                    if (idx >= 0) sb.AppendLine(l.Substring(0, idx) + "V".Color(okColor) + l.Substring(idx + 1));
                    else sb.AppendLine(l);
                }
                else if (trimmed.StartsWith("X ") || trimmed == "X")
                {
                    int idx = l.IndexOf('X');
                    if (idx >= 0) sb.AppendLine(l.Substring(0, idx) + "X".Color(failColor) + l.Substring(idx + 1));
                    else sb.AppendLine(l);
                }
                else sb.AppendLine(l);
            }

            return sb.ToString().TrimEnd();
        }

        public static (bool ok, string message) EvaluateYamlBlock(string raw)
        {
            // Минималистичный парсер: поддержка типов scene и script
            if (string.IsNullOrEmpty(raw)) return (false, "Пустой блок check");

            string[] lines = raw.Split('\n');
            var messages = new StringBuilder();

            // Определим тип блока
            string blockType = lines.Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("type:", System.StringComparison.OrdinalIgnoreCase))
                ?.Split(':')[1].Trim().Trim('"');

            bool hasScriptRules = lines.Any(l => l.TrimStart().StartsWith("filename:") || l.TrimStart().StartsWith("- filename:"))
                                  || lines.Any(l => l.TrimStart().StartsWith("contains:") || l.TrimStart().StartsWith("- contains:"));
            bool hasSceneRules = lines.Any(l => l.TrimStart().StartsWith("- object_exists:"))
                                 || lines.Any(l => l.TrimStart().StartsWith("- component_exists"));

            if (!string.IsNullOrEmpty(blockType))
            {
                blockType = blockType.ToLowerInvariant();
                if (blockType == "script") { hasSceneRules = false; hasScriptRules = true; }
                else if (blockType == "scene") { hasScriptRules = false; hasSceneRules = true; }
            }

            bool overallOk = true;

            // Если есть scene-правила — обработаем первыми
            if (hasSceneRules)
            {
                bool sceneOk = true;
                int objectsTotal = 0, objectsOk = 0;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("- object_exists:"))
                    {
                        var name = trimmed.Split(':')[1].Trim().Trim('"');
                        var go = GameObject.Find(name);
                        bool ok = go != null;
                        sceneOk &= ok;
                        objectsTotal++;
                        if (ok) objectsOk++;
                        messages.AppendLine((ok ? "V" : "X") + $" object_exists: {name}");
                    }
                }
                if (objectsTotal > 0)
                    messages.AppendLine($"Итого (объекты): {objectsOk}/{objectsTotal}");

                // Соберём все пары component_exists
                var componentPairs = new List<(string obj, string type)>();
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (t.StartsWith("- component_exists"))
                    {
                        string on = null, ct = null;
                        int j = i + 1;
                        for (; j < lines.Length; j++)
                        {
                            var tj = lines[j].Trim();
                            if (tj.StartsWith("- ") || string.IsNullOrWhiteSpace(tj)) break;
                            if (tj.StartsWith("object:")) on = tj.Split(':')[1].Trim().Trim('"');
                            if (tj.StartsWith("type:")) ct = tj.Split(':')[1].Trim().Trim('"');
                        }
                        if (!string.IsNullOrEmpty(on) && !string.IsNullOrEmpty(ct))
                            componentPairs.Add((on, ct));
                    }
                }

                int compsTotal = 0, compsOk = 0;
                foreach (var (objName, compType) in componentPairs)
                {
                    var go = GameObject.Find(objName);
                    var type = System.Type.GetType(compType)
                               ?? System.Type.GetType("UnityEngine." + compType + ", UnityEngine")
                               ?? System.Type.GetType(compType + ", Assembly-CSharp");
                    bool ok = go != null && type != null && go.GetComponent(type) != null;
                    sceneOk &= ok;
                    compsTotal++;
                    if (ok) compsOk++;
                    messages.AppendLine((ok ? "V" : "X") + $" component_exists: {objName}.{compType}");
                }
                if (compsTotal > 0)
                    messages.AppendLine($"Итого (компоненты): {compsOk}/{compsTotal}");

                overallOk &= sceneOk;
                if (hasScriptRules) messages.AppendLine("");
            }

            // Далее — script-правила (можно совместно со сценой)
            if (hasScriptRules)
            {
                // rules:
                // - filename: "PlayerController.cs"
                // - contains: "public class PlayerController"
                // - contains: "Update"
                // - contains: "transform.Translate"
                string filename = lines.Select(l => l.Trim())
                    .Where(l => l.StartsWith("- filename:") || l.StartsWith("filename:"))
                    .Select(l => l.Split(':')[1].Trim().Trim('"'))
                    .FirstOrDefault();

                var containsTerms = lines.Select(l => l.Trim())
                    .Where(l => l.StartsWith("- contains:") || l.StartsWith("contains:"))
                    .Select(l => l.Split(':')[1].Trim().Trim('"'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (string.IsNullOrEmpty(filename)) return (false, "script: не задан filename");

                // Поиск файла в проекте
                string fileOnly = Path.GetFileName(filename);
                string nameNoExt = Path.GetFileNameWithoutExtension(fileOnly);
                string foundPath = null;

                // 1) точное совпадение имени файла среди скриптов
                var guids = AssetDatabase.FindAssets(nameNoExt + " t:MonoScript");
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetFileName(p), fileOnly, System.StringComparison.OrdinalIgnoreCase))
                    {
                        foundPath = p;
                        break;
                    }
                }

                // 2) fallback: любое совпадение имени файла среди всех ассетов
                if (string.IsNullOrEmpty(foundPath))
                {
                    guids = AssetDatabase.FindAssets(nameNoExt);
                    foreach (var guid in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.Equals(Path.GetFileName(p), fileOnly, System.StringComparison.OrdinalIgnoreCase))
                        {
                            foundPath = p;
                            break;
                        }
                    }
                }

                // 3) fallback: прямой поиск по файловой системе внутри Assets
                if (string.IsNullOrEmpty(foundPath))
                {
                    try
                    {
                        var matches = Directory.GetFiles("Assets", fileOnly, SearchOption.AllDirectories);
                        foundPath = matches.FirstOrDefault();
                    }
                    catch
                    {
                        /* ignore */
                    }
                }

                if (string.IsNullOrEmpty(foundPath))
                {
                    messages.AppendLine("❌ script: файл не найден: " + filename);
                    return (false, messages.ToString());
                }

                // Конвертация в абсолютный путь от корня проекта
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string fullPath =
                    Path.GetFullPath(Path.Combine(projectRoot, foundPath.Replace('/', Path.DirectorySeparatorChar)));
                string text = string.Empty;
                try
                {
                    text = File.ReadAllText(fullPath, Encoding.UTF8);
                }
                catch
                {
                    text = File.ReadAllText(fullPath);
                }

                bool scriptOk = true;
                messages.AppendLine($"script: найден файл {filename} ({foundPath})");
                messages.AppendLine("Проверки:");
                var missing = new List<string>();
                foreach (var term in containsTerms)
                {
                    bool ok = text.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    scriptOk &= ok;
                    messages.AppendLine((ok ? "V" : "X") + " contains: \"" + term + "\"");
                    if (!ok) missing.Add(term);
                }

                messages.AppendLine($"Итого: {containsTerms.Count - missing.Count}/{containsTerms.Count} совпадений");
                if (missing.Count > 0)
                {
                    messages.AppendLine("Отсутствуют:"); 
                    foreach (var m in missing) messages.AppendLine(" - " + m);
                }

                overallOk &= scriptOk;
            }

            return (overallOk, messages.ToString());
        }
    }
}