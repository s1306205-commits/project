
using UnityEngine;

public class CompanionManager : MonoBehaviour
{
    [Header("角色清單")]
    [SerializeField] private GameObject[] companions;

    [Header("角色出現位置")]
    [SerializeField] private Transform spawnPoint;

    private GameObject currentCompanion;

    public void SelectCompanion(int index)
    {
        if (index < 0 || index >= companions.Length)
            return;

        if (companions[index] == null)
            return;

        // 先收起目前角色
        HideCompanion();

        // 啟用指定角色
        currentCompanion = companions[index];

        if (spawnPoint != null)
        {
            currentCompanion.transform.position =
                spawnPoint.position;

            currentCompanion.transform.rotation =
                spawnPoint.rotation;
        }

        currentCompanion.SetActive(true);

        Debug.Log("已召喚：" + currentCompanion.name);
    }

    public void HideCompanion()
    {
        if (currentCompanion != null)
        {
            currentCompanion.SetActive(false);
            currentCompanion = null;
        }
    }

    private void Start()
    {
        foreach (GameObject companion in companions)
        {
            if (companion != null)
                companion.SetActive(false);
        }
    }
}
