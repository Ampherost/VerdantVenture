using System;
using UnityEngine.Serialization;

[Serializable]
public struct UnitStats
{
    public int maxHP, currentHP;
    public int attack;
    [FormerlySerializedAs("physDef")] public int defense;
    [FormerlySerializedAs("specDef")] public int resistance;
    public int speed, skill, luck;
    public int moveRange;

    public static UnitStats Default => new UnitStats
    {
        maxHP = 20, currentHP = 20, attack = 5,
        defense = 2, resistance = 2, moveRange = 4
    };
}
