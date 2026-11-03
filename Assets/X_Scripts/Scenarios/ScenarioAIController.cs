
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ScenarioAIController : MonoBehaviour
{
    private OpenAIService openAIService;
    private CancellationTokenSource requestCancellation;

    private bool isReady = false;

    public bool IsReady => isReady;

    
    private const string ClassroomPrompt = @"
    你是一個 VR 校園情境模擬系統。

    你需要根據提供的情境扮演不同 NPC，
    例如同學、老師或朋友。

    角色規則：
    1. 根據情境中的 NPC 身分進行回答。
    2. 如果 NPC 是老師，使用自然的老師語氣。
    3. 如果 NPC 是同學，使用自然的學生語氣。
    4. 如果 NPC 是朋友，使用朋友之間的口語。
    5. NPC 不要突然變成心理陪伴角色。
    6. NPC 的反應要自然，不必每次都立即妥協。
    7. 不要產生嚴重侮辱、威脅或歧視內容。
    8. 不要把玩家遭受的嘲笑歸咎於玩家。

    溝通回饋規則：
    1. 說明玩家回答可能造成的影響。
    2. 不要只判斷正確或錯誤。
    3. 尊重玩家的感受與選擇。
    4. 回饋應簡短且具建設性。

    請使用繁體中文。

    NPC 回應：
    使用自然、簡短的繁體中文。
    最多 2 句，總長不超過 50 字。

    溝通回饋：
    針對玩家回答提供具體建議。
    最多 2 句，總長不超過 70 字。

    避免重複內容，不要長篇說教。

    只能使用以下格式：

    NPC回應：內容
    溝通回饋：內容
    ";


    private void Awake()
    {
        // 從電腦環境變數讀取金鑰，不寫死在程式中
        string apiKey =
            "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";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError(
                "沒有找到 OPENAI_API_KEY 環境變數"
            );
            return;
        }

        openAIService = new OpenAIService(
            apiKey,
            ClassroomPrompt
        );
        isReady = true;
    }

    public async Task<string> GenerateResponseAsync(
        string situation,
        string playerAnswer)
    {
        if (!isReady)
            throw new InvalidOperationException(
                "OpenAI 尚未初始化，請檢查 API Key。"
            );

        CancelCurrentRequest();

        requestCancellation = new CancellationTokenSource();
        CancellationToken token = requestCancellation.Token;

        string prompt =
            "現在進行 VR 校園人際情境模擬。\n" +
            "請根據以下情境，生成自然的 NPC 回應，" +
            "並提供簡短的溝通建議。\n\n" +

            "【情境】\n" + situation + "\n\n" +
            "【玩家回答】\n" + playerAnswer + "\n\n" +

            "【規則】\n" +
            "1. 使用繁體中文。\n" +
            "2. NPC 是真實的校園人物，不是心理諮商師。\n" +
            "3. NPC 回答自然、簡短，最多兩句。\n" +
            "4. NPC 不必每次都接受玩家的說法。\n" +
            "5. 回饋不得責怪遭受嘲笑的玩家。\n" +
            "6. 不要把求助老師視為失敗。\n" +
            "7. 不要增加情境中未提供的嚴重事件。\n\n" +

            "請嚴格使用以下格式，不要加其他內容：\n" +
            "NPC回應：內容\n" +
            "溝通回饋：內容";

        try
        {
            string response =
                await openAIService.AskStreamingAsync(
                    prompt,
                    delta => { },
                    token
                );

            token.ThrowIfCancellationRequested();

            return response;
        }
        catch (OperationCanceledException)
        {
            Debug.Log("AI 請求已取消");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError("情境 AI 發生錯誤：" + ex.Message);
            throw;
        }
    }

    public void CancelCurrentRequest()
    {
        if (requestCancellation != null)
        {
            if (!requestCancellation.IsCancellationRequested)
                requestCancellation.Cancel();

            // 不在這裡 Dispose，
            // 避免仍在進行的網路請求使用到 Token。
            requestCancellation = null;
        }
    }

    private void OnDestroy()
    {
        CancelCurrentRequest();
    }
}
