using System;
using System.Collections;
using UnityEngine;

/// <summary>Out-parameter for WaitFor, which cannot have one because it is a coroutine.</summary>
public class TutorialWaitResult
{
    public bool timedOut;
}

/// <summary>
/// Walks the step list of a TutorialSequenceSO and gives each step the services
/// it needs: target resolving, the hand, the caption, the tooltip, the focus gate
/// and condition waiting.
///
/// Lives on the TutorialOverlay object (the manager adds it if it is missing),
/// because the overlay is the scene object the tutorial draws through - if the
/// scene goes away, so does the running tutorial, which is what we want.
/// </summary>
[DisallowMultipleComponent]
public class TutorialRunner : MonoBehaviour
{
    [Tooltip("Hard ceiling on a whole sequence, unscaled seconds. This is now the " +
             "ONLY thing that will ever hand the screen back on its own - the " +
             "per-step player timeout and the tap-to-escape hatch are both off, " +
             "because a tap outside the highlight must do nothing. Keep it long. " +
             "0 = no ceiling at all.")]
    [SerializeField] private float hardStopSeconds = 600f;

    private TutorialOverlay _overlay;
    private Camera _worldCamera;
    private BoardGridXY _board;
    private Coroutine _sequence;
    private bool _abortRequested;

    public bool IsRunning => _sequence != null;

    public TutorialOverlay Overlay => _overlay;
    public TutorialHand Hand => _overlay ? _overlay.Hand : null;
    public TutorialTooltip Tooltip => _overlay ? _overlay.Tooltip : null;
    public TutorialFocusGate Focus => _overlay ? _overlay.Focus : null;

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
        if (_overlay && _overlay.Focus) _overlay.Focus.OnAbortRequested -= HandleAbortRequested;

        _overlay = overlay;

        if (_overlay)
        {
            _overlay.ClearAll();
            if (_overlay.Focus) _overlay.Focus.OnAbortRequested += HandleAbortRequested;
        }
    }

    private void OnDisable()
    {
        // The screen must never stay locked because this object went away.
        if (_overlay) _overlay.ClearAll();
    }

    private void OnDestroy()
    {
        if (_overlay && _overlay.Focus) _overlay.Focus.OnAbortRequested -= HandleAbortRequested;
    }

    /// <summary>Runs a sequence to the end, then calls onFinished.</summary>
    public void Run(TutorialSequenceSO sequence, Action onFinished)
    {
        Stop();
        _abortRequested = false;
        _finishRequested = false;
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

    /// <summary>
    /// Asks the running sequence to stop at the next opportunity. Used by the
    /// escape hatch and by steps that time out: the tutorial is NOT marked as seen,
    /// so it gets another chance later.
    /// </summary>
    public void AbortSequence()
    {
        _abortRequested = true;
        if (_overlay) _overlay.ClearAll();
    }

    /// <summary>True once something has asked the sequence to stop.</summary>
    public bool AbortRequested => _abortRequested;

    /// <summary>
    /// Ends the sequence EARLY BUT AS COMPLETED: the remaining steps are skipped and
    /// onFinished still runs, so the tutorial is marked as seen. For a lesson that
    /// has already made its point, e.g. the hero-upgrade loop once the player can no
    /// longer afford the next hero.
    /// </summary>
    public void FinishSequence()
    {
        _finishRequested = true;
        if (_overlay) _overlay.ClearAll();
    }

    private bool _finishRequested;

    private void HandleAbortRequested() => AbortSequence();

    private IEnumerator RunSequence(TutorialSequenceSO sequence, Action onFinished)
    {
        // Wall clock, not an accumulator: a step that yields one long nested
        // coroutine only pumps once per completion, so counting deltas here would
        // under-measure the sequence by whole minutes.
        float startedAt = Time.unscaledTime;

        // try/finally, so an exception thrown inside a step can never leave the
        // focus gate shut with the player locked out of their own game.
        try
        {
            if (!sequence || !sequence.HasSteps)
            {
                Debug.LogWarning("[Tutorial] Sequence is empty - nothing to play.");
            }
            else
            {
                for (int i = 0; i < sequence.steps.Count; i++)
                {
                    if (_abortRequested || _finishRequested) break;

                    var step = sequence.steps[i];
                    if (step == null) continue;   // an empty row in the SerializeReference list

                    var running = step.Run(this);
                    while (running.MoveNext())
                    {
                        if (_abortRequested || _finishRequested) break;

                        if (hardStopSeconds > 0f && Time.unscaledTime - startedAt >= hardStopSeconds)
                        {
                            Debug.LogWarning($"[Tutorial] '{sequence.TutorialId}' hit the {hardStopSeconds}s " +
                                             "hard stop - releasing the screen and aborting.");
                            _abortRequested = true;
                            break;
                        }

                        yield return running.Current;
                    }
                }
            }
        }
        finally
        {
            if (_overlay) _overlay.ClearAll();
            _sequence = null;
        }

        if (_abortRequested)
        {
            // Deliberately does NOT call onFinished: that is what writes the
            // "already seen" flag, and an aborted lesson has not been seen.
            Debug.Log($"[Tutorial] '{(sequence ? sequence.TutorialId : "?")}' aborted - not marked as seen.");
            var manager = TutorialManager.Instance;
            if (manager) manager.StopCurrent();
            yield break;
        }

        onFinished?.Invoke();
    }

    // ------------------------------------------------------------------
    //  Services the steps call
    // ------------------------------------------------------------------

    public bool TryResolve(TutorialTarget target, out Vector2 screenPos)
    {
        return target.TryResolveScreen(WorldCamera, Board, out screenPos);
    }

    /// <summary>The target's whole rectangle - what the gate and the tooltip use.</summary>
    public bool TryResolveRect(TutorialTarget target, out Rect screenRect)
    {
        return target.TryResolveScreenRect(WorldCamera, Board, out screenRect);
    }

    /// <summary>
    /// Is this target on screen AND able to take a click right now?
    ///
    /// The second half is what makes a guided step wait out the 0.35s panel slide
    /// in MainMenuPanelController, which turns blocksRaycasts off on the whole panel
    /// while it tweens. Pointing a hand at a button during that window would be
    /// telling the player to press something that cannot be pressed.
    /// </summary>
    public bool IsTargetReady(TutorialTarget target)
    {
        if (target.kind == TutorialTarget.Kind.SceneAnchor)
        {
            var anchor = TutorialAnchor.Find(target.anchorId);
            return anchor && anchor.IsInteractableNow();
        }

        return TryResolve(target, out _);
    }

    public void ShowCaption(string text)
    {
        if (_overlay && _overlay.Caption) _overlay.Caption.Show(text);
    }

    public void HideCaption()
    {
        if (_overlay && _overlay.Caption) _overlay.Caption.Hide();
    }

    public void ShowTooltip(string text, Func<Rect?> targetRect, TutorialTooltipSide side)
    {
        if (_overlay && _overlay.Tooltip) _overlay.Tooltip.Show(text, targetRect, side);
    }

    public void HideTooltip()
    {
        if (_overlay && _overlay.Tooltip) _overlay.Tooltip.Hide();
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

    /// <summary>
    /// Blocks until a condition is met. The try/finally matters: if the whole
    /// coroutine is stopped mid-wait, End() still runs and the watcher lets go
    /// of the static MatchResolver.OnBlast hook.
    /// </summary>
    public IEnumerator WaitFor(TutorialCondition condition)
    {
        yield return WaitFor(condition, 0f, null);
    }

    /// <summary>
    /// As above, with a safety timeout. `result.timedOut` tells the caller whether
    /// the condition was actually met or whether we simply gave up - a step must
    /// not treat "gave up" as "the player did it".
    /// </summary>
    public IEnumerator WaitFor(TutorialCondition condition, float timeoutSeconds, TutorialWaitResult result)
    {
        if (result != null) result.timedOut = false;

        var watcher = new TutorialConditionWatcher();
        watcher.Begin(condition, Board);

        float waited = 0f;

        try
        {
            while (!watcher.IsSatisfied)
            {
                if (_abortRequested) yield break;

                float dt = Time.unscaledDeltaTime;
                watcher.Tick(dt);

                if (timeoutSeconds > 0f)
                {
                    waited += dt;
                    if (waited >= timeoutSeconds)
                    {
                        if (result != null) result.timedOut = true;
                        yield break;
                    }
                }

                yield return null;
            }
        }
        finally
        {
            watcher.End();
        }
    }
}
