
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
        // 按住滑鼠右鍵才可以轉動視角
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X")
                           * mouseSensitivity;

            float mouseY = Input.GetAxis("Mouse Y")
                           * mouseSensitivity;

            // 左右旋轉角色
            transform.Rotate(Vector3.up * mouseX);

            // 上下旋轉攝影機
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

        // WASD 移動
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x
                     + transform.forward * z;

        move = Vector3.ClampMagnitude(move, 1f);

        // Shift 加速奔跑
        float speed = Input.GetKey(KeyCode.LeftShift)
            ? runSpeed : moveSpeed;

        // 地面偵測與空白鍵跳躍
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            // 按下 Space 跳躍
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = Mathf.Sqrt(
                    jumpHeight * -2f * gravity
                );
            }
        }

        // 重力
        verticalVelocity += gravity * Time.deltaTime;

        // 計算移動速度
        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;

        // 移動角色
        controller.Move(velocity * Time.deltaTime);

        // 按下滑鼠右鍵時鎖定游標
        if (Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 放開右鍵或按 Esc 解鎖游標
        if (Input.GetMouseButtonUp(1) ||
            Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
