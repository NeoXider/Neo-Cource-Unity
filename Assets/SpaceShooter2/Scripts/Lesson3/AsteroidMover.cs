using UnityEngine;

namespace SpaceShooter2.Lesson3
{
    public class AsteroidMover : FallingObjectBase
    {
        [SerializeField] private int scoreValue = 5;

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
