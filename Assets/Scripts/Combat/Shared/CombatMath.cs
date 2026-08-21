using UnityEngine;

public static class CombatMath
{
    // Diminishing-returns armor: damage = ATK * 100/(100 + DEF), clamped to >= 1
    public static float DamagePerHit(UnitStatsRuntime attacker, UnitStatsRuntime defender)
    {
        // Safety net: a unit whose stats were never computed used to crash the
        // whole trigger callback here. Deal no damage instead of throwing, so a
        // mis-configured unit is visible as "does nothing" rather than as a
        // NullReferenceException storm in the console.
        if (attacker == null || defender == null)
            return 0f;

        float A = Mathf.Max(0f, defender.defense);
        float raw = attacker.attack * (100f / (100f + A));
        return Mathf.Max(1f, raw);
    }

    public static float TimeBetweenAttacks(UnitStatsSO attacker)
    {
        return attacker.attackSpeed > 0f ? 1f / attacker.attackSpeed : Mathf.Infinity;
    }

    public static bool InRange(UnitStatsSO attacker, Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) <= attacker.attackRange + 1e-4f;
    }
}
