using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    // =========================================================
    // Scene
    // =========================================================

    [Header("Scene")]

    // 要進入的 Scene 名稱
    public string targetSceneName;


    // =========================================================
    // UI
    // =========================================================

    [Header("UI")]

    public GameObject confirmPanel;


    // =========================================================
    // Player
    // =========================================================

    [Header("Player")]

    public string playerTag = "Player";


    // =========================================================
    // State
    // =========================================================

    private bool playerInside = false;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        if (!playerInside)
        {
            return;
        }


        // PC 測試：
        // Enter / E = 確認
        if (
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.E)
        )
        {
            ConfirmEnter();
        }


        // Esc = 取消
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
        }
    }


    // =========================================================
    // Enter Trigger
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }


        playerInside = true;


        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }


        Debug.Log(
            $"🚪 發現傳送門：{targetSceneName}"
        );
    }


    // =========================================================
    // Exit Trigger
    // =========================================================

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }


        playerInside = false;


        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }


    // =========================================================
    // Confirm
    // =========================================================

    public void ConfirmEnter()
    {
        if (!playerInside)
        {
            return;
        }


        if (
            string.IsNullOrWhiteSpace(
                targetSceneName
            )
        )
        {
            Debug.LogError(
                "❌ ScenePortal 沒有設定 Target Scene Name！"
            );

            return;
        }


        Debug.Log(
            $"🌎 前往 Scene：{targetSceneName}"
        );


        SceneManager.LoadScene(
            targetSceneName
        );
    }


    // =========================================================
    // Cancel
    // =========================================================

    public void Cancel()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }
}