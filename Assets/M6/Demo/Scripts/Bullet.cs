using UnityEngine;

namespace Lessons.M6.Y4
{
    public class Bullet : MovingObject
    {
        public int damage = 1;

        // Вызывается, когда пуля коснулась другого коллайдера
        void OnTriggerEnter2D(Collider2D other)
        {
            // Пуле всё равно, камень это или корабль. Главное — враг
            Enemy enemy = other.GetComponent<Enemy>();

            // Это не враг — летим дальше
            if (enemy == null)
            {
                return;
            }

            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
