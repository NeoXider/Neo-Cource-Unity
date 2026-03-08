using UnityEngine;

namespace SpaceShooter2.Lesson3
{
    public class PlayerShipController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float minX = -7f;
        [SerializeField] private float maxX = 7f;
        [SerializeField] private float minY = -4f;
        [SerializeField] private float maxY = 4f;

        [Header("Shooting")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;

        [Header("Links")]
        [SerializeField] private GameManager gameManager;

        public void Update()
        {
            if (gameManager.IsGameOver)
            {
                return;
            }

            Move();
            TryShoot();
        }

        private void Move()
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveY = Input.GetAxis("Vertical");

            Vector3 position = transform.position;
            position.x += moveX * moveSpeed * Time.deltaTime;
            position.y += moveY * moveSpeed * Time.deltaTime;
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            transform.position = position;
        }

        private void TryShoot()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            }
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<AsteroidMover>() != null)
            {
                gameManager.FinishGame();
            }

            if (other.GetComponent<EnemyShip>() != null)
            {
                gameManager.FinishGame();
            }

            if (other.GetComponent<EnemyProjectileMover>() != null)
            {
                gameManager.FinishGame();
            }
        }
    }
}
