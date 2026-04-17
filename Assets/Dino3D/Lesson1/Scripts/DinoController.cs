using UnityEngine;

namespace Dino3D.Lesson1
{
    public class DinoController : MonoBehaviour
    {
        public float speed = 5f;
        public float jumpForce = 5f;
        private Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            transform.position += Vector3.right * speed * Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Space))
                Jump();
        }

        void Jump()
        {
            rb.velocity = new Vector3(0, jumpForce, 0);
        }
    }
}
