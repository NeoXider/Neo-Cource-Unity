using UnityEngine;

namespace SpaceShooter2.Lesson2
{
    public class FallingObjectBase : MonoBehaviour
    {
        public float speed = 3f;
        public float destroyY = -7f;

        public virtual void Update()
        {
            transform.position += Vector3.down * speed * Time.deltaTime;

            if (transform.position.y < destroyY)
            {
                Destroy(gameObject);
            }
        }
    }
}
