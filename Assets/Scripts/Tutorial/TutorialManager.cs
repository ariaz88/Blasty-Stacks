using System;
using UnityEngine;

/// <summary>
/// The entry point of the tutorial system, and the owner of the "player has
/// already seen this one" flags.
///
/// Singleton, DontDestroyOnLoad, like LevelManager / GameStartManager /
/// CurrencyManager. It differs from them in ONE way on purpose: Get() creates
/// the manager if no scene placed one, so opening a tutorial scene directly in
/// the Editor works without a boot scene.
///
/// The completed flags go through SaveSystem (never PlayerPrefs directly), so
/// they ride the same GAME_SAVE_V1 blob as everything else and are wiped by the
/// same reset paths.
/// </summary>
[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>Raised with the tutorialId when a tutorial starts / finishes.</summary>
    public static event Action<string> OnTutorialStarted;
    public static event Action<string> OnTutorialFinished;

    private TutorialRunner _runner;

    public bool IsPlaying { get; private set; }
    public string PlayingId { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// The manager, creating one if the scene has none. Cheap to call.
    /// </summary>
    public static TutorialManager Get()
    {
        if (Instance) return Instance;

        var existing = FindObjectOfType<TutorialManager>();
        if (existing) return Instance ? Instance : existing;

        var go = new GameObject("TutorialManager (auto)");
        go.AddComponent<TutorialManager>();   // Awake claims Instance
        return Instance;
    }

    // ------------------------------------------------------------------
    //  Progress flags - static so callers (e.g. MenuLoader at boot) can ask
    //  without spawning the manager.
    // ------------------------------------------------------------------

    public static bool IsTutorialDone(string tutorialId)
        => SaveSystem.IsTutorialCompleted(tutorialId);

    public static void MarkTutorialDone(string tutorialId)
        => SaveSystem.SetTutorialCompleted(tutorialId, true);

    /// <summary>Forgets every tutorial. Used by Tools/Tutorial/Reset Tutorial Progress.</summary>
    public static void ResetAllProgress()
        => SaveSystem.ResetTutorials();

    // ------------------------------------------------------------------
    //  Playing
    // ------------------------------------------------------------------

    /// <summary>
    /// Starts a tutorial. Returns false when it could not start (already
    /// playing, no sequence, or no TutorialOverlay in the scene).
    /// onFinished runs after the last step, whether or not playOnce is set.
    /// </summary>
    public bool Play(TutorialSequenceSO sequence, TutorialOverlay overlay = null, Action onFinished = null)
    {
        if (!sequence)
        {
            Debug.LogWarning("[Tutorial] Play called with no sequence.");
            return false;
        }

        if (IsPlaying)
        {
            Debug.LogWarning($"[Tutorial] '{PlayingId}' is already playing - refusing to start '{sequence.TutorialId}'.");
            return false;
        }

        if (!overlay) overlay = TutorialOverlay.FindInScene();
        if (!overlay)
        {
            Debug.LogWarning($"[Tutorial] No TutorialOverlay in the scene - '{sequence.TutorialId}' cannot be shown. " +
                             "Drag Assets/PREFABS/Tutorial/TutorialOverlay.prefab into the scene.");
            return false;
        }

        _runner = overlay.GetComponent<TutorialRunner>();
        if (!_runner) _runner = overlay.gameObject.AddComponent<TutorialRunner>();
        _runner.Bind(overlay);

        IsPlaying = true;
        PlayingId = sequence.TutorialId;

        try { OnTutorialStarted?.Invoke(PlayingId); }
        catch (Exception e) { Debug.LogException(e, this); }

        _runner.Run(sequence, () => Finish(sequence, onFinished));
        return true;
    }

    /// <summary>Cuts the running tutorial short. Does NOT mark it as seen.</summary>
    public void StopCurrent()
    {
        if (!IsPlaying) return;

        if (_runner) _runner.Stop();

        IsPlaying = false;
        PlayingId = null;
    }

    private void Finish(TutorialSequenceSO sequence, Action onFinished)
    {
        IsPlaying = false;
        PlayingId = null;

        if (sequence.playOnce) MarkTutorialDone(sequence.TutorialId);

        try { OnTutorialFinished?.Invoke(sequence.TutorialId); }
        catch (Exception e) { Debug.LogException(e, this); }

        try { onFinished?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }
}
