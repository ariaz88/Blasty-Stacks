 using UnityEngine;

[CreateAssetMenu(menuName = "Units/Unit Definition", fileName = "UnitDefinitionSO")]
public class UnitDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Min(0)] public int unitId;                 // Unique per unit (used by save/runtime models)
    public string displayName = "Warrior";      // UI name
    public Sprite portrait;

    [Header("Visual")]
    public GameObject runtimePrefab;   // FULL player prefab (colliders, animator, scripts)

    public GameObject visualPrefab;          // existing character visual (Animator + mesh)
    public Vector3 uiVisualScale = Vector3.one;


    [Header("Unlock Requirement (Stage X-Y)")]
    [SerializeField] public int requiredLevelIndex = 1;              // e.g., 1
    [SerializeField] public int requiredStageIndexWithinLevel = 1;    // e.g., 16
                                                                      // UI card/detail portrait

    [Header("Design Stats")]
    public UnitStatsSO baseStats;               // Your existing SO with L1 stats


    [Header("Startup State (optional)")]
    [SerializeField] public bool startsDeployed = false;              // initial roster seed


    [Header("Tags (optional)")]
    public FighterType classType;               // Mirrors or complements baseStats.type for UI filtering, etc.

    public string GetRequirementText()
    {
        return $"Stage {requiredLevelIndex}-{requiredStageIndexWithinLevel}";
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (baseStats != null)
            classType = baseStats.type; // keep in sync for convenience (optional)
    }
#endif
}
