using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored per-stage pacing tables. Spreadsheet "Level" means STAGE, not chapter.
///
/// What is left here after 2026-09-16 is deliberately only the things that shape a
/// battle's SIZE and LENGTH - hero deployment counts, enemy counts per wave, and the
/// blows-to-die band. Every table that set a CP TARGET or decided a winner was
/// removed; see the note below. Nothing in this file may grow back into a rule that
/// knows which side is supposed to win.
///
/// Deployments and enemy waves cover stages 1-10.
/// </summary>
public static class LevelBattleRules
{
    private static readonly int[][] Deployments =
    {
        new[] { 1, 1, 1 }, new[] { 1, 1, 2 }, new[] { 1, 1, 2, 1 },
        new[] { 1, 1, 2, 4 }, new[] { 1, 1, 2, 2, 2, 4 },
        new[] { 1, 1, 2, 2, 4 }, new[] { 1, 1, 2, 1, 2, 2, 4 },
        new[] { 1, 1, 2, 2, 2, 4 }, new[] { 1, 2, 2, 2, 2, 4 },
        new[] { 1, 1, 2, 2, 2, 3, 4 }
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
    // ------------------------------------------------------------------
    //  REMOVED 2026-09-16 - the CP TARGET tables
    // ------------------------------------------------------------------
    // PerHeroCP, EnemyUnits/EnemyUnitCPs and ReferenceEnemyCP are gone, along with
    // FirstWinningMatch, DamageBudgetPerEnemy, MinHitsToSpendBudget and the
    // flank/surround/tutorial constants that enforced a pre-decided result.
    //
    // They described CP the wrong way round. CP is BOTTOM-UP: a unit's CP comes from
    // that unit's own stats, and a side's total is the plain sum of its units
    // (CPBattleController.PlayerCP / EnemyCP). A per-level CP TARGET can only be met
    // by writing stats back into units, which is exactly what erased the player's
    // upgrades. Enemy strength now comes from each type's own ProgressionConfigSO
    // growth curve; hero strength from the player's upgrades. Nothing normalises
    // either side, and nothing decides the winner in advance.
    //
    // The workbook figures those tables encoded (Wittle_Defender_Levels_6-10_Balance
    // .xlsx) are now a VALIDATION TARGET, not an input: author base stats and curves,
    // sum the CP, compare, adjust. Do not reintroduce them as constants.

    /// <summary>
    /// HOW MANY ENEMIES EACH LEVEL FIELDS, SPLIT INTO WAVES. One int per wave, in
    /// spawn order. Authored by Arash 2026-09-14.
    ///
    ///   L1 [1]     L2 [2]     L3 [3]
    ///   L4 [2,2]   L5 [2,3]              L4 cut 5 -> 4 (Arash, 2026-09-16): wave 1 is
    ///                                    two SIDE BY SIDE, wave 2 is one of each type
    ///   L6 [2,2,2] L7 [3,3]   L8 [3,3]   L6 went 5 -> 6 in THREE waves (Arash, 2026-09-21)
    ///   L9 [3,4]   L10 [3,4]
    ///
    /// Levels 4 and up MUST arrive in two or more waves - that is the point of the
    /// table, not the totals. Levels 4/5 already fielded five enemies; what
    /// changes is that they no longer all appear at once.
    ///
    /// Enemy counts are a separate pacing decision from hero deployment counts.
    ///
    /// COUNTS ONLY. This table says how many enemies arrive and when; it says nothing
    /// about how strong they are. Enemy strength comes from each type's UnitStatsSO
    /// base scaled by its own ProgressionConfigSO growth curve, and total enemy CP is
    /// whatever those units happen to sum to.
    ///
    /// Levels 11+ are unauthored: the LevelConfig asset's own waves are used verbatim,
    /// so counts stop growing there while stats keep rising. Extending this table (or
    /// replacing it with a curve) is open work.
    /// </summary>
    private static readonly int[][] EnemyWaves =
    {
        new[] { 1 },        // level 1
        new[] { 2 },        // level 2
        new[] { 3 },        // level 3
        new[] { 2, 2 },     // level 4 - two side by side, then one of each type
        new[] { 2, 3 },     // level 5
        new[] { 2, 2, 2 },  // level 6: SIX enemies in THREE waves of two (Arash, 2026-09-21).
                            // Wave 1 is the returning pair; waves 2 and 3 are the new type only.
        // Levels 7-10 re-authored 2026-09-22 (Arash). The new type introduced at
        // stage 6 is the ONLY newcomer through stage 10 - no Orc, no Crusader - so
        // the ramp is carried by count and by the per-stage CP curve instead.
        new[] { 1, 2, 2, 2 },       // level 7  - 7 enemies
        new[] { 1, 2, 2, 3 },       // level 8  - 8 enemies in FOUR waves (Arash,
                                    //            2026-09-22). The old 5th wave was
                                    //            dropped; the final wave is now the
                                    //            3-slot one: skeleton, zombie, skeleton.
        new[] { 1, 2, 2, 2, 2 },    // level 9  - 9 enemies (Arash, 2026-09-22).
                                    //            The LAST TWO waves are two
                                    //            skeletons each, nothing else.
        new[] { 2, 2, 3, 4 },       // level 10 - 11 enemies in FOUR waves
                                    //            (Arash, 2026-09-22, for testing).
                                    //            Final wave is skeletons only.
    };

    /// <summary>Highest level this wave table covers.</summary>
    public static int MaxAuthoredEnemyLevel => EnemyWaves.Length;

    /// <summary>
    /// TEMPORARY, STAGE 6 ONLY (Arash, 2026-09-21 - "برای این لول فعلا").
    ///
    /// A reward for clearing the WHOLE board: if the player completed 100% of the
    /// stage's matches, the FINAL enemy wave fields one extra enemy. Otherwise the
    /// authored count stands. At stage 6 that is 3 Skeleton Swordsmen in wave 3
    /// instead of 2, so a full clear is met with a harder last wave.
    ///
    /// This is a COUNT rule, like the rest of this file - it does not touch stats
    /// and does not decide a winner. It is checked when the final wave is about to
    /// SPAWN, not when the battle starts, because the player is still clearing
    /// matches while the earlier waves are being fought.
    ///
    /// Scoped to one level on purpose. Generalising it means giving
    /// <see cref="EnemyWaves"/> a per-level bonus column rather than widening this
    /// constant.
    /// </summary>
    public const int FullClearBonusLevel = 6;

    /// <summary>
    /// How many enemies the final wave actually fields. <paramref name="authoredCount"/>
    /// is what <see cref="EnemyWaveCounts"/> says; the bonus applies only on the
    /// level above and only on a 100% match clear.
    /// </summary>
    public static int FinalWaveCount(int level, int authoredCount, bool allMatchesCleared) =>
        level == FullClearBonusLevel && allMatchesCleared ? authoredCount + 1 : authoredCount;

    /// <summary>
    /// True when the player has cleared every match the deployment table defines
    /// for this level. Null-safe: no wave manager means "not cleared".
    /// </summary>
    public static bool AllMatchesCleared(int level, PlayerWaveManager heroes) =>
        heroes != null && AppliesTo(level) && heroes.MatchesCleared >= TotalPairs(level);

    /// <summary>
    /// Enemy count per wave for this level, in spawn order - or null when the
    /// level is past the authored range, in which case the LevelConfig asset's own
    /// waves are used unchanged.
    /// </summary>
    public static int[] EnemyWaveCounts(int level) =>
        level >= 1 && level <= EnemyWaves.Length ? EnemyWaves[level - 1] : null;

    /// <summary>Total enemies this level fields across every wave. 0 when unauthored.</summary>
    public static int TotalEnemies(int level)
    {
        var waves = EnemyWaveCounts(level);
        if (waves == null) return 0;

        int total = 0;
        foreach (int n in waves) total += n;
        return total;
    }

    /// <summary>
    /// What ONE blow takes off the PLAYER's base while that base still has living
    /// defenders, as a fraction of its maximum health.
    ///
    /// The base used to take literally nothing in that case, which read as a bug: an
    /// enemy that walked past the duel and hammered the castle produced no reaction
    /// at all. One percent is visible feedback without deciding anything - a hundred
    /// connected blows to fell a base, far longer than any battle here lasts. Once
    /// every hero has fallen the base takes full damage and the stage ends.
    ///
    /// IT IS GATED ON WHETHER DEFENDERS ARE ALIVE, not on any intended outcome. It
    /// briefly keyed off CPBattleController.BattleIsAnExpectedWin - damping the base
    /// only in battles a script had already decided the player would win. That
    /// question no longer exists; the battle decides its own result.
    ///
    /// Applied to the PLAYER's base only. The ENEMY gate keeps full immunity while
    /// its own defenders live (CPBattleController.HasLivingDefenders) rather than
    /// chipping, because felling that gate ENDS the stage in a win - a stray hero
    /// should not be able to finish a battle its army is losing.
    /// </summary>
    public const float BaseChipPerBlow = 0.01f;

    /// <summary>
    /// THE BASE-STATE CALIBRATION TARGET. It is an AUTHORING number, not a runtime
    /// rule - nothing enforces it during a battle (Arash, 2026-09-16).
    ///
    /// WHAT IT MEANS. At the BASE state - stage 1, nothing upgraded - a character
    /// should take about this many blows to die. Author each unit's level-1 maxHP to
    /// land there against its level-1 opponent:
    ///
    ///     maxHP  such that  EffectiveHP / ATK_opponent  ~= 8
    ///                       (EffectiveHP = maxHP x (1 + DEF/100))
    ///
    /// WHAT IT DOES NOT MEAN. It is NOT "nobody may ever die in under eight blows".
    /// The count is SUPPOSED to drift away from 8 as the campaign runs, and that
    /// drift is the game's difficulty signal:
    ///
    ///     enemies grow EVERY stage; the player upgrades only every ~5 stages,
    ///     so inside a cycle the hero falls progressively behind -
    ///         stage 1  ~8 blows to kill the hero
    ///         stage 3  ~7
    ///         stage 4  ~5     <- "you are under-levelled, go upgrade"
    ///         stage 5  upgrade restores ATK *and* HP, back to ~8
    ///
    /// A hard floor of eight would ERASE that signal: an under-levelled hero would
    /// still survive eight blows and the player would never feel behind. It would
    /// also quietly protect whoever is losing, which is the whole class of behaviour
    /// this system was stripped of. So there is no such floor - see
    /// CharacterStats.SafetyBlowFloor for the loose guard that remains.
    ///
    /// SIZING THE TWO GROWTH RATES. Enemy growth per stage `g` and the hero's upgrade
    /// factor `U` every `N` stages should satisfy
    ///
    ///     U ~= g^N
    ///
    /// so each cycle returns to roughly the same place. Whether the enemy ends a
    /// cycle slightly ahead or slightly behind the hero is a DESIGN choice, taken
    /// from the balance workbook - Arash will set the actual figures.
    /// </summary>
    public const int BaseStateBlowsToKill = 8;

    // ------------------------------------------------------------------
    //  REMOVED 2026-09-16 - every runtime bound on how fast a unit may die
    // ------------------------------------------------------------------
    // Gone: MaxHitsToKillAnyone (11), DefenseForMaxHits (80), HitsToKill(defence),
    // the maxHP/11 damage FLOOR, and finally the hard maxHP/8 damage CEILING that
    // guaranteed eight blows.
    //
    // Removed in two steps, both at Arash's direction:
    //
    //  1. The UPPER bound went first. The maxHP/11 floor lifted every weak blow to
    //     the same value, so a feeble attacker and a strong one landed IDENTICAL
    //     damage - ATK stopped affecting who wins, and CP (ATK x AtkSpd x EffectiveHP)
    //     could only predict through attack speed. Scaling hits-to-die by defence
    //     also double-counted defence, which already reduces every blow in
    //     CombatMath.DamagePerHit.
    //
    //  2. The eight-blow FLOOR went next, once the model was stated fully: eight is
    //     where the BASE state should sit, not a law for the whole campaign. Enemies
    //     grow every stage while the player upgrades every ~5, so a hero is MEANT to
    //     start dying in 7, then 5 blows as a cycle runs - that is the signal to go
    //     and upgrade. A hard floor would have hidden it, and would have protected
    //     whoever was losing.
    //
    // What remains is CharacterStats.SafetyBlowFloor, a deliberately loose guard that
    // exists only so a freak mismatch cannot read as a one-shot. Do not tighten it
    // back towards eight, and do not reintroduce an upper bound.

    // ------------------------------------------------------------------
    //  REMOVED 2026-09-16 - the TUTORIAL PRESENTATION block (levels 1-3)
    // ------------------------------------------------------------------
    // IsTutorialPresentation, TutorialHitsToKillEnemy and TutorialHeroDamagePerHit
    // are gone, together with CPBattleController.ScriptTutorialBlow and
    // IsTutorialExchange, which read them.
    //
    // They existed because levels 1-3 were guaranteed wins at CP ratios where real
    // combat looked wrong - a CP 100 hero deleting a CP 35 enemy in one blow - so
    // the exchange was scripted for appearance. The ratios themselves were the
    // problem, and they came from heroes vastly outnumbering enemies (12 heroes
    // against 5 at level 5) plus the CP normalisation that flattened both sides.
    //
    // The answer now is to fix the STATS and the counts, not to stage the fight.
    // Every stage runs the same real combat, and the 8-11 blow band in
    // CharacterStats.HitsToKillMe governs pacing everywhere.
    //
    // Earlier removals, kept as history: "TutorialEnemySpeedScale = 0.6f" (a 40%
    // enemy slow-down, dropped 2026-09-12 for one uniform authored speed) and
    // "SoloRushHitsToKill = 2" (a two-hit kill for a lone outnumbered hero, dropped
    // 2026-09-11). Do not reintroduce a per-level speed or lethality multiplier.

    public static int ResolveLevel(GameObject owner, LevelConfig config)
    {
        string name = owner.scene.name;
        int marker = name.LastIndexOf("_Stage_", StringComparison.Ordinal);
        if (marker >= 0 && int.TryParse(name.Substring(marker + 7), out int stage)) return stage;
        return config ? config.levelNumber : LevelManager.CurrentStage;
    }
}
