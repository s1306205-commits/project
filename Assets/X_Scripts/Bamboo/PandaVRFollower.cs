
using UnityEngine;

public class PandaVRFollower : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    public Transform playerCamera;

    public Transform panda;

    public VRPlayerMovementState movementState;


    // =========================================================
    // Idle Position
    // =========================================================

    [Header("Idle Position")]

    public float idleDistance = 1.5f;

    public float idleHorizontalOffset = 0f;


    // =========================================================
    // Run Position
    // =========================================================

    [Header("Run Position")]

    public float runDistance = 2f;

    public float runHorizontalOffset = 0.2f;


    // =========================================================
    // Ground Detection
    // =========================================================

    [Header("Ground Detection")]

    public LayerMask groundLayer;

    public float rayStartHeight = 10f;

    public float rayDistance = 40f;

    // 熊貓根物件原點與腳底的高度差
    public float footOffset = 0f;


    // =========================================================
    // Follow Settings
    // =========================================================

    [Header("Follow Settings")]

    public float followSpeed = 3f;

    public float verticalSpeed = 12f;

    public float rotationSpeed = 5f;


    // =========================================================
    // Rotation
    // =========================================================

    [Header("Rotation")]

    public float rotationOffset = 0f;


    // =========================================================
    // Private
    // =========================================================

    private bool wasActive = false;

    private bool warnedNoGround = false;


    // =========================================================
    // Late Update
    // =========================================================

    private void LateUpdate()
    {
        if (
            playerCamera == null ||
            panda == null ||
            movementState == null
        )
        {
            return;
        }

        // 熊貓被隱藏時停止跟隨
        if (!panda.gameObject.activeInHierarchy)
        {
            wasActive = false;
            return;
        }

        bool isMoving = movementState.IsMoving;


        // =====================================================
        // 1. 計算移動方向
        // =====================================================

        Vector3 direction = isMoving
            ? movementState.MoveDirection
            : playerCamera.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = playerCamera.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        direction.Normalize();


        // =====================================================
        // 2. 計算跟隨距離
        // =====================================================

        float distance = isMoving
            ? runDistance
            : idleDistance;

        float horizontalOffset = isMoving
            ? runHorizontalOffset
            : idleHorizontalOffset;

        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                direction
            ).normalized;


        // =====================================================
        // 3. 計算水平目標位置
        // =====================================================

        Vector3 targetPosition =
            playerCamera.position
            + direction * distance
            + right * horizontalOffset;


        // =====================================================
        // 4. 第一次啟用時直接放到目標位置
        // =====================================================

        if (!wasActive)
        {
            Vector3 initialPosition = targetPosition;

            if (TryGetGroundHeight(
                initialPosition,
                out float initialGroundY))
            {
                initialPosition.y =
                    initialGroundY + footOffset;

                panda.position = initialPosition;
            }

            wasActive = true;
        }


        // =====================================================
        // 5. 平滑水平跟隨
        // =====================================================

        Vector3 nextPosition = panda.position;

        Vector3 horizontalTarget =
            new Vector3(
                targetPosition.x,
                nextPosition.y,
                targetPosition.z
            );

        nextPosition = Vector3.Lerp(
            nextPosition,
            horizontalTarget,
            Mathf.Clamp01(
                followSpeed * Time.deltaTime
            )
        );


        // =====================================================
        // 6. 偵測實際移動後的地面高度
        // =====================================================

        if (TryGetGroundHeight(
            nextPosition,
            out float groundY))
        {
            float targetY = groundY + footOffset;

            nextPosition.y = Mathf.MoveTowards(
                nextPosition.y,
                targetY,
                verticalSpeed * Time.deltaTime
            );

            warnedNoGround = false;
        }
        else
        {
            // 偵測不到地板時維持目前高度
            nextPosition.y = panda.position.y;

            if (!warnedNoGround)
            {
                Debug.LogWarning(
                    "PandaVRFollower：沒有偵測到 Ground，" +
                    "請檢查地面 Layer 和 Collider。"
                );

                warnedNoGround = true;
            }
        }

        panda.position = nextPosition;


        // =====================================================
        // 7. 設定熊貓方向
        // =====================================================

        Vector3 lookDirection;

        if (isMoving)
        {
            // 玩家移動時朝移動方向
            lookDirection = direction;
        }
        else
        {
            // 玩家停止時面向玩家
            lookDirection =
                playerCamera.position - panda.position;
        }

        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up
                );

            targetRotation *= Quaternion.Euler(
                0f,
                rotationOffset,
                0f
            );

            panda.rotation = Quaternion.Slerp(
                panda.rotation,
                targetRotation,
                Mathf.Clamp01(
                    rotationSpeed * Time.deltaTime
                )
            );
        }
    }


    // =========================================================
    // Ground Raycast
    // =========================================================

    private bool TryGetGroundHeight(
        Vector3 position,
        out float groundY)
    {
        Vector3 rayOrigin =
            position + Vector3.up * rayStartHeight;

        bool foundGround = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        if (foundGround)
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = position.y;
        return false;
    }
}
