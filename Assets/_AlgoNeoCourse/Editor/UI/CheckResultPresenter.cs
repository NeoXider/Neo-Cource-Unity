using UnityEditor;
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

            var resultLabel = new Label(message)
            {
                enableRichText = true
            };
            resultLabel.name = "check-result-label";
            resultLabel.AddToClassList("check-result-label");

            var line = anchor.parent;
            var container = line?.parent;

            if (container != null)
            {
                int lineIndex = container.IndexOf(line);
                container.Insert(lineIndex + 1, resultLabel);
            }
            else
            {
                var parent = anchor.parent;
                parent.Insert(parent.IndexOf(anchor) + 1, resultLabel);
            }

            _currentResultElement = resultLabel;
        }
    }
}
