using UnityEngine;

namespace Dino3D.Lesson4
{
    public class CameraController : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        void LateUpdate()
        {
            Vector3 pos = transform.position;
            pos.x = target.position.x + offset.x;
            transform.position = pos;
        }
    }
}
