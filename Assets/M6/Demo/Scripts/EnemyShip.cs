using UnityEngine;

namespace Lessons.M6.Y4
{
    public class EnemyShip : Enemy
    {
        public float swaySpeed = 1f;
        public float swayFrequency = 1.3f;

        // Дополняем движение родителя качанием влево-вправо
        protected override void Move()
        {
            // Сначала всё, что умеет родитель: летим по прямой
            base.Move();

            // Потом своё: синус толкает корабль то вправо, то влево
            float side = Mathf.Sin(transform.position.y * swayFrequency);
            transform.position += Vector3.right * side * swaySpeed * Time.deltaTime;
        }
    }
}
