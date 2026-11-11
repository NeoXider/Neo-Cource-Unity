using UnityEngine;

namespace Lessons.M5.Y6
{
    public class SplitterEnemy : EnemyUnit
    {
        public EnemyUnit smallPrefab;
        public int pieces = 2;

        // Погибая, распадается на маленьких врагов
        protected override void Die()
        {
            for (int i = 0; i < pieces; i++)
            {
                // Чуть в стороне от места гибели, чтобы кусочки не слиплись
                Vector3 offset = Vector3.right * Random.Range(-0.5f, 0.5f);
                Instantiate(smallPrefab, transform.position + offset, Quaternion.identity);
            }

            // Дальше всё как у обычного врага: награда и исчезновение
            base.Die();
        }
    }
}
