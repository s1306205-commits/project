
using UnityEngine;

public class CanvasSwitcher : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject selectionCanvas;
    [SerializeField] private GameObject scenarioCanvas;

    [Header("情境管理器")]
    [SerializeField] private ScenarioManager scenarioManager;

    private void Awake()
    {
        // 啟動時顯示主選單
        selectionCanvas.SetActive(true);
        scenarioCanvas.SetActive(false);

        Debug.Log("CanvasSwitcher：已初始化主選單");
    }

    // 返回主選單
    public void ShowSelection()
    {
        Debug.Log("Menu 按鈕已觸發");

        if (scenarioManager != null)
        {
            scenarioManager.StopScenario();
        }

        selectionCanvas.SetActive(true);
        scenarioCanvas.SetActive(false);

        Debug.Log("已返回情境選擇畫面");
    }

    // 人際衝突
    public void SelectConflict()
    {
        OpenScenario("人際衝突");
    }

    // 課堂表達
    public void SelectSpeaking()
    {
        OpenScenario("課堂發言");
    }

    // 壓力與情緒管理
    public void SelectFriendship()
    {
        OpenScenario("壓力與情緒管理");
    }

    private void OpenScenario(string category)
    {
        if (scenarioManager == null)
        {
            Debug.LogError("ScenarioManager 未指定");
            return;
        }

        Debug.Log("準備進入：" + category);

        // 先開啟情境畫面
        scenarioCanvas.SetActive(true);

        // 再載入題目
        scenarioManager.ConfigureScenarioByCategory(category);

        // 關閉主選單
        selectionCanvas.SetActive(false);

        Debug.Log("已進入情境：" + category);
    }
}
