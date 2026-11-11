using UnityEngine;

namespace Lessons.M6.Y4
{
    public class Bonus : MovingObject
    {
        // Вызывается, когда бонус коснулся другого коллайдера
        void OnTriggerEnter2D(Collider2D other)
        {
            PlayerShooter shooter = other.GetComponent<PlayerShooter>();

            // Бонус подбирает только корабль игрока
            if (shooter == null)
            {
                return;
            }

            shooter.AddBullet();
            Destroy(gameObject);
        }
    }
}
