using TMPro;
using UnityEngine;

namespace TowerDefenseRPG.Lesson5
{
    public class GameManager : MonoBehaviour
    {
        public CrossbowShooter crossbowShooter;
        public DefensePoint defensePoint;
        public TMP_Text currencyText;
        public TMP_Text baseHealthText;
        public TMP_Text waveText;

        public int currentCurrency;
        public int currentWave = 1;
        public int damageUpgradeCost = 5;
        public int attackSpeedUpgradeCost = 5;

        public void Start()
        {
            UpdateUI();
        }

        public void Update()
        {
            UpdateUI();
        }

        public void AddCurrency(int value)
        {
            currentCurrency += value;
            UpdateUI();
        }

        public void SetWave(int waveNumber)
        {
            currentWave = waveNumber;
            UpdateUI();
        }

        public bool CanSpendCurrency(int price)
        {
            return currentCurrency >= price;
        }

        public void TryBuyDamageUpgrade()
        {
            if (!CanSpendCurrency(damageUpgradeCost))
            {
                return;
            }

            currentCurrency -= damageUpgradeCost;
            crossbowShooter.UpgradeDamage(1);
            UpdateUI();
        }

        public void TryBuyAttackSpeedUpgrade()
        {
            if (!CanSpendCurrency(attackSpeedUpgradeCost))
            {
                return;
            }

            currentCurrency -= attackSpeedUpgradeCost;
            crossbowShooter.UpgradeAttackSpeed(0.1f);
            UpdateUI();
        }

        public void UpdateUI()
        {
            if (currencyText != null)
            {
                currencyText.text = "Монеты: " + currentCurrency;
            }

            if (baseHealthText != null)
            {
                baseHealthText.text = "HP базы: " + defensePoint.currentHealth;
            }

            if (waveText != null)
            {
                waveText.text = "Волна: " + currentWave;
            }
        }
    }
}
