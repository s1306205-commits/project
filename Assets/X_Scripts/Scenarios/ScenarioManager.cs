
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScenarioManager : MonoBehaviour
{
    public enum ScenarioState
    {
        WaitingForChoice,
        WaitingForVoice,
        WaitingForAI,
        ShowingResult
    }

    [Header("情境文字")]
    [SerializeField] private TMP_Text situationText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("選項按鈕")]
    [SerializeField] private Button choiceA;
    [SerializeField] private Button choiceB;
    [SerializeField] private Button choiceC;
    [SerializeField] private Button freeAnswerButton;
    [SerializeField] private Button restartButton;

    [Header("AI 系統")]
    [SerializeField] private ScenarioAIController scenarioAI;

    [Header("NPC 語音")]
    [SerializeField]
    private ScenarioVoiceController scenarioVoice;

    public ScenarioState CurrentState { get; private set; }

    public event Action OnFreeAnswerRequested;

    private int currentScenario = 0;
    private int scenarioVersion = 0;

    private string situation;
    private string[] choices = new string[3];
    private string npcRole;

    private void Awake()
    {
        choiceA.onClick.AddListener(() => SelectChoice(0));
        choiceB.onClick.AddListener(() => SelectChoice(1));
        choiceC.onClick.AddListener(() => SelectChoice(2));

        freeAnswerButton.onClick.AddListener(RequestFreeAnswer);
        restartButton.onClick.AddListener(StartScenario);
    }

    private void Start()
    {
        ConfigureScenario(0);
    }

    // 切換情境
    public void ConfigureScenario(int index)
    {
        currentScenario = index;

        switch (index)
        {
            case 0:
                // 情境一：人際衝突
                npcRole = "同班同學";
                situation =
                    "情境：你在教室被同學嘲笑。\n" +
                    "同學：你連這麼簡單的題目都不會喔？";

                choices[0] = "我不喜歡你這樣說話。";
                choices[1] = "你自己也沒多厲害啊！";
                choices[2] = "先離開，尋求老師協助。";
                break;

            case 1:
                // 情境二：課堂發言
                npcRole = "老師";
                situation =
                    "情境：你在課堂上想回答問題，" +
                    "但擔心答錯而猶豫。\n" +
                    "老師：這一題有人知道答案嗎？" +
                    "有沒有同學願意試試看？";

                choices[0] = "老師，我想試著回答。";
                choices[1] = "我有點不確定，但我想試試。";
                choices[2] = "如果答錯大家會不會笑我?";
                break;

            case 2:
                // 情境三：朋友誤會
                npcRole = "朋友";
                situation =
                    "情境：你與朋友最近產生誤會，" +
                    "彼此很少說話。\n" +
                    "朋友：你最近為什麼都不跟我說話？";

                choices[0] = "因為你最近很煩人。";
                choices[1] = "我不想談論。";
                choices[2] = "我們可以好好談談嗎？";
                break;

            default:
                Debug.LogWarning("沒有這個情境：" + index);
                return;
        }

        // 修改三個按鈕顯示的文字
        SetButtonText(choiceA, choices[0]);
        SetButtonText(choiceB, choices[1]);
        SetButtonText(choiceC, choices[2]);

        StartScenario();
    }

    private void SetButtonText(Button button, string value)
    {
        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = value;
        else
            Debug.LogWarning(button.name + " 找不到 TMP 文字");
    }

    
    public async void StartScenario()
    {
        // 讓上一輪 AI 回答失效
        scenarioVersion++;

        if (scenarioAI != null)
            scenarioAI.CancelCurrentRequest();

        // 暫時禁止再次操作
        CurrentState = ScenarioState.WaitingForAI;
        SetChoiceButtonsActive(false);
        restartButton.gameObject.SetActive(false);

        // 停止上一輪語音
        if (scenarioVoice != null)
            await scenarioVoice.StopSpeechAsync();

        // 返回等待玩家選擇
        CurrentState = ScenarioState.WaitingForChoice;

        situationText.gameObject.SetActive(true);
        situationText.text = situation;

        resultText.text = "";
        feedbackText.text = "";

        resultText.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);

        SetChoiceButtonsActive(true);
        restartButton.gameObject.SetActive(true);

        if (scenarioVoice != null)
            scenarioVoice.PlayAnimation("Idle");

        Debug.Log("情境重新開始");
    }

    public void SelectChoice(int choiceIndex)
    {
        if (CurrentState != ScenarioState.WaitingForChoice)
            return;

        if (choiceIndex < 0 || choiceIndex >= choices.Length)
            return;

        string playerAnswer = choices[choiceIndex];

        Debug.Log("玩家選擇：" + playerAnswer);

        _ = ProcessAIResponseAsync(playerAnswer);
    }

    private async Task ProcessAIResponseAsync(
        string playerAnswer)
    {
        if (scenarioAI == null || !scenarioAI.IsReady)
        {
            Debug.LogError("ScenarioAIController 尚未準備完成");
            resultText.gameObject.SetActive(true);
            resultText.text = "AI 尚未連線，請檢查設定。";
            return;
        }

        CurrentState = ScenarioState.WaitingForAI;

        int currentVersion = scenarioVersion;

        // AI 思考時播放 Wait 動畫
        if (scenarioVoice != null)
        {
            scenarioVoice.PlayAnimation("Wait");
        }

        SetChoiceButtonsActive(false);

        resultText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(false);

        resultText.text = "正在思考...";
        feedbackText.text = "";

        restartButton.gameObject.SetActive(true);

        try
        {
            string aiSituation =
                "目前NPC身分：" + npcRole + "\n" +
                situation;

            string response =
                await scenarioAI.GenerateResponseAsync(
                    aiSituation,
                    playerAnswer
                );

            if (currentVersion != scenarioVersion)
                return;

            string npcResponse = response;
            string feedback = "";

            const string npcTag = "NPC回應：";
            const string feedbackTag = "溝通回饋：";

            int npcIndex = response.IndexOf(npcTag);
            int feedbackIndex = response.IndexOf(feedbackTag);

            if (npcIndex >= 0 && feedbackIndex > npcIndex)
            {
                npcResponse = response.Substring(
                    npcIndex + npcTag.Length,
                    feedbackIndex - npcIndex - npcTag.Length
                ).Trim();

                feedback = response.Substring(
                    feedbackIndex + feedbackTag.Length
                ).Trim();
            }
            else
            {
                feedback = "AI 未能分離回饋內容，請重新嘗試。";
            }

            
            CurrentState = ScenarioState.ShowingResult;

            // 顯示 NPC 回應
            resultText.gameObject.SetActive(true);
            resultText.text = npcResponse;

            // 先不顯示 AI 回饋
            feedbackText.gameObject.SetActive(false);

            // 隱藏四個選項
            SetChoiceButtonsActive(false);

            // 語音播放結束前，先隱藏 Restart
            restartButton.gameObject.SetActive(true);

            // 機器人說話
            if (scenarioVoice != null)
            {
                await scenarioVoice.SpeakAsync(npcResponse);
            }

            // 如果玩家已切換情境，不更新舊畫面
            if (currentVersion != scenarioVersion)
                return;

            // 語音結束後，才顯示回饋
            feedbackText.text = feedback;
            feedbackText.gameObject.SetActive(true);

            // 顯示 Restart
            restartButton.gameObject.SetActive(true);

        }
        catch (OperationCanceledException)
        {
            // 切換或重啟情境時取消請求
        }
        catch (Exception ex)
        {
            if (currentVersion != scenarioVersion)
                return;

            Debug.LogError("AI 回應失敗：" + ex.Message);

            ShowResult(
                "AI 回應失敗，請重新嘗試。",
                ""
            );
        }
    }

    private void ShowResult(
        string npcResponse,
        string feedback)
    {
        CurrentState = ScenarioState.ShowingResult;

        resultText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(true);

        resultText.text = npcResponse;
        feedbackText.text = feedback;

        SetChoiceButtonsActive(false);
        restartButton.gameObject.SetActive(true);
    }

    private void RequestFreeAnswer()
    {
        if (CurrentState != ScenarioState.WaitingForChoice)
            return;

        CurrentState = ScenarioState.WaitingForVoice;

        SetChoiceButtonsActive(false);

        resultText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(false);

        resultText.text = "請開始說話...";

        restartButton.gameObject.SetActive(true);

        OnFreeAnswerRequested?.Invoke();
    }

    // 未來接收 ElevenLabs STT 的辨識結果
    public void SubmitSpokenAnswer(string playerText)
    {
        if (CurrentState != ScenarioState.WaitingForVoice)
            return;

        if (string.IsNullOrWhiteSpace(playerText))
        {
            CurrentState = ScenarioState.WaitingForChoice;

            resultText.gameObject.SetActive(false);
            SetChoiceButtonsActive(true);
            restartButton.gameObject.SetActive(false);
            return;
        }

        _ = ProcessAIResponseAsync(playerText);
    }

    private void SetChoiceButtonsActive(bool active)
    {
        choiceA.gameObject.SetActive(active);
        choiceB.gameObject.SetActive(active);
        choiceC.gameObject.SetActive(active);
        freeAnswerButton.gameObject.SetActive(active);
    }

    // 返回情境選單前可以呼叫
    public async void StopScenario()
    {
        scenarioVersion++;

        if (scenarioAI != null)
            scenarioAI.CancelCurrentRequest();

        CurrentState = ScenarioState.ShowingResult;

        SetChoiceButtonsActive(false);
        restartButton.gameObject.SetActive(false);

        if (scenarioVoice != null)
            await scenarioVoice.StopSpeechAsync();

        Debug.Log("已離開目前情境");
    }
}
