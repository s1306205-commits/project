using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PM2 : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 10f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraRotationX;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }

        Cursor.lockState = CursorLockMode.None;
Cursor.visible = true;
    }

    void Update()
    {
        // Mouse movement
        
        // Only rotate camera while holding right mouse button
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X")
                           * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y")
                           * mouseSensitivity;

            // Rotate player left and right
            transform.Rotate(Vector3.up * mouseX);

            // Look up and down
            cameraRotationX -= mouseY;
            cameraRotationX = Mathf.Clamp(
                cameraRotationX, -85f, 85f
            );

            if (playerCamera != null)
            {
                playerCamera.localRotation =
                    Quaternion.Euler(cameraRotationX, 0f, 0f);
            }
        }


        // WASD movement
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x
                     + transform.forward * z;

        move = Vector3.ClampMagnitude(move, 1f);

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? runSpeed : moveSpeed;

        // Gravity and jump
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (Input.GetButtonDown("Jump"))
            {
                verticalVelocity = Mathf.Sqrt(
                    jumpHeight * -2f * gravity
                );
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        // Unlock mouse
        
        // Lock cursor only while holding right mouse button
        if (Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetMouseButtonUp(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

    }
}