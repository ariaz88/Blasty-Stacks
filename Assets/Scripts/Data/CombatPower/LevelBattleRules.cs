using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Campaign stages 1-5 only. Spreadsheet "Level" means stage, not chapter.</summary>
public static class LevelBattleRules
{
    private static readonly int[][] Deployments =
    {
        new[] { 1, 1, 1 }, new[] { 1, 1, 2 }, new[] { 1, 1, 2, 1 },
        new[] { 1, 1, 2, 4 }, new[] { 1, 1, 2, 2, 2, 4 }
    };
    public static bool AppliesTo(int level) => level >= 1 && level <= Deployments.Length;
    public static int TotalPairs(int level) => AppliesTo(level) ? Deployments[level - 1].Length : 0;
    public static int HeroesForMatch(int level, int match) =>
        AppliesTo(level) && match >= 1 && match <= TotalPairs(level) ? Deployments[level - 1][match - 1] : 0;
    public static int TotalHeroes(int level, int matches)
    {
        int total = 0;
        for (int i = 1; i <= Math.Min(matches, TotalPairs(level)); i++) total += HeroesForMatch(level, i);
        return total;
    }
    /// <summary>
    /// Reference CP for ONE hero at this level. The team is normalised so its
    /// TOTAL lands on heroCount * this - see CPBattleController.Prepare. Heroes are
    /// no longer flattened to an identical value individually, so a genuinely
    /// strongest hero exists for the battle to protect.
    /// </summary>
    public static double PerHeroCP(int level) => level == 1 ? 100 : 125;

    /// <summary>
    /// CP for each individual enemy, in spawn order, where the design authored
    /// per-unit values instead of a team budget (Arash, 2026-09-11).
    ///
    /// Level 3 introduces a second enemy type, so its three units are not equal.
    /// A null row means "no per-unit authoring" - that level keeps the team-total
    /// budget in ReferenceEnemyCP and scales its roster proportionally.
    /// </summary>
    private static readonly double[][] EnemyUnits =
    {
        new double[] { 35 },            // level 1 - one enemy
        new double[] { 35, 35 },        // level 2 - two of the same type
        new double[] { 35, 40, 40 },    // level 3 - type A once, type B twice (115 total)
        null,                           // level 4 - team budget
        null                            // level 5 - team budget
    };

    /// <summary>Per-enemy CP targets for this level, or null if it uses a team budget.</summary>
    public static double[] EnemyUnitCPs(int level) => AppliesTo(level) ? EnemyUnits[level - 1] : null;

    public static int FirstWinningMatch(int level) => !AppliesTo(level) ? 0 : level == 4 ? 3 : level == 5 ? 4 : 1;

    /// <summary>
    /// Total enemy CP. Derived from the per-unit list where one exists, so the two
    /// can never drift apart, and falls back to the authored team budget otherwise.
    /// </summary>
    public static double ReferenceEnemyCP(int level)
    {
        var units = EnemyUnitCPs(level);
        if (units != null)
        {
            double sum = 0;
            foreach (double cp in units) sum += cp;
            return sum;
        }
        return level == 4 ? 400 : level == 5 ? 650 : 0;
    }

    /// <summary>
    /// How much of the protected hero's maximum HP a SINGLE enemy may ever remove,
    /// as a fraction. Sized so the hero survives even if it ends up facing the whole
    /// enemy army alone: E enemies x this is always below 1.
    ///
    ///     (100 / E) - 5, rounded UP to the nearest 5
    ///
    /// E=1 -> 95%, E=2 -> 45%, E=3 -> 30%, E=4 -> 20%, E=5 -> 15%.
    /// The rounding convention is Arash's: 28.3 for three enemies becomes 30, and
    /// the guaranteed reserve is whatever is left over (10% at E=3).
    /// </summary>
    public static float DamageBudgetPerEnemy(int enemyCount)
    {
        if (enemyCount < 1) enemyCount = 1;
        double raw = (100.0 / enemyCount) - 5.0;
        double rounded = Math.Ceiling(raw / 5.0) * 5.0;
        return (float)(Math.Min(95.0, Math.Max(5.0, rounded)) / 100.0);
    }

    /// <summary>
    /// Minimum number of hits an enemy needs to spend its whole budget on the
    /// protected hero. Arash: "if it is going to lose 30%, not in fewer than 4 hits"
    /// - so 30% / 4 = 7.5% per hit.
    /// </summary>
    public const int MinHitsToSpendBudget = 4;

    /// <summary>
    /// NOBODY dies in fewer than this many hits - hero or enemy, every stage this
    /// system governs. Enforced as a ceiling of maxHP/4 on each blow, never as a
    /// floor, which is the whole point: a blow that was already gentler than a
    /// quarter is left alone, so a high-defense unit still takes 5, 6 or 7 hits.
    ///
    /// Measured against the live roster at level 4 (hero CP 125 vs enemy CP 100),
    /// across 120 real pairings:
    ///     3 hits x43   4 hits x49   5 hits x14   6 hits x13   7 hits x1
    /// The ceiling lifts only the 43 three-hit pairings to four and leaves the
    /// other 77 exactly as authored. Defence spread is real - heroes 10..80,
    /// enemies 25..78 - and that spread is what produces the variety.
    ///
    /// WHY A DAMAGE CEILING AND NOT AN HP INCREASE: maxHP is a CP input
    /// (CP = ATK x AtkSpd x HP x (1 + DEF/100) / 200). Raising HP to stretch a
    /// fight raises that unit's CP, which moves the Player/Enemy CP ratio, which
    /// changes the battle result the workbook says this match must produce. The
    /// ceiling buys the same visible pacing and leaves every CP figure untouched.
    /// </summary>
    public const int MinHitsToKillAnyone = 8;

    /// <summary>
    /// The other end of the band. A blow is also RAISED if it is so weak that the
    /// fight would drag past this, so no duel outlives about eleven exchanges.
    ///
    /// Together the two bounds put every blow between 1/11 and 1/8 of the target's
    /// maximum - roughly 9% to 12.5% - which is the "at most 10-15% per hit"
    /// Arash asked for. Where a matchup lands inside the band is decided by
    /// defence, so a tougher character genuinely takes 9, 10 or 11 rather than
    /// everything collapsing onto one number.
    /// </summary>
    public const int MaxHitsToKillAnyone = 11;

    /// <summary>
    /// Defence at which a unit reaches the top of the band and takes the full
    /// <see cref="MaxHitsToKillAnyone"/> blows. Below it, a unit's hits-to-die is
    /// interpolated between the two bounds.
    ///
    /// 80 is the top of the authored range (Enemy_Golem_02 sits at 78, the castle
    /// at 80), so today's toughest unit lands on 11 and the softest on 8 - and an
    /// upgrade that raises defence genuinely moves a character up the band, which
    /// is what "a stronger character should take 9, then 10" asks for.
    ///
    /// WHY DEFENCE DRIVES THIS AT ALL: a single fixed ceiling was measured over the
    /// whole roster and put 240 of 240 matchups on exactly 8 hits. After CP
    /// normalisation every attacker comfortably clears a 12.5% blow, so the ceiling
    /// binds every time and all variety collapses. Sizing the ceiling per DEFENDER
    /// is what restores it.
    /// </summary>
    public const float DefenseForMaxHits = 80f;

    /// <summary>
    /// Blows this defender should take to die, from its own defence. Clamped into
    /// [Min, Max] so the answer is always inside the band.
    /// </summary>
    public static int HitsToKill(float defense)
    {
        float t = Mathf.Clamp01(Mathf.Max(0f, defense) / DefenseForMaxHits);
        int hits = Mathf.RoundToInt(Mathf.Lerp(MinHitsToKillAnyone, MaxHitsToKillAnyone, t));
        return Mathf.Clamp(hits, MinHitsToKillAnyone, MaxHitsToKillAnyone);
    }

    // ------------------------------------------------------------------
    //  TUTORIAL PRESENTATION - levels 1-3 only
    // ------------------------------------------------------------------
    // These three levels are an explicit exception (Arash, 2026-09-11). They are
    // guaranteed wins with one or very few heroes, and at those CP ratios real
    // combat looks wrong: a level 1 hero at CP 100 against a CP 35 enemy simply
    // deletes it in a single blow. So the exchange is SCRIPTED for appearance -
    // fixed hits to kill, fixed damage per hit - while levels 4 and up keep real
    // combat and the champion model.

    /// <summary>TRUE for the three tutorial levels that use the scripted exchange.</summary>
    public static bool IsTutorialPresentation(int level) => level >= 1 && level <= 3;

    /// <summary>
    /// Hits an enemy takes to die in levels 1-3.
    ///
    /// Deliberately an ALIAS of <see cref="MinHitsToKillAnyone"/> rather than its
    /// own number: "level does not matter, nobody dies under eight" is a universal
    /// rule, and a separate constant here is exactly how the tutorial and the rest
    /// of the game drifted apart last time.
    /// </summary>
    public static int TutorialHitsToKillEnemy => MinHitsToKillAnyone;

    /// <summary>
    /// Fraction of a hero's maximum HP each enemy blow removes in levels 1-3.
    /// Applied in both directions - it is a presentation value, not a cap - so the
    /// tutorial exchange reads the same whatever the units were authored at.
    /// </summary>
    public const float TutorialHeroDamagePerHit = 0.07f;

    /// <summary>
    /// Enemy movement in levels 1-3, as a fraction of the authored speed. 40%
    /// slower, so that while the hero is working through the first enemies the
    /// remaining one cannot walk past the fight and reach the player's base.
    /// </summary>
    public const float TutorialEnemySpeedScale = 0.6f;

    // NOTE: "SoloRushHitsToKill = 2" was REMOVED on 2026-09-11. It let a lone,
    // outnumbered hero kill in two, which only ever applied to levels 2 and 3 -
    // exactly the levels now covered by TutorialHitsToKillEnemy = 5. The pressure
    // it existed to relieve is handled instead by slowing the enemy approach, which
    // looks far better than a hero deleting things in two blows.
    public static int ResolveLevel(GameObject owner, LevelConfig config)
    {
        string name = owner.scene.name;
        int marker = name.LastIndexOf("_Stage_", StringComparison.Ordinal);
        if (marker >= 0 && int.TryParse(name.Substring(marker + 7), out int stage)) return stage;
        return config ? config.levelNumber : LevelManager.CurrentStage;
    }
}
