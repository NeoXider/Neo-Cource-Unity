using System;
using System.Collections.Generic;
using System.Linq;
using NeoCource.Editor.Utils;
using UnityEngine;

namespace NeoCource.Editor.Tasks.Builtin
{
    // Сложная «скрытая» проверка сцены: один вызов проверяет существование объекта и наличие набора компонентов
    // Пример ссылки:
    // unity://check?type=scene-all&target=Player&components=Rigidbody,BoxCollider,MeshRenderer
    [TaskCheck("scene-all")]
    public class SceneAllCheck : ITaskCheck
    {
        public string Key => "scene-all";

        public bool TryExecute(Dictionary<string, string> args, out string message)
        {
            message = string.Empty;
            if (args == null)
            {
                message = "Параметры не заданы";
                return false;
            }

            args.TryGetValue("target", out string targetName);
            args.TryGetValue("components", out string componentsCsv);

            if (string.IsNullOrWhiteSpace(targetName))
            {
                message = "Нужно указать target";
                return false;
            }

            List<string> lines = new();
            GameObject go = GameObject.Find(targetName);
            bool overall = go != null;
            int objectsTotal = 1;
            int objectsOk = go != null ? 1 : 0;

            lines.Add((go != null ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) +
                      $" object_exists: {targetName}");
            lines.Add($"Итого (объекты): {objectsOk}/{objectsTotal}");

            int componentsTotal = 0;
            int componentsOk = 0;

            if (!string.IsNullOrWhiteSpace(componentsCsv))
            {
                List<string> parts = componentsCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                componentsTotal = parts.Count;

                foreach (string compName in parts)
                {
                    bool ok = false;
                    string error = "";
                    if (go != null)
                    {
                        Type t = Type.GetType(compName)
                                 ?? Type.GetType("UnityEngine." + compName + ", UnityEngine")
                                 ?? Type.GetType(compName + ", Assembly-CSharp");
                        if (t == null)
                        {
                            error = $" (ОШИБКА: не найден тип компонента '{compName}')";
                        }
                        else
                        {
                            ok = go.GetComponent(t) != null;
                            if (!ok)
                            {
                                error = $" (ОШИБКА: компонент '{compName}' не найден на объекте)";
                            }
                        }
                    }
                    else
                    {
                        error = " (ОШИБКА: игровой объект с именем '" + targetName + "' не найден на сцене)";
                    }

                    if (ok)
                    {
                        componentsOk++;
                    }

                    overall &= ok;
                    lines.Add((ok ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) +
                              $" component_exists: {targetName}.{compName}" + error);
                }

                if (componentsTotal > 0)
                {
                    lines.Add($"Итого (компоненты): {componentsOk}/{componentsTotal}");
                }
            }

            message = string.Join("\n", lines);
            return overall;
        }
    }
}