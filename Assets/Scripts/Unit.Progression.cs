using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The level reached and stat gains from one completed level-up.</summary>
public readonly struct LevelUpResult
{
    public int Level { get; }
    public UnitStats Gains { get; }

    public LevelUpResult(int level, UnitStats gains)
    {
        Level = level;
        Gains = gains;
    }
}

public partial class Unit
{
    [Header("Progression")]
    public int currentLevel = 1;
    public int currentExp = 0;

    /// <summary>Fired after stats are applied and OnHPChanged has notified HP observers.</summary>
    public event Action<Unit, LevelUpResult> OnLevelUp;

    public StatGrowths Growths => ClassDefinition.CombinedGrowths(definition, currentClass);
    public UnitStats StatCaps => ClassDefinition.CapsOf(currentClass);
    public int MaxLevel => ClassDefinition.MaxLevelOf(currentClass);
    public bool AtMaxLevel => currentLevel >= MaxLevel;

    /// <summary>
    /// Award EXP, returning one result per level gained. Non-positive awards and awards
    /// at max level do nothing. Reaching max level discards all remaining EXP.
    /// The optional growth RNG is forwarded to each LevelUp and must return [1,100].
    /// </summary>
    public List<LevelUpResult> GainExp(int amount, Func<int> rng = null)
    {
        var results = new List<LevelUpResult>();
        if (amount <= 0 || AtMaxLevel) return results;

        long remaining = amount;
        while (remaining > 0 && !AtMaxLevel)
        {
            int needed = LevelUpResolver.ExpPerLevel - currentExp;
            if (remaining < needed)
            {
                currentExp += (int)remaining;
                break;
            }

            remaining -= needed;
            currentExp = 0;
            results.Add(LevelUp(rng).Value);
        }
        return results;
    }

    /// <summary>
    /// Gain one level using an independent growth RNG in [1,100]. Returns null when
    /// already at max level, meaning nothing changed (no rolls or events).
    /// Otherwise applies stats, notifies OnHPChanged, then publishes OnLevelUp.
    /// Direct calls preserve EXP unless this level reaches the class limit.
    /// </summary>
    public LevelUpResult? LevelUp(Func<int> rng = null)
    {
        if (AtMaxLevel) return null;
        if (rng == null) rng = () => UnityEngine.Random.Range(1, 101);

        UnitStats gains = LevelUpResolver.RollGains(stats, Growths, StatCaps, rng);
        stats += gains;
        currentLevel++;
        if (AtMaxLevel) currentExp = 0;
        var result = new LevelUpResult(currentLevel, gains);
        OnHPChanged?.Invoke(this);
        OnLevelUp?.Invoke(this, result);
        return result;
    }
}
