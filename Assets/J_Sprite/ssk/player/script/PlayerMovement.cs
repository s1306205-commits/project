using UnityEngine;

[RequireComponent(typeof(CharacterController))]

public class PlayerMovement : MonoBehaviour
{
    // =========================================================
    // Movement Settings
    // =========================================================

    [Header("Movement Settings")]

    public float moveSpeed = 5f;

    public float runSpeed = 10f;

    public float gravity = -9.81f;


    // =========================================================
    // Camera
    // =========================================================

    [Header("Camera")]

    public Transform playerCamera;


    // =========================================================
    // Front Character
    // =========================================================

    [Header("Front Character")]

    // Kiwi
    public Transform frontObject;

    // 距離 Camera 多遠
    public float frontDistance = 2f;

    // 上下位置
    public float frontHeightOffset = 0f;

    // 左右位置
    public float frontHorizontalOffset = 0f;


    // =========================================================
    // Private
    // =========================================================

    private CharacterController controller;

    private float verticalVelocity;

    private bool isMoving;


    // =========================================================
    // Public
    // =========================================================

    public bool IsMoving
    {
        get
        {
            return isMoving;
        }
    }


    // =========================================================
    // Start
    // =========================================================

    void Start()
    {
        controller =
            GetComponent<CharacterController>();
    }


    // =========================================================
    // Update
    // =========================================================

    void Update()
    {
        float horizontal =
            Input.GetAxis("Horizontal");

        float vertical =
            Input.GetAxis("Vertical");


        // =====================================================
        // 判斷玩家是否正在移動
        // =====================================================

        isMoving =
            Mathf.Abs(horizontal) > 0.01f ||
            Mathf.Abs(vertical) > 0.01f;


        // =====================================================
        // Camera 方向
        // =====================================================

        Vector3 forward =
            playerCamera.forward;

        Vector3 right =
            playerCamera.right;


        // 不讓 Camera 上下角度影響玩家移動
        forward.y = 0f;

        right.y = 0f;


        forward.Normalize();

        right.Normalize();


        // =====================================================
        // 移動方向
        // =====================================================

        Vector3 move =
            right * horizontal +
            forward * vertical;


        move =
            Vector3.ClampMagnitude(
                move,
                1f
            );


        // =====================================================
        // 速度
        // =====================================================

        float currentSpeed =
            moveSpeed;


        if (
            Input.GetKey(
                KeyCode.LeftShift
            )
        )
        {
            currentSpeed =
                runSpeed;
        }


        // =====================================================
        // Gravity
        // =====================================================

        if (
            controller.isGrounded &&
            verticalVelocity < 0
        )
        {
            verticalVelocity = -2f;
        }


        verticalVelocity +=
            gravity *
            Time.deltaTime;


        // =====================================================
        // Move
        // =====================================================

        Vector3 velocity =
            move * currentSpeed +
            Vector3.up *
            verticalVelocity;


        controller.Move(
            velocity *
            Time.deltaTime
        );
    }


    // =========================================================
    // Late Update
    // =========================================================

    void LateUpdate()
    {
        UpdateFrontObject();
    }


    // =========================================================
    // Kiwi 跟隨
    // =========================================================

    void UpdateFrontObject()
    {
        if (
            playerCamera == null ||
            frontObject == null
        )
        {
            return;
        }


        // =====================================================
        // 位置永遠在 Camera 前方
        // =====================================================

        Vector3 targetPosition =
            playerCamera.position
            +
            playerCamera.forward *
            frontDistance
            +
            playerCamera.up *
            frontHeightOffset
            +
            playerCamera.right *
            frontHorizontalOffset;


        frontObject.position =
            targetPosition;


        // =====================================================
        // Rotation
        // =====================================================

        Vector3 playerRotation =
            transform.eulerAngles;


        if (isMoving)
        {
            // -------------------------------------------------
            // 玩家移動：
            //
            // Kiwi 跟 Player 同方向
            //
            // 之後這個狀態可以播放 Fly。
            // -------------------------------------------------

            frontObject.rotation =
                Quaternion.Euler(
                    playerRotation.x,
                    playerRotation.y,
                    playerRotation.z
                );
        }
        else
        {
            // -------------------------------------------------
            // 玩家靜止：
            //
            // Kiwi 轉過來面對 Player。
            // Y 比 Player 多 180 度。
            // -------------------------------------------------

            frontObject.rotation =
                Quaternion.Euler(
                    playerRotation.x,
                    playerRotation.y + 180f,
                    playerRotation.z
                );
        }
    }
}