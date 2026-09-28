using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Kiwi - Fly Rotation")]
    public float flyPitchOffset = -10f;
    public float flyYawOffset = 0f;

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
    // Kiwi
    // =========================================================

    [Header("Kiwi")]

    public Transform frontObject;


    // =========================================================
    // Kiwi - 靜止時的位置
    // =========================================================

    [Header("Kiwi - Idle / Think Position")]

    // Kiwi 距離玩家多遠
    public float idleDistance = 2f;

    // Kiwi 高度
    public float idleHeight = 0f;

    // Kiwi 左右偏移
    // 正數 = 右邊
    // 負數 = 左邊
    public float idleHorizontalOffset = 0f;


    // =========================================================
    // Kiwi - 飛行時的位置
    // =========================================================

    [Header("Kiwi - Fly Position")]

    // 飛行時 Kiwi 會跑到玩家前方多遠
    public float flyDistance = 3.5f;

    // 飛行時比平常高多少
    public float flyHeight = 0.5f;

    // 飛行時左右偏移
    public float flyHorizontalOffset = 0.15f;


    // =========================================================
    // Kiwi - Follow Settings
    // =========================================================

    [Header("Kiwi - Follow Settings")]

    // Kiwi 追目標位置的速度
    public float followSpeed = 3f;

    // Kiwi 轉向速度
    public float rotationSpeed = 5f;


    // =========================================================
    // Kiwi - Flying Bob
    // =========================================================

    [Header("Kiwi - Flying Bob")]

    // 上下浮動幅度
    public float flyBobAmount = 0.025f;

    // 上下浮動速度
    public float flyBobSpeed =1.8f;


    // =========================================================
    // Private
    // =========================================================

    private CharacterController controller;

    private float verticalVelocity;

    private bool isMoving;

    // 玩家目前真正的移動方向
    private Vector3 currentMoveDirection;


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
        // 是否正在移動
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


        // 不讓 Camera 上下角度影響移動
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


        // 記錄玩家真正的移動方向
        if (move.sqrMagnitude > 0.01f)
        {
            currentMoveDirection =
                move.normalized;
        }


        // =====================================================
        // Speed
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
        UpdateKiwi();
    }


    // =========================================================
    // Kiwi
    // =========================================================

    void UpdateKiwi()
    {
        if (
            playerCamera == null ||
            frontObject == null
        )
        {
            return;
        }


        // =====================================================
        // 玩家正在移動
        // =====================================================

        if (isMoving)
        {
            UpdateFlyingKiwi();
        }

        // =====================================================
        // 玩家停止
        // =====================================================

        else
        {
            UpdateStandingKiwi();
        }
    }


    // =========================================================
    // Kiwi 飛行
    // =========================================================

    void UpdateFlyingKiwi()
    {
        // =====================================================
        // 飛行方向
        //
        // 使用玩家真正移動的方向，
        // 而不是直接黏 Camera.forward。
        // =====================================================

        Vector3 flyDirection =
            currentMoveDirection;


        if (flyDirection.sqrMagnitude < 0.01f)
        {
            flyDirection =
                transform.forward;
        }


        flyDirection.y = 0f;

        flyDirection.Normalize();


        // =====================================================
        // 右方向
        // =====================================================

        Vector3 rightDirection =
            Vector3.Cross(
                Vector3.up,
                flyDirection
            ).normalized;


        // =====================================================
        // 上下浮動
        // =====================================================

        float bob =
            Mathf.Sin(
                Time.time *
                flyBobSpeed
            )
            *
            flyBobAmount;


        // =====================================================
        // 飛行目標位置
        //
        // 注意：
        // 現在是以 Player 世界座標為基準，
        // 不再是 Camera 畫面座標。
        // =====================================================

        Vector3 targetPosition =
            transform.position
            +
            flyDirection *
            flyDistance
            +
            Vector3.up *
            (flyHeight + bob)
            +
            rightDirection *
            flyHorizontalOffset;


        // =====================================================
        // 平滑飛向目標
        // =====================================================

        frontObject.position =
            Vector3.Lerp(
                frontObject.position,
                targetPosition,
                followSpeed *
                Time.deltaTime
            );


        // =====================================================
        // 飛行方向
        // =====================================================

        if (flyDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    flyDirection,
                    Vector3.up
                );

            targetRotation *= Quaternion.Euler(
                flyPitchOffset,
                flyYawOffset,
                0f
            );


            frontObject.rotation =
                Quaternion.Slerp(
                    frontObject.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }
    }


    // =========================================================
    // Kiwi 靜止 / 對話
    // =========================================================

    void UpdateStandingKiwi()
    {
        // =====================================================
        // Camera 水平前方
        //
        // 這裡仍然讓 Kiwi 待在玩家視線附近，
        // 方便進行對話。
        //
        // 但 Kiwi 不再瞬間黏著 Camera。
        // =====================================================

        Vector3 cameraForward =
            playerCamera.forward;


        cameraForward.y = 0f;

        cameraForward.Normalize();


        Vector3 cameraRight =
            playerCamera.right;


        cameraRight.y = 0f;

        cameraRight.Normalize();


        // =====================================================
        // 對話位置
        // =====================================================

        Vector3 targetPosition =
            transform.position
            +
            cameraForward *
            idleDistance
            +
            Vector3.up *
            idleHeight
            +
            cameraRight *
            idleHorizontalOffset;


        // =====================================================
        // 平滑回到玩家附近
        // =====================================================

        frontObject.position =
            Vector3.Lerp(
                frontObject.position,
                targetPosition,
                followSpeed *
                Time.deltaTime
            );


        // =====================================================
        // Kiwi 面向玩家
        // =====================================================

        Vector3 directionToPlayer =
            transform.position -
            frontObject.position;


        directionToPlayer.y = 0f;


        if (
            directionToPlayer.sqrMagnitude >
            0.01f
        )
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    directionToPlayer,
                    Vector3.up
                );


            frontObject.rotation =
                Quaternion.Slerp(
                    frontObject.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }
    }
}