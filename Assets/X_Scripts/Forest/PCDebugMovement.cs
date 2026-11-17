
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PCDebugMovement : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;

    [Header("Movement")]
    public bool enableKeyboardMovement = true;
    public float moveSpeed = 2f;
    public float runSpeed = 4f;
    public float turnSpeed = 90f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    // 落地時保持輕微向下速度
    public float groundedVelocity = -2f;

    // 控制下坡時貼地的輔助速度
    public float groundSnapSpeed = 4f;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        verticalVelocity = 0f;
    }

    private void Update()
    {
        // 停用 PC 控制時，完全不干涉 XR 移動
        if (!enableKeyboardMovement)
            return;

        if (controller == null || playerCamera == null)
            return;

        // ==========================================
        // 1. 取得 WASD
        // ==========================================

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move =
            forward * vertical +
            right * horizontal;

        move = Vector3.ClampMagnitude(move, 1f);

        // ==========================================
        // 2. Shift 加速
        // ==========================================

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? runSpeed
            : moveSpeed;

        // ==========================================
        // 3. 重力與貼地
        // ==========================================

        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVelocity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // ==========================================
        // 4. 合併水平與垂直移動
        // ==========================================

        Vector3 finalMove = move * speed;
        finalMove.y = verticalVelocity;

        CollisionFlags flags = controller.Move(
            finalMove * Time.deltaTime
        );

        // ==========================================
        // 5. 下坡貼地輔助
        // ==========================================

        if ((flags & CollisionFlags.Below) != 0)
        {
            verticalVelocity = groundedVelocity;
        }
        else if (verticalVelocity <= 0f &&
                 move.sqrMagnitude > 0.001f)
        {
            // 向下探測少量距離，協助跟隨緩坡
            Vector3 rayOrigin =
                transform.position + Vector3.up * 0.2f;

            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                controller.height + 0.5f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != controller &&
                    hit.normal.y >=
                    Mathf.Cos(controller.slopeLimit *
                              Mathf.Deg2Rad))
                {
                    controller.Move(
                        Vector3.down *
                        groundSnapSpeed *
                        Time.deltaTime
                    );
                }
            }
        }

        // ==========================================
        // 6. Q / E 轉向
        // ==========================================

        float turn = 0f;

        if (Input.GetKey(KeyCode.Q))
            turn -= 1f;

        if (Input.GetKey(KeyCode.E))
            turn += 1f;

        transform.Rotate(
            Vector3.up,
            turn * turnSpeed * Time.deltaTime
        );
    }
}
