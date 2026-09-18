using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private GridManager grid;
    private TurnManager turns;
    private Unit player;
    private Unit enemy;
    private CombatController controller;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        grid = Create("Test grid").AddComponent<GridManager>();
        player = CreateUnit("Player", Team.Player, new Vector2Int(1, 1));
        enemy = CreateUnit("Enemy", Team.Enemy, new Vector2Int(2, 1));
        turns = Create("Test turns").AddComponent<TurnManager>();
        controller = Create("Test controller").AddComponent<CombatController>();
        controller.enabled = false; // Drive commands explicitly, without mouse input.

        yield return null;
        yield return null;
        Assert.That(turns.CombatStarted, Is.True);
        Assert.That(turns.CombatOver, Is.False);
        Assert.That(player.IsOnGrid && enemy.IsOnGrid, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Destroy controllers before their dependencies, including after a failed assertion.
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.Destroy(objects[i]);
        objects.Clear();
        foreach (var asset in assets) Object.Destroy(asset);
        assets.Clear();
        yield return null;
    }

    [TestCase(8, 3, 5)]
    [TestCase(5, 5, 1)]
    [TestCase(3, 8, 1)]
    public void Damage_SubtractsDefenseButAlwaysDealsAtLeastOne(int attack, int defense, int expected)
    {
        Assert.That(Unit.ComputeDamage(attack, defense), Is.EqualTo(expected));
    }

    [Test]
    public void TakeDamage_UpdatesHealth_AndNotifiesObserversOnce()
    {
        int notifications = 0;
        player.OnHPChanged += unit => notifications++;
        player.TakeDamage(7);
        Assert.That(player.currentHP, Is.EqualTo(15));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public void Forecast_DoesNotChangeHealth_AndMatchesAttackAndCounter()
    {
        player.attack = 8;
        enemy.defense = 3;
        enemy.attack = 6;
        player.defense = 2;

        AttackForecast forecast = player.PreviewAttack(enemy);

        Assert.That(forecast.isValid, Is.True);
        Assert.That(forecast.targetCounters, Is.True);
        Assert.That(forecast.targetHPAfter, Is.EqualTo(15));
        Assert.That(forecast.attackerHPAfter, Is.EqualTo(16));
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(enemy.currentHP, Is.EqualTo(20));

        player.Attack(enemy);

        Assert.That(enemy.currentHP, Is.EqualTo(forecast.targetHPAfter));
        Assert.That(player.currentHP, Is.EqualTo(forecast.attackerHPAfter));
    }

    [Test]
    public void LethalAttack_PreventsCounter_FreesCell_AndWinsBattle()
    {
        enemy.currentHP = 2;
        Vector2Int cell = enemy.Cell;
        AttackForecast forecast = player.PreviewAttack(enemy);
        Assert.That(forecast.targetDies, Is.True);
        Assert.That(forecast.targetCounters, Is.False);

        player.Attack(enemy);

        Assert.That(enemy.currentHP, Is.Zero);
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(grid.GetUnitAt(cell), Is.Null);
        Assert.That(turns.CombatOver, Is.True);
        Assert.That(turns.Winner, Is.EqualTo(Team.Player));
        Assert.That(turns.CanEndPlayerPhase, Is.False);
    }

    [Test]
    public void AttackRange_RejectsFriendlyOutOfRangeAndOffGridTargets()
    {
        enemy.team = Team.Player;
        Assert.That(player.CanAttack(enemy), Is.False);
        enemy.team = Team.Enemy;
        Assert.That(enemy.TryWarpTo(new Vector2Int(5, 1)), Is.True);
        Assert.That(player.PreviewAttack(enemy).isValid, Is.False);
        Assert.That(enemy.TryWarpTo(new Vector2Int(2, 1)), Is.True);
        enemy.RemoveFromGrid();
        Assert.That(player.CanAttack(enemy), Is.False);
    }

    [UnityTest]
    public IEnumerator EndTurn_DuringMovement_IsRejected_ThenWorksAfterArrival()
    {
        Assert.That(enemy.TryWarpTo(new Vector2Int(4, 1)), Is.True);
        InvokeController("Select", player);
        controller.StartCoroutine((IEnumerator)InvokeController("MoveSelectedTo", new Vector2Int(3, 1)));

        AssertEndTurnIsBlocked();
        yield return WaitForAction();

        Assert.That(player.Cell, Is.EqualTo(new Vector2Int(3, 1)));
        Assert.That(grid.GetUnitAt(player.Cell), Is.SameAs(player));
        Assert.That(turns.CurrentPhase, Is.EqualTo(Team.Player));
        Assert.That(turns.CanEndPlayerPhase, Is.True);
        turns.EndPhaseEarly(Team.Player);
        Assert.That(turns.CurrentPhase, Is.EqualTo(Team.Enemy));
    }

    [UnityTest]
    public IEnumerator EndTurn_DuringAttackPause_IsRejected_ThenNormalPhaseFlowResumes()
    {
        InvokeController("Select", player);
        controller.StartCoroutine((IEnumerator)InvokeController("ResolveAttack", player, enemy));

        AssertEndTurnIsBlocked();
        yield return WaitForAction();

        Assert.That(player.HasActed, Is.True);
        Assert.That(turns.CurrentPhase, Is.EqualTo(Team.Enemy));
        turns.EndPhaseEarly(Team.Enemy);
        Assert.That(turns.CurrentPhase, Is.EqualTo(Team.Player));
        Assert.That(turns.CanEndPlayerPhase, Is.True);
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        assets.Add(asset);
        return asset;
    }

    [Test]
    public void SpecialWeapon_UsesResistance_AndAddsMight()
    {
        player.equippedWeapon.damageType = DamageCategory.Special;
        player.equippedWeapon.might = 4;
        enemy.stats.defense = 8;
        enemy.stats.resistance = 1;
        Assert.That(player.PreviewAttack(enemy).damage, Is.EqualTo(8));
        player.Attack(enemy);
        Assert.That(enemy.currentHP, Is.EqualTo(12));
    }

    [Test]
    public void Bow_RejectsAdjacentTarget_AndPreventsMeleeCounterAtTwoTiles()
    {
        player.equippedWeapon.minRange = player.equippedWeapon.maxRange = 2;
        Assert.That(player.CanAttack(enemy), Is.False);
        Assert.That(enemy.TryWarpTo(new Vector2Int(3, 1)), Is.True);
        Assert.That(player.CanAttack(enemy), Is.True);
        Assert.That(player.PreviewAttack(enemy).targetCounters, Is.False);
    }

    [TestCase(3, 17)]
    [TestCase(4, 14)]
    [TestCase(-4, 17)]
    public void SpeedThreshold_FollowUpsMatchProjection(int difference, int remaining)
    {
        player.stats.speed = Mathf.Max(0, difference);
        enemy.stats.speed = Mathf.Max(0, -difference);
        var f = player.PreviewAttack(enemy);
        player.Attack(enemy);
        Assert.That(enemy.currentHP, Is.EqualTo(remaining));
        Assert.That(enemy.currentHP, Is.EqualTo(f.targetHPAfter));
        Assert.That(player.currentHP, Is.EqualTo(f.attackerHPAfter));
    }

    [Test]
    public void Miss_GrantsInitiationCharge_ButNoDamageOrReceivedCharge()
    {
        player.EquipSpecial(CreateAsset<SpecialAttackData>());
        enemy.EquipSpecial(CreateAsset<SpecialAttackData>());
        player.equippedWeapon.baseHit = enemy.equippedWeapon.baseHit = 0;
        player.Attack(enemy);
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(enemy.currentHP, Is.EqualTo(20));
        Assert.That(player.currentBurstPips, Is.EqualTo(1));
        Assert.That(enemy.currentBurstPips, Is.Zero);
    }

    [Test]
    public void Critical_TriplesDamage_AndKOGrantsBonusCharge()
    {
        player.EquipSpecial(CreateAsset<SpecialAttackData>());
        player.equippedWeapon.baseCrit = 100;
        enemy.currentHP = 9;
        player.Attack(enemy);
        Assert.That(enemy.currentHP, Is.Zero);
        Assert.That(player.currentBurstPips, Is.EqualTo(2));
    }

    [Test]
    public void ChargedSpecial_BypassesArmor_PreventsCounters_AndCannotCrit()
    {
        var special = CreateAsset<SpecialAttackData>();
        special.bypassesArmor = special.preventsCounter = true;
        special.damageMultiplier = 2;
        player.EquipSpecial(special);
        Assert.That(player.Attack(enemy, true), Is.Empty);
        player.GainBurstPip(99);
        player.equippedWeapon.baseCrit = 100;
        var f = player.PreviewAttack(enemy, player.Cell, true);
        Assert.That(f.critChance, Is.Zero);
        Assert.That(f.targetCounters, Is.False);
        Assert.That(player.currentBurstPips, Is.EqualTo(3));
        player.Attack(enemy, true);
        Assert.That(enemy.currentHP, Is.EqualTo(10));
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(player.currentBurstPips, Is.EqualTo(1));
        player.EquipSpecial(null);
        Assert.That(player.currentBurstPips, Is.Zero);
        Assert.That(player.MaxBurstPips, Is.Zero);
    }

    [Test]
    public void LegacyFields_DeserializeIntoStruct_AndDefinitionCopiesIndependently()
    {
        var definition = CreateAsset<UnitDefinition>();
        // Exercise OnAfterDeserialize using the actual serialized fields.
        // Recognition of old asset names via FormerlySerializedAs needs an Editor asset-import test.
        JsonUtility.FromJsonOverwrite(
            "{\"legacy_maxHP\":32,\"legacy_currentHP\":12,\"legacy_attack\":9,\"legacy_defense\":7,\"legacy_moveRange\":6}",
            definition);
        Assert.That(definition.baseStats.maxHP, Is.EqualTo(32));
        Assert.That(definition.baseStats.currentHP, Is.EqualTo(12));
        Assert.That(definition.baseStats.attack, Is.EqualTo(9));
        Assert.That(definition.baseStats.defense, Is.EqualTo(7));
        Assert.That(definition.baseStats.resistance, Is.EqualTo(7));
        Assert.That(definition.baseStats.moveRange, Is.EqualTo(6));

        definition.ApplyTo(player);
        Assert.That(player.maxHP, Is.EqualTo(32));
        Assert.That(player.currentHP, Is.EqualTo(32), "ApplyTo spawns units at full health.");
        Assert.That(player.stats.resistance, Is.EqualTo(7));
        player.stats.attack = 1;
        Assert.That(definition.baseStats.attack, Is.EqualTo(9));

        // Once imported, stale legacy values must not overwrite subsequent edits.
        definition.baseStats.attack = 11;
        definition.OnAfterDeserialize();
        Assert.That(definition.baseStats.attack, Is.EqualTo(11));
    }

    [TestCase(49, 19, 11)] // Hit and critical: 3 damage tripled.
    [TestCase(49, 20, 17)] // A roll equal to crit chance is not a critical.
    [TestCase(50, 0, 20)]  // A roll equal to hit chance misses.
    public void Resolver_InjectedRolls_ControlHitAndCriticalBoundaries(int hit, int crit, int hp)
    {
        player.equippedWeapon.baseHit = 50;
        player.equippedWeapon.baseCrit = 20;
        enemy.equippedWeapon.baseHit = 0;
        var rolls = new Queue<int>(hit < 50 ? new[] { hit, crit, 99 } : new[] { hit, 99 });

        CombatResolver.ResolveCombat(player, enemy, () => rolls.Dequeue());

        Assert.That(enemy.currentHP, Is.EqualTo(hp));
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(rolls, Is.Empty, "Misses must not consume a critical roll.");
    }

    [Test]
    public void Resolver_SpecialSkipsCritRoll_AndFollowUpUsesOrdinaryDamage()
    {
        var special = CreateAsset<SpecialAttackData>();
        special.preventsCounter = true;
        special.damageMultiplier = 2;
        player.EquipSpecial(special);
        player.GainBurstPip(99);
        player.stats.speed = 4;
        player.equippedWeapon.baseCrit = 20;
        var rolls = new Queue<int>(new[] { 0, 0, 19 });

        CombatResolver.ResolveCombat(player, enemy, () => rolls.Dequeue(), useSpecial: true);

        Assert.That(enemy.currentHP, Is.EqualTo(5)); // Special 6, then ordinary critical 9.
        Assert.That(player.currentHP, Is.EqualTo(20));
        Assert.That(player.currentBurstPips, Is.EqualTo(1));
        Assert.That(rolls, Is.Empty);
    }

    [Test]
    public void Resolver_InvalidAttack_DoesNotRollOrGrantCharge()
    {
        player.EquipSpecial(CreateAsset<SpecialAttackData>());
        enemy.team = Team.Player;

        string log = CombatResolver.ResolveCombat(player, enemy, () =>
        {
            Assert.Fail("Invalid attacks must not roll.");
            return 0;
        });

        Assert.That(log, Is.Empty);
        Assert.That(player.currentBurstPips, Is.Zero);
        Assert.That(enemy.currentHP, Is.EqualTo(20));
    }
    [Test]
    public void GainExp_125LevelsBareUnitOnceAndLeaves25WithoutStatGains()
    {
        UnitStats before = player.stats;
        Assert.That(player.definition, Is.Null);
        Assert.That(player.currentClass, Is.Null);

        var results = player.GainExp(125);

        Assert.That(player.currentLevel, Is.EqualTo(2));
        Assert.That(player.currentExp, Is.EqualTo(25));
        Assert.That(player.stats, Is.EqualTo(before));
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Level, Is.EqualTo(2));
        Assert.That(results[0].Gains, Is.EqualTo(default(UnitStats)));
    }

    [Test]
    public void GainExp_250LevelsTwiceAndFiresTwoEvents()
    {
        int events = 0;
        player.OnLevelUp += (unit, gains) => events++;

        var results = player.GainExp(250);

        Assert.That(player.currentLevel, Is.EqualTo(3));
        Assert.That(player.currentExp, Is.EqualTo(50));
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(events, Is.EqualTo(2));
        Assert.That(results[0].Level, Is.EqualTo(2));
        Assert.That(results[1].Level, Is.EqualTo(3));
    }

    [Test]
    public void GainExp_AtMaxLevelLeavesLevelAndExpUnchanged()
    {
        player.currentLevel = player.MaxLevel;
        player.currentExp = 7;
        int events = 0;
        player.OnLevelUp += (unit, gains) => events++;

        Assert.That(player.GainExp(100), Is.Empty);

        Assert.That(player.currentLevel, Is.EqualTo(20));
        Assert.That(player.currentExp, Is.EqualTo(7));
        Assert.That(events, Is.Zero);
    }

    [Test]
    public void GainExp_StopsAtClassMaxLevelAndDiscardsRemainingExp()
    {
        player.currentClass = CreateAsset<ClassDefinition>();
        player.currentClass.maxLevel = 2;

        var results = player.GainExp(250);

        Assert.That(player.AtMaxLevel, Is.True);
        Assert.That(player.currentLevel, Is.EqualTo(2));
        Assert.That(player.currentExp, Is.Zero);
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void GainExp_NonPositiveAmountsDoNothing()
    {
        player.currentExp = 25;
        Assert.That(player.GainExp(0), Is.Empty);
        Assert.That(player.GainExp(-100), Is.Empty);
        Assert.That(player.currentLevel, Is.EqualTo(1));
        Assert.That(player.currentExp, Is.EqualTo(25));
    }

    [Test]
    public void LevelUp_AppliesClassGrowthAndHPBeforeHealthThenLevelEvents()
    {
        player.definition = CreateAsset<UnitDefinition>();
        player.definition.personalGrowths = default;
        player.currentClass = CreateAsset<ClassDefinition>();
        player.currentClass.growthModifiers = new StatGrowths
        {
            hp = 100, attack = 100, defense = 100, resistance = 100,
            speed = 100, skill = 100, luck = 100
        };
        player.currentHP = 10;
        var order = new List<string>();
        player.OnHPChanged += unit =>
        {
            Assert.That(unit.maxHP, Is.EqualTo(21));
            Assert.That(unit.currentHP, Is.EqualTo(11));
            Assert.That(unit.currentLevel, Is.EqualTo(2));
            order.Add("HP");
        };
        player.OnLevelUp += (unit, result) =>
        {
            Assert.That(result.Level, Is.EqualTo(2));
            Assert.That(result.Gains.maxHP, Is.EqualTo(1));
            Assert.That(result.Gains.currentHP, Is.EqualTo(1));
            Assert.That(result.Gains.moveRange, Is.Zero);
            order.Add("Level");
        };

        player.LevelUp(() => 1);

        Assert.That(order, Is.EqualTo(new[] { "HP", "Level" }));
    }

    [Test]
    public void LevelUp_WithoutClassUsesOnlyPersonalGrowthsAndRoll100()
    {
        player.definition = CreateAsset<UnitDefinition>();
        player.definition.personalGrowths = new StatGrowths { hp = 100, attack = 50 };
        Assert.That(player.currentClass, Is.Null);
        int attackBefore = player.attack;

        player.LevelUp(() => 100);

        Assert.That(player.maxHP, Is.EqualTo(21));
        Assert.That(player.currentHP, Is.EqualTo(21));
        Assert.That(player.attack, Is.EqualTo(attackBefore));
        Assert.That(player.currentLevel, Is.EqualTo(2));
    }

    [Test]
    public void ApplyTo_StoresDefinitionAndResetsProgression()
    {
        var definition = CreateAsset<UnitDefinition>();
        player.currentLevel = 9;
        player.currentExp = 75;

        definition.ApplyTo(player);

        Assert.That(player.definition, Is.SameAs(definition));
        Assert.That(player.currentLevel, Is.EqualTo(1));
        Assert.That(player.currentExp, Is.Zero);
    }

    [Test]
    public void LevelUp_AtMaxReturnsNullWithoutRollsEventsOrStateChanges()
    {
        player.currentLevel = player.MaxLevel;
        player.currentExp = 7;
        UnitStats before = player.stats;
        player.OnHPChanged += unit => Assert.Fail("No HP event at max level.");
        player.OnLevelUp += (unit, result) => Assert.Fail("No level event at max level.");

        var result = player.LevelUp(() => { Assert.Fail("No rolls at max level."); return 1; });

        Assert.That(result, Is.Null);
        Assert.That(player.stats, Is.EqualTo(before));
        Assert.That(player.currentLevel, Is.EqualTo(player.MaxLevel));
        Assert.That(player.currentExp, Is.EqualTo(7));
    }

    [Test]
    public void GainExp_HugeAwardStopsAtLimitWithoutOverflow()
    {
        player.currentExp = 99;
        var results = player.GainExp(int.MaxValue);
        Assert.That(results.Count, Is.EqualTo(19));
        Assert.That(player.currentLevel, Is.EqualTo(20));
        Assert.That(player.currentExp, Is.Zero);
    }

    [Test]
    public void LevelUp_UsesClassCapsAndReturnsThePublishedResult()
    {
        player.definition = CreateAsset<UnitDefinition>();
        player.definition.personalGrowths = new StatGrowths { hp = 300, attack = 100 };
        player.currentClass = CreateAsset<ClassDefinition>();
        player.currentClass.statCaps = new UnitStats { maxHP = 21, attack = player.attack };
        LevelUpResult? published = null;
        player.OnLevelUp += (unit, result) => published = result;

        var result = player.LevelUp(() => 100);

        Assert.That(result.HasValue, Is.True);
        Assert.That(result, Is.EqualTo(published));
        Assert.That(result.Value.Gains.maxHP, Is.EqualTo(1));
        Assert.That(result.Value.Gains.attack, Is.Zero);
    }

    [Test]
    public void GainExp_ForwardsGrowthRngToEveryLevelAndConsumesSevenRollsPerLevel()
    {
        player.definition = CreateAsset<UnitDefinition>();
        player.definition.personalGrowths = new StatGrowths
        {
            hp = 50, attack = 50, defense = 50, resistance = 50,
            speed = 50, skill = 50, luck = 50
        };
        var growthRolls = new Queue<int>(new[]
        {
            50, 50, 50, 50, 50, 50, 50,
            51, 51, 51, 51, 51, 51, 51
        });
        var before = player.stats;

        var results = player.GainExp(250, () => growthRolls.Dequeue());

        var gains = new UnitStats
        {
            maxHP = 1, currentHP = 1, attack = 1, defense = 1,
            resistance = 1, speed = 1, skill = 1, luck = 1
        };
        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0].Gains, Is.EqualTo(gains));
        Assert.That(results[1].Gains, Is.EqualTo(default(UnitStats)));
        Assert.That(player.stats, Is.EqualTo(before + gains));
        Assert.That(player.currentExp, Is.EqualTo(50));
        Assert.That(growthRolls, Is.Empty);
    }

    private void AssertEndTurnIsBlocked()
    {
        Assert.That(turns.IsPlayerActionInProgress, Is.True);
        Assert.That(turns.CanEndPlayerPhase, Is.False);
        turns.EndPhaseEarly(Team.Player);
        Assert.That(turns.CurrentPhase, Is.EqualTo(Team.Player));
        Assert.That(player.HasActed, Is.False);
    }

    private IEnumerator WaitForAction()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (turns.IsPlayerActionInProgress && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(turns.IsPlayerActionInProgress, Is.False, "Action did not finish within five seconds.");
    }

    // Only the input adapter is private. These tests run the real movement/attack
    // coroutines instead of setting the lock manually and testing a boolean in isolation.
    private object InvokeController(string method, params object[] args)
    {
        MethodInfo command = typeof(CombatController).GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(command, Is.Not.Null, "Controller command was renamed: " + method);
        return command.Invoke(controller, args);
    }

    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        return obj;
    }

    private Unit CreateUnit(string name, Team team, Vector2Int cell)
    {
        GameObject obj = Create(name);
        obj.transform.position = grid.CellToWorld(cell);
        Unit unit = obj.AddComponent<Unit>();
        unit.unitName = name;
        unit.team = team;
        unit.maxHP = unit.currentHP = 20;
        unit.equippedWeapon = CreateAsset<WeaponData>();
        unit.equippedWeapon.baseHit = 200; // Guarantee existing deterministic assertions.
        return unit;
    }
}
