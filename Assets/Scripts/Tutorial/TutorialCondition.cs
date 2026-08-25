using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WHEN a tutorial step is finished. Together with TutorialTarget this is what
/// makes the step list data instead of code: a step says "point here, until
/// that happens".
///
/// Kinds:
///   Duration      - after `seconds`.
///   TapAnywhere   - the player touches/clicks anywhere.
///   MatchBlasted  - the board blasted a group. Rides the EXISTING static hook
///                   MatchResolver.OnBlast, so nothing in the puzzle code had
///                   to change to support this.
///   BoardEmpty    - no occupied cells left on the BoardGridXY.
///   AnchorClicked - a UI Button carrying a TutorialAnchor was pressed.
///   Never         - never satisfied on its own; the sequence must be stopped
///                   from outside. Useful while authoring.
/// </summary>
[Serializable]
public struct TutorialCondition
{
    public enum Kind
    {
        Duration = 0,
        TapAnywhere = 1,
        MatchBlasted = 2,
        BoardEmpty = 3,
        AnchorClicked = 4,
        Never = 5,
    }

    public Kind kind;

    [Tooltip("Duration only: seconds to wait (unscaled).")]
    public float seconds;

    [Tooltip("MatchBlasted only: how many blasts to wait for. 0 counts as 1.")]
    public int requiredCount;

    [Tooltip("AnchorClicked only: anchorId of a TutorialAnchor sitting on a Button.")]
    public string anchorId;

    public static TutorialCondition ForDuration(float s)
        => new TutorialCondition { kind = Kind.Duration, seconds = s };

    public static TutorialCondition ForMatch(int count = 1)
        => new TutorialCondition { kind = Kind.MatchBlasted, requiredCount = count };
}

/// <summary>
/// Runtime side of TutorialCondition. Begin() subscribes, Tick() polls, End()
/// unsubscribes - End MUST run or the static MatchResolver.OnBlast keeps a dead
/// delegate alive across scene loads. TutorialRunner.WaitFor guards it with
/// try/finally for exactly that reason.
/// </summary>
public class TutorialConditionWatcher
{
    private TutorialCondition _condition;
    private BoardGridXY _board;

    private float _elapsed;
    private int _blastCount;
    private bool _clicked;
    private Button _hookedButton;
    private bool _active;

    public bool IsSatisfied { get; private set; }

    public void Begin(TutorialCondition condition, BoardGridXY board)
    {
        End(); // never stack two subscriptions

        _condition = condition;
        _board = board;
        _elapsed = 0f;
        _blastCount = 0;
        _clicked = false;
        IsSatisfied = false;
        _active = true;

        switch (_condition.kind)
        {
            case TutorialCondition.Kind.MatchBlasted:
                MatchResolver.OnBlast += OnBlast;
                break;

            case TutorialCondition.Kind.AnchorClicked:
            {
                var anchor = TutorialAnchor.Find(_condition.anchorId);
                _hookedButton = anchor ? anchor.GetComponent<Button>() : null;
                if (_hookedButton) _hookedButton.onClick.AddListener(OnAnchorClicked);
                else Debug.LogWarning($"[Tutorial] AnchorClicked condition found no Button on anchor '{_condition.anchorId}'.");
                break;
            }
        }
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (!_active || IsSatisfied) return;

        _elapsed += unscaledDeltaTime;

        switch (_condition.kind)
        {
            case TutorialCondition.Kind.Duration:
                if (_elapsed >= _condition.seconds) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.TapAnywhere:
                if (WasTappedThisFrame()) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.MatchBlasted:
                if (_blastCount >= Mathf.Max(1, _condition.requiredCount)) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.BoardEmpty:
                if (_board && !_board.HasAnyOccupiedCells()) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.AnchorClicked:
                if (_clicked) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.Never:
                break;
        }
    }

    public void End()
    {
        if (!_active) return;
        _active = false;

        MatchResolver.OnBlast -= OnBlast;

        if (_hookedButton)
        {
            _hookedButton.onClick.RemoveListener(OnAnchorClicked);
            _hookedButton = null;
        }
    }

    private void OnBlast(int groups) => _blastCount++;

    private void OnAnchorClicked() => _clicked = true;

    // The project ships with activeInputHandler = Both, and the board itself
    // reads legacy Input (BoardInputController), so this stays on legacy Input
    // to behave identically to the rest of the game.
    private static bool WasTappedThisFrame()
    {
        if (Input.GetMouseButtonDown(0)) return true;

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) return true;
        }
        return false;
    }
}
