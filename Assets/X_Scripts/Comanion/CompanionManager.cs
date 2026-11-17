
using System;
using UnityEngine;

public class CompanionManager : MonoBehaviour
{
    [Header("角色清單")]
    [SerializeField] private GameObject[] companions;

    [Header("角色出現位置")]
    [SerializeField] private Transform spawnPoint;

    private GameObject currentCompanion;

    private int currentIndex = -1;

    public GameObject CurrentCompanion =>
        currentCompanion;

    public int CurrentIndex => currentIndex;

    // 角色切換通知
    public event Action<int> OnCompanionChanged;

    public bool IsDogSelected()
    {
        return currentIndex == 1;
    }

    public void SelectCompanion(int index)
    {
        if (companions == null ||
            index < 0 ||
            index >= companions.Length)
            return;

        if (companions[index] == null)
            return;

        // 如果是同一隻角色，不用重新召喚
        if (currentIndex == index &&
            currentCompanion != null)
        {
            currentCompanion.SetActive(true);
            return;
        }

        // 隱藏舊角色
        if (currentCompanion != null)
        {
            currentCompanion.SetActive(false);
        }

        // 設定新角色
        currentIndex = index;
        currentCompanion = companions[index];

        if (spawnPoint != null)
        {
            currentCompanion.transform.position =
                spawnPoint.position;

            currentCompanion.transform.rotation =
                spawnPoint.rotation;
        }

        currentCompanion.SetActive(true);

        Debug.Log(
            "已召喚：" + currentCompanion.name
        );

        // 通知語音系統切換 AI 角色
        OnCompanionChanged?.Invoke(index);
    }

    public void HideCompanion()
    {
        if (currentCompanion != null)
        {
            currentCompanion.SetActive(false);
        }

        currentCompanion = null;
        currentIndex = -1;

        OnCompanionChanged?.Invoke(-1);
    }

    // 背包開啟時暫時隱藏角色
    // 不會改變目前選擇的角色
    public void SetCompanionVisible(bool visible)
    {
        if (currentCompanion != null)
        {
            currentCompanion.SetActive(visible);
        }
    }

    private void Start()
    {
        if (companions == null)
            return;

        foreach (GameObject companion in companions)
        {
            if (companion != null)
                companion.SetActive(false);
        }
    }
}
