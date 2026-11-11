using UnityEngine;

namespace Lessons.M5.Y6
{
    public class CrossbowShooter : MonoBehaviour
    {
        public GameObject boltPrefab;
        public Transform shootPoint;
        public float shootInterval = 0.5f;
        public int boltDamage = 1;

        private float timer;

        // Вызывается каждый кадр
        void Update()
        {
            timer += Time.deltaTime;

            // Арбалет стреляет сам, как только прошла пауза
            if (timer >= shootInterval)
            {
                timer = 0f;
                Shoot();
            }
        }

        // Создаёт стрелу и передаёт ей урон арбалета
        private void Shoot()
        {
            GameObject bolt = Instantiate(boltPrefab, shootPoint.position, transform.rotation);
            bolt.GetComponent<BoltProjectile>().damage = boltDamage;
        }
    }
}
