
using UnityEngine;

public class BackpackMenu : MonoBehaviour
{
    [Header("角色選擇面板")]
    [SerializeField] private CanvasGroup backpackPanel;

    [Header("角色管理器")]
    [SerializeField] private CompanionManager companionManager;

    private bool isOpen = false;

    private void Start()
    {
        SetPanelVisible(false);
    }

    // 點擊背包圖示時呼叫
    public void ToggleBackpack()
    {
        SetPanelVisible(!isOpen);
    }

    // 顯示或隱藏 Panel

    private void SetPanelVisible(bool visible)
    {
        isOpen = visible;

        if (backpackPanel == null)
        {
            Debug.LogError("沒有指定 BackpackPanel！");
            return;
        }

        backpackPanel.alpha = visible ? 1f : 0f;
        backpackPanel.interactable = visible;
        backpackPanel.blocksRaycasts = visible;

        // 開啟背包時隱藏角色
        // 關閉背包時恢復角色
        if (companionManager != null)
        {
            companionManager.SetCompanionVisible(!visible);
        }
    }


    // 選完角色後自動關閉
    public void CloseBackpack()
    {
        SetPanelVisible(false);
    }
}
