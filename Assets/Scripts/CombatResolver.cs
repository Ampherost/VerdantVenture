using System;

/// <summary>Combat exchange rules, independent of battle setup and scene loading.</summary>
public static class CombatResolver
{
    /// <summary>
    /// Resolve one exchange using the supplied random roll (an integer from 0 through 99).
    /// Specials replace the initiating strike only; follow-ups are ordinary attacks.
    /// Charge must be ready before initiation. One initiation pip is awarded per exchange.
    /// </summary>
    public static string ResolveCombat(Unit attacker, Unit defender, Func<int> roll, bool useSpecial = false)
    {
        if (attacker == null || !attacker.CanAttack(defender) ||
            (useSpecial && !attacker.CanUseSpecial)) return string.Empty;

        if (roll == null) throw new ArgumentNullException(nameof(roll));

        SpecialAttackData special = useSpecial ? attacker.equippedSpecial : null;
        bool counterAllowed = special == null || !special.preventsCounter;
        bool attackerDoubles = AttackForecast.Doubles(attacker, defender);
        bool defenderDoubles = AttackForecast.Doubles(defender, attacker);
        if (useSpecial) attacker.ConsumeBurst();
        attacker.GainBurstPip(1);
        var log = new System.Text.StringBuilder();
        ResolveStrike(attacker, defender, special, log, roll);
        if (attacker.IsAlive && defender.IsAlive && counterAllowed && defender.CanAttack(attacker))
            ResolveStrike(defender, attacker, null, log, roll);
        if (attacker.IsAlive && defender.IsAlive)
        {
            if (attackerDoubles && attacker.CanAttack(defender))
                ResolveStrike(attacker, defender, null, log, roll);
            else if (defenderDoubles && counterAllowed && defender.CanAttack(attacker))
                ResolveStrike(defender, attacker, null, log, roll);
        }
        return log.ToString().TrimEnd();
    }

    private static void ResolveStrike(Unit attacker, Unit defender, SpecialAttackData special,
        System.Text.StringBuilder log, Func<int> roll)
    {
        if (roll() >= AttackForecast.HitChance(attacker, defender))
        {
            log.AppendLine($"{attacker.unitName} misses {defender.unitName}.");
            return;
        }
        bool critical = special == null &&
            roll() < AttackForecast.CritChance(attacker, defender);
        int damage = AttackForecast.Damage(attacker, defender, special);
        if (critical) damage *= AttackForecast.CriticalMultiplier;
        int removed = Math.Min(defender.currentHP, damage);
        // Award KO charge before death callbacks can end the encounter.
        if (damage >= defender.currentHP) attacker.GainBurstPip(1);
        defender.ApplyDamage(damage);
        log.AppendLine($"{attacker.unitName} hits {defender.unitName} for {removed}" +
            (critical ? " (critical!)." : "."));
        if (!defender.IsAlive) log.AppendLine($"{defender.unitName} is defeated!");
    }
}
