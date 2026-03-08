using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class WaveSpawner : MonoBehaviour
    {
        public EnemyUnit[] enemyPrefabs;
        public Transform spawnPoint;
        public Transform targetPoint;
        public GameManager gameManager;

        public int startEnemiesPerWave = 3;
        public float timeBetweenSpawns = 1.2f;
        public float timeBetweenWaves = 4f;

        public int currentWave = 1;
        public int enemiesLeftToSpawn;
        public float spawnTimer;
        public float waveTimer;

        public void Start()
        {
            StartWave();
        }

        public void Update()
        {
            if (enemiesLeftToSpawn > 0)
            {
                spawnTimer -= Time.deltaTime;

                if (spawnTimer <= 0f)
                {
                    SpawnEnemy();
                    enemiesLeftToSpawn--;
                    spawnTimer = timeBetweenSpawns;
                }
            }
            else
            {
                waveTimer -= Time.deltaTime;

                if (waveTimer <= 0f)
                {
                    currentWave++;
                    StartWave();
                }
            }
        }

        public void StartWave()
        {
            enemiesLeftToSpawn = startEnemiesPerWave + currentWave - 1;
            spawnTimer = 0f;
            waveTimer = timeBetweenWaves;

            if (gameManager != null)
            {
                gameManager.SetWave(currentWave);
            }
        }

        public void SpawnEnemy()
        {
            int prefabIndex = 0;
            if (enemyPrefabs.Length > 1 && currentWave >= 2)
            {
                prefabIndex = Random.Range(0, enemyPrefabs.Length);
            }

            EnemyUnit enemy = Instantiate(enemyPrefabs[prefabIndex], spawnPoint.position, spawnPoint.rotation);
            enemy.targetPoint = targetPoint;
            enemy.gameManager = gameManager;
            enemy.maxHealth += currentWave - 1;
            enemy.currentHealth = enemy.maxHealth;
        }
    }
}
