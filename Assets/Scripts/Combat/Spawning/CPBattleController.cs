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
                            $"heroes lose {LevelBattleRules.TutorialHeroDamagePerHit * 100f:F0}% per hit."
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

        // NOTE: levels 1-3 used to walk their enemies in at 60% speed so a spare
        // enemy could not reach the player's base while the hero was busy. Arash
        // dropped that on 2026-09-12 in favour of one uniform speed for everyone,
        // authored on the stat assets. Do not reintroduce a per-level multiplier
        // here - change the stat block instead, so the Inspector stays truthful.

        ResetHealth(enemy.GetComponent<EnemyStats>(), enemy.unitStats);
        enemy.cp = CPCalculator.DisplayCP(CPCalculator.UnitPower(enemy.unitStats));
        enemies.Add(enemy.GetComponent<EnemyStats>());
        EnsureRecovery(enemy.gameObject);
    }

    /// <summary>
    /// TRUE while levels 1-3 are running their scripted exchange. CharacterStats
    /// reads it so those levels use a FLAT eight blows for everyone, instead of the
    /// 8-11 defence band that governs levels 4 and up.
    /// </summary>
    public static bool IsTutorialExchange
    {
        get { var b = Instance; return b && b.IsPrepared && b.tutorialExchange; }
    }

    /// <summary>
    /// TRUE for the one hero this battle protects. PlayerStats uses it to suppress
    /// the CharacterStats damage FLOOR: the champion's blows are deliberately
    /// budgeted down (45%/8 = 5.6% at two enemies) and the floor would raise them
    /// straight back to maxHP/11 = 9.1%, spending the whole allowance in five hits.
    /// </summary>
    public static bool IsProtected(CharacterStats unit)
    {
        var battle = Instance;
        return battle && battle.IsPrepared && unit && battle.champion == unit;
    }

    /// <summary>
    /// TRUE while this scene is running a battle the player is MEANT TO WIN.
    ///
    /// The player's base reads it to decide how hard a blow lands on it (Arash,
    /// 2026-09-12): in a battle the player is supposed to win, an enemy that slips
    /// past the fight and reaches the castle must not be able to decide the match, so
    /// it only chips. In a battle the player is supposed to LOSE, the same enemy deals
    /// its normal damage - that loss is the point, and damping it would leave the
    /// match unable to end the way the workbook says it must.
    ///
    /// FALSE where no battle is prepared at all - stage 6+, or a scene entered
    /// directly - so those keep ordinary damage, which is the only sensible default
    /// when nothing has declared an intended outcome.
    ///
    /// The champion cannot die in a prepared win (its reserve floor in
    /// LimitDamageToChampion sees to that), so this can never leave a battle that the
    /// player has actually lost grinding away at an un-fellable base.
    /// </summary>
    public static bool BattleIsAnExpectedWin(Component member)
    {
        var battle = Instance;
        return battle && battle.IsPrepared && member
               && member.gameObject.scene == battle.gameObject.scene
               && battle.PlayerShouldWin;
    }

    // A defended base cannot be bypassed by an extra attacker walking past the
    // battle. Once its defending army is defeated, normal weapon damage applies.
    //
    // NOTE: only the ENEMY gate still uses this. The player's base moved to
    // BattleIsAnExpectedWin above on 2026-09-12 - what protects it is the battle
    // being a scripted win, not whether its defenders happen to be alive.
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
    /// Counts the enemies genuinely fighting the champion on each side of it.
    ///
    /// BEING NEARBY IS NOT ENOUGH, and that distinction is the whole rule. An enemy
    /// counts only when all of these hold:
    ///   * it is alive;
    ///   * it is actually FIGHTING THIS HERO - its own currentTarget is the champion,
    ///     not some other hero it happens to be standing beside;
    ///   * it is in its own attack position, i.e. close and level enough that its
    ///     weapon can really land;
    ///   * it is clearly to one side, using the same dead zone the movement code
    ///     uses, so an enemy directly above or below counts as neither.
    ///
    /// Two enemies flanking a hero while one of them is busy with somebody else is
    /// not a flank - the hero is only actually taking free hits when both of them
    /// are hitting IT.
    ///
    /// IT COUNTS RATHER THAN STOPPING AT ONE PER SIDE. The earlier version returned
    /// the moment it had found a left and a right, which is all a yes/no flank test
    /// needs - but it cannot tell 1-and-1 from 1-and-2, and those are now different
    /// rules. The extra work is bounded by the enemy count of the stage (5 at most).
    /// </summary>
    void CountEngagedFlankers(out int left, out int right)
    {
        left = right = 0;
        if (!champion) return;

        Vector2 hero = champion.transform.position;
        float engage = LevelBattleRules.FlankEngageRange;

        foreach (var unit in enemies)
        {
            if (!unit || unit.currentHP <= 0f) continue;

            // Cheap reject first, so the component lookups below are rare.
            Vector2 d = (Vector2)unit.transform.position - hero;
            if (d.sqrMagnitude > engage * engage) continue;

            bool onLeft = d.x <= -MeleeEngagement.SideDeadZoneX;
            bool onRight = d.x >= MeleeEngagement.SideDeadZoneX;
            if (!onLeft && !onRight) continue;          // dead ahead / behind

            var loco = unit.GetComponent<EnemyLocoMotion>();
            if (!loco || loco.currentTarget != champion) continue;   // fighting someone else
            if (!loco.IsInAttackPosition()) continue;                // cannot actually land a blow

            if (onLeft) left++; else right++;
        }
    }

    /// <summary>
    /// TRUE while the protected hero is genuinely caught between enemies that are
    /// able to hit it - at least one on its left and one on its right. Public for
    /// on-screen debugging.
    /// </summary>
    public bool ChampionIsFlanked
    {
        get { CountEngagedFlankers(out int left, out int right); return left > 0 && right > 0; }
    }

    /// <summary>
    /// TRUE while the hero is not merely flanked but PILED ON:
    /// <see cref="LevelBattleRules.SurroundedEnemyCount"/> or more enemies fighting it
    /// from both sides at once - one on one side and two on the other, say.
    ///
    /// Reported from a level 3 playthrough (Arash, 2026-09-12). Three enemies is not
    /// the same situation as being caught between two, and it gets its own per-blow
    /// ceiling rather than another halving. Public for on-screen debugging.
    /// </summary>
    public bool ChampionIsSurrounded
    {
        get
        {
            CountEngagedFlankers(out int left, out int right);
            return left > 0 && right > 0 && left + right >= LevelBattleRules.SurroundedEnemyCount;
        }
    }

    /// <summary>
    /// The whole guarantee, in four clamps:
    ///   1. no single blow exceeds budget / MinHitsToSpendBudget of the hero's max HP,
    ///      halved again while it is flanked and floored again while it is surrounded;
    ///   2. each enemy has a LIFETIME allowance and stops mattering once spent;
    ///   3. a hard reserve that all the budgets together still leave standing.
    /// Clamp 3 is belt-and-braces - with 1 and 2 honoured it cannot bind - but it
    /// means an unexpected damage source can never take the hero below the floor.
    /// </summary>
    float LimitDamageToChampion(Component attacker, float damage)
    {
        float max = champion.maxHealth;
        if (max <= 0f) return damage;

        // Caught between two enemies the hero can only ever face one of them, so the
        // other strikes it for free. Halve what both of them land - the blow AND the
        // cap, by the same factor, so a weak attacker is halved too rather than just
        // being left under an unchanged ceiling.
        //
        // The lifetime allowance below is NOT scaled: an enemy still gets its full
        // 30%, it simply needs twice as many blows to spend it. Being surrounded
        // buys the hero time, it does not make the enemies weaker overall.
        //
        // ONE COUNT, NOT TWO PROPERTY READS. ChampionIsFlanked and ChampionIsSurrounded
        // each walk the enemy set and touch components; reading both here would do the
        // same work twice per blow and, worse, could disagree if a unit moved between
        // them.
        CountEngagedFlankers(out int onLeft, out int onRight);
        bool flanked = onLeft > 0 && onRight > 0;
        float flankScale = flanked ? LevelBattleRules.FlankedDamageScale : 1f;
        damage = Mathf.Min(damage * flankScale, max * perHitCap * flankScale);

        // PILED ON - three or more of them, from both sides at once. Reported from a
        // level 3 playthrough: one enemy on one side of the hero and two on the other,
        // all three landing blows, and 3.75% a blow was too much for that. A CEILING
        // rather than a set value, because at level 5 the halved blow is already 0.94%
        // and setting 1% there would make five attackers hurt MORE than two.
        if (flanked && onLeft + onRight >= LevelBattleRules.SurroundedEnemyCount)
            damage = Mathf.Min(damage, max * LevelBattleRules.SurroundedDamagePerHit);

        object key = attacker ? (object)attacker : this;
        spentByEnemy.TryGetValue(key, out float spent);
        damage = Mathf.Min(damage, Mathf.Max(0f, max * budgetPerEnemy - spent));
        spentByEnemy[key] = spent + damage;

        // The reserve is the END-OF-BATTLE floor only: every enemy together cannot
        // take more than (budget x enemyCount), so this is what is left when all of
        // them have spent everything.
        //
        // IT IS DELIBERATELY *NOT* STAGED. A staged version was tried - unlocking
        // one enemy's budget per enemy still standing - so that the hero would still
        // be above half when the first of them died. Arash rejected it: each enemy's
        // allowance is ITS OWN, and two enemies striking at once may spend both. In
        // level 3 that is 30% + 30% = 60%, so the hero drops to 40% while the third
        // walks on the base, instead of freezing at a 70% floor.
        //
        // The per-enemy allowance above is the real limit; this line only stops the
        // last few blows of a fully-spent army from killing the protected hero.
        float reserve = max * Mathf.Max(0f, 1f - budgetPerEnemy * plannedEnemyCount);
        damage = Mathf.Min(damage, Mathf.Max(0f, champion.currentHP - reserve));

        return Mathf.Max(0f, damage);
    }

    // NOTE: "RushKill" was REMOVED on 2026-09-11. It let a lone, outnumbered hero
    // kill in two hits, which only ever applied to levels 2 and 3 - exactly the
    // levels the tutorial script now covers at four. The pressure it relieved is
    // handled instead by the eight-blow script, which looks far better than a hero
    // deleting things in two.

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
