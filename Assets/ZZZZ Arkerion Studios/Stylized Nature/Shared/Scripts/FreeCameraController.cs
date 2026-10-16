using UnityEngine;
using System.Collections;

namespace Arkerion.Camera
{
    public class FreeCameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 10f;
        public float sprintMultiplier = 2.5f;
        public float verticalSpeed = 8f;

        [Header("Look")]
        public float lookSensitivity = 2f;
        public bool lockCursorOnStart = true;
        public bool startLookingStraight = true;

        private float rotationX = 0f; // yaw
        private float rotationY = 0f; // pitch

        IEnumerator Start()
        {
            if (startLookingStraight)
            {
                rotationX = transform.eulerAngles.y;
                rotationY = 0f;
                transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
            }
            else
            {
                Vector3 rot = transform.eulerAngles;
                rotationX = rot.y;

                float pitch = rot.x;
                if (pitch > 180f)
                    pitch -= 360f;

                rotationY = pitch;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yield return null;

            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            HandleMouseLook();
            HandleMovement();
            HandleCursorToggle();
        }

        void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            rotationX += mouseX;
            rotationY -= mouseY;
            rotationY = Mathf.Clamp(rotationY, -89f, 89f);

            transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }

        void HandleMovement()
        {
            float currentSpeed = moveSpeed;

            if (Input.GetKey(KeyCode.LeftShift))
                currentSpeed *= sprintMultiplier;

            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            transform.position += move * currentSpeed * Time.deltaTime;

            if (Input.GetKey(KeyCode.E))
                transform.position += Vector3.up * verticalSpeed * Time.deltaTime;

            if (Input.GetKey(KeyCode.Q))
                transform.position += Vector3.down * verticalSpeed * Time.deltaTime;
        }

        void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}