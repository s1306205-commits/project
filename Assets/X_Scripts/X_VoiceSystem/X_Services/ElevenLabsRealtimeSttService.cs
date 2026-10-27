using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class ElevenLabsRealtimeSttService
{
    private readonly string apiKey;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellation;

    private AudioClip microphoneClip;
    private string microphoneName;

    // 我們希望送給 ElevenLabs 的格式
    private const int TargetSampleRate = 16000;

    // Unity 麥克風循環錄音 Buffer
    private const int RecordingLengthSeconds = 10;

    // 100 ms @ 16 kHz = 1600 samples
    private const int ChunkSamples = 1600;

    private int lastMicrophonePosition = 0;

    // 暫存 Unity 每幀取得的 samples
    private readonly List<float> sampleBuffer =
        new List<float>();


    // =========================================================
    // 單一發送佇列
    // =========================================================

    private readonly ConcurrentQueue<OutgoingMessage>
        outgoingQueue =
            new ConcurrentQueue<OutgoingMessage>();

    private readonly SemaphoreSlim outgoingSignal =
        new SemaphoreSlim(0);

    private Task sendLoopTask;


    // =========================================================
    // STT 卡住保險
    // =========================================================

    // Partial 文字 1.3 秒都沒有變化
    // 就主動要求 ElevenLabs commit
    private const double PartialStallCommitSeconds = 1.3;

    // 避免「嗯」「啊」之類太短的文字被強制送出
    private const int MinAutoCommitCharacters = 3;

    // 最新 partial
    private string latestPartialText = "";

    // 最新 partial 真正發生變化的時間
    private DateTime lastPartialChangedUtc =
        DateTime.MinValue;

    // 避免同一段一直重複排 commit
    private bool autoCommitRequested = false;

    // AI 說話時由 VoiceSystemManager 關閉
    private bool autoCommitAllowed = true;


    public event Action<string> PartialTranscriptReceived;

    public event Func<string, Task> FinalTranscriptReceived;

    public event Action SpeechDetected;


    public ElevenLabsRealtimeSttService(
        string apiKey)
    {
        this.apiKey = apiKey;
    }


    // =========================================================
    // 啟動 STT
    // =========================================================

    public async Task StartAsync()
    {
        cancellation =
            new CancellationTokenSource();

        webSocket =
            new ClientWebSocket();


        webSocket.Options.SetRequestHeader(
            "xi-api-key",
            apiKey
        );


        string url =
            "wss://api.elevenlabs.io/v1/" +
            "speech-to-text/realtime" +
            "?model_id=scribe_v2_realtime" +
            "&language_code=zh" +
            "&audio_format=pcm_16000" +
            "&commit_strategy=vad" +
            "&vad_silence_threshold_secs=1.2";


        Debug.Log(
            "🔗 正在連線 ElevenLabs Realtime STT..."
        );


        await webSocket.ConnectAsync(
            new Uri(url),
            cancellation.Token
        );


        Debug.Log(
            "✅ ElevenLabs Realtime STT 已連線"
        );


        sendLoopTask =
            SendLoopAsync(
                cancellation.Token
            );


        StartMicrophone();


        _ = ReceiveLoopAsync(
            cancellation.Token
        );
    }


    // =========================================================
    // 啟動 Unity 麥克風
    // =========================================================

    private void StartMicrophone()
    {
        if (
            Microphone.devices.Length == 0
        )
        {
            throw new Exception(
                "Unity 找不到任何麥克風。"
            );
        }


        microphoneName =
            Microphone.devices[0];


        Debug.Log(
            $"🎤 使用麥克風：{microphoneName}"
        );


        microphoneClip =
            Microphone.Start(
                microphoneName,
                true,
                RecordingLengthSeconds,
                TargetSampleRate
            );


        if (
            microphoneClip == null
        )
        {
            throw new Exception(
                "Unity 麥克風啟動失敗。"
            );
        }


        lastMicrophonePosition = 0;

        sampleBuffer.Clear();


        Debug.Log(
            $"🎤 實際錄音格式：" +
            $"{microphoneClip.frequency} Hz / " +
            $"{microphoneClip.channels} channel(s)"
        );


        if (
            microphoneClip.frequency !=
            TargetSampleRate
        )
        {
            Debug.LogWarning(
                $"⚠️ 麥克風實際取樣率為 " +
                $"{microphoneClip.frequency} Hz，" +
                $"不是要求的 {TargetSampleRate} Hz。"
            );
        }


        Debug.Log(
            "🎤 Unity 麥克風已啟動"
        );
    }


    // =========================================================
    // 是否允許自動 Commit
    // =========================================================

    public void SetAutoCommitAllowed(
        bool allowed)
    {
        autoCommitAllowed =
            allowed;
    }


    // =========================================================
    // VoiceSystemManager.Update() 每幀呼叫
    // =========================================================

    public void UpdateMicrophone()
    {
        if (
            microphoneClip == null ||
            webSocket == null ||
            webSocket.State !=
                WebSocketState.Open
        )
        {
            return;
        }


        int currentPosition =
            Microphone.GetPosition(
                microphoneName
            );


        if (
            currentPosition < 0
        )
        {
            return;
        }


        if (
            currentPosition ==
            lastMicrophonePosition
        )
        {
            CheckAutoCommitTimeout();

            return;
        }


        // =====================================================
        // 一般情況
        // =====================================================

        if (
            currentPosition >
            lastMicrophonePosition
        )
        {
            ReadMicrophoneRange(
                lastMicrophonePosition,
                currentPosition -
                lastMicrophonePosition
            );
        }

        // =====================================================
        // Unity Microphone 循環 Buffer 繞回開頭
        // =====================================================

        else
        {
            int remainingFrames =
                microphoneClip.samples -
                lastMicrophonePosition;


            if (
                remainingFrames > 0
            )
            {
                ReadMicrophoneRange(
                    lastMicrophonePosition,
                    remainingFrames
                );
            }


            if (
                currentPosition > 0
            )
            {
                ReadMicrophoneRange(
                    0,
                    currentPosition
                );
            }
        }


        lastMicrophonePosition =
            currentPosition;


        // =====================================================
        // 累積到約 100 ms 才排入送出佇列
        // =====================================================

        QueueBufferedChunks();


        // =====================================================
        // 檢查 STT 是否卡住
        // =====================================================

        CheckAutoCommitTimeout();
    }


    // =========================================================
    // STT 卡住保險
    // =========================================================

    private void CheckAutoCommitTimeout()
    {
        if (
            !autoCommitAllowed ||
            autoCommitRequested ||
            string.IsNullOrWhiteSpace(
                latestPartialText
            ) ||
            latestPartialText
                .Trim()
                .Length <
                MinAutoCommitCharacters ||
            lastPartialChangedUtc ==
                DateTime.MinValue
        )
        {
            return;
        }


        double stalledSeconds =
            (
                DateTime.UtcNow -
                lastPartialChangedUtc
            ).TotalSeconds;


        if (
            stalledSeconds <
            PartialStallCommitSeconds
        )
        {
            return;
        }


        autoCommitRequested =
            true;


        Debug.Log(
            $"⏱️ STT partial 已 " +
            $"{stalledSeconds:F1}s 沒有變化，" +
            $"排入強制 commit：" +
            $"{latestPartialText}"
        );


        QueueCommit();
    }


    // =========================================================
    // 從 Unity AudioClip 讀取一段資料
    // =========================================================

    private void ReadMicrophoneRange(
        int offsetFrames,
        int frameCount)
    {
        if (
            microphoneClip == null ||
            frameCount <= 0
        )
        {
            return;
        }


        int channels =
            microphoneClip.channels;


        float[] rawSamples =
            new float[
                frameCount *
                channels
            ];


        bool success =
            microphoneClip.GetData(
                rawSamples,
                offsetFrames
            );


        if (!success)
        {
            Debug.LogWarning(
                "⚠️ Unity Microphone.GetData 失敗"
            );

            return;
        }


        // =====================================================
        // Mono
        // =====================================================

        if (
            channels == 1
        )
        {
            sampleBuffer.AddRange(
                rawSamples
            );

            return;
        }


        // =====================================================
        // Stereo / 多聲道 → Mono
        // =====================================================

        for (
            int frame = 0;
            frame < frameCount;
            frame++
        )
        {
            float sum = 0f;


            for (
                int channel = 0;
                channel < channels;
                channel++
            )
            {
                int index =
                    frame *
                    channels +
                    channel;


                sum +=
                    rawSamples[index];
            }


            float monoSample =
                sum /
                channels;


            sampleBuffer.Add(
                monoSample
            );
        }
    }


    // =========================================================
    // 每滿 1600 samples
    // → 約 100 ms
    // → 排進單一 Send Queue
    // =========================================================

    private void QueueBufferedChunks()
    {
        while (
            sampleBuffer.Count >=
            ChunkSamples
        )
        {
            float[] chunk =
                sampleBuffer.GetRange(
                    0,
                    ChunkSamples
                ).ToArray();


            sampleBuffer.RemoveRange(
                0,
                ChunkSamples
            );


            byte[] pcmBytes =
                ConvertFloatToPcm16(
                    chunk
                );


            outgoingQueue.Enqueue(
                new OutgoingMessage
                {
                    Type =
                        OutgoingMessageType.Audio,

                    AudioBytes =
                        pcmBytes
                }
            );


            outgoingSignal.Release();
        }
    }


    // =========================================================
    // 強制 Commit 也排進同一條 Queue
    // =========================================================

    private void QueueCommit()
    {
        outgoingQueue.Enqueue(
            new OutgoingMessage
            {
                Type =
                    OutgoingMessageType.Commit
            }
        );


        outgoingSignal.Release();
    }


    // =========================================================
    // 單一 Send Loop
    // =========================================================

    private async Task SendLoopAsync(
        CancellationToken token)
    {
        try
        {
            while (
                !token.IsCancellationRequested
            )
            {
                await outgoingSignal.WaitAsync(
                    token
                );


                while (
                    outgoingQueue.TryDequeue(
                        out OutgoingMessage message
                    )
                )
                {
                    if (
                        token.IsCancellationRequested
                    )
                    {
                        return;
                    }


                    if (
                        webSocket == null ||
                        webSocket.State !=
                            WebSocketState.Open
                    )
                    {
                        return;
                    }


                    if (
                        message.Type ==
                        OutgoingMessageType.Audio
                    )
                    {
                        await SendAudioMessageAsync(
                            message.AudioBytes,
                            token
                        );
                    }

                    else if (
                        message.Type ==
                        OutgoingMessageType.Commit
                    )
                    {
                        await SendCommitMessageAsync(
                            token
                        );
                    }
                }
            }
        }

        catch (
            OperationCanceledException
        )
        {
        }

        catch (Exception ex)
        {
            if (
                !token.IsCancellationRequested
            )
            {
                Debug.LogError(
                    $"❌ STT SendLoop 錯誤：" +
                    $"{ex.Message}"
                );
            }
        }
    }


    // =========================================================
    // 真正送 Audio
    // =========================================================

    private async Task SendAudioMessageAsync(
        byte[] pcmBytes,
        CancellationToken token)
    {
        if (
            pcmBytes == null ||
            pcmBytes.Length == 0
        )
        {
            return;
        }


        string base64Audio =
            Convert.ToBase64String(
                pcmBytes
            );


        InputAudioMessage message =
            new InputAudioMessage
            {
                message_type =
                    "input_audio_chunk",

                audio_base_64 =
                    base64Audio
            };


        string json =
            JsonUtility.ToJson(
                message
            );


        byte[] jsonBytes =
            Encoding.UTF8.GetBytes(
                json
            );


        await webSocket.SendAsync(
            new ArraySegment<byte>(
                jsonBytes
            ),

            WebSocketMessageType.Text,

            true,

            token
        );
    }


    // =========================================================
    // 真正送 Commit
    // =========================================================

    private async Task SendCommitMessageAsync(
        CancellationToken token)
    {
        byte[] silence =
            new byte[
                ChunkSamples * 2
            ];


        CommitAudioMessage message =
            new CommitAudioMessage
            {
                message_type =
                    "input_audio_chunk",

                audio_base_64 =
                    Convert.ToBase64String(
                        silence
                    ),

                commit = true
            };


        string json =
            JsonUtility.ToJson(
                message
            );


        byte[] jsonBytes =
            Encoding.UTF8.GetBytes(
                json
            );


        await webSocket.SendAsync(
            new ArraySegment<byte>(
                jsonBytes
            ),

            WebSocketMessageType.Text,

            true,

            token
        );


        Debug.Log(
            "📨 已送出 STT 強制 commit"
        );
    }


    // =========================================================
    // Unity float → PCM16
    // =========================================================

    private byte[] ConvertFloatToPcm16(
        float[] samples)
    {
        byte[] bytes =
            new byte[
                samples.Length *
                2
            ];


        int byteIndex = 0;


        for (
            int i = 0;
            i < samples.Length;
            i++
        )
        {
            float sample =
                Mathf.Clamp(
                    samples[i],
                    -1f,
                    1f
                );


            short pcmSample =
                (short)(
                    sample < 0
                    ? sample *
                      32768f
                    : sample *
                      32767f
                );


            bytes[byteIndex++] =
                (byte)(
                    pcmSample &
                    0xFF
                );


            bytes[byteIndex++] =
                (byte)(
                    (
                        pcmSample >>
                        8
                    ) &
                    0xFF
                );
        }


        return bytes;
    }


    // =========================================================
    // 接收 ElevenLabs
    // =========================================================

    private async Task ReceiveLoopAsync(
        CancellationToken token)
    {
        byte[] receiveBuffer =
            new byte[32768];


        try
        {
            while (
                !token.IsCancellationRequested &&
                webSocket != null &&
                webSocket.State ==
                    WebSocketState.Open
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
                            await webSocket
                                .ReceiveAsync(
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


                    ProcessMessage(
                        json
                    );
                }
            }
        }

        catch (
            OperationCanceledException
        )
        {
        }

        catch (Exception ex)
        {
            if (
                !token.IsCancellationRequested
            )
            {
                Debug.LogError(
                    $"❌ STT 接收錯誤：" +
                    $"{ex.Message}"
                );
            }
        }
    }


    // =========================================================
    // 處理 STT 訊息
    // =========================================================

    private void ProcessMessage(
        string json)
    {
        try
        {
            SttResponse response =
                JsonUtility.FromJson<SttResponse>(
                    json
                );


            if (
                response == null
            )
            {
                return;
            }


            // =================================================
            // Session Started
            // =================================================

            if (
                response.message_type ==
                "session_started"
            )
            {
                Debug.Log(
                    "✅ STT Session Started"
                );

                return;
            }


            // =================================================
            // Partial
            // =================================================

            if (
                response.message_type ==
                "partial_transcript"
            )
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        response.text
                    )
                )
                {
                    string cleanPartial =
                        response.text.Trim();


                    if (
                        cleanPartial !=
                        latestPartialText
                    )
                    {
                        latestPartialText =
                            cleanPartial;

                        lastPartialChangedUtc =
                            DateTime.UtcNow;

                        autoCommitRequested =
                            false;
                    }


                    Debug.Log(
                        $"即時：{response.text}"
                    );


                    PartialTranscriptReceived?
                        .Invoke(
                            response.text
                        );


                    SpeechDetected?
                        .Invoke();
                }


                return;
            }


            // =================================================
            // Final
            // =================================================

            if (
                response.message_type ==
                "committed_transcript"
            )
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        response.text
                    )
                )
                {
                    latestPartialText =
                        "";

                    lastPartialChangedUtc =
                        DateTime.MinValue;

                    autoCommitRequested =
                        false;


                    Debug.Log(
                        $"✅ 最終：{response.text}"
                    );


                    if (
                        FinalTranscriptReceived !=
                        null
                    )
                    {
                        _ =
                            FinalTranscriptReceived
                                .Invoke(
                                    response.text
                                );
                    }
                }


                return;
            }
        }

        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ STT JSON 解析失敗：" +
                $"{ex.Message}"
            );


            Debug.Log(
                $"原始 JSON：{json}"
            );
        }
    }


    // =========================================================
    // 關閉
    // =========================================================

    public async Task StopAsync()
    {
        cancellation?.Cancel();


        if (
            !string.IsNullOrEmpty(
                microphoneName
            ) &&
            Microphone.IsRecording(
                microphoneName
            )
        )
        {
            Microphone.End(
                microphoneName
            );
        }


        sampleBuffer.Clear();


        while (
            outgoingQueue.TryDequeue(
                out _
            )
        )
        {
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

                    "Unity stopped",

                    CancellationToken.None
                );
            }

            catch
            {
            }
        }


        if (
            sendLoopTask != null
        )
        {
            try
            {
                await sendLoopTask;
            }

            catch
            {
            }
        }


        webSocket?.Dispose();

        webSocket = null;


        cancellation?.Dispose();

        cancellation = null;


        Debug.Log(
            "🛑 Voice STT 已停止"
        );
    }


    // =========================================================
    // 跳過目前麥克風累積資料
    // =========================================================

    public void SkipCurrentMicrophoneData()
    {
        if (
            microphoneClip == null ||
            string.IsNullOrEmpty(
                microphoneName
            )
        )
        {
            return;
        }


        int currentPosition =
            Microphone.GetPosition(
                microphoneName
            );


        if (
            currentPosition >= 0
        )
        {
            lastMicrophonePosition =
                currentPosition;

            sampleBuffer.Clear();


            while (
                outgoingQueue.TryDequeue(
                    out _
                )
            )
            {
            }


            latestPartialText =
                "";

            lastPartialChangedUtc =
                DateTime.MinValue;

            autoCommitRequested =
                false;
        }
    }


    // =========================================================
    // Queue Message
    // =========================================================

    private enum OutgoingMessageType
    {
        Audio,
        Commit
    }


    private class OutgoingMessage
    {
        public OutgoingMessageType Type;

        public byte[] AudioBytes;
    }


    // =========================================================
    // JSON Classes
    // =========================================================

    [Serializable]
    private class InputAudioMessage
    {
        public string message_type;

        public string audio_base_64;
    }


    [Serializable]
    private class CommitAudioMessage
    {
        public string message_type;

        public string audio_base_64;

        public bool commit;
    }


    [Serializable]
    private class SttResponse
    {
        public string message_type;

        public string text;
    }
}