using UnityEngine;

public class KiwiAnimationController : MonoBehaviour
{
    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    public Animator animator;

    public PlayerMovement playerMovement;


    // =========================================================
    // Speech Settings
    // =========================================================

    [Header("Speech Settings")]

    // 至少辨識到幾個字
    // 才認為玩家真的開始一輪對話
    public int minimumSpeechCharacters = 3;


    // =========================================================
    // Private
    // =========================================================

    private bool isTalking = false;

    // 一輪對話是否正在進行
    private bool conversationActive = false;


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // =====================================================
        // 對話進行中：
        // Talk 優先，不因為 Player 移動而切掉
        // =====================================================

        if (conversationActive)
        {
            return;
        }


        // =====================================================
        // 沒有對話時：
        // 玩家移動目前先維持 Idle
        //
        // 之後有 Fly 動畫，
        // 再把這裡改成 Fly。
        // =====================================================

        if (
            playerMovement != null &&
            playerMovement.IsMoving
        )
        {
            ForceIdle();
        }
    }


    // =========================================================
    // 玩家說話 Partial
    // =========================================================

    public void OnPlayerSpeechPartial(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }


        string cleanText =
            text.Trim();


        // =====================================================
        // 太短的不算
        //
        // 嗯、啊、喔...
        // =====================================================

        if (
            cleanText.Length <
            minimumSpeechCharacters
        )
        {
            return;
        }


        // =====================================================
        // 確認玩家開始一輪有效對話
        // =====================================================

        StartConversation();
    }


    // =========================================================
    // 玩家 Final
    // =========================================================

    public void OnPlayerSpeechFinal(string text)
    {
        // =====================================================
        // ★ 這裡現在不要回 Idle
        //
        // 玩家講完之後，
        // 還要等 AI 回答播放完成。
        // =====================================================

        if (!conversationActive)
        {
            StartConversation();
        }
    }


    // =========================================================
    // 開始一輪對話
    // =========================================================

    public void StartConversation()
    {
        conversationActive = true;

        StartTalking();
    }


    // =========================================================
    // AI 回答真正播放完成
    // =========================================================

    public void EndConversation()
    {
        conversationActive = false;

        StopTalking();

        Debug.Log(
            "🐦 對話完成 → Kiwi Idle"
        );
    }


    // =========================================================
    // Talk
    // =========================================================

    private void StartTalking()
    {
        if (isTalking)
        {
            return;
        }


        isTalking = true;


        if (animator != null)
        {
            animator.SetBool(
                "IsTalking",
                true
            );
        }


        Debug.Log(
            "🐦 Kiwi → Talk"
        );
    }


    // =========================================================
    // Idle
    // =========================================================

    private void StopTalking()
    {
        isTalking = false;


        if (animator != null)
        {
            animator.SetBool(
                "IsTalking",
                false
            );
        }


        Debug.Log(
            "🐦 Kiwi → Idle"
        );
    }


    // =========================================================
    // 強制 Idle
    // =========================================================

    public void ForceIdle()
    {
        conversationActive = false;

        StopTalking();
    }
}