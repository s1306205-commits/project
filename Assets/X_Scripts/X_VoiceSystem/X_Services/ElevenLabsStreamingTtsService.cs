using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ElevenLabsStreamingTtsService
{
    private readonly string apiKey;
    private readonly string voiceId;
    private readonly AudioSource audioSource;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellation;

    private readonly SemaphoreSlim sendLock =
        new SemaphoreSlim(1, 1);


    // =========================================================
    // TTS 設定
    // =========================================================

    private const int SampleRate = 24000;

    // 播放 Ring Buffer
    // 約 20 秒容量
    private const int RingBufferSize =
        SampleRate * 20;


    private readonly float[] ringBuffer =
        new float[RingBufferSize];


    private int readPosition = 0;
    private int writePosition = 0;
    private int bufferedSamples = 0;


    private readonly object bufferLock =
        new object();


    private AudioClip streamingClip;


    private bool sessionActive = false;
    private bool serverFinished = false;


    public bool IsSpeaking
    {
        get
        {
            lock (bufferLock)
            {
                return
                    sessionActive ||
                    bufferedSamples > 0;
            }
        }
    }


    public ElevenLabsStreamingTtsService(
        string apiKey,
        string voiceId,
        AudioSource audioSource)
    {
        this.apiKey =
            apiKey;

        this.voiceId =
            voiceId;

        this.audioSource =
            audioSource;
    }


    // =========================================================
    // 開始一次新的 AI 回答
    // =========================================================

    public async Task StartSessionAsync()
    {
        await StopSessionAsync();


        ClearAudioBuffer();


        cancellation =
            new CancellationTokenSource();


        webSocket =
            new ClientWebSocket();


        webSocket.Options.SetRequestHeader(
            "xi-api-key",
            apiKey
        );


        string url =
            $"wss://api.elevenlabs.io/v1/" +
            $"text-to-speech/{voiceId}/stream-input" +
            $"?model_id=eleven_flash_v2_5" +
            $"&output_format=pcm_24000";


        Debug.Log(
            "🔗 正在連線 ElevenLabs Streaming TTS..."
        );


        await webSocket.ConnectAsync(
            new Uri(url),
            cancellation.Token
        );


        Debug.Log(
            "✅ ElevenLabs Streaming TTS 已連線"
        );


        sessionActive = true;
        serverFinished = false;


        // =====================================================
        // ElevenLabs 初始化訊息
        // =====================================================

        InitialMessage initial =
            new InitialMessage
            {
                text = " ",

                voice_settings =
                    new VoiceSettings
                    {
                        stability = 0.5f,
                        similarity_boost = 0.8f,
                        use_speaker_boost = false
                    },

                generation_config =
                    new GenerationConfig
                    {
                        // 沿用你 Console 版較穩的設定
                        chunk_length_schedule =
                            new int[]
                            {
                                80,
                                120,
                                160,
                                200
                            }
                    }
            };


        await SendJsonAsync(
            JsonUtility.ToJson(initial)
        );


        // 開始接音訊
        _ = ReceiveLoopAsync(
            cancellation.Token
        );


        // =====================================================
        // 建立 Unity Streaming AudioClip
        // =====================================================

        streamingClip =
            AudioClip.Create(
                "ElevenLabs_Streaming_TTS",
                SampleRate * 60,
                1,
                SampleRate,
                true,
                OnAudioRead
            );


        audioSource.loop = true;
        audioSource.clip = streamingClip;

        audioSource.Play();


        Debug.Log(
            "🔊 Unity Streaming AudioSource 啟動"
        );
    }


    // =========================================================
    // GPT Streaming 產生一段文字
    // → 傳給 ElevenLabs
    // =========================================================

    public async Task SendTextAsync(
        string text)
    {
        if (
            string.IsNullOrWhiteSpace(text) ||
            webSocket == null ||
            webSocket.State !=
            WebSocketState.Open
        )
        {
            return;
        }


        // 官方建議 streamed text 保留適當空格。
        TextMessage message =
            new TextMessage
            {
                text = text + " "
            };


        await SendJsonAsync(
            JsonUtility.ToJson(message)
        );
    }


    // =========================================================
    // GPT 完成
    //
    // flush 剩下文字
    // =========================================================

    public async Task FlushAsync()
    {
        if (
            webSocket == null ||
            webSocket.State !=
            WebSocketState.Open
        )
        {
            return;
        }


        FlushMessage message =
            new FlushMessage
            {
                text = " ",
                flush = true
            };


        await SendJsonAsync(
            JsonUtility.ToJson(message)
        );


        Debug.Log(
            "🚿 TTS Flush"
        );
    }


    // =========================================================
    // 告訴 ElevenLabs：
    // 這次文字全部結束
    // =========================================================

    public async Task FinishInputAsync()
    {
        if (
            webSocket == null ||
            webSocket.State !=
            WebSocketState.Open
        )
        {
            return;
        }


        EndMessage message =
            new EndMessage
            {
                text = ""
            };


        await SendJsonAsync(
            JsonUtility.ToJson(message)
        );


        Debug.Log(
            "✅ TTS 文字輸入完成"
        );
    }


    // =========================================================
    // WebSocket Send
    // =========================================================

    private async Task SendJsonAsync(
        string json)
    {
        if (
            webSocket == null ||
            webSocket.State !=
            WebSocketState.Open
        )
        {
            return;
        }


        await sendLock.WaitAsync();


        try
        {
            if (
                webSocket == null ||
                webSocket.State !=
                WebSocketState.Open
            )
            {
                return;
            }


            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    json
                );


            await webSocket.SendAsync(
                new ArraySegment<byte>(
                    bytes
                ),

                WebSocketMessageType.Text,

                true,

                cancellation.Token
            );
        }
        finally
        {
            sendLock.Release();
        }
    }


    // =========================================================
    // 接收 ElevenLabs PCM
    // =========================================================

    private async Task ReceiveLoopAsync(
        CancellationToken token)
    {
        byte[] receiveBuffer =
            new byte[65536];


        try
        {
            while (
                !token.IsCancellationRequested &&
                webSocket != null &&
                (
                    webSocket.State ==
                    WebSocketState.Open ||
                    webSocket.State ==
                    WebSocketState.CloseReceived
                )
            )
            {
                using (
                    System.IO.MemoryStream stream =
                        new System.IO.MemoryStream()
                )
                {
                    WebSocketReceiveResult result;


                    do
                    {
                        result =
                            await webSocket.ReceiveAsync(
                                new ArraySegment<byte>(
                                    receiveBuffer
                                ),
                                token
                            );


                        if (
                            result.MessageType ==
                            WebSocketMessageType.Close
                        )
                        {
                            serverFinished = true;
                            sessionActive = false;

                            return;
                        }


                        stream.Write(
                            receiveBuffer,
                            0,
                            result.Count
                        );
                    }
                    while (
                        !result.EndOfMessage
                    );


                    if (
                        result.MessageType !=
                        WebSocketMessageType.Text
                    )
                    {
                        continue;
                    }


                    string json =
                        Encoding.UTF8.GetString(
                            stream.ToArray()
                        );


                    ProcessServerMessage(
                        json
                    );
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (
                cancellation != null &&
                !cancellation.IsCancellationRequested
            )
            {
                Debug.LogError(
                    $"❌ Streaming TTS 接收錯誤：" +
                    $"{ex.Message}"
                );
            }
        }
    }


    // =========================================================
    // ElevenLabs JSON
    // =========================================================

    private void ProcessServerMessage(
        string json)
    {
        try
        {
            ServerMessage message =
                JsonUtility.FromJson<ServerMessage>(
                    json
                );


            if (message == null)
            {
                return;
            }


            if (
                !string.IsNullOrEmpty(
                    message.audio
                )
            )
            {
                byte[] pcm =
                    Convert.FromBase64String(
                        message.audio
                    );


                AddPcmToBuffer(
                    pcm
                );
            }


            if (
                message.is_final ||
                message.isFinal
            )
            {
                serverFinished = true;
                sessionActive = false;

                Debug.Log(
                    "✅ ElevenLabs TTS 音訊產生完成"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ TTS JSON 解析失敗：" +
                $"{ex.Message}"
            );
        }
    }


    // =========================================================
    // PCM16 → Ring Buffer
    // =========================================================

    private void AddPcmToBuffer(
        byte[] pcm)
    {
        if (
            pcm == null ||
            pcm.Length < 2
        )
        {
            return;
        }


        int sampleCount =
            pcm.Length / 2;


        lock (bufferLock)
        {
            for (
                int i = 0;
                i < sampleCount;
                i++
            )
            {
                int index =
                    i * 2;


                short sample =
                    (short)(
                        pcm[index] |
                        (
                            pcm[index + 1]
                            << 8
                        )
                    );


                float value =
                    sample / 32768f;


                // 如果真的塞滿，
                // 丟掉最舊的一個 sample。
                if (
                    bufferedSamples >=
                    RingBufferSize
                )
                {
                    readPosition =
                        (
                            readPosition + 1
                        )
                        % RingBufferSize;

                    bufferedSamples--;
                }


                ringBuffer[
                    writePosition
                ] = value;


                writePosition =
                    (
                        writePosition + 1
                    )
                    % RingBufferSize;


                bufferedSamples++;
            }
        }
    }


    // =========================================================
    // Unity Audio Thread
    //
    // AudioSource 需要聲音時，
    // 從 Ring Buffer 拿 PCM。
    // =========================================================

    private void OnAudioRead(
        float[] data)
    {
        lock (bufferLock)
        {
            for (
                int i = 0;
                i < data.Length;
                i++
            )
            {
                if (
                    bufferedSamples > 0
                )
                {
                    data[i] =
                        ringBuffer[
                            readPosition
                        ];


                    readPosition =
                        (
                            readPosition + 1
                        )
                        % RingBufferSize;


                    bufferedSamples--;
                }
                else
                {
                    // 還沒有收到下一包聲音
                    data[i] = 0f;
                }
            }
        }
    }


    // =========================================================
    // 清空 Audio Buffer
    // =========================================================

    private void ClearAudioBuffer()
    {
        lock (bufferLock)
        {
            readPosition = 0;
            writePosition = 0;
            bufferedSamples = 0;
        }
    }


    // =========================================================
    // 是否完全播放完
    // =========================================================

    public bool HasFinishedPlaying
    {
        get
        {
            lock (bufferLock)
            {
                return
                    serverFinished &&
                    bufferedSamples == 0;
            }
        }
    }


    // =========================================================
    // 等待音訊播完
    // =========================================================

    public async Task WaitUntilFinishedAsync()
    {
        // =========================================================
        // 先等 ElevenLabs 告訴我們：
        // 所有 TTS 音訊都已經產生完成
        // =========================================================

        while (!serverFinished)
        {
            await Task.Yield();
        }


        // =========================================================
        // 再等 Ring Buffer 裡的 PCM 全部被 Audio Thread 取走
        // =========================================================

        while (true)
        {
            int remainingSamples;

            lock (bufferLock)
            {
                remainingSamples =
                    bufferedSamples;
            }


            if (remainingSamples <= 0)
            {
                break;
            }


            await Task.Yield();
        }


    // =========================================================
    // Audio Thread 剛把最後 samples 取走，
    // 不代表喇叭已經實際播放到最後。
    //
    // 額外等待一小段時間，避免最後一句被 Stop() 截掉。
    // =========================================================

    await Task.Delay(800);


    if (
        audioSource != null &&
        audioSource.isPlaying
    )
    {
        audioSource.Stop();
    }


    sessionActive = false;


    Debug.Log(
        "🔇 Streaming TTS 播放完成"
    );
}


    // =========================================================
    // 關閉目前 Session
    // =========================================================

    public async Task StopSessionAsync()
    {
        sessionActive = false;
        serverFinished = true;


        if (
            audioSource != null &&
            audioSource.isPlaying
        )
        {
            audioSource.Stop();
        }


        ClearAudioBuffer();


        if (cancellation != null)
        {
            cancellation.Cancel();
        }


        if (
            webSocket != null &&
            webSocket.State ==
            WebSocketState.Open
        )
        {
            try
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus
                        .NormalClosure,

                    "TTS stopped",

                    CancellationToken.None
                );
            }
            catch
            {
            }
        }


        webSocket?.Dispose();
        webSocket = null;


        cancellation?.Dispose();
        cancellation = null;
    }


    // =========================================================
    // JSON Classes
    // =========================================================

    [Serializable]
    private class InitialMessage
    {
        public string text;

        public VoiceSettings
            voice_settings;

        public GenerationConfig
            generation_config;
    }


    [Serializable]
    private class VoiceSettings
    {
        public float stability;

        public float similarity_boost;

        public bool use_speaker_boost;
    }


    [Serializable]
    private class GenerationConfig
    {
        public int[]
            chunk_length_schedule;
    }


    [Serializable]
    private class TextMessage
    {
        public string text;
    }


    [Serializable]
    private class FlushMessage
    {
        public string text;

        public bool flush;
    }


    [Serializable]
    private class EndMessage
    {
        public string text;
    }


    [Serializable]
    private class ServerMessage
    {
        public string audio;

        // ElevenLabs 文件不同範例曾使用兩種形式，
        // 兩個都接。
        public bool is_final;
        public bool isFinal;
    }
}