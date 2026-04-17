using UnityEngine;

namespace Dino3D.Lesson2
{
    public class CameraController : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        void LateUpdate()
        {
            // BUGFIX: В самом курсе (y2.md) допущена механическая опечатка!
            // Там было сказано добавить offset каждый кадр к текущей позиции камеры (pos + offset).
            // Из-за этого камера улетала со скоростью света. 
            // Правильное решение - прибавлять offset к позиции ИГРОКА.
            
            Vector3 pos = transform.position;
            // Камера следует за игроком только по оси X (+ учитывая смещение offset.x)
            pos.x = target.position.x + offset.x;
            transform.position = pos;
        }
    }
}
