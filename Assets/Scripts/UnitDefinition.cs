using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// The template for one kind of unit: which prefab to spawn and what its base stats are.
///
/// This is the *shared* half of a unit's identity — it never changes during play. The
/// mutable half (current HP, whether they're dead) lives in a PartyMember, or is simply
/// discarded when the battle ends in the case of enemies.
///
/// Create with: Assets -> Create -> SRPG -> Unit Definition
/// </summary>
[CreateAssetMenu(fileName = "NewUnit", menuName = "SRPG/Unit Definition")]
public class UnitDefinition : ScriptableObject, ISerializationCallbackReceiver
{
    [Header("Identity")]
    public string unitName = "Unit";

    [Tooltip("Prefab with a Unit component on it. Sprite, collider, animator — whatever you " +
             "want the unit to look like in the combat scene.")]
    public GameObject prefab;

    [Header("Base Stats")]
    // TODO (Issue 1: StatGrowths struct and per-unit innate growth rates): hide template
    // currentHP in a custom drawer; ApplyTo always spawns at baseStats.maxHP.
    public UnitStats baseStats = UnitStats.Default;

    [Header("Growth")]
    [Tooltip("Innate per-level growth chances (%) before any class modifier is applied.")]
    public StatGrowths personalGrowths = StatGrowths.Default;

    [Header("Class")]
    public ClassDefinition defaultClass;

    [Header("Equipment")]
    public WeaponData equippedWeapon;
    public SpecialAttackData equippedSpecial;
    public int maxHP { get => baseStats.maxHP; set => baseStats.maxHP = value; }
    public int currentHP { get => baseStats.currentHP; set => baseStats.currentHP = value; }
    public int attack { get => baseStats.attack; set => baseStats.attack = value; }
    public int moveRange { get => baseStats.moveRange; set => baseStats.moveRange = value; }
    public int defense { get => baseStats.defense; set => baseStats.defense = value; }
    public int resistance { get => baseStats.resistance; set => baseStats.resistance = value; }
    public int AttackRange => equippedWeapon != null ? equippedWeapon.maxRange : 1;
    public int MinAttackRange => equippedWeapon != null ? equippedWeapon.minRange : 1;
    public int attackRange => AttackRange;

    // Import old flat serialized stats once. Existing script properties forward to the struct.
    [SerializeField, HideInInspector, FormerlySerializedAs("maxHP")] private int legacy_maxHP = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("currentHP")] private int legacy_currentHP = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("attack")] private int legacy_attack = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("defense")] private int legacy_defense = -1;
    [SerializeField, HideInInspector, FormerlySerializedAs("moveRange")] private int legacy_moveRange = -1;
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        if (legacy_maxHP >= 0) { baseStats.maxHP = legacy_maxHP; legacy_maxHP = -1; }
        if (legacy_currentHP >= 0) { baseStats.currentHP = legacy_currentHP; legacy_currentHP = -1; }
        if (legacy_attack >= 0) { baseStats.attack = legacy_attack; legacy_attack = -1; }
        if (legacy_defense >= 0) { baseStats.defense = baseStats.resistance = legacy_defense; legacy_defense = -1; }
        if (legacy_moveRange >= 0) { baseStats.moveRange = legacy_moveRange; legacy_moveRange = -1; }
    }


    /// <summary>
    /// Stamp these stats onto a freshly spawned Unit. Current HP is set to full — callers
    /// that are restoring a wounded party member overwrite it afterward.
    /// </summary>
    public void ApplyTo(Unit unit)
    {
        if (unit == null) return;

        unit.unitName = unitName;
        unit.definition = this;
        unit.currentLevel = 1;
        unit.currentExp = 0;
        unit.stats = baseStats;
        unit.currentClass = defaultClass;
        unit.currentHP = baseStats.maxHP;
        unit.equippedWeapon = equippedWeapon;
        unit.EquipSpecial(equippedSpecial);
    }

    /// <summary>True if this definition can actually produce a unit.</summary>
    public bool IsSpawnable => prefab != null && prefab.GetComponent<Unit>() != null;
}
