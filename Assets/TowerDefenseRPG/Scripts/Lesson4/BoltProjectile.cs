using UnityEngine;

namespace TowerDefenseRPG.Lesson4
{
    public class BoltProjectile : MonoBehaviour
    {
        public float moveSpeed = 12f;
        public float lifeTime = 3f;
        public int damage = 1;

        public void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        public void Update()
        {
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        }

        public void OnTriggerEnter(Collider other)
        {
            EnemyUnit enemy = other.GetComponent<EnemyUnit>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
