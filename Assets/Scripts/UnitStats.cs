using System;

[Serializable]
public struct UnitStats
{
    public int maxHP, currentHP;
    public int attack, physDef, specDef;
    public int speed, skill, luck;
    public int moveRange;

    public static UnitStats Default => new UnitStats
    {
        maxHP = 20, currentHP = 20, attack = 5,
        physDef = 2, specDef = 2, moveRange = 4
    };
}
