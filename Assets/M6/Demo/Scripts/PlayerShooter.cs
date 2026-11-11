using UnityEngine;

namespace Lessons.M6.Y4
{
    public class PlayerShooter : Shooter
    {
        public int maxBullets = 5;
        public float bulletSpacing = 0.25f;

        private int bulletCount = 1;

        // Бонус: ещё одна пуля в залпе, но не больше maxBullets
        public void AddBullet()
        {
            if (bulletCount < maxBullets)
            {
                bulletCount += 1;
            }
        }

        // Вместо одной пули выпускает ряд из bulletCount пуль
        protected override void Shoot()
        {
            // Левый край ряда: так ряд стоит ровно по центру точки выстрела
            float startX = -(bulletCount - 1) * bulletSpacing / 2f;

            for (int i = 0; i < bulletCount; i++)
            {
                Vector3 offset = Vector3.right * (startX + i * bulletSpacing);
                Instantiate(bulletPrefab, shootPoint.position + offset, Quaternion.identity);
            }
        }
    }
}
