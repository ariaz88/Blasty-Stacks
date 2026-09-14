using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the DEPLOYMENT QUEUE: once BATTLE is pressed, one 6-second load per match
/// the player cleared, back to back, each releasing that match's heroes onto the
/// field the instant its bar fills.
///
/// This is the second half of PHASE 2. The first half stopped heroes spawning on
/// every match and made each match deal CARDS instead
/// (HeroCardRevealDirector); the heroes earned that way sat in
/// PlayerWaveManager.EarnedBatches with no way onto the battlefield. This is that
/// way.
///
/// WHY A SEQUENCER AND NOT A TIMER PER CELL:
/// the loads are strictly serial and their number is only known when BATTLE is
/// pressed (it equals the matches cleared). One owner walking a queue keeps
/// "which load is running" in a single place, which is what the panel renders and
/// what decides who spawns next. Per-cell timers would each have to re-derive it.
///
/// It owns NO roster state: the batches, their order and their contents all come
/// from PlayerWaveManager. It decides only WHEN, and asks PlayerWaveManager to do
/// the spawning.
/// </summary>
[DisallowMultipleComponent]
public class HeroDeploymentSequencer : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long ONE load takes, in seconds. Ref2 shows 6s. Every load is " +
             "the same length regardless of how many heroes it releases.")]
    [Min(0.1f)] [SerializeField] private float loadDuration = 6f;

    [Tooltip("Dead time between one load finishing and the next starting. 0 = the " +
             "next bar starts on the same frame, which is what the brief asks for.")]
    [Min(0f)] [SerializeField] private float gapBetweenLoads = 0f;

    [Header("Refs (left empty = found in the scene at Awake)")]
    [SerializeField] private PlayerWaveManager waveManager;
    [SerializeField] private BattleStartController battleStart;

    /// <summary>Index of the load currently running, 0-based. -1 when idle.</summary>
    public int CurrentLoadIndex { get; private set; } = -1;

    /// <summary>0..1 progress of the running load. 0 when idle.</summary>
    public float CurrentLoadProgress { get; private set; }

    /// <summary>How many loads this battle will run - fixed when BATTLE is pressed.</summary>
    public int TotalLoads { get; private set; }

    /// <summary>True from the first load starting until the last one has released.</summary>
    public bool Running { get; private set; }

    /// <summary>
    /// The heroes the RUNNING load will release; null when idle.
    ///
    /// Exposed so a listener that subscribed too late can catch up on the load
    /// already in flight. HeroStatsPanel needs exactly that: the sequencer starts
    /// its queue from BattleStartController.OnBattleStarted and fires
    /// LoadStarted(0) inside that same call, before the panel has finished
    /// building its cells - so load 1 would otherwise never be drawn.
    /// </summary>
    public IReadOnlyList<UnitDefinitionSO> CurrentBatch { get; private set; }

    /// <summary>
    /// A load began. Carries its 0-based index and the heroes it will release, so
    /// the panel can light exactly those cells and grey the rest.
    /// </summary>
    public event Action<int, IReadOnlyList<UnitDefinitionSO>> LoadStarted;

    /// <summary>Progress of the running load, 0..1, once per frame.</summary>
    public event Action<int, float> LoadProgress;

    /// <summary>A load filled and its heroes have been handed to PlayerWaveManager.</summary>
    public event Action<int, IReadOnlyList<UnitDefinitionSO>> LoadCompleted;

    /// <summary>Every load has run. The panel uses this to settle into its final look.</summary>
    public event Action AllLoadsCompleted;

    private Coroutine routine;

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
    }

    private void OnDisable()
    {
        if (battleStart) battleStart.OnBattleStarted -= HandleBattleStarted;
    }

    private void HandleBattleStarted()
    {
        // The puzzle phase is over at this point (BattleStartController seals the
        // wave manager), so the batch list can no longer grow and the queue length
        // is final.
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RunQueue());
    }

    /// <summary>Stops the queue where it stands. Nothing further is released.</summary>
    public void StopQueue()
    {
        if (routine != null) StopCoroutine(routine);

        routine = null;
        Running = false;
        CurrentLoadIndex = -1;
        CurrentLoadProgress = 0f;
    }

    private IEnumerator RunQueue()
    {
        if (!waveManager) yield break;

        // Snapshot the batches. PlayerWaveManager is sealed for battle by now, so
        // this cannot change under us - copying makes that explicit rather than
        // relying on it.
        var batches = new List<IReadOnlyList<UnitDefinitionSO>>();
        foreach (var b in waveManager.EarnedBatches)
            if (b != null && b.Count > 0) batches.Add(b);

        TotalLoads = batches.Count;
        if (TotalLoads == 0)
        {
            AllLoadsCompleted?.Invoke();
            yield break;
        }

        Running = true;

        for (int i = 0; i < batches.Count; i++)
        {
            CurrentLoadIndex = i;
            CurrentLoadProgress = 0f;
            CurrentBatch = batches[i];
            LoadStarted?.Invoke(i, batches[i]);

            for (float t = 0f; t < loadDuration; t += Time.deltaTime)
            {
                CurrentLoadProgress = Mathf.Clamp01(t / loadDuration);
                LoadProgress?.Invoke(i, CurrentLoadProgress);
                yield return null;
            }

            CurrentLoadProgress = 1f;
            LoadProgress?.Invoke(i, 1f);

            // Release FIRST, announce after, so a listener reacting to
            // LoadCompleted already sees the heroes on their way.
            waveManager.DeployBatch(batches[i]);
            LoadCompleted?.Invoke(i, batches[i]);

            if (gapBetweenLoads > 0f && i < batches.Count - 1)
                yield return new WaitForSeconds(gapBetweenLoads);
        }

        Running = false;
        CurrentLoadIndex = -1;
        CurrentLoadProgress = 0f;
        CurrentBatch = null;
        routine = null;

        AllLoadsCompleted?.Invoke();
    }
}
