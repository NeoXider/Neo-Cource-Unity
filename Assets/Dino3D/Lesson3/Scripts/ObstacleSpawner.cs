using UnityEngine;

namespace Dino3D.Lesson3
{
    public class ObstacleSpawner : MonoBehaviour
    {
        public GameObject obstaclePrefab;
        public float spawnInterval = 1.5f;

        private float timer;

        void Update()
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
