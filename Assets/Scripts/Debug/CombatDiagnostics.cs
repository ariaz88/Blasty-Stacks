using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic logging for the "XP filled without the hero striking" investigation
/// (2026-08-31). Every log line is prefixed [CD] so the console can be filtered to just these.
///
/// This exists to answer three questions with evidence rather than inference:
///   1. Does a player ever damage an enemy while NOT in AttackState?
///   2. Does an enemy ever damage a player while NOT attacking?
///   3. When XP is granted, which enemy died, and what was the last thing that damaged it?
///
/// DELETE THIS FILE AND ITS CALL SITES once the cause is settled. The call sites are all
/// guarded by <see cref="Enabled"/>, so flipping that to false silences everything without
/// removing code.
/// </summary>
public static class CombatDiagnostics
{
    /// <summary>Master switch. Set false to silence without removing the call sites.</summary>
    public static bool Enabled = true;

    /// <summary>Logs every damage application, whether or not it looks legitimate.</summary>
    public static bool LogAllDamage = true;

    /// <summary>
    /// Logs each time a weapon's damage window opens. Noisy - it fires on every swing - but
    /// it is what reveals which clip is actually playing when the attack event lands.
    /// </summary>
    public static bool LogColliderWindow = true;

    private static float T => Time.time;

    public static void PlayerHit(string attacker, string victim, string stateName,
                                 bool inAttackState, float damage, float hpBefore, float hpAfter)
    {
        if (!Enabled) return;

        // A hit landed OUTSIDE the attack state is the smoking gun for contact damage.
        string tag = inAttackState ? "OK-ATTACKING" : ">>> NOT-ATTACKING <<<";

        Debug.Log($"[CD] {T:F2}s  PLAYER->ENEMY  {tag}\n" +
                  $"      attacker='{attacker}'  victim='{victim}'\n" +
                  $"      playerState='{stateName}'  dmg={damage:F1}  hp {hpBefore:F1} -> {hpAfter:F1}");
    }

    public static void EnemyHit(string attacker, string victim, bool isAttacking,
                                float damage, float hpBefore, float hpAfter)
    {
        if (!Enabled) return;

        string tag = isAttacking ? "OK-ATTACKING" : ">>> NOT-ATTACKING <<<";

        Debug.Log($"[CD] {T:F2}s  ENEMY->PLAYER  {tag}\n" +
                  $"      attacker='{attacker}'  victim='{victim}'\n" +
                  $"      dmg={damage:F1}  hp {hpBefore:F1} -> {hpAfter:F1}");
    }

    public static void EnemyDied(string enemy, float xpValue)
    {
        if (!Enabled) return;
        Debug.Log($"[CD] {T:F2}s  ENEMY DIED  '{enemy}'  grants xp={xpValue}");
    }

    public static void XpGranted(float amount, float xpNow, float threshold, int level)
    {
        if (!Enabled) return;
        Debug.Log($"[CD] {T:F2}s  XP +{amount}  ->  {xpNow}/{threshold}  (level {level})");
    }

    public static void LevelUp(int newLevel)
    {
        if (!Enabled) return;
        Debug.Log($"[CD] {T:F2}s  ===== LEVEL UP -> {newLevel} =====");
    }
}
