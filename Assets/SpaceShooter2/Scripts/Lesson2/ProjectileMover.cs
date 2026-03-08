using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class ProjectileMover : MonoBehaviour
    {
        public float speed = 10f;
        public float lifeTime = 2f;

        public void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        public void Update()
        {
            transform.position += Vector3.up * speed * Time.deltaTime;
        }
    }
}
