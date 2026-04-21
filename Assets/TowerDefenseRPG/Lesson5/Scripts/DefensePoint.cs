using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class DefensePoint : MonoBehaviour
    {
        public int health = 10;

        public void TakeDamage(int p_damage)
        {
            health -= p_damage;
            if (HasLost())
            {
                Debug.Log("СТЕНА ПАЛА! ИГРА ПРОИГРАНА!");
            }
        }

        public bool HasLost()
        {
            return health <= 0;
        }
    }
}
