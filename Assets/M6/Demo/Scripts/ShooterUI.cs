using TMPro;
using UnityEngine;

namespace Lessons.M6.Y4
{
    public class ShooterUI : MonoBehaviour
    {
        public TMP_Text scoreText;
        public TMP_Text livesText;
        public GameObject gameOverPanel;
        public TMP_Text resultText;

        // Вызывается один раз при старте сцены
        void Start()
        {
            gameOverPanel.SetActive(false);
        }

        // Показывает счёт
        public void ShowScore(int score)
        {
            scoreText.text = "Очки: " + score;
        }

        // Показывает, сколько жизней осталось
        public void ShowLives(int lives)
        {
            livesText.text = "Жизни: " + lives;
        }

        // Показывает панель проигрыша с итогом и рекордом
        public void ShowGameOver(int score, int bestScore)
        {
            resultText.text = "Счёт: " + score + "\nРекорд: " + bestScore;
            gameOverPanel.SetActive(true);
        }
    }
}
