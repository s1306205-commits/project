using UnityEngine;

public class KiwiAnimationController : MonoBehaviour
{
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

    // VR 玩家移動狀態
    public VRPlayerMovementState playerMovement;


    // =========================================================
    // Speech Settings
    // =========================================================

    [Header("Speech Settings")]

    public int minimumSpeechCharacters = 3;


    // =========================================================
    // Private
    // =========================================================

    private KiwiState currentAnimationState =
        KiwiState.Idle;

    private KiwiState conversationState =
        KiwiState.Idle;

    private bool conversationHasStarted =
        false;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        conversationHasStarted = false;

        conversationState =
            KiwiState.Idle;

        ForceAnimationState(
            KiwiState.Idle
        );
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // Fly 最高優先權
        if (
            playerMovement != null &&
            playerMovement.IsMoving
        )
        {
            SetAnimationState(
                KiwiState.Fly
            );

            return;
        }


        // 沒移動就回對話狀態
        SetAnimationState(
            conversationState
        );
    }


    // =========================================================
    // Player Speech Partial
    // =========================================================

    public void OnPlayerSpeechPartial(
        string text)
    {
        if (
            string.IsNullOrWhiteSpace(
                text
            )
        )
        {
            return;
        }


        string cleanText =
            text.Trim();


        if (
            cleanText.Length <
            minimumSpeechCharacters
        )
        {
            return;
        }


        conversationHasStarted = true;

        conversationState =
            KiwiState.Think;
    }


    // =========================================================
    // Player Speech Final
    // =========================================================

    public void OnPlayerSpeechFinal(
        string text)
    {
        conversationHasStarted = true;

        conversationState =
            KiwiState.Think;
    }


    // =========================================================
    // AI Speech Started
    // =========================================================

    public void OnAiSpeechStarted()
    {
        conversationHasStarted = true;

        conversationState =
            KiwiState.Talk;


        Debug.Log(
            "🐦 AI 開始發聲 → Talk"
        );
    }


    // =========================================================
    // AI Speech Finished
    // =========================================================

    public void OnAiSpeechFinished()
    {
        conversationHasStarted = true;

        conversationState =
            KiwiState.Think;


        Debug.Log(
            "🐦 AI 語音結束 → Think"
        );
    }


    // =========================================================
    // Barge-in
    // =========================================================

    public void OnPlayerBargeIn()
    {
        conversationHasStarted = true;

        conversationState =
            KiwiState.Think;


        Debug.Log(
            "🐦 玩家插話 → Think"
        );
    }


    // =========================================================
    // Reset
    // =========================================================

    public void ResetCharacter()
    {
        conversationHasStarted = false;

        conversationState =
            KiwiState.Idle;


        ForceAnimationState(
            KiwiState.Idle
        );
    }


    // =========================================================
    // Set Animation
    // =========================================================

    private void SetAnimationState(
        KiwiState newState)
    {
        if (
            currentAnimationState ==
            newState
        )
        {
            return;
        }


        currentAnimationState =
            newState;


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
    // Force Animation
    // =========================================================

    private void ForceAnimationState(
        KiwiState newState)
    {
        currentAnimationState =
            newState;


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