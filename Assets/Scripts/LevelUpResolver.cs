using System;

/// <summary>Pure stat growth resolution using supplied rolls or deterministic expected gains.</summary>
public static class LevelUpResolver
{
    public const int ExpPerLevel = 100;

    /// <summary>
    /// Roll one level of gains. rng must return an integer in [1,100], unlike
    /// CombatResolver's [0,99] contract. One roll is consumed for each of the seven
    /// combat stats, even for zero growth or a capped stat. Negative growth is zero;
    /// g / 100 points are guaranteed, plus one when rng() is at most g % 100.
    /// Each gain is min(rolled, max(0, cap - current)); cap 0 means uncapped.
    /// Gains never reduce a stat already above its cap. Current HP and movement are
    /// never rolled and their caps are ignored: currentHP equals the maxHP gain,
    /// and moveRange is zero.
    /// </summary>
    /// <exception cref="ArgumentNullException">rng is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">rng returns outside [1,100].</exception>
    public static UnitStats RollGains(UnitStats current, StatGrowths growths, UnitStats caps, Func<int> rng)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        var gains = new UnitStats
        {
            maxHP = ClampGain(Roll(growths.hp, rng), current.maxHP, caps.maxHP),
            attack = ClampGain(Roll(growths.attack, rng), current.attack, caps.attack),
            defense = ClampGain(Roll(growths.defense, rng), current.defense, caps.defense),
            resistance = ClampGain(Roll(growths.resistance, rng), current.resistance, caps.resistance),
            speed = ClampGain(Roll(growths.speed, rng), current.speed, caps.speed),
            skill = ClampGain(Roll(growths.skill, rng), current.skill, caps.skill),
            luck = ClampGain(Roll(growths.luck, rng), current.luck, caps.luck)
        };
        gains.currentHP = gains.maxHP;
        return gains;
    }

    /// <summary>
    /// Expected gains for N levels: Math.Round(levels * growth / 100,
    /// MidpointRounding.AwayFromZero), so ten levels at 45% yield 5 and at 44% yield 4.
    /// Negative growth and non-positive levels yield zero. Uses the same caps and
    /// HP/movement rules as RollGains, without RNG. Gains saturate at int.MaxValue
    /// if the uncapped expectation cannot fit in a UnitStats field.
    /// </summary>
    public static UnitStats ExpectedGains(UnitStats current, StatGrowths growths, UnitStats caps, int levels)
    {
        var gains = new UnitStats
        {
            maxHP = ClampGain(Expected(growths.hp, levels), current.maxHP, caps.maxHP),
            attack = ClampGain(Expected(growths.attack, levels), current.attack, caps.attack),
            defense = ClampGain(Expected(growths.defense, levels), current.defense, caps.defense),
            resistance = ClampGain(Expected(growths.resistance, levels), current.resistance, caps.resistance),
            speed = ClampGain(Expected(growths.speed, levels), current.speed, caps.speed),
            skill = ClampGain(Expected(growths.skill, levels), current.skill, caps.skill),
            luck = ClampGain(Expected(growths.luck, levels), current.luck, caps.luck)
        };
        gains.currentHP = gains.maxHP;
        return gains;
    }

    private static int Roll(int growth, Func<int> rng)
    {
        int roll = rng();
        if (roll < 1 || roll > 100)
            throw new ArgumentOutOfRangeException(nameof(rng), roll, "Growth rolls must be in [1,100].");
        growth = Math.Max(0, growth);
        return growth / 100 + (roll <= growth % 100 ? 1 : 0);
    }

    private static long Expected(int growth, int levels) =>
        (long)Math.Round((double)Math.Max(0, levels) * Math.Max(0, growth) / 100,
            MidpointRounding.AwayFromZero);

    private static int ClampGain(long gain, int current, int cap)
    {
        long available = cap == 0 ? long.MaxValue : Math.Max(0L, (long)cap - current);
        return (int)Math.Min(int.MaxValue, Math.Min(Math.Max(0L, gain), available));
    }
}
