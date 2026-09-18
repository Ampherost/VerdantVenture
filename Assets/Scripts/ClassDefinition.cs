using System.Collections.Generic;
using UnityEngine;

public enum ClassTier { Base, Intermediate, Advanced }

/// <summary>Shared class data: growth modifiers, stat ceilings and future promotion choices.</summary>
[CreateAssetMenu(fileName = "NewClass", menuName = "SRPG/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    [Header("Identity")]
    public string className = "Class";
    public ClassTier tier = ClassTier.Base;

    [Header("Leveling")]
    [Tooltip("Level at which EXP stops accumulating in this class.")]
    public int maxLevel = 20;
    [Tooltip("Minimum level before promotionOptions become available. Ignored if empty.")]
    public int promotionLevel = 10;

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
}
