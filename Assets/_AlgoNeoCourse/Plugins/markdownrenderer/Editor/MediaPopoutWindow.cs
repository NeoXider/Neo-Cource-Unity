using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Окно, в которое открепляется медиа-элемент (видео, картинка) из отрендеренного Markdown.
///     Публичное: потребителю из другой сборки нужны <see cref="Popout" /> и <see cref="EnsureClosed" />.
/// </summary>
public class MediaPopoutWindow : EditorWindow
{
    private VisualElement m_OriginalContainer;
    private int m_OriginalPositionIndex;

    private VisualElement m_PopOutElement;

    private void OnDestroy()
    {
        try
        {
            if (m_PopOutElement == null)
            {
                return;
            }

            m_PopOutElement.RemoveFromClassList("popout-media");
            if (m_OriginalContainer != null)
            {
                // Исходный контейнер могли перестроить, пока окно было открыто: индекс не должен выйти за границы.
                int index = Mathf.Clamp(m_OriginalPositionIndex, 0, m_OriginalContainer.childCount);
                m_OriginalContainer.Insert(index, m_PopOutElement);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MediaPopoutWindow: couldn't return the element to its container: {ex.Message}");
        }
    }

    private void OnFocus()
    {
        //needed to receive key event
        rootVisualElement.focusable = true;
        rootVisualElement.Focus();
        // Фокус приходит много раз: снимаем подписку перед новой, чтобы обработчик не множился.
        rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyPressed);
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyPressed);
    }

    private void OnLostFocus()
    {
        //remove focus from the root
        rootVisualElement.Blur();
        rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyPressed);
    }

    private void OnKeyPressed(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape)
        {
            Close();
        }
    }

    public static void Popout(VisualElement element)
    {
        if (element == null || element.parent == null)
        {
            return;
        }

        MediaPopoutWindow win = CreateInstance<MediaPopoutWindow>();
        win.ShowUtility();

        //we go back up the parent chain to make sure every stylesheet that could be relevant to that element is also
        //added to the window
        VisualElement currentElement = element;
        while (currentElement != null)
        {
            for (int i = 0; i < currentElement.styleSheets.count; ++i)
            {
                if (!win.rootVisualElement.styleSheets.Contains(currentElement.styleSheets[i]))
                {
                    win.rootVisualElement.styleSheets.Add(currentElement.styleSheets[i]);
                }
            }

            currentElement = currentElement.parent;
        }

        win.m_PopOutElement = element;

        win.m_OriginalContainer = element.parent;
        win.m_OriginalPositionIndex = win.m_OriginalContainer.IndexOf(element);

        win.rootVisualElement.Add(element);
        win.m_PopOutElement.AddToClassList("popout-media");
    }

    /// <summary>
    ///     Закрывает все открытые окна открепления. Вызывается хозяином рендера при смене страницы или слайда,
    ///     чтобы откреплённое медиа не пережило свой документ. Окно не создаёт и не фокусирует
    ///     (<c>GetWindow</c> сделал бы и то и другое): закрываются только существующие экземпляры.
    /// </summary>
    public static void EnsureClosed()
    {
        foreach (MediaPopoutWindow win in Resources.FindObjectsOfTypeAll<MediaPopoutWindow>())
        {
            if (win != null)
            {
                win.Close();
            }
        }
    }
}
