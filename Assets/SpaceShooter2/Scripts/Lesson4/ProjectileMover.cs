using UnityEngine;

namespace SpaceShooter2.Lesson4
{
    public class ProjectileMover : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float lifeTime = 2f;

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
