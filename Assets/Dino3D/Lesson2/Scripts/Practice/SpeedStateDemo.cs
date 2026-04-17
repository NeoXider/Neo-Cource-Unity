using UnityEngine;

namespace Dino3D.Lesson2.Practice
{
    public class SpeedStateDemo : MonoBehaviour
    {
        public int speed = 6;
        void Start()
        {
            if (speed >= 8) Debug.Log("Скорость: высокая");
            else if (speed >= 5) Debug.Log("Скорость: нормальная");
            else Debug.Log("Скорость: низкая");
        }
    }
}
