using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class EnemyShip : FallingObjectBase
    {
        public GameObject enemyProjectilePrefab;
        public Transform firePoint;
        public float startDelay = 1f;
        public float fireRate = 2f;

        public void Start()
        {
            InvokeRepeating(nameof(Fire), startDelay, fireRate);
        }

        public void Fire()
        {
            Instantiate(enemyProjectilePrefab, firePoint.position, firePoint.rotation);
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            ProjectileMover projectile = other.GetComponent<ProjectileMover>();

            if (projectile != null)
            {
                Destroy(projectile.gameObject);
                Destroy(gameObject);
            }
        }
    }
}
