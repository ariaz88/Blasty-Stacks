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

/// <summary>
/// One beat of a GUIDED chain: a tooltip and a pointing hand on ONE element, with
/// every other pixel of the screen dead. This is the "press exactly this" beat the
/// onboarding is built from.
///
/// Three things make it survive the real menu rather than only a tidy test scene:
///
///  - It WAITS for the target to be present AND able to take a click.
///    MainMenuPanelController turns blocksRaycasts off on the whole panel for the
///    0.35s of its slide, so a hand shown on arrival would be pointing at a dead
///    button for a third of a second. Fighting those CanvasGroups is useless - the
///    controller rewrites them on every tab change - so the step simply holds.
///
///  - Target, hole and tooltip are resolved through a LIVE lookup every frame,
///    never cached: the first deployed card is Instantiated fresh on every panel
///    open, and the Upgrade button is two different GameObjects that swap by
///    affordability. Both stay correct because nothing is remembered.
///
///  - The gate stays shut BETWEEN beats (keepGateAfter). Releasing it per step
///    would hand the player a fully live screen during the panel tween that the
///    next beat is still waiting on.
/// </summary>
[Serializable]
public class TutorialFocusTapStep : TutorialStep
{
    [TextArea(1, 3)]
    [Tooltip("Bubble text shown next to the target. Empty = no bubble.")]
    public string tooltip = "";

    public TutorialTooltipSide tooltipSide = TutorialTooltipSide.Auto;

    [Tooltip("The ONE thing the player may touch. Use SceneAnchor.")]
    public TutorialTarget at;

    [Tooltip("Screen pixels of slack around the target when punching the hole.")]
    public Vector2 holePadding = new Vector2(16f, 16f);

    [Tooltip("Darkness of the blocked area. 0 = block without darkening - which is " +
             "what the Lose panel wants, since it already draws its own 0.75 scrim.")]
    [Range(0f, 1f)] public float dimAlpha = 0f;

    public TutorialHandLoopTimings timings = TutorialHandLoopTimings.Default;

    [Tooltip("What ends the beat. AnchorClicked for a plain button; UnitUpgraded " +
             "where the tap can be refused by the game.")]
    public TutorialCondition until = new TutorialCondition { kind = TutorialCondition.Kind.AnchorClicked };

    [Tooltip("Give up waiting for the target to APPEAR, unscaled seconds. 0 = forever.")]
    public float resolveTimeout = 10f;

    [Tooltip("Give up waiting for the player to ACT, unscaled seconds. 0 = forever.")]
    public float giveUpAfterSeconds = 90f;

    [Tooltip("Keep the screen shut after this beat - the next one re-aims the hole. " +
             "Turn OFF on the last step of a sequence.")]
    public bool keepGateAfter = true;

    [Tooltip("Dead time after the tap, so a panel swap settles before the next beat.")]
    public float settleAfter = 0f;

    public enum HeroGuard { Off, AbortChain, EndChain }

    [Tooltip("For a step that points at a HERO CARD. Checked before the hand appears: " +
             "if that hero cannot be upgraded right now (or the deck has no card " +
             "there), the chain must not walk the player to a greyed-out Upgrade " +
             "button with the screen locked.\n" +
             "AbortChain - stop, NOT marked as seen (the first hero: nothing taught yet).\n" +
             "EndChain   - stop, marked as seen (heroes 2-4: the lesson already landed).")]
    public HeroGuard ifHeroNotUpgradable = HeroGuard.Off;

    // A card that is still missing this long after the panel is up is not coming:
    // the deck is shorter than this step's index.
    private const float MissingCardGrace = 0.6f;

    public override IEnumerator Run(TutorialRunner runner)
    {
        var gate = runner.Focus;

        // Shut the screen FIRST, before anything is resolved. On the Lose panel this
        // IS the requirement - REPLAY must never be pressable - and the panel is
        // still fading in at this point.
        if (gate) gate.BlockAll(dimAlpha);

        float waited = 0f;
        float cardMissingFor = 0f;
        while (!runner.IsTargetReady(at))
        {
            if (runner.AbortRequested) yield break;

            if (ifHeroNotUpgradable != HeroGuard.Off && IsListUpButCardMissing())
            {
                cardMissingFor += Time.unscaledDeltaTime;
                if (cardMissingFor >= MissingCardGrace)
                {
                    StopForHeroGuard(runner, "the deck has no card here");
                    yield break;
                }
            }
            else cardMissingFor = 0f;

            waited += Time.unscaledDeltaTime;
            if (resolveTimeout > 0f && waited >= resolveTimeout)
            {
                Debug.LogWarning($"[Tutorial] Focus step '{note}' gave up waiting for target " +
                                 $"'{Describe()}' to become usable - releasing the screen.");
                if (gate) gate.Release();
                runner.AbortSequence();
                yield break;
            }

            yield return null;
        }

        if (ifHeroNotUpgradable != HeroGuard.Off && !TargetHeroUpgradable())
        {
            StopForHeroGuard(runner, "that hero cannot be upgraded right now");
            yield break;
        }

        // One live lookup, shared by the hole and the bubble so they can never
        // disagree about where the target is.
        TutorialTarget target = at;
        Func<Rect?> rect = () => runner.TryResolveRect(target, out var r) ? r : (Rect?)null;

        if (gate) gate.FocusOn(rect, holePadding, dimAlpha);
        if (!string.IsNullOrEmpty(tooltip)) runner.ShowTooltip(tooltip, rect, tooltipSide);

        if (runner.Hand != null)
        {
            runner.Hand.StartTapLoop(() =>
            {
                if (!runner.TryResolve(target, out var p)) return TutorialDragPoints.None;
                return TutorialDragPoints.At(p, p);
            }, timings);
        }

        // Convenience: an AnchorClicked condition with no id of its own watches the
        // very anchor this step is pointing at, which is what it always means.
        TutorialCondition condition = until;
        if (condition.kind == TutorialCondition.Kind.AnchorClicked &&
            string.IsNullOrEmpty(condition.anchorId) &&
            at.kind == TutorialTarget.Kind.SceneAnchor)
        {
            condition.anchorId = at.anchorId;
        }

        var wait = new TutorialWaitResult();
        yield return runner.WaitFor(condition, giveUpAfterSeconds, wait);

        if (runner.Hand != null) runner.Hand.StopAndHide();
        runner.HideTooltip();

        if (wait.timedOut)
        {
            Debug.LogWarning($"[Tutorial] Focus step '{note}' timed out waiting for the player " +
                             $"on '{Describe()}' - releasing the screen.");
            if (gate) gate.Release();
            runner.AbortSequence();
            yield break;
        }

        if (runner.AbortRequested) yield break;

        if (gate)
        {
            if (keepGateAfter) gate.BlockAll(dimAlpha);
            else gate.Release();
        }

        if (settleAfter > 0f)
            yield return runner.WaitFor(TutorialCondition.ForDuration(settleAfter));
    }

    private string Describe()
        => at.kind == TutorialTarget.Kind.SceneAnchor ? $"anchor '{at.anchorId}'" : at.kind.ToString();

    // ------------------------------------------------------------------
    //  Hero guard
    // ------------------------------------------------------------------

    /// <summary>
    /// The anchor is live (so its panel is open) but resolves to nothing - the
    /// list exists and has no item at this index.
    /// </summary>
    private bool IsListUpButCardMissing()
    {
        if (at.kind != TutorialTarget.Kind.SceneAnchor) return false;
        var anchor = TutorialAnchor.Find(at.anchorId);
        return anchor && !anchor.ResolveTransform();
    }

    /// <summary>
    /// True when the card this step points at belongs to a hero the player can
    /// upgrade right now. Anything we cannot read (no progression service, e.g. a
    /// stripped test scene; a target that is not a card) counts as "yes" - the
    /// guard only ever stops the chain on a definite "no".
    /// </summary>
    private bool TargetHeroUpgradable()
    {
        if (at.kind != TutorialTarget.Kind.SceneAnchor) return true;

        var anchor = TutorialAnchor.Find(at.anchorId);
        var t = anchor ? anchor.ResolveTransform() : null;
        var card = t ? t.GetComponentInChildren<UnitCardView>(true) : null;
        if (!card) return true;

        var gsm = GameStartManager.Instance;
        var progression = gsm ? gsm.ProgressionService : null;
        if (progression == null) return true;

        return progression.CanUpgrade(card.UnitId, out _);
    }

    private void StopForHeroGuard(TutorialRunner runner, string why)
    {
        bool end = ifHeroNotUpgradable == HeroGuard.EndChain;
        Debug.Log($"[Tutorial] Focus step '{note}': {why} - " +
                  (end ? "ending the chain here (marked as seen)." : "aborting (NOT marked as seen)."));

        if (runner.Focus) runner.Focus.Release();
        if (end) runner.FinishSequence();
        else runner.AbortSequence();
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
