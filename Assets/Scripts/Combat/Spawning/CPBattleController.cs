using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// READ-ONLY combat-power reporter. It measures; it never decides.
///
/// WHAT THIS IS
/// ------------
/// It keeps the roster of both sides, sums their CP, and reports the ratio. CP is
/// computed BOTTOM-UP - every unit's CP comes from that unit's own stats, and a
/// side's total is the plain sum of its units:
///
///     CP_unit  = ATK x AtkSpd x maxHP x (1 + DEF/100) / 200   (CPCalculator.UnitPower)
///     PlayerCP = sum of CP over the heroes on the field
///     EnemyCP  = sum of CP over the enemies on the field
///     Ratio    = PlayerCP / EnemyCP
///
/// Totals are READOUTS. Nothing here is ever written back into a unit, so a hero
/// fights with exactly the stats the player's upgrades produced and an enemy with
/// exactly the stats its type's growth curve produced.
///
/// WHAT IT REPLACED, AND WHY NONE OF IT MAY COME BACK (2026-09-16)
/// --------------------------------------------------------------
/// This file used to decide the winner before the battle began:
///   * it rescaled every hero to a fixed PerHeroCP(level) target, which ERASED the
///     player's Units-menu upgrades at battle start - the reported bug;
///   * it set PlayerShouldWin from MatchesCleared and threw if the CP figures
///     disagreed with that script;
///   * it picked one "champion" hero and gave every enemy a lifetime damage
///     allowance against it, so that hero could not lose;
///   * it scripted levels 1-3 outright - enemies died on a fixed blow, heroes lost
///     a flat 7% per blow;
///   * it distributed a team CP budget across the enemies (ScaleToCP), which is
///     top-down authoring and contradicts the bottom-up model above.
///
/// The battle now decides the winner on its own. CP only lets us PREDICT, with an
/// accuracy measured by CPCombatVerification, that the higher-CP side probably wins.
/// If a future change needs a side to win, change its STATS or its growth curve -
/// never this file.
///
/// The minimum-blows rule is NOT here and is not enforcement: it constrains how
/// long a fight lasts, not who wins it. It lives in CharacterStats.ClampIncomingBlow.
/// </summary>
public sealed class CPBattleController : MonoBehaviour
{
    public static CPBattleController Instance { get; private set; }

    /// <summary>TRUE once a battle roster has been snapshotted for this scene.</summary>
    public bool IsPrepared { get; private set; }

    /// <summary>Global stage number this battle is running, for the report line.</summary>
    public int Level { get; private set; }

    // ---- reserved for later tuning ----
    // Arash asked for a per-side curve to be available on this script. Nothing reads
    // these yet, and nothing may read them to change a unit's stats - they exist for
    // REPORTING or prediction weighting only.
    [Header("Reserved (unused) - per-side tuning curves")]
    [SerializeField] private AnimationCurve playerCurve = AnimationCurve.Linear(1f, 1f, 50f, 1f);
    [SerializeField] private AnimationCurve enemyCurve = AnimationCurve.Linear(1f, 1f, 50f, 1f);
    public AnimationCurve PlayerCurve => playerCurve;
    public AnimationCurve EnemyCurve => enemyCurve;

    readonly List<PlayerManager> heroUnits = new();
    readonly List<EnemyManager> enemyUnits = new();
    readonly HashSet<CharacterStats> heroes = new();
    readonly HashSet<CharacterStats> enemies = new();

    // ---- telemetry, for the verification harness ----
    public int HeroDeaths { get; private set; }
    public int EnemyDeaths { get; private set; }
    public int FewestHitsBeforeDeath { get; private set; } = int.MaxValue;
    readonly Dictionary<CharacterStats, int> receivedHits = new();

    public int HeroCount => heroUnits.Count;
    public int EnemyCount => enemyUnits.Count;

    // ------------------------------------------------------------------
    //  CP - summed live, never cached, never written back
    // ------------------------------------------------------------------

    /// <summary>Total CP of the heroes on the field: the plain sum of their individual CP.</summary>
    public double PlayerCP
    {
        get
        {
            double total = 0;
            foreach (var hero in heroUnits) if (hero) total += CPCalculator.UnitPower(hero.unitStats);
            return total;
        }
    }

    /// <summary>Total CP of the enemies on the field: the plain sum of their individual CP.</summary>
    public double EnemyCP
    {
        get
        {
            double total = 0;
            foreach (var enemy in enemyUnits) if (enemy) total += CPCalculator.UnitPower(enemy.unitStats);
            return total;
        }
    }

    /// <summary>
    /// PlayerCP / EnemyCP. Above 1 means the player is predicted to win - a
    /// PREDICTION, not a guarantee. How reliable it is at a given ratio is measured
    /// by CPCombatVerification, not asserted here.
    /// </summary>
    public double Ratio
    {
        get { double e = EnemyCP; return e > 0 ? PlayerCP / e : 0; }
    }

    // ------------------------------------------------------------------
    //  Registration
    // ------------------------------------------------------------------

    /// <summary>
    /// Snapshots whichever heroes are already on the field and starts reporting.
    /// Deliberately CANNOT fail: heroes now arrive progressively through
    /// HeroDeploymentSequencer's timed loads, so an empty roster at battle start is
    /// the normal case, not an error. Late arrivals register through
    /// <see cref="RegisterHero"/>.
    /// </summary>
    public void Prepare(EnemySpawner source, PlayerWaveManager waves)
    {
        Instance = this;
        IsPrepared = true;
        Level = waves ? waves.RuleLevel : LevelBattleRules.ResolveLevel(gameObject, source ? source.levelConfig : null);

        if (waves != null)
            foreach (var hero in waves.ReleasedHeroes) RegisterHero(hero);

        Debug.Log($"[CP Battle] Stage {Level} begins. Heroes {HeroCount} (CP {PlayerCP:F1}), " +
                  $"enemies {EnemyCount} (CP {EnemyCP:F1}). Predicted ratio R={Ratio:F2} - " +
                  "reported only; the battle decides the result.", this);
    }

    /// <summary>
    /// Registers a hero for CP reporting and gives it the melee unstick helper.
    ///
    /// The MeleeContactRecovery attachment is the reason this must be called for
    /// EVERY hero at EVERY stage. It used to be skipped outside stages 1-5, which
    /// left later stages without the unstick fix.
    /// </summary>
    public static void RegisterHero(PlayerManager hero)
    {
        if (!hero) return;
        EnsureRecovery(hero.gameObject);

        var battle = Instance;
        if (!battle || !battle.IsPrepared || battle.heroUnits.Contains(hero)) return;
        battle.heroUnits.Add(hero);
        var health = hero.GetComponent<PlayerStats>();
        if (health) battle.heroes.Add(health);
    }

    /// <summary>
    /// Registers an enemy for CP reporting and gives it the melee unstick helper.
    /// Its stats are NOT touched - they come from its own UnitStatsSO base scaled by
    /// its own type's ProgressionConfigSO curve, in EnemyManager.RebuildFromBase.
    /// </summary>
    public void RegisterEnemy(EnemyManager enemy)
    {
        if (!enemy || enemyUnits.Contains(enemy)) return;
        EnsureRecovery(enemy.gameObject);
        enemyUnits.Add(enemy);
        var health = enemy.GetComponent<EnemyStats>();
        if (health) enemies.Add(health);
    }

    static void EnsureRecovery(GameObject go)
    {
        if (!go.GetComponent<MeleeContactRecovery>()) go.AddComponent<MeleeContactRecovery>();
    }

    // ------------------------------------------------------------------
    //  Structural combat rule (outcome-neutral)
    // ------------------------------------------------------------------

    /// <summary>
    /// TRUE while the gate's own side still has living defenders.
    ///
    /// KEPT DELIBERATELY, and it is NOT outcome forcing: it never asks who is
    /// supposed to win, and it applies the same way whatever the CP figures say. It
    /// encodes one structural rule - you must defeat the defending army before you
    /// can fell the castle - which stops a single fast unit walking past a live
    /// battle and ending the match on its own. Without it the winner would often be
    /// decided by who ran past first rather than by who was stronger, which would
    /// make CP LESS predictive, not more.
    /// </summary>
    public static bool HasLivingDefenders(Component gate)
    {
        var battle = Instance;
        if (!battle || !battle.IsPrepared || !gate || gate.gameObject.scene != battle.gameObject.scene) return false;
        var defenders = gate is PlayerGateStats ? battle.heroes : battle.enemies;
        foreach (var unit in defenders) if (unit && unit.currentHP > 0) return true;
        return false;
    }

    // ------------------------------------------------------------------
    //  Telemetry
    // ------------------------------------------------------------------

    /// <summary>
    /// Every blow landed in this battle, by anyone. Set false to silence the log.
    /// Left ON by default while stages 1-3 are being tuned: the whole point is to
    /// settle "how many hits did that actually take" with a record instead of a
    /// count from watching, which counts ANIMATION SWINGS - including the ones that
    /// never connect - rather than blows that landed.
    /// </summary>
    public static bool LogEveryBlow = true;

    /// <summary>Blows landed on ANY unit since this battle began.</summary>
    public int TotalBlowsThisBattle { get; private set; }

    /// <summary>
    /// Records a blow that actually landed. PURELY OBSERVATIONAL - it returns
    /// nothing and changes nothing. Called by PlayerStats/EnemyStats AFTER the
    /// damage is final but BEFORE it is subtracted, so target.currentHP here is
    /// still the health the unit had going into this blow.
    /// </summary>
    public static void ReportBlow(CharacterStats target, float damage, Object attacker = null)
    {
        var battle = Instance;
        if (!battle || !battle.IsPrepared || !target || damage <= 0f) return;
        if (target.gameObject.scene != battle.gameObject.scene) return;

        bool isHero = battle.heroes.Contains(target);
        if (!isHero && !battle.enemies.Contains(target)) return;

        int hits = battle.receivedHits.TryGetValue(target, out int previous) ? previous + 1 : 1;
        battle.receivedHits[target] = hits;
        battle.TotalBlowsThisBattle++;

        float before = target.currentHP;
        float after = Mathf.Max(0f, before - damage);
        bool lethal = damage >= before;

        if (lethal)
        {
            if (isHero) battle.HeroDeaths++; else battle.EnemyDeaths++;
            if (hits < battle.FewestHitsBeforeDeath) battle.FewestHitsBeforeDeath = hits;
        }

        if (!LogEveryBlow) return;

        Debug.Log($"[BLOW #{battle.TotalBlowsThisBattle}] " +
                  $"{(attacker ? attacker.name : "<unknown>")} -> {target.name} " +
                  $"({(isHero ? "HERO" : "ENEMY")})  " +
                  $"dmg {damage:F1}   HP {before:F1} -> {after:F1} / {target.maxHealth:F0}   " +
                  $"this target has now taken {hits} blow(s)" +
                  (lethal ? $"   *** DEAD after {hits} blow(s) ***" : ""), target);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
