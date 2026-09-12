using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stages 1-5: reference CP calibration and the guaranteed-win model.
/// Never ticks HP, kills units remotely, stops waves, or destroys a gate.
///
/// THE MODEL (Arash, 2026-09-11)
/// -----------------------------
/// Exactly ONE hero is assisted - the strongest on the field, chosen when the
/// battle is prepared. It carries the guarantee on its own. Every other hero
/// fights with NO assistance whatsoever: it wins, loses or dies exactly as it
/// would with this component absent, and the damage it deals on the way is real.
/// That is what makes the battle read as genuine rather than staged.
///
/// The protected hero is not invulnerable and has no blanket HP floor. Instead
/// each INDIVIDUAL enemy gets a lifetime allowance of how much of that hero it may
/// ever remove - see LevelBattleRules.DamageBudgetPerEnemy - sized so the hero
/// survives even in the worst case where every ally falls and it faces the entire
/// enemy army alone. The allowance is also spread over at least four hits, so no
/// single blow can ever take a visible chunk out of it.
///
/// WHAT THIS REPLACED, and why none of it may come back:
///   * a 25%-of-max-HP cap on EVERY hit in the game, which flattened every
///     exchange on both sides;
///   * a rule that nothing may die before the fourth hit it receives, which made
///     the outnumbered fast-kill below impossible;
///   * a last-survivor 1%-HP floor that triggered at the end of a fight rather
///     than being budgeted before it;
///   * a damage reduction hard-coded to one specific level and hero count.
/// </summary>
public sealed class CPBattleController : MonoBehaviour
{
    public static CPBattleController Instance { get; private set; }
    public bool IsPrepared { get; private set; }
    public bool PlayerShouldWin { get; private set; }
    public double PlayerCP { get; private set; }
    public double EnemyCP { get; private set; }
    public double EnemyScale { get; private set; }
    public string BattleResult { get; private set; }
    public int Level { get; private set; }
    public int HeroCount { get; private set; }
    public int HeroDeaths { get; private set; }
    public int EnemyDeaths { get; private set; }
    public int FewestHitsBeforeDeath { get; private set; } = int.MaxValue;

    readonly HashSet<CharacterStats> heroes = new();
    readonly HashSet<CharacterStats> enemies = new();
    readonly Dictionary<CharacterStats, int> receivedHits = new();

    // ---- the protected hero ----
    PlayerStats champion;
    PlayerManager championUnit;
    float budgetPerEnemy;                 // fraction of champion max HP, per enemy
    float perHitCap;                      // budgetPerEnemy / MinHitsToSpendBudget
    int plannedEnemyCount;
    bool tutorialExchange;                // levels 1-3: the scripted presentation
    readonly Dictionary<object, float> spentByEnemy = new();

    /// <summary>The one hero carrying the guarantee, or null when the player is meant to lose.</summary>
    public PlayerManager Champion => championUnit;

    /// <summary>Team-wide stat factor from normalisation, so late reinforcements match.</summary>
    float teamFactor;

    /// <summary>
    /// Ensures the melee unstick helper and applies the team normalisation factor
    /// to a hero that arrived AFTER the battle was prepared (a mid-battle
    /// reinforcement). Heroes present at Prepare() time are scaled there instead,
    /// in one pass, because team normalisation needs to see the whole roster.
    /// </summary>
    public static void CalibrateHero(PlayerManager hero, int level)
    {
        if (!hero || !LevelBattleRules.AppliesTo(level)) return;
        EnsureRecovery(hero.gameObject);

        var battle = Instance;
        if (!battle || !battle.IsPrepared || battle.teamFactor <= 0f) return;
        ApplyFactor(hero.unitStats, battle.teamFactor);
        ResetHealth(hero.GetComponent<PlayerStats>(), hero.unitStats);
    }

    static void EnsureRecovery(GameObject go)
    {
        if (!go.GetComponent<MeleeContactRecovery>()) go.AddComponent<MeleeContactRecovery>();
    }

    /// <summary>
    /// Scales a unit to an absolute CP target. Equal ATK/HP scaling keeps each
    /// archetype's offensive/defensive shape; CP is linear in both, so each stat
    /// moves by the square root of the wanted factor.
    /// </summary>
    static void ScaleToCP(UnitStatsRuntime stats, double target)
    {
        double raw = CPCalculator.UnitPower(stats);
        if (!(raw > 0)) throw new ArgumentException("Combat stats must have positive CP.");
        ApplyFactor(stats, (float)Math.Sqrt(target / raw));
    }

    static void ApplyFactor(UnitStatsRuntime stats, float factor)
    {
        if (stats == null || !(factor > 0f)) return;
        stats.attack *= factor;
        stats.maxHP *= factor;
    }

    static void ResetHealth(CharacterStats health, UnitStatsRuntime stats)
    {
        if (!health) throw new ArgumentException("Missing combat health.");
        health.currentHP = health.maxHealth = stats.maxHP;
    }

    public bool Prepare(EnemySpawner source, PlayerWaveManager waves)
    {
        try
        {
            Level = waves.RuleLevel;
            if (!LevelBattleRules.AppliesTo(Level) || !waves.DeploymentsReady) return false;

            var roster = new List<PlayerManager>();
            foreach (var hero in waves.ReleasedHeroes) if (hero) roster.Add(hero);
            if (roster.Count == 0) throw new ArgumentException("No heroes were released for this battle.");

            // ---- TEAM-TOTAL normalisation, not per-hero ----
            // Scaling every hero to an identical CP made the whole roster
            // mathematically tied, so "the strongest hero" did not exist and the
            // model below had nothing to protect. Normalising the TOTAL instead
            // keeps Player CP exactly on the workbook figure AND preserves each
            // hero's share of it, so upgrades and archetypes still rank.
            double rawTeam = 0;
            foreach (var hero in roster) rawTeam += CPCalculator.UnitPower(hero.unitStats);
            if (!(rawTeam > 0)) throw new ArgumentException("Hero roster must have positive CP.");

            PlayerCP = roster.Count * LevelBattleRules.PerHeroCP(Level);
            teamFactor = (float)Math.Sqrt(PlayerCP / rawTeam);

            foreach (var hero in roster)
            {
                ApplyFactor(hero.unitStats, teamFactor);
                var health = hero.GetComponent<PlayerStats>();
                ResetHealth(health, hero.unitStats);
                heroes.Add(health);
                EnsureRecovery(hero.gameObject);
            }
            HeroCount = roster.Count;

            EnemyCP = LevelBattleRules.ReferenceEnemyCP(Level);
            EnemyScale = EnemyCP / source.PlannedEnemyCP();
            if (double.IsNaN(EnemyScale) || double.IsInfinity(EnemyScale) || EnemyScale <= 0)
                throw new ArgumentException("Enemy roster must have positive finite CP.");

            PlayerShouldWin = waves.MatchesCleared >= LevelBattleRules.FirstWinningMatch(Level);
            if ((PlayerCP > EnemyCP) != PlayerShouldWin)
                throw new ArgumentException("Reference CP disagrees with the deployment outcome.");
            BattleResult = "Expected " + (PlayerShouldWin ? "Win" : "Loss");

            ChooseChampion(roster);

            Instance = this;
            IsPrepared = true;
            Debug.Log($"[CP Battle] Stage {Level}, heroes {HeroCount}, enemies {plannedEnemyCount}, " +
                      $"CP {PlayerCP:F1}/{EnemyCP:F1} (R={PlayerCP / EnemyCP:F2}). {BattleResult}. " +
                      (champion
                          ? $"Protected: '{championUnit.name}' - each enemy may take at most " +
                            $"{budgetPerEnemy * 100f:F0}% of it, over at least " +
                            $"{LevelBattleRules.MinHitsToSpendBudget} hits."
                          : "No hero is protected; this battle is a loss and runs unassisted.") +
                      (tutorialExchange
                          ? $" Tutorial exchange: enemies die on hit {LevelBattleRules.TutorialHitsToKillEnemy}, " +
                            $"heroes lose {LevelBattleRules.TutorialHeroDamagePerHit * 100f:F0}% per hit, " +
                            $"enemy speed x{LevelBattleRules.TutorialEnemySpeedScale:F2}."
                          : ""), this);
            return true;
        }
        catch (ArgumentException e) { Debug.LogError("[CP Battle] " + e.Message, this); return false; }
    }

    /// <summary>
    /// Picks the single hero that carries the guarantee: the strongest by CP after
    /// normalisation. Ties break on instance id so the choice is stable rather than
    /// depending on spawn order.
    ///
    /// Nothing is chosen when the player is meant to LOSE - a lost battle runs with
    /// no assistance at all, which is what lets a Slow Loss earn its length from the
    /// armies being near-matched instead of from a damage multiplier.
    /// </summary>
    void ChooseChampion(List<PlayerManager> roster)
    {
        plannedEnemyCount = Mathf.Max(1, Level);   // stages 1-5 field exactly `level` enemies
        tutorialExchange = LevelBattleRules.IsTutorialPresentation(Level);
        if (!PlayerShouldWin) return;

        PlayerManager best = null;
        double bestCP = double.NegativeInfinity;
        foreach (var hero in roster)
        {
            double cp = CPCalculator.UnitPower(hero.unitStats);
            if (cp > bestCP || (cp == bestCP && best && hero.GetInstanceID() < best.GetInstanceID()))
            {
                bestCP = cp;
                best = hero;
            }
        }
        if (!best) return;

        championUnit = best;
        champion = best.GetComponent<PlayerStats>();
        budgetPerEnemy = LevelBattleRules.DamageBudgetPerEnemy(plannedEnemyCount);
        perHitCap = budgetPerEnemy / LevelBattleRules.MinHitsToSpendBudget;
    }

    int enemiesRegistered;

    public void RegisterEnemy(EnemyManager enemy)
    {
        // Per-unit CP where the design authored it, otherwise share out the team
        // budget in proportion to each type's authored strength.
        var units = LevelBattleRules.EnemyUnitCPs(Level);
        double target = units != null && enemiesRegistered < units.Length
            ? units[enemiesRegistered]
            : CPCalculator.UnitPower(enemy.unitStats) * EnemyScale;
        enemiesRegistered++;

        ScaleToCP(enemy.unitStats, target);

        // Levels 1-3 walk their enemies in slowly. With the exchange scripted to
        // four hits per kill, a full-speed third enemy can stroll past the fight
        // and reach the player's base while the hero is still on the first two.
        if (LevelBattleRules.IsTutorialPresentation(Level))
            enemy.unitStats.moveSpeed *= LevelBattleRules.TutorialEnemySpeedScale;

        ResetHealth(enemy.GetComponent<EnemyStats>(), enemy.unitStats);
        enemy.cp = CPCalculator.DisplayCP(CPCalculator.UnitPower(enemy.unitStats));
        enemies.Add(enemy.GetComponent<EnemyStats>());
        EnsureRecovery(enemy.gameObject);
    }

    // A defended base cannot be bypassed by an extra attacker walking past the
    // battle. Once its defending army is defeated, normal weapon damage applies.
    public static bool HasLivingDefenders(Component gate)
    {
        var battle = Instance;
        if (!battle || !battle.IsPrepared || !gate || gate.gameObject.scene != battle.gameObject.scene) return false;
        var defenders = gate is PlayerGateStats ? battle.heroes : battle.enemies;
        foreach (var unit in defenders) if (unit && unit.currentHP > 0) return true;
        return false;
    }

    /// <summary>
    /// Called only when a weapon actually hits this particular unit.
    ///
    /// <paramref name="attacker"/> is the unit that swung, and it is REQUIRED for
    /// the model to work: budgets are tracked per individual enemy, so the same
    /// blow from two different enemies must be accounted separately.
    ///
    /// Everything that is not the protected hero passes through untouched.
    /// </summary>
    public static float AdjustIncomingDamage(CharacterStats target, float damage, Component attacker = null)
    {
        damage = Mathf.Max(0, damage);
        var battle = Instance;
        if (!battle || !battle.IsPrepared || !target || target.gameObject.scene != battle.gameObject.scene)
            return damage;

        bool isHero = battle.heroes.Contains(target);
        bool isEnemy = !isHero && battle.enemies.Contains(target);
        if (!isHero && !isEnemy) return damage;

        if (GameplayPause.IsPaused || !LevelGameManager.IsBattleRunning) return 0;
        if (damage <= 0 || target.currentHP <= 0) return 0;

        int hits = battle.receivedHits.TryGetValue(target, out int previous) ? previous + 1 : 1;
        battle.receivedHits[target] = hits;

        // NOTE: the four-hit ceiling USED to live here and it did not hold - a
        // level 4 enemy still died in two hits, because every early return in this
        // method (no battle prepared, unit missing from the registered sets, stale
        // static Instance, scene mismatch) skips it silently. It now lives in
        // CharacterStats.ClampIncomingBlow, applied by PlayerStats/EnemyStats after
        // this call, where nothing can bypass it. Do not re-add it here.

        // Levels 1-3 script the exchange outright and have the final say - in
        // particular their killing blow is taken from currentHP, and the clamp
        // downstream lets a fourth-hit kill through rather than clipping it.
        if (battle.tutorialExchange)
            damage = battle.ScriptTutorialBlow(target, isEnemy, hits);

        // The budget still applies on top of the tutorial figure, so the protected
        // hero can never be worn past its reserve however long the fight runs.
        if (isHero && target == battle.champion)
            damage = battle.LimitDamageToChampion(attacker, damage);

        if (damage <= 0) return 0;

        if (damage >= target.currentHP)
        {
            if (isHero) battle.HeroDeaths++; else battle.EnemyDeaths++;
            battle.FewestHitsBeforeDeath = Math.Min(battle.FewestHitsBeforeDeath, hits);
        }
        return damage;
    }

    /// <summary>
    /// Levels 1-3 only: the exchange is scripted so it LOOKS like a fight.
    ///
    /// At these CP ratios real combat reads badly - a level 1 hero at CP 100
    /// against a CP 35 enemy deletes it in one blow, which is what was reported.
    /// So an enemy loses exactly a quarter of its maximum per hit and dies on the
    /// fourth, and a hero loses a flat 7% per hit. Both are set in BOTH directions,
    /// not capped, so the tutorial reads identically whatever the units were
    /// authored at - and the kill is taken from currentHP on the fourth swing so
    /// float drift can never leave a sliver of health behind.
    /// </summary>
    float ScriptTutorialBlow(CharacterStats target, bool isEnemy, int hits)
    {
        if (target.maxHealth <= 0f) return 0f;

        if (isEnemy)
        {
            // The DEFENDER's own number from the toughness band, not a constant.
            // A fixed divisor here would fight CharacterStats.ClampIncomingBlow:
            // a defence-65 enemy is clamped to maxHealth/10, so a scripted
            // maxHealth/8 blow - and the currentHP kill that follows it - would
            // both be clipped and the enemy would outlive its own script.
            int need = target.HitsToKillMe;
            return hits >= need ? target.currentHP : target.maxHealth / need;
        }

        return target.maxHealth * LevelBattleRules.TutorialHeroDamagePerHit;
    }

    /// <summary>
    /// The whole guarantee, in three clamps:
    ///   1. no single blow exceeds budget / 4 of the hero's maximum HP;
    ///   2. each enemy has a LIFETIME allowance and stops mattering once spent;
    ///   3. a hard reserve that all the budgets together still leave standing.
    /// Clamp 3 is belt-and-braces - with 1 and 2 honoured it cannot bind - but it
    /// means an unexpected damage source can never take the hero below the floor.
    /// </summary>
    float LimitDamageToChampion(Component attacker, float damage)
    {
        float max = champion.maxHealth;
        if (max <= 0f) return damage;

        damage = Mathf.Min(damage, max * perHitCap);

        object key = attacker ? (object)attacker : this;
        spentByEnemy.TryGetValue(key, out float spent);
        damage = Mathf.Min(damage, Mathf.Max(0f, max * budgetPerEnemy - spent));
        spentByEnemy[key] = spent + damage;

        float reserve = max * Mathf.Max(0f, 1f - budgetPerEnemy * plannedEnemyCount);
        damage = Mathf.Min(damage, Mathf.Max(0f, champion.currentHP - reserve));

        return Mathf.Max(0f, damage);
    }

    // NOTE: "RushKill" was REMOVED on 2026-09-11. It let a lone, outnumbered hero
    // kill in two hits, which only ever applied to levels 2 and 3 - exactly the
    // levels the tutorial script now covers at four. The pressure it relieved is
    // handled instead by slowing the enemy approach (TutorialEnemySpeedScale),
    // which looks far better than a hero deleting things in two blows.

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
