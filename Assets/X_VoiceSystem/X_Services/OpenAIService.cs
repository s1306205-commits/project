using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAIService
{
    private readonly string apiKey;

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


    public OpenAIService(string apiKey)
    {
        this.apiKey = apiKey;
    }


    public async Task<string> AskAsync(string userText)
    {
        StringBuilder prompt = new StringBuilder();

        prompt.AppendLine(CharacterPrompt);


        if (history.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("以下是你和使用者最近的對話紀錄：");

            foreach (ConversationMessage message in history)
            {
                prompt.AppendLine(
                    $"{message.Role}：{message.Text}"
                );
            }
        }


        prompt.AppendLine();
        prompt.AppendLine($"玩家：{userText}");
        prompt.AppendLine();
        prompt.AppendLine(
            "請根據前面的對話脈絡，以角色身分直接回答玩家。"
        );


        string json =
            JsonUtility.ToJson(
                new ResponseRequest
                {
                    model = Model,
                    input = prompt.ToString()
                }
            );


        using UnityWebRequest request =
            new UnityWebRequest(
                "https://api.openai.com/v1/responses",
                "POST"
            );


        byte[] body =
            Encoding.UTF8.GetBytes(json);


        request.uploadHandler =
            new UploadHandlerRaw(body);

        request.downloadHandler =
            new DownloadHandlerBuffer();


        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "Authorization",
            $"Bearer {apiKey}"
        );


        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();


        while (!operation.isDone)
        {
            await Task.Yield();
        }


        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new Exception(
                $"OpenAI API 錯誤：{request.responseCode}\n" +
                request.downloadHandler.text
            );
        }


        ResponseResult response =
            JsonUtility.FromJson<ResponseResult>(
                request.downloadHandler.text
            );


        string reply = ExtractOutputText(response);


        history.Add(
            new ConversationMessage("玩家", userText)
        );

        history.Add(
            new ConversationMessage("角色", reply)
        );


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


        return reply;
    }


    private string ExtractOutputText(
        ResponseResult response)
    {
        if (
            response == null ||
            response.output == null
        )
        {
            return "目前沒有取得回覆。";
        }


        foreach (OutputItem item in response.output)
        {
            if (item.content == null)
                continue;


            foreach (ContentItem content in item.content)
            {
                if (
                    content.type == "output_text" &&
                    !string.IsNullOrEmpty(content.text)
                )
                {
                    return content.text;
                }
            }
        }


        return "目前沒有取得回覆。";
    }


    public void ClearHistory()
    {
        history.Clear();
    }


    // =========================================
    // JSON Classes
    // =========================================

    [Serializable]
    private class ResponseRequest
    {
        public string model;
        public string input;
    }


    [Serializable]
    private class ResponseResult
    {
        public OutputItem[] output;
    }


    [Serializable]
    private class OutputItem
    {
        public ContentItem[] content;
    }


    [Serializable]
    private class ContentItem
    {
        public string type;
        public string text;
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