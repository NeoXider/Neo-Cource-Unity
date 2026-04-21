using UnityEngine;

namespace TowerDefenseRPG.Lesson2
{
    public class EnemyUnit : MonoBehaviour
    {
        public int maxHealth = 3;
        public int currentHealth = 3;
        public float moveSpeed = 2f;

        void Start()
        {
            currentHealth = maxHealth;
        }

        void Update()
        {
            transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
        }

        public void TakeDamage(int damage)
        {
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
