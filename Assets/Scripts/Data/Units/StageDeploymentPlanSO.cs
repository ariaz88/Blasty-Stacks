using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WHICH hero types each match of each level awards, and how many of each.
///
/// LevelBattleRules.Deployments already says HOW MANY heroes a match is worth
/// (stage 4 is 1,1,2,4). It says nothing about WHICH types, and until now the
/// types were drawn at random from the deployed roster. PHASE 2 needs them
/// authorable: "match 1 gives one of type A, match 3 gives one A and one B" is a
/// level-design decision, not a dice roll.
///
/// This asset holds that decision for every level and every match. It is
/// OPTIONAL and additive:
///   - A level with an entry here uses it verbatim.
///   - A level with no entry keeps the old behaviour exactly - counts from
///     LevelBattleRules, types drawn at random. Nothing breaks by leaving this
///     asset empty, and levels can be converted one at a time.
///
/// Counts here are authoritative when a level IS authored: a match that lists
/// {A x2, B x1} awards three heroes regardless of what LevelBattleRules says, so
/// the two never have to be kept in sync by hand. Validate() reports where they
/// disagree, as a design aid rather than an error.
/// </summary>
[CreateAssetMenu(fileName = "StageDeploymentPlan",
                 menuName = "Blasty/Stage Deployment Plan")]
public class StageDeploymentPlanSO : ScriptableObject
{
    /// <summary>One hero type and how many of it a single match awards.</summary>
    [Serializable]
    public class Entry
    {
        [Tooltip("UnitDefinitionSO.unitId of the hero type this match awards.")]
        public int unitId;

        [Tooltip("How many heroes of that type. The card deal shows ONE card per " +
                 "type with this number under it; the spawn puts this many on the " +
                 "gates.")]
        [Min(1)] public int count = 1;
    }

    /// <summary>What one match awards - any number of types, each with a count.</summary>
    [Serializable]
    public class MatchPlan
    {
        [Tooltip("Left empty = this match awards nothing.")]
        public List<Entry> entries = new();
    }

    /// <summary>The whole run of matches for one level, in order.</summary>
    [Serializable]
    public class LevelPlan
    {
        [Tooltip("The level this plan is for - the same number " +
                 "LevelBattleRules.ResolveLevel returns. 1-based.")]
        [Min(1)] public int level = 1;

        [Tooltip("One entry per match, in the order the player clears them. " +
                 "Element 0 is the FIRST match.")]
        public List<MatchPlan> matches = new();
    }

    [SerializeField] private List<LevelPlan> levels = new();

    /// <summary>True when this asset has anything authored for that level.</summary>
    public bool HasPlanFor(int level) => FindLevel(level) != null;

    /// <summary>How many matches that level is authored for. 0 when unauthored.</summary>
    public int MatchCount(int level)
    {
        var plan = FindLevel(level);
        return plan?.matches?.Count ?? 0;
    }

    /// <summary>
    /// What match number <paramref name="match"/> (1-BASED, to match
    /// LevelBattleRules.HeroesForMatch) awards. Null when that level or that
    /// match is not authored - callers then fall back to the random draw.
    /// </summary>
    public IReadOnlyList<Entry> EntriesFor(int level, int match)
    {
        var plan = FindLevel(level);
        if (plan?.matches == null) return null;

        int index = match - 1;
        if (index < 0 || index >= plan.matches.Count) return null;

        var entries = plan.matches[index]?.entries;
        return entries != null && entries.Count > 0 ? entries : null;
    }

    /// <summary>Total heroes that match awards, summing every entry's count.</summary>
    public int HeroCountFor(int level, int match)
    {
        var entries = EntriesFor(level, match);
        if (entries == null) return 0;

        int total = 0;
        foreach (var e in entries) total += Mathf.Max(1, e.count);
        return total;
    }

    private LevelPlan FindLevel(int level)
    {
        foreach (var p in levels)
            if (p != null && p.level == level) return p;

        return null;
    }

    /// <summary>
    /// Design aid: lists every authored match whose total disagrees with
    /// LevelBattleRules, and every level authored for a different number of
    /// matches. Returns an empty string when everything lines up.
    ///
    /// Deliberately NOT an error. The plan wins at runtime - this only points out
    /// where the two tables tell different stories, which is nearly always a typo.
    /// </summary>
    public string Validate()
    {
        var report = new System.Text.StringBuilder();

        foreach (var p in levels)
        {
            if (p == null) continue;

            int expectedMatches = LevelBattleRules.TotalPairs(p.level);
            if (expectedMatches > 0 && p.matches.Count != expectedMatches)
                report.AppendLine($"level {p.level}: authored {p.matches.Count} matches, " +
                                  $"LevelBattleRules says {expectedMatches}");

            for (int m = 1; m <= p.matches.Count; m++)
            {
                int planned = HeroCountFor(p.level, m);
                int expected = LevelBattleRules.HeroesForMatch(p.level, m);

                if (expected > 0 && planned != expected)
                    report.AppendLine($"level {p.level} match {m}: plan awards {planned} " +
                                      $"hero(es), LevelBattleRules says {expected}");
            }
        }

        return report.ToString();
    }
}
