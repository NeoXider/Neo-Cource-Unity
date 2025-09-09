using UnityEditor;
using UnityEngine.UIElements;

namespace NeoCource.Editor.UI
{
    internal static class CheckResultPresenter
    {
        private static VisualElement _currentResultElement;

        public static void Show(VisualElement anchor, string message)
        {
            // Удаляем предыдущий результат, если он был
            if (_currentResultElement != null && _currentResultElement.parent != null)
            {
                _currentResultElement.RemoveFromHierarchy();
            }

            if (anchor == null || anchor.parent == null)
            {
                return; // Некуда добавлять результат
            }

            // Создаем новый элемент для текста результата
            var resultLabel = new Label(message)
            {
                enableRichText = true // Включаем поддержку <color> тегов
            };
            resultLabel.style.marginTop = 5;
            resultLabel.style.paddingLeft = 10;
            resultLabel.style.paddingRight = 10;
            resultLabel.style.paddingTop = 5;
            resultLabel.style.paddingBottom = 5;
            resultLabel.style.borderLeftWidth = 2;
            resultLabel.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f));
            resultLabel.name = "check-result-label"; // Для возможной стилизации через USS

            // Вставляем результат сразу после кнопки
            var parent = anchor.parent;
            int anchorIndex = parent.IndexOf(anchor);
            parent.Insert(anchorIndex + 1, resultLabel);

            _currentResultElement = resultLabel;
        }
    }
}
