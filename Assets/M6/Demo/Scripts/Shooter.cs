using UnityEngine;

namespace Lessons.M6.Y4
{
    public class Shooter : MonoBehaviour
    {
        public GameObject bulletPrefab;
        public Transform shootPoint;
        public float shootInterval = 0.25f;

        // Вызывается один раз при появлении объекта
        void Start()
        {
            // Расписание: Unity сама вызывает Shoot каждые shootInterval секунд
            InvokeRepeating(nameof(Shoot), shootInterval, shootInterval);
        }

        // Создаёт пулю в точке выстрела. virtual: наследник может стрелять по-своему
        protected virtual void Shoot()
        {
            Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
        }
    }
}
