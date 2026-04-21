using UnityEngine;

namespace TowerDefenseRPG.Lesson4
{
    public class CrossbowShooter : MonoBehaviour
    {
        public GameObject boltPrefab;
        public Transform shootPoint;
        public float shootCooldown = 0.5f;
        private float nextShootTime;
        
        public DefensePoint targetWall;

        public bool CanShoot()
        {
            if (targetWall != null && targetWall.HasLost())
            {
                return false;
            }
            return true;
        }

        void Update()
        {
            if (!CanShoot()) return;

            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dir = mousePos - transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (Input.GetMouseButton(0) && Time.time >= nextShootTime)
            {
                nextShootTime = Time.time + shootCooldown;
                Shoot();
            }
        }

        void Shoot()
        {
            Instantiate(boltPrefab, shootPoint.position, shootPoint.rotation);
        }
    }
}
