using UnityEngine;

namespace Dino3D.Lesson4
{
    public class ObstacleSpawner : MonoBehaviour
    {
        public GameObject obstaclePrefab;
        public float spawnInterval = 1.5f;

        private float timer;
        private GameController gc;

        void Start()
        {
            gc = Object.FindFirstObjectByType<GameController>();
        }

        void Update()
        {
            if (!gc.isGameOver)
            {
                timer += Time.deltaTime;
                if (timer >= spawnInterval)
                {
                    timer = 0f;
                    Instantiate(obstaclePrefab, transform.position, Quaternion.identity);
                }
            }
        }
    }
}
