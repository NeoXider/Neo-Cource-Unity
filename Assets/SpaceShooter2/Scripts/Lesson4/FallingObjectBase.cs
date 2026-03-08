using UnityEngine;

namespace SpaceShooter2.Lesson4
{
    public class FallingObjectBase : MonoBehaviour
    {
        [SerializeField] private float speed = 3f;
        [SerializeField] private float destroyY = -7f;

        private GameManager gameManager;

        protected GameManager GameManager => gameManager;

        protected virtual void Update()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                return;
            }

            float speedMultiplier = 1f;
            if (gameManager != null)
            {
                speedMultiplier = gameManager.SpeedMultiplier;
            }

            transform.position += Vector3.down * speed * speedMultiplier * Time.deltaTime;

            if (transform.position.y < destroyY)
            {
                Destroy(gameObject);
            }
        }

        public void SetGameManager(GameManager manager)
        {
            gameManager = manager;
        }
    }
}
