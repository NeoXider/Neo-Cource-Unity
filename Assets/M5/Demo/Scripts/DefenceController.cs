using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lessons.M5.Y6
{
    public class DefenceController : MonoBehaviour
    {
        public DefenceUI gameUI;

        // Вызывается один раз при старте сцены
        void Start()
        {
            // После перезапуска время снова идёт
            Time.timeScale = 1f;
        }

        // Проигрыш: показываем панель и останавливаем время
        public void GameOver()
        {
            gameUI.ShowGameOver();
            Time.timeScale = 0f;
        }

        // Перезапускает сцену: партия начинается заново
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
