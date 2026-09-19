using System;

/// <summary>Combat exchange rules, independent of battle setup and scene loading.</summary>
public static class CombatResolver
{
    /// <summary>
    /// Resolve one exchange using the supplied random roll (an integer from 0 through 99).
    /// Specials replace the initiating strike only; follow-ups are ordinary attacks.
    /// Charge must be ready before initiation. One initiation pip is awarded per exchange.
    /// growthRoll is independent, uses [1,100], and is passed only to GainExp.
    /// EXP and level-up log lines follow the strikes, before deferred battle completion.
    /// </summary>
    public static string ResolveCombat(Unit attacker, Unit defender, Func<int> roll, bool useSpecial = false,
        Func<int> growthRoll = null)
    {
        if (attacker == null || !attacker.CanAttack(defender) ||
            (useSpecial && !attacker.CanUseSpecial)) return string.Empty;

        if (roll == null) throw new ArgumentNullException(nameof(roll));

        // Capture the same manager for both ends, treating destroyed Unity objects as absent.
        TurnManager turns = TurnManager.Instance != null ? TurnManager.Instance : null;
        turns?.BeginExchange();
        try
        {
            SpecialAttackData special = useSpecial ? attacker.equippedSpecial : null;
            bool counterAllowed = special == null || !special.preventsCounter;
            bool attackerDoubles = AttackForecast.Doubles(attacker, defender);
            bool defenderDoubles = AttackForecast.Doubles(defender, attacker);
            int attackerLevel = attacker.currentLevel;
            int defenderLevel = defender.currentLevel;
            if (useSpecial) attacker.ConsumeBurst();
            attacker.GainBurstPip(1);
            var log = new System.Text.StringBuilder();
            int attackerHits = ResolveStrike(attacker, defender, special, log, roll) ? 1 : 0;
            int defenderHits = 0;
            if (attacker.IsAlive && defender.IsAlive && counterAllowed && defender.CanAttack(attacker))
                defenderHits += ResolveStrike(defender, attacker, null, log, roll) ? 1 : 0;
            if (attacker.IsAlive && defender.IsAlive)
            {
                if (attackerDoubles && attacker.CanAttack(defender))
                    attackerHits += ResolveStrike(attacker, defender, null, log, roll) ? 1 : 0;
                else if (defenderDoubles && counterAllowed && defender.CanAttack(attacker))
                    defenderHits += ResolveStrike(defender, attacker, null, log, roll) ? 1 : 0;
            }

            bool attackerKilled = !defender.IsAlive;
            bool defenderKilled = !attacker.IsAlive;
            if (turns == null || !turns.CombatOver)
            {
                AwardExp(attacker, attackerLevel, defenderLevel, attackerHits, attackerKilled, growthRoll, log);
                AwardExp(defender, defenderLevel, attackerLevel, defenderHits, defenderKilled, growthRoll, log);
            }
            return log.ToString().TrimEnd();
        }
        finally
        {
            turns?.EndExchange();
        }
    }

    private static bool ResolveStrike(Unit attacker, Unit defender, SpecialAttackData special,
        System.Text.StringBuilder log, Func<int> roll)
    {
        if (roll() >= AttackForecast.HitChance(attacker, defender))
        {
            log.AppendLine($"{attacker.unitName} misses {defender.unitName}.");
            return false;
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
        return true;
    }

    private static void AwardExp(Unit unit, int level, int opponentLevel, int hits, bool killed,
        Func<int> growthRoll, System.Text.StringBuilder log)
    {
        if (unit.team != Team.Player || !unit.IsAlive) return;
        int exp = ExpRules.ForExchange(level, opponentLevel, hits, killed);
        log.AppendLine($"{unit.unitName} gains {exp} EXP.");
        foreach (var result in unit.GainExp(exp, growthRoll))
        {
            UnitStats gains = result.Gains;
            log.AppendLine($"{unit.unitName} → Lv {result.Level}! " +
                $"+{gains.maxHP} HP +{gains.attack} ATK +{gains.defense} DEF " +
                $"+{gains.resistance} RES +{gains.speed} SPD +{gains.skill} SKL +{gains.luck} LCK");
        }
    }
}
