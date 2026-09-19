using System.Collections.Generic;
using UnityEngine;

/// <summary>State owned by one battle, shared by setup, objectives, and result writing.</summary>
public class Deployment
{
    public readonly Dictionary<Unit, PartyMember> deployed = new Dictionary<Unit, PartyMember>();
    public readonly Dictionary<PartyMember, PartyMember.Snapshot> snapshotBeforeBattle =
        new Dictionary<PartyMember, PartyMember.Snapshot>();
    public readonly List<KeyValuePair<Unit, Vector2Int>> pendingPlacement =
        new List<KeyValuePair<Unit, Vector2Int>>();
    public Unit Boss;
}
