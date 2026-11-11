using UnityEngine;

namespace Lessons.M5.Y6
{
    public class CrossbowAim : MonoBehaviour
    {
        // Вызывается каждый кадр
        void Update()
        {
            // Где сейчас курсор мыши в игровом мире
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Направление от арбалета к курсору
            Vector2 aimDirection = mousePosition - transform.position;

            // Поворачиваем арбалет носом в эту сторону
            transform.up = aimDirection;
        }
    }
}
