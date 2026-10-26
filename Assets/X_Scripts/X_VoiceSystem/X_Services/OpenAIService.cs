using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIService
{
    private readonly string apiKey;
    private readonly string activeCharacterPrompt;

    private const string Model = "gpt-5-mini";
    private const int MaxConversationRounds = 3;

    private readonly List<ConversationMessage> history =
        new List<ConversationMessage>();


    private const string CharacterPrompt = @"
    你是一個溫柔、自然的心理陪伴角色。
    你的主要任務是陪伴使用者聊天、傾聽使用者，
    並適度回應他目前表達的情緒。

    回答規則：
    1. 每次回答最多 1～2 句。
    2. 回答盡量簡短，通常控制在 40 個中文字以內。
    3. 使用自然口語，不要像報告、文章或客服。
    4. 不要一次提供很多建議。
    5. 一次最多問一個問題。
    6. 如果只是一般聊天，可以自然回應，不必每句都分析情緒。
    7. 使用繁體中文回答。
    8. 不要主動進行心理疾病診斷。
    9. 不要聲稱自己是心理師或醫師。
    10. 除非使用者明確要求詳細說明，否則不要長篇回答。

    語氣要溫和、有陪伴感，但不要過度熱情或說教。
    ";



    public OpenAIService(
    string apiKey,
    string customPrompt = null)
    {
        this.apiKey = apiKey;

        activeCharacterPrompt =
            string.IsNullOrWhiteSpace(customPrompt)
            ? CharacterPrompt
            : customPrompt;
    }


    // =========================================================
    // GPT Streaming
    // =========================================================

    public async Task<string> AskStreamingAsync(
        string userText,
        Action<string> onDelta,
        CancellationToken cancellationToken)
    {
        string prompt =
            BuildPrompt(userText);


        ResponseRequest requestBody =
            new ResponseRequest
            {
                model = Model,
                input = prompt,
                stream = true
            };


        string json =
            JsonUtility.ToJson(
                requestBody
            );


        byte[] body =
            Encoding.UTF8.GetBytes(
                json
            );


        StreamingDownloadHandler downloadHandler =
            new StreamingDownloadHandler(
                onDelta
            );


        using UnityWebRequest request =
            new UnityWebRequest(
                "https://api.openai.com/v1/responses",
                UnityWebRequest.kHttpVerbPOST
            );


        request.uploadHandler =
            new UploadHandlerRaw(body);


        request.downloadHandler =
            downloadHandler;


        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );


        request.SetRequestHeader(
            "Authorization",
            $"Bearer {apiKey}"
        );


        Debug.Log(
            "🧠 GPT Streaming 開始..."
        );


        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();


        // =====================================================
        // 等 GPT
        //
        // 同時檢查是否被 Barge-in 取消
        // =====================================================

        while (!operation.isDone)
        {
            if (
                cancellationToken
                    .IsCancellationRequested
            )
            {
                request.Abort();

                throw new
                    OperationCanceledException(
                        cancellationToken
                    );
            }


            await Task.Yield();
        }


        cancellationToken
            .ThrowIfCancellationRequested();


        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            throw new Exception(
                $"OpenAI Streaming API 錯誤：" +
                $"{request.responseCode}\n" +
                downloadHandler.ErrorText
            );
        }


        string fullReply =
            downloadHandler.FullText;


        if (
            string.IsNullOrWhiteSpace(
                fullReply
            )
        )
        {
            throw new Exception(
                "GPT Streaming 完成，但沒有取得文字。"
            );
        }


        // =====================================================
        // 只有「正常完成」的回答才寫進記憶
        //
        // 被玩家打斷的回答不寫入 history。
        // =====================================================

        history.Add(
            new ConversationMessage(
                "玩家",
                userText
            )
        );


        history.Add(
            new ConversationMessage(
                "角色",
                fullReply
            )
        );


        TrimHistory();


        Debug.Log(
            "✅ GPT Streaming 完成"
        );


        return fullReply;
    }


    // =========================================================
    // Prompt
    // =========================================================

    private string BuildPrompt(
        string userText)
    {
        StringBuilder prompt =
            new StringBuilder();


        prompt.AppendLine(
            activeCharacterPrompt
        );


        if (history.Count > 0)
        {
            prompt.AppendLine();


            prompt.AppendLine(
                "以下是你和使用者最近的對話紀錄："
            );


            foreach (
                ConversationMessage message
                in history
            )
            {
                prompt.AppendLine(
                    $"{message.Role}：" +
                    $"{message.Text}"
                );
            }
        }


        prompt.AppendLine();


        prompt.AppendLine(
            $"玩家：{userText}"
        );


        prompt.AppendLine();


        prompt.AppendLine(
            "請依照本次角色設定及前面的對話脈絡回答玩家。"
        );


        return prompt.ToString();
    }


    // =========================================================
    // Memory
    // =========================================================

    private void TrimHistory()
    {
        while (
            history.Count >
            MaxConversationRounds * 2
        )
        {
            history.RemoveAt(0);


            if (history.Count > 0)
            {
                history.RemoveAt(0);
            }
        }
    }


    public void ClearHistory()
    {
        history.Clear();
    }


    // =========================================================
    // JSON
    // =========================================================

    [Serializable]
    private class ResponseRequest
    {
        public string model;
        public string input;
        public bool stream;
    }


    [Serializable]
    private class StreamingEvent
    {
        public string type;
        public string delta;
    }


    // =========================================================
    // SSE Streaming
    // =========================================================

    private class StreamingDownloadHandler
        : DownloadHandlerScript
    {
        private readonly
            Action<string> onDelta;


        private readonly StringBuilder
            pendingData =
                new StringBuilder();


        private readonly StringBuilder
            fullText =
                new StringBuilder();


        private readonly StringBuilder
            rawResponse =
                new StringBuilder();


        public string FullText =>
            fullText.ToString();


        public string ErrorText =>
            rawResponse.ToString();


        public StreamingDownloadHandler(
            Action<string> onDelta)
            : base(new byte[8192])
        {
            this.onDelta =
                onDelta;
        }


        protected override bool ReceiveData(
            byte[] data,
            int dataLength)
        {
            if (
                data == null ||
                dataLength <= 0
            )
            {
                return true;
            }


            string chunk =
                Encoding.UTF8.GetString(
                    data,
                    0,
                    dataLength
                );


            rawResponse.Append(
                chunk
            );


            pendingData.Append(
                chunk
            );


            ProcessSseBuffer();


            return true;
        }


        private void ProcessSseBuffer()
        {
            string current =
                pendingData
                    .ToString()
                    .Replace(
                        "\r\n",
                        "\n"
                    );


            int separatorIndex;


            while (
                (
                    separatorIndex =
                        current.IndexOf(
                            "\n\n",
                            StringComparison.Ordinal
                        )
                ) >= 0
            )
            {
                string eventBlock =
                    current.Substring(
                        0,
                        separatorIndex
                    );


                current =
                    current.Substring(
                        separatorIndex + 2
                    );


                ProcessSseEvent(
                    eventBlock
                );
            }


            pendingData.Clear();


            pendingData.Append(
                current
            );
        }


        private void ProcessSseEvent(
            string eventBlock)
        {
            if (
                string.IsNullOrWhiteSpace(
                    eventBlock
                )
            )
            {
                return;
            }


            string[] lines =
                eventBlock.Split('\n');


            foreach (
                string rawLine
                in lines
            )
            {
                string line =
                    rawLine.Trim();


                if (
                    !line.StartsWith(
                        "data:",
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }


                string json =
                    line.Substring(5)
                        .Trim();


                if (
                    string.IsNullOrWhiteSpace(
                        json
                    ) ||
                    json == "[DONE]"
                )
                {
                    continue;
                }


                try
                {
                    StreamingEvent evt =
                        JsonUtility
                            .FromJson<
                                StreamingEvent
                            >(json);


                    if (
                        evt == null ||
                        evt.type !=
                        "response.output_text.delta" ||
                        string.IsNullOrEmpty(
                            evt.delta
                        )
                    )
                    {
                        continue;
                    }


                    fullText.Append(
                        evt.delta
                    );


                    onDelta?.Invoke(
                        evt.delta
                    );
                }
                catch (
                    Exception ex
                )
                {
                    Debug.LogWarning(
                        $"⚠️ GPT SSE 解析失敗：" +
                        $"{ex.Message}"
                    );
                }
            }
        }
    }


    private class ConversationMessage
    {
        public string Role;
        public string Text;


        public ConversationMessage(
            string role,
            string text)
        {
            Role = role;
            Text = text;
        }
    }
}