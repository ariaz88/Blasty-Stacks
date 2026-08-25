using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One whole tutorial, as an asset: an id used for the "already seen this"
/// flag, and an ordered list of steps.
///
/// The steps use [SerializeReference], so different step TYPES live in the same
/// list inside this single asset. Adding a tutorial to the game is therefore:
/// create one of these, fill the list, point a TutorialTrigger at it. No code.
/// </summary>
[CreateAssetMenu(fileName = "Tut_", menuName = "Blasty/Tutorial Sequence", order = 0)]
public class TutorialSequenceSO : ScriptableObject
{
    [Tooltip("Stable id used as the save flag. NEVER rename it after release - " +
             "a changed id makes every player see the tutorial again. Empty = use the asset name.")]
    public string tutorialId = "";

    [Tooltip("Mark this tutorial as seen when it finishes, so it never plays again.")]
    public bool playOnce = true;

    [Tooltip("Ordered beats. Use the + button and pick a step type.")]
    [SerializeReference] public List<TutorialStep> steps = new List<TutorialStep>();

    /// <summary>The save flag key. Falls back to the asset name when unset.</summary>
    public string TutorialId => string.IsNullOrEmpty(tutorialId) ? name : tutorialId;

    public bool HasSteps => steps != null && steps.Count > 0;
}
