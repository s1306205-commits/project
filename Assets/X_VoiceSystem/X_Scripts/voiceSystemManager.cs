using System;
using UnityEngine;

public class VoiceSystemManager : MonoBehaviour
{
    private OpenAIService openAIService;

    private ElevenLabsRealtimeSttService
        sttService;

    private ElevenLabsTtsService
        ttsService;


    private AudioSource audioSource;

    private bool wasTtsSpeaking = false;
    
    // =========================================================
    // 啟動
    // =========================================================

    private async void Start()
    {
        Debug.Log(
            "🚀 Unity Voice System 啟動中..."
        );


        // =====================================================
        // API Keys
        // =====================================================

        string openAIKey =
            Environment.GetEnvironmentVariable(
                "OPENAI_API_KEY"
            );


        string elevenLabsKey =
            Environment.GetEnvironmentVariable(
                "ELEVENLABS_API_KEY"
            );


        if (
            string.IsNullOrWhiteSpace(openAIKey) ||
            string.IsNullOrWhiteSpace(elevenLabsKey)
        )
        {
            Debug.LogError(
                "❌ 找不到 API Key"
            );

            return;
        }


        // =====================================================
        // Voice ID
        //
        // ⚠️ 換成你之前已經成功使用的 Voice ID
        // =====================================================

        string voiceId =
            "QoWyx69qbz2L0PN6Q4bK";


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


        // =====================================================
        // Services
        // =====================================================

        openAIService =
            new OpenAIService(
                openAIKey
            );


        sttService =
            new ElevenLabsRealtimeSttService(
                elevenLabsKey
            );


        ttsService =
            new ElevenLabsTtsService(
                elevenLabsKey,
                voiceId,
                audioSource
            );


        // =====================================================
        // STT 最終辨識
        // =====================================================

        sttService.FinalTranscriptReceived +=
            async (text) =>
            {
                Debug.Log(
                    $"👤 玩家：{text}"
                );


                try
                {
                    Debug.Log(
                        "🧠 GPT 思考中..."
                    );


                    string reply =
                        await openAIService
                            .AskAsync(text);


                    Debug.Log(
                        $"🤖 AI：{reply}"
                    );


                    // =========================================
                    // GPT → ElevenLabs → Unity AudioSource
                    // =========================================

                    await ttsService
                        .SpeakAsync(reply);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"❌ AI 對話錯誤：{ex.Message}"
                    );
                }
            };


        // =====================================================
        // 啟動 STT
        // =====================================================

        try
        {
            await sttService
                .StartAsync();


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
    // 麥克風
    // =========================================================

    private void Update()
    {
        bool isTtsSpeaking =
            ttsService != null &&
            ttsService.IsSpeaking;


        // AI 正在說話
        if (isTtsSpeaking)
        {
            wasTtsSpeaking = true;

            return;
        }


        // AI 剛剛講完
        if (wasTtsSpeaking)
        {
            wasTtsSpeaking = false;

            // 丟掉 AI 說話期間麥克風錄到的內容
            sttService?
                .SkipCurrentMicrophoneData();
        }


        sttService?
            .UpdateMicrophone();
    }


    // =========================================================
    // 關閉
    // =========================================================

    private async void OnDestroy()
    {
        ttsService?.Stop();


        if (sttService != null)
        {
            await sttService
                .StopAsync();
        }
    }
}