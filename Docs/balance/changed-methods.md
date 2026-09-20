# Script changes in this balance task

| Script | Changed/added methods or members | Purpose |
|---|---|---|
| `LevelBattleRules.cs` | `Deployments`, `EnemyWaves` tables | Match-by-match hero counts through stage 10 and enemy count plateaus |
| `UpgradeCostSO.cs` | `GetHeroXpCostForLevel`; XP cost fields | Hero XP cost grows 1, 2, 3… |
| `CurrencyManager.cs` | `TrySpendUpgradeResources` | Check and debit coins and XP together before notifying UI |
| `PlayerProgressionService.cs` | `GetUpgradeHeroXpCost`, `CanUpgrade`, `TryUpgrade` | Actual manual upgrade affordability and spending |
| `UnitsPanelController.cs` | `WireUpgradeButton`, `BuildUnlockedStats`, `HandleCurrencyChanged`, `IsUpgradeable` | XP-aware button, cost text and upgrade indicators |
| `UnitDetailView.cs` | `SetHeroXpCost` | Display required/owned Hero XP |
| `StageRewardCalculator.cs` | `GetRewardForStageAndHpCase`, `EarlyHeroXp` | Gradual early coins and scarce XP, existing gem calculation retained |
| `GameStartManager.cs` | `Awake`, `OnResetButtonClicked` | Preserve campaign save at boot; explicit reset still available |
| `DirectPlayBootstrap.cs` | Comments only | Remove obsolete claim that every boot wipes saves |
| `EarlyCampaignAuthoring.cs` (new, Editor only) | `Apply`, `Curve`, `FindEntry`, `ScaleAttackAndHp`, `Power`; asset accessors | Reproducible candidate balance authoring, not runtime CP adjustment |
| `EarlyCampaignVerification.cs` (new, Editor only) | `Start`, `Changed`, `Tick`, `Finish`, `WriteRows`, `SetProperty`, `Limit`, `Run` | Real-scene scenario matrix and CSV measurements with save restoration |
| `EarlyCampaignEconomyVerification.cs` (new, Editor only) | `Run`, `Require` | Actual progression/wallet milestone and failure checks |

Assets: stage 5–10 spawn configurations, enemy progression, Orc/Skeleton base stats, upgrade costs, new-hero definitions and independent base-stat assets. Stage 1–4 combat assets and the four starting heroes' base-stat assets are unchanged.
