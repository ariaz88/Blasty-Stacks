using UnityEngine;

[CreateAssetMenu(menuName = "TD/Progression Config (Curves)")]

public class ProgressionConfigSO : ScriptableObject
{
    [Header("Curve input: x = level (1..Max), y = per-level percent (e.g., 0.08 = +8%)")]
    public AnimationCurve atkPctByLevel = AnimationCurve.Linear(1, 0.08f, 50, 0.03f);
    public AnimationCurve hpPctByLevel = AnimationCurve.Linear(1, 0.10f, 50, 0.04f);
    public AnimationCurve atkSpdPctByLevel = AnimationCurve.Linear(1, 0.02f, 50, 0.01f);

    // There is no movePctByLevel. moveSpeed does not grow - it is a fixed 0.3 for
    // every unit on both sides and is not part of progression at all.
    // Arash's directive, 2026-09-10.

    public AnimationCurve defPctByLevel = AnimationCurve.Linear(1, 0.02f, 50, 0.005f);

    // attackRange has no growth curve: range is excluded from CP and from
    // progression. See A2 in Docs/cp-analysis/NEXT_VERSION_CHANGES.md.

    [Header("Safety")]
    [Tooltip("Clamp evaluated percents into this range to avoid wild values.")]
    public Vector2 pctClamp = new Vector2(-0.25f, 0.50f); // -25% .. +50% per level
}
