using TMPro;
using UnityEngine;

namespace SpaceShooter2.Lesson4
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameManager gameManager;

        public void Start()
        {
            gameManager.OnScoreChanged.AddListener(UpdateScoreText);
            gameManager.OnGameOver.AddListener(ShowGameOver);
            UpdateScoreText(gameManager.Score);
            statusText.text = "Игра идёт";
            gameOverPanel.SetActive(false);
        }

        public void OnDestroy()
        {
            gameManager.OnScoreChanged.RemoveListener(UpdateScoreText);
            gameManager.OnGameOver.RemoveListener(ShowGameOver);
        }

        public void UpdateScoreText(int value)
        {
            scoreText.text = "Score: " + value;
        }

        public void ShowGameOver()
        {
            statusText.text = "Game Over";
            gameOverPanel.SetActive(true);
        }
    }
}
