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
    /// How many blows a unit takes to die is EMERGENT - it comes from the attacker's
    /// ATK against this unit's HP and defence, and nothing here enforces a number
    /// (Arash, 2026-09-16).
    ///
    /// Eight blows is where the BASE state should sit, and that is achieved by
    /// AUTHORING each unit's level-1 maxHP (see LevelBattleRules.BaseStateBlowsToKill),
    /// not by clamping damage at runtime. The count is SUPPOSED to drift:
    ///
    ///     enemies grow every stage; the player upgrades only every ~5 stages, so a
    ///     hero slides from ~8 blows at stage 1 to ~7 at stage 3 to ~5 at stage 4,
    ///     then an upgrade raises its ATK and HP together and restores ~8.
    ///
    /// That slide IS the difficulty signal - "you are under-levelled, go upgrade".
    /// A hard eight-blow floor used to live here and it erased that signal entirely,
    /// keeping an under-levelled hero alive for its full eight blows. It also
    /// silently protected whoever was losing, which is the class of behaviour this
    /// whole system was stripped of. Do not bring it back.
    ///
    /// WHAT REMAINS is <see cref="SafetyBlowFloor"/>: a deliberately LOOSE guard so a
    /// freak mismatch can never read as a one-shot. It is not balance - it is there
    /// so a bug or an extreme pairing looks like a fight rather than a teleport.
    ///
    /// Nothing raises a weak blow. A soft attacker genuinely grinds, which is what
    /// keeps ATK - and therefore CP - deciding real fights.
    ///
    /// A blow that was already zero stays zero: damage cancelled upstream (pause,
    /// battle over) is never revived.
    ///
    /// THE PAUSE GATE LIVES HERE. It used to sit in
    /// CPBattleController.AdjustIncomingDamage, which was the only thing blocking
    /// damage while the game was paused or before the battle started - PlayerStats
    /// and EnemyStats never checked it themselves. That method is gone, so the gate
    /// moved down to this chokepoint, where no early return can skip it.
    /// </summary>
    public float ClampIncomingBlow(float damage, Object attacker = null)
    {
        damage = Mathf.Max(0f, damage);
        if (damage <= 0f || maxHealth <= 0f) return damage;

        // Nothing lands while the game is paused or the battle is not running.
        if (GameplayPause.IsPaused || !LevelGameManager.IsBattleRunning) return 0f;

        hitsTaken++;
        if (attacker)
        {
            attackers.TryGetValue(attacker, out int landed);
            attackers[attacker] = landed + 1;
        }

        // LOOSE SAFETY GUARD ONLY - not balance. A single blow may never remove more
        // than 1/SafetyBlowFloor of maximum health, so nothing ever reads as a
        // one-shot. At a sane base calibration (~8 blows) this never binds; it exists
        // for freak pairings and for stages where one side has run far ahead.
        damage = Mathf.Min(damage, maxHealth / SafetyBlowFloor);

        return Mathf.Max(0f, damage);
    }

    /// <summary>
    /// This unit's defence. Overridden by PlayerStats and EnemyStats to read the
    /// live stat block. It does NOT select a hits-to-die band any more: defence works
    /// the ordinary way, reducing each incoming blow in CombatMath.DamagePerHit,
    /// which by itself makes a tough unit take more swings.
    /// </summary>
    protected virtual float Defense => 0f;

    /// <summary>
    /// The absolute fewest blows that may kill ANY unit, as a last-resort guard.
    ///
    /// Deliberately far below the base-state target of eight. It is NOT a balance
    /// lever: a fight that ends this fast means one side has massively outgrown the
    /// other, and the game should SHOW that rather than hide it. Four simply stops
    /// the extreme case from looking like an instant kill.
    ///
    /// FOUR IS ARASH'S NUMBER (2026-09-16). Raising it back towards eight would
    /// re-create the hard floor that erased the under-levelled signal - an
    /// under-levelled hero is MEANT to start dying in 7, then 5 blows. Set base HP
    /// instead.
    /// </summary>
    public const int SafetyBlowFloor = 4;

    /// <summary>
    /// The base-state expectation for this unit, for logging and for
    /// CharacterStats.ReportDeath - NOT a rule anything enforces.
    /// </summary>
    public int HitsToKillMe => LevelBattleRules.BaseStateBlowsToKill;

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
        // The verdict is INFORMATIONAL, not a pass/fail. Blows-to-die is emergent, so
        // a number under the base-state expectation is not a broken rule - it usually
        // means this unit is out-levelled, which is exactly what the log should make
        // visible. Only the safety floor is ever a genuine fault.
        int expected = HitsToKillMe;
        string verdict = hitsTaken < SafetyBlowFloor
            ? "<<< BELOW SAFETY FLOOR - investigate"
            : hitsTaken < expected ? "fast - this unit is out-levelled"
            : hitsTaken > expected ? "slow - this unit is over-levelled"
            : "on the base-state expectation";

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
                  $"[{split}]. Base-state expectation {expected} (defence {Defense:F0}). {verdict}" +
                  (attackers.Count > 1
                      ? $"  (a viewer watching only one of them would have counted {fewest})"
                      : ""), this);
    }
}
