using TMPro;
using UnityEngine;

namespace Lessons.M5.Y6
{
    public class DefenceUI : MonoBehaviour
    {
        public TMP_Text wallText;
        public TMP_Text coinsText;
        public TMP_Text damagePriceText;
        public TMP_Text speedPriceText;
        public TMP_Text waveText;
        public GameObject gameOverPanel;

        // Вызывается один раз при старте сцены
        void Start()
        {
            gameOverPanel.SetActive(false);
        }

        // Показывает прочность стены
        public void ShowWallHealth(int health)
        {
            wallText.text = "Стена: " + health;
        }

        // Показывает, сколько монет в кошельке
        public void ShowCoins(int coins)
        {
            coinsText.text = "Монеты: " + coins;
        }

        // Показывает цены на кнопках магазина
        public void ShowPrices(int damagePrice, int speedPrice)
        {
            damagePriceText.text = "Урон\n" + damagePrice;
            speedPriceText.text = "Скорость\n" + speedPrice;
        }

        // Показывает номер волны
        public void ShowWave(int wave)
        {
            waveText.text = "Волна " + wave;
        }

        // Показывает панель проигрыша
        public void ShowGameOver()
        {
            gameOverPanel.SetActive(true);
        }
    }
}
