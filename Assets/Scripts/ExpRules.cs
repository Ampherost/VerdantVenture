using System;

/// <summary>Pure EXP formulas using levels and the outcome of one combat exchange.</summary>
public static class ExpRules
{
    // Placeholder balance values; tune later.
    public const int BaseHitExp = 10;
    public const int BaseKillExp = 30;
    public const int PerLevelDelta = 3;
    public const int MinHitExp = 1;
    public const int MinKillExp = 5;

    public static int ForHit(int attackerLevel, int defenderLevel) =>
        Clamp(BaseHitExp + ((long)defenderLevel - attackerLevel) * PerLevelDelta, MinHitExp);

    public static int ForKill(int attackerLevel, int defenderLevel) =>
        Clamp(BaseKillExp + ((long)defenderLevel - attackerLevel) * PerLevelDelta * 2, MinKillExp);

    /// <summary>Sum hit and kill EXP; a whiff earns MinHitExp. Negative hit counts count as zero.</summary>
    public static int ForExchange(int attackerLevel, int defenderLevel, int hitsLanded, bool killed)
    {
        int hits = Math.Max(0, hitsLanded);
        if (hits == 0 && !killed) return MinHitExp;
        long exp = (long)hits * ForHit(attackerLevel, defenderLevel);
        if (killed) exp += ForKill(attackerLevel, defenderLevel);
        return Clamp(exp, 0);
    }

    // Saturate unrepresentable awards instead of wrapping to negative EXP.
    private static int Clamp(long exp, int minimum) => (int)Math.Min(int.MaxValue, Math.Max(minimum, exp));
}
