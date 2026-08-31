using System.Collections.Generic;
using UnityEngine;

/// <summary>One card as offered to the player.</summary>
public struct BuffOffer
{
    public SkillData skill;
    public int unitId;           // which hero it buffs; meaningless when isGlobal
    public bool isGlobal;
    public int currentStars;     // stars on THIS (hero, stat) card - drives the value
    public int heroStars;        // stars the HERO holds across every stat - drives the display
    public float increment;      // total increment if taken (0.5 = +50%)

    public bool IsValid => skill != null;
}

/// <summary>
/// Chooses which cards to put on a level-up screen.
///
/// Heroes are keyed by UNIT ID, not by FighterType. Every UnitDefinitionSO in this
/// project carries classType = Warrior, so FighterType cannot tell two heroes
/// apart - unitId is the only real identity, and it is what PlayerStatsApplier
/// already uses to look a unit up in the database.
///
/// The screen always has 3 slots but the roster is 1-4 heroes, so the draw has to
/// satisfy three things at once:
///
///   ROTATION        every hero gets shown; one that sat out comes back.
///   SPECIALIZATION  a hero the player has been buffing shows up more often than
///                   one that has never been buffed.
///   VARIETY         no two cards on one screen buff the same stat.
///
/// Rotation and specialization pull in OPPOSITE directions, so they are given
/// different strengths: rotation is a HARD floor (forceAfterRounds guarantees a
/// neglected hero returns), specialization is a SOFT nudge (it only tilts the
/// weights). No hero is ever starved, but the invested one trends up.
///
/// Plain C# - no MonoBehaviour, no Unity lifecycle - so the logic can be reasoned
/// about and stepped through on its own.
/// </summary>
public class BuffDraw
{
    private readonly RogueliteConfigSO cfg;

    private readonly List<int> roster = new List<int>();
    private readonly List<SkillData> heroPool = new List<SkillData>();
    private readonly List<SkillData> globalPool = new List<SkillData>();

    private readonly Dictionary<int, int> roundsSinceShown = new Dictionary<int, int>();
    private readonly Dictionary<(int, SkillData), int> heroStars = new Dictionary<(int, SkillData), int>();
    private readonly Dictionary<SkillData, int> globalStars = new Dictionary<SkillData, int>();
    private readonly Dictionary<SkillData, int> totalPicks = new Dictionary<SkillData, int>();

    // Stars a hero holds across ALL buffs - drives the investment half of the
    // draw weight, and the star row under that hero in the HUD.
    private readonly Dictionary<int, int> heroTotalStars = new Dictionary<int, int>();

    private readonly HashSet<int> lastOffered = new HashSet<int>();

    private bool hasLastPicked;
    private int lastPickedUnit;

    // Reused across draws. A level-up happens a handful of times per stage, so
    // this is for clarity rather than for performance.
    private readonly List<int> heroScratch = new List<int>();
    private readonly List<SkillData> skillScratch = new List<SkillData>();
    private readonly List<int> pickPool = new List<int>();

    public BuffDraw(RogueliteConfigSO config)
    {
        cfg = config;
    }

    public IReadOnlyList<int> Roster => roster;

    /// <summary>
    /// Called once, at battle start, with the unit ids actually on the field and
    /// the authored buff pool. The pool is split by target mode here so the draw
    /// never has to test it again.
    /// </summary>
    public void Configure(IEnumerable<int> unitIds, IEnumerable<SkillData> pool)
    {
        Reset();

        if (unitIds != null)
        {
            foreach (var id in unitIds)
            {
                if (roster.Contains(id)) continue;
                roster.Add(id);
                roundsSinceShown[id] = 0;
            }
        }

        if (pool != null)
        {
            foreach (var s in pool)
            {
                if (s == null) continue;

                if (s.targetMode == SkillTargetMode.AllUnits) globalPool.Add(s);
                else heroPool.Add(s);
            }
        }
    }

    /// <summary>
    /// Replaces the roster WITHOUT touching progress.
    ///
    /// Called before every draw, because a hero whose last unit has died must stop
    /// being offered - and may come back later if the player revives it. Configure()
    /// cannot be used for that: it calls Reset() and would wipe every star the player
    /// has earned this stage.
    /// </summary>
    public void UpdateRoster(IEnumerable<int> unitIds)
    {
        roster.Clear();
        if (unitIds == null) return;

        foreach (var id in unitIds)
        {
            if (roster.Contains(id)) continue;

            roster.Add(id);

            // A hero that was never in the roster starts un-neglected; one that is
            // returning keeps the rotation age it had.
            if (!roundsSinceShown.ContainsKey(id)) roundsSinceShown[id] = 0;
        }
    }

    public void Reset()
    {
        roster.Clear();
        heroPool.Clear();
        globalPool.Clear();
        roundsSinceShown.Clear();
        heroStars.Clear();
        globalStars.Clear();
        totalPicks.Clear();
        heroTotalStars.Clear();
        lastOffered.Clear();
        hasLastPicked = false;
    }

    public int StarsFor(int unitId, SkillData skill)
    {
        if (skill == null) return 0;
        return heroStars.TryGetValue((unitId, skill), out int n) ? n : 0;
    }

    public int StarsForGlobal(SkillData skill)
    {
        if (skill == null) return 0;
        return globalStars.TryGetValue(skill, out int n) ? n : 0;
    }

    /// <summary>Total times this buff has been picked, across every hero. Drives the HUD icon rows.</summary>
    public int TotalPicks(SkillData skill)
    {
        if (skill == null) return 0;
        return totalPicks.TryGetValue(skill, out int n) ? n : 0;
    }

    /// <summary>Stars this hero holds across every buff. Public so the HUD can draw its star row.</summary>
    public int TotalStars(int unitId)
    {
        return heroTotalStars.TryGetValue(unitId, out int n) ? n : 0;
    }

    /// <summary>
    /// True when nothing can be offered any more - every buff on every hero is
    /// maxed and no global card is left. Checked WITHOUT running a draw, since a
    /// draw mutates the rotation state.
    /// </summary>
    public bool IsExhausted
    {
        get
        {
            for (int i = 0; i < roster.Count; i++)
                if (HasAnySkillLeft(roster[i])) return false;

            for (int i = 0; i < globalPool.Count; i++)
                if (StarsForGlobal(globalPool[i]) < globalPool[i].MaxStars) return false;

            return true;
        }
    }

    // ------------------------------------------------------------------ draw

    /// <summary>
    /// Builds one level-up screen. Returns fewer than <paramref name="slots"/>
    /// offers only when the pool is genuinely exhausted.
    /// </summary>
    public List<BuffOffer> Draw(int slots)
    {
        var offers = new List<BuffOffer>(slots);
        var usedStats = new HashSet<SkillEffectType>();

        lastOffered.Clear();

        // --- Step 1: at most ONE global card, so there are always >= 2 hero choices.
        if (globalPool.Count > 0 && cfg != null && Random.value < cfg.globalCardChance)
        {
            var g = PickGlobal(usedStats);
            if (g.IsValid)
            {
                offers.Add(g);
                usedStats.Add(g.skill.effectType);
            }
        }

        // --- Step 2: which heroes appear.
        int statSlots = slots - offers.Count;
        PickHeroes(statSlots, heroScratch);

        // --- Step 3: one stat per hero slot, never repeating a stat on this screen.
        for (int i = 0; i < heroScratch.Count; i++)
        {
            int unitId = heroScratch[i];
            var skill = PickSkillFor(unitId, usedStats);
            if (skill == null) continue;

            usedStats.Add(skill.effectType);
            lastOffered.Add(unitId);

            int stars = StarsFor(unitId, skill);
            offers.Add(new BuffOffer
            {
                skill = skill,
                unitId = unitId,
                isGlobal = false,
                currentStars = stars,
                heroStars = TotalStars(unitId),
                increment = skill.IncrementAtStars(stars)
            });
        }

        // --- Backfill: a hero may have had every stat maxed or already used on
        //     this screen. Rather than show a short screen, top up with a global
        //     card, then with any legal hero card at all.
        while (offers.Count < slots)
        {
            var g = PickGlobal(usedStats);
            if (!g.IsValid) break;

            offers.Add(g);
            usedStats.Add(g.skill.effectType);
        }

        while (offers.Count < slots)
        {
            var extra = PickAnyRemaining(usedStats);
            if (!extra.IsValid) break;

            offers.Add(extra);
            usedStats.Add(extra.skill.effectType);
            if (!extra.isGlobal) lastOffered.Add(extra.unitId);
        }

        return offers;
    }

    /// <summary>
    /// Book-keeping after the player has chosen. Advances the star count, sets the
    /// momentum target, and ages every hero that was NOT on the screen.
    /// </summary>
    public void Commit(BuffOffer chosen)
    {
        if (!chosen.IsValid) return;

        if (chosen.isGlobal)
        {
            globalStars[chosen.skill] = StarsForGlobal(chosen.skill) + 1;
        }
        else
        {
            var key = (chosen.unitId, chosen.skill);
            heroStars[key] = StarsFor(chosen.unitId, chosen.skill) + 1;
            heroTotalStars[chosen.unitId] = TotalStars(chosen.unitId) + 1;

            hasLastPicked = true;
            lastPickedUnit = chosen.unitId;
        }

        totalPicks[chosen.skill] = TotalPicks(chosen.skill) + 1;

        // Heroes shown this round reset; everyone else ages by one. This single
        // line is what makes the neglected hero come back next round.
        for (int i = 0; i < roster.Count; i++)
        {
            int id = roster[i];
            roundsSinceShown[id] = lastOffered.Contains(id) ? 0 : RoundsSinceShown(id) + 1;
        }
    }

    // --------------------------------------------------------------- internals

    private int RoundsSinceShown(int unitId)
    {
        return roundsSinceShown.TryGetValue(unitId, out int n) ? n : 0;
    }

    /// <summary>
    /// Two independent pulls, multiplied together:
    ///
    ///   PITY        rises the longer a hero has gone unshown, so a neglected hero
    ///               comes back - the fairness half.
    ///   INVESTMENT  rises with the stars a hero already holds, so a hero the
    ///               player has been buffing keeps appearing more often than one
    ///               that has never been buffed - the build half.
    ///
    /// momentumBonus then adds a small extra nudge for the hero picked on the
    /// previous screen, so an immediate follow-up feels responsive.
    ///
    /// Worked example, 4 heroes, round 2 after A was picked in round 1
    /// (pityStep 1, investmentStep 0.5, momentumBonus 1.5):
    ///
    ///   A  shown, 1 star, last picked -> (1 + 0) * (1 + 0.5) * 1.5 = 2.25
    ///   D  never shown, 0 stars       -> (1 + 1) * (1 + 0)         = 2.00
    ///   B  shown, 0 stars             -> (1 + 0) * (1 + 0)         = 1.00
    ///   C  shown, 0 stars             -> (1 + 0) * (1 + 0)         = 1.00
    ///
    /// A and D are the likely two, and one of B/C fills the third slot.
    /// </summary>
    private float Weight(int unitId)
    {
        float w = 1f;

        if (cfg != null)
        {
            w = (1f + cfg.pityStep * RoundsSinceShown(unitId))
              * (1f + cfg.investmentStep * TotalStars(unitId));

            if (hasLastPicked && unitId == lastPickedUnit) w *= cfg.momentumBonus;
        }

        return Mathf.Max(0.0001f, w);
    }

    /// <summary>
    /// Fills <paramref name="result"/> with the heroes to feature: forced ones
    /// first, then weighted without replacement. If more slots are wanted than
    /// there are heroes, heroes repeat - Step 3 then gives them a different stat.
    /// </summary>
    private void PickHeroes(int count, List<int> result)
    {
        result.Clear();
        if (count <= 0 || roster.Count == 0) return;

        // Only heroes that still have something left to offer.
        var pool = pickPool;
        pool.Clear();
        for (int i = 0; i < roster.Count; i++)
            if (HasAnySkillLeft(roster[i])) pool.Add(roster[i]);

        if (pool.Count == 0) return;

        // Hard fairness floor: anyone unseen for forceAfterRounds goes in first,
        // most-neglected first.
        if (cfg != null && cfg.forceAfterRounds > 0)
        {
            pool.Sort((a, b) => RoundsSinceShown(b).CompareTo(RoundsSinceShown(a)));

            for (int i = pool.Count - 1; i >= 0 && result.Count < count; i--)
            {
                int id = pool[i];
                if (RoundsSinceShown(id) < cfg.forceAfterRounds) continue;

                result.Add(id);
                pool.RemoveAt(i);
            }
        }

        // Weighted, without replacement - distinct heroes while any remain.
        while (result.Count < count && pool.Count > 0)
        {
            int idx = WeightedIndex(pool);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        // Fewer heroes than slots (roster of 1 or 2): hand the leftovers back out
        // by weight, allowing repeats. The stat filter keeps the cards different.
        if (result.Count < count)
        {
            var refill = new List<int>();
            for (int i = 0; i < roster.Count; i++)
                if (HasAnySkillLeft(roster[i])) refill.Add(roster[i]);

            while (result.Count < count && refill.Count > 0)
                result.Add(refill[WeightedIndex(refill)]);
        }
    }

    private int WeightedIndex(List<int> candidates)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++) total += Weight(candidates[i]);

        float roll = Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= Weight(candidates[i]);
            if (roll <= 0f) return i;
        }

        return candidates.Count - 1;
    }

    private bool HasAnySkillLeft(int unitId)
    {
        for (int i = 0; i < heroPool.Count; i++)
        {
            var s = heroPool[i];
            if (s.AppliesToUnit(unitId) && StarsFor(unitId, s) < s.MaxStars) return true;
        }
        return false;
    }

    /// <summary>
    /// A stat this hero can still take, excluding stats already on this screen.
    ///
    /// The pick is WEIGHTED by how many stars this exact (hero, stat) card already
    /// holds, so a card the player has been stacking keeps coming back to be stacked
    /// further, instead of every stat being equally likely every time.
    /// </summary>
    private SkillData PickSkillFor(int unitId, HashSet<SkillEffectType> usedStats)
    {
        skillScratch.Clear();

        for (int i = 0; i < heroPool.Count; i++)
        {
            var s = heroPool[i];
            if (!s.AppliesToUnit(unitId)) continue;
            if (StarsFor(unitId, s) >= s.MaxStars) continue;
            if (usedStats.Contains(s.effectType)) continue;

            skillScratch.Add(s);
        }

        if (skillScratch.Count == 0) return null;
        if (skillScratch.Count == 1 || cfg == null) return skillScratch[0];

        float total = 0f;
        for (int i = 0; i < skillScratch.Count; i++) total += CardWeight(unitId, skillScratch[i]);

        float roll = Random.value * total;
        for (int i = 0; i < skillScratch.Count; i++)
        {
            roll -= CardWeight(unitId, skillScratch[i]);
            if (roll <= 0f) return skillScratch[i];
        }

        return skillScratch[skillScratch.Count - 1];
    }

    private float CardWeight(int unitId, SkillData skill)
    {
        float w = 1f + cfg.cardInvestmentStep * StarsFor(unitId, skill);
        return Mathf.Max(0.0001f, w);
    }

    private BuffOffer PickGlobal(HashSet<SkillEffectType> usedStats)
    {
        skillScratch.Clear();

        for (int i = 0; i < globalPool.Count; i++)
        {
            var s = globalPool[i];
            if (StarsForGlobal(s) >= s.MaxStars) continue;
            if (usedStats.Contains(s.effectType)) continue;

            skillScratch.Add(s);
        }

        if (skillScratch.Count == 0) return default;

        var chosen = skillScratch[Random.Range(0, skillScratch.Count)];
        int stars = StarsForGlobal(chosen);

        return new BuffOffer
        {
            skill = chosen,
            isGlobal = true,
            currentStars = stars,
            increment = chosen.IncrementAtStars(stars)
        };
    }

    /// <summary>Last-resort backfill: any hero/stat pair still legal on this screen.</summary>
    private BuffOffer PickAnyRemaining(HashSet<SkillEffectType> usedStats)
    {
        for (int h = 0; h < roster.Count; h++)
        {
            int unitId = roster[h];
            var skill = PickSkillFor(unitId, usedStats);
            if (skill == null) continue;

            int stars = StarsFor(unitId, skill);
            return new BuffOffer
            {
                skill = skill,
                unitId = unitId,
                isGlobal = false,
                currentStars = stars,
                heroStars = TotalStars(unitId),
                increment = skill.IncrementAtStars(stars)
            };
        }

        return default;
    }
}
