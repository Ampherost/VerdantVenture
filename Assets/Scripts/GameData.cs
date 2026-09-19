using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One party member's mutable progression and HP. Plain [Serializable] class so it
/// survives scene loads inside GameData without any
/// save/load plumbing.
/// </summary>
[Serializable]
public class PartyMember
{
    public UnitDefinition definition;

    [Tooltip("Level 0 is the serialized sentinel for progression not yet initialized.")]
    public int level;
    public int exp;
    [Tooltip("Grown stats, seeded from the definition once. stats.currentHP is dead data; " +
             "PartyMember.currentHP is the sole owner of persistent HP.")]
    public UnitStats stats;
    public ClassDefinition currentClass;

    [Tooltip("HP carried between battles. -1 means 'not initialised yet' — it gets filled " +
             "from grown MaxHP after progression is initialized.")]
    public int currentHP = -1;

    [Tooltip("Untick to bench someone without removing them from the roster.")]
    public bool inActiveParty = true;

    [Tooltip("Set when a unit falls and permadeath is on. Dead members are never deployed.")]
    public bool isDead;

    public string Name => definition != null ? definition.unitName : "(missing definition)";
    public int MaxHP => Mathf.Max(1, stats.maxHP);

    /// <summary>Can this member be put on the board right now?</summary>
    public bool IsDeployable
    {
        get
        {
            EnsureInitialised();
            return !isDead && inActiveParty && definition != null && definition.IsSpawnable && currentHP > 0;
        }
    }

    /// <summary>Seed progression before resolving the legacy uninitialized-HP sentinel.</summary>
    public void EnsureInitialised()
    {
        if (level == 0)
        {
            stats = definition != null ? definition.baseStats : default;
            currentClass = definition != null ? definition.defaultClass : null;
            level = 1;
            exp = 0;
        }
        if (currentHP < 0) currentHP = MaxHP;
        currentHP = Mathf.Clamp(currentHP, 0, MaxHP);
    }

    public void FullHeal()
    {
        if (isDead) return;
        EnsureInitialised();
        currentHP = MaxHP;
    }

    /// <summary>Apply template identity/equipment, then grown state and the separately owned HP.</summary>
    public void ApplyTo(Unit unit)
    {
        if (unit == null) return;
        EnsureInitialised();
        if (definition != null) definition.ApplyTo(unit);
        unit.definition = definition;
        unit.stats = stats;
        unit.currentLevel = level;
        unit.currentExp = exp;
        unit.currentClass = currentClass;
        unit.currentHP = currentHP;
    }

    /// <summary>Copy progression only. The result writer handles HP and casualty rules separately.</summary>
    public void ReadBackFrom(Unit unit)
    {
        if (unit == null) return;
        stats = unit.stats;
        level = unit.currentLevel;
        exp = unit.currentExp;
        currentClass = unit.currentClass;
    }

    /// <summary>A value copy of all member state before a battle, including death and roster flags.</summary>
    public readonly struct Snapshot
    {
        public readonly UnitDefinition definition;
        public readonly int currentHP, level, exp;
        public readonly UnitStats stats;
        public readonly ClassDefinition currentClass;
        public readonly bool isDead, inActiveParty;

        public Snapshot(PartyMember member)
        {
            definition = member.definition;
            currentHP = member.currentHP;
            level = member.level;
            exp = member.exp;
            stats = member.stats;
            currentClass = member.currentClass;
            isDead = member.isDead;
            inActiveParty = member.inActiveParty;
        }
    }

    public Snapshot CaptureSnapshot() => new Snapshot(this);

    /// <summary>Restore exactly; do not reseed, heal or clear the saved death flag.</summary>
    public void RestoreSnapshot(Snapshot snapshot)
    {
        definition = snapshot.definition;
        currentHP = snapshot.currentHP;
        level = snapshot.level;
        exp = snapshot.exp;
        stats = snapshot.stats;
        currentClass = snapshot.currentClass;
        isDead = snapshot.isDead;
        inActiveParty = snapshot.inActiveParty;
    }
}

/// <summary>
/// The one object that outlives scene loads. Holds the roster, each member's progression and HP,
/// and where to put the player when a battle sends them back to the overworld.
///
/// SETUP: make a prefab with this component, fill in the starting roster, and drop a copy
/// into every scene. The first one to wake up claims Instance and marks itself
/// DontDestroyOnLoad; every later copy destroys itself immediately, so you never end up
/// with two rosters.
///
/// Deliberately runs very early (-500) so anything that reads the party in its own Awake
/// finds a built roster rather than an empty list.
/// </summary>
[DefaultExecutionOrder(-500)]
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    [Header("Starting Roster")]
    [Tooltip("Who the player starts the game with. Built into live PartyMembers on first boot.")]
    public List<UnitDefinition> startingRoster = new List<UnitDefinition>();

    [Header("Between-Battle Rules")]
    [Tooltip("Heal everyone to full after every victory. Turn off for a more attritional game.")]
    public bool healAfterBattle = false;

    [Tooltip("A unit that falls in a won battle is gone for good. Ignored on a defeat, so a " +
             "total party wipe is a retreat rather than the end of the game.")]
    public bool permadeath = false;

    [Tooltip("HP a fallen unit comes back with when permadeath is off.")]
    [Min(1)] public int reviveHP = 1;

    // ---- Return point (set when a battle launches, consumed when we come back) ----
    [HideInInspector] public string returnSceneName;
    [HideInInspector] public Vector3 returnPosition;
    [HideInInspector] public bool hasReturnPoint;

    private readonly List<PartyMember> party = new List<PartyMember>();

    /// <summary>The live party. Order is deployment order.</summary>
    public IReadOnlyList<PartyMember> Party => party;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A second copy in a later scene. The original already holds the real roster.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildPartyFromRoster();
    }

    /// <summary>
    /// Turn the inspector roster into live PartyMembers. Runs once — later scene loads find
    /// the party already populated and leave it alone.
    /// </summary>
    private void BuildPartyFromRoster()
    {
        if (party.Count > 0) return;

        foreach (var def in startingRoster)
        {
            if (def == null) continue;

            if (!def.IsSpawnable)
                Debug.LogWarning($"[GameData] Roster entry '{def.name}' has no prefab (or the " +
                                 $"prefab has no Unit component). It can't be deployed.", this);

            var member = new PartyMember { definition = def };
            member.EnsureInitialised();
            party.Add(member);
        }

        if (party.Count == 0)
            Debug.LogWarning("[GameData] Starting roster is empty — no player units will spawn " +
                             "in combat. Assign some Unit Definitions.", this);
    }

    // ---- Roster helpers ----

    public PartyMember Find(UnitDefinition definition)
    {
        foreach (var m in party)
            if (m.definition == definition) return m;
        return null;
    }

    /// <summary>Recruit someone mid-game (an NPC joining after a conversation, say).</summary>
    public PartyMember Recruit(UnitDefinition definition)
    {
        if (definition == null) return null;

        PartyMember existing = Find(definition);
        if (existing != null) return existing;

        var member = new PartyMember { definition = definition };
        member.EnsureInitialised();
        party.Add(member);
        Debug.Log($"[GameData] {member.Name} joined the party.");
        return member;
    }

    public void HealAll()
    {
        foreach (var m in party) m.FullHeal();
    }

    /// <summary>True if nobody can be put on the board — usually a game over.</summary>
    public bool PartyIsWipedOut()
    {
        foreach (var m in party)
            if (m.IsDeployable) return false;
        return true;
    }

    // ---- Return point ----

    public void SetReturnPoint(string sceneName, Vector3 position)
    {
        returnSceneName = sceneName;
        returnPosition = position;
        hasReturnPoint = true;
    }

    public void ClearReturnPoint() => hasReturnPoint = false;
}
