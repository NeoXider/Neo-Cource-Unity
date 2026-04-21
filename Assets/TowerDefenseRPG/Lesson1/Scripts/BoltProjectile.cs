using UnityEngine;

namespace TowerDefenseRPG.Lesson1
{
    public class BoltProjectile : MonoBehaviour
    {
        public float moveSpeed = 36f;
        public float lifeTime = 6f;

        void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);
        }
    }
}
