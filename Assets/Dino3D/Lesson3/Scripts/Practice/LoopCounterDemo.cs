using UnityEngine;

namespace Dino3D.Lesson3.Practice
{
    public class LoopCounterDemo : MonoBehaviour
    {
        void Start()
        {
            for (int i = 0; i < 5; i++)
            {
                Debug.Log("Спавн препятствия №" + i);
            }
        }
    }
}
