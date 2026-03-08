using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class EnemyProjectileMover : MonoBehaviour
    {
        public float speed = 6f;
        public float lifeTime = 4f;

        public void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        public void Update()
        {
            transform.position += Vector3.down * speed * Time.deltaTime;
        }
    }
}
