using UnityEngine;

namespace SpaceShooter2.Lesson3
{
    public class EnemyShip : FallingObjectBase
    {
        [SerializeField] private int scoreValue = 10;
        [SerializeField] private GameObject enemyProjectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float startDelay = 1f;
        [SerializeField] private float fireRate = 2f;

        public void Start()
        {
            InvokeRepeating(nameof(Fire), startDelay, fireRate);
        }

        public void Fire()
        {
            if (GameManager != null && GameManager.IsGameOver)
            {
                return;
            }

            GameObject projectileObject = Instantiate(enemyProjectilePrefab, firePoint.position, firePoint.rotation);
            EnemyProjectileMover projectile = projectileObject.GetComponent<EnemyProjectileMover>();

            if (projectile != null)
            {
                projectile.SetGameManager(GameManager);
            }
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            ProjectileMover projectile = other.GetComponent<ProjectileMover>();

            if (projectile != null)
            {
                if (GameManager != null)
                {
                    GameManager.AddScore(scoreValue);
                }

                Destroy(projectile.gameObject);
                Destroy(gameObject);
            }
        }
    }
}
