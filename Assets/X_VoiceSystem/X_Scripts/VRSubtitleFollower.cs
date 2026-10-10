using UnityEngine;

public class VRSubtitleFollower : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    // XR Main Camera
    public Transform vrCamera;


    // =========================================================
    // Position
    // =========================================================

    [Header("Position")]

    // 字幕距離眼睛多遠
    public float distance = 1.5f;

    // 字幕比視線中心低多少
    public float verticalOffset = -0.25f;


    // =========================================================
    // Smooth
    // =========================================================

    [Header("Smooth Follow")]

    // 位置追蹤速度
    public float positionSmoothSpeed = 5f;

    // 旋轉追蹤速度
    public float rotationSmoothSpeed = 5f;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        if (vrCamera == null)
        {
            Debug.LogError(
                "❌ VRSubtitleFollower 沒有指定 VR Camera！"
            );

            return;
        }


        // 遊戲開始時直接放到正確位置
        SnapToCamera();
    }


    // =========================================================
    // Late Update
    // =========================================================

    private void LateUpdate()
    {
        if (vrCamera == null)
        {
            return;
        }


        // =====================================================
        // Camera 水平方向
        //
        // 不使用 Camera 上下看的角度，
        // 避免低頭時字幕跑到地板、
        // 抬頭時字幕飛到天空。
        // =====================================================

        Vector3 forward =
            vrCamera.forward;


        forward.y = 0f;


        if (forward.sqrMagnitude < 0.001f)
        {
            forward =
                vrCamera.parent != null
                ? vrCamera.parent.forward
                : Vector3.forward;

            forward.y = 0f;
        }


        forward.Normalize();


        // =====================================================
        // 目標位置
        // =====================================================

        Vector3 targetPosition =
            vrCamera.position
            +
            forward * distance
            +
            Vector3.up * verticalOffset;


        // =====================================================
        // 平滑位置
        // =====================================================

        float positionT =
            1f -
            Mathf.Exp(
                -positionSmoothSpeed *
                Time.deltaTime
            );


        transform.position =
            Vector3.Lerp(
                transform.position,
                targetPosition,
                positionT
            );


        // =====================================================
        // 字幕面向玩家
        // =====================================================

        Vector3 directionToCamera =
            vrCamera.position -
            transform.position;


        directionToCamera.y = 0f;


        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    -directionToCamera,
                    Vector3.up
                );


            float rotationT =
                1f -
                Mathf.Exp(
                    -rotationSmoothSpeed *
                    Time.deltaTime
                );


            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationT
                );
        }
    }


    // =========================================================
    // Snap
    // =========================================================

    private void SnapToCamera()
    {
        Vector3 forward =
            vrCamera.forward;


        forward.y = 0f;


        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }


        forward.Normalize();


        transform.position =
            vrCamera.position
            +
            forward * distance
            +
            Vector3.up * verticalOffset;


        transform.rotation =
            Quaternion.LookRotation(
                forward,
                Vector3.up
            );
    }
}