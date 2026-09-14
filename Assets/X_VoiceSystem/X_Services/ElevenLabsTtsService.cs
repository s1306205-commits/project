using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ElevenLabsTtsService
{
    private readonly string apiKey;
    private readonly string voiceId;
    private readonly AudioSource audioSource;

    private const int SampleRate = 24000;

    public bool IsSpeaking
    {
        get
        {
            return audioSource != null &&
                   audioSource.isPlaying;
        }
    }


    public ElevenLabsTtsService(
        string apiKey,
        string voiceId,
        AudioSource audioSource)
    {
        this.apiKey = apiKey;
        this.voiceId = voiceId;
        this.audioSource = audioSource;
    }


    // =========================================================
    // 文字 → ElevenLabs → PCM → Unity AudioClip
    // =========================================================

    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }


        string url =
            $"https://api.elevenlabs.io/v1/" +
            $"text-to-speech/{voiceId}" +
            $"?output_format=pcm_24000";


        TtsRequest body =
            new TtsRequest
            {
                text = text,

                // 先使用低延遲模型。
                model_id = "eleven_flash_v2_5"
            };


        string json =
            JsonUtility.ToJson(body);


        byte[] jsonBytes =
            Encoding.UTF8.GetBytes(json);


        using UnityWebRequest request =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST
            );


        request.uploadHandler =
            new UploadHandlerRaw(jsonBytes);

        request.downloadHandler =
            new DownloadHandlerBuffer();


        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );


        request.SetRequestHeader(
            "xi-api-key",
            apiKey
        );


        Debug.Log(
            "🔊 ElevenLabs 正在產生語音..."
        );


        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();


        while (!operation.isDone)
        {
            await Task.Yield();
        }


        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            throw new Exception(
                $"ElevenLabs TTS 錯誤 " +
                $"{request.responseCode}：" +
                request.downloadHandler.text
            );
        }


        byte[] pcmData =
            request.downloadHandler.data;


        if (
            pcmData == null ||
            pcmData.Length == 0
        )
        {
            throw new Exception(
                "ElevenLabs 沒有回傳音訊。"
            );
        }


        AudioClip clip =
            CreateAudioClipFromPcm16(
                pcmData
            );


        audioSource.clip =
            clip;


        audioSource.Play();


        Debug.Log(
            "🔊 AI 開始說話"
        );


        // 等待播放完成
        while (audioSource.isPlaying)
        {
            await Task.Yield();
        }


        Debug.Log(
            "✅ AI 語音播放完成"
        );
    }


    // =========================================================
    // PCM16 little-endian → Unity AudioClip
    // =========================================================

    private AudioClip CreateAudioClipFromPcm16(
        byte[] pcmData)
    {
        int sampleCount =
            pcmData.Length / 2;


        float[] samples =
            new float[sampleCount];


        for (
            int i = 0;
            i < sampleCount;
            i++
        )
        {
            int byteIndex =
                i * 2;


            short pcmSample =
                (short)(
                    pcmData[byteIndex] |
                    (
                        pcmData[byteIndex + 1]
                        << 8
                    )
                );


            samples[i] =
                pcmSample / 32768f;
        }


        AudioClip clip =
            AudioClip.Create(
                "ElevenLabs_TTS",
                sampleCount,
                1,
                SampleRate,
                false
            );


        clip.SetData(
            samples,
            0
        );


        return clip;
    }


    // =========================================================
    // 之後 Barge-in 會直接使用
    // =========================================================

    public void Stop()
    {
        if (
            audioSource != null &&
            audioSource.isPlaying
        )
        {
            audioSource.Stop();

            Debug.Log(
                "🛑 AI 語音停止"
            );
        }
    }


    [Serializable]
    private class TtsRequest
    {
        public string text;
        public string model_id;
    }
}