using UnityEngine;

namespace Lessons.M5.Y6
{
    public class Wallet : MonoBehaviour
    {
        public DefenceUI gameUI;

        private int coins;

        // Вызывается один раз при старте сцены
        void Start()
        {
            // Достаём сохранённые монеты (0, если сохранения ещё нет)
            coins = PlayerPrefs.GetInt("Coins", 0);
            gameUI.ShowCoins(coins);
        }

        // Кладёт монеты в кошелёк
        public void AddCoins(int amount)
        {
            coins += amount;
            UpdateCoins();
        }

        // Пытается потратить монеты. Отвечает, получилось ли
        public bool TrySpend(int price)
        {
            // Монет не хватает — ничего не списываем
            if (coins < price)
            {
                return false;
            }

            coins -= price;
            UpdateCoins();
            return true;
        }

        // Монет стало больше или меньше — сохраняем и обновляем надпись
        private void UpdateCoins()
        {
            PlayerPrefs.SetInt("Coins", coins);
            gameUI.ShowCoins(coins);
        }

        // Стирает сохранённые монеты
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey("Coins");
        }
    }
}
