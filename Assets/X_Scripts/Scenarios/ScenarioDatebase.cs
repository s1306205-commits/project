
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "ScenarioDatabase",
    menuName = "VR Scenario/Database"
)]
public class ScenarioDatabase : ScriptableObject
{
    public List<ScenarioData> scenarios =
        new List<ScenarioData>();
}
