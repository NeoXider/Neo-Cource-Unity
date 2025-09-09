using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NeoCource.Editor.Settings;
using NeoCource.Editor.Tasks;
using NeoCource.Editor.Utils;

namespace NeoCource.Editor
{
    internal static class AlgoNeoTaskChecker
    {
        public static string Execute(Dictionary<string, string> args)
        {
            if (args == null || !args.TryGetValue("type", out var type) || string.IsNullOrEmpty(type))
            {
                EditorUtility.DisplayDialog("Проверка задания", "Параметры проверки не заданы", "Ок");
                return null;
            }

            if (!ValidationSettings.Instance.IsCheckEnabled(type))
            {
                if (ValidationSettings.Instance.LogVerbose)
                    Debug.Log($"[AlgoNeoCourse] Проверка '{type}' отключена настройками");
                return null;
            }

            (bool success, string message, string title) result;

            if (type == "from-block" && args != null && args.TryGetValue("__raw_block__", out var raw))
            {
                var (blockOk, blockMsg) = AlgoNeoYamlChecker.Evaluate(raw);
                result = (blockOk, blockMsg, "Проверка блока");
            }
            else if (TaskCheckRegistry.TryGet(type, out var checker))
            {
                var ok = checker.TryExecute(args, out var msg);
                result = (ok, msg, $"Проверка задания: {type}");
            }
            else
            {
                EditorUtility.DisplayDialog("Проверка задания", $"Неизвестная проверка: {type}", "Ок");
                return null;
            }

            DisplayResult(result.title, result.message, result.success, args);
            
            return result.message;
        }

        private static void DisplayResult(string title, string message, bool success, IReadOnlyDictionary<string, string> args)
        {
            args.TryGetValue("dialog", out var dialogMode);
            bool useDialogs = ValidationSettings.Instance.EnableDialogs;
            if (!string.IsNullOrEmpty(dialogMode))
            {
                if (dialogMode.Equals("console", System.StringComparison.OrdinalIgnoreCase)) useDialogs = false;
                else if (dialogMode.Equals("dialog", System.StringComparison.OrdinalIgnoreCase)) useDialogs = true;
            }

            if (useDialogs)
            {
                if ((success && !ValidationSettings.Instance.ShowSuccessDialog) || 
                    (!success && !ValidationSettings.Instance.ShowFailureDialog)) return;
                
                EditorUtility.DisplayDialog(title, message, "Ок");
            }
            else
            {
                var settings = CourseSettings.instance;
                var resultText = "[AlgoNeoCourse] " + (success ? "OK" : "FAIL");
                var coloredResult = resultText.Color(success ? settings.okLogColor : settings.failLogColor);
                
                Debug.Log(coloredResult + "\n" + (message ?? string.Empty));
            }
        }
    }
}
