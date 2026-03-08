using UnityEngine;

namespace SpaceShooter2.Lesson3
{
    public class TimedSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject asteroidPrefab;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float minX = -7f;
        [SerializeField] private float maxX = 7f;
        [SerializeField] private float spawnY = 6f;
        [SerializeField] private float asteroidStartDelay = 1f;
        [SerializeField] private float asteroidRate = 1.5f;
        [SerializeField] private float enemyStartDelay = 2f;
        [SerializeField] private float enemyRate = 3f;
        [SerializeField] private GameManager gameManager;

        public void Start()
        {
            InvokeRepeating(nameof(SpawnAsteroid), asteroidStartDelay, asteroidRate);
            InvokeRepeating(nameof(SpawnEnemy), enemyStartDelay, enemyRate);
        }

        public void SpawnAsteroid()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                return;
            }

            GameObject asteroidObject = Instantiate(asteroidPrefab, GetSpawnPosition(), Quaternion.identity);
            AsteroidMover asteroid = asteroidObject.GetComponent<AsteroidMover>();

            if (asteroid != null)
            {
                asteroid.SetGameManager(gameManager);
            }
        }

        public void SpawnEnemy()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                return;
            }

            GameObject enemyObject = Instantiate(enemyPrefab, GetSpawnPosition(), Quaternion.identity);
            EnemyShip enemy = enemyObject.GetComponent<EnemyShip>();

            if (enemy != null)
            {
                enemy.SetGameManager(gameManager);
            }
        }

        private Vector3 GetSpawnPosition()
        {
            float randomX = Random.Range(minX, maxX);
            return new Vector3(randomX, spawnY, 0f);
        }
    }
}
