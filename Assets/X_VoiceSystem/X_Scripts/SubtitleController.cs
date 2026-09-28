using UnityEngine;
using TMPro;

public class SubtitleController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]

    // 整個半透明字幕背板
    public GameObject subtitlePanel;

    // TMP 字幕文字
    public TMP_Text subtitleText;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        // 遊戲開始時不要顯示字幕
        HideSubtitle();
    }


    // =========================================================
    // 開始一段新的 AI 字幕
    // =========================================================

    public void StartSubtitle()
    {
        if (subtitleText != null)
        {
            subtitleText.text = "";
        }

        if (subtitlePanel != null)
        {
            subtitlePanel.SetActive(true);
        }
    }


    // =========================================================
    // 更新字幕
    //
    // 之後 GPT Streaming 每產生一些文字，
    // 就會呼叫這個。
    // =========================================================

    public void SetText(string text)
    {
        if (subtitleText == null)
        {
            return;
        }

        subtitleText.text = text;

        // 有文字才顯示背板
        if (
            subtitlePanel != null &&
            !string.IsNullOrWhiteSpace(text)
        )
        {
            subtitlePanel.SetActive(true);
        }
    }


    // =========================================================
    // 隱藏字幕
    // =========================================================

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
    }
}