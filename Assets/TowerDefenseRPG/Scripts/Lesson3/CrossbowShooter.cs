using UnityEngine;

namespace TowerDefenseRPG.Lesson3
{
    public class CrossbowShooter : MonoBehaviour
    {
        public Transform shootPoint;
        public BoltProjectile boltPrefab;
        public KeyCode shootKey = KeyCode.Space;
        public int boltDamage = 1;
        public float attackCooldown = 0.7f;

        public float currentCooldown;

        public void Update()
        {
            currentCooldown -= Time.deltaTime;

            if (Input.GetKeyDown(shootKey))
            {
                TryShoot();
            }
        }

        public void TryShoot()
        {
            if (!CanShoot())
            {
                return;
            }

            Shoot();
            currentCooldown = attackCooldown;
        }

        public bool CanShoot()
        {
            return currentCooldown <= 0f;
        }

        public void Shoot()
        {
            BoltProjectile newBolt = Instantiate(boltPrefab, shootPoint.position, shootPoint.rotation);
            newBolt.damage = boltDamage;
        }
    }
}
