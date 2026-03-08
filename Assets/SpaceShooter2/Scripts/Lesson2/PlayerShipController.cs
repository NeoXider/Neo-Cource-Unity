using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class PlayerShipController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float minX = -7f;
        public float maxX = 7f;
        public float minY = -4f;
        public float maxY = 4f;
        public GameObject projectilePrefab;
        public Transform firePoint;

        public void Update()
        {
            Move();
            TryShoot();
        }

        public void Move()
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

        public void TryShoot()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            }
        }
    }
}
