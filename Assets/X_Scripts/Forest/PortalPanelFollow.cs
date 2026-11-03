
using UnityEngine;

public class PortalPanelFollow : MonoBehaviour
{
    [Header("VR 相機")]
    [SerializeField] private Transform vrCamera;

    [Header("面板距離")]
    [SerializeField] private float distance = 1.5f;

    [Header("高度偏移")]
    [SerializeField] private float heightOffset = 0f;

    private void OnEnable()
    {
        // 首次啟用時可能尚未指定相機
        if (vrCamera == null)
        {
            Camera cam = Camera.main;

            if (cam != null)
                vrCamera = cam.transform;
        }

        // 等待第一幀更新後定位
        StartCoroutine(PositionNextFrame());
    }

    private System.Collections.IEnumerator PositionNextFrame()
    {
        yield return null;
        PositionPanel();
    }

    public void PositionPanel()
    {
        if (vrCamera == null)
        {
            Debug.LogWarning("尚未指定 VR Camera");
            return;
        }

        // 使用水平視線方向，避免面板傾斜
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();

        // 放在玩家面前
        transform.position =
            vrCamera.position +
            forward * distance +
            Vector3.up * heightOffset;

        // 讓 Canvas 正面朝向玩家
        transform.rotation =
            Quaternion.LookRotation(forward);
    }
}
