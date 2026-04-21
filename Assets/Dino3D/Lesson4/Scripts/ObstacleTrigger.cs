using UnityEngine;

namespace Dino3D.Lesson4
{
    public class ObstacleTrigger : MonoBehaviour
    {
        void Start()
        {
            Destroy(gameObject, 10f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<DinoController>())
            {
                GameController gc = Object.FindFirstObjectByType<GameController>();
                gc.GameOver();
            }
        }
    }
}
