using UnityEngine;

namespace Lessons.M5.Y6
{
    public class DefensePoint : MonoBehaviour
    {
        public int maxHealth = 10;
        public DefenceUI gameUI;
        public DefenceController gameController;

        private int currentHealth;

        // Вызывается один раз при старте сцены
        void Start()
        {
            currentHealth = maxHealth;
            gameUI.ShowWallHealth(currentHealth);
        }

        // Получает урон от врага, который дошёл до стены
        public void TakeDamage(int damage)
        {
            currentHealth -= damage;

            // Меньше нуля прочность не бывает
            if (currentHealth < 0)
            {
                currentHealth = 0;
            }

            gameUI.ShowWallHealth(currentHealth);

            if (currentHealth == 0)
            {
                gameController.GameOver();
            }
        }
    }
}
