using System;
using System.Collections.Generic;
using UnityEngine;

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

            var go = GameObject.Find(targetName);
            if (go == null) { message = $"Не найден объект {targetName}"; return false; }

            var type = Type.GetType(componentName) ?? Type.GetType("UnityEngine." + componentName + ", UnityEngine") ??
                       Type.GetType(componentName + ", Assembly-CSharp");
            if (type == null) { message = $"Не найден тип компонента {componentName}"; return false; }

            var has = go.GetComponent(type) != null;
            message = has ? "Компонент найден" : $"Нет компонента {componentName} на {targetName}";
            return has;
        }
    }
}


