using UnityEngine;

namespace SpaceShooter2.Lesson4
{
    public class EnemyProjectileMover : MonoBehaviour
    {
        [SerializeField] private float speed = 6f;
        [SerializeField] private float lifeTime = 4f;

        private GameManager gameManager;

        public void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        public void Update()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                return;
            }

            float multiplier = 1f;
            if (gameManager != null)
            {
                multiplier = gameManager.SpeedMultiplier;
            }

            transform.position += Vector3.down * speed * multiplier * Time.deltaTime;
        }

        public void SetGameManager(GameManager manager)
        {
            gameManager = manager;
        }
    }
}
