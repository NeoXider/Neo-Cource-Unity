using UnityEngine;

namespace Lessons.M6.Y4
{
    public class ScrollingBackground : MonoBehaviour
    {
        public float speed = 1f;
        public float loopHeight = 10f;

        // Вызывается каждый кадр
        void Update()
        {
            // Медленно тянем фон вниз
            transform.position += Vector3.down * speed * Time.deltaTime;

            // Уехал на высоту одной картинки — возвращаем наверх
            if (transform.position.y <= -loopHeight)
            {
                transform.position += Vector3.up * loopHeight;
            }
        }
    }
}
