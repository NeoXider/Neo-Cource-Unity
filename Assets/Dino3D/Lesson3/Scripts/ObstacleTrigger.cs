using UnityEngine;

namespace Dino3D.Lesson3
{
    public class ObstacleTrigger : MonoBehaviour
    {
        void Start()
        {
            Destroy(gameObject, 10f); // Авто-удаление через 10 секунд
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<DinoController>())
            {
                Debug.Log("Столкновение с препятствием!");
            }
        }
    }
}
