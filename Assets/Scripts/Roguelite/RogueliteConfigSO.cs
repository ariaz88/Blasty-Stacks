using UnityEngine;

/// <summary>
/// Every tunable number for the in-battle roguelite layer, in one asset.
///
/// Stage length is deliberately NOT a setting here. The XP curve is
/// self-balancing: a longer stage has more enemies, so it produces more
/// level-ups on its own. <see cref="maxLevel"/> is the only safety cap.
/// </summary>
[CreateAssetMenu(menuName = "Roguelite/Roguelite Config", fileName = "RogueliteConfigSO")]
public class RogueliteConfigSO : ScriptableObject
{
    [Header("XP Curve")]
    [Tooltip("Basic-enemy kills needed for the FIRST level-up. The reference game uses 2.")]
    [Min(0.01f)] public float baseKills = 2f;

    [Tooltip("threshold(L) = baseKills * ratio^(L-1). The reference game measures at ~1.32, " +
             "which gives 50%, 38%, 29%, 22%, 17% ... of the bar per basic kill.")]
    [Min(1f)] public float ratio = 1.32f;

    [Tooltip("Hard cap on the buff level. Once reached, no further XP is collected and no " +
             "more cards are offered. 0 = uncapped.")]
    [Min(0)] public int maxLevel = 0;

    [Header("Draw - fairness (rotation)")]
    [Tooltip("Weight added for every level-up a hero has gone WITHOUT appearing on a card. " +
             "1 = a hero that sat out one round has double weight next round. 0 = no rotation.")]
    [Min(0f)] public float pityStep = 1f;

    [Tooltip("A hero unseen for this many level-ups is FORCED onto the next screen. " +
             "This is the hard fairness floor. 0 = never forced.")]
    [Min(0)] public int forceAfterRounds = 2;

    [Header("Draw - specialization (investment)")]
    [Tooltip("Weight added per STAR a hero already holds, so a hero the player has been " +
             "buffing keeps showing up more often than one that has never been buffed.\n" +
             "0.5 = a hero with 2 stars has double the weight of an unbuffed one. 0 = off.")]
    [Min(0f)] public float investmentStep = 0.5f;

    [Tooltip("Extra weight multiplier for the hero picked on the PREVIOUS screen, on top of " +
             "the investment bonus - lets the player follow up immediately. 1 = no extra bias.")]
    [Min(0.01f)] public float momentumBonus = 1.5f;

    [Header("Draw - global cards")]
    [Tooltip("Chance that ONE of the three slots is an army-wide card instead of a hero card. " +
             "Never more than one per screen, so there are always at least two hero choices.")]
    [Range(0f, 1f)] public float globalCardChance = 0.15f;

    /// <summary>XP needed to clear <paramref name="level"/>, in basic-enemy units. Level is 1-based.</summary>
    public float ThresholdFor(int level)
    {
        return baseKills * Mathf.Pow(ratio, Mathf.Max(0, level - 1));
    }
}
