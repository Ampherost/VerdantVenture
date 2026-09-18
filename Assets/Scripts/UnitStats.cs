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

    /// <summary>Add every field without clamping. Callers decide how to handle HP and movement.</summary>
    public static UnitStats operator +(UnitStats a, UnitStats b) => new UnitStats
    {
        maxHP = a.maxHP + b.maxHP,
        currentHP = a.currentHP + b.currentHP,
        attack = a.attack + b.attack,
        defense = a.defense + b.defense,
        resistance = a.resistance + b.resistance,
        speed = a.speed + b.speed,
        skill = a.skill + b.skill,
        luck = a.luck + b.luck,
        moveRange = a.moveRange + b.moveRange
    };
}
