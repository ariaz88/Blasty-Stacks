using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Preconfigure stage unlock + stars in the Inspector (before Play).
/// When Play starts, the plan is applied exactly once:
/// - Level is ensured/reset to 'stagesPerLevel'
/// - Stages 1..(currentUnlocked-1) get stars per your selections
/// - Stage 'currentUnlocked' becomes the next playable (0 stars)
/// - SaveSystem.Save() is called
/// - (Optional) HomeManager.RefreshFromSave() is invoked if assigned
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class DebugStageManager : MonoBehaviour
{
    [Header("Target Level")]
    [Min(1)] public int levelId = 1;
    [Min(1)] public int stagesPerLevel = 20;

    [Tooltip("This will become the next playable stage (1-based). " +
             "All stages before it will be marked as completed per your star plan.")]
    [Min(1)] public int currentUnlockedStage_1Based = 1;

    [Header("Apply Behavior")]
    [Tooltip("If true, the plan applies automatically once when you enter Play Mode.")]
    public bool applyOnEnterPlay = true;

    [Tooltip("If assigned, we'll try to call RefreshFromSave() after applying.")]
    public UnityEngine.Object homeManager;

    [Serializable]
    public enum StarAward
    {
        None = 0,         // (not used for previous stages UI, but handy if you want no award)
        OneStar = 1,      // Clear with HP > 0 and < 50%
        TwoStars = 2,     // HP >= 50% and < 100%
        ThreeStars = 3    // HP = 100%
    }

    [Serializable]
    public class StageStarPlan
    {
        [HideInInspector] public int stageNumber_1Based;
        public StarAward award = StarAward.OneStar;
    }

    // We keep a plan for all stages so you can reuse it,
    // but the Editor will only show 1..(currentUnlocked-1).
    [SerializeField] private List<StageStarPlan> starPlanForAllStages = new List<StageStarPlan>();

    // One-shot guard per play session
    private bool _appliedThisPlay;

    private void OnValidate()
    {
        // Clamp inputs
        if (stagesPerLevel < 1) stagesPerLevel = 1;
        if (currentUnlockedStage_1Based < 1) currentUnlockedStage_1Based = 1;
        if (currentUnlockedStage_1Based > stagesPerLevel) currentUnlockedStage_1Based = stagesPerLevel;

        // Ensure plan list length == stagesPerLevel
        if (starPlanForAllStages == null) starPlanForAllStages = new List<StageStarPlan>();
        if (starPlanForAllStages.Count < stagesPerLevel)
        {
            int start = starPlanForAllStages.Count;
            for (int i = start; i < stagesPerLevel; i++)
            {
                starPlanForAllStages.Add(new StageStarPlan
                {
                    stageNumber_1Based = i + 1,
                    award = StarAward.OneStar
                });
            }
        }
        else if (starPlanForAllStages.Count > stagesPerLevel)
        {
            starPlanForAllStages.RemoveRange(stagesPerLevel, starPlanForAllStages.Count - stagesPerLevel);
        }

        // Keep stage numbers aligned
        for (int i = 0; i < starPlanForAllStages.Count; i++)
            starPlanForAllStages[i].stageNumber_1Based = i + 1;
    }

    private void OnEnable()
    {
        if (Application.isPlaying && applyOnEnterPlay && !_appliedThisPlay)
        {
            ApplyPlan();
            _appliedThisPlay = true;
        }
    }

    /// <summary>
    /// Applies the configured plan to SaveSystem once.
    /// </summary>
    public void ApplyPlan()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugStageManager] Enter Play Mode to apply.");
            return;
        }
#endif
        // Prepare level cleanly
        var lvl = SaveSystem.EnsureLevel(levelId, stagesPerLevel);
        // Reset level stars and unlock state
        if (lvl.stars == null || lvl.stars.Length != stagesPerLevel)
        {
            lvl.stars = new int[stagesPerLevel];
        }
        else
        {
            Array.Clear(lvl.stars, 0, lvl.stars.Length);
        }

        // We want 'currentUnlocked' to be the next playable (index = currentUnlocked-1).
        // That means all previous stages (1..currentUnlocked-1) are completed per plan.
        int targetIndex0 = Mathf.Clamp(currentUnlockedStage_1Based - 1, 0, stagesPerLevel - 1);

        // Apply stars for stages 1..(currentUnlocked-1)
        for (int s1 = 1; s1 <= targetIndex0; s1++)
        {
            var plan = starPlanForAllStages[s1 - 1].award;
            int stars = Mathf.Clamp((int)plan, 0, 3);
            lvl.stars[s1 - 1] = stars;
        }

        // Set highestUnlocked so that 'currentUnlocked' (1-based) is the next selectable.
        // Your UI treats highestUnlocked as an index of last unlocked, so set to (currentUnlocked-1).
        lvl.highestUnlocked = targetIndex0;

        // Persist
        SaveSystem.Save();

        // Optional: Refresh Home UI
        TryRefreshHomeUI();

        Debug.Log($"[DebugStageManager] Applied plan to Level {levelId}: " +
                  $"stagesPerLevel={stagesPerLevel}, currentUnlocked={currentUnlockedStage_1Based} " +
                  $"(previous stages awarded per plan).");
    }

    private void TryRefreshHomeUI()
    {
        if (homeManager == null) return;
        var t = homeManager.GetType();

        // Prefer RefreshFromSave() if present
        var m = t.GetMethod("RefreshFromSave", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (m != null) { m.Invoke(homeManager, null); return; }

        // Fallback: if there's a method to rebuild visuals
        var m2 = t.GetMethod("RefreshAllVisuals", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (m2 != null) { m2.Invoke(homeManager, null); return; }
    }

    // Expose read-only access to the full plan for the custom editor
    public IReadOnlyList<StageStarPlan> GetPlan() => starPlanForAllStages;
}

#if UNITY_EDITOR
[CustomEditor(typeof(DebugStageManager))]
public class DebugStageManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var dm = (DebugStageManager)target;
        serializedObject.Update();

        // Header fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stagesPerLevel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentUnlockedStage_1Based"));
        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("applyOnEnterPlay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("homeManager"));
        EditorGUILayout.Space(8);

        // Dynamic plan UI: show 1..(currentUnlocked-1)
        int showCount = Mathf.Clamp(dm.currentUnlockedStage_1Based - 1, 0, dm.stagesPerLevel);
        if (showCount > 0)
        {
            EditorGUILayout.LabelField($"Previous stages star plan (1..{dm.currentUnlockedStage_1Based - 1})", EditorStyles.boldLabel);
            var planProp = serializedObject.FindProperty("starPlanForAllStages");
            for (int i = 0; i < showCount; i++)
            {
                var elem = planProp.GetArrayElementAtIndex(i);
                var awardProp = elem.FindPropertyRelative("award");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Stage {i + 1}", GUILayout.Width(80));
                EditorGUILayout.PropertyField(awardProp, GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No previous stages to configure (currentUnlocked is 1).", MessageType.Info);
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Apply Now (Play Mode)"))
            {
                dm.ApplyPlan();
            }
        }

        EditorGUILayout.HelpBox(
            "How it works:\n" +
            "• Before Play: set 'currentUnlockedStage' and choose stars for previous stages.\n" +
            "• On Play: the plan applies once.\n" +
            "• 'currentUnlocked' becomes next playable (0★).",
            MessageType.None
        );

        serializedObject.ApplyModifiedProperties();
    }
}
#endif





///// <summary>
///// Minimal debug helper to mark a stage as completed with 1/2/3 stars,
///// using your existing SaveSystem + Home UI. Designed for Inspector use.
///// </summary>
