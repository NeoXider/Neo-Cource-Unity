using UnityEngine;

namespace Lessons.M6.Y4
{
    public class PlayerMovement : MonoBehaviour
    {
        // Вызывается каждый кадр
        void Update()
        {
            // Палец на экране телефона — это зажатая левая кнопка мыши
            if (Input.GetMouseButton(0))
            {
                // Корабль встаёт туда, где сейчас палец
                Vector3 target = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                target.z = 0f;
                transform.position = target;
            }
        }
    }
}
