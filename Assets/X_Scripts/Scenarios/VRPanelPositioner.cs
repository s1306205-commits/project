
using UnityEngine;

public class VRPanelPositioner : MonoBehaviour
{
    [Header("VR 玩家相機")]
    [SerializeField] private Transform vrCamera;

    [Header("面板位置設定")]
    [SerializeField] private float distance = 1.8f;
    [SerializeField] private float heightOffset = -0.15f;

    public void PlacePanel(Transform panel)
    {
        if (vrCamera == null || panel == null)
            return;

        // 取得玩家水平面朝向
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        // 將面板移到玩家前方
        panel.position =
            vrCamera.position +
            forward * distance +
            Vector3.up * heightOffset;

        // 面板正面朝向玩家
        panel.rotation =
            Quaternion.LookRotation(forward, Vector3.up);
    }
}
