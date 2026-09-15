using UnityEngine;

/// <summary>HP projections assume all strikes hit without criticals; probabilities are separate.</summary>
public struct AttackForecast
{
    public const int CriticalMultiplier = 3;
    public int hitChance, critChance, counterHitChance, counterCritChance;
    public int attackerStrikes, counterStrikes;
    public bool usesSpecial;

    public static int Damage(Unit attacker, Unit defender, SpecialAttackData special = null)
    {
        int armor = attacker.equippedWeapon != null &&
            attacker.equippedWeapon.damageType == DamageCategory.Special
            ? defender.stats.resistance : defender.stats.defense;
        if (special != null && special.bypassesArmor) armor = 0;
        int damage = Unit.ComputeDamage(attacker.TotalAttackPower, armor);
        return special == null ? damage : Mathf.Max(1,
            Mathf.FloorToInt(damage * Mathf.Max(0f, special.damageMultiplier)));
    }

    public static int HitChance(Unit attacker, Unit defender) =>
        Mathf.Clamp(attacker.TotalHitRate - defender.TotalAvoidRate, 0, 100);
    public static int CritChance(Unit attacker, Unit defender, bool special = false) =>
        special ? 0 : Mathf.Clamp(attacker.TotalCritRate - defender.CritAvoid, 0, 100);
    public static bool Doubles(Unit attacker, Unit defender) =>
        (long)attacker.stats.speed - defender.stats.speed >= 4;

    public static AttackForecast Calculate(Unit attacker, Unit target, Vector2Int fromCell,
        bool useSpecial = false)
    {
        var f = new AttackForecast { attacker = attacker, target = target, usesSpecial = useSpecial };
        if (attacker != null) f.attackerHPBefore = f.attackerHPAfter = attacker.currentHP;
        if (target != null) f.targetHPBefore = f.targetHPAfter = target.currentHP;
        if (attacker == null || !attacker.CanAttackFrom(fromCell, target, target != null ? target.Cell : default) ||
            (useSpecial && !attacker.CanUseSpecial)) return f;

        f.isValid = true;
        SpecialAttackData special = useSpecial ? attacker.equippedSpecial : null;
        f.damage = Damage(attacker, target, special);
        f.hitChance = HitChance(attacker, target);
        f.critChance = CritChance(attacker, target, useSpecial);
        f.counterDamage = Damage(target, attacker);
        f.counterHitChance = HitChance(target, attacker);
        f.counterCritChance = CritChance(target, attacker);
        f.attackerStrikes = Doubles(attacker, target) ? 2 : 1;
        f.targetCounters = (special == null || !special.preventsCounter) &&
            target.CanAttackFrom(target.Cell, attacker, fromCell);
        f.counterStrikes = f.targetCounters ? (Doubles(target, attacker) ? 2 : 1) : 0;

        // Initiator, counter, then the faster unit's follow-up. Stop on projected death.
        f.targetHPAfter = Mathf.Max(0, f.targetHPAfter - f.damage);
        if (f.targetHPAfter > 0 && f.targetCounters)
            f.attackerHPAfter = Mathf.Max(0, f.attackerHPAfter - f.counterDamage);
        else { f.targetCounters = false; f.counterStrikes = 0; }
        if (f.targetHPAfter > 0 && f.attackerHPAfter > 0)
        {
            if (f.attackerStrikes == 2) f.targetHPAfter = Mathf.Max(0, f.targetHPAfter - Damage(attacker, target));
            if (f.counterStrikes == 2) f.attackerHPAfter = Mathf.Max(0, f.attackerHPAfter - f.counterDamage);
        }
        f.targetDies = f.targetHPAfter == 0;
        f.attackerDies = f.attackerHPAfter == 0;
        return f;
    }

    public Unit attacker;
    public Unit target;

    /// <summary>False when the attack isn't legal (null, dead, same team, out of range).</summary>
    public bool isValid;

    /// <summary>Raw damage the strike deals, before HP clamping. This is the number to show.</summary>
    public int damage;

    public int targetHPBefore;
    public int targetHPAfter;
    public bool targetDies;

    /// <summary>True if the target survives and can reach back.</summary>
    public bool targetCounters;
    public int counterDamage;

    public int attackerHPBefore;
    public int attackerHPAfter;
    public bool attackerDies;

    /// <summary>HP actually removed from the target (differs from 'damage' on overkill).</summary>
    public int ActualDamage => targetHPBefore - targetHPAfter;

    /// <summary>HP actually removed from the attacker by the counter.</summary>
    public int ActualCounterDamage => attackerHPBefore - attackerHPAfter;

    public string TargetHPText => $"{targetHPBefore} -> {targetHPAfter}";
    public string AttackerHPText => $"{attackerHPBefore} -> {attackerHPAfter}";

    /// <summary>One-line summary of the counter, ready for a forecast panel.</summary>
    public string CounterText
    {
        get
        {
            if (!isValid) return "-";

            if (!targetCounters) return "No counter";
            return $"Counters for {counterDamage} x {counterStrikes} ({counterHitChance}% hit, {counterCritChance}% crit)";
        }
    }
}
