using System;
using System.Collections.Generic;
using UnityEngine;
using NeoCource.Editor.Utils;

namespace NeoCource.Editor.Tasks.Builtin
{
    [TaskCheck("component-present")]
    public class ComponentPresentCheck : ITaskCheck
    {
        public string Key => "component-present";

        public bool TryExecute(Dictionary<string, string> args, out string message)
        {
            message = string.Empty;
            if (args == null) { message = "Параметры не заданы"; return false; }

            args.TryGetValue("target", out var targetName);
            args.TryGetValue("component", out var componentName);
            if (string.IsNullOrEmpty(targetName) || string.IsNullOrEmpty(componentName))
            {
                message = "Нужно указать target и component"; return false;
            }

            var lines = new List<string>();
            var go = GameObject.Find(targetName);
            bool ok = false;
            string error = "";

            if (go == null)
            {
                error = $" (ОШИБКА: игровой объект с именем '{targetName}' не найден на сцене)";
            }
            else
            {
                var t = Type.GetType(componentName)
                        ?? Type.GetType("UnityEngine." + componentName + ", UnityEngine")
                        ?? Type.GetType(componentName + ", Assembly-CSharp");
                if (t == null)
                {
                    error = $" (ОШИБКА: не найден тип компонента '{componentName}')";
                }
                else
                {
                    ok = go.GetComponent(t) != null;
                    if (!ok)
                    {
                        error = $" (ОШИБКА: компонент '{componentName}' не найден на объекте)";
                    }
                }
            }

            lines.Add((ok ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) + $" component_exists: {targetName}.{componentName}" + error);
            lines.Add($"Итого (компоненты): {(ok ? 1 : 0)}/1");

            message = string.Join("\n", lines);
            return ok;
        }
    }
}
