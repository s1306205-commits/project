
using System;
using System.Threading.Tasks;
using UnityEngine;

public class ScenarioVoiceController : MonoBehaviour
{
    [Header("機器人")]
    [SerializeField] private Animator robotAnimator;

    [Header("聲音播放")]
    [SerializeField] private AudioSource audioSource;

    [Header("ElevenLabs Voice ID")]
    [SerializeField] private string voiceId;
    [ContextMenu("測試 NPC 語音")]
    public void TestVoice()
    {
        _ = SpeakAsync(
            "你好，我是教室裡的機器人。"
        );
    }

    private ElevenLabsStreamingTtsService ttsService;
    private bool isSpeaking = false;

    public bool IsSpeaking => isSpeaking;

    private void Awake()
    {
        string apiKey =
            Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("找不到 ElevenLabs API Key");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError("沒有指定 AudioSource");
            return;
        }

        ttsService = new ElevenLabsStreamingTtsService(
            apiKey,
            voiceId,
            audioSource
        );

        PlayAnimation("Idle");
    }

    public void PlayAnimation(string stateName)
    {
        if (robotAnimator != null)
        {
            robotAnimator.CrossFade(
                stateName,
                0.15f
            );
        }
    }

    public async Task SpeakAsync(string text)
    {
        if (ttsService == null ||
            string.IsNullOrWhiteSpace(text) ||
            isSpeaking)
            return;

        isSpeaking = true;

        try
        {
            await ttsService.StartSessionAsync();

            PlayAnimation("Talk");

            await ttsService.SendTextAsync(text);
            await ttsService.FlushAsync();
            await ttsService.FinishInputAsync();

            await ttsService.WaitUntilFinishedAsync();

            Debug.Log("NPC 語音播放完成");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "NPC 語音錯誤：" + ex.Message
            );
        }
        finally
        {
            PlayAnimation("Idle");
            isSpeaking = false;
        }
    }

    public async Task StopSpeechAsync()
    {
        if (ttsService != null)
        {
            await ttsService.StopSessionAsync();
        }

        PlayAnimation("Idle");
    }

    private async void OnDestroy()
    {
        if (ttsService != null)
        {
            await ttsService.StopSessionAsync();
        }
    }
}
