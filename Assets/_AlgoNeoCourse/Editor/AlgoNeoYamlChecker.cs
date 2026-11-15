using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NeoCource.Editor.Utils;

// EditMode-тесты проверяют движок правил напрямую, без рефлексии.
[assembly: InternalsVisibleTo("AlgoNeoCourse.Editor.Tests")]

namespace NeoCource.Editor
{
    // Итог блока check: провал обязательного правила — задание не засчитано; провал только
    // дополнительных (optional) — засчитано с замечаниями; всё выполнено — идеально.
    internal enum CheckVerdict
    {
        Failed,
        PassedWithRemarks,
        Perfect
    }

    internal static partial class AlgoNeoYamlChecker
    {
        // Вердикт блока check и текст отчёта (консоль / диалог). Строится из EvaluateRules —
        // тех же результатов, что показывает карточка задания.
        public static (bool ok, string message) Evaluate(string raw)
        {
            return Evaluate(raw, out _);
        }

        public static (bool ok, string message) Evaluate(string raw, out List<CheckRuleResult> results)
        {
            results = EvaluateRules(raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (false, AlgoNeoEditorUtils.FailMarkColored() +
                               " Пустой блок check — проверять нечего. Доступные правила: " + KnownRulesList);
            }

            return Summarize(results);
        }

        // Блок пройден, если в нём есть хотя бы одно правило и выполнены ВСЕ обязательные.
        // Дополнительные (optional: true) на зачёт не влияют — только на «идеально».
        public static bool IsPassed(List<CheckRuleResult> results)
        {
            return GetVerdict(results) != CheckVerdict.Failed;
        }

        public static CheckVerdict GetVerdict(List<CheckRuleResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return CheckVerdict.Failed;
            }

            bool remarks = false;
            foreach (CheckRuleResult result in results)
            {
                if (result == null || result.rule == null)
                {
                    return CheckVerdict.Failed;
                }

                if (result.ok)
                {
                    continue;
                }

                // Ошибка в самом правиле (опечатка, нет type) — всегда провал, даже у дополнительного:
                // такую проверку нужно чинить автору урока, а не прощать.
                if (!result.rule.optional || !string.IsNullOrEmpty(result.rule.problem))
                {
                    return CheckVerdict.Failed;
                }

                remarks = true;
            }

            return remarks ? CheckVerdict.PassedWithRemarks : CheckVerdict.Perfect;
        }

        public static (bool ok, string message) Summarize(List<CheckRuleResult> results)
        {
            CheckVerdict verdict = GetVerdict(results);
            bool ok = verdict != CheckVerdict.Failed;
            StringBuilder messages = new();
            int passed = 0;
            int total = 0;
            int optionalPassed = 0;
            int optionalTotal = 0;

            if (results != null)
            {
                foreach (CheckRuleResult result in results)
                {
                    if (result?.rule == null)
                    {
                        continue;
                    }

                    bool remark = !result.ok && result.rule.optional && string.IsNullOrEmpty(result.rule.problem);
                    if (result.rule.optional)
                    {
                        optionalTotal++;
                        optionalPassed += result.ok ? 1 : 0;
                    }
                    else
                    {
                        total++;
                        passed += result.ok ? 1 : 0;
                    }

                    messages.Append(result.ok
                        ? AlgoNeoEditorUtils.OkMarkColored()
                        : remark
                            ? AlgoNeoEditorUtils.WarnMarkColored()
                            : AlgoNeoEditorUtils.FailMarkColored());
                    messages.Append(' ').Append(result.rule.ToLogLine());
                    if (result.ok && !string.IsNullOrEmpty(result.info))
                    {
                        messages.Append(" (").Append(result.info).Append(')');
                    }
                    else if (!result.ok && !string.IsNullOrEmpty(result.hint))
                    {
                        messages.Append(remark ? " (ЗАМЕЧАНИЕ: " : " (ОШИБКА: ").Append(result.hint).Append(')');
                    }

                    messages.AppendLine();
                }
            }

            messages.Append($"Итого: {passed}/{total}");
            if (optionalTotal > 0)
            {
                messages.Append($", дополнительно: {optionalPassed}/{optionalTotal}");
            }

            if (verdict == CheckVerdict.PassedWithRemarks)
            {
                messages.Append(" — засчитано, есть замечания");
            }
            else if (verdict == CheckVerdict.Perfect && optionalTotal > 0)
            {
                messages.Append(" — идеально");
            }

            return (ok, messages.ToString());
        }
    }
}
