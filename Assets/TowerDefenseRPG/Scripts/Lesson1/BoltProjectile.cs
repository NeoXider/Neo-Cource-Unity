using UnityEngine;

namespace TowerDefenseRPG.Lesson1
{
    public class BoltProjectile : MonoBehaviour
    {
        public float moveSpeed = 12f;
        public float lifeTime = 3f;

        public void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        public void Update()
        {
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        }
    }
}
