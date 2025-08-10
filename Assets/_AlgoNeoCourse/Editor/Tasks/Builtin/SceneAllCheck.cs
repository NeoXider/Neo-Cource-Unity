using System;
using System.Collections.Generic;
using System.Linq;
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

            args.TryGetValue("target", out var targetName);
            args.TryGetValue("components", out var componentsCsv);

            if (string.IsNullOrWhiteSpace(targetName))
            {
                message = "Нужно указать target";
                return false;
            }

            var lines = new List<string>();
            var go = GameObject.Find(targetName);
            bool overall = go != null;
            lines.Add((go != null ? ">" : "X") + $" object-exists: {targetName}");

            if (!string.IsNullOrWhiteSpace(componentsCsv))
            {
                var parts = componentsCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s))
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();

                foreach (var compName in parts)
                {
                    bool ok = false;
                    if (go != null)
                    {
                        var t = Type.GetType(compName) 
                                ?? Type.GetType("UnityEngine." + compName + ", UnityEngine")
                                ?? Type.GetType(compName + ", Assembly-CSharp");
                        ok = (t != null) && (go.GetComponent(t) != null);
                    }
                    overall &= ok;
                    lines.Add((ok ? ">" : "X") + $" component-present: {targetName}.{compName}");
                }
            }

            lines.Add("Итого: " + (overall ? "OK" : "FAIL"));
            message = string.Join("\n", lines);
            return overall;
        }
    }
}


