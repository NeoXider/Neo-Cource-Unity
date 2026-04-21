using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class EnemyUnit : MonoBehaviour
    {
        public int maxHealth = 3;
        public int currentHealth = 3;
        public float moveSpeed = 2f;
        
        public DefensePoint targetWall;
        public float attackCooldown = 1.0f;
        private float nextAttackTime;

        public GameManager gameManager;
        public int reward = 5;

        void Start()
        {
            currentHealth = maxHealth;
        }

        public bool IsAlive()
        {
            return currentHealth > 0;
        }

        void Update()
        {
            if (!IsAlive()) return;

            if (targetWall != null && !targetWall.HasLost())
            {
                transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
            }
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive()) return;

            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                if (gameManager != null)
                {
                    gameManager.AddCurrency(reward);
                }
                Destroy(gameObject);
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsAlive()) return;

            if (collision.gameObject.CompareTag("BaseField"))
            {
                targetWall = collision.gameObject.GetComponent<DefensePoint>();
            }
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsAlive() || targetWall == null || targetWall.HasLost()) return;

            if (collision.gameObject.CompareTag("BaseField"))
            {
                if (Time.time >= nextAttackTime)
                {
                    nextAttackTime = Time.time + attackCooldown;
                    targetWall.TakeDamage(1); 
                    Debug.Log("Враг ударил стену!");
                }
            }
        }
    }
}
