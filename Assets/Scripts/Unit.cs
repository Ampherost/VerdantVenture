using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum Team { Player, Enemy }

/// <summary>
/// A single combat unit that occupies one grid cell.
/// Attach to a sprite GameObject in the CombatScene and register it with the grid at start.
///
/// A unit holds exactly one cell at a time. PlaceAt() releases the previous cell before
/// claiming a new one, so teleports / reinforcements / rescue can't leak occupancy.
/// </summary>
public partial class Unit : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Identity")]
    public string unitName = "Unit";
    public UnitDefinition definition;
    public Team team = Team.Player;

    [Header("Stats")]
    public UnitStats stats = UnitStats.Default;
    public ClassDefinition currentClass;
    public string ClassName => currentClass != null ? currentClass.className : "—";
    public WeaponData equippedWeapon;
    public SpecialAttackData equippedSpecial;
    public int maxHP { get => stats.maxHP; set => stats.maxHP = value; }
    public int currentHP { get => stats.currentHP; set => stats.currentHP = value; }
    public int attack { get => stats.attack; set => stats.attack = value; }
    public int moveRange { get => stats.moveRange; set => stats.moveRange = value; }
    public int defense { get => stats.defense; set => stats.defense = value; }
    public int resistance { get => stats.resistance; set => stats.resistance = value; }
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
        if (legacy_maxHP >= 0) { stats.maxHP = legacy_maxHP; legacy_maxHP = -1; }
        if (legacy_currentHP >= 0) { stats.currentHP = legacy_currentHP; legacy_currentHP = -1; }
        if (legacy_attack >= 0) { stats.attack = legacy_attack; legacy_attack = -1; }
        if (legacy_defense >= 0) { stats.defense = stats.resistance = legacy_defense; legacy_defense = -1; }
        if (legacy_moveRange >= 0) { stats.moveRange = legacy_moveRange; legacy_moveRange = -1; }
    }


    [Header("Movement")]
    [Tooltip("How fast the unit slides between tiles, in cells/sec.")]
    public float moveSpeed = 6f;

    // ---- UI hooks ----
    // Fired whenever currentHP changes, so health bars and info panels can redraw
    // without polling every frame.
    public event Action<Unit> OnHPChanged;
    // Fired once, immediately before the unit is deactivated.
    public event Action<Unit> OnDied;

    // Grid position (source of truth for logic; transform follows it).
    public Vector2Int Cell { get; private set; }

    // Set true after this unit has acted this turn.
    public bool HasActed { get; set; }

    public bool IsAlive => currentHP > 0;
    public bool IsMoving { get; private set; }

    /// <summary>True while this unit holds a cell on the grid.</summary>
    public bool IsOnGrid { get; private set; }

    /// <summary>0..1 health fraction, safe against a zero/negative maxHP.</summary>
    public float HPFraction => maxHP <= 0 ? 0f : Mathf.Clamp01((float)currentHP / maxHP);

    private void Start()
    {
        SnapToGrid();
    }

    // ---- Placement ----

    /// <summary>
    /// Claim a grid cell based on where this unit was placed in the editor.
    ///
    /// If that cell is unusable — off the painted map, blocked, or already claimed by
    /// another unit (Start() order across GameObjects is arbitrary, so which unit gets
    /// there first is not something to rely on) — the unit is nudged to the nearest free
    /// cell and a warning names both tiles. A unit that cannot be placed at all is
    /// deactivated and unregistered, because an unplaceable-but-alive unit can never be
    /// selected and would stall the player phase forever.
    /// </summary>
    public void SnapToGrid()
    {
        var grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogError($"[Unit] '{unitName}' found no GridManager in the scene.", this);
            enabled = false;
            return;
        }

        Vector2Int desired = grid.WorldToCell(transform.position);

        if (grid.IsWalkable(desired) && PlaceAt(desired))
            return;

        string reason = DescribeBlockage(grid, desired);

        if (grid.TryFindNearestFreeCell(desired, out Vector2Int free) && PlaceAt(free))
        {
            Debug.LogWarning(
                $"[Unit] '{unitName}' was placed on cell {desired} but {reason}. " +
                $"Nudged to {free} — fix the placement in the scene.", this);
            return;
        }

        Debug.LogError(
            $"[Unit] '{unitName}' could not be placed near {desired} ({reason}) and no free " +
            $"cell was found. Deactivating it so it doesn't stall the turn loop.", this);

        RemoveFromGrid();
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(this);
        gameObject.SetActive(false);
    }

    private string DescribeBlockage(GridManager grid, Vector2Int cell)
    {
        if (!grid.InBounds(cell))
            return "that cell is outside the playable map";

        Unit other = grid.GetUnitAt(cell);
        if (other != null)
            return $"'{other.unitName}' ({other.name}) is already standing there";

        return "that cell is blocked by an obstacle";
    }

    /// <summary>
    /// Put the unit on 'cell', releasing whatever cell it held before.
    ///
    /// Refuses and returns false if the destination is off-map or held by another unit —
    /// validation happens before the old cell is released, so a rejected placement leaves
    /// the unit exactly where it was rather than stranding it off the grid.
    /// </summary>
    public bool PlaceAt(Vector2Int cell)
    {
        var grid = GridManager.Instance;
        if (grid == null) return false;

        if (!grid.InBounds(cell))
        {
            Debug.LogWarning(
                $"[Unit] '{unitName}' cannot take cell {cell}: outside the playable map.", this);
            return false;
        }

        Unit sitting = grid.GetUnitAt(cell);
        if (sitting != null && sitting != this)
        {
            Debug.LogWarning(
                $"[Unit] '{unitName}' cannot take cell {cell}: " +
                $"'{sitting.unitName}' is already there.", this);
            return false;
        }

        // Release the cell we were holding before claiming the new one.
        if (IsOnGrid && Cell != cell)
            grid.ClearCell(Cell, this);

        Cell = cell;
        transform.position = grid.CellToWorld(cell);
        grid.SetUnit(cell, this);
        IsOnGrid = true;
        return true;
    }

    /// <summary>
    /// Instant relocation for teleports, reinforcements, warp-in skills and the like.
    /// Takes 'cell' when it's free; with allowNudge, falls back to the nearest free cell
    /// so a summon aimed at an occupied tile lands beside it instead of failing outright.
    /// Returns false only if nothing suitable was found.
    /// </summary>
    public bool TryWarpTo(Vector2Int cell, bool allowNudge = true)
    {
        var grid = GridManager.Instance;
        if (grid == null) return false;

        if ((grid.IsWalkable(cell) || grid.GetUnitAt(cell) == this) && PlaceAt(cell))
            return true;

        if (!allowNudge) return false;

        if (grid.TryFindNearestFreeCell(cell, out Vector2Int free) && PlaceAt(free))
        {
            Debug.Log($"{unitName} warped to {free} ({cell} was unavailable).");
            return true;
        }

        Debug.LogWarning($"[Unit] '{unitName}' found no free cell near {cell} to warp to.", this);
        return false;
    }

    /// <summary>
    /// Take the unit off the board without killing it — rescue, capture, retreat, or a
    /// transport pickup. The tile is freed; the unit stays alive and registered, so put
    /// it back with PlaceAt / TryWarpTo when it's dropped off.
    /// </summary>
    public void RemoveFromGrid()
    {
        if (!IsOnGrid) return;
        if (GridManager.Instance != null)
            GridManager.Instance.ClearCell(Cell, this);
        IsOnGrid = false;
    }

    /// <summary>Smoothly move along a path (list of adjacent cells), updating occupancy at the end.</summary>
    public IEnumerator MoveAlong(System.Collections.Generic.List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) yield break;

        IsMoving = true;
        Vector2Int from = Cell;

        foreach (var step in path)
        {
            Vector3 target = GridManager.Instance.CellToWorld(step);
            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
        }

        Vector2Int dest = path[path.Count - 1];
        GridManager.Instance.MoveUnit(from, dest, this);
        Cell = dest;
        IsOnGrid = true;
        IsMoving = false;
    }

    // ---- Damage ----

    /// <summary>
    /// The one damage formula. Both the forecast and the real hit call this, so they
    /// cannot drift apart — change the rules here and the preview follows automatically.
    /// </summary>
    public static int ComputeDamage(int rawAttack, int defense)
    {
        return Mathf.Max(1, rawAttack - defense);
    }

    /// <summary>Legacy physical raw-attack entry point.</summary>
    public void TakeDamage(int rawAttack) => ApplyDamage(ComputeDamage(rawAttack, stats.defense));

    /// <summary>Apply resolved damage without subtracting armor again.</summary>
    public void ApplyDamage(int damage)
    {
        if (!IsAlive || damage <= 0) return;
        currentHP = Mathf.Max(0, currentHP - damage);
        GainBurstPip(1);
        OnHPChanged?.Invoke(this);
        if (!IsAlive) Die();
    }

    public int TotalAttackPower => stats.attack + (equippedWeapon != null ? equippedWeapon.might : 0);
    public int TotalHitRate => (equippedWeapon != null ? equippedWeapon.baseHit : 80) + stats.skill * 2 + stats.luck / 2;
    public int TotalAvoidRate => stats.speed * 2 + stats.luck;
    public int TotalCritRate => (equippedWeapon != null ? equippedWeapon.baseCrit : 0) + stats.skill / 2;
    public int CritAvoid => stats.luck;
    public int currentBurstPips { get; private set; }
    public int MaxBurstPips => equippedSpecial != null ? equippedSpecial.pipCost : 0;
    public bool HasSpecial => equippedSpecial != null;
    public bool CanUseSpecial => HasSpecial && MaxBurstPips > 0 && currentBurstPips >= MaxBurstPips;

    public void GainBurstPip(int amount)
    {
        currentBurstPips = (int)System.Math.Min(System.Math.Max(0, MaxBurstPips),
            (long)currentBurstPips + System.Math.Max(0, amount));
    }

    public void ConsumeBurst() => currentBurstPips = 0;

    /// <summary>Changing techniques resets charge to prevent banking a cheaper gauge.</summary>
    public void EquipSpecial(SpecialAttackData newSpecial)
    {
        equippedSpecial = newSpecial;
        ConsumeBurst();
    }

    private void Die()
    {
        Debug.Log($"{unitName} was defeated.");

        OnDied?.Invoke(this);

        RemoveFromGrid();
        gameObject.SetActive(false);

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyUnitDied(this);
    }

    // ---- Range ----

    /// <summary>Manhattan distance to another cell — used for attack range checks.</summary>
    public int DistanceTo(Vector2Int other)
    {
        return Mathf.Abs(Cell.x - other.x) + Mathf.Abs(Cell.y - other.y);
    }

    /// <summary>True if 'target' is within this unit's attack range from its current cell.</summary>
    public bool CanAttack(Unit target)
    {
        return CanAttackFrom(Cell, target, target != null ? target.Cell : Vector2Int.zero);
    }

    /// <summary>
    /// Range check with both cells supplied, so a forecast can ask "could I hit that from
    /// over there?" without moving anyone. Everything except the two cells is read from
    /// live state, so a dead or off-board unit is still untouchable.
    /// </summary>
    public bool CanAttackFrom(Vector2Int myCell, Unit target, Vector2Int targetCell)
    {
        if (target == null || !target.IsAlive || !IsAlive) return false;
        if (!IsOnGrid || !target.IsOnGrid) return false;     // off-board units are untouchable
        if (target.team == team) return false;               // no friendly fire

        int d = Mathf.Abs(myCell.x - targetCell.x) + Mathf.Abs(myCell.y - targetCell.y);
        return d >= MinAttackRange && d <= AttackRange;
    }

    // ---- Attacking ----

    public AttackForecast PreviewAttack(Unit target) => PreviewAttack(target, Cell);
    public AttackForecast PreviewAttack(Unit target, Vector2Int fromCell) =>
        AttackForecast.Calculate(this, target, fromCell);
    public AttackForecast PreviewAttack(Unit target, Vector2Int fromCell, bool useSpecial) =>
        AttackForecast.Calculate(this, target, fromCell, useSpecial);
    public string Attack(Unit target) => CombatResolver.ResolveCombat(this, target, () => UnityEngine.Random.Range(0, 100));
    public string Attack(Unit target, bool useSpecial) => CombatResolver.ResolveCombat(this, target, () => UnityEngine.Random.Range(0, 100), useSpecial);

#if UNITY_EDITOR
    // ---- Edit-time overlap warning ----
    // Draws the cell this unit will snap to. Red means another unit snaps to the same
    // cell, so you can catch duplicate placement before entering play mode.
    private void OnDrawGizmos()
    {
        GridManager grid = GridManager.Instance != null
            ? GridManager.Instance
            : FindFirstObjectByType<GridManager>();
        if (grid == null) return;

        Vector2Int myCell = grid.WorldToCell(transform.position);
        bool clash = false;

        foreach (var other in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if (other == this) continue;
            if (grid.WorldToCell(other.transform.position) == myCell) { clash = true; break; }
        }

        Gizmos.color = clash ? Color.red : new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireCube(grid.CellToWorld(myCell),
                            new Vector3(grid.cellSize, grid.cellSize, 0f) * 0.9f);
    }
#endif
}
