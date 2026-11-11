using UnityEngine;

namespace Lessons.M6.Y4
{
    public class MovingObject : MonoBehaviour
    {
        public Vector3 direction = Vector3.down;
        public float speed = 3f;
        public float limitY = 6f;

        // Вызывается каждый кадр
        void Update()
        {
            Move();

            // Улетел за верхний или нижний край — убираем
            if (transform.position.y > limitY || transform.position.y < -limitY)
            {
                Destroy(gameObject);
            }
        }

        // Полёт по прямой. virtual: наследник может дополнить движение
        protected virtual void Move()
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
