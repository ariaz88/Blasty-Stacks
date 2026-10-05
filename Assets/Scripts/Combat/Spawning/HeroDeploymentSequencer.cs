using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Releases the heroes the player earned in the puzzle, ONE TYPE PER TIMER, all
/// timers running IN PARALLEL - a mana-style deployment. (Reworked 2026-09-27.)
///
/// When BATTLE is pressed it counts how many heroes of each type were earned
/// (which match they came from no longer matters) and gives every type its own
/// "track": total, remaining, and a load time from
/// PlayerWaveManager.ResolveDeployInterval (the hero's
/// UnitDefinitionSO.deployInterval, or the level's override). Every track starts
/// loading on the same frame. Each time a track's load fills, ONE hero of that
/// type is handed to PlayerWaveManager.DeployOne, the count drops by one and the
/// load starts again - until that type runs out.
///
/// So with Valkyrie x3 at 5.5s and Minotaur x2 at 7s: Valkyries at 5.5, 11, 16.5;
/// Minotaurs at 7, 14. The first hero of a type comes out after ONE full load,
/// never at 0s.
///
/// The level ending (win OR loss - LevelGameManager leaves Playing) stops every
/// track where it stands. Heroes that have not come out by then never will.
///
/// It owns no roster and spawns nothing itself: the counts come from
/// PlayerWaveManager.EarnedHeroes and the spawning is PlayerWaveManager's. The
/// Hero Stats panel reads <see cref="Tracks"/> every frame to draw the cards.
/// </summary>
[DisallowMultipleComponent]
public class HeroDeploymentSequencer : MonoBehaviour
{
    /// <summary>One hero type's deployment timer. Read-only outside this class.</summary>
    public class Track
    {
        public UnitDefinitionSO Def { get; internal set; }
        public int UnitId => Def ? Def.unitId : -1;

        /// <summary>How many of this type were earned - the "/total" on the card.</summary>
        public int Total { get; internal set; }

        /// <summary>How many have NOT come out yet - the left number on the card.</summary>
        public int Remaining { get; internal set; }

        /// <summary>Seconds one load takes.</summary>
        public float Interval { get; internal set; }

        /// <summary>Seconds into the current load.</summary>
        public float Elapsed { get; internal set; }

        /// <summary>0..1 fill of the current load. 0 once the type has run out.</summary>
        public float Progress => Remaining > 0 && Interval > 0f ? Mathf.Clamp01(Elapsed / Interval) : 0f;
    }

    [Tooltip("How long the sequencer may wait for PlayerWaveManager to finish awarding " +
             "every cleared match before it counts the heroes. Must comfortably " +
             "exceed PlayerWaveManager.nextWaveDelay (0.75s) times the number of " +
             "matches in a stage. It is a SAFETY NET, not a pacing value - in normal " +
             "play the wait ends in well under a second.")]
    [Min(0.5f)] [SerializeField] private float awardWaitTimeout = 5f;

    [Header("Refs (left empty = found in the scene at Awake)")]
    [SerializeField] private PlayerWaveManager waveManager;
    [SerializeField] private BattleStartController battleStart;

    private readonly List<Track> tracks = new();

    /// <summary>
    /// One track per earned hero type, in units-database order. Null until the
    /// tracks exist (BATTLE not pressed yet, or still waiting on the award
    /// pipeline); an EMPTY list means the battle started with no heroes.
    /// </summary>
    public IReadOnlyList<Track> Tracks => tracksReady ? tracks : null;

    /// <summary>True while the timers are counting.</summary>
    public bool Running { get; private set; }

    /// <summary>
    /// True while any earned hero has yet to reach the field: a track still has
    /// some left, a released hero is waiting for a free stage, or BATTLE was just
    /// pressed and the heroes have not even been counted yet. LevelGameManager's
    /// mutual-wipe check reads this so an empty field with heroes still loading
    /// is not called a defeat.
    /// </summary>
    public bool HeroesStillComing
    {
        get
        {
            if (stopped) return false;
            if (waitingForAwards) return true;
            if (waveManager && waveManager.HeroesWaitingForGate > 0) return true;

            foreach (var t in tracks)
                if (t.Remaining > 0) return true;

            return false;
        }
    }

    /// <summary>One hero of this track's type has just been handed to PlayerWaveManager.</summary>
    public event Action<Track> HeroReleased;

    /// <summary>Every track has run out. Not raised when the level end stops them.</summary>
    public event Action AllDeployed;

    private bool tracksReady;
    private bool waitingForAwards;
    private bool stopped;
    private Coroutine startRoutine;

    private void Awake()
    {
        if (!waveManager) waveManager = FindObjectOfType<PlayerWaveManager>(true);
        if (!battleStart) battleStart = FindObjectOfType<BattleStartController>(true);

        if (!waveManager)
            Debug.LogError("[HeroDeploymentSequencer] No PlayerWaveManager in the scene - " +
                           "no hero can ever be deployed.", this);
    }

    private void OnEnable()
    {
        if (battleStart) battleStart.OnBattleStarted += HandleBattleStarted;
        LevelGameManager.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        if (battleStart) battleStart.OnBattleStarted -= HandleBattleStarted;
        LevelGameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleBattleStarted()
    {
        if (stopped) return;

        if (startRoutine != null) StopCoroutine(startRoutine);
        startRoutine = StartCoroutine(BuildTracksWhenAwarded());
    }

    /// <summary>
    /// Win, loss or revive offer - anything that is not Playing ends deployment
    /// for good. The bars freeze where they are; nothing further is released.
    /// </summary>
    private void HandleGameStateChanged(LevelGameManager.GameState state)
    {
        if (state != LevelGameManager.GameState.Playing) StopDeployment();
    }

    /// <summary>Stops every track where it stands. Nothing further is released.</summary>
    public void StopDeployment()
    {
        if (startRoutine != null) StopCoroutine(startRoutine);
        startRoutine = null;

        stopped = true;
        Running = false;
        waitingForAwards = false;

        // A hero whose card already filled but who is still waiting for a free
        // stage must not walk out after the level has ended either.
        if (waveManager) waveManager.ClearGateQueue();
    }

    private IEnumerator BuildTracksWhenAwarded()
    {
        if (!waveManager) yield break;

        waitingForAwards = true;

        // !! WAIT FOR THE AWARD PIPELINE BEFORE COUNTING. (2026-09-16)
        //
        // SealForBattle stops new MATCHES being counted. It does NOT flush the
        // award pipeline - PlannedWaveLoop converts a cleared match into earned
        // heroes only after `nextWaveDelay` (0.75s). Clear the last match and
        // press BATTLE inside that window and EarnedHeroes is still missing that
        // match. Counting straight away once produced ZERO heroes for a whole
        // battle: the enemies walked to an undefended base and the stage was an
        // automatic loss (reproduced on stage 1, gate 500 -> 458).
        //
        // So wait until every cleared match has actually been awarded. The timeout
        // is a safety net: on expiry we take whatever exists rather than hanging.
        float waited = 0f;
        while (waveManager.MatchesReleased < waveManager.MatchesCleared
               && !waveManager.DeploymentFailed
               && waited < awardWaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (waveManager.MatchesReleased < waveManager.MatchesCleared)
            Debug.LogWarning($"[HeroDeployment] Started with {waveManager.MatchesReleased} of " +
                             $"{waveManager.MatchesCleared} matches awarded after {waited:F2}s" +
                             (waveManager.DeploymentFailed ? " (deployment had FAILED)." : "."), this);

        waitingForAwards = false;
        startRoutine = null;

        BuildTracks();

        // The first hero of every type appears on its stage as soon as the cards
        // are on screen, and waits there for its card's load (2026-10-05). The
        // rest follow one by one, each appearing once the previous one has landed.
        foreach (var t in tracks)
            waveManager.StageHeroes(t.Def, t.Total);

        // Every track starts at Elapsed 0 on this same frame - the parallel start.
        Running = tracks.Count > 0;
        if (!Running) AllDeployed?.Invoke();
    }

    /// <summary>Counts EarnedHeroes per type into one track each.</summary>
    private void BuildTracks()
    {
        tracks.Clear();

        foreach (var def in waveManager.EarnedHeroes)
        {
            if (!def) continue;

            Track track = null;
            foreach (var t in tracks)
                if (t.Def == def) { track = t; break; }

            if (track == null)
            {
                track = new Track { Def = def, Interval = waveManager.ResolveDeployInterval(def) };
                tracks.Add(track);
            }

            track.Total++;
            track.Remaining++;
        }

        // Database order, so the cards read the same way every run instead of
        // following whichever type happened to be earned first.
        var gsm = GameStartManager.Instance;
        var db = gsm ? gsm.unitsDatabase : null;
        if (db != null)
            tracks.Sort((a, b) => db.IndexOf(a.UnitId).CompareTo(db.IndexOf(b.UnitId)));

        tracksReady = true;

        foreach (var t in tracks)
            Debug.Log($"[HeroDeployment] {t.Def.displayName}: x{t.Total}, one every {t.Interval:0.##}s", this);
    }

    /// <summary>
    /// Advances every track by the frame's time. Update rather than one coroutine
    /// per type: all tracks tick from the SAME deltaTime in the same loop, so they
    /// cannot drift apart, and pausing (timeScale 0) freezes them together.
    /// </summary>
    private void Update()
    {
        if (!Running) return;

        float dt = Time.deltaTime;
        bool anyLeft = false;

        foreach (var t in tracks)
        {
            if (t.Remaining <= 0) continue;

            t.Elapsed += dt;

            if (t.Elapsed >= t.Interval)
            {
                // Carry the overshoot into the next load so the rhythm stays
                // exactly one hero per Interval, whatever the frame rate.
                t.Elapsed -= t.Interval;
                t.Remaining--;
                if (t.Remaining == 0) t.Elapsed = 0f;

                waveManager.DeployOne(t.Def);
                HeroReleased?.Invoke(t);
            }

            if (t.Remaining > 0) anyLeft = true;
        }

        if (!anyLeft)
        {
            Running = false;
            AllDeployed?.Invoke();
        }
    }
}
