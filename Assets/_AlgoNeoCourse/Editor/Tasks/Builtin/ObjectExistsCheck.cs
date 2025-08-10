using System.Collections.Generic;
using UnityEngine;

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
            message = ok ? $"Объект найден: {target}" : $"Не найден объект {target}";
            return ok;
        }
    }
}


