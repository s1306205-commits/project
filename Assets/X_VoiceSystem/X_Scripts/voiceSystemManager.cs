using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class VoiceSystemManager : MonoBehaviour
{
    // =========================================================
    // API Keys
    // =========================================================

    private const string OpenAIKey =
        "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";

    private const string ElevenLabsKey =
        "sk_ad23b99ade2545ba6d37d75eba786465b7a14264699dc33c";

    private const string VoiceId =
        "QoWyx69qbz2L0PN6Q4bK";


    // =========================================================
    // Character Animation
    // =========================================================

    [Header("Character Animation")]

    public KiwiAnimationController kiwiAnimation;


    // =========================================================
    // Subtitle
    // =========================================================

    [Header("Subtitle")]

    public SubtitleController subtitleController;


    // GPT 完整字幕
    private readonly StringBuilder subtitleBuffer =
        new StringBuilder();

    // GPT Streaming 可能不在 Unity Main Thread。
    // 所以先把最新字幕存起來，
    // 再由 Update() 真正更新 Unity UI。
    private readonly object subtitleLock =
        new object();

    private string pendingSubtitleText = "";

    private bool subtitleUpdatePending = false;


    // AI 說完之後字幕繼續停留多久
    private const float SubtitleRemainSeconds = 1.2f;


    // =========================================================
    // Barge-in 設定
    // =========================================================

    private const float BargeInConfirmSeconds = 0.35f;

    private const int BargeInMinCharacters = 2;


    // =========================================================
    // Services
    // =========================================================

    private OpenAIService openAIService;

    private ElevenLabsRealtimeSttService sttService;

    private ElevenLabsStreamingTtsService ttsService;

    private AudioSource audioSource;


    // =========================================================
    // Conversation
    // =========================================================

    private CancellationTokenSource conversationCancellation;

    private bool conversationBusy = false;

    private bool bargeInTriggered = false;

    private bool aiTalkAnimationStarted = false;


    // =========================================================
    // Barge-in Candidate
    // =========================================================

    private bool bargeInCandidateActive = false;

    private float bargeInCandidateStartTime = 0f;

    private string lastBargeInPartial = "";

    private int bargeInPartialGrowthCount = 0;


    // =========================================================
    // GPT → TTS Buffer
    // =========================================================

    private readonly StringBuilder ttsTextBuffer =
        new StringBuilder();

    private readonly SemaphoreSlim ttsTextSendLock =
        new SemaphoreSlim(1, 1);


    // =========================================================
    // AI 說完後的 STT 防誤觸
    // =========================================================

    // AI 說完後多久內，不允許 STT 開啟新的玩家對話
    private const float PostTtsIgnoreSeconds = 0.5f;

    private float ignorePlayerSpeechUntil = 0f;


    // =========================================================
    // Start
    // =========================================================

    private async void Start()
    {
        Debug.Log(
            "🚀 Unity Voice System 啟動中..."
        );


        if (
            OpenAIKey.Contains("在這裡") ||
            ElevenLabsKey.Contains("在這裡")
        )
        {
            Debug.LogError(
                "❌ 請先填入 API Key"
            );

            return;
        }


        // =====================================================
        // AudioSource
        // =====================================================

        audioSource =
            GetComponent<AudioSource>();


        if (audioSource == null)
        {
            Debug.LogError(
                "❌ X_VoiceSystem 沒有 AudioSource！"
            );

            return;
        }


        audioSource.playOnAwake = false;


        // =====================================================
        // Services
        // =====================================================

        openAIService =
            new OpenAIService(
                OpenAIKey
            );


        sttService =
            new ElevenLabsRealtimeSttService(
                ElevenLabsKey
            );


        ttsService =
            new ElevenLabsStreamingTtsService(
                ElevenLabsKey,
                VoiceId,
                audioSource
            );


        // =====================================================
        // STT Events
        // =====================================================

        sttService.FinalTranscriptReceived +=
            HandleFinalTranscriptAsync;


        sttService.PartialTranscriptReceived +=
            HandlePartialTranscript;


        // =====================================================
        // 字幕初始隱藏
        // =====================================================

        subtitleBuffer.Clear();

        subtitleController?.
            HideSubtitle();


        // =====================================================
        // Start STT
        // =====================================================

        try
        {
            await sttService.StartAsync();


            Debug.Log(
                "🎤 可以開始說話了！"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ STT 啟動失敗：{ex.Message}"
            );
        }
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // =====================================================
        // Subtitle Main Thread Update
        // =====================================================

        if (subtitleUpdatePending)
        {
            string textToShow;

            lock (subtitleLock)
            {
                textToShow =
                    pendingSubtitleText;

                subtitleUpdatePending =
                    false;
            }

            if (
                !string.IsNullOrWhiteSpace(
                    textToShow
                )
            )
            {
                subtitleController?.
                    SetText(
                        textToShow
                    );
            }
        }


        sttService?.
            UpdateMicrophone();


        CheckBargeInCandidate();


        if (
            IsAiActuallySpeaking() &&
            !aiTalkAnimationStarted
        )
        {
            aiTalkAnimationStarted = true;


            kiwiAnimation?.
                OnAiSpeechStarted();
        }
    }


    // =========================================================
    // Partial Transcript
    // =========================================================

    private void HandlePartialTranscript(
        string partial)
    {
        // =====================================================
        // 空文字直接忽略
        // =====================================================

        if (
            string.IsNullOrWhiteSpace(
                partial
            )
        )
        {
            return;
        }


        // =====================================================
        // 玩家語音動畫判斷
        //
        // 只有：
        // 1. AI 沒有正在說話
        // 2. AI 說完後的冷卻期已經結束
        //
        // 才允許 Partial 開啟新的玩家對話。
        // =====================================================

        bool canStartPlayerConversation =
            !IsAiActuallySpeaking()
            &&
            Time.realtimeSinceStartup >=
            ignorePlayerSpeechUntil;


        if (canStartPlayerConversation)
        {
            kiwiAnimation?.
                OnPlayerSpeechPartial(
                    partial
                );
        }


        // =====================================================
        // 以下保持原本 Barge-in 2.0
        // =====================================================


        // AI 沒有真的播放聲音
        // 就不需要判斷是不是插話
        if (!IsAiActuallySpeaking())
        {
            ResetBargeInCandidate();

            return;
        }


        if (bargeInTriggered)
        {
            return;
        }


        string cleanPartial =
            partial.Trim();


        // =====================================================
        // 第一次收到插話候選
        // =====================================================

        if (!bargeInCandidateActive)
        {
            bargeInCandidateActive = true;


            bargeInCandidateStartTime =
                Time.realtimeSinceStartup;


            lastBargeInPartial =
                cleanPartial;


            bargeInPartialGrowthCount = 0;


            Debug.Log(
                $"🎧 插話候選：{cleanPartial}"
            );


            return;
        }


        // =====================================================
        // Partial 有繼續增加
        // =====================================================

        if (
            cleanPartial.Length >
            lastBargeInPartial.Length
        )
        {
            bargeInPartialGrowthCount++;


            lastBargeInPartial =
                cleanPartial;


            float duration =
                Time.realtimeSinceStartup -
                bargeInCandidateStartTime;


            Debug.Log(
                $"🎧 插話候選：{cleanPartial} " +
                $"({duration * 1000f:F0} ms)"
            );
        }

        // =====================================================
        // STT 修改原本辨識結果
        // =====================================================

        else if (
            cleanPartial !=
            lastBargeInPartial
        )
        {
            bargeInPartialGrowthCount++;


            lastBargeInPartial =
                cleanPartial;
        }
    }


    // =========================================================
    // Barge-in 判斷
    // =========================================================

    private void CheckBargeInCandidate()
    {
        if (!bargeInCandidateActive)
        {
            return;
        }


        if (bargeInTriggered)
        {
            return;
        }


        if (!IsAiActuallySpeaking())
        {
            ResetBargeInCandidate();

            return;
        }


        float duration =
            Time.realtimeSinceStartup -
            bargeInCandidateStartTime;


        // 至少持續 350ms
        if (
            duration <
            BargeInConfirmSeconds
        )
        {
            return;
        }


        // 至少 2 個字
        if (
            lastBargeInPartial.Length <
            BargeInMinCharacters
        )
        {
            return;
        }


        // Partial 至少更新過一次
        if (
            bargeInPartialGrowthCount < 1
        )
        {
            return;
        }


        // =====================================================
        // Barge-in 成立
        // =====================================================

        bargeInTriggered = true;


        kiwiAnimation?.
            OnPlayerBargeIn();


        Debug.Log(
            $"🛑 Barge-in 確認：" +
            $"{lastBargeInPartial} " +
            $"({duration * 1000f:F0} ms)"
        );


        _ = InterruptCurrentResponseAsync();
    }


    // =========================================================
    // AI 是否真的正在播放
    // =========================================================

    private bool IsAiActuallySpeaking()
    {
        if (audioSource == null)
        {
            return false;
        }


        return
            audioSource.isPlaying &&
            ttsService != null &&
            ttsService.IsSpeaking;
    }


    // =========================================================
    // 中斷 AI
    // =========================================================

    private async Task
        InterruptCurrentResponseAsync()
    {
        try
        {
            Debug.Log(
                "✋ 正在中斷 AI..."
            );


            // =================================================
            // Cancel GPT
            // =================================================

            conversationCancellation?
                .Cancel();


            // =================================================
            // 清除尚未送出去的 TTS 文字
            // =================================================

            ttsTextBuffer.Clear();


            // =================================================
            // 清除被打斷的 AI 字幕
            // =================================================

            subtitleBuffer.Clear();


            subtitleController?.
                HideSubtitle();


            // =================================================
            // Stop TTS
            // =================================================

            if (ttsService != null)
            {
                await ttsService
                    .StopSessionAsync();
            }


            // Barge-in 時不要 SkipCurrentMicrophoneData()
            // 因為現在正在收玩家的插話。


            ResetBargeInCandidate();


            Debug.Log(
                "✋ AI 已停止，繼續聽玩家..."
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ Barge-in 中斷錯誤：" +
                $"{ex.Message}"
            );
        }
    }


    // =========================================================
    // Final Transcript
    // =========================================================

    private async Task HandleFinalTranscriptAsync(
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


        // =====================================================
        // AI 剛說完產生的殘留 STT
        // 不要拿去問 GPT
        // =====================================================

        if (
            Time.realtimeSinceStartup <
            ignorePlayerSpeechUntil
        )
        {
            Debug.Log(
                $"🔇 忽略 AI 殘留 STT：{text}"
            );

            return;
        }


        kiwiAnimation?.
            OnPlayerSpeechFinal(
                text
            );


        // =====================================================
        // 如果上一輪正在取消
        // 等上一輪完全離開
        // =====================================================

        if (conversationBusy)
        {
            Debug.Log(
                "⏳ 等待上一輪 AI 回答結束..."
            );


            while (conversationBusy)
            {
                await Task.Yield();
            }
        }


        Debug.Log(
            $"👤 玩家：{text}"
        );


        conversationBusy = true;

        bargeInTriggered = false;

        ResetBargeInCandidate();


        // =====================================================
        // 新 CancellationToken
        // =====================================================

        conversationCancellation?
            .Dispose();


        conversationCancellation =
            new CancellationTokenSource();


        CancellationToken token =
            conversationCancellation.Token;


        try
        {
            // =================================================
            // Start TTS WebSocket
            // =================================================

            await ttsService
                .StartSessionAsync();


            token
                .ThrowIfCancellationRequested();


            // =================================================
            // 清除 TTS Buffer
            // =================================================

            ttsTextBuffer.Clear();


            // =================================================
            // 準備新的 AI 字幕
            // =================================================

            subtitleBuffer.Clear();


            subtitleController?.
                HideSubtitle();


            Debug.Log(
                "🧠 GPT Streaming..."
            );


            // =================================================
            // GPT Streaming
            // =================================================

            string fullReply =
                await openAIService
                    .AskStreamingAsync(
                        text,

                        delta =>
                        {
                            if (
                                !token
                                    .IsCancellationRequested
                            )
                            {
                                HandleGptDelta(
                                    delta,
                                    token
                                );
                            }
                        },

                        token
                    );


            token
                .ThrowIfCancellationRequested();


            // =================================================
            // Flush 剩餘文字
            // =================================================

            await FlushRemainingTextAsync(
                token
            );


            token
                .ThrowIfCancellationRequested();


            // =================================================
            // TTS Flush
            // =================================================

            await ttsService
                .FlushAsync();


            token
                .ThrowIfCancellationRequested();


            await ttsService
                .FinishInputAsync();


            Debug.Log(
                $"🤖 AI 完整回答：{fullReply}"
            );


            // =================================================
            // 等 TTS 真正播完
            // =================================================

            await ttsService
                .WaitUntilFinishedAsync();


            token
                .ThrowIfCancellationRequested();


            // =================================================
            // AI 聲音真正播放完成
            // =================================================

            aiTalkAnimationStarted = false;


            // =================================================
            // 清除 AI 說話期間累積的麥克風資料
            // =================================================

            sttService?.
                SkipCurrentMicrophoneData();


            // =================================================
            // 防止 AI 剛說完的殘留 STT
            // =================================================

            ignorePlayerSpeechUntil =
                Time.realtimeSinceStartup +
                PostTtsIgnoreSeconds;


            // =================================================
            // AI 說完 → Kiwi Think
            // =================================================

            kiwiAnimation?.
                OnAiSpeechFinished();


            // =================================================
            // 字幕繼續停留 1.2 秒
            // =================================================

            await Task.Delay(
                Mathf.RoundToInt(
                    SubtitleRemainSeconds *
                    1000f
                ),
                token
            );


            token
                .ThrowIfCancellationRequested();


            // =================================================
            // 隱藏字幕
            // =================================================

            subtitleController?.
                HideSubtitle();


            subtitleBuffer.Clear();


            Debug.Log(
                "✅ 本輪對話完成"
            );
        }

        catch (OperationCanceledException)
        {
            // =================================================
            // 上一輪被 Barge-in 中斷
            // =================================================

            ttsTextBuffer.Clear();

            subtitleBuffer.Clear();


            subtitleController?.
                HideSubtitle();


            aiTalkAnimationStarted = false;


            Debug.Log(
                "🟡 上一輪 AI 回答已取消"
            );
        }

        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ AI Streaming 對話錯誤：" +
                $"{ex.Message}"
            );


            // 發生錯誤時不要把舊字幕留在畫面
            subtitleBuffer.Clear();


            subtitleController?.
                HideSubtitle();


            aiTalkAnimationStarted = false;


            if (ttsService != null)
            {
                await ttsService
                    .StopSessionAsync();
            }
        }

        finally
        {
            conversationBusy = false;

            ResetBargeInCandidate();
        }
    }


    // =========================================================
    // GPT Delta
    // =========================================================

    private void HandleGptDelta(
        string delta,
        CancellationToken token)
    {
        if (
            token.IsCancellationRequested ||
            string.IsNullOrEmpty(delta)
        )
        {
            return;
        }


        lock (subtitleLock)
        {
            subtitleBuffer.Append(
                delta
            );


            pendingSubtitleText =
                subtitleBuffer.ToString();


            subtitleUpdatePending =
                true;
        }


        // =====================================================
        // TTS Buffer
        // =====================================================

        ttsTextBuffer.Append(
            delta
        );


        string current =
            ttsTextBuffer.ToString();


        if (
            current.Length >= 20 ||
            EndsWithSpeechPunctuation(
                current
            )
        )
        {
            string textToSend =
                current;


            ttsTextBuffer.Clear();


            _ = SendTextToTtsAsync(
                textToSend,
                token
            );
        }
    }


    // =========================================================
    // Send TTS
    // =========================================================

    private async Task SendTextToTtsAsync(
        string text,
        CancellationToken token)
    {
        await ttsTextSendLock
            .WaitAsync();


        try
        {
            if (
                token.IsCancellationRequested ||
                string.IsNullOrWhiteSpace(
                    text
                )
            )
            {
                return;
            }


            Debug.Log(
                $"➡️ TTS：{text}"
            );


            await ttsService
                .SendTextAsync(
                    text
                );
        }

        catch (Exception ex)
        {
            if (
                !token
                    .IsCancellationRequested
            )
            {
                Debug.LogError(
                    $"❌ TTS 文字傳送失敗：" +
                    $"{ex.Message}"
                );
            }
        }

        finally
        {
            ttsTextSendLock
                .Release();
        }
    }


    // =========================================================
    // Flush Remaining
    // =========================================================

    private async Task FlushRemainingTextAsync(
        CancellationToken token)
    {
        await ttsTextSendLock
            .WaitAsync();


        try
        {
            if (
                token.IsCancellationRequested
            )
            {
                return;
            }


            string remaining =
                ttsTextBuffer
                    .ToString();


            ttsTextBuffer.Clear();


            if (
                !string.IsNullOrWhiteSpace(
                    remaining
                )
            )
            {
                Debug.Log(
                    $"➡️ TTS 最後文字：" +
                    $"{remaining}"
                );


                await ttsService
                    .SendTextAsync(
                        remaining
                    );
            }
        }

        finally
        {
            ttsTextSendLock
                .Release();
        }
    }


    // =========================================================
    // Reset Barge-in
    // =========================================================

    private void ResetBargeInCandidate()
    {
        bargeInCandidateActive = false;

        bargeInCandidateStartTime = 0f;

        lastBargeInPartial = "";

        bargeInPartialGrowthCount = 0;
    }


    // =========================================================
    // Punctuation
    // =========================================================

    private bool EndsWithSpeechPunctuation(
        string text)
    {
        if (
            string.IsNullOrEmpty(
                text
            )
        )
        {
            return false;
        }


        char last =
            text[text.Length - 1];


        return
            last == '，' ||
            last == '。' ||
            last == '！' ||
            last == '？' ||
            last == ',' ||
            last == '.' ||
            last == '!' ||
            last == '?' ||
            last == '；' ||
            last == ';';
    }


    // =========================================================
    // Destroy
    // =========================================================

    private async void OnDestroy()
    {
        conversationCancellation?
            .Cancel();


        if (sttService != null)
        {
            sttService.PartialTranscriptReceived -=
                HandlePartialTranscript;


            sttService.FinalTranscriptReceived -=
                HandleFinalTranscriptAsync;
        }


        if (ttsService != null)
        {
            await ttsService
                .StopSessionAsync();
        }


        if (sttService != null)
        {
            await sttService
                .StopAsync();
        }


        conversationCancellation?
            .Dispose();


        ttsTextSendLock.Dispose();
    }
}