using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NeoCource.Editor.Utils;

// EditMode-тесты проверяют движок правил напрямую, без рефлексии.
[assembly: InternalsVisibleTo("AlgoNeoCourse.Editor.Tests")]

namespace NeoCource.Editor
{
    internal static partial class AlgoNeoYamlChecker
    {
        // Вердикт блока check и текст отчёта (консоль / диалог). Строится из EvaluateRules —
        // тех же результатов, что показывает карточка задания.
        public static (bool ok, string message) Evaluate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (false, AlgoNeoEditorUtils.FailMarkColored() +
                               " Пустой блок check — проверять нечего. Доступные правила: " + KnownRulesList);
            }

            return Summarize(EvaluateRules(raw));
        }

        // Блок пройден, только если в нём есть хотя бы одно правило и ВСЕ правила выполнены.
        public static bool IsPassed(List<CheckRuleResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return false;
            }

            foreach (CheckRuleResult result in results)
            {
                if (result == null || !result.ok)
                {
                    return false;
                }
            }

            return true;
        }

        public static (bool ok, string message) Summarize(List<CheckRuleResult> results)
        {
            bool ok = IsPassed(results);
            StringBuilder messages = new();
            int passed = 0;
            int total = 0;

            if (results != null)
            {
                foreach (CheckRuleResult result in results)
                {
                    if (result?.rule == null)
                    {
                        continue;
                    }

                    total++;
                    if (result.ok)
                    {
                        passed++;
                    }

                    messages.Append(result.ok
                        ? AlgoNeoEditorUtils.OkMarkColored()
                        : AlgoNeoEditorUtils.FailMarkColored());
                    messages.Append(' ').Append(result.rule.ToLogLine());
                    if (result.ok && !string.IsNullOrEmpty(result.info))
                    {
                        messages.Append(" (").Append(result.info).Append(')');
                    }
                    else if (!result.ok && !string.IsNullOrEmpty(result.hint))
                    {
                        messages.Append(" (ОШИБКА: ").Append(result.hint).Append(')');
                    }

                    messages.AppendLine();
                }
            }

            messages.Append($"Итого: {passed}/{total}");
            return (ok, messages.ToString());
        }
    }
}
