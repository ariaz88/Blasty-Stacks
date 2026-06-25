using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    /// <summary>
    /// Fires when stage changes, but gives (levelIndex, stageIndexWithinLevel, globalStage).
    /// This is raised right after OnStageChanged(int).
    /// </summary>
    public event Action<int, int, int> OnLevelStageChanged;

    [Header("Level/Stage structure")]
    [SerializeField] private int stagesPerLevel = 20;   // e.g., 20 stages (1..20) per level
    public static int StagesPerLevel => Instance ? Instance.stagesPerLevel : 20;

    // Current "level index" (1..N) and "stage index within level" (1..stagesPerLevel)
    public static int CurrentLevelIndex =>
        Instance ? Instance.GetLevelIndex(Instance._currentStage) : 1;

    public static int CurrentStageIndexInLevel =>
        Instance ? Instance.GetStageIndexInLevel(Instance._currentStage) : 1;




    // Read anywhere: LevelManager.CurrentStage
    public static int CurrentStage => Instance ? Instance._currentStage : 1;

    [Header("Progress")]
    [SerializeField] private int startingStage = 1;
    [SerializeField] private int maxStage = 999;               // clamp if needed

    [Header("Optional: auto-load scenes per stage (index = stage-1)")]
    [SerializeField] private string[] stageSceneNames;          // leave empty to manage scenes yourself

    public event Action<int> OnStageChanged;                    // passes new stage

    const string PlayerPrefsKey = "LM.CurrentStage";
    int _currentStage;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // load saved stage or start fresh
        _currentStage = PlayerPrefs.GetInt(PlayerPrefsKey, startingStage);
        _currentStage = Mathf.Clamp(_currentStage, 1, Mathf.Max(1, maxStage));
    }

    // Call this when the player wins the current level
    public void MarkLevelWon()
    {
        SetStage(_currentStage + 1, loadScene: true);
    }

    // Call this to restart current stage after loss/quit
    public void RestartStage()
    {
        LoadSceneForStage(_currentStage);
    }

    // Jump to a specific stage (e.g., from menu/debug)
    public void SetStage(int stage, bool loadScene = true)
    {
        int clamped = Mathf.Clamp(stage, 1, Mathf.Max(1, maxStage));
        if (clamped == _currentStage && !loadScene) return;

        _currentStage = clamped;
        PlayerPrefs.SetInt(PlayerPrefsKey, _currentStage);
        PlayerPrefs.Save();

        OnStageChanged?.Invoke(_currentStage);

        // NEW: also broadcast (level, stageInLevel, globalStage)
        int L = GetLevelIndex(_currentStage);
        int S = GetStageIndexInLevel(_currentStage);
        OnLevelStageChanged?.Invoke(L, S, _currentStage);

        if (loadScene) LoadSceneForStage(_currentStage);
    }


    void LoadSceneForStage(int stage)
    {
        if (stageSceneNames != null && stageSceneNames.Length >= stage && stageSceneNames[stage - 1] != string.Empty)
        {
            string sceneName = stageSceneNames[stage - 1];
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
        }

        // If no mapping provided, reload current active scene (you can replace this)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Convert global stage (1..∞) → (levelIndex, stageIndexWithinLevel).</summary>
    public void GlobalToLevelStage(int globalStage, out int levelIndex, out int stageIndexWithinLevel)
    {
        int g = Mathf.Max(1, globalStage);
        levelIndex = ((g - 1) / stagesPerLevel) + 1;
        stageIndexWithinLevel = ((g - 1) % stagesPerLevel) + 1;
    }

    /// <summary>Convert (levelIndex, stageIndexWithinLevel) → global stage (1..∞).</summary>
    public int LevelStageToGlobal(int levelIndex, int stageIndexWithinLevel)
    {
        int L = Mathf.Max(1, levelIndex);
        int S = Mathf.Clamp(stageIndexWithinLevel, 1, stagesPerLevel);
        return (L - 1) * stagesPerLevel + S;
    }

    /// <summary>Return the level index for a given global stage.</summary>
    public int GetLevelIndex(int globalStage)
    {
        int g = Mathf.Max(1, globalStage);
        return ((g - 1) / stagesPerLevel) + 1;
    }

    /// <summary>Return the stage index within the current level for a given global stage.</summary>
    public int GetStageIndexInLevel(int globalStage)
    {
        int g = Mathf.Max(1, globalStage);
        return ((g - 1) % stagesPerLevel) + 1;
    }

    /// <summary>
    /// Have we reached (or passed) the required (levelIndex, stageIndexWithinLevel)?
    /// Example: HasReached(1, 16) means "Stage 1-16 or higher".
    /// </summary>
    public bool HasReached(int requiredLevelIndex, int requiredStageIndexWithinLevel)
    {
        int targetGlobal = LevelStageToGlobal(requiredLevelIndex, requiredStageIndexWithinLevel);
        return _currentStage >= targetGlobal;
    }

    public static void ResetProgressToStart()
    {
        // If the singleton already exists, reset its runtime value
        if (Instance != null)
        {
            Instance._currentStage = Instance.startingStage;

            // Overwrite the PlayerPrefs entry
            PlayerPrefs.SetInt(PlayerPrefsKey, Instance._currentStage);
            PlayerPrefs.Save();

            // Re-fire events so any listeners can update if needed
            Instance.OnStageChanged?.Invoke(Instance._currentStage);

            int L = Instance.GetLevelIndex(Instance._currentStage);
            int S = Instance.GetStageIndexInLevel(Instance._currentStage);
            Instance.OnLevelStageChanged?.Invoke(L, S, Instance._currentStage);

            Debug.Log("[LevelManager] Progress reset to starting stage: " + Instance._currentStage);
        }
        else
        {
            // If LevelManager hasn't been created yet in this session,
            // just clear its PlayerPrefs key so Awake() will use startingStage.
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            Debug.Log("[LevelManager] PlayerPrefs stage key cleared; will use startingStage on next Awake.");
        }
    }


}
