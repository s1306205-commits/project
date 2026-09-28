using UnityEngine;

public class KiwiAnimationController : MonoBehaviour
{
    // =========================================================
    // Animation States
    // =========================================================

    public enum KiwiState
    {
        Idle = 0,
        Talk = 1,
        Think = 2,
        Fly = 3
    }


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

    // 至少辨識到幾個字，
    // 才認為玩家真的開始講話
    public int minimumSpeechCharacters = 3;


    // =========================================================
    // Private Variables
    // =========================================================

    // Animator 現在實際播放的動畫
    private KiwiState currentAnimationState = KiwiState.Idle;


    // 如果現在沒有 Fly，
    // Kiwi 原本應該播放的動畫
    //
    // 例如：
    // AI正在講話 → Talk
    // 玩家講話 / 等待AI → Think
    private KiwiState conversationState = KiwiState.Idle;


    // 是否已經開始過第一次對話
    //
    // false：
    // 遊戲剛開始 → Idle
    //
    // true：
    // 已經開始對話 → 不再回 Idle
    private bool conversationHasStarted = false;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        // 遊戲剛開始
        conversationHasStarted = false;

        conversationState = KiwiState.Idle;

        ForceAnimationState(KiwiState.Idle);
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // =====================================================
        // Fly 最高優先權
        //
        // 只要玩家正在移動，
        // 不管目前是 Talk / Think / Idle，
        // Kiwi 都只能播放 Fly。
        // =====================================================

        if (
            playerMovement != null &&
            playerMovement.IsMoving
        )
        {
            SetAnimationState(KiwiState.Fly);

            return;
        }


        // =====================================================
        // 玩家沒有移動
        //
        // 回到 Kiwi 原本應該播放的動畫
        // =====================================================

        SetAnimationState(conversationState);
    }


    // =========================================================
    // 玩家開始說話
    // =========================================================

    public void OnPlayerSpeechPartial(string text)
    {
        // 沒有文字
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }


        string cleanText = text.Trim();


        // =====================================================
        // 太短的辨識結果不算正式開始對話
        //
        // 避免「嗯」、「啊」或雜音誤觸發
        // =====================================================

        if (cleanText.Length < minimumSpeechCharacters)
        {
            return;
        }


        // =====================================================
        // 第一次有效對話開始
        // =====================================================

        conversationHasStarted = true;


        // =====================================================
        // 玩家正在說話
        //
        // Kiwi 是在「聽玩家講話」，
        // 所以播放 Think，
        // 不是 Talk。
        // =====================================================

        conversationState = KiwiState.Think;
    }


    // =========================================================
    // 玩家說完話
    // =========================================================

    public void OnPlayerSpeechFinal(string text)
    {
        conversationHasStarted = true;


        // =====================================================
        // 玩家講完後，
        // GPT / TTS 還在準備回答。
        //
        // 所以 Kiwi 繼續 Think。
        // =====================================================

        conversationState = KiwiState.Think;
    }


    // =========================================================
    // AI 真正開始播放語音
    // =========================================================

    public void OnAiSpeechStarted()
    {
        conversationHasStarted = true;


        // =====================================================
        // 只有 Kiwi 自己真的開始講話，
        // 才播放 Talk。
        // =====================================================

        conversationState = KiwiState.Talk;

        Debug.Log("🐦 AI 開始發聲 → Talk");
    }


    // =========================================================
    // AI 語音完全播放完成
    // =========================================================

    public void OnAiSpeechFinished()
    {
        conversationHasStarted = true;


        // =====================================================
        // AI 講完之後，
        // Kiwi 等待玩家下一句。
        //
        // 不再回 Idle，
        // 而是進入 Think。
        // =====================================================

        conversationState = KiwiState.Think;

        Debug.Log("🐦 AI 語音結束 → Think");
    }


    // =========================================================
    // 玩家在 AI 說話時插話
    // =========================================================

    public void OnPlayerBargeIn()
    {
        conversationHasStarted = true;


        // =====================================================
        // 玩家開始插話後，
        // Kiwi 停止自己說話，
        // 改成聽玩家說話。
        //
        // 所以是 Think。
        // =====================================================

        conversationState = KiwiState.Think;

        Debug.Log("🐦 玩家插話 → Think");
    }


    // =========================================================
    // Reset Character
    //
    // 如果未來重新開始場景，
    // 可以呼叫這個方法。
    //
    // 會真正回到最初的 Idle。
    // =========================================================

    public void ResetCharacter()
    {
        conversationHasStarted = false;

        conversationState = KiwiState.Idle;

        ForceAnimationState(KiwiState.Idle);
    }


    // =========================================================
    // Set Animation State
    //
    // 真正控制 Animator
    // =========================================================

    private void SetAnimationState(KiwiState newState)
    {
        // =====================================================
        // 如果已經在播放這個動畫，
        // 就不要每一幀重複設定。
        // =====================================================

        if (currentAnimationState == newState)
        {
            return;
        }


        currentAnimationState = newState;


        // =====================================================
        // 傳送 State 給 Animator
        // =====================================================

        if (animator != null)
        {
            animator.SetInteger(
                "State",
                (int)newState
            );
        }


        Debug.Log(
            $"🐦 Kiwi Animation → {newState}"
        );
    }


    // =========================================================
    // Force Animation State
    //
    // Start / Reset 使用。
    //
    // 不管目前是不是同一個 State，
    // 都強制傳給 Animator。
    // =========================================================

    private void ForceAnimationState(KiwiState newState)
    {
        currentAnimationState = newState;


        if (animator != null)
        {
            animator.SetInteger(
                "State",
                (int)newState
            );
        }


        Debug.Log(
            $"🐦 Kiwi Animation → {newState}"
        );
    }
}