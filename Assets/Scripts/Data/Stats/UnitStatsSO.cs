using UnityEngine;

public enum FighterType { Warrior, Archer , Horseman , Mage }

[CreateAssetMenu(fileName = "UnitStats", menuName = "TD/Unit Stats")]
public class UnitStatsSO : ScriptableObject
{
    [Header("Core")]
    public float attack = 20f;   // ATK
    public float defense = 10f;   // DEF
    public float maxHP = 100f;

    [Header("Dynamics")]
    public float attackSpeed = 1.0f;   // hits/sec
    public float moveSpeed = 3.5f;   // units/sec
    public float attackRange = 1.5f;   // meters

    public FighterType type = FighterType.Warrior;
}
