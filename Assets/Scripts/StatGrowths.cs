using System;
using UnityEngine;

/// <summary>Per-level stat growth percentages. Personal and class growths add field by field.</summary>
[Serializable]
public struct StatGrowths
{
    // Above 100 is legal: each full 100 guarantees a point, with a chance at one more.
    // Negative values remain intact here; level-up rolls will clamp them to zero.
    [Tooltip("Maximum HP growth chance per level (%).")]
    public int hp;
    [Tooltip("Attack growth chance per level (%).")]
    public int attack;
    [Tooltip("Defense growth chance per level (%).")]
    public int defense;
    [Tooltip("Resistance growth chance per level (%).")]
    public int resistance;
    [Tooltip("Speed growth chance per level (%).")]
    public int speed;
    [Tooltip("Skill growth chance per level (%).")]
    public int skill;
    [Tooltip("Luck growth chance per level (%).")]
    public int luck;

    public static StatGrowths Default => new StatGrowths
    {
        hp = 60, attack = 40, defense = 30, resistance = 30,
        speed = 40, skill = 40, luck = 30
    };

    /// <summary>Add percentages without clamping, including negative modifiers.</summary>
    public static StatGrowths operator +(StatGrowths a, StatGrowths b) => new StatGrowths
    {
        hp = a.hp + b.hp,
        attack = a.attack + b.attack,
        defense = a.defense + b.defense,
        resistance = a.resistance + b.resistance,
        speed = a.speed + b.speed,
        skill = a.skill + b.skill,
        luck = a.luck + b.luck
    };
}
