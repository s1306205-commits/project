using UnityEngine;

public class WingRotationLimiter : MonoBehaviour
{
    public enum WingSide
    {
        Left,
        Right
    }

    [Header("這是哪一邊翅膀")]
    public WingSide wingSide;

    [Header("整體最大偏轉角度")]
    [Range(10f, 90f)]
    public float maxTotalAngle = 40f;

    private Quaternion baseRotation;

    private Vector3 minDelta;
    private Vector3 maxDelta;

    void Awake()
    {
        // 記住角色正常站姿時的翅膀旋轉
        baseRotation = transform.localRotation;

        SetLimits();
    }

    void LateUpdate()
    {
        // 目前姿勢相對於正常姿勢的旋轉
        Quaternion relativeRotation =
            Quaternion.Inverse(baseRotation) * transform.localRotation;

        Vector3 delta = relativeRotation.eulerAngles;

        // 轉成 -180 ~ 180
        delta.x = ToSignedAngle(delta.x);
        delta.y = ToSignedAngle(delta.y);
        delta.z = ToSignedAngle(delta.z);

        // 第一層：限制各軸
        delta.x = Mathf.Clamp(delta.x, minDelta.x, maxDelta.x);
        delta.y = Mathf.Clamp(delta.y, minDelta.y, maxDelta.y);
        delta.z = Mathf.Clamp(delta.z, minDelta.z, maxDelta.z);

        Quaternion limitedRotation = Quaternion.Euler(delta);

        // 第二層：避免 X/Y/Z 同時旋轉太多
        float totalAngle =
            Quaternion.Angle(Quaternion.identity, limitedRotation);

        if (totalAngle > maxTotalAngle)
        {
            float ratio = maxTotalAngle / totalAngle;

            limitedRotation = Quaternion.Slerp(
                Quaternion.identity,
                limitedRotation,
                ratio
            );
        }

        // 套回正常姿勢
        transform.localRotation =
            baseRotation * limitedRotation;
    }

    void SetLimits()
    {
        if (wingSide == WingSide.Left)
        {
            // 實測：
            // X -40 ~ +25
            // Y -50 ~ +40
            // Z -30 ~ +45
            //
            // 留 5 度安全距離

            minDelta = new Vector3(
                -35f,
                -45f,
                -25f
            );

            maxDelta = new Vector3(
                20f,
                35f,
                40f
            );
        }
        else
        {
            // 實測：
            // X -40 ~ +40
            // Y -30 ~ +45
            // Z -50 ~ +25
            //
            // 留 5 度安全距離

            minDelta = new Vector3(
                -35f,
                -25f,
                -45f
            );

            maxDelta = new Vector3(
                35f,
                40f,
                20f
            );
        }
    }

    float ToSignedAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}