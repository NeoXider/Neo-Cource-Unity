using UnityEngine;

namespace Dino3D.Lesson4
{
    public class DinoController : MonoBehaviour
    {
        public float speed = 5f;
        public float jumpForce = 7f;
        public float rayDistance = 1.1f;
        
        private Rigidbody rb;
        private bool isGrounded;
        private GameController gc;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            gc = Object.FindFirstObjectByType<GameController>();
        }

        void Update()
        {
            if (!gc.isGameOver)
            {
                transform.position += Vector3.right * speed * Time.deltaTime;

                Debug.DrawRay(transform.position, Vector3.down * rayDistance, Color.red);
                isGrounded = Physics.Raycast(transform.position, Vector3.down, rayDistance);

                bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
                if (isGrounded && jumpPressed)
                {
                    Jump();
                }
            }
        }

        void Jump()
        {
            rb.velocity = new Vector3(0, jumpForce, 0);
        }
    }
}
