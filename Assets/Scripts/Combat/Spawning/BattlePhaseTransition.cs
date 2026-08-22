// BattlePhaseTransition.cs
using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// The visual hand-off from the puzzle phase to the battle phase.
///
///   BEFORE BATTLE : the camera frames the board + the player gate, and the
///                   Feature Panel is on screen.
///   AFTER  BATTLE : the camera and the background slide UP together, the
///                   puzzle board is switched off entirely, and combat begins.
///
/// The move is a RELATIVE OFFSET applied to an explicit list of transforms -
/// Main Camera and BackGroundImage - so the enemy gate comes into view without
/// any other object in the scene being repositioned.
/// </summary>
public class BattlePhaseTransition : MonoBehaviour
{
    [Header("Move Up On Battle")]
    [Tooltip("The transforms that slide up: Main Camera and BackGroundImage. " +
             "NOTHING else in the scene is touched.")]
    [SerializeField] private Transform[] moveUpTargets;

    [Tooltip("How far up they travel, in world units.")]
    [SerializeField] private float moveUpDistance = 6.75f;

    [Header("Motion")]
    [Tooltip("Seconds for the move. 0 = snap instantly.")]
    [SerializeField, Min(0f)] private float duration = 1.1f;
    [SerializeField] private Ease ease = Ease.InOutCubic;

    [Header("Objects")]
    [Tooltip("Switched OFF for the battle - the Puzzle Board and the Feature Panel.")]
    [SerializeField] private GameObject[] hideWhenBattleStarts;

    [Tooltip("Switched ON for the battle - the combat HUD, if you have one. " +
             "Instant. For anything that should FADE in, use fadeInAfterMove below.")]
    [SerializeField] private GameObject[] showWhenBattleStarts;

    [Header("Fades")]
    [Tooltip("UI that fades out QUICKLY the instant BATTLE is pressed - the Feature Panel. " +
             "Put it HERE instead of in hideWhenBattleStarts, which only fires once the " +
             "camera has already arrived. A CanvasGroup is added automatically if missing.")]
    [SerializeField] private GameObject[] fadeOutOnPress;

    [Tooltip("Seconds for that fade. It runs alongside the camera move, so keep it short.")]
    [SerializeField, Min(0f)] private float pressFadeOutDuration = 0.2f;

    [Tooltip("UI that fades IN only after the camera has finished its whole move - the " +
             "Heroes Stats panel. These are activated by this script, so leave them " +
             "switched OFF in the scene.")]
    [SerializeField] private GameObject[] fadeInAfterMove;

    [Tooltip("Seconds for that fade-in.")]
    [SerializeField, Min(0f)] private float arriveFadeInDuration = 0.45f;

    [Header("Timing")]
    [Tooltip("ON  = hide the board only after the move finishes, by which point " +
             "it is already off screen, so the player never sees it pop out.\n" +
             "OFF = hide it the instant BATTLE is pressed.")]
    [SerializeField] private bool hideAfterCameraArrives = true;

    /// <summary>True while the move is running.</summary>
    public bool IsPlaying { get; private set; }

    private Sequence sequence;

    private void OnDestroy()
    {
        sequence?.Kill();
    }

    /// <summary>
    /// Runs the transition. <paramref name="onComplete"/> fires once the move has
    /// settled - the caller uses that to release the enemy spawner, so waves only
    /// start when the player is actually looking at the battlefield.
    /// </summary>
    public void Play(Action onComplete)
    {
        if (IsPlaying) return;

        SetActiveAll(showWhenBattleStarts, true);

        // Straight away, before the camera has moved a pixel: the Feature Panel
        // must be gone almost immediately, not linger for the whole pan.
        FadeOutAndDisable(fadeOutOnPress, pressFadeOutDuration);

        if (!hideAfterCameraArrives)
            SetActiveAll(hideWhenBattleStarts, false);

        int moved = CountValidTargets();
        if (moved == 0)
        {
            Debug.LogWarning("[BattlePhaseTransition] No moveUpTargets assigned - " +
                             "nothing will slide up.", this);
        }

        if (moved == 0 || duration <= 0f || Mathf.Approximately(moveUpDistance, 0f))
        {
            ApplyEndStateInstantly();
            Finish(onComplete);
            return;
        }

        IsPlaying = true;

        sequence = DOTween.Sequence();
        foreach (var t in moveUpTargets)
        {
            if (!t) continue;
            sequence.Join(t.DOMoveY(t.position.y + moveUpDistance, duration).SetEase(ease));
        }

        sequence.OnComplete(() => Finish(onComplete));
    }

    private void ApplyEndStateInstantly()
    {
        if (moveUpTargets == null) return;

        foreach (var t in moveUpTargets)
        {
            if (!t) continue;
            t.position += Vector3.up * moveUpDistance;
        }
    }

    private int CountValidTargets()
    {
        if (moveUpTargets == null) return 0;

        int n = 0;
        foreach (var t in moveUpTargets)
            if (t) n++;

        return n;
    }

    private void Finish(Action onComplete)
    {
        IsPlaying = false;

        if (hideAfterCameraArrives)
            SetActiveAll(hideWhenBattleStarts, false);

        // The battle HUD arrives only now, with the camera already settled on the
        // battlefield - never mid-pan, and never before the board is gone.
        FadeInAndEnable(fadeInAfterMove, arriveFadeInDuration);

        onComplete?.Invoke();
    }

    private static void SetActiveAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (var go in objects)
        {
            if (go) go.SetActive(active);
        }
    }

    /// <summary>
    /// A CanvasGroup on the object, created on demand. Works on an INACTIVE
    /// GameObject, which is what lets FadeInAndEnable set alpha to 0 before the
    /// object is ever switched on.
    /// </summary>
    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        if (!group) group = go.AddComponent<CanvasGroup>();
        return group;
    }

    private static void FadeOutAndDisable(GameObject[] objects, float duration)
    {
        if (objects == null) return;

        foreach (var go in objects)
        {
            if (!go || !go.activeSelf) continue;

            var group = EnsureCanvasGroup(go);
            group.DOKill();

            // Stop taking clicks the moment the fade starts - a half-faded
            // BATTLE button is still a live button otherwise.
            group.interactable = false;
            group.blocksRaycasts = false;

            if (duration <= 0f)
            {
                group.alpha = 0f;
                go.SetActive(false);
                continue;
            }

            var target = go;
            group.DOFade(0f, duration)
                 .SetEase(Ease.Linear)
                 .OnComplete(() => { if (target) target.SetActive(false); });
        }
    }

    private static void FadeInAndEnable(GameObject[] objects, float duration)
    {
        if (objects == null) return;

        foreach (var go in objects)
        {
            if (!go) continue;

            var group = EnsureCanvasGroup(go);
            group.DOKill();

            // Alpha BEFORE SetActive, so the panel never flashes at full opacity
            // for one frame before the tween's first update.
            group.alpha = 0f;
            go.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;

            if (duration <= 0f)
            {
                group.alpha = 1f;
                continue;
            }

            group.DOFade(1f, duration).SetEase(Ease.OutQuad);
        }
    }
}
