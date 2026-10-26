
using UnityEngine;

public class CanvasSwitcher : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject selectionCanvas;
    [SerializeField] private GameObject scenarioCanvas;

    [Header("情境管理器")]
    [SerializeField] private ScenarioManager scenarioManager;

    private void Start()
    {
        ShowSelection();
    }

    public void ShowSelection()
    {
        if (scenarioManager != null)
            scenarioManager.StopScenario();

        scenarioCanvas.SetActive(false);
        selectionCanvas.SetActive(true);
    }

    public void SelectConflict()
    {
        OpenScenario(0);
    }

    public void SelectSpeaking()
    {
        OpenScenario(1);
    }

    public void SelectFriendship()
    {
        OpenScenario(2);
    }

    private void OpenScenario(int index)
    {
        if (scenarioManager == null)
        {
            Debug.LogError("尚未連接 ScenarioManager");
            return;
        }

        // 載入對應劇情與選項
        scenarioManager.ConfigureScenario(index);

        // 關閉選單
        selectionCanvas.SetActive(false);

        // 開啟情境面板
        scenarioCanvas.SetActive(true);

        Debug.Log("已切換至情境：" + index);
    }
}
