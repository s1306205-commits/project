
using UnityEngine;

public class DogVRFollower : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public Transform dog;
    public VRPlayerMovementState movementState;

    [Header("Idle Position")]
    public float idleDistance = 2f;
    public float idleHorizontalOffset = 0f;

    [Header("Run Position")]
    public float runDistance = 2.5f;
    public float runHorizontalOffset = 0.2f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    public float rayStartHeight = 10f;
    public float rayDistance = 40f;

    // 狗狗根物件到腳底的高度修正
    public float footOffset = 0f;

    [Header("Follow Settings")]
    public float followSpeed = 3f;
    public float verticalSpeed = 12f;
    public float rotationSpeed = 5f;

    [Header("Rotation")]
    public float rotationOffset = 0f;

    private bool wasActive = false;
    private bool warnedNoGround = false;

    private void LateUpdate()
    {
        if (playerCamera == null ||
            dog == null ||
            movementState == null)
            return;

        if (!dog.gameObject.activeInHierarchy)
        {
            wasActive = false;
            return;
        }

        bool isMoving = movementState.IsMoving;

        // 計算水平移動方向
        Vector3 direction = isMoving
            ? movementState.MoveDirection
            : playerCamera.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = playerCamera.forward;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        float distance = isMoving
            ? runDistance
            : idleDistance;

        float horizontalOffset = isMoving
            ? runHorizontalOffset
            : idleHorizontalOffset;

        Vector3 right =
            Vector3.Cross(Vector3.up, direction).normalized;

        // 先計算狗狗的水平目標位置
        Vector3 targetPosition =
            playerCamera.position
            + direction * distance
            + right * horizontalOffset;

        // 從目標位置上方偵測地面
        Vector3 rayOrigin =
            targetPosition + Vector3.up * rayStartHeight;

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
            // 地面高度 + 模型腳底修正
            targetPosition.y =
                hit.point.y + footOffset;

            warnedNoGround = false;
        }
        else
        {
            // 沒找到地面時不強制移動到 Y=0
            targetPosition.y = dog.position.y;

            if (!warnedNoGround)
            {
                Debug.LogWarning(
                    "DogVRFollower：沒有偵測到 Ground，" +
                    "請檢查地面 Layer 和 Collider。"
                );
                warnedNoGround = true;
            }
        }

        // 第一次召喚時直接貼到地面
        if (!wasActive)
        {
            if (foundGround)
            {
                dog.position = targetPosition;
            }

            wasActive = true;
        }
        else
        {
            Vector3 nextPosition = dog.position;

            // 水平跟隨
            Vector3 horizontalTarget = new Vector3(
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

            // 垂直貼地
            if (foundGround)
            {
                nextPosition.y = Mathf.MoveTowards(
                    nextPosition.y,
                    targetPosition.y,
                    verticalSpeed * Time.deltaTime
                );
            }

            dog.position = nextPosition;
        }

        // 設定狗狗方向
        Vector3 lookDirection;

        if (isMoving)
        {
            lookDirection = direction;
        }
        else
        {
            lookDirection =
                playerCamera.position - dog.position;
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

            dog.rotation = Quaternion.Slerp(
                dog.rotation,
                targetRotation,
                Mathf.Clamp01(
                    rotationSpeed * Time.deltaTime
                )
            );
        }
    }
}
