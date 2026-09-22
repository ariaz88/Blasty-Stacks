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
            // 5 XP, not 4: stage 1 pays 1 Hero XP, so one is spare here. The four first
            // upgrades now cost 50 coins each, so COINS are exactly consumed (200 of 200).
            Require(wallet.Coins == 200 && wallet.HeroXP == 5, "Minimum rewards before stage 6");
            foreach (int id in ids) Require(service.TryUpgrade(id) && model.GetLevel(id) == 2, "First upgrade " + id);
            Require(wallet.Coins == 0 && wallet.HeroXP == 1, "Four upgrades debit both currencies");
            int coinsBefore = wallet.Coins;
            Require(!service.TryUpgrade(ids[0]) && wallet.Coins == coinsBefore, "Insufficient resources do not debit coins");
            for (int stage = 6; stage <= 9; stage++)
            {
                var reward = StageRewardCalculator.GetRewardForStageAndHpCase(stage, 1, cfg);
                wallet.AddCoins(reward.coins); wallet.AddHeroXP(reward.heroXP);
            }
            // Stages 6-9 pay 340 coins and 7 XP (1 at stage 6, then 2 each). Four second
            // upgrades cost 80 coins + 2 XP apiece, so 320 of 340 coins and all 8 XP.
            Require(wallet.Coins == 340 && wallet.HeroXP == 8, "Rewards banked by stage 9");
            foreach (int id in ids) Require(service.TryUpgrade(id) && model.GetLevel(id) == 3, "Second upgrade " + id);
            Require(wallet.Coins == 20 && wallet.HeroXP == 0, "Four second upgrades are affordable, with nothing to spare");
            wallet.SetCoins(1000); wallet.SetHeroXP(0);
            Require(!service.TryUpgrade(ids[0]) && wallet.Coins == 1000 && model.GetLevel(ids[0]) == 3, "XP shortage is atomic");
            wallet.SetCoins(0); wallet.SetHeroXP(20);
            Require(!service.TryUpgrade(ids[0]) && wallet.HeroXP == 20, "Coin shortage is atomic");
            // Authored tables: XP 1,2,3,4,4,5,5,6 then floor(L/2)+2; coins 50,80,200,340,...
            Require(costs.GetHeroXpCostForLevel(1) == 1 && costs.GetHeroXpCostForLevel(2) == 2
                 && costs.GetHeroXpCostForLevel(3) == 3 && costs.GetHeroXpCostForLevel(4) == 4
                 && costs.GetHeroXpCostForLevel(5) == 4 && costs.GetHeroXpCostForLevel(8) == 6
                 && costs.GetHeroXpCostForLevel(9) == 6 && costs.GetHeroXpCostForLevel(10) == 7, "XP progression");
            Require(costs.GetCostForLevel(1) == 50 && costs.GetCostForLevel(2) == 80
                 && costs.GetCostForLevel(3) == 200 && costs.GetCostForLevel(8) == 2900, "Authored coin table");
            // Past the table the tail decays instead of exploding: a flat ~1.7 would reach ~1e13.
            Require(costs.GetCostForLevel(9) == 4350 && costs.GetCostForLevel(49) < 500_000_000, "Coin tail stays finite");

            // Tunnelling guard: the coins a player banks by stage 6 must not fund a THIRD
            // upgrade on a single hero, which is what makes spreading across the deck the
            // affordable play. 270 coins at minimum HP, 324 clearing every stage at full HP.
            foreach (int banked in new[] { 270, 324 })
            {
                var solo = new PlayerUnitsModel(); solo.InitializeFromDatabase(db, ids); solo.SeedInitialDeployed(ids);
                var soloService = new PlayerProgressionService(solo, wallet, costs, db, EarlyCampaignAuthoring.HeroGrowth);
                wallet.SetCoins(banked); wallet.SetHeroXP(6);
                Require(soloService.TryUpgrade(ids[0]) && soloService.TryUpgrade(ids[0]) && solo.GetLevel(ids[0]) == 3,
                    "Two upgrades on one hero fit " + banked + " coins");
                Require(wallet.Coins == banked - 130 && wallet.HeroXP == 3, "Two upgrades cost 130 coins and 3 XP");
                int before = wallet.Coins;
                Require(!soloService.TryUpgrade(ids[0]) && solo.GetLevel(ids[0]) == 3 && wallet.Coins == before,
                    "A third upgrade on the same hero is blocked at " + banked + " coins");
            }

            model.GetState(ids[0]).level = costs.levelCap;
            Require(!service.CanUpgrade(ids[0], out var reason) && reason == "AtCap", "Upgrade cap");
            Debug.Log("[Economy verification] PASS: both four-hero milestones, the tunnelling guard, atomic resource failures, authored cost tables and cap.");
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
