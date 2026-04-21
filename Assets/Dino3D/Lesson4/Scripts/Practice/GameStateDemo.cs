using UnityEngine;

namespace Dino3D.Lesson4.Practice
{
    public class GameStateDemo : MonoBehaviour
    {
        bool isGameOver = false;

        void Start()
        {
            if (isGameOver)
            {
                Debug.Log("Экран Game Over");
            }
            else
            {
                Debug.Log("Играем");
            }
        }
    }
}
