using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NeoCource.Editor.UI
{
    // Карточка практического задания: вместо сырого YAML — список того, что проверит Unity,
    // с отметкой по каждому пункту после нажатия «Проверить».
    // Пункты бывают обязательные и дополнительные (optional: true). Итог: «есть ошибки» (не выполнен
    // обязательный пункт), «засчитано · есть замечания» (не выполнены только дополнительные),
    // «идеально» (выполнено всё — карточка коротко блестит).
    internal sealed class CheckCard : VisualElement
    {
        private const string NeutralIcon = "○";
        private const string OkIcon = "✓";
        private const string FailIcon = "✕";
        private const string RemarkIcon = "!";

        private readonly List<Row> rows = new();
        private readonly Label status;
        private readonly Button button;
        private VisualElement shine;
        private readonly List<IVisualElementScheduledItem> scheduledItems = new();

        public string Raw { get; }
        public string Link { get; }

        // Последний показанный итог (для тестов и перерисовки).
        public CheckVerdict? Verdict { get; private set; }

        public CheckCard(string raw, string link, Button checkButton, CheckVerdict? savedVerdict)
        {
            Raw = raw;
            Link = link;
            button = checkButton;
            AddToClassList("check-card");
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == this)
                {
                    CancelAnimations();
                }
            });

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

                if (rule.optional)
                {
                    line.AddToClassList("check-rule--optional");
                    Label tag = new("дополнительно");
                    tag.AddToClassList("check-rule__tag");
                    line.Add(tag);
                }

                Add(line);
                rows.Add(new Row { line = line, icon = icon, hint = hint, optional = rule.optional });
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

            switch (savedVerdict)
            {
                case CheckVerdict.Perfect:
                    ApplyVerdictClasses(CheckVerdict.Perfect);
                    SetStatus("идеально", "check-card__status--perfect");
                    break;
                case CheckVerdict.PassedWithRemarks:
                    ApplyVerdictClasses(CheckVerdict.PassedWithRemarks);
                    SetStatus("засчитано · есть замечания", "check-card__status--remarks");
                    break;
                case CheckVerdict.Failed:
                    SetStatus("есть ошибки — проверьте ещё раз", "check-card__status--fail");
                    break;
                default:
                    SetStatus("не проверено", null);
                    break;
            }

            Verdict = savedVerdict;
        }

        // Показывает результат по каждому пункту; строки отмечаются по очереди.
        public void ShowResults(List<CheckRuleResult> results)
        {
            CancelAnimations();
            CheckVerdict verdict = AlgoNeoYamlChecker.GetVerdict(results);
            Verdict = verdict;

            int mandatoryTotal = 0;
            int mandatoryPassed = 0;
            int remarks = 0;
            int count = results?.Count ?? 0;
            for (int i = 0; i < rows.Count && i < count; i++)
            {
                Row row = rows[i];
                CheckRuleResult result = results[i];
                // Ошибка в самом правиле — провал даже у дополнительного пункта.
                bool remark = !result.ok && row.optional && string.IsNullOrEmpty(result.rule?.problem);
                if (!row.optional)
                {
                    mandatoryTotal++;
                    mandatoryPassed += result.ok ? 1 : 0;
                }
                else if (remark)
                {
                    remarks++;
                }

                row.line.RemoveFromClassList("check-rule--ok");
                row.line.RemoveFromClassList("check-rule--fail");
                row.line.RemoveFromClassList("check-rule--remark");
                row.icon.text = NeutralIcon;
                row.hint.style.display = DisplayStyle.None;

                // Поочерёдное появление: 70 мс на пункт.
                scheduledItems.Add(row.line.schedule.Execute(() =>
                {
                    row.icon.text = result.ok ? OkIcon : remark ? RemarkIcon : FailIcon;
                    row.line.AddToClassList(result.ok ? "check-rule--ok" :
                        remark ? "check-rule--remark" : "check-rule--fail");
                    if (!result.ok && !string.IsNullOrEmpty(result.hint))
                    {
                        row.hint.text = result.hint;
                        row.hint.style.display = DisplayStyle.Flex;
                    }
                }).StartingIn(40 + i * 70));
            }

            ApplyVerdictClasses(verdict);
            switch (verdict)
            {
                case CheckVerdict.Perfect:
                    SetStatus(count > 0 ? $"идеально · {count}/{count}" : "идеально", "check-card__status--perfect");
                    PlayShine(40 + rows.Count * 70);
                    break;
                case CheckVerdict.PassedWithRemarks:
                    SetStatus(remarks == 1 ? "засчитано · 1 замечание" : $"засчитано · замечаний: {remarks}",
                        "check-card__status--remarks");
                    break;
                default:
                    SetStatus(mandatoryTotal > 0 ? $"готово {mandatoryPassed} из {mandatoryTotal}" : "есть ошибки",
                        "check-card__status--fail");
                    PlayShake(40 + rows.Count * 70);
                    break;
            }

            if (button != null)
            {
                button.RemoveFromClassList("check-button--success");
                button.RemoveFromClassList("check-button--fail");
                button.RemoveFromClassList("check-button--remarks");
                switch (verdict)
                {
                    case CheckVerdict.Perfect:
                        button.AddToClassList("check-button--success");
                        button.text = "Проверено";
                        break;
                    case CheckVerdict.PassedWithRemarks:
                        // Задание засчитано: кнопка как у выполненного; исправить и перепроверить можно всегда.
                        button.AddToClassList("check-button--remarks");
                        button.text = "Проверено";
                        break;
                    default:
                        button.AddToClassList("check-button--fail");
                        button.text = "Проверить ещё раз";
                        break;
                }
            }

            // Короткий «поп» статуса: класс снимается, transition из USS возвращает размер.
            status.AddToClassList("check-card__status--pop");
            scheduledItems.Add(status.schedule.Execute(() => status.RemoveFromClassList("check-card__status--pop"))
                .StartingIn(220));
        }

        private void CancelAnimations()
        {
            foreach (IVisualElementScheduledItem item in scheduledItems)
            {
                item.Pause();
            }

            scheduledItems.Clear();
            style.translate = StyleKeyword.Null;
            status.RemoveFromClassList("check-card__status--pop");
            shine?.RemoveFromHierarchy();
            shine = null;
        }

        private void ApplyVerdictClasses(CheckVerdict verdict)
        {
            EnableInClassList("check-card--done", verdict != CheckVerdict.Failed);
            EnableInClassList("check-card--perfect", verdict == CheckVerdict.Perfect);
            EnableInClassList("check-card--remarks", verdict == CheckVerdict.PassedWithRemarks);
            if (button != null && verdict == CheckVerdict.Perfect)
            {
                button.AddToClassList("check-button--success");
            }
            else if (button != null && verdict == CheckVerdict.PassedWithRemarks)
            {
                button.AddToClassList("check-button--remarks");
            }
        }

        // Ошибка: карточка коротко вздрагивает влево-вправо (затухающие 6 → 3 px), когда все пункты
        // уже отмечены. Сдвиг крошечный и короткий; горизонтальной прокрутки у слайда нет.
        private static readonly float[] ShakeOffsets = { -6f, 6f, -4f, 3f, -1.5f, 0f };
        private const long ShakeStepMs = 55;

        private void PlayShake(long delayMs)
        {
            for (int i = 0; i < ShakeOffsets.Length; i++)
            {
                float offset = ShakeOffsets[i];
                scheduledItems.Add(schedule.Execute(() => style.translate = new StyleTranslate(new Translate(offset, 0f)))
                    .StartingIn(delayMs + i * ShakeStepMs));
            }
        }

        // Блик «идеально»: светлая полоса проходит по карточке слева направо и гаснет.
        // Полоса всё время внутри карточки (left 0%..85%, ширина 15%), карточка её обрезает —
        // границы содержимого слайда не меняются, горизонтальной прокрутки не появляется.
        private void PlayShine(long delayMs)
        {
            shine?.RemoveFromHierarchy();
            shine = new VisualElement { pickingMode = PickingMode.Ignore };
            shine.AddToClassList("check-card__shine");
            Insert(0, shine);

            VisualElement current = shine;
            scheduledItems.Add(current.schedule.Execute(() => current.AddToClassList("check-card__shine--run"))
                .StartingIn(delayMs));
            scheduledItems.Add(current.schedule.Execute(() => current.AddToClassList("check-card__shine--fade"))
                .StartingIn(delayMs + 650));
            scheduledItems.Add(current.schedule.Execute(() =>
            {
                if (shine == current)
                {
                    shine = null;
                }

                current.RemoveFromHierarchy();
            }).StartingIn(delayMs + 1100));
        }

        private void SetStatus(string text, string modifierClass)
        {
            status.text = text;
            status.RemoveFromClassList("check-card__status--ok");
            status.RemoveFromClassList("check-card__status--fail");
            status.RemoveFromClassList("check-card__status--perfect");
            status.RemoveFromClassList("check-card__status--remarks");
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
            public bool optional;
        }
    }
}
