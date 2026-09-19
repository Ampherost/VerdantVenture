using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Spawns in BattleRunner.Awake and positions in BattleRunner.Start, before Unit.Start.</summary>
public static class BattleSpawner
{
    public static Deployment Spawn(EncounterData encounter, GameData data, Object context = null)
    {
        var deployment = new Deployment();
        ClearHandPlacedUnits();
        SpawnParty(encounter, data, deployment, context);
        SpawnEnemies(encounter, deployment, context);
        return deployment;
    }
    /// <summary>
    /// Remove units placed by hand in the editor — this encounter brings its own cast.
    /// Deactivating first matters: Destroy is deferred to the end of the frame, so
    /// TurnManager's Awake sweep would otherwise still find them. Its sweep excludes
    /// inactive objects, so this hides them in time.
    /// </summary>
    private static void ClearHandPlacedUnits()
    {
        foreach (var u in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if (u == null) continue;
            u.gameObject.SetActive(false);
            Object.Destroy(u.gameObject);
        }
    }

    private static void SpawnParty(EncounterData encounter, GameData data, Deployment deployment, Object context)
    {
        if (data == null)
        {
            Debug.LogError("[BattleSpawner] No GameData found, so there's no party to deploy. " +
                           "Drop the GameData prefab into your first scene.", context);
            return;
        }

        List<Vector2Int> cells = encounter.playerSpawnCells;
        int slots = Mathf.Min(cells.Count, encounter.maxDeployed);
        int slot = 0;

        foreach (var member in data.Party)
        {
            if (slot >= slots) break;
            if (!member.IsDeployable) continue;

            Unit unit = SpawnUnit(member.definition, cells[slot], Team.Player, null, deployment, context);
            if (unit == null) continue;

            member.ApplyTo(unit);

            deployment.deployed[unit] = member;
            deployment.snapshotBeforeBattle[member] = member.CaptureSnapshot();
            slot++;
        }

        if (slot == 0)
            Debug.LogError("[BattleSpawner] Nobody was deployed. Either the party is empty, " +
                           "everyone is at 0 HP or benched, or their definitions have no " +
                           "prefabs assigned.", context);
    }

    private static void SpawnEnemies(EncounterData encounter, Deployment deployment, Object context)
    {
        if (encounter.enemies == null) return;

        bool bossSelected = false;
        foreach (var spawn in encounter.enemies)
        {
            if (spawn == null || spawn.definition == null) continue;
            Unit unit = SpawnUnit(spawn.definition, spawn.cell, Team.Enemy, spawn.nameOverride, deployment, context);
            if (unit == null) continue;
            ClassDefinition cls = spawn.classOverride ?? spawn.definition.defaultClass;
            unit.currentClass = cls;
            unit.currentLevel = Mathf.Max(1, spawn.level);
            if (unit.currentLevel > 1)
                unit.stats += LevelUpResolver.ExpectedGains(unit.stats,
                    ClassDefinition.CombinedGrowths(spawn.definition, cls),
                    ClassDefinition.CapsOf(cls), unit.currentLevel - 1);
            unit.currentHP = unit.maxHP;
            if (spawn.isBoss && !bossSelected)
            {
                deployment.Boss = unit;
                bossSelected = true;
            }
        }
    }

    private static Unit SpawnUnit(UnitDefinition def, Vector2Int cell, Team team, string nameOverride,
        Deployment deployment, Object context)
    {
        if (def == null || !def.IsSpawnable)
        {
            Debug.LogError($"[BattleSpawner] '{(def != null ? def.name : "null")}' has no prefab " +
                           $"with a Unit component, so it can't be spawned.", context);
            return null;
        }

        GameObject go = Object.Instantiate(def.prefab);
        Unit unit = go.GetComponent<Unit>();

        def.ApplyTo(unit);
        unit.team = team;
        if (!string.IsNullOrWhiteSpace(nameOverride)) unit.unitName = nameOverride;
        go.name = $"{unit.unitName} [{team}]";

        // Positioning waits for Start — see the execution-order comment on BattleRunner.
        deployment.pendingPlacement.Add(new KeyValuePair<Unit, Vector2Int>(unit, cell));

        // TurnManager's own Awake sweep normally catches this, but register anyway in case
        // its Awake happened to run first.
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterUnit(unit);

        return unit;
    }

    public static void Place(Deployment deployment, GridManager grid)
    {
        if (grid == null)
        {
            if (deployment.pendingPlacement.Count > 0)
                Debug.LogError("[BattleSpawner] No GridManager in the scene — spawned units have " +
                               "nowhere to stand.");
            return;
        }

        foreach (var pair in deployment.pendingPlacement)
        {
            if (pair.Key == null) continue;

            if (!grid.InBounds(pair.Value))
                Debug.LogWarning($"[BattleSpawner] '{pair.Key.unitName}' is set to spawn on " +
                                 $"{pair.Value}, which isn't part of the painted map. It'll be " +
                                 $"nudged to the nearest free tile.", pair.Key);

            pair.Key.transform.position = grid.CellToWorld(pair.Value);
        }

        deployment.pendingPlacement.Clear();
    }

}
