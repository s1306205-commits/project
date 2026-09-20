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
    // Barge-in 設定
    // =========================================================

    // 玩家語音至少持續多久才算插話
    private const float BargeInConfirmSeconds = 0.35f;

    // 至少辨識出幾個字
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


        // ★ Barge-in 2.0
        //
        // 不再使用 SpeechDetected。
        // 直接看 partial transcript 的持續變化。
        sttService.PartialTranscriptReceived +=
            HandlePartialTranscript;


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
        // Barge-in 模式：
        //
        // AI 說話時麥克風也必須持續送 STT，
        // 否則不可能偵測玩家插話。
        // =====================================================

        sttService?.UpdateMicrophone();


        // =====================================================
        // 檢查 Barge-in candidate 持續時間
        // =====================================================

        CheckBargeInCandidate();
    }


    // =========================================================
    // Partial Transcript
    // =========================================================

    private void HandlePartialTranscript(
        string partial)
    {
        if (
            string.IsNullOrWhiteSpace(
                partial
            )
        )
        {
            return;
        }


        // =====================================================
        // AI 沒有真的播放聲音
        //
        // 不需要進行插話判斷。
        // =====================================================

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
        // 第一次收到 candidate
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
        // Partial 有繼續成長
        //
        // 例如：
        //
        // 等
        // 等一下
        // 等一下我
        //
        // 才算真的有持續說話跡象。
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
        else if (
            cleanPartial !=
            lastBargeInPartial
        )
        {
            // =================================================
            // STT 有時不是單純 append，
            // 而是修改前面的辨識結果。
            //
            // 例如：
            //
            // 等一
            // 等一下
            //
            // 這仍然算有效更新。
            // =================================================

            bargeInPartialGrowthCount++;


            lastBargeInPartial =
                cleanPartial;
        }
    }


    // =========================================================
    // 每幀確認 Barge-in
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


        // AI 已經沒在播放
        if (!IsAiActuallySpeaking())
        {
            ResetBargeInCandidate();

            return;
        }


        float duration =
            Time.realtimeSinceStartup -
            bargeInCandidateStartTime;


        // =====================================================
        // 條件 1：
        // 至少持續 350 ms
        // =====================================================

        if (
            duration <
            BargeInConfirmSeconds
        )
        {
            return;
        }


        // =====================================================
        // 條件 2：
        // 至少 2 個字
        // =====================================================

        if (
            lastBargeInPartial.Length <
            BargeInMinCharacters
        )
        {
            return;
        }


        // =====================================================
        // 條件 3：
        // Partial 至少有繼續更新一次
        //
        // 避免單一殘留 partial：
        //
        // 「今天」
        //
        // 放著 350ms 就誤觸。
        // =====================================================

        if (
            bargeInPartialGrowthCount <
            1
        )
        {
            return;
        }


        // =====================================================
        // 通過全部條件
        // =====================================================

        bargeInTriggered = true;


        Debug.Log(
            $"🛑 Barge-in 確認：" +
            $"{lastBargeInPartial} " +
            $"({duration * 1000f:F0} ms)"
        );


        _ = InterruptCurrentResponseAsync();
    }


    // =========================================================
    // AI 是否「真的正在播放」
    // =========================================================

    private bool IsAiActuallySpeaking()
    {
        if (audioSource == null)
        {
            return false;
        }


        // =====================================================
        // 這裡不使用 conversationBusy。
        //
        // conversationBusy 包含：
        //
        // GPT 思考
        // TTS WebSocket 連線
        // TTS 產生
        //
        // 但那些都不代表 AI 已經從喇叭發出聲音。
        // =====================================================

        return
            audioSource.isPlaying &&
            ttsService != null &&
            ttsService.IsSpeaking;
    }


    // =========================================================
    // 中斷目前 AI
    // =========================================================

    private async Task InterruptCurrentResponseAsync()
    {
        try
        {
            Debug.Log(
                "✋ 正在中斷 AI..."
            );


            // =================================================
            // 1. Cancel GPT
            // =================================================

            conversationCancellation?
                .Cancel();


            // =================================================
            // 2. 清除尚未送出去的 GPT 文字
            // =================================================

            ttsTextBuffer.Clear();


            // =================================================
            // 3. Stop TTS
            // =================================================

            if (ttsService != null)
            {
                await ttsService
                    .StopSessionAsync();
            }


            // =================================================
            // ★ 非常重要
            //
            // 不要：
            //
            // sttService.SkipCurrentMicrophoneData();
            //
            // 因為現在麥克風裡面正是玩家插話。
            // =================================================


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
        // 上一輪正在取消
        //
        // 等它完全離開。
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
        // 新的一輪 CancellationToken
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


            token.ThrowIfCancellationRequested();


            ttsTextBuffer.Clear();


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


            token.ThrowIfCancellationRequested();


            // =================================================
            // Flush 剩餘文字
            // =================================================

            await FlushRemainingTextAsync(
                token
            );


            token.ThrowIfCancellationRequested();


            // =================================================
            // TTS Flush
            // =================================================

            await ttsService
                .FlushAsync();


            token.ThrowIfCancellationRequested();


            await ttsService
                .FinishInputAsync();


            Debug.Log(
                $"🤖 AI 完整回答：{fullReply}"
            );


            // =================================================
            // 等聲音真正播放完成
            // =================================================

            await ttsService
                .WaitUntilFinishedAsync();


            token.ThrowIfCancellationRequested();


            Debug.Log(
                "✅ 本輪對話完成"
            );
        }
        catch (OperationCanceledException)
        {
            ttsTextBuffer.Clear();


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
    // Reset Barge-in Candidate
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