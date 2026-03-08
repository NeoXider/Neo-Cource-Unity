using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class AsteroidMover : FallingObjectBase
    {
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
