using UnityEngine;

namespace Lessons.M6.Y4
{
    public class Spawner : MonoBehaviour
    {
        public GameObject[] prefabs;
        public float spawnInterval = 1.2f;
        public float rangeX = 2f;
        public float minInterval = 0.4f;
        public float intervalStep = 0.01f;

        private float interval;

        // Вызывается один раз при старте сцены
        void Start()
        {
            interval = spawnInterval;

            // Заводим первый вызов Spawn — один раз, через interval секунд
            Invoke(nameof(Spawn), interval);
        }

        // Создаёт случайный префаб и заводит следующий вызов — чуть раньше прошлого
        private void Spawn()
        {
            int index = Random.Range(0, prefabs.Length);
            float x = Random.Range(-rangeX, rangeX);
            Vector3 position = new Vector3(x, transform.position.y, 0f);

            Instantiate(prefabs[index], position, Quaternion.identity);

            // Каждый новый враг появляется чуть быстрее прошлого
            if (interval > minInterval)
            {
                interval -= intervalStep;
            }

            // Следующий вызов — уже с новой паузой
            Invoke(nameof(Spawn), interval);
        }
    }
}
