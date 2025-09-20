using System.Collections.Generic;
using UnityEngine;
using NeoCource.Editor.Utils;

namespace NeoCource.Editor.Tasks.Builtin
{
    [TaskCheck("object-exists")]
    public class ObjectExistsCheck : ITaskCheck
    {
        public string Key => "object-exists";

        public bool TryExecute(Dictionary<string, string> args, out string message)
        {
            message = string.Empty;
            if (args == null || !args.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target))
            {
                message = "Нужно указать target";
                return false;
            }

            var go = GameObject.Find(target);
            bool ok = go != null;

            var lines = new List<string>();
            lines.Add((ok ? AlgoNeoEditorUtils.OkMarkColored() : AlgoNeoEditorUtils.FailMarkColored()) + $" object_exists: {target}");
            lines.Add($"Итого (объекты): {(ok ? 1 : 0)}/1");

            message = string.Join("\n", lines);
            return ok;
        }
    }
}
