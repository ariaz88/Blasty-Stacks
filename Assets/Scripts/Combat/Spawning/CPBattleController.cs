using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stages 1-5: reference CP calibration and hit-only tutorial assistance.
/// Never ticks HP, kills units remotely, stops waves, or destroys a gate.</summary>
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
    readonly HashSet<CharacterStats> heroes = new();
    readonly HashSet<CharacterStats> enemies = new();
    readonly Dictionary<CharacterStats, int> receivedHits = new();
    public int HeroDeaths { get; private set; }
    public int EnemyDeaths { get; private set; }
    public int FewestHitsBeforeDeath { get; private set; } = int.MaxValue;

    public static void CalibrateHero(PlayerManager hero, int level)
    {
        if (!hero || !LevelBattleRules.AppliesTo(level)) return;
        ScaleToCP(hero.unitStats, level == 1 ? 100 : 125);
        ResetHealth(hero.GetComponent<PlayerStats>(), hero.unitStats);
        if (!hero.GetComponent<MeleeContactRecovery>()) hero.gameObject.AddComponent<MeleeContactRecovery>();
    }
    static void ScaleToCP(UnitStatsRuntime stats, double target)
    {
        double raw = CPCalculator.UnitPower(stats);
        if (!(raw > 0)) throw new ArgumentException("Combat stats must have positive CP.");
        // Equal ATK/HP scaling keeps each archetype's offensive/defensive shape.
        float factor = (float)Math.Sqrt(target / raw);
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
            foreach (var hero in waves.ReleasedHeroes)
            {
                CalibrateHero(hero, Level);
                heroes.Add(hero.GetComponent<PlayerStats>());
                PlayerCP += CPCalculator.UnitPower(hero.unitStats);
            }
            HeroCount = heroes.Count;
            EnemyCP = LevelBattleRules.ReferenceEnemyCP(Level);
            EnemyScale = EnemyCP / source.PlannedEnemyCP();
            if (double.IsNaN(EnemyScale) || double.IsInfinity(EnemyScale) || EnemyScale <= 0)
                throw new ArgumentException("Enemy roster must have positive finite CP.");
            PlayerShouldWin = waves.MatchesCleared >= LevelBattleRules.FirstWinningMatch(Level);
            if ((PlayerCP > EnemyCP) != PlayerShouldWin)
                throw new ArgumentException("Reference CP disagrees with the deployment outcome.");
            BattleResult = "Expected " + (PlayerShouldWin ? "Win" : "Loss");
            Instance = this;
            IsPrepared = true;
            Debug.Log($"[CP Battle] Stage {Level}, heroes {HeroCount}, enemies {Level}, CP {PlayerCP:F1}/{EnemyCP:F1}. {BattleResult}; actual attacks and gate destruction required.", this);
            return true;
        }
        catch (ArgumentException e) { Debug.LogError("[CP Battle] " + e.Message, this); return false; }
    }
    public void RegisterEnemy(EnemyManager enemy)
    {
        double target = CPCalculator.UnitPower(enemy.unitStats) * EnemyScale;
        ScaleToCP(enemy.unitStats, target);
        ResetHealth(enemy.GetComponent<EnemyStats>(), enemy.unitStats);
        enemy.cp = CPCalculator.DisplayCP(CPCalculator.UnitPower(enemy.unitStats));
        enemies.Add(enemy.GetComponent<EnemyStats>());
        if (!enemy.GetComponent<MeleeContactRecovery>()) enemy.gameObject.AddComponent<MeleeContactRecovery>();
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

    /// <summary>Called only when a weapon actually hits this particular unit.
    /// The intended winner's last survivor has a 1%-HP safety net, not a remote kill.
    /// CP is the reference score; these encounter-assist multipliers are separate.</summary>
    public static float AdjustIncomingDamage(CharacterStats target, float damage)
    {
        damage = Mathf.Max(0, damage);
        var battle = Instance;
        if (!battle || !battle.IsPrepared || !target || target.gameObject.scene != battle.gameObject.scene)
            return damage;
        bool isHero = battle.heroes.Contains(target);
        if (!isHero && !battle.enemies.Contains(target)) return damage;
        if (GameplayPause.IsPaused || !LevelGameManager.IsBattleRunning) return 0;
        if (damage <= 0 || target.currentHP <= 0) return 0;
        bool winningSide = isHero == battle.PlayerShouldWin;
        // No blanket winning-team armor or damage bonus: front-line winners can
        // now die through the same physical attacks as the weaker side.
        bool slowLoss = battle.Level == 5 && battle.HeroCount == 4;
        if (slowLoss && isHero) damage *= 0.6f;
        damage = Mathf.Min(damage, target.maxHealth / 4f);
        int living = 0;
        foreach (var unit in isHero ? battle.heroes : battle.enemies)
            if (unit && unit.currentHP > 0) living++;
        if (winningSide && living <= 1)
            damage = Mathf.Min(damage, Mathf.Max(0, target.currentHP - target.maxHealth * 0.01f));
        if (damage <= 0) return 0;
        int hits = battle.receivedHits.TryGetValue(target, out int previous) ? previous + 1 : 1;
        battle.receivedHits[target] = hits;
        // Also protect against float rounding or pre-existing non-hit HP loss.
        if (hits < 4) damage = Mathf.Min(damage, Mathf.Max(0, target.currentHP - target.maxHealth * 0.001f));
        if (damage >= target.currentHP)
        {
            if (isHero) battle.HeroDeaths++; else battle.EnemyDeaths++;
            battle.FewestHitsBeforeDeath = Math.Min(battle.FewestHitsBeforeDeath, hits);
        }
        return damage;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
