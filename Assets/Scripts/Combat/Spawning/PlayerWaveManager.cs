using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWaveManager : MonoBehaviour
{
 


    [Header("Refs")]
    [SerializeField] private MatchResolver matchResolver;    // assign your MatchResolver
    [SerializeField] private GameObject[] playerPrefabs;     // assign multiple Player prefabs here
    [SerializeField] private GameObject playerRootPrefab;


    [SerializeField] private Transform[] gatePoints = new Transform[4]; // 4 spawn points

    [Header("Wave Settings")]
    [SerializeField, Min(0f)] private float nextWaveDelay = 0.75f;
    [SerializeField] private int minPerWave = 1;   // inclusive
    [SerializeField] private int maxPerWave = 4;   // inclusive


    [Tooltip("If true, we also freeze physics while locked (Rigidbody2D.Static).")]
    [SerializeField] private bool freezePhysicsWhileLocked = true;


    [Header("Spawn Safety")]
    [SerializeField] private LayerMask playerLayer;                 // layer used by players
    [SerializeField, Min(0f)] private float occupyCheckRadius = 0.3f;

    // runtime
    private readonly List<PlayerManager> currentWave = new();
    private bool waveLocked = false;
    private bool running = false;

    [SerializeField, Min(0.1f)] private float jumpWaitTimeout = 0.5f; // failsafe

    [Header("Stage Animation")]
    private bool unlockAnimInProgress = false;
    private int pendingStageEvents = 0;


    private GameStartManager _gsm;
    private PlayerUnitsModel PlayerUnits => _gsm ? _gsm.PlayerUnits : null;
    private UnitsDatabaseSO _unitsDb;


    private void OnEnable()
    {
        if (matchResolver)
            MatchResolver.OnBlast += HandleBlast;   // <-- static subscribe
    }

    private void OnDisable()
    {
        if (matchResolver)
            MatchResolver.OnBlast -= HandleBlast;   // <-- static unsubscribe
    }

    private void Awake()
    {
        _gsm = FindObjectOfType<GameStartManager>();
        if (_gsm == null)
        {
            Debug.LogError("PlayerWaveManager: GameStartManager not found.");
            return;
        }

        _unitsDb = _gsm.unitsDatabase;
    }

    private List<UnitDefinitionSO> GetDeployedUnitDefinitions()
    {
        var list = new List<UnitDefinitionSO>();
        if (PlayerUnits == null || _unitsDb == null) return list;

        foreach (var def in _unitsDb.Units)
        {
            if (!def) continue;
            if (!PlayerUnits.IsUnlocked(def.unitId)) continue;
            if (!PlayerUnits.IsDeployed(def.unitId)) continue;

            list.Add(def);
        }

        return list;
    }

    private void Start()
    {
        BeginWaves();
  

    }
    public void RestartAfterRevive()
    {
        StopAllCoroutines();
        running = false;
        waveLocked = false;
        currentWave.Clear();

        BeginWaves();   // uses WaveLoop that checks puzzle again
    }

    // Call this to start the loop (e.g., at level start)
    public void BeginWaves()
    {
        if (running) return;
        running = true;
        StartCoroutine(WaveLoop());
    }

    // Call this when level ends (win/lose)
    public void StopWaves()
    {
        running = false;
        StopAllCoroutines();
    }


    // ---------------- Wave Loop ----------------

    private IEnumerator WaveLoop2()
    {
        // Initial wave: only if puzzle has stacks
        if (PuzzleHasStacks())
        {
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
        else
        {
            // Board already empty → no reason to run the wave system
            running = false;
            yield break;
        }

        while (running)
        {
            // Wait until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked)
                yield return null;

            if (!running) yield break;

            // After the wave is unlocked, check if the puzzle is now empty.
            // If yes, do NOT spawn any more players.
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // A little delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Double-check right before spawning the next wave
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }


    private void SpawnLockedWave(int count)
    {
        // Safety: don't spawn if puzzle is empty
        if (!PuzzleHasStacks())
            return;

        currentWave.Clear();

        // choose up to 'count' free gate indices without replacement
        var freeIndices = GetFreeGateIndices();
        Shuffle(freeIndices);

        int toSpawn = Mathf.Clamp(count, 0, freeIndices.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            int gi = freeIndices[i];


            var stage = gatePoints[gi];
            var pm = SpawnOneAt(stage.position, stage.rotation);

            if (pm != null)
            {
                pm.childSnapshot = new List<ChildLocalSnapshot>();

                foreach (Transform child in pm.transform)
                {
                    pm.childSnapshot.Add(new ChildLocalSnapshot
                    {
                        t = child,
                        localPos = child.localPosition,
                        localRot = child.localRotation,
                        localScale = child.localScale
                    });
                }

                Transform topAnchor = GetStageTopAnchor(stage);
                pm.transform.SetParent(topAnchor, true);


                ApplyLock(pm, true);
                currentWave.Add(pm);
            }

        }

        waveLocked = true; // this wave starts locked
    }



    // ---------------- Wave Loop Without Checking Blocks Present in The Board ----------------
    private IEnumerator WaveLoop1()
    {

        // initial wave
        SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));

        while (running)
        {
            // Wait here until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked) yield return null;

            if (!running) yield break;

            // A little delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }

    private void SpawnLockedWave1(int count)
    {
        currentWave.Clear();

        // choose up to 'count' free gate indices without replacement
        var freeIndices = GetFreeGateIndices();
        Shuffle(freeIndices);

        int toSpawn = Mathf.Clamp(count, 0, freeIndices.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            int gi = freeIndices[i];
            var pm = SpawnOneAt(gatePoints[gi].position, gatePoints[gi].rotation);
            GameObject.FindObjectOfType<RogueliteManager>().RegisterPlayer(pm.GetComponent<PlayerStatsApplier>());


            if (pm != null)
            {
                ApplyLock(pm, true);
                currentWave.Add(pm);
            }
        }

        waveLocked = true; // this wave starts locked
    }


    private IEnumerator WaveLoop()
    {
        // 1) Wait until there is at least one active block on the board
        //    (MatchResolver will dynamically search PieceSimple for us).
        while (running && (matchResolver == null || !matchResolver.HasAnyStacksLeft()))
        {
            // Wait one frame and try again
            yield return null;
        }

        // If something stopped waves externally while we were waiting, bail out.
        if (!running)
            yield break;

        // 2) Now we know there are blocks on the board → spawn the first wave
        SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));

        while (running)
        {
            // Wait until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked)
                yield return null;

            if (!running)
                yield break;

            // After the wave is unlocked, check if the puzzle is now empty.
            // If yes, do NOT spawn any more players.
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Small delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Double-check right before spawning the next wave
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }

    // ---------------- End Of Wave Loop ----------------


    private PlayerManager SpawnOneAt1(Vector3 pos, Quaternion rot)
    {
        if (playerPrefabs[0] == null) return null;
        var go = Instantiate(playerPrefabs[0], pos, rot);

        Vector3 ls = go.transform.localScale;
        ls *= 1.1f;
        go.transform.localScale = ls;

        var pm = go.GetComponent<PlayerManager>();

        if (pm == null)
        {
            Debug.LogWarning("Spawned prefab has no PlayerManager.", go);
            return null;
        }

        return pm;
    }
    private PlayerManager SpawnOneAt2(Vector3 pos, Quaternion rot)
    {
        // safety: no prefabs assigned
        if (playerPrefabs == null || playerPrefabs.Length == 0)
        {
            Debug.LogWarning("PlayerWaveManager: No playerPrefabs assigned in the inspector.");
            return null;
        }

        // pick a random prefab from the array
        GameObject randomPrefab = playerPrefabs[UnityEngine.Random.Range(0, playerPrefabs.Length)];
        if (randomPrefab == null)
        {
            Debug.LogWarning("PlayerWaveManager: Selected random player prefab is null.");
            return null;
        }

        // instantiate the chosen prefab
        var go = Instantiate(randomPrefab, pos, rot);

        // same scale tweak as before
        Vector3 ls = go.transform.localScale;
        ls *= 1.1f;
        go.transform.localScale = ls;

        var pm = go.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogWarning("Spawned prefab has no PlayerManager.", go);
            return null;
        }

        return pm;
    }

    private PlayerManager SpawnOneAt3(Vector3 pos, Quaternion rot)
    {
        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Choose which unit to spawn (random or ordered)
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        if (!def.visualPrefab)
        {
            Debug.LogWarning($"Unit {def.displayName} has no runtimePrefab.");
            return null;
        }

        var go = Instantiate(def.visualPrefab, pos, rot);

        // Scale tweak (keep your existing logic)
        go.transform.localScale *= 1.1f;

        var pm = go.GetComponent<PlayerManager>();
        if (!pm)
        {
            Debug.LogWarning("Spawned prefab missing PlayerManager.", go);
            Destroy(go);
            return null;
        }


        return pm;
    }

    private PlayerManager SpawnOneAt4(Vector3 pos, Quaternion rot)
    {
        if (playerRootPrefab == null)
        {
            Debug.LogError("PlayerWaveManager: playerRootPrefab not assigned.");
            return null;
        }

        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Choose which deployed unit to spawn
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        // 1) Spawn player root (brain, physics, FSM)
        var root = Instantiate(playerRootPrefab, pos, rot);
        var pm = root.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogError("Player root prefab has no PlayerManager.");
            Destroy(root);
            return null;
        }

        // 2) Inject visual
        if (pm.visualRoot != null && def.visualPrefab != null)
        {
            for (int i = pm.visualRoot.childCount - 1; i >= 0; i--)
                Destroy(pm.visualRoot.GetChild(i).gameObject);

            var visual = Instantiate(def.visualPrefab, pm.visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = def.uiVisualScale;
        }

        // 3) Configure PlayerStatsApplier (THIS IS THE CRITICAL PART)
        var statsApplier = pm.playerStatsApplier != null
            ? pm.playerStatsApplier
            : pm.GetComponent<PlayerStatsApplier>();

        if (statsApplier != null)
        {
            statsApplier.enabled = true;

            // 🔑 Single source of truth
            typeof(PlayerStatsApplier)
                .GetField("unitId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(statsApplier, def.unitId);

            //// Force immediate recompute (safe even if Awake already ran)

            //// Sync PlayerManager runtime cache
            statsApplier.SetUnitId(def.unitId);
            statsApplier.ApplyNow();
            pm.unitStats = statsApplier.CurrentStats;

        }
        else
        {
            Debug.LogWarning("Spawned player has no PlayerStatsApplier.");
        }

        return pm;
    }
    private PlayerManager SpawnOneAt5(Vector3 pos, Quaternion rot)
    {
        if (playerRootPrefab == null)
        {
            Debug.LogError("PlayerWaveManager: playerRootPrefab not assigned.");
            return null;
        }

        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        var root = Instantiate(playerRootPrefab, pos, rot);
        var pm = root.GetComponent<PlayerManager>();
        if (!pm)
        {
            Destroy(root);
            return null;
        }

        // Inject visual
        if (pm.visualRoot != null && def.visualPrefab != null)
        {
            for (int i = pm.visualRoot.childCount - 1; i >= 0; i--)
                Destroy(pm.visualRoot.GetChild(i).gameObject);

            var visual = Instantiate(def.visualPrefab, pm.visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = def.uiVisualScale;
        }

        // Configure stats
        var applier = pm.playerStatsApplier;
        if (applier != null)
        {
            applier.SetUnitId(def.unitId);
            applier.ApplyNow();
            pm.unitStats = applier.CurrentStats;
        }

        return pm;
    }
    private PlayerManager SpawnOneAt(Vector3 pos, Quaternion rot)
    {
        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Pick one deployed unit
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        if (def.runtimePrefab == null)
        {
            Debug.LogError($"Unit '{def.displayName}' has no runtimePrefab assigned.");
            return null;
        }

        // Instantiate THAT unit directly
        var go = Instantiate(def.runtimePrefab, pos, rot);

        var pm = go.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogError($"Runtime prefab '{def.runtimePrefab.name}' has no PlayerManager.");
            Destroy(go);
            return null;
        }

        // Apply unit identity to stats system
        if (pm.playerStatsApplier != null)
        {
            pm.playerStatsApplier.SetUnitId(def.unitId);
            pm.playerStatsApplier.ApplyNow();
            pm.unitStats = pm.playerStatsApplier.CurrentStats;
        }

        return pm;
    }


    private void ApplyLock(PlayerManager pm, bool locked)
    {
        // Option A: switch to your Lock state if provided
        if (locked /*&& lockState != null*/)
        {
            pm.SwitchToNextState(pm.PlayerLockState);  // <-- use your actual API to change states
        }

        // Option B: fallback flags if no state provided
        pm.canMove = !locked;
        pm.SetAnimMoving(false);

        if (pm.playerRigidbody != null && freezePhysicsWhileLocked)
            pm.playerRigidbody.bodyType = locked ? RigidbodyType2D.Static : RigidbodyType2D.Dynamic;

    }

    private void UnlockCurrentWave1()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

                pm.SwitchToNextState(pm.PlayerPursueTargetState); // <-- use your state-machine API
               pm.isUnlocked = true;

            // Option B: unfreeze flags
            ApplyLock(pm, false);
        }

        waveLocked = false;
    }


    private void UnlockCurrentWave()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            // Unfreeze/unlock flags first if you need
            pm.isUnlocked = true;
            ApplyLock(pm, false);

            // Kick off: jump now, then pursue when done
            StartCoroutine(JumpThenSwitch(pm));
        }

        waveLocked = false;
    }




    // Called by MatchResolver when a group clears
    private void HandleBlast(int _)
    {
        if (!running) return;
        if (!waveLocked) return; // already unlocked; ignore extra blasts
        if (unlockAnimInProgress) return; // ignore extra blasts while animation is running

        PlayStageUnlockAnimations();

    }


    private void PlayStageUnlockAnimations()
    {
        // prevent retriggering while we are waiting for animation events
        if (unlockAnimInProgress)
            return;

        pendingStageEvents = 0;

        for (int i = 0; i < gatePoints.Length; i++)
        {
            Transform gate = gatePoints[i];
            if (gate == null) continue;

            // only trigger the stage if it currently has a player on it
            Collider2D c = Physics2D.OverlapCircle((Vector2)gate.position, occupyCheckRadius, playerLayer);
            if (c == null) continue;

            Animator anim = gate.GetComponentInChildren<Animator>();
            if (anim == null) continue;

            pendingStageEvents++;

            // THIS IS THE ONLY CORRECT PLACE
            anim.ResetTrigger("Throw");
            anim.SetTrigger("Throw");

        }

        // If for any reason no stage detected (shouldn’t happen), unlock immediately as a fallback
        if (pendingStageEvents == 0)
        {
            UnlockCurrentWaveViaAnimation();
            return;
        }

        unlockAnimInProgress = true;
    }
    public void OnOneStageUnlockEventFired()
    {
        if (!unlockAnimInProgress)
            return;

        pendingStageEvents--;

        if (pendingStageEvents > 0)
            return;

        // all relevant stages fired the event -> now unlock the wave
        unlockAnimInProgress = false;
        UnlockCurrentWaveViaAnimation();
    }


    private void UnlockCurrentWaveViaAnimation1()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            pm.isUnlocked = true;
            ApplyLock(pm, false);

            StartCoroutine(JumpThenSwitch(pm));
        }

        waveLocked = false;
    }
    private void UnlockCurrentWaveViaAnimation()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            // 1) DETACH from stage
            pm.transform.SetParent(null, true);

            // 2) RESTORE CHILD LOCAL TRANSFORMS (THIS IS YOUR CODE)
            if (pm.childSnapshot != null)
            {
                foreach (var s in pm.childSnapshot)
                {
                    if (s.t == null) continue;
                    s.t.localPosition = s.localPos;
                    s.t.localRotation = s.localRot;
                    s.t.localScale = s.localScale;
                }
            }

            // 3) UNLOCK + JUMP
            pm.isUnlocked = true;
            ApplyLock(pm, false);
            StartCoroutine(JumpThenSwitch(pm));
        }

        waveLocked = false;
    }


    private System.Collections.IEnumerator JumpThenSwitch(PlayerManager pm)
    {
        // Find the jumper on this player (root or children)
        var jumper = pm.GetComponent<FrogJumpTransformOnly>();
        if (jumper == null)
        {
            // no jump component; just switch immediately
            pm.SwitchToNextState(pm.PlayerPursueTargetState);
            yield break;
        }

        // Start a jump if not already mid-jump
        if (!jumper.IsJumping)
            jumper.TriggerJump();

        // Wait until jump completes (with timeout failsafe)
        float t0 = Time.time;
        while (jumper.IsJumping && (Time.time - t0) < jumpWaitTimeout)
            yield return null;

        // Now move to pursue
        pm.SwitchToNextState(pm.PlayerPursueTargetState);

    }



    // ==== helpers ====

    private List<int> GetFreeGateIndices()
    {
        var list = new List<int>(4);
        for (int i = 0; i < gatePoints.Length; i++)
        {
            if (!gatePoints[i]) continue;

            // If something sits there, skip it (prevents stacking)
            var c = Physics2D.OverlapCircle((Vector2)gatePoints[i].position, occupyCheckRadius, playerLayer);
            if (c == null) list.Add(i);
        }
        return list;
    }

    private static void Shuffle<T>(IList<T> arr)
    {
        for (int i = arr.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (gatePoints == null) return;
        Gizmos.color = new Color(0, 1, 0, 0.35f);
        foreach (var t in gatePoints)
        {
            if (!t) continue;
            Gizmos.DrawWireSphere(t.position, occupyCheckRadius);
        }
    }


    private bool PuzzleHasStacks()
    {
        return matchResolver != null && matchResolver.HasAnyStacksLeft();
    }

    // Adding  a helper to find Top child  on a stage
    private Transform GetStageTopAnchor(Transform stage)
    {
        if (stage == null) return null;

        var top = stage.Find("Top_0");
        if (top == null)
        {
            Debug.LogWarning($"Stage '{stage.name}' has no child named 'Top_0'.");
            return stage; // fallback: parent to stage root
        }

        return top;
    }


}
