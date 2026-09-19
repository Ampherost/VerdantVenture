using System.Collections.Generic;
using UnityEngine;

public struct PartyRules
{
    public bool permadeath;
    public int reviveHP;
    public bool healAfterBattle;
}

/// <summary>Writes battle outcomes using supplied party state and rules, without scene lookups.</summary>
public static class PartyResultWriter
{
    /// <summary>
    /// Push each deployed unit's progression and final HP back onto its party member.
    ///
    /// Permadeath only applies to a won battle: a total party wipe is treated as a retreat,
    /// otherwise a single loss would empty the roster and leave the game unwinnable.
    /// </summary>
    public static void Write(IEnumerable<KeyValuePair<Unit, PartyMember>> deployed, bool victory, PartyRules rules, System.Action heal)
    {
        foreach (var pair in deployed)
        {
            Unit unit = pair.Key;
            PartyMember member = pair.Value;
            if (member == null) continue;

            member.EnsureInitialised();
            // Keep gains even for casualties; revive/heal rules must use the grown MaxHP.
            member.ReadBackFrom(unit);

            // A destroyed GameObject compares equal to null; treat that as a casualty.
            member.currentHP = unit == null ? 0 : Mathf.Max(0, unit.currentHP);

            if (member.currentHP <= 0)
            {
                if (victory && rules.permadeath)
                {
                    member.isDead = true;
                    Debug.Log($"[PartyResultWriter] {member.Name} was lost for good.");
                    continue;
                }

                member.currentHP = Mathf.Clamp(rules.reviveHP, 1, member.MaxHP);
                Debug.Log($"[PartyResultWriter] {member.Name} fell but was recovered " +
                          $"({member.currentHP} HP).");
            }
        }

        if (victory && rules.healAfterBattle) heal?.Invoke();
    }

    /// <summary>Undo writeback, restoring all pre-battle progression, HP and flags exactly.</summary>
    public static void Restore(IEnumerable<KeyValuePair<PartyMember, PartyMember.Snapshot>> snapshots)
    {
        foreach (var pair in snapshots)
        {
            if (pair.Key == null) continue;
            pair.Key.RestoreSnapshot(pair.Value);
        }
    }

}
