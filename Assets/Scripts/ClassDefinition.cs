using System.Collections.Generic;
using UnityEngine;

public enum ClassTier { Base, Intermediate, Advanced }

/// <summary>Shared class data: growth modifiers, stat ceilings and future promotion choices.</summary>
[CreateAssetMenu(fileName = "NewClass", menuName = "SRPG/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    public const int DefaultMaxLevel = 20;

    [Header("Identity")]
    public string className = "Class";
    public ClassTier tier = ClassTier.Base;

    [Header("Leveling")]
    [Tooltip("Level at which EXP stops accumulating in this class.")]
    [Min(1)] public int maxLevel = DefaultMaxLevel;
    [Tooltip("Minimum level before promotionOptions become available. Ignored if empty.")]
    [Min(1)] public int promotionLevel = 10;

    [Header("Growth")]
    [Tooltip("Added to the unit's personalGrowths on every level-up. May be negative.")]
    public StatGrowths growthModifiers;

    [Header("Stats")]
    [Tooltip("Flat bonuses applied once, on entering this class via promotion.")]
    public UnitStats promotionBonuses;
    [Tooltip("Hard ceiling per stat while in this class. Level-ups never exceed these.")]
    public UnitStats statCaps = UnitStats.MaxCaps;

    [Header("Promotion")]
    public List<ClassDefinition> promotionOptions = new List<ClassDefinition>();

    /// <summary>Add personal and class growths; each missing input contributes zero.</summary>
    public static StatGrowths CombinedGrowths(UnitDefinition def, ClassDefinition cls) =>
        (def != null ? def.personalGrowths : StatGrowths.Zero) +
        (cls != null ? cls.growthModifiers : StatGrowths.Zero);

    /// <summary>Return authored caps, or permissive default caps for a classless unit.</summary>
    public static UnitStats CapsOf(ClassDefinition cls) => cls != null ? cls.statCaps : UnitStats.MaxCaps;

    /// <summary>Return the class level limit, or the default limit for a classless unit.</summary>
    public static int MaxLevelOf(ClassDefinition cls) => cls != null ? cls.maxLevel : DefaultMaxLevel;
}
