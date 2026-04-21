using UnityEngine;
using TMPro;

namespace TowerDefenseRPG.Lesson5
{
    public class GameManager : MonoBehaviour
    {
        public int currentCurrency = 0;
        public TMP_Text currencyText;
        public CrossbowShooter crossbowShooter;
        public int upgradeCost = 15;

        void Start()
        {
            UpdateUI();
        }

        public void AddCurrency(int value)
        {
            currentCurrency += value;
            UpdateUI();
        }

        public void UpdateUI()
        {
            if (currencyText != null)
            {
                currencyText.text = "Золото: " + currentCurrency;
            }
        }

        public void BuyAttackSpeed()
        {
            if (currentCurrency >= upgradeCost)
            {
                currentCurrency -= upgradeCost;
                crossbowShooter.shootCooldown -= 0.1f;
                UpdateUI();
                Debug.Log("Скорострельность куплена!");
            }
        }
    }
}
