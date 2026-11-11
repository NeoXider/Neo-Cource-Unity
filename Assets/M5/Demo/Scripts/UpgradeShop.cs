using UnityEngine;

namespace Lessons.M5.Y6
{
    public class UpgradeShop : MonoBehaviour
    {
        public Wallet wallet;
        public CrossbowShooter crossbow;
        public DefenceUI gameUI;
        public int basePrice = 10;
        public int priceStep = 5;

        private int damageLevel;
        private int speedLevel;

        // Вызывается один раз при старте сцены
        void Start()
        {
            // Достаём сохранённые уровни (0, если сохранения ещё нет)
            damageLevel = PlayerPrefs.GetInt("DamageLevel", 0);
            speedLevel = PlayerPrefs.GetInt("SpeedLevel", 0);
            ApplyUpgrades();
        }

        // Цена следующего уровня: чем выше уровень, тем дороже
        private int GetPrice(int level)
        {
            return basePrice + priceStep * level;
        }

        // Кнопка «Урон»: стрелы бьют сильнее
        public void BuyDamage()
        {
            // Не хватило монет — покупка не состоялась
            if (!wallet.TrySpend(GetPrice(damageLevel)))
            {
                return;
            }

            damageLevel += 1;
            ApplyUpgrades();
        }

        // Кнопка «Скорость»: арбалет стреляет чаще
        public void BuySpeed()
        {
            if (!wallet.TrySpend(GetPrice(speedLevel)))
            {
                return;
            }

            speedLevel += 1;
            ApplyUpgrades();
        }

        // Сохраняет уровни, пересчитывает силу арбалета и обновляет цены
        private void ApplyUpgrades()
        {
            PlayerPrefs.SetInt("DamageLevel", damageLevel);
            PlayerPrefs.SetInt("SpeedLevel", speedLevel);

            crossbow.boltDamage = 1 + damageLevel;
            crossbow.shootInterval = 0.5f / (1f + speedLevel * 0.2f);
            gameUI.ShowPrices(GetPrice(damageLevel), GetPrice(speedLevel));
        }

        // Стирает сохранённые уровни улучшений
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey("DamageLevel");
            PlayerPrefs.DeleteKey("SpeedLevel");
        }
    }
}
