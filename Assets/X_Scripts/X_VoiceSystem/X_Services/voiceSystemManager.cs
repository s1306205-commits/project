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

    [Header("Companion")]
    public CompanionManager companionManager;

    private OpenAIService kiwiAIService;
    private OpenAIService dogAIService;

    private int activeCompanionIndex = -1;
    private bool switchingCompanion = false;

    // 防止舊的非同步切換覆蓋新的角色
    private int companionChangeVersion = 0;

    private const string KiwiPrompt = @"
    你是一隻名叫 Kiwi 的奇異鳥，
    在 VR 森林中陪伴玩家聊天。

    你的個性溫柔、沉穩、善於傾聽。
    玩家難過時，先理解玩家的感受。

    使用繁體中文（台灣）。
    每次回答 1～2 句，盡量不超過 40 字。
    自然口語，一次最多問一個問題。
    不主動診斷心理疾病。
    ";

    private const string DogPrompt = @"
    你是一隻可愛、活潑、親切的小狗，
    正在 VR 森林裡陪伴玩家聊天。

    你喜歡鼓勵玩家，也願意認真傾聽。
    玩家難過時先理解感受，不急著說教。

    使用繁體中文（台灣）。
    每次回答 1～2 句，盡量不超過 40 字。
    自然口語，一次最多問一個問題。
    不要一直汪汪叫。
    不主動診斷心理疾病。
    ";


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

    //


    private void HandleCompanionChanged(int index)
    {
        companionChangeVersion++;

        // 立即封鎖舊角色對話
        switchingCompanion = true;
        activeCompanionIndex = -1;
        openAIService = null;

        // 取消上一輪 AI 請求
        conversationCancellation?.Cancel();

        if (!changeWorkerRunning)
        {
            _ = ProcessCompanionChangesAsync();
        }
    }

    private bool changeWorkerRunning = false;

    private async Task ProcessCompanionChangesAsync()
    {
        changeWorkerRunning = true;

        try
        {
            while (true)
            {
                int version = companionChangeVersion;

                // 取消舊角色的 TTS
                if (ttsService != null)
                {
                    await ttsService.StopSessionAsync();
                }

                // 等待舊對話完成
                while (conversationBusy)
                {
                    await Task.Yield();
                }

                // 等待期間如果又切換了角色，
                // 重新處理最新要求
                if (version != companionChangeVersion)
                    continue;

                ttsTextBuffer.Clear();
                subtitleBuffer.Clear();

                lock (subtitleLock)
                {
                    pendingSubtitleText = "";
                    subtitleUpdatePending = false;
                }

                subtitleController?.HideSubtitle();

                aiTalkAnimationStarted = false;
                bargeInTriggered = false;
                ResetBargeInCandidate();

                int index = companionManager != null
                    ? companionManager.CurrentIndex
                    : -1;

                switch (index)
                {
                    case 0:
                        openAIService = kiwiAIService;
                        kiwiAnimation?.ResetCharacter();
                        Debug.Log("🐦 已切換 Kiwi AI");
                        break;

                    case 1:
                        openAIService = dogAIService;
                        Debug.Log("🐶 已切換 Dog AI");
                        break;

                    default:
                        openAIService = null;
                        Debug.Log("目前沒有選擇角色");
                        break;
                }

                activeCompanionIndex = index;

                sttService?.SkipCurrentMicrophoneData();

                ignorePlayerSpeechUntil =
                    Time.realtimeSinceStartup + 0.5f;

                break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "角色切換失敗：" + ex.Message
            );
        }
        finally
        {
            changeWorkerRunning = false;
            switchingCompanion = false;

            // 避免切換期間又收到新的角色選擇
            int latestIndex = companionManager != null
                ? companionManager.CurrentIndex
                : -1;

            if (activeCompanionIndex != latestIndex)
            {
                HandleCompanionChanged(latestIndex);
            }
        }
    }




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


        kiwiAIService = new OpenAIService(
            OpenAIKey,
            KiwiPrompt
        );

        dogAIService = new OpenAIService(
            OpenAIKey,
            DogPrompt
        );

        // 尚未選擇角色時不提供 AI 對話
        openAIService = null;



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

            if (companionManager != null)
            {
                companionManager.OnCompanionChanged +=
                    HandleCompanionChanged;

                // 同步目前角色
                HandleCompanionChanged(
                    companionManager.CurrentIndex
                );
            }
            else
            {
                Debug.LogError(
                    "VoiceSystemManager 尚未指定 CompanionManager"
                );
            }

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


        // =====================================================
        // STT
        // =====================================================

        if (sttService != null)
        {
            // AI 說話期間不要讓 STT 自動強制 Commit
            sttService.SetAutoCommitAllowed(
                !IsAiActuallySpeaking()
            );

            sttService.UpdateMicrophone();
        }


        // =====================================================
        // Barge-in
        // =====================================================

        CheckBargeInCandidate();


        // =====================================================
        // AI Talk Animation
        // =====================================================

        if (
            IsAiActuallySpeaking() &&
            !aiTalkAnimationStarted
        )
        {
            aiTalkAnimationStarted = true;


            if (activeCompanionIndex == 0)
                kiwiAnimation?.OnAiSpeechStarted();
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
            if (activeCompanionIndex == 0)
                kiwiAnimation?.OnPlayerSpeechPartial(partial);
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


        if (activeCompanionIndex == 0)
            kiwiAnimation?.OnPlayerBargeIn();


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
        if (switchingCompanion ||
            activeCompanionIndex < 0 ||
            openAIService == null)
        {
            return;
        }
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


        if (activeCompanionIndex == 0)
            kiwiAnimation?.OnPlayerSpeechFinal(text);


        // =====================================================
        // 如果上一輪正在取消
        // 等上一輪完全離開
        // =====================================================


        if (conversationBusy)
        {
            Debug.Log("⏳ 等待上一輪 AI 回答結束...");

            while (conversationBusy)
            {
                await Task.Yield();
            }
        }

        // 無論是否有等待，都重新確認
        if (switchingCompanion ||
            activeCompanionIndex < 0 ||
            openAIService == null)
        {
            return;
        }

        // 固定這輪所使用的 AI 角色
        OpenAIService selectedAIService = openAIService;



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
                await selectedAIService.AskStreamingAsync(
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

            if (activeCompanionIndex == 0)
                kiwiAnimation?.OnAiSpeechFinished();


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
        if (companionManager != null)
        {
            companionManager.OnCompanionChanged -=
                HandleCompanionChanged;
        }
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