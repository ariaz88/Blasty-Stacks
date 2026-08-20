using UnityEngine;

[System.Serializable]


public class UnitStatsRuntime
{
    public float attack, defense, maxHP;
    public float attackSpeed, moveSpeed, attackRange;
    public FighterType type;

    public bool initialized;

    public void FromSO(UnitStatsSO so)
    {
        if (!so) return;
        attack = so.attack;
        defense = so.defense;
        maxHP = so.maxHP;
        attackSpeed = so.attackSpeed;
        moveSpeed = so.moveSpeed;
        attackRange = so.attackRange;
        type = so.type;
        initialized = true;
    }

    public void ApplyMultipliers(float atkMult = 1f, float defMult = 1f, float hpMult = 1f,
                                 float moveMult = 1f, float atkSpdMult = 1f, float rangeMult = 1f)
    {
        attack *= atkMult;
        defense *= defMult;
        maxHP *= hpMult;
        moveSpeed *= moveMult;
        attackSpeed *= atkSpdMult;
        attackRange *= rangeMult;
    }


   
    public void ApplyLevelGrowth(int level,
                                 float atkPct,
                                 float hpPct,
                                 float movePct,
                                 float atkSpdPct,
                                 float defPct = 0f,
                                 float rangePct = 0f)
    {
        int d = Mathf.Max(0, level - 1);

        float gA = Mathf.Pow(1f + atkPct, d);
        float gH = Mathf.Pow(1f + hpPct, d);
        float gMv = Mathf.Pow(1f + movePct, d);
        float gAS = Mathf.Pow(1f + atkSpdPct, d);
        float gD = Mathf.Pow(1f + defPct, d);
        float gR = Mathf.Pow(1f + rangePct, d);

        attack *= gA;
        maxHP *= gH;
        moveSpeed *= gMv;
        attackSpeed *= gAS;
        defense *= gD;
        attackRange *= gR;
    }

    // Runtime multipliers (buffs/wave/gear)
}
