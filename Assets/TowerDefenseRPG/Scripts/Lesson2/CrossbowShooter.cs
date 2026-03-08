using UnityEngine;

namespace TowerDefenseRPG.Lesson2
{
    public class CrossbowShooter : MonoBehaviour
    {
        public Transform shootPoint;
        public BoltProjectile boltPrefab;
        public KeyCode shootKey = KeyCode.Space;
        public int boltDamage = 1;

        public void Update()
        {
            if (Input.GetKeyDown(shootKey))
            {
                Shoot();
            }
        }

        public void Shoot()
        {
            BoltProjectile newBolt = Instantiate(boltPrefab, shootPoint.position, shootPoint.rotation);
            newBolt.damage = boltDamage;
        }
    }
}
