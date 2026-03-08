using UnityEngine;

namespace TowerDefenseRPG.Lesson6
{
    public class DefensePoint : MonoBehaviour
    {
        public int maxHealth = 10;
        public int currentHealth = 10;

        public void Start()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(int damage)
        {
            currentHealth -= damage;

            if (currentHealth < 0)
            {
                currentHealth = 0;
            }
        }

        public void UpgradeMaxHealth(int value)
        {
            maxHealth += value;
            currentHealth += value;
        }

        public bool HasLost()
        {
            return currentHealth <= 0;
        }
    }
}
