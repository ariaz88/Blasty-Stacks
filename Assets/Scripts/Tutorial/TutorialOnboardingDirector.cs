using System.Collections;
using UnityEngine;

/// <summary>
/// Starts the onboarding chain the first time a particular stage is LOST.
///
/// It listens on LevelGameManager.OnGameStateChanged rather than on the panel,
/// because that event fires one line BEFORE revivePanel.ShowLosePanel()
/// (LevelGameManager.cs:204 / :209) - we are already counting before the panel has
/// even been switched on, so there is no frame in which REPLAY is reachable before
/// the gate closes.
///
/// The second half of the chain lives in MenuScene and is armed purely by this
/// sequence's save flag, through TutorialTrigger.requiresTutorialId. Nothing is
/// carried across the scene load in memory.
///
/// NOTE: OnGameStateChanged is STATIC, so OnDisable must unsubscribe or the
/// delegate leaks across every scene load for the rest of the session.
/// </summary>
[DisallowMultipleComponent]
public class TutorialOnboardingDirector : MonoBehaviour
{
    [Header("What to play")]
    [SerializeField] private TutorialSequenceSO sequence;

    [Tooltip("Overlay to draw through. Left empty, the one in the scene is found.")]
    [SerializeField] private TutorialOverlay overlay;

    [Header("When")]
    [SerializeField] private int levelId = 1;
    [SerializeField] private int stage1Based = 6;

    [Tooltip("Only fire when the player has never cleared this stage. Stars are " +
             "written on a WIN only, so zero stars really does mean 'never beaten'.")]
    [SerializeField] private bool requireNeverCleared = true;

    [Tooltip("Unscaled wait after the defeat, so RevivePanel's 0.35s fade-in has " +
             "finished before a hand lands on a half-transparent button.")]
    [SerializeField] private float panelSettleDelay = 0.55f;

    [Header("Preconditions")]
    [Tooltip("Do not start the chain unless the first deployed hero can actually be " +
             "upgraded right now. The chain ends on an Upgrade tap, so starting it " +
             "while the player is broke would walk them into a dead end.")]
    [SerializeField] private bool requireUpgradeAffordable = true;

    [Header("Testing")]
    [Tooltip("EDITOR ONLY: play even if the tutorial is already marked as seen.")]
    [SerializeField] private bool forceReplayInEditor = false;

    private void OnEnable() => LevelGameManager.OnGameStateChanged += HandleStateChanged;
    private void OnDisable() => LevelGameManager.OnGameStateChanged -= HandleStateChanged;

    private void HandleStateChanged(LevelGameManager.GameState state)
    {
        if (state != LevelGameManager.GameState.Lost) return;
        if (!sequence) return;

        if (HomeManager.CurrentStage1Based != stage1Based) return;

        if (requireNeverCleared && SaveSystem.GetStars(levelId, stage1Based - 1) != 0) return;

        bool alreadySeen = TutorialManager.IsTutorialDone(sequence.TutorialId);
#if UNITY_EDITOR
        if (forceReplayInEditor) alreadySeen = false;
#endif
        if (alreadySeen) return;

        if (TutorialManager.Get().IsPlaying) return;

        if (requireUpgradeAffordable && !CanStartUpgradeChain())
        {
            // Deliberately writes NO flag: the lesson stays pending and can fire on
            // a later defeat, or from whatever moment we decide to move it to.
            Debug.Log("[Tutorial] Skipping the upgrade onboarding - no deployed hero is " +
                      "affordable to upgrade right now. Nothing was marked as seen.");
            return;
        }

        StartCoroutine(PlayAfterPanel());
    }

    /// <summary>
    /// THE gate for "is this a good moment to teach upgrading". Kept as one named
    /// method on purpose: moving the onboarding to a different trigger point later
    /// should be a change here and nowhere else.
    /// </summary>
    private bool CanStartUpgradeChain()
    {
        var gsm = GameStartManager.Instance;
        var progression = gsm ? gsm.ProgressionService : null;
        if (progression == null) return false;

        var units = FindObjectOfType<UnitsPanelController>(true);
        var container = units ? units.DeployedContainer : null;

        // The roster panel is not in this scene, so fall back to asking the model
        // about every unit the player owns.
        if (!container) return AnyOwnedUnitUpgradable(progression);

        for (int i = 0; i < container.childCount; i++)
        {
            var card = container.GetChild(i).GetComponent<UnitCardView>();
            if (card && progression.CanUpgrade(card.UnitId, out _)) return true;
        }

        return AnyOwnedUnitUpgradable(progression);
    }

    private static bool AnyOwnedUnitUpgradable(PlayerProgressionService progression)
    {
        var gsm = GameStartManager.Instance;
        var db = gsm ? gsm.unitsDatabase : null;
        if (!db || db.Units == null) return false;

        foreach (var def in db.Units)
        {
            if (def == null) continue;
            if (progression.CanUpgrade(def.unitId, out _)) return true;
        }

        return false;
    }

    private IEnumerator PlayAfterPanel()
    {
        // WaitForSecondsRealtime, not WaitForSeconds: ShowLosePanel has already set
        // Time.timeScale to 0, so a scaled wait would never finish.
        if (panelSettleDelay > 0f) yield return new WaitForSecondsRealtime(panelSettleDelay);

        if (!overlay) overlay = TutorialOverlay.FindInScene();

        TutorialManager.Get().Play(sequence, overlay);
    }
}
