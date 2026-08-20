// PuzzleMoveBudget.cs
using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The per-level cap on how many moves the player may make on the board before
/// the puzzle phase is over ("BATTLE BEGINS IN n MOVES").
///
/// What counts as a move: picking a piece up AND dropping it on a DIFFERENT
/// anchor cell. Tapping a piece and releasing it where it already sat is not a
/// move, and neither is a drag that the board refused (the piece slides back to
/// its original anchor, so the anchor is unchanged). BoardInputController is
/// what makes that call - see its EndDrag.
///
/// Running out of moves only locks the board. It does NOT start the battle:
/// the player still has to press BATTLE (see BattleStartController).
///
/// The budget lives on the scene, not on LevelConfig, because the two
/// LevelConfig assets (Spawner1/Spawner2) are shared by all 20 stage scenes,
/// while each stage IS its own scene - so the scene is the per-level place.
/// </summary>
public class PuzzleMoveBudget : MonoBehaviour
{
    [Header("Budget")]
    [Tooltip("Moves allowed on this stage's board. 0 = unlimited.")]
    [SerializeField, Min(0)] private int movesAllowed = 8;

    [Header("Optional UI")]
    [Tooltip("Label showing the moves left. Safe to leave empty.")]
    [SerializeField] private TMP_Text movesRemainingText;

    [Tooltip("Format for that label. {0} = moves left, {1} = total allowed.")]
    [SerializeField] private string movesTextFormat = "{0}";

    /// <summary>Moves allowed on this stage. 0 means unlimited.</summary>
    public int MovesAllowed => movesAllowed;

    /// <summary>Valid moves made so far.</summary>
    public int MovesUsed { get; private set; }

    public bool IsUnlimited => movesAllowed <= 0;

    /// <summary>Moves left. int.MaxValue when the budget is unlimited.</summary>
    public int MovesRemaining => IsUnlimited ? int.MaxValue : Mathf.Max(0, movesAllowed - MovesUsed);

    /// <summary>False once the budget is spent - the board stops accepting new drags.</summary>
    public bool HasMovesLeft => IsUnlimited || MovesUsed < movesAllowed;

    /// <summary>Raised after every counted move, and after ResetBudget.</summary>
    public event Action OnMovesChanged;

    /// <summary>Raised the first time the budget hits zero.</summary>
    public event Action OnMovesExhausted;

    private void Start()
    {
        RefreshUI();
    }

    /// <summary>
    /// Counts one valid move. Called by BoardInputController only after it has
    /// confirmed the piece actually changed cell.
    /// </summary>
    public void RegisterMove()
    {
        if (!HasMovesLeft) return;   // already spent; never count past the cap

        MovesUsed++;
        RefreshUI();
        RaiseChanged();

        if (!HasMovesLeft)
        {
            try { OnMovesExhausted?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }

    /// <summary>Puts the budget back to full. For retry / revive flows.</summary>
    public void ResetBudget()
    {
        if (MovesUsed == 0) return;

        MovesUsed = 0;
        RefreshUI();
        RaiseChanged();
    }

    private void RefreshUI()
    {
        if (!movesRemainingText) return;

        movesRemainingText.text = IsUnlimited
            ? string.Empty
            : string.Format(movesTextFormat, MovesRemaining, movesAllowed);
    }

    private void RaiseChanged()
    {
        try { OnMovesChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }
}
