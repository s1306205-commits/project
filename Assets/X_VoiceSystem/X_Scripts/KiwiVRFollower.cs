using UnityEngine;

public class KiwiVRFollower : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    // XR Origin
    public Transform xrPlayer;

    // XR Origin 裡的 Main Camera
    public Transform playerCamera;

    // Kiwi 本體
    public Transform kiwi;

    // 判斷 XR 玩家是否移動
    public VRPlayerMovementState movementState;


    // =========================================================
    // Kiwi - Idle / Think Position
    // =========================================================

    [Header("Kiwi - Idle / Think Position")]

    public float idleDistance = 2f;

    // 這裡改成相對「頭部高度」
    public float idleHeightOffset = -0.3f;

    public float idleHorizontalOffset = 0f;


    // =========================================================
    // Kiwi - Fly Position
    // =========================================================

    [Header("Kiwi - Fly Position")]

    public float flyDistance = 3.5f;

    // 相對頭部高度
    public float flyHeightOffset = 0f;

    public float flyHorizontalOffset = 0.15f;


    // =========================================================
    // Follow
    // =========================================================

    [Header("Follow Settings")]

    public float followSpeed = 3f;

    public float rotationSpeed = 5f;


    // =========================================================
    // Fly Bob
    // =========================================================

    [Header("Flying Bob")]

    public float flyBobAmount = 0.025f;

    public float flyBobSpeed = 1.8f;


    // =========================================================
    // Rotation
    // =========================================================

    [Header("Fly Rotation")]

    public float flyPitchOffset = -10f;

    public float flyYawOffset = 0f;


    // =========================================================
    // Late Update
    // =========================================================

    private void LateUpdate()
    {
        if (
            xrPlayer == null ||
            playerCamera == null ||
            kiwi == null ||
            movementState == null
        )
        {
            return;
        }


        if (movementState.IsMoving)
        {
            UpdateFlyingKiwi();
        }
        else
        {
            UpdateStandingKiwi();
        }
    }


    // =========================================================
    // Kiwi Flying
    // =========================================================

    private void UpdateFlyingKiwi()
    {
        Vector3 flyDirection =
            movementState.MoveDirection;


        if (
            flyDirection.sqrMagnitude <
            0.01f
        )
        {
            flyDirection =
                playerCamera.forward;
        }


        flyDirection.y = 0f;

        flyDirection.Normalize();


        Vector3 rightDirection =
            Vector3.Cross(
                Vector3.up,
                flyDirection
            ).normalized;


        float bob =
            Mathf.Sin(
                Time.time *
                flyBobSpeed
            )
            *
            flyBobAmount;


        // =====================================================
        // VR 版使用頭部高度
        // =====================================================

        Vector3 targetPosition =
            new Vector3(
                xrPlayer.position.x,
                playerCamera.position.y,
                xrPlayer.position.z
            )
            +
            flyDirection *
            flyDistance
            +
            Vector3.up *
            (flyHeightOffset + bob)
            +
            rightDirection *
            flyHorizontalOffset;


        kiwi.position =
            Vector3.Lerp(
                kiwi.position,
                targetPosition,
                followSpeed *
                Time.deltaTime
            );


        // =====================================================
        // Kiwi 朝飛行方向
        // =====================================================

        if (
            flyDirection.sqrMagnitude >
            0.01f
        )
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    flyDirection,
                    Vector3.up
                );


            targetRotation *=
                Quaternion.Euler(
                    flyPitchOffset,
                    flyYawOffset,
                    0f
                );


            kiwi.rotation =
                Quaternion.Slerp(
                    kiwi.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }
    }


    // =========================================================
    // Kiwi Standing / Conversation
    // =========================================================

    private void UpdateStandingKiwi()
    {
        Vector3 cameraForward =
            playerCamera.forward;


        cameraForward.y = 0f;

        cameraForward.Normalize();


        Vector3 cameraRight =
            playerCamera.right;


        cameraRight.y = 0f;

        cameraRight.Normalize();


        // =====================================================
        // 對話時以 VR 頭部位置為基準
        //
        // 所以 Kiwi 不會跑到玩家腳邊。
        // =====================================================

        Vector3 targetPosition =
            playerCamera.position
            +
            cameraForward *
            idleDistance
            +
            Vector3.up *
            idleHeightOffset
            +
            cameraRight *
            idleHorizontalOffset;


        kiwi.position =
            Vector3.Lerp(
                kiwi.position,
                targetPosition,
                followSpeed *
                Time.deltaTime
            );


        // =====================================================
        // Kiwi 面向玩家的頭
        // =====================================================

        Vector3 directionToPlayer =
            playerCamera.position -
            kiwi.position;


        // 保持 Kiwi 直立
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


            kiwi.rotation =
                Quaternion.Slerp(
                    kiwi.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }
    }
}