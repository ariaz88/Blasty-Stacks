using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The scene-side hook that starts a tutorial. This is the piece that makes the
/// system reusable: every future tutorial is "make a TutorialSequenceSO, drop
/// the overlay prefab in the scene, add one of these" - no new code.
///
/// In the first tutorial's scene (Tutorial_Board_01) it also owns what happens
/// AFTER: loadSceneOnComplete sends the player on to MenuScene.
///
/// Already-seen behaviour: if the sequence is playOnce and its flag is set, the
/// tutorial is skipped BUT loadSceneOnComplete still runs - otherwise a player
/// who somehow lands back on the tutorial scene would be stranded there.
/// </summary>
[DisallowMultipleComponent]
public class TutorialTrigger : MonoBehaviour
{
    [Header("What to play")]
    [SerializeField] private TutorialSequenceSO sequence;

    [Tooltip("Overlay to draw through. Left empty, the one in the scene is found.")]
    [SerializeField] private TutorialOverlay overlay;

    [Header("When")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Only play once THIS tutorial id is already completed. That is how a " +
             "chain crosses a scene load: the runner dies with its scene, but the " +
             "save flag does not. Empty = no prerequisite.")]
    [SerializeField] private string requiresTutorialId = "";

    [Tooltip("Delay before starting, so the scene has settled (BoardBootstrapper " +
             "places the pieces in Start, and the hand must point at where they ended up).")]
    [SerializeField] private float startDelay = 0.4f;

    [Header("When the tutorial finishes")]
    [Tooltip("Scene to load once the tutorial ends. Empty = stay in this scene.")]
    [SerializeField] private string loadSceneOnComplete = "";

    [Tooltip("Realtime seconds between the tutorial ending and the switch to loadSceneOnComplete.")]
    [SerializeField] private float loadSceneDelay = 0.6f;

    [Tooltip("Read loadSceneOnComplete in the BACKGROUND while the tutorial plays, so the end " +
             "of the tutorial is a quick switch instead of a blocking load (MenuScene froze the " +
             "game for 1-5 s). Only for a scene that leaves ONLY through this trigger: a held " +
             "background load holds up every other scene load until it is let in.")]
    [SerializeField] private bool preloadSceneOnComplete = true;

    [Header("Testing")]
    [Tooltip("EDITOR ONLY: play the tutorial even if it is already marked as seen.")]
    [SerializeField] private bool forceReplayInEditor = false;

    // loadSceneOnComplete, loading in the background and held just short of
    // activation until HandleComplete lets it in. Null when not preloading.
    private AsyncOperation _preload;

    private IEnumerator Start()
    {
        if (!playOnStart) yield break;

        // Shut the screen for the length of startDelay when this tutorial is
        // actually going to play. Without it the delay is an unguarded window in
        // which the player can press anything - long enough, at 0.9s on the menu,
        // to start a battle before the first beat has even begun.
        bool willPlay = WillPlay();
        if (willPlay) BlockDuringDelay(true);

        // Start reading the next scene NOW, so the player spends the tutorial waiting
        // on the load instead of a frozen screen after it.
        BeginPreload();

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        Play();

        // The first step re-asserts the block itself, so handing over is seamless -
        // but if the tutorial did not actually start we must not leave the player
        // locked out.
        if (willPlay && !TutorialManager.Get().IsPlaying) BlockDuringDelay(false);
    }

    /// <summary>Same gating questions Play() asks, without the side effects.</summary>
    private bool WillPlay()
    {
        if (!sequence) return false;

        if (!string.IsNullOrEmpty(requiresTutorialId) &&
            !TutorialManager.IsTutorialDone(requiresTutorialId))
            return false;

        bool alreadySeen = sequence.playOnce && TutorialManager.IsTutorialDone(sequence.TutorialId);
#if UNITY_EDITOR
        if (forceReplayInEditor) alreadySeen = false;
#endif
        return !alreadySeen;
    }

    private void BlockDuringDelay(bool block)
    {
        var target = overlay ? overlay : TutorialOverlay.FindInScene();
        if (target) target.SetBlockInput(block);
    }

    /// <summary>Starts the tutorial (or skips straight to the follow-up scene).</summary>
    public void Play()
    {
        if (!sequence)
        {
            Debug.LogWarning("[Tutorial] TutorialTrigger has no sequence assigned.", this);
            return;
        }

        // Not armed yet. Return outright rather than going through HandleComplete:
        // this is "not my turn", not "finished", so no follow-up scene must load.
        if (!string.IsNullOrEmpty(requiresTutorialId) &&
            !TutorialManager.IsTutorialDone(requiresTutorialId))
        {
            return;
        }

        bool alreadySeen = sequence.playOnce && TutorialManager.IsTutorialDone(sequence.TutorialId);

#if UNITY_EDITOR
        if (forceReplayInEditor) alreadySeen = false;
#endif

        if (alreadySeen)
        {
            Debug.Log($"[Tutorial] '{sequence.TutorialId}' already seen - skipping.");
            HandleComplete();
            return;
        }

        if (!TutorialManager.Get().Play(sequence, overlay, HandleComplete))
            HandleComplete();   // could not start - do not strand the player here
    }

    private void HandleComplete()
    {
        if (string.IsNullOrEmpty(loadSceneOnComplete)) return;

        StartCoroutine(LoadAfterDelay());
    }

    /// <summary>
    /// Starts loading loadSceneOnComplete in the background, held just short of
    /// activation. Only when this visit is certain to end in that scene: a held load
    /// that is never let in would hold up every other scene load for good.
    /// </summary>
    private void BeginPreload()
    {
        if (!preloadSceneOnComplete || _preload != null || string.IsNullOrEmpty(loadSceneOnComplete)) return;

        // "Not my turn yet" never routes onwards, so it must not preload either.
        if (!string.IsNullOrEmpty(requiresTutorialId) &&
            !TutorialManager.IsTutorialDone(requiresTutorialId))
            return;

        _preload = SceneManager.LoadSceneAsync(loadSceneOnComplete);
        if (_preload != null) _preload.allowSceneActivation = false;
    }

    private IEnumerator LoadAfterDelay()
    {
        if (loadSceneDelay > 0f) yield return new WaitForSecondsRealtime(loadSceneDelay);

        // Already read in the background - letting it in is only the switch.
        if (_preload != null)
        {
            _preload.allowSceneActivation = true;
            yield break;
        }

        SceneManager.LoadScene(loadSceneOnComplete);
    }
}
