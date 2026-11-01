
using System;
using System.Collections.Generic;
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
    [SerializeField] private Button nextButton;

    [Header("題庫（依清單順序排列）")]
    [SerializeField]
    private List<ScenarioData> scenarios =
        new List<ScenarioData>();

    [SerializeField]
    private bool loadFirstScenarioOnStart = true;

    [Header("AI 系統")]
    [SerializeField]
    private ScenarioAIController scenarioAI;

    [Header("NPC 語音")]
    [SerializeField]
    private ScenarioVoiceController scenarioVoice;

    public ScenarioState CurrentState { get; private set; }

    public event Action OnFreeAnswerRequested;

    private int currentScenario = -1;
    private int scenarioVersion = 0;
    private ScenarioData currentData;

    private void Awake()
    {
        choiceA.onClick.AddListener(
            () => SelectChoice(0));

        choiceB.onClick.AddListener(
            () => SelectChoice(1));

        choiceC.onClick.AddListener(
            () => SelectChoice(2));

        freeAnswerButton.onClick.AddListener(
            RequestFreeAnswer);

        restartButton.onClick.AddListener(
            StartScenario);

        nextButton.onClick.AddListener(
            NextScenario);

        SetChoiceButtonsActive(false);

        restartButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (loadFirstScenarioOnStart)
        {
            ConfigureScenario(0);
        }
    }

    // 根據題庫索引載入情境
    public void ConfigureScenario(int index)
    {
        if (index < 0 ||
            index >= scenarios.Count ||
            scenarios[index] == null)
        {
            Debug.LogWarning(
                "找不到情境索引：" + index);
            return;
        }

        currentScenario = index;
        currentData = scenarios[index];

        // 設定三個選項
        SetButtonText(
            choiceA, currentData.choiceA);

        SetButtonText(
            choiceB, currentData.choiceB);

        SetButtonText(
            choiceC, currentData.choiceC);

        StartScenario();
    }

    
    public void ConfigureScenarioByCategory(string category)
    {
        for (int i = 0; i < scenarios.Count; i++)
        {
            if (scenarios[i] != null &&
                scenarios[i].category == category)
            {
                ConfigureScenario(i);
                return;
            }
        }

        Debug.LogWarning("找不到情境類型：" + category);
    }


    private void SetButtonText(
        Button button, string value)
    {
        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.text = value;
        }
        else
        {
            Debug.LogWarning(
                button.name + " 找不到 TMP 文字");
        }
    }

    // 開始或重新開始目前情境
    public async void StartScenario()
    {
        if (currentData == null)
        {
            Debug.LogWarning("尚未選擇情境");
            return;
        }

        scenarioVersion++;
        int myVersion = scenarioVersion;

        // 取消上一輪 AI 請求
        if (scenarioAI != null)
        {
            scenarioAI.CancelCurrentRequest();
        }

        CurrentState = ScenarioState.WaitingForAI;

        SetChoiceButtonsActive(false);

        restartButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);

        Debug.Log("選項按鈕顯示程式已執行");

        Debug.Log("Choice A activeSelf: " +
            choiceA.gameObject.activeSelf);

        Debug.Log("Choice A activeInHierarchy: " +
            choiceA.gameObject.activeInHierarchy);

        // 停止上一輪 NPC 語音
        try
        {
            if (scenarioVoice != null)
            {
                await scenarioVoice.StopSpeechAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "停止上一輪語音失敗：" + ex.Message);
        }

        // 防止舊情境覆蓋新情境
        if (myVersion != scenarioVersion)
            return;

        CurrentState = ScenarioState.WaitingForChoice;

        // 顯示情境
        situationText.gameObject.SetActive(true);
        situationText.text = currentData.situation;

        // 清除上一題結果
        resultText.text = "";
        feedbackText.text = "";

        resultText.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);

        // 顯示選項
        SetChoiceButtonsActive(true);

        restartButton.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(false);

        // NPC 待機
        if (scenarioVoice != null)
        {
            scenarioVoice.PlayAnimation("Idle");
        }

        Debug.Log(
            "情境開始：" + currentData.scenarioID);
    }

    // 玩家選擇 A、B、C
    public void SelectChoice(int choiceIndex)
    {
        if (CurrentState != ScenarioState.WaitingForChoice)
            return;

        if (currentData == null)
            return;

        if (choiceIndex < 0 || choiceIndex > 2)
            return;

        string playerAnswer =
            currentData.GetChoice(choiceIndex);

        if (string.IsNullOrWhiteSpace(playerAnswer))
        {
            Debug.LogWarning("選項內容為空");
            return;
        }

        Debug.Log("玩家選擇：" + playerAnswer);

        _ = ProcessAIResponseAsync(playerAnswer);
    }

    // 處理 AI 回應
    private async Task ProcessAIResponseAsync(
        string playerAnswer)
    {
        if (CurrentState != ScenarioState.WaitingForChoice &&
            CurrentState != ScenarioState.WaitingForVoice)
        {
            return;
        }

        int myVersion = scenarioVersion;

        CurrentState = ScenarioState.WaitingForAI;

        SetChoiceButtonsActive(false);

        nextButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);

        resultText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(false);

        feedbackText.text = "";

        if (scenarioAI == null || !scenarioAI.IsReady)
        {
            Debug.LogError(
                "ScenarioAIController 尚未準備完成");

            resultText.text =
                "AI 尚未連線，請檢查設定。";

            return;
        }

        resultText.text = "正在思考...";

        // AI 思考動畫
        if (scenarioVoice != null)
        {
            scenarioVoice.PlayAnimation("Wait");
        }

        try
        {
            string aiSituation =
                "目前NPC身分：" +
                currentData.npcRole + "\n" +
                currentData.situation;

            string response =
                await scenarioAI.GenerateResponseAsync(
                    aiSituation,
                    playerAnswer
                );

            if (myVersion != scenarioVersion)
                return;

            // 分離 NPC 回應與溝通回饋
            string npcResponse = response;
            string feedback;

            const string npcTag = "NPC回應：";
            const string feedbackTag = "溝通回饋：";

            int npcIndex =
                response.IndexOf(
                    npcTag,
                    StringComparison.Ordinal);

            int feedbackIndex =
                response.IndexOf(
                    feedbackTag,
                    StringComparison.Ordinal);

            if (npcIndex >= 0 &&
                feedbackIndex > npcIndex)
            {
                npcResponse =
                    response.Substring(
                        npcIndex + npcTag.Length,
                        feedbackIndex -
                        npcIndex -
                        npcTag.Length
                    ).Trim();

                feedback =
                    response.Substring(
                        feedbackIndex +
                        feedbackTag.Length
                    ).Trim();
            }
            else
            {
                feedback =
                    "AI 未能分離回饋內容，請重新嘗試。";
            }

            CurrentState = ScenarioState.ShowingResult;

            // 顯示 NPC 回應
            resultText.gameObject.SetActive(true);
            resultText.text = npcResponse;

            // 先隱藏回饋
            feedbackText.gameObject.SetActive(false);

            // NPC 說話
            if (scenarioVoice != null)
            {
                await scenarioVoice.SpeakAsync(
                    npcResponse);
            }

            if (myVersion != scenarioVersion)
                return;

            // 語音結束才顯示回饋
            feedbackText.text = feedback;
            feedbackText.gameObject.SetActive(true);

            restartButton.gameObject.SetActive(true);

            // 有相同類型的下一題才顯示 Next
            bool hasNext =
                FindNextInSameCategory() >= 0;

            nextButton.gameObject.SetActive(hasNext);
        }
        catch (OperationCanceledException)
        {
            // 切換或重新開始時取消舊請求
        }
        catch (Exception ex)
        {
            if (myVersion != scenarioVersion)
                return;

            Debug.LogError(
                "AI 回應失敗：" + ex.Message);

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
        nextButton.gameObject.SetActive(false);
    }

    // 玩家選擇自由語音
    private void RequestFreeAnswer()
    {
        if (CurrentState != ScenarioState.WaitingForChoice)
            return;

        if (currentData == null ||
            !currentData.allowFreeVoice)
            return;

        CurrentState = ScenarioState.WaitingForVoice;

        SetChoiceButtonsActive(false);

        resultText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(false);

        resultText.text = "請開始說話...";

        restartButton.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(false);

        OnFreeAnswerRequested?.Invoke();
    }

    // 接收 ElevenLabs STT 辨識結果
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
            nextButton.gameObject.SetActive(false);

            return;
        }

        _ = ProcessAIResponseAsync(playerText);
    }

    // 顯示或隱藏四個回答按鈕
    private void SetChoiceButtonsActive(bool active)
    {
        choiceA.gameObject.SetActive(active);
        choiceB.gameObject.SetActive(active);
        choiceC.gameObject.SetActive(active);

        bool showFree =
            active &&
            currentData != null &&
            currentData.allowFreeVoice;

        freeAnswerButton.gameObject.SetActive(showFree);
    }

    // 尋找同一類型的下一題
    private int FindNextInSameCategory()
    {
        if (currentData == null)
            return -1;

        for (int i = currentScenario + 1;
             i < scenarios.Count;
             i++)
        {
            if (scenarios[i] != null &&
                string.Equals(
                    scenarios[i].category,
                    currentData.category,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // 下一題
    public void NextScenario()
    {
        if (CurrentState != ScenarioState.ShowingResult)
            return;

        if (!feedbackText.gameObject.activeSelf)
            return;

        int nextIndex = FindNextInSameCategory();

        if (nextIndex >= 0)
        {
            ConfigureScenario(nextIndex);
        }
    }

    // 離開情境，返回選單
    public async void StopScenario()
    {
        scenarioVersion++;
        int myVersion = scenarioVersion;

        if (scenarioAI != null)
        {
            scenarioAI.CancelCurrentRequest();
        }

        CurrentState = ScenarioState.ShowingResult;

        SetChoiceButtonsActive(false);

        restartButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);

        try
        {
            if (scenarioVoice != null)
            {
                await scenarioVoice.StopSpeechAsync();
            }
        }
        catch (Exception ex)
        {
            if (myVersion == scenarioVersion)
            {
                Debug.LogWarning(
                    "停止情境語音失敗：" +
                    ex.Message);
            }
        }

        if (myVersion == scenarioVersion)
        {
            Debug.Log("已離開目前情境");
        }
    }
}
