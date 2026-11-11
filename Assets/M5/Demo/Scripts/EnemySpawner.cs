using UnityEngine;

namespace Lessons.M5.Y6
{
    public class EnemySpawner : MonoBehaviour
    {
        public EnemyUnit[] enemyPrefabs;
        public float spawnInterval = 1.5f;
        public float rangeX = 2.2f;
        public int waveSize = 3;
        public int waveGrowth = 2;
        public float wavePause = 4f;
        public DefenceUI gameUI;

        private float timer;
        private int wave = 1;
        private int spawnedInWave;

        // Вызывается один раз при старте сцены
        void Start()
        {
            gameUI.ShowWave(wave);
        }

        // Вызывается каждый кадр
        void Update()
        {
            timer += Time.deltaTime;

            // Время следующего врага ещё не пришло
            if (timer < spawnInterval)
            {
                return;
            }

            timer = 0f;
            SpawnEnemy();
            spawnedInWave += 1;

            // Волна выпущена целиком — начинаем следующую
            if (spawnedInWave >= waveSize + wave * waveGrowth)
            {
                StartNextWave();
            }
        }

        // Новая волна: счётчик с нуля, перед ней передышка
        private void StartNextWave()
        {
            wave += 1;
            spawnedInWave = 0;

            // Таймер уходит в минус — и спавнер молчит wavePause секунд
            timer = -wavePause;
            gameUI.ShowWave(wave);
        }

        // Создаёт случайного врага в случайном месте над экраном
        private void SpawnEnemy()
        {
            // С каждой волной открывается ещё один вид врага
            int kinds = wave;

            if (kinds > enemyPrefabs.Length)
            {
                kinds = enemyPrefabs.Length;
            }

            int index = Random.Range(0, kinds);
            float x = Random.Range(-rangeX, rangeX);
            Vector3 position = new Vector3(x, transform.position.y, 0f);

            Instantiate(enemyPrefabs[index], position, Quaternion.identity);
        }
    }
}
