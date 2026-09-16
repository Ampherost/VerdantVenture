using NUnit.Framework;
using UnityEngine;

public class ObjectiveFactoryTests
{
    private GameObject root;
    private EncounterData encounter;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Objective test root");
        encounter = ScriptableObject.CreateInstance<EncounterData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(encounter);
    }

    [Test]
    public void BossIsConfiguredBeforeAwakeAndResolvesWhenThatUnitFalls()
    {
        var bossObject = new GameObject("Boss");
        bossObject.transform.SetParent(root.transform);
        bossObject.SetActive(false);
        var boss = bossObject.AddComponent<Unit>();
        boss.currentHP = 10;
        encounter.victory = VictoryCondition.DefeatBoss;

        ObjectiveFactory.Install(encounter, new Deployment { Boss = boss }, root.transform);

        var objective = root.GetComponentInChildren<DefeatBossObjective>();
        Assert.That(objective, Is.Not.Null);
        Assert.That(objective.isActiveAndEnabled, Is.True);
        Assert.That(objective.boss, Is.SameAs(boss));
        Assert.That(objective.IsResolved(null, out _), Is.False);
        boss.currentHP = 0;
        Assert.That(objective.IsResolved(null, out Team? winner), Is.True);
        Assert.That(winner, Is.EqualTo(Team.Player));
    }

    [Test]
    public void SurvivalObjectiveUsesEncounterRoundCount()
    {
        encounter.victory = VictoryCondition.SurviveRounds;
        encounter.roundsToSurvive = 7;
        ObjectiveFactory.Install(encounter, new Deployment(), root.transform);
        var objective = root.GetComponentInChildren<SurviveRoundsObjective>();
        Assert.That(objective.rounds, Is.EqualTo(7));
        Assert.That(objective.survivingTeam, Is.EqualTo(Team.Player));
        Assert.That(objective.isActiveAndEnabled, Is.True);
    }

    [Test]
    public void RoutUsesTurnManagerWithoutAddingAnObjective()
    {
        encounter.victory = VictoryCondition.RoutEnemies;
        ObjectiveFactory.Install(encounter, new Deployment(), root.transform);
        Assert.That(root.GetComponentsInChildren<CombatObjective>(), Is.Empty);
    }
}
