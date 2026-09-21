using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EarlyCampaignEconomyVerification
{
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
        bool hadSave = PlayerPrefs.HasKey("GAME_SAVE_V1");
        string saved = PlayerPrefs.GetString("GAME_SAVE_V1", "");
        var cache = typeof(SaveSystem).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        object oldCache = cache.GetValue(null);
        var go = new GameObject("Temporary economy verification"); go.SetActive(false);
        try
        {
            cache.SetValue(null, null);
            var wallet = go.AddComponent<CurrencyManager>();
            var db = EarlyCampaignAuthoring.Database;
            int[] ids = db.Units.Where(d => d.startsDeployed).Select(d => d.unitId).ToArray();
            var model = new PlayerUnitsModel(); model.InitializeFromDatabase(db, ids); model.SeedInitialDeployed(ids);
            var costs = AssetDatabase.LoadAssetAtPath<UpgradeCostSO>("Assets/Scripts/UI/UI-SOs/UgradeCost-SOs/UpgradeCostSO.asset");
            var service = new PlayerProgressionService(model, wallet, costs, db, EarlyCampaignAuthoring.HeroGrowth);
            var cfg = new StageRewardConfig { baseRewardStageValues = new WinPanel.RewardValues() };
            for (int stage = 1; stage <= 5; stage++)
            {
                var reward = StageRewardCalculator.GetRewardForStageAndHpCase(stage, 1, cfg);
                wallet.AddCoins(reward.coins); wallet.AddHeroXP(reward.heroXP);
            }
            // 5 XP, not 4: stage 1 pays 1 Hero XP as of 2026-09-21, so one is spare here.
            // Coins (200 banked vs 160 needed) remain what gates the four first upgrades.
            Require(wallet.Coins == 200 && wallet.HeroXP == 5, "Minimum rewards before stage 6");
            foreach (int id in ids) Require(service.TryUpgrade(id) && model.GetLevel(id) == 2, "First upgrade " + id);
            Require(wallet.Coins == 40 && wallet.HeroXP == 1, "Four upgrades debit both currencies");
            int coinsBefore = wallet.Coins;
            Require(!service.TryUpgrade(ids[0]) && wallet.Coins == coinsBefore, "Insufficient resources do not debit coins");
            for (int stage = 6; stage <= 9; stage++)
            {
                var reward = StageRewardCalculator.GetRewardForStageAndHpCase(stage, 1, cfg);
                wallet.AddCoins(reward.coins); wallet.AddHeroXP(reward.heroXP);
            }
            foreach (int id in ids) Require(service.TryUpgrade(id) && model.GetLevel(id) == 3, "Second upgrade " + id);
            wallet.SetCoins(1000); wallet.SetHeroXP(0);
            Require(!service.TryUpgrade(ids[0]) && wallet.Coins == 1000 && model.GetLevel(ids[0]) == 3, "XP shortage is atomic");
            wallet.SetCoins(0); wallet.SetHeroXP(20);
            Require(!service.TryUpgrade(ids[0]) && wallet.HeroXP == 20, "Coin shortage is atomic");
            Require(costs.GetHeroXpCostForLevel(1) == 1 && costs.GetHeroXpCostForLevel(2) == 2 && costs.GetHeroXpCostForLevel(3) == 3, "XP progression");
            model.GetState(ids[0]).level = costs.levelCap;
            Require(!service.CanUpgrade(ids[0], out var reason) && reason == "AtCap", "Upgrade cap");
            Debug.Log("[Economy verification] PASS: both four-hero milestones, atomic resource failures, XP cost progression and cap.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (hadSave) PlayerPrefs.SetString("GAME_SAVE_V1", saved); else PlayerPrefs.DeleteKey("GAME_SAVE_V1");
            PlayerPrefs.Save(); cache.SetValue(null, oldCache);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
