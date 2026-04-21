using UnityEngine;

namespace TowerDefenseRPG.Lesson4
{
    public class BoltProjectile : MonoBehaviour
    {
        public float moveSpeed = 36f;
        public float lifeTime = 6f;
        
        public int damage = 1;

        void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D coll)
        {
            EnemyUnit enemy = coll.GetComponent<EnemyUnit>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
