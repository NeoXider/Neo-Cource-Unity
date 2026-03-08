using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class EnemyUnit : MonoBehaviour
    {
        public int maxHealth = 3;
        public int currentHealth = 3;
        public int damageToBase = 1;
        public int reward = 2;
        public float moveSpeed = 2f;
        public Transform targetPoint;
        public GameManager gameManager;

        public void Start()
        {
            currentHealth = maxHealth;
        }

        public void Update()
        {
            if (targetPoint != null)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPoint.position,
                    moveSpeed * Time.deltaTime);
            }
        }

        public void TakeDamage(int damage)
        {
            currentHealth -= damage;

            if (!IsAlive())
            {
                if (gameManager != null)
                {
                    gameManager.AddCurrency(reward);
                }

                Destroy(gameObject);
            }
        }

        public bool IsAlive()
        {
            return currentHealth > 0;
        }

        public void OnTriggerEnter(Collider other)
        {
            DefensePoint defensePoint = other.GetComponent<DefensePoint>();
            if (defensePoint != null)
            {
                defensePoint.TakeDamage(damageToBase);
                Destroy(gameObject);
            }
        }
    }
}
