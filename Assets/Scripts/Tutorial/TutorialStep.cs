using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// One beat of a tutorial. Steps are plain [Serializable] classes stored in a
/// TutorialSequenceSO through [SerializeReference], so ONE asset holds a whole
/// tutorial instead of one asset per step, and adding a new kind of beat means
/// adding a class here - nothing else in the system changes.
///
/// A step is a coroutine: it sets up the overlay, waits for its condition, and
/// tidies up. TutorialRunner just walks the list.
/// </summary>
[Serializable]
public abstract class TutorialStep
{
    [Tooltip("Free-text label so the step list is readable in the Inspector. Not used at runtime.")]
    public string note;

    public abstract IEnumerator Run(TutorialRunner runner);
}

/// <summary>Shows a line of text and waits. No hand.</summary>
[Serializable]
public class TutorialCaptionStep : TutorialStep
{
    [TextArea(1, 3)] public string text = "";

    [Tooltip("Clear the caption when this step ends. Leave off to carry the text into the next step.")]
    public bool hideCaptionWhenDone = false;

    public TutorialCondition until = TutorialCondition.ForDuration(1.5f);

    public override IEnumerator Run(TutorialRunner runner)
    {
        runner.ShowCaption(text);

        yield return runner.WaitFor(until);

        if (hideCaptionWhenDone) runner.HideCaption();
    }
}

/// <summary>
/// The core beat of the first tutorial: loop a press-drag-release gesture from
/// one place to another until the player does it themselves.
/// </summary>
[Serializable]
public class TutorialHandDragStep : TutorialStep
{
    [TextArea(1, 3)]
    [Tooltip("Caption shown while the hand loops. Leave empty to keep whatever is on screen.")]
    public string caption = "";

    [Tooltip("Where the gesture starts - the piece the player should grab.")]
    public TutorialTarget from;

    [Tooltip("Where the gesture ends - where that piece should end up.")]
    public TutorialTarget to;

    public TutorialHandLoopTimings timings = TutorialHandLoopTimings.Default;

    [Tooltip("What ends the step. For the board lesson this is MatchBlasted.")]
    public TutorialCondition until = TutorialCondition.ForMatch(1);

    public bool hideCaptionWhenDone = false;

    public override IEnumerator Run(TutorialRunner runner)
    {
        if (!string.IsNullOrEmpty(caption)) runner.ShowCaption(caption);

        // resolved per cycle, not once, so a target that moves stays pointed at
        runner.Hand.StartDragLoop(() =>
        {
            if (!runner.TryResolve(from, out var a) || !runner.TryResolve(to, out var b))
                return TutorialDragPoints.None;

            return TutorialDragPoints.At(a, b);
        }, timings);

        yield return runner.WaitFor(until);

        runner.Hand.StopAndHide();
        if (hideCaptionWhenDone) runner.HideCaption();
    }
}

/// <summary>Loops a tap-in-place gesture. For "press this button" tutorials.</summary>
[Serializable]
public class TutorialHandTapStep : TutorialStep
{
    [TextArea(1, 3)] public string caption = "";

    public TutorialTarget at;

    public TutorialHandLoopTimings timings = TutorialHandLoopTimings.Default;

    public TutorialCondition until = new TutorialCondition { kind = TutorialCondition.Kind.TapAnywhere };

    public bool hideCaptionWhenDone = false;

    public override IEnumerator Run(TutorialRunner runner)
    {
        if (!string.IsNullOrEmpty(caption)) runner.ShowCaption(caption);

        runner.Hand.StartTapLoop(() =>
        {
            if (!runner.TryResolve(at, out var p)) return TutorialDragPoints.None;
            return TutorialDragPoints.At(p, p);
        }, timings);

        yield return runner.WaitFor(until);

        runner.Hand.StopAndHide();
        if (hideCaptionWhenDone) runner.HideCaption();
    }
}

/// <summary>
/// Teaches matching by pointing at whatever match the board currently offers,
/// instead of at hard-coded cells. Each hand cycle re-asks the board, so:
///   - drag the hinted stack somewhere else and the hint re-aims from its NEW
///     position to a spot that still makes the match;
///   - blast a pair and the hint moves on to the NEXT matchable pair, all the
///     way to an empty board, without authoring a step per pair.
///
/// The hinted move is always one the player could actually perform - see
/// TutorialBoardHints, which walks the drag cell by cell the way the board does.
/// </summary>
[Serializable]
public class TutorialMatchGuideStep : TutorialStep
{
    [TextArea(1, 3)] public string caption = "";

    public TutorialHandLoopTimings timings = TutorialHandLoopTimings.Default;

    [Tooltip("What ends the step. BoardEmpty walks the player through every pair; " +
             "MatchBlasted with count 1 stops after the first match.")]
    public TutorialCondition until = new TutorialCondition { kind = TutorialCondition.Kind.BoardEmpty };

    public bool hideCaptionWhenDone = true;

    [Header("Ordering")]
    [Tooltip("Teach the pair containing the stack sitting on THIS cell first. " +
             "Without it the shortest available drag wins, which is not always the " +
             "clearest first lesson. Leave at (-1,-1) for automatic.")]
    public Vector2Int firstPairAtCell = new Vector2Int(-1, -1);

    public override IEnumerator Run(TutorialRunner runner)
    {
        if (!string.IsNullOrEmpty(caption)) runner.ShowCaption(caption);

        // resolved once, at the start, while the board is still untouched
        PieceSimple preferred = null;
        if (firstPairAtCell.x >= 0 && firstPairAtCell.y >= 0)
        {
            preferred = TutorialBoardHints.PieceAtCell(runner.Board, firstPairAtCell);
            if (!preferred)
                Debug.LogWarning($"[Tutorial] Guide step '{note}' found no stack on cell {firstPairAtCell}.");
        }

        // The stack currently being demonstrated. Held across cycles so the hand
        // keeps dragging the SAME stack (from wherever the player left it) instead
        // of flipping to whichever direction happens to be shortest this frame.
        PieceSimple sticky = null;

        runner.Hand.StartDragLoop(() =>
        {
            var points = runner.ResolveMatchHint(preferred, sticky, out var chosen);
            sticky = points.valid ? chosen : null;   // that pair is gone - let it re-pick
            return points;
        }, timings);

        yield return runner.WaitFor(until);

        runner.Hand.StopAndHide();
        if (hideCaptionWhenDone) runner.HideCaption();
    }
}

/// <summary>Dead time. Useful for letting a blast animation finish before the next beat.</summary>
[Serializable]
public class TutorialWaitStep : TutorialStep
{
    public float seconds = 0.5f;

    public override IEnumerator Run(TutorialRunner runner)
    {
        yield return runner.WaitFor(TutorialCondition.ForDuration(seconds));
    }
}
