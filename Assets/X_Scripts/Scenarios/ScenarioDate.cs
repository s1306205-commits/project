
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewScenario",
    menuName = "VR Scenario/Scenario Data"
)]
public class ScenarioData : ScriptableObject
{
    [Header("分類與題目資訊")]
    public string scenarioID = "CONFLICT_001";
    public string category = "人際衝突";
    public string title = "新情境";
    public string npcRole = "同班同學";

    [Header("情境描述")]
    [TextArea(3, 8)]
    public string situation;

    [Header("三種選擇（依 A、B、C 順序）")]
    [TextArea(2, 4)]
    public string choiceA;

    [TextArea(2, 4)]
    public string choiceB;

    [TextArea(2, 4)]
    public string choiceC;

    [Header("自由語音")]
    public bool allowFreeVoice = true;

    public string GetChoice(int index)
    {
        switch (index)
        {
            case 0: return choiceA;
            case 1: return choiceB;
            case 2: return choiceC;
            default: return string.Empty;
        }
    }
}
