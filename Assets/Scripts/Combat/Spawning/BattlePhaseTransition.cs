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

    [Tooltip("Switched ON for the battle - the combat HUD, if you have one.")]
    [SerializeField] private GameObject[] showWhenBattleStarts;

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
}
