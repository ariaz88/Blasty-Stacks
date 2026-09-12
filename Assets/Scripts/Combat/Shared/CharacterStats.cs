using System.Collections.Generic;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public float currentHP ;
    public float maxHealth = 100;
    public void Init(float maxHP)
    {
        currentHP = Mathf.Max(1f, maxHP);
    }

    public bool IsAlive => currentHP > 0f;

    /// <summary>
    /// How many weapon blows this unit has actually taken. Reset with the unit,
    /// never serialized - a pooled or revived unit starts its count again.
    /// </summary>
    [System.NonSerialized] private int hitsTaken;
    public int HitsTaken => hitsTaken;

    /// <summary>
    /// THE FOUR-HIT RULE. Nobody - hero or enemy, any stage - dies in fewer than
    /// <see cref="LevelBattleRules.MinHitsToKillAnyone"/> blows.
    ///
    /// WHY IT LIVES HERE AND NOT IN CPBattleController.
    /// It was implemented there first and did not hold in play: a level 4 enemy
    /// still died in two hits. That method returns the damage UNTOUCHED on several
    /// paths that are invisible at runtime - no battle prepared, the unit missing
    /// from the registered hero/enemy sets, a stale static Instance left by a
    /// previous battle, or a scene mismatch. Every one of those silently skips the
    /// ceiling. Down here there is nothing to miss: the unit clamps its own
    /// incoming damage, so the rule holds even with no CP battle in the scene at
    /// all - which is also what makes it true for stage 6+.
    ///
    /// Three mechanisms, deliberately:
    ///   1. a CEILING of maxHealth / HitsToKillMe. That divisor is THIS unit's own
    ///      number, interpolated from its defence between 8 and 11, so a tougher
    ///      character genuinely takes 9, 10 or 11 blows. A single fixed ceiling was
    ///      tried first and measured 240 of 240 matchups onto exactly 8 - after CP
    ///      normalisation every attacker clears a flat 12.5% blow, so it bound
    ///      every time and all variety collapsed;
    ///   2. a FLOOR of maxHealth/11, so a very soft blow is lifted and no duel
    ///      drags past about eleven exchanges;
    ///   3. a lethality guard - before the eighth blow the unit cannot be reduced
    ///      past a sliver of health. This catches float drift and any damage that
    ///      arrives outside the normal weapon path.
    ///
    /// Neither bound applies to a blow that was already zero: damage cancelled
    /// upstream (pause, battle over, a spent budget) stays cancelled.
    /// </summary>
    public float ClampIncomingBlow(float damage, Object attacker = null)
    {
        damage = Mathf.Max(0f, damage);
        if (damage <= 0f || maxHealth <= 0f) return damage;

        hitsTaken++;
        if (attacker) attackers.Add(attacker);

        float ceiling = maxHealth / HitsToKillMe;
        float floor = maxHealth / LevelBattleRules.MaxHitsToKillAnyone;
        damage = Mathf.Clamp(damage, Mathf.Min(floor, ceiling), ceiling);

        if (hitsTaken < LevelBattleRules.MinHitsToKillAnyone)
            damage = Mathf.Min(damage, Mathf.Max(0f, currentHP - maxHealth * 0.001f));

        return Mathf.Max(0f, damage);
    }

    /// <summary>
    /// This unit's defence, for the toughness band. Overridden by PlayerStats and
    /// EnemyStats to read the live stat block, so an upgrade that raises defence
    /// immediately moves the character up the band.
    /// </summary>
    protected virtual float Defense => 0f;

    /// <summary>Blows this unit takes to die: 8 at no defence, up to 11 at 80+.</summary>
    public int HitsToKillMe => LevelBattleRules.HitsToKill(Defense);

    /// <summary>Clears the hit history, for a revived or re-pooled unit.</summary>
    public void ResetHitHistory() { hitsTaken = 0; attackers.Clear(); }

    // Who has actually landed a blow on this unit. Needed to tell a genuine rule
    // break from the ordinary case of several attackers sharing one kill.
    [System.NonSerialized] private readonly HashSet<Object> attackers = new HashSet<Object>();

    /// <summary>
    /// One line per death, so "it died in four hits" can be checked instead of
    /// argued about. Prints the TOTAL blows the unit absorbed, how many different
    /// attackers landed them, and the number the band required.
    ///
    /// Deliberately unconditional: a handful of lines per battle is nothing, and
    /// the alternative is another round of guessing from a video.
    /// </summary>
    public void ReportDeath()
    {
        int required = HitsToKillMe;
        string verdict = hitsTaken >= required ? "OK" : "<<< RULE BROKEN";
        Debug.Log($"[HITS] '{name}' died after {hitsTaken} blow(s) from {attackers.Count} attacker(s). " +
                  $"Required at least {required} (defence {Defense:F0}). {verdict}", this);
    }
}
