using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Dino3D.Lesson4
{
    public class GameController : MonoBehaviour
    {
        public bool isGameOver;
        public TMP_Text scoreText;
        public GameObject gameOverPanel;

        private float score;

        void Update()
        {
            if (!isGameOver)
            {
                score += Time.deltaTime;
                if (scoreText != null)
                    scoreText.text = ((int)score).ToString();
            }
        }

        public void GameOver()
        {
            isGameOver = true;
            if(gameOverPanel != null) gameOverPanel.SetActive(true);
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
