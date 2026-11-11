using UnityEngine;

namespace Lessons.M5.Y6
{
    public class ArmoredEnemy : EnemyUnit
    {
        public int armor = 1;

        // Броня съедает часть урона, остальное достаётся врагу
        public override void TakeDamage(int damage)
        {
            int reducedDamage = damage - armor;

            // Хотя бы единица урона проходит всегда
            if (reducedDamage < 1)
            {
                reducedDamage = 1;
            }

            // Дальше всё как у обычного врага
            base.TakeDamage(reducedDamage);
        }
    }
}
