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
///   UnitUpgraded  - a hero actually levelled up. Rides the existing
///                   PlayerProgressionService.OnUnitUpgraded, so it is true only
///                   when the game ACCEPTED the upgrade - a tap the game refuses
///                   (not enough coins) cannot advance the tutorial.
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
        UnitUpgraded = 6,
    }

    public Kind kind;

    [Tooltip("Duration only: seconds to wait (unscaled).")]
    public float seconds;

    [Tooltip("MatchBlasted only: how many blasts to wait for. 0 counts as 1.")]
    public int requiredCount;

    [Tooltip("AnchorClicked only: anchorId of a TutorialAnchor sitting on a Button.")]
    public string anchorId;

    [Tooltip("UnitUpgraded only: which hero must level up. 0 or less = any hero.")]
    public int unitId;

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
    private PlayerProgressionService _hookedProgression;
    private bool _upgraded;
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
        _upgraded = false;
        IsSatisfied = false;
        _active = true;

        switch (_condition.kind)
        {
            case TutorialCondition.Kind.MatchBlasted:
                MatchResolver.OnBlast += OnBlast;
                break;

            case TutorialCondition.Kind.AnchorClicked:
                HookAnchorButton();
                break;

            case TutorialCondition.Kind.UnitUpgraded:
                HookProgression();
                break;
        }
    }

    /// <summary>
    /// Subscribes to the live anchor's Button.
    ///
    /// ResolveButton, not GetComponent: a hero card carries its Button on a CHILD
    /// literally named "Button", so looking only at the anchored object would find
    /// nothing and the step could never complete.
    /// </summary>
    private void HookAnchorButton()
    {
        var anchor = TutorialAnchor.Find(_condition.anchorId);
        var button = anchor ? anchor.ResolveButton() : null;

        // let go of a button that has been swapped out from under us
        if (_hookedButton && _hookedButton != button)
            _hookedButton.onClick.RemoveListener(OnAnchorClicked);

        _hookedButton = button;
        if (!_hookedButton) return;

        // RE-ADD EVERY TICK, not just when the button object changes.
        //
        // UnitsPanelController.WireBackButton and WireUpgradeButton both call
        // onClick.RemoveAllListeners(), which drops OUR subscription along with
        // their own - and they re-run on every currency change and after every
        // upgrade. Checking "is it still the same Button component" is not enough,
        // because the component survives while its listener list is emptied.
        //
        // Symptom when this was missing: after upgrading a hero, the Back step's
        // click was never seen, so the "Go back" tooltip stayed on screen forever
        // and the sequence never finished.
        //
        // Remove-then-Add keeps exactly one subscription; UnityEvent would
        // otherwise stack a duplicate every frame.
        _hookedButton.onClick.RemoveListener(OnAnchorClicked);
        _hookedButton.onClick.AddListener(OnAnchorClicked);
    }

    private void HookProgression()
    {
        var service = GameStartManager.Instance ? GameStartManager.Instance.ProgressionService : null;
        if (service == _hookedProgression) return;

        if (_hookedProgression != null) _hookedProgression.OnUnitUpgraded -= OnUnitUpgraded;

        _hookedProgression = service;
        if (_hookedProgression != null) _hookedProgression.OnUnitUpgraded += OnUnitUpgraded;
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
                // Re-hook every frame, because the button the anchor stands for can
                // be SWAPPED under us: UnitsButton_InActive -> _Selected, and
                // UpgradeButton -> UpgradeButton_DISABLED. HookAnchorButton is a
                // no-op when nothing changed.
                HookAnchorButton();
                if (_clicked) IsSatisfied = true;
                break;

            case TutorialCondition.Kind.UnitUpgraded:
                // The service is built by GameStartManager during boot, so it may
                // not exist yet on the first frames of a directly-opened scene.
                HookProgression();
                if (_upgraded) IsSatisfied = true;
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

        if (_hookedProgression != null)
        {
            _hookedProgression.OnUnitUpgraded -= OnUnitUpgraded;
            _hookedProgression = null;
        }
    }

    private void OnBlast(int groups) => _blastCount++;

    private void OnAnchorClicked() => _clicked = true;

    private void OnUnitUpgraded(int unitId, int oldLevel, int newLevel, int cost)
    {
        if (_condition.unitId > 0 && _condition.unitId != unitId) return;
        _upgraded = true;
    }

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
