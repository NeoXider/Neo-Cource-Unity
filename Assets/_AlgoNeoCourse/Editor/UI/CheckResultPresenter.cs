using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace NeoCource.Editor.UI
{
    internal static class CheckResultPresenter
    {
        // Один результат на корень визуального дерева — разные окна не затирают друг друга.
        private static readonly Dictionary<VisualElement, Label> _resultByRoot = new();

        public static void Show(VisualElement anchor, string message)
        {
            ShowInternal(anchor, message, null);
        }

        // Перегрузка с явным статусом: успех — анимация появления, неуспех — «толчок».
        public static void Show(VisualElement anchor, string message, bool success)
        {
            ShowInternal(anchor, message, success);
        }

        private static void ShowInternal(VisualElement anchor, string message, bool? success)
        {
            if (anchor == null)
            {
                return;
            }

            // Ключ — самый верхний родитель (корень окна), чтобы окна не затирали друг друга.
            VisualElement root = anchor;
            while (root.parent != null)
            {
                root = root.parent;
            }

            // Удаляем предыдущий результат только в том же корне.
            if (_resultByRoot.TryGetValue(root, out Label prev))
            {
                if (prev != null && prev.parent != null)
                {
                    prev.RemoveFromHierarchy();
                }

                _resultByRoot.Remove(root);
            }

            if (anchor.parent == null)
            {
                return;
            }

            Label resultLabel = new(message)
            {
                enableRichText = true
            };
            resultLabel.name = "check-result-label";
            resultLabel.AddToClassList("check-result-label");

            VisualElement line = anchor.parent;
            VisualElement container = line?.parent;

            if (container != null)
            {
                int lineIndex = container.IndexOf(line);
                container.Insert(lineIndex + 1, resultLabel);
            }
            else
            {
                VisualElement parent = anchor.parent;
                parent.Insert(parent.IndexOf(anchor) + 1, resultLabel);
            }

            _resultByRoot[root] = resultLabel;

            // Сама кнопка «Проверить» красится в зелёный/красный по итогу.
            if (success.HasValue && anchor is Button checkButton)
            {
                checkButton.RemoveFromClassList("check-button--success");
                checkButton.RemoveFromClassList("check-button--fail");
                checkButton.AddToClassList(success.Value
                    ? "check-button--success"
                    : "check-button--fail");
            }

            // Анимация появления через transition opacity из USS: стартуем с 0, кадр спустя — показываем.
            // Без загруженного USS инлайн-прозрачность всё равно доводит элемент до видимого состояния.
            resultLabel.style.opacity = 0;
            resultLabel.schedule.Execute(() =>
            {
                resultLabel.AddToClassList("check-result--show");
                resultLabel.style.opacity = 1;
            }).StartingIn(30);

            // При неуспехе — класс «толчка», снимаем через расписание.
            if (success == false)
            {
                resultLabel.AddToClassList("check-result--fail-shake");
                resultLabel.schedule.Execute(() => resultLabel.RemoveFromClassList("check-result--fail-shake"))
                    .StartingIn(500);
            }
        }
    }
}
