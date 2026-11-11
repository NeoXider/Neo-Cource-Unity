using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lessons.M6.Y4
{
    public class ShooterController : MonoBehaviour
    {
        public ShooterUI gameUI;

        private int score;

        // Вызывается один раз при старте сцены
        void Start()
        {
            // После перезапуска время снова идёт
            Time.timeScale = 1f;
            gameUI.ShowScore(score);
        }

        // Начисляет очки за сбитого врага
        public void AddScore(int points)
        {
            score += points;
            gameUI.ShowScore(score);
        }

        // Проигрыш: обновляем рекорд, показываем итог и останавливаем время
        public void GameOver()
        {
            // Достаём сохранённый рекорд (0, если его ещё нет)
            int bestScore = PlayerPrefs.GetInt("BestScore", 0);

            // Побили рекорд — записываем новый
            if (score > bestScore)
            {
                bestScore = score;
                PlayerPrefs.SetInt("BestScore", bestScore);
            }

            gameUI.ShowGameOver(score, bestScore);
            Time.timeScale = 0f;
        }

        // Перезапускает сцену: игра начинается заново
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
