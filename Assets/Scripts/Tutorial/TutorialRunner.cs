using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Walks the step list of a TutorialSequenceSO and gives each step the services
/// it needs: target resolving, the hand, the caption, and condition waiting.
///
/// Lives on the TutorialOverlay object (the manager adds it if it is missing),
/// because the overlay is the scene object the tutorial draws through - if the
/// scene goes away, so does the running tutorial, which is what we want.
/// </summary>
[DisallowMultipleComponent]
public class TutorialRunner : MonoBehaviour
{
    private TutorialOverlay _overlay;
    private Camera _worldCamera;
    private BoardGridXY _board;
    private Coroutine _sequence;

    public bool IsRunning => _sequence != null;

    public TutorialOverlay Overlay => _overlay;
    public TutorialHand Hand => _overlay ? _overlay.Hand : null;

    /// <summary>The camera board targets are projected through. Camera.main by default.</summary>
    public Camera WorldCamera
    {
        get
        {
            if (!_worldCamera) _worldCamera = Camera.main;
            return _worldCamera;
        }
    }

    /// <summary>The puzzle board, looked up lazily. Null in scenes that have none.</summary>
    public BoardGridXY Board
    {
        get
        {
            if (!_board) _board = FindObjectOfType<BoardGridXY>();
            return _board;
        }
    }

    public void Bind(TutorialOverlay overlay)
    {
        _overlay = overlay;
        if (_overlay) _overlay.ClearAll();
    }

    /// <summary>Runs a sequence to the end, then calls onFinished.</summary>
    public void Run(TutorialSequenceSO sequence, Action onFinished)
    {
        Stop();
        _sequence = StartCoroutine(RunSequence(sequence, onFinished));
    }

    /// <summary>Cuts a running tutorial short. Does NOT call onFinished.</summary>
    public void Stop()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        if (_overlay) _overlay.ClearAll();
    }

    private IEnumerator RunSequence(TutorialSequenceSO sequence, Action onFinished)
    {
        if (!sequence || !sequence.HasSteps)
        {
            Debug.LogWarning("[Tutorial] Sequence is empty - nothing to play.");
        }
        else
        {
            for (int i = 0; i < sequence.steps.Count; i++)
            {
                var step = sequence.steps[i];
                if (step == null) continue;   // an empty row in the SerializeReference list

                yield return step.Run(this);
            }
        }

        if (_overlay) _overlay.ClearAll();
        _sequence = null;

        onFinished?.Invoke();
    }

    // ------------------------------------------------------------------
    //  Services the steps call
    // ------------------------------------------------------------------

    public bool TryResolve(TutorialTarget target, out Vector2 screenPos)
    {
        return target.TryResolveScreen(WorldCamera, Board, out screenPos);
    }

    /// <summary>
    /// The gesture to demonstrate RIGHT NOW, worked out from the live board:
    /// which stack to drag (from wherever it currently sits) and where to drop it
    /// so it blasts. Returns an invalid result when no match is available, which
    /// makes the hand hide until one is.
    ///
    /// Called once per hand cycle, so the hint follows the player: move the
    /// hinted stack elsewhere and the next cycle starts from its new home; blast
    /// a pair and the next cycle points at the next pair.
    /// </summary>
    public TutorialDragPoints ResolveMatchHint() => ResolveMatchHint(null, null, out _);

    /// <summary>
    /// As above, with control over which stack is demonstrated:
    ///   preferred - teach the pair containing this stack first;
    ///   sticky    - keep demonstrating THIS stack as the one that moves, so the
    ///               gesture never reverses direction under the player.
    /// `chosenMover` comes back so the caller can feed it in as `sticky` next time.
    /// </summary>
    public TutorialDragPoints ResolveMatchHint(PieceSimple preferred, PieceSimple sticky, out PieceSimple chosenMover)
    {
        chosenMover = null;

        var board = Board;
        var cam = WorldCamera;
        if (!board || !cam) return TutorialDragPoints.None;

        if (!TutorialBoardHints.TryFindMatchHint(board, preferred, sticky, out var mover, out var target, out _))
            return TutorialDragPoints.None;

        chosenMover = mover;

        // Start on the middle of the stack to grab, END ON THE MIDDLE OF THE STACK
        // IT SHOULD MEET - not on the empty cell beside it. Aiming at the landing
        // cell made the hand stop short of the second stack, and kept the path flat
        // whenever the two stacks sat at different heights.
        Vector3 fromWorld = TutorialBoardHints.PieceCenterWorld(board, mover, mover.Anchor);
        Vector3 toWorld = TutorialBoardHints.PieceCenterWorld(board, target, target.Anchor);

        return TutorialDragPoints.At(cam.WorldToScreenPoint(fromWorld),
                                     cam.WorldToScreenPoint(toWorld));
    }

    public void ShowCaption(string text)
    {
        if (_overlay && _overlay.Caption) _overlay.Caption.Show(text);
    }

    public void HideCaption()
    {
        if (_overlay && _overlay.Caption) _overlay.Caption.Hide();
    }

    /// <summary>
    /// Blocks until a condition is met. The try/finally matters: if the whole
    /// coroutine is stopped mid-wait, End() still runs and the watcher lets go
    /// of the static MatchResolver.OnBlast hook.
    /// </summary>
    public IEnumerator WaitFor(TutorialCondition condition)
    {
        var watcher = new TutorialConditionWatcher();
        watcher.Begin(condition, Board);

        try
        {
            while (!watcher.IsSatisfied)
            {
                watcher.Tick(Time.unscaledDeltaTime);
                yield return null;
            }
        }
        finally
        {
            watcher.End();
        }
    }
}
