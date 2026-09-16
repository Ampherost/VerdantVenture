using System.Collections.Generic;
using UnityEngine;

/// <summary>State owned by one battle, shared by setup, objectives, and result writing.</summary>
public class Deployment
{
    public readonly Dictionary<Unit, PartyMember> deployed = new Dictionary<Unit, PartyMember>();
    public readonly Dictionary<PartyMember, int> hpBeforeBattle = new Dictionary<PartyMember, int>();
    public readonly List<KeyValuePair<Unit, Vector2Int>> pendingPlacement =
        new List<KeyValuePair<Unit, Vector2Int>>();
    public Unit Boss;
}
