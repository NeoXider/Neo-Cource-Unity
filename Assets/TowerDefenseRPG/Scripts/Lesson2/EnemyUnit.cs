using UnityEngine;

namespace TowerDefenseRPG.Lesson2
{
    public class EnemyUnit : MonoBehaviour
    {
        public int maxHealth = 3;
        public int currentHealth = 3;
        public int damageToBase = 1;
        public float moveSpeed = 2f;
        public Transform targetPoint;

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

            if (currentHealth <= 0)
            {
                Destroy(gameObject);
            }
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
