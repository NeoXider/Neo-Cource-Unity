using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NeoCource.Editor.UI
{
    // Карточка практического задания: вместо сырого YAML — список того, что проверит Unity,
    // с отметкой по каждому пункту после нажатия «Проверить».
    internal sealed class CheckCard : VisualElement
    {
        private const string NeutralIcon = "○";
        private const string OkIcon = "✓";
        private const string FailIcon = "✕";

        private readonly List<Row> rows = new();
        private readonly Label status;
        private readonly Button button;

        public string Raw { get; }
        public string Link { get; }

        public CheckCard(string raw, string link, Button checkButton, bool? savedResult)
        {
            Raw = raw;
            Link = link;
            button = checkButton;
            AddToClassList("check-card");

            VisualElement header = new();
            header.AddToClassList("check-card__header");
            Label title = new("Практика · что проверит Unity");
            title.AddToClassList("check-card__title");
            header.Add(title);
            status = new Label();
            status.AddToClassList("check-card__status");
            header.Add(status);
            Add(header);

            foreach (CheckRule rule in AlgoNeoYamlChecker.ParseRules(raw))
            {
                VisualElement line = new();
                line.AddToClassList("check-rule");

                Label icon = new(NeutralIcon);
                icon.AddToClassList("check-rule__icon");
                line.Add(icon);

                VisualElement texts = new();
                texts.AddToClassList("check-rule__texts");
                Label text = new(rule.Describe()) { enableRichText = true };
                text.AddToClassList("check-rule__text");
                texts.Add(text);
                Label hint = new();
                hint.AddToClassList("check-rule__hint");
                hint.style.display = DisplayStyle.None;
                texts.Add(hint);
                line.Add(texts);

                Add(line);
                rows.Add(new Row { line = line, icon = icon, hint = hint });
            }

            if (button != null)
            {
                VisualElement footer = new();
                footer.AddToClassList("check-card__footer");
                button.RemoveFromHierarchy();
                button.text = "Проверить";
                footer.Add(button);
                Add(footer);
            }

            if (savedResult == true)
            {
                SetStatus("выполнено", "check-card__status--ok");
                AddToClassList("check-card--done");
                button?.AddToClassList("check-button--success");
            }
            else if (savedResult == false)
            {
                SetStatus("есть ошибки — проверьте ещё раз", "check-card__status--fail");
            }
            else
            {
                SetStatus("не проверено", null);
            }
        }

        // Показывает результат по каждому пункту; строки отмечаются по очереди.
        public void ShowResults(List<CheckRuleResult> results, bool overallOk)
        {
            int passed = 0;
            for (int i = 0; i < rows.Count && i < results.Count; i++)
            {
                Row row = rows[i];
                CheckRuleResult result = results[i];
                if (result.ok)
                {
                    passed++;
                }

                row.line.RemoveFromClassList("check-rule--ok");
                row.line.RemoveFromClassList("check-rule--fail");
                row.icon.text = NeutralIcon;
                row.hint.style.display = DisplayStyle.None;

                // Поочерёдное появление: 70 мс на пункт.
                row.line.schedule.Execute(() =>
                {
                    row.icon.text = result.ok ? OkIcon : FailIcon;
                    row.line.AddToClassList(result.ok ? "check-rule--ok" : "check-rule--fail");
                    if (!result.ok && !string.IsNullOrEmpty(result.hint))
                    {
                        row.hint.text = result.hint;
                        row.hint.style.display = DisplayStyle.Flex;
                    }
                }).StartingIn(40 + i * 70);
            }

            int total = rows.Count;
            EnableInClassList("check-card--done", overallOk);
            if (overallOk)
            {
                SetStatus(total > 0 ? $"всё верно · {passed}/{total}" : "всё верно", "check-card__status--ok");
            }
            else
            {
                SetStatus(total > 0 ? $"готово {passed} из {total}" : "есть ошибки", "check-card__status--fail");
            }

            if (button != null)
            {
                button.RemoveFromClassList("check-button--success");
                button.RemoveFromClassList("check-button--fail");
                button.AddToClassList(overallOk ? "check-button--success" : "check-button--fail");
                button.text = overallOk ? "Проверено" : "Проверить ещё раз";
            }

            // Короткий «поп» статуса: класс снимается, transition из USS возвращает размер.
            status.AddToClassList("check-card__status--pop");
            status.schedule.Execute(() => status.RemoveFromClassList("check-card__status--pop")).StartingIn(220);
        }

        private void SetStatus(string text, string modifierClass)
        {
            status.text = text;
            status.RemoveFromClassList("check-card__status--ok");
            status.RemoveFromClassList("check-card__status--fail");
            if (modifierClass != null)
            {
                status.AddToClassList(modifierClass);
            }
        }

        private sealed class Row
        {
            public VisualElement line;
            public Label icon;
            public Label hint;
        }
    }
}
