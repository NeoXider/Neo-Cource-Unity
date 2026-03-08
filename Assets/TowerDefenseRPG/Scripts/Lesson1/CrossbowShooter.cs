using UnityEngine;

namespace TowerDefenseRPG.Lesson1
{
    public class CrossbowShooter : MonoBehaviour
    {
        public Transform shootPoint;
        public BoltProjectile boltPrefab;
        public KeyCode shootKey = KeyCode.Space;

        public void Update()
        {
            if (Input.GetKeyDown(shootKey))
            {
                Shoot();
            }
        }

        public void Shoot()
        {
            Instantiate(boltPrefab, shootPoint.position, shootPoint.rotation);
        }
    }
}
