using UnityEngine;

namespace Lessons.M6.Y4
{
    public class PlayerHealth : MonoBehaviour
    {
        public int maxLives = 3;
        public ShooterUI gameUI;
        public ShooterController gameController;

        private int lives;

        // Вызывается один раз при старте сцены
        void Start()
        {
            lives = maxLives;
            gameUI.ShowLives(lives);
        }

        // Корабль получил попадание: минус одна жизнь
        public void LoseLife()
        {
            // Уже сбитый корабль попаданий не считает
            if (lives <= 0)
            {
                return;
            }

            lives -= 1;
            gameUI.ShowLives(lives);

            if (lives <= 0)
            {
                gameController.GameOver();
                Destroy(gameObject);
            }
        }
    }
}
