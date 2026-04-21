using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class WaveSpawner : MonoBehaviour
    {
        public EnemyUnit[] enemyPrefabs;
        public GameManager gameManager;
        public Transform topSpawnBound; // Верхняя граница спавна
        
        public float timeBetweenSpawns = 2f;
        private float nextSpawnTime;

        public int enemiesPerWave = 3;
        private int enemiesLeft = 3;
        public int currentWaveLabel = 1;

        void Update()
        {
            // 1. Идет волна (враги остались)
            if (enemiesLeft > 0)
            {
                if (Time.time >= nextSpawnTime)
                {
                    nextSpawnTime = Time.time + timeBetweenSpawns;
                    int rand = Random.Range(0, enemyPrefabs.Length);

                    float randomY = Random.Range(transform.position.y, topSpawnBound.position.y);
                    Vector3 spawnPos = new Vector3(transform.position.x, randomY, 0);

                    EnemyUnit newEnemy = Instantiate(enemyPrefabs[rand], spawnPos, Quaternion.identity);
                    newEnemy.gameManager = gameManager;
                    
                    enemiesLeft--; // Уменьшаем остаток врагов для этой волны
                }
            }
            else // 2. Волна кончилась — ждем и запускаем новую, усложненную!
            {
                if (Time.time >= nextSpawnTime) // Ждем ту же самую паузу, но побольше
                {
                    currentWaveLabel++;
                    enemiesPerWave += 2; // УСЛОЖНЯЕМ ИГРУ: На 2 врага больше в новой волне!
                    enemiesLeft = enemiesPerWave;
                    
                    Debug.Log("Началась волна: " + currentWaveLabel);
                }
            }
        }
    }
}
