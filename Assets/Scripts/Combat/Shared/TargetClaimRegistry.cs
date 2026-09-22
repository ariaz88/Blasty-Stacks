using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Is anybody already dealing with that enemy?"
///
/// THE RULE THIS EXISTS FOR (Arash, 2026-09-17): a hero picking a target must
/// prefer an enemy NOBODY is handling over an enemy that is merely closer. The
/// symptom it fixes: a hero spawns, walks to the nearest enemy, a second hero
/// spawns on the same side and walks to that SAME enemy - while the other enemy
/// strolls unopposed into the player base. Two heroes on one target is not worth
/// an undefended base.
///
/// A CLAIM IS NOT A SEPARATE OBJECT. A hero claims an enemy simply by holding it
/// in PlayerManager.currentTarget, which is set the moment it targets, stays set
/// while it walks over, and stays set while it fights. So "already targeted",
/// "already walking towards" and "already fighting" are one condition, and no
/// claim can ever leak or go stale behind a hero's back.
///
/// WHY A REGISTRY AND NOT FindObjectsOfType: counting claimants is asked once per
/// candidate enemy per acquisition. FindObjectsOfType allocates and walks the
/// whole scene every time; a HashSet the heroes file themselves into does not.
/// PlayerManager.OnEnable/OnDisable own both ends, so a destroyed or pooled hero
/// cannot linger here.
///
/// Modelled on AttackSlotRegistry: a static, no per-frame interaction between
/// units, one decision taken at the moment of choosing.
/// </summary>
public static class TargetClaimRegistry
{
    private static readonly HashSet<PlayerManager> Heroes = new HashSet<PlayerManager>();

    /// <summary>
    /// Wipes the static set when Play mode starts. Required because "Enter Play
    /// Mode Options" can disable the domain reload, in which case this set
    /// survives from the previous run and the first battle would count claims
    /// held by heroes that no longer exist. Same reasoning as HeroRoster.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Heroes.Clear();

    public static void Register(PlayerManager hero)
    {
        if (hero != null) Heroes.Add(hero);
    }

    public static void Unregister(PlayerManager hero)
    {
        if (hero != null) Heroes.Remove(hero);
    }

    /// <summary>Drops everything. Call between stages if a reload ever leaks.</summary>
    public static void ClearAll() => Heroes.Clear();

    /// <summary>
    /// How many OTHER heroes are currently handling this enemy.
    ///
    /// <paramref name="self"/> is excluded on purpose: a hero must never see its
    /// own claim as a reason to avoid its own target. Without that exclusion a
    /// hero re-evaluating its target would read "1 claimant" on the enemy it is
    /// already fighting and wander off to a free one - and the hero it handed the
    /// fight to would do the same thing back. That is the oscillation SESSIONS.md
    /// records happening five separate times in this codebase.
    /// </summary>
    public static int OtherClaimants(EnemyStats enemy, PlayerManager self)
    {
        if (enemy == null) return 0;

        int n = 0;
        foreach (var hero in Heroes)
        {
            if (hero == null || hero == self) continue;
            if (hero.currentTarget != enemy) continue;
            if (!IsHandling(hero)) continue;
            n++;
        }

        return n;
    }

    /// <summary>
    /// A hero only counts as handling its target once it is actually IN the
    /// field and still alive.
    ///
    /// isUnlocked: heroes spawn onto the castle gates LOCKED and a puzzle match
    /// is what throws them out. One still sitting on a gate is not defending
    /// anything, and counting its target would push a newly released hero away
    /// from an enemy nobody is really on. Same test HeroRoster.IsInBattle uses.
    ///
    /// Dead heroes keep currentTarget set while the dying animation plays (the
    /// GameObject lives on for ~0.5s), so without this check a corpse would keep
    /// an enemy reserved and the next hero would walk past it.
    /// </summary>
    private static bool IsHandling(PlayerManager hero)
    {
        if (!hero.isUnlocked) return false;
        if (hero.PlayerDeathState != null && hero.currentState == hero.PlayerDeathState) return false;
        return true;
    }

    /// <summary>
    /// "Of everyone handling this enemy, am I the closest to it?"
    ///
    /// THE RULE THIS EXISTS FOR (Arash, 2026-09-22): a hero may only walk away
    /// from its target if somebody CLOSER is left holding it. Re-balancing is
    /// meant to spread heroes over the battlefield, not to hand a fight to a hero
    /// that is further away than the one leaving - that leaves the enemy covered
    /// worse than before, which is a straight loss no amount of spread pays for.
    ///
    /// The reported case, from play: several heroes are on the only enemy on the
    /// field (early stages field ONE enemy in wave 1, so a whole deployment has
    /// nothing else to pick). A new wave spawns, its enemies have zero claimants,
    /// and every hero on the old target sees a strict improvement - including the
    /// one that was about to reach it. That hero turns around and walks the length
    /// of the field while the enemy it had cornered is left to a hero behind it.
    ///
    /// EXACTLY ONE keeper per enemy, so this can never freeze a whole group:
    /// nearest wins, and an exact distance tie is broken by instance id - the same
    /// deterministic tie-break <see cref="Beats"/> uses, and for the same reason.
    /// Everybody else on that target is free to leave.
    ///
    /// Returns TRUE when nobody else claims the enemy at all, which is the honest
    /// answer - a lone hero IS the nearest claimant. TryRebalance already returns
    /// earlier in that case, so the trivial answer is never the deciding one.
    /// </summary>
    public static bool IsNearestClaimant(EnemyStats enemy, PlayerManager self)
    {
        if (enemy == null || self == null) return false;

        Vector2 target = enemy.transform.position;
        float mine = ((Vector2)self.transform.position - target).sqrMagnitude;

        foreach (var hero in Heroes)
        {
            if (hero == null || hero == self) continue;
            if (hero.currentTarget != enemy) continue;
            if (!IsHandling(hero)) continue;

            float theirs = ((Vector2)hero.transform.position - target).sqrMagnitude;

            if (Mathf.Approximately(theirs, mine))
            {
                // Dead heat: the lower instance id keeps it. Without a tie-break
                // both heroes read "I am not further away" and BOTH stay, which
                // would defeat the re-balance entirely for a symmetric pair.
                if (hero.GetInstanceID() < self.GetInstanceID()) return false;
                continue;
            }

            if (theirs < mine) return false;
        }

        return true;
    }

    /// <summary>
    /// Ranks one candidate enemy against the best found so far, in this order:
    ///
    ///   1. FEWEST other claimants   - an unhandled enemy beats a closer handled one.
    ///   2. NEAREST                  - among equally-handled enemies, the close one.
    ///   3. Lowest instance id       - deterministic tie-break, never a coin flip.
    ///
    /// Step 1 IS the new rule and step 2 is the old behaviour it sits on top of:
    /// when every enemy is equally covered - including the common case where all
    /// of them are free - this collapses back to plain "nearest". When 12 heroes
    /// face 5 enemies, step 1 spreads them ~3/3/2/2/2 instead of stacking eight
    /// onto whoever happens to be closest.
    ///
    /// Step 3 exists because SESSIONS.md records the same class of bug five
    /// times: two units comparing equal values both conclude "I am not worse",
    /// and both take the same action. Distances are compared SQUARED, so any
    /// margin added here must be squared too.
    /// </summary>
    public static bool Beats(EnemyStats candidate, int candidateClaims, float candidateDistSq,
                             EnemyStats best, int bestClaims, float bestDistSq)
    {
        if (candidate == null) return false;
        if (best == null) return true;

        if (candidateClaims != bestClaims) return candidateClaims < bestClaims;
        if (!Mathf.Approximately(candidateDistSq, bestDistSq)) return candidateDistSq < bestDistSq;

        return candidate.GetInstanceID() < best.GetInstanceID();
    }

    /// <summary>
    /// Set false once targeting is signed off. Left ON deliberately: the rule
    /// failed its first playtest SILENTLY - two heroes walked at one enemy and
    /// the screenshot could not say whether the rule had not run, had run and
    /// found no free enemy, or had run before the second enemy existed. One line
    /// per pick answers that without another round of guessing from an image.
    /// Same reasoning as CPBattleController.LogEveryBlow.
    /// </summary>
    public static bool LogPicks = true;

    /// <summary>
    /// One line per target acquisition: who picked what, how many heroes were
    /// already on it, and how far away it was. A pick showing "others=1" when a
    /// free enemy exists on the field is the rule being broken; a pick showing
    /// "others=0" on the far enemy is the rule working.
    ///
    /// Acquisitions are rare - spawn and after a kill - so this is a handful of
    /// lines per battle, not per frame.
    /// </summary>
    public static void LogPick(PlayerManager hero, EnemyStats chosen, int otherClaims, float distSq, string phase)
    {
        if (!LogPicks || hero == null || chosen == null) return;

        Debug.Log($"[TARGET] '{hero.name}' -> '{chosen.name}' " +
                  $"(others already on it: {otherClaims}, distance {Mathf.Sqrt(distSq):F2}, {phase})", hero);
    }
}
