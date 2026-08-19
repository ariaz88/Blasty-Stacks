// CPWeightsConfigSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "TD/CP Weights (Curves)")]
public class CPWeightsConfigSO : ScriptableObject
{
    [Header("Curve inputs: x = level (1..Max), y = weight value")]
    public AnimationCurve wAttackByLevel = AnimationCurve.Linear(1, 1.00f, 50, 0.90f);
    public AnimationCurve wHPByLevel = AnimationCurve.Linear(1, 0.15f, 50, 0.12f);
    public AnimationCurve wMoveSpeedByLevel = AnimationCurve.Linear(1, 0.25f, 50, 0.20f);
    public AnimationCurve wAttackSpeedByLevel = AnimationCurve.Linear(1, 0.40f, 50, 0.45f);
    public AnimationCurve wDefenseByLevel = AnimationCurve.Linear(1, 0.00f, 50, 0.05f);

    // NEW: range matters a bit (keep small so CP isn’t dominated by range)
    public AnimationCurve wRangeByLevel = AnimationCurve.Linear(1, 0.05f, 50, 0.04f);

    [Header("Type flavor (curves allowed)")]
    public AnimationCurve meleeMultByLevel = AnimationCurve.Linear(1, 1.00f, 50, 1.00f);
    public AnimationCurve rangedMultByLevel = AnimationCurve.Linear(1, 1.05f, 50, 1.05f);

    [Header("Safety clamps for evaluated weights")]
    public Vector2 wClamp = new Vector2(-10f, 10f);

    [Header("Optional global multiplier for all weights (per level)")]
    public AnimationCurve globalWeightScaleByLevel = AnimationCurve.Linear(1, 1.00f, 50, 1.00f);
}
