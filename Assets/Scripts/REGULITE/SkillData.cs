using UnityEngine;

[CreateAssetMenu(menuName = "Roguelite/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public string description;
    public SkillCategory category;
    public SkillEffectType effectType;
    public Sprite normalIcon;
    public Sprite evolvedIcon;
    public float baseIncrement;
    public float maxIncrement;
    public float evolvedIncrement;

    // NEW: restricts the skill to certain fighter types
    public FighterType[] allowedTypes;
}

public enum SkillCategory { Offensive, Defensive }
public enum SkillEffectType { AttackSpeed, AttackDamage, Health }
