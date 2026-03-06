using UnityEngine.UIElements;

namespace NeoCource.Editor.UI
{
    internal static class CheckResultPresenter
    {
        private static VisualElement _currentResultElement;

        public static void Show(VisualElement anchor, string message)
        {
            if (_currentResultElement != null && _currentResultElement.parent != null)
            {
                _currentResultElement.RemoveFromHierarchy();
            }

            if (anchor == null || anchor.parent == null)
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

            _currentResultElement = resultLabel;
        }
    }
}