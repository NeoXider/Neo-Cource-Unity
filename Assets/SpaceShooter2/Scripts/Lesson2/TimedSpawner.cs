using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class TimedSpawner : MonoBehaviour
    {
        public GameObject asteroidPrefab;
        public GameObject enemyPrefab;
        public float minX = -7f;
        public float maxX = 7f;
        public float spawnY = 6f;
        public float asteroidStartDelay = 1f;
        public float asteroidRate = 1.5f;
        public float enemyStartDelay = 2f;
        public float enemyRate = 3f;

        public void Start()
        {
            InvokeRepeating(nameof(SpawnAsteroid), asteroidStartDelay, asteroidRate);
            InvokeRepeating(nameof(SpawnEnemy), enemyStartDelay, enemyRate);
        }

        public void SpawnAsteroid()
        {
            Vector3 position = GetSpawnPosition();
            Instantiate(asteroidPrefab, position, Quaternion.identity);
        }

        public void SpawnEnemy()
        {
            Vector3 position = GetSpawnPosition();
            Instantiate(enemyPrefab, position, Quaternion.identity);
        }

        public Vector3 GetSpawnPosition()
        {
            float randomX = Random.Range(minX, maxX);
            return new Vector3(randomX, spawnY, 0f);
        }
    }
}
