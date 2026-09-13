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
    public float ClampIncomingBlow(float damage, Object attacker = null, bool allowFloor = true)
    {
        damage = Mathf.Max(0f, damage);
        if (damage <= 0f || maxHealth <= 0f) return damage;

        hitsTaken++;
        if (attacker)
        {
            attackers.TryGetValue(attacker, out int landed);
            attackers[attacker] = landed + 1;
        }

        float ceiling = maxHealth / HitsToKillMe;
        damage = Mathf.Min(damage, ceiling);

        // The floor lifts blows that are weak because of STATS. It must never lift
        // one that a design budget deliberately lowered, which is what `allowFloor`
        // is for.
        //
        // THE BUG THIS FIXES. The protected hero's budget works out at 45%/8 = 5.6%
        // per blow at two enemies, but the floor is maxHP/11 = 9.1%, so every one of
        // those blows was raised back up to 9.1%. The hero then spent its whole 45%
        // allowance in five blows instead of eight and froze on the reserve for the
        // rest of the fight - reported from a level 2 playthrough as "it takes 10%
        // per hit, not 5%".
        if (allowFloor)
        {
            float floor = maxHealth / LevelBattleRules.MaxHitsToKillAnyone;
            damage = Mathf.Max(damage, Mathf.Min(floor, ceiling));
        }

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

    /// <summary>
    /// Blows this unit takes to die: 8 at no defence, up to 11 at defence 80+.
    ///
    /// LEVELS 1-3 ARE FLAT AT EIGHT. Those levels run a scripted exchange and the
    /// design calls for the kill to land on the eighth blow every time; letting the
    /// defence band apply there made a defence-65 enemy take 10 and a defence-78 one
    /// take 11, which is what was reported. The band is for levels 4 and up.
    /// </summary>
    public int HitsToKillMe =>
        CPBattleController.IsTutorialExchange
            ? LevelBattleRules.MinHitsToKillAnyone
            : LevelBattleRules.HitsToKill(Defense);

    /// <summary>Clears the hit history, for a revived or re-pooled unit.</summary>
    public void ResetHitHistory() { hitsTaken = 0; attackers.Clear(); }

    // Who has landed a blow on this unit, and HOW MANY each landed. The count per
    // attacker is the number a player watching one duel actually sees: a unit that
    // dies "after four hits" on screen has usually taken its full eight, half of
    // them from a neighbour whose weapon box happened to overlap it.
    [System.NonSerialized] private readonly Dictionary<Object, int> attackers = new Dictionary<Object, int>();

    /// <summary>
    /// One line per death, so "it died in four hits" can be checked instead of
    /// argued about. Prints the TOTAL blows the unit absorbed, the breakdown per
    /// attacker, and the number the band required.
    ///
    /// THE BREAKDOWN IS THE POINT. The rule counts blows per VICTIM; a player counts
    /// swings in the one duel being watched. Those are the same number only while a
    /// single attacker is landing everything. A level 4 recording (2026-09-12) showed
    /// an enemy "dying in four blows" that the log recorded as ten from two
    /// attackers - the second being a hero fighting somebody else whose weapon box
    /// reached across. Without the per-attacker split that reads as a broken clamp.
    ///
    /// Deliberately unconditional: a handful of lines per battle is nothing, and
    /// the alternative is another round of guessing from a video.
    /// </summary>
    public void ReportDeath()
    {
        int required = HitsToKillMe;
        string verdict = hitsTaken >= required ? "OK" : "<<< RULE BROKEN";

        int fewest = int.MaxValue;
        var split = new System.Text.StringBuilder();
        foreach (var pair in attackers)
        {
            if (split.Length > 0) split.Append(", ");
            split.Append($"{(pair.Key ? pair.Key.name : "<gone>")} x{pair.Value}");
            if (pair.Value < fewest) fewest = pair.Value;
        }
        if (attackers.Count == 0) { split.Append("none"); fewest = 0; }

        Debug.Log($"[HITS] '{name}' died after {hitsTaken} blow(s) from {attackers.Count} attacker(s) " +
                  $"[{split}]. Required at least {required} (defence {Defense:F0}). {verdict}" +
                  (attackers.Count > 1
                      ? $"  (a viewer watching only one of them would have counted {fewest})"
                      : ""), this);
    }
}
