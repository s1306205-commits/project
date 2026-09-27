using UnityEngine;

public class WingAngleTester : MonoBehaviour
{
    public enum TestAxis
    {
        X,
        Y,
        Z
    }

    [Header("測試哪個 Local 軸")]
    public TestAxis axis = TestAxis.X;

    [Header("相對正常姿勢旋轉幾度")]
    [Range(-180f, 180f)]
    public float angle = 0f;

    private Quaternion baseRotation;

    void Start()
    {
        // 記住按下 Play 時的正常姿勢
        baseRotation = transform.localRotation;
    }

    void Update()
    {
        Vector3 rotateAxis;

        switch (axis)
        {
            case TestAxis.X:
                rotateAxis = Vector3.right;
                break;

            case TestAxis.Y:
                rotateAxis = Vector3.up;
                break;

            default:
                rotateAxis = Vector3.forward;
                break;
        }

        transform.localRotation =
            baseRotation * Quaternion.AngleAxis(angle, rotateAxis);
    }
}