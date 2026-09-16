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
    public void RestoreResetsHPAndClearsDeath()
    {
        member.currentHP = 0;
        member.isDead = true;
        PartyResultWriter.Restore(new Dictionary<PartyMember, int> { [member] = 12 });
        Assert.That(member.currentHP, Is.EqualTo(12));
        Assert.That(member.isDead, Is.False);
    }
}
