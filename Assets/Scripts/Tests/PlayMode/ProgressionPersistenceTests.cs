using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ProgressionPersistenceTests
{
    private readonly List<Object> objects = new List<Object>();
    private UnitDefinition definition;
    private GameData data;
    private EncounterData encounter;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        BattleLauncher.ClearPending();
        var prefab = NewObject("Inactive test prefab");
        prefab.SetActive(false);
        prefab.AddComponent<Unit>();
        definition = NewAsset<UnitDefinition>();
        definition.prefab = prefab;
        definition.personalGrowths = new StatGrowths { hp = 100, attack = 100 };
        var dataRoot = NewObject("Progression test data");
        dataRoot.SetActive(false);
        data = dataRoot.AddComponent<GameData>();
        data.startingRoster.Add(definition);
        dataRoot.SetActive(true);
        encounter = NewAsset<EncounterData>();
        encounter.playerSpawnCells.Add(Vector2Int.zero);
    }

    [TearDown]
    public void TearDown()
    {
        BattleLauncher.ClearPending();
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test]
    public void CombatEndThenSecondSpawnPreservesLevelExpStatsClassAndHP()
    {
        var cls = NewAsset<ClassDefinition>();
        var first = Spawn();
        Unit unit = PartyUnit(first);
        unit.currentClass = cls;
        unit.GainExp(150, () => 100);
        unit.currentHP = 7;
        UnitStats grown = unit.stats;
        FinishBattle(first, Team.Player);
        PartyMember member = data.Party[0];
        Assert.That(member.level, Is.EqualTo(2));
        Assert.That(member.exp, Is.EqualTo(50));
        Assert.That(member.stats, Is.EqualTo(grown));
        Assert.That(member.currentClass, Is.SameAs(cls));
        Assert.That(member.currentHP, Is.EqualTo(7));

        Unit respawned = PartyUnit(Spawn());
        Assert.That(respawned.currentLevel, Is.EqualTo(2));
        Assert.That(respawned.currentExp, Is.EqualTo(50));
        Assert.That(respawned.stats, Is.EqualTo(grown));
        Assert.That(respawned.currentClass, Is.SameAs(cls));
        Assert.That(respawned.definition, Is.SameAs(definition));
    }

    [Test]
    public void FallenMemberKeepsGainsAndRevivesAtGrownMaxHP()
    {
        data.permadeath = false;
        data.reviveHP = 999;
        var deployment = Spawn();
        Unit unit = PartyUnit(deployment);
        unit.GainExp(150, () => 100);
        unit.currentHP = 0;
        FinishBattle(deployment, Team.Player);
        PartyMember member = data.Party[0];
        Assert.That(member.level, Is.EqualTo(2));
        Assert.That(member.exp, Is.EqualTo(50));
        Assert.That(member.stats.attack, Is.EqualTo(definition.baseStats.attack + 1));
        Assert.That(member.currentHP, Is.EqualTo(definition.baseStats.maxHP + 1));
        Assert.That(member.isDead, Is.False);
        Assert.That(PartyUnit(Spawn()).currentHP, Is.EqualTo(member.currentHP));
    }

    [TestCase(0)] // Explicit Retry.
    [TestCase(1)] // Continue with DefeatAction.RetryBattle.
    [TestCase(2)] // Automatic delay completing with DefeatAction.RetryBattle.
    public void RetryRoutesRestoreExactPreBattleStateBeforeSceneLoad(int route)
    {
        encounter.onDefeat = DefeatAction.RetryBattle;
        var deployment = Spawn();
        PartyMember member = data.Party[0];
        var before = deployment.snapshotBeforeBattle[member];
        Unit unit = PartyUnit(deployment);
        unit.GainExp(250, () => 100);
        unit.currentClass = NewAsset<ClassDefinition>();
        unit.currentHP = 0;
        BattleRunner runner = FinishBattle(deployment, Team.Enemy);
        Assert.That(member.level, Is.EqualTo(3));

        // Check the synchronous restoration before Unity processes the requested
        // scene load. This runs the real exit route, not a substitute restore call.
        if (route == 0) runner.Retry();
        else if (route == 1) runner.ReturnNow();
        else
        {
            var delay = (IEnumerator)typeof(BattleRunner).GetMethod("ReturnAfterDelay", PrivateInstance)
                .Invoke(runner, null);
            Assert.That(delay.MoveNext(), Is.True);
            Assert.That(delay.Current, Is.TypeOf<WaitForSeconds>());
            Assert.That(delay.MoveNext(), Is.False);
        }
        Assert.That(member.CaptureSnapshot(), Is.EqualTo(before));
    }

    [Test]
    public void EnemyLevelTenUsesNineExpectedLevelsIdenticallyAcrossSpawns()
    {
        definition.personalGrowths = new StatGrowths { hp = 50 };
        var cls = NewAsset<ClassDefinition>();
        encounter.enemies.Add(new EncounterData.EnemySpawn { definition = definition,
            level = 10, classOverride = cls, cell = Vector2Int.one });
        Unit first = EnemyUnit(Spawn());
        Unit second = EnemyUnit(Spawn());
        Assert.That(first.currentLevel, Is.EqualTo(10));
        Assert.That(first.maxHP, Is.EqualTo(definition.baseStats.maxHP + 5));
        Assert.That(first.currentHP, Is.EqualTo(first.maxHP));
        Assert.That(first.currentClass, Is.SameAs(cls));
        Assert.That(second.stats, Is.EqualTo(first.stats));
        Assert.That(second.currentLevel, Is.EqualTo(first.currentLevel));
    }

    [Test]
    public void EnemyLevelOneAppliesClassOverrideWithoutGrowth()
    {
        definition.defaultClass = NewAsset<ClassDefinition>();
        var cls = NewAsset<ClassDefinition>();
        cls.growthModifiers = new StatGrowths { hp = 1000 };
        encounter.enemies.Add(new EncounterData.EnemySpawn { definition = definition,
            level = 1, classOverride = cls, cell = Vector2Int.one });
        Unit enemy = EnemyUnit(Spawn());
        Assert.That(enemy.currentClass, Is.SameAs(cls));
        Assert.That(enemy.currentLevel, Is.EqualTo(1));
        Assert.That(enemy.maxHP, Is.EqualTo(definition.baseStats.maxHP));
        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EncounterValidationWarnsAboveResolvedClassLimit(bool useOverride)
    {
        encounter.name = "Level warning test";
        var cls = NewAsset<ClassDefinition>();
        cls.maxLevel = 5;
        if (!useOverride) definition.defaultClass = cls;
        encounter.enemies.Add(new EncounterData.EnemySpawn { definition = definition,
            level = 6, classOverride = useOverride ? cls : null });
        LogAssert.Expect(LogType.Warning,
            "[Encounter] 'Level warning test' has an enemy at level 6, above its class maximum of 5.");
        Assert.That(encounter.Validate(), Is.True, "An over-level spawn is a warning, not a launch error.");
    }

    private Deployment Spawn()
    {
        var deployment = BattleSpawner.Spawn(encounter, data);
        foreach (var pair in deployment.pendingPlacement) objects.Add(pair.Key.gameObject);
        return deployment;
    }

    private static Unit PartyUnit(Deployment deployment)
    {
        foreach (var pair in deployment.deployed) return pair.Key;
        throw new AssertionException("No party unit spawned.");
    }

    private static Unit EnemyUnit(Deployment deployment)
    {
        foreach (var pair in deployment.pendingPlacement)
            if (pair.Key.team == Team.Enemy) return pair.Key;
        throw new AssertionException("No enemy unit spawned.");
    }

    private BattleRunner FinishBattle(Deployment deployment, Team winner)
    {
        var runner = NewObject("Progression test runner").AddComponent<BattleRunner>();
        runner.enabled = false;
        runner.autoReturn = false;
        typeof(BattleRunner).GetField("deployment", PrivateInstance).SetValue(runner, deployment);
        typeof(BattleRunner).GetField("encounter", PrivateInstance).SetValue(runner, encounter);
        typeof(BattleRunner).GetMethod("HandleCombatEnd", PrivateInstance).Invoke(runner, new object[] { (Team?)winner });
        return runner;
    }

    private GameObject NewObject(string name)
    {
        var root = new GameObject(name);
        objects.Add(root);
        return root;
    }

    private T NewAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        objects.Add(asset);
        return asset;
    }
}
