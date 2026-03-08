using UnityEngine;

namespace TowerDefenseRPG.Lesson2
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
        }
    }
}
