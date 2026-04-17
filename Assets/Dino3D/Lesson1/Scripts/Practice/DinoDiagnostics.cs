using UnityEngine;

namespace Dino3D.Lesson1.Practice
{
    public class DinoDiagnostics : MonoBehaviour
    {
        public int coins = 7;
        public int needCoins = 10;
        public float speed = 6f;
        public float minSpeed = 5f;
        public string skinName = "Dino";
        public string bannedName = "Cheater";

        void Start()
        {
            bool hasEnoughCoins = coins >= needCoins;
            bool isFastEnough = speed > minSpeed;
            bool isDefaultSkin = skinName == "Dino";
            bool isNameAllowed = skinName != bannedName;
            bool isNonNegativeCoins = coins >= 0;

            Debug.Log($"hasEnoughCoins = {hasEnoughCoins}");
            Debug.Log($"isFastEnough = {isFastEnough}");
            Debug.Log($"isDefaultSkin = {isDefaultSkin}");
            Debug.Log($"isNameAllowed = {isNameAllowed}");
            Debug.Log($"isNonNegativeCoins = {isNonNegativeCoins}");
        }
    }
}
