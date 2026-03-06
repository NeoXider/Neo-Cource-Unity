using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NeoCource.Editor.Utils;
using UnityEngine;

namespace NeoCource.Editor
{
    internal static class AlgoNeoYamlChecker
    {
        public static (bool ok, string message) Evaluate(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return (false, "Пустой блок check");
            }

            string[] lines = raw.Split('\n');
            StringBuilder messages = new();

            bool hasScriptRules = lines.Any(l =>
                                      l.TrimStart().StartsWith("filename:", StringComparison.OrdinalIgnoreCase) ||
                                      l.TrimStart().StartsWith("- filename:", StringComparison.OrdinalIgnoreCase))
                                  || lines.Any(l =>
                                      l.TrimStart().StartsWith("contains:", StringComparison.OrdinalIgnoreCase) ||
                                      l.TrimStart().StartsWith("- contains:", StringComparison.OrdinalIgnoreCase));
            bool hasSceneRules = lines.Any(l =>
                                     l.TrimStart().StartsWith("- object_exists:", StringComparison.OrdinalIgnoreCase))
                                 || lines.Any(l =>
                                     l.TrimStart().StartsWith("- component_exists",
                                         StringComparison.OrdinalIgnoreCase));

            bool overallOk = true;

            if (hasSceneRules)
            {
                (bool sceneOk, string sceneMsg) = EvaluateSceneRules(lines);
                overallOk &= sceneOk;
                messages.Append(sceneMsg);
                if (hasScriptRules)
                {
                    messages.AppendLine();
                }
            }

            if (hasScriptRules)
            {
                (bool scriptOk, string scriptMsg) = EvaluateScriptRules(lines);
                overallOk &= scriptOk;
                messages.Append(scriptMsg);
            }

            return (overallOk, messages.ToString());
        }

        private static (bool ok, string message) EvaluateSceneRules(IEnumerable<string> lines)
        {
            StringBuilder messages = new();
            bool sceneOk = true;

            int objectsTotal = 0, objectsOk = 0;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("- object_exists:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = trimmed.Split(':')[1].Trim().Trim('"');
                    GameObject go = GameObject.Find(name);
                    bool ok = go != null;
                    sceneOk &= ok;
                    objectsTotal++;
                    if (ok)
                    {
                        objectsOk++;
                    }

                    messages.AppendLine(
                        (ok ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) +
                        $" object_exists: {name}");
                }
            }

            if (objectsTotal > 0)
            {
                messages.AppendLine($"Итого (объекты): {objectsOk}/{objectsTotal}");
            }

            List<(string obj, string type)> componentPairs = new();
            string[] linesArr = lines.ToArray();
            for (int i = 0; i < linesArr.Length; i++)
            {
                string t = linesArr[i].Trim();
                if (t.StartsWith("- component_exists", StringComparison.OrdinalIgnoreCase))
                {
                    string on = null, ct = null;
                    for (int j = i + 1; j < linesArr.Length; j++)
                    {
                        string tj = linesArr[j].Trim();
                        if (tj.StartsWith("- ") || string.IsNullOrWhiteSpace(tj))
                        {
                            break;
                        }

                        if (tj.StartsWith("object:", StringComparison.OrdinalIgnoreCase))
                        {
                            on = tj.Split(':')[1].Trim().Trim('"');
                        }

                        if (tj.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                        {
                            ct = tj.Split(':')[1].Trim().Trim('"');
                        }
                    }

                    if (!string.IsNullOrEmpty(on) && !string.IsNullOrEmpty(ct))
                    {
                        componentPairs.Add((on, ct));
                    }
                }
            }

            int compsTotal = 0, compsOk = 0;
            foreach ((string objName, string compType) in componentPairs)
            {
                compsTotal++;
                bool ok = false;
                GameObject go = GameObject.Find(objName);
                if (go == null)
                {
                    messages.AppendLine(
                        $"{AlgoNeoEditorUtils.FailMarkColored()} component_exists: {objName}.{compType} (ОШИБКА: игровой объект с именем '{objName}' не найден на сцене)");
                }
                else
                {
                    Type type = AlgoNeoEditorUtils.FindTypeByName(compType);
                    if (type == null)
                    {
                        messages.AppendLine(
                            $"{AlgoNeoEditorUtils.FailMarkColored()} component_exists: {objName}.{compType} (ОШИБКА: не удалось найти тип компонента '{compType}')");
                    }
                    else if (go.GetComponent(type) == null)
                    {
                        messages.AppendLine(
                            $"{AlgoNeoEditorUtils.FailMarkColored()} component_exists: {objName}.{compType} (ОШИБКА: на объекте '{objName}' не найден компонент типа '{type.FullName}')");
                    }
                    else
                    {
                        ok = true;
                        compsOk++;
                        messages.AppendLine(
                            $"{AlgoNeoEditorUtils.OkMarkColored()} component_exists: {objName}.{compType}");
                    }
                }

                sceneOk &= ok;
            }

            if (compsTotal > 0)
            {
                messages.AppendLine($"Итого (компоненты): {compsOk}/{compsTotal}");
            }

            return (sceneOk, messages.ToString());
        }

        private static (bool ok, string message) EvaluateScriptRules(IEnumerable<string> lines)
        {
            StringBuilder messages = new();
            bool scriptOk = true;

            string filename = lines.Select(l => l.Trim())
                .Where(l => l.StartsWith("- filename:", StringComparison.OrdinalIgnoreCase) ||
                            l.StartsWith("filename:", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Split(':')[1].Trim().Trim('"'))
                .FirstOrDefault();

            List<string> containsTerms = lines.Select(l => l.Trim())
                .Where(l => l.StartsWith("- contains:", StringComparison.OrdinalIgnoreCase) ||
                            l.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Split(':')[1].Trim().Trim('"'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (string.IsNullOrEmpty(filename))
            {
                return (false, $"{AlgoNeoEditorUtils.FailMarkColored()} script: не задан filename");
            }

            string foundPath = AlgoNeoEditorUtils.FindAssetPath(filename);
            if (string.IsNullOrEmpty(foundPath))
            {
                messages.AppendLine($"{AlgoNeoEditorUtils.FailMarkColored()} script: файл не найден: {filename}");
                return (false, messages.ToString());
            }

            string text = AlgoNeoEditorUtils.ReadAllTextWithRetries(foundPath);
            messages.AppendLine($"script: найден файл {filename} ({foundPath})");
            messages.AppendLine("Проверки:");
            List<string> missing = new();
            foreach (string term in containsTerms)
            {
                bool ok = text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
                scriptOk &= ok;
                messages.AppendLine((ok ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) +
                                    $" contains: \"{term}\"");
                if (!ok)
                {
                    missing.Add(term);
                }
            }

            messages.AppendLine($"Итого: {containsTerms.Count - missing.Count}/{containsTerms.Count} совпадений");
            if (missing.Count > 0)
            {
                messages.AppendLine("Отсутствуют:");
                foreach (string m in missing)
                {
                    messages.AppendLine(" - " + m);
                }
            }

            return (scriptOk, messages.ToString());
        }
    }
}