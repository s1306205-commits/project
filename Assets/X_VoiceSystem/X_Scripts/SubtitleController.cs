using UnityEngine;
using TMPro;

public class SubtitleController : MonoBehaviour
{
    [Header("References")]
    public GameObject subtitlePanel;
    public TMP_Text subtitleText;


    private void Start()
    {
        HideSubtitle();
    }


    private void Update()
    {
        // 按 F8 強制測試字幕
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Debug.Log("🧪 F8 字幕測試");

            SetText("這是一段 VR 字幕測試");
        }


        // 按 F9 關閉字幕
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.Log("🧪 F9 關閉字幕");

            HideSubtitle();
        }
    }


    public void SetText(string text)
    {
        Debug.Log(
            $"📝 Subtitle SetText：{text}"
        );


        if (subtitlePanel == null)
        {
            Debug.LogError(
                "❌ Subtitle Panel 沒有指定！"
            );

            return;
        }


        if (subtitleText == null)
        {
            Debug.LogError(
                "❌ Subtitle Text 沒有指定！"
            );

            return;
        }


        subtitleText.text = text;

        subtitlePanel.SetActive(true);


        Debug.Log(
            $"🟢 Subtitle Panel Active = " +
            $"{subtitlePanel.activeSelf}"
        );
    }


    public void HideSubtitle()
    {
        if (subtitleText != null)
        {
            subtitleText.text = "";
        }


        if (subtitlePanel != null)
        {
            subtitlePanel.SetActive(false);
        }


        Debug.Log(
            "⚫ Subtitle Hide"
        );
    }
}