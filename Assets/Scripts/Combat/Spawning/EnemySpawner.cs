using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Config")]
    public LevelConfig levelConfig;

    [Header("CP weighting (optional)")]
    public CPWeightsConfigSO cpWeights;

    [Header("Battle Gate")]
    [Tooltip("ON  = puzzle-only start; nothing spawns until StartBattle() is called " +
             "(the BATTLE button, through BattleStartController).\n" +
             "OFF = old behaviour, waves begin as soon as the level loads.")]
    [SerializeField] private bool waitForBattleStart = false;

    [Header("Spawn Area")]
    [Tooltip("ON  = enemies spawn in a box measured FROM THE ENEMY GATE, using the " +
             "offsets below. Survives moving the gate, and is per-scene so it does " +
             "not disturb other stages that share the same LevelConfig asset.\n" +
             "OFF = use each Wave's absolute spawnMin/spawnMax world coordinates.")]
    [SerializeField] private bool spawnRelativeToEnemyGate = false;

    [Tooltip("The enemy gate. Left empty = found by the 'EnemyGate' tag, else by name.")]
    [SerializeField] private Transform enemyGateAnchor;

    [Tooltip("Spawn box corner relative to the gate. Negative Y = below the gate, " +
             "i.e. in front of it, facing the player.")]
    [SerializeField] private Vector2 gateRelativeMin = new Vector2(-3f, -4f);
    [SerializeField] private Vector2 gateRelativeMax = new Vector2(3f, -2f);

    // Track wave-alive count if you still want to wait for clear
    int _alive;

    bool _battleStarted;

    /// <summary>True once the wave loop has actually been kicked off.</summary>
    public bool BattleStarted => _battleStarted;

    /// <summary>
    /// True once at least one enemy actually exists. Player units wait for this
    /// before advancing, so heroes never march at an empty field.
    /// </summary>
    public bool HasSpawnedFirstEnemy { get; private set; }

    /// <summary>Raised the moment the very first enemy of the stage is spawned.</summary>
    public event System.Action OnFirstEnemySpawned;

    /// <summary>
    /// Resolves a wave's spawn box, either as authored absolute world coordinates
    /// or as an offset box around the enemy gate.
    /// </summary>
    void ResolveSpawnArea(Wave wave, out Vector2 min, out Vector2 max)
    {
        if (!spawnRelativeToEnemyGate)
        {
            min = wave.spawnMin;
            max = wave.spawnMax;
            return;
        }

        if (!enemyGateAnchor) enemyGateAnchor = FindEnemyGate();

        Vector2 gate = enemyGateAnchor ? (Vector2)enemyGateAnchor.position : Vector2.zero;
        if (!enemyGateAnchor)
            Debug.LogWarning($"{name}: spawnRelativeToEnemyGate is ON but no gate was found - " +
                             "spawning around the world origin.", this);

        min = gate + gateRelativeMin;
        max = gate + gateRelativeMax;
    }

    Transform FindEnemyGate()
    {
        var tagged = GameObject.FindGameObjectWithTag("EnemyGate");
        if (tagged) return tagged.transform;

        var byName = GameObject.Find("EnemyCastle");
        return byName ? byName.transform : null;
    }

    void Start()
    {
        if (!levelConfig)
        {
            Debug.LogWarning($"{name}: missing LevelConfig.");
            return;
        }

        // Puzzle-only phase: hold every wave until the player presses BATTLE.
        if (waitForBattleStart)
            return;

        StartBattle();
    }

    /// <summary>
    /// Kicks off the wave loop. Safe to call repeatedly - only the first call runs.
    /// Called by BattleStartController when the BATTLE button is pressed.
    /// </summary>
    public void StartBattle()
    {
        if (_battleStarted) return;

        if (!levelConfig)
        {
            Debug.LogWarning($"{name}: missing LevelConfig.");
            return;
        }

        _battleStarted = true;
        StartCoroutine(RunLevel());
    }
    bool IsGameplayPaused()
    {
        return HudCurrencyView.Instance != null && HudCurrencyView.Instance.IsGameplayPaused;
    }

    // Wait for duration seconds, but don�t advance while the game is paused
    IEnumerator WaitForSecondsGameplay(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsGameplayPaused())
            {
                elapsed += Time.deltaTime;
            }

            yield return null; // wait one frame
        }
    }


    IEnumerator RunLevel1()
    {
        yield return WaitForSecondsGameplay(levelConfig.startDelay);


        for (int i = 0; i < levelConfig.waves.Count; i++)
        {
            var wave = levelConfig.waves[i];

            if (wave.delayBeforeWave > 0f)
                yield return WaitForSecondsGameplay(wave.delayBeforeWave);


            switch (wave.formation)
            {
                case SpawnFormation.AllTogetherGrid:
                    yield return SpawnAllTogetherGrid(wave, levelConfig.levelNumber);
                    break;

                case SpawnFormation.TwoRows:
                    yield return SpawnTwoRows(wave, levelConfig.levelNumber);
                    break;
            }

            // Wait for wave clear (optional)
            yield return new WaitUntil(() => _alive == 0);
        }

    }
    IEnumerator RunLevel()
    {
        // Use LevelManager�s global additive stage index as the stage/CP level.
        // Fallback to levelConfig.levelNumber if LevelManager is not present
        // (e.g. when testing the scene directly).
        int stageLevel = LevelManager.Instance != null
            ? LevelManager.CurrentStage
            : levelConfig.levelNumber;

        // Initial delay before first wave (respects pause)
        yield return WaitForSecondsGameplay(levelConfig.startDelay);

        for (int i = 0; i < levelConfig.waves.Count; i++)
        {
            var wave = levelConfig.waves[i];

            if (wave.delayBeforeWave > 0f)
                yield return WaitForSecondsGameplay(wave.delayBeforeWave);

            switch (wave.formation)
            {
                case SpawnFormation.AllTogetherGrid:
                    // CP/difficulty for this wave comes from LevelManager.CurrentStage
                    yield return SpawnAllTogetherGrid(wave, stageLevel);
                    break;

                case SpawnFormation.TwoRows:
                    yield return SpawnTwoRows(wave, stageLevel);
                    break;
            }

            // Wait for wave clear (optional)
            yield return new WaitUntil(() => _alive == 0);
        }

        // Stage complete � if you ever want to auto-advance directly from here,
        // you would call LevelManager.Instance?.MarkLevelWon();
        // (Right now WinPanel handles it after the player presses Claim.)
    }


    // ---------- FORMATION HELPERS ----------

    IEnumerator SpawnAllTogetherGrid(Wave wave, int stageLevel)
    {
        // 1) Build a combined list of instances to spawn (interleaved types)
        var toSpawn = new List<(WaveEntry entry, int idx)>();
        int total = 0;
        foreach (var e in wave.entries)
        {
            if (!ValidateEntry(e)) continue;
            total += e.count;
        }
        if (total == 0) yield break;

        // Interleave by round-robin across entries so types are mixed visually
        int maxCount = 0; foreach (var e in wave.entries) if (e.count > maxCount) maxCount = e.count;
        for (int i = 0; i < maxCount; i++)
            foreach (var e in wave.entries)
                if (ValidateEntry(e) && i < e.count)
                    toSpawn.Add((e, i));

        // 2) Generate grid positions inside the wave�s spawn area
        ResolveSpawnArea(wave, out var areaMin, out var areaMax);
        var positions = GenerateGridPositions(areaMin, areaMax, total, wave.gridColumns, wave.minSlotSpacing);

        // 3) Instantiate all at once (tiny stagger just for VFX ordering if you like)
        for (int n = 0; n < toSpawn.Count; n++)
        {
            var (entry, _) = toSpawn[n];
            var pos = positions[n];
            SpawnOne(entry, pos, stageLevel);
        }
        yield return null;
    }

    IEnumerator SpawnTwoRows(Wave wave, int stageLevel)
    {
        // Split entries by row
        var front = new List<WaveEntry>();
        var back = new List<WaveEntry>();
        foreach (var e in wave.entries)
        {
            if (!ValidateEntry(e)) continue;
            if (e.row == RowIndex.FrontRow) front.Add(e); else back.Add(e);
        }



        ResolveSpawnArea(wave, out var areaMin, out var areaMax);

        var yMin = Mathf.Min(areaMin.y, areaMax.y);
        var yMax = Mathf.Max(areaMin.y, areaMax.y);
        float ySpan = Mathf.Max(0.01f, yMax - yMin);

        float gap = Mathf.Abs(wave.rowYOffset) > 0f ? Mathf.Abs(wave.rowYOffset) : (ySpan * 0.5f);

        float frontY, backY;
        if (wave.frontAnchor == FrontRowAnchor.MinY)
        {
            // Front at lower Y (closer to camera in your case)
            frontY = yMin;
            backY = Mathf.Min(yMax, frontY + gap);  // push back row upward
        }
        else
        {
            // Front at higher Y
            frontY = yMax;
            backY = Mathf.Max(yMin, frontY - gap);  // push back row downward
        }



        float xMin = Mathf.Min(areaMin.x, areaMax.x);
        float xMax = Mathf.Max(areaMin.x, areaMax.x);

        // ---- FRONT ROW ----
        int frontCount = 0; foreach (var e in front) frontCount += e.count;
        if (frontCount > 0)
        {
            var xs = DistributeAlongX(xMin, xMax, frontCount);
            int idx = 0;
            foreach (var e in front)
                for (int i = 0; i < e.count; i++)
                    SpawnOne(e, new Vector3(xs[idx++], frontY, 0f), stageLevel);
        }

        // optional delay between rows
        if (back.Count > 0 && wave.secondRowDelay > 0f)
            yield return WaitForSecondsGameplay(wave.secondRowDelay);


        // ---- BACK ROW ----
        int backCount = 0; foreach (var e in back) backCount += e.count;
        if (backCount > 0)
        {
            var xs = DistributeAlongX(xMin, xMax, backCount);
            int idx = 0;
            foreach (var e in back)
                for (int i = 0; i < e.count; i++)
                    SpawnOne(e, new Vector3(xs[idx++], backY, 0f), stageLevel);
        }

        yield return null;
    }

    // ---------- LOW-LEVEL UTILS ----------

    bool ValidateEntry(WaveEntry e)
    {
        if (!e.enemyPrefab) { Debug.LogWarning("WaveEntry missing enemyPrefab"); return false; }
        if (!e.statsBase) { Debug.LogWarning("WaveEntry missing statsBase"); return false; }
        return true;
    }

    void SpawnOne(WaveEntry entry, Vector3 pos, int stageLevel)
    {
        var go = Instantiate(entry.enemyPrefab, pos, Quaternion.identity);
        _alive++;

        if (!HasSpawnedFirstEnemy)
        {
            HasSpawnedFirstEnemy = true;
            OnFirstEnemySpawned?.Invoke();
        }

        var em = go.GetComponent<EnemyManager>();
        if (em)
        {
            em.statsBase = entry.statsBase;
            em.unitLevel = entry.unitLevel;         // growth (ProgressionConfigSO in EnemyManager)
            em.stageLevel = stageLevel;              // CP weights stage
            em.cpWeights = cpWeights;               // optional
            em.Initialize(stageLevel);               // builds stats, sets HP, computes CP
        }

        var eh = go.GetComponent<EnemyStats>();
        if (eh) StartCoroutine(WatchDeath(eh));
    }

    IEnumerator WatchDeath(EnemyStats e)
    {
        while (e && !e.enemyIsdead) yield return null;
        _alive = Mathf.Max(0, _alive - 1);
    }

    // Generate N positions in a grid that fits inside [min,max]
    static List<Vector3> GenerateGridPositions(Vector2 min, Vector2 max, int count, int columns, Vector2 minSpacing)
    {
        var positions = new List<Vector3>(count);

        float xMin = Mathf.Min(min.x, max.x);
        float xMax = Mathf.Max(min.x, max.x);
        float yMin = Mathf.Min(min.y, max.y);
        float yMax = Mathf.Max(min.y, max.y);
        float w = Mathf.Max(0.01f, xMax - xMin);
        float h = Mathf.Max(0.01f, yMax - yMin);

        // columns: auto if not set
        int cols = (columns > 0) ? columns : Mathf.CeilToInt(Mathf.Sqrt(count));
        cols = Mathf.Max(1, cols);
        int rows = Mathf.CeilToInt(count / (float)cols);

        // spacing: try to fit evenly, respecting minSpacing
        float dx = w / (cols + 1);
        float dy = h / (rows + 1);
        dx = Mathf.Max(dx, (minSpacing.x > 0f ? minSpacing.x : dx));
        dy = Mathf.Max(dy, (minSpacing.y > 0f ? minSpacing.y : dy));

        // recompute effective cols/rows to keep positions inside area
        // we�ll center within the rectangle
        float xStart = xMin + dx;
        float yStart = yMin + dy;

        int placed = 0;
        for (int r = 0; r < rows && placed < count; r++)
        {
            for (int c = 0; c < cols && placed < count; c++)
            {
                float x = xStart + c * dx;
                float y = yStart + (rows - 1 - r) * dy; // top-to-bottom
                x = Mathf.Clamp(x, xMin, xMax);
                y = Mathf.Clamp(y, yMin, yMax);
                positions.Add(new Vector3(x, y, 0f));
                placed++;
            }
        }
        return positions;
    }

    // Evenly spaced X positions between xMin..xMax (inclusive ends)
    static float[] DistributeAlongX(float xMin, float xMax, int count)
    {
        var xs = new float[count];
        if (count == 1) { xs[0] = (xMin + xMax) * 0.5f; return xs; }
        float step = (xMax - xMin) / (count - 1);
        for (int i = 0; i < count; i++) xs[i] = xMin + i * step;
        return xs;
    }
}
