using UnityEngine;

public enum SkillCategory { Offensive, Defensive }

/// <summary>
/// Which stat on <see cref="UnitStatsRuntime"/> this buff multiplies.
///
/// ORDER MATTERS. The first three are the original values and must keep their
/// indices, or the three existing SkillData assets and the 21 stage scenes that
/// reference them would deserialize onto the wrong stat. New stats are appended.
///
/// attackRange is deliberately absent - it is not in the buff pool for now.
/// Adding it later means appending one more value here, nothing else.
/// </summary>
public enum SkillEffectType { AttackSpeed, AttackDamage, Health, Defense, MoveSpeed }

/// <summary>Whether a drawn card buffs one hero type or the whole army.</summary>
public enum SkillTargetMode { SpecificUnit, AllUnits }

/// <summary>
/// One buff DEFINITION - a stat and how much it grants at each star.
///
/// It does NOT name a hero. The hero is chosen at draw time by
/// <see cref="BuffDraw"/>, which is why a roster of 4 heroes x 5 of these
/// assets produces 20 distinct cards from only 5 authored assets.
/// </summary>
[CreateAssetMenu(menuName = "Roguelite/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public string description;
    public SkillCategory category;
    public SkillEffectType effectType;
    public Sprite normalIcon;
    public Sprite evolvedIcon;

    [Header("Legacy increments (used only when Value Per Star is empty)")]
    public float baseIncrement;
    public float maxIncrement;
    public float evolvedIncrement;

    [Tooltip("Optional whitelist. Empty = this buff can be offered for ANY hero type.")]
    public FighterType[] allowedTypes;

    [Header("Targeting")]
    [Tooltip("SpecificUnit = the draw picks one hero and only that hero type is buffed.\n" +
             "AllUnits = an army-wide card, offered rarely.")]
    public SkillTargetMode targetMode = SkillTargetMode.SpecificUnit;

    [Header("Value per star")]
    [Tooltip("Increment granted the 1st, 2nd, 3rd... time ONE hero takes this buff.\n" +
             "0.5, 0.6, 0.75 = +50%, +60%, +75% - the values grow with the hero's star count.\n" +
             "Leave EMPTY to fall back to the legacy increments above.")]
    public float[] valuePerStar;

    [Tooltip("How many times a single hero may take this buff. " +
             "0 = valuePerStar.Length, or 6 in legacy mode.")]
    [Min(0)] public int maxStars = 0;

    /// <summary>Star cap for one hero. Never returns less than 1.</summary>
    public int MaxStars
    {
        get
        {
            if (maxStars > 0) return maxStars;
            if (valuePerStar != null && valuePerStar.Length > 0) return valuePerStar.Length;
            return 6;   // legacy: the old evolve threshold
        }
    }

    /// <summary>
    /// The TOTAL increment this buff grants a hero that already holds
    /// <paramref name="starsAlreadyTaken"/> stars of it and is about to take one more.
    /// 0.5 means +50%, i.e. a x1.5 multiplier.
    /// </summary>
    public float IncrementAtStars(int starsAlreadyTaken)
    {
        if (valuePerStar != null && valuePerStar.Length > 0)
        {
            int i = Mathf.Clamp(starsAlreadyTaken, 0, valuePerStar.Length - 1);
            return valuePerStar[i];
        }

        // Legacy curve, preserved so the three existing assets still behave as before.
        int times = starsAlreadyTaken + 1;
        return times < 6
            ? Mathf.Min(times * baseIncrement, maxIncrement)
            : evolvedIncrement;
    }

    /// <summary>True when this buff may be offered for the given hero type.</summary>
    public bool AppliesTo(FighterType type)
    {
        if (allowedTypes == null || allowedTypes.Length == 0) return true;

        for (int i = 0; i < allowedTypes.Length; i++)
            if (allowedTypes[i] == type) return true;

        return false;
    }
}
