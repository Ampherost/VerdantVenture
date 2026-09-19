using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PartyResultWriterTests
{
    private GameObject unitObject;
    private Unit unit;
    private UnitDefinition definition;
    private PartyMember member;
    private Dictionary<Unit, PartyMember> deployed;

    [SetUp]
    public void SetUp()
    {
        unitObject = new GameObject("Result test unit");
        unitObject.SetActive(false);
        unit = unitObject.AddComponent<Unit>();
        definition = ScriptableObject.CreateInstance<UnitDefinition>();
        definition.maxHP = 20;
        member = new PartyMember { definition = definition, currentHP = 12 };
        deployed = new Dictionary<Unit, PartyMember> { [unit] = member };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(unitObject);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void DefeatNeverKillsPermanently()
    {
        unit.currentHP = 0;
        PartyResultWriter.Write(deployed, false, new PartyRules { permadeath = true, reviveHP = 3 }, null);
        Assert.That(member.isDead, Is.False);
        Assert.That(member.currentHP, Is.EqualTo(3));
    }

    [Test]
    public void VictoryWithPermadeathMarksCasualtyDead()
    {
        unit.currentHP = 0;
        PartyResultWriter.Write(deployed, true, new PartyRules { permadeath = true, reviveHP = 3 }, null);
        Assert.That(member.isDead, Is.True);
        Assert.That(member.currentHP, Is.Zero);
    }

    [TestCase(-5, 1)]
    [TestCase(0, 1)]
    [TestCase(7, 7)]
    [TestCase(99, 20)]
    public void ReviveHPIsClamped(int reviveHP, int expected)
    {
        unit.currentHP = -4;
        PartyResultWriter.Write(deployed, true, new PartyRules { reviveHP = reviveHP }, null);
        Assert.That(member.currentHP, Is.EqualTo(expected));
        Assert.That(member.isDead, Is.False);
    }

    [TestCase(false, false, 4)]
    [TestCase(true, true, 0)]
    public void DestroyedUnitCountsAsCasualty(bool victory, bool dead, int hp)
    {
        Object.DestroyImmediate(unitObject);
        Assert.That(unit == null, Is.True);
        PartyResultWriter.Write(deployed, victory, new PartyRules { permadeath = true, reviveHP = 4 }, null);
        Assert.That(member.isDead, Is.EqualTo(dead));
        Assert.That(member.currentHP, Is.EqualTo(hp));
    }

    [TestCase(false, false, 0)]
    [TestCase(false, true, 0)]
    [TestCase(true, false, 0)]
    [TestCase(true, true, 1)]
    public void HealOnlyRunsAfterVictoryWhenEnabled(bool victory, bool healAfterBattle, int expectedCalls)
    {
        unit.currentHP = 6;
        int calls = 0;
        var benched = new PartyMember { definition = definition, currentHP = 2, inActiveParty = false };
        PartyResultWriter.Write(deployed, victory, new PartyRules { healAfterBattle = healAfterBattle }, () =>
        {
            Assert.That(member.currentHP, Is.EqualTo(6), "Write HP before healing the roster.");
            calls++;
            member.FullHeal();
            benched.FullHeal();
        });
        Assert.That(calls, Is.EqualTo(expectedCalls));
        Assert.That(member.currentHP, Is.EqualTo(expectedCalls == 1 ? 20 : 6));
        Assert.That(benched.currentHP, Is.EqualTo(expectedCalls == 1 ? 20 : 2));
    }

    [Test]
    public void HealingDoesNotRevivePermanentCasualties()
    {
        unit.currentHP = 0;
        PartyResultWriter.Write(deployed, true,
            new PartyRules { permadeath = true, healAfterBattle = true }, member.FullHeal);
        Assert.That(member.isDead, Is.True);
        Assert.That(member.currentHP, Is.Zero);
    }

    [Test]
    public void RestoreResetsAllProgressionAndRestoresSavedDeathFlag()
    {
        member.EnsureInitialised();
        member.level = 3;
        member.exp = 25;
        member.stats.maxHP = 30;
        var before = member.CaptureSnapshot();
        member.currentHP = 0;
        member.isDead = true;
        member.level = 6;
        member.exp = 75;
        member.stats.maxHP = 50;
        PartyResultWriter.Restore(new Dictionary<PartyMember, PartyMember.Snapshot> { [member] = before });
        Assert.That(member.currentHP, Is.EqualTo(12));
        Assert.That(member.isDead, Is.False);
        Assert.That(member.level, Is.EqualTo(3));
        Assert.That(member.exp, Is.EqualTo(25));
        Assert.That(member.stats, Is.EqualTo(before.stats));
    }

    [Test]
    public void ReviveUsesGrownMaxHPAndKeepsCasualtyProgression()
    {
        unit.stats.maxHP = 35;
        unit.currentHP = 0;
        unit.currentLevel = 4;
        unit.currentExp = 50;
        PartyResultWriter.Write(deployed, true, new PartyRules { reviveHP = 99 }, null);
        Assert.That(member.MaxHP, Is.EqualTo(35));
        Assert.That(member.currentHP, Is.EqualTo(35));
        Assert.That(member.level, Is.EqualTo(4));
        Assert.That(member.exp, Is.EqualTo(50));
        Assert.That(member.isDead, Is.False);
    }

    [Test]
    public void PermadeathRecordsProgressionBeforeMarkingCasualtyDead()
    {
        unit.stats.attack = 12;
        unit.currentLevel = 3;
        unit.currentExp = 40;
        unit.currentHP = 0;
        PartyResultWriter.Write(deployed, true, new PartyRules { permadeath = true }, null);
        Assert.That(member.stats.attack, Is.EqualTo(12));
        Assert.That(member.level, Is.EqualTo(3));
        Assert.That(member.exp, Is.EqualTo(40));
        Assert.That(member.isDead, Is.True);
    }
}
