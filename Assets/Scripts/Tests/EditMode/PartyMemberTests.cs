using NUnit.Framework;
using UnityEngine;

public class PartyMemberTests
{
    private UnitDefinition definition;
    private ClassDefinition cls;
    private GameObject root;
    private Unit unit;

    [SetUp]
    public void SetUp()
    {
        definition = ScriptableObject.CreateInstance<UnitDefinition>();
        cls = ScriptableObject.CreateInstance<ClassDefinition>();
        definition.defaultClass = cls;
        definition.baseStats.maxHP = 30;
        root = new GameObject("Party member test unit");
        root.SetActive(false);
        unit = root.AddComponent<Unit>();
        definition.prefab = root;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(cls);
    }

    [Test]
    public void LegacySerializedMember_SeedsProgressionBeforeMinusOneHP()
    {
        var member = new PartyMember { definition = definition };
        JsonUtility.FromJsonOverwrite("{\"currentHP\":-1}", member);
        Assert.That(member.level, Is.Zero);
        Assert.That(member.stats.maxHP, Is.Zero);
        member.EnsureInitialised();
        Assert.That(member.stats, Is.EqualTo(definition.baseStats));
        Assert.That(member.currentClass, Is.SameAs(cls));
        Assert.That(member.level, Is.EqualTo(1));
        Assert.That(member.exp, Is.Zero);
        Assert.That(member.currentHP, Is.EqualTo(30));
        Assert.That(member.IsDeployable, Is.True);
    }

    [Test]
    public void ReinitializationPreservesGrownStateAndClampsToGrownMaxHP()
    {
        var member = new PartyMember { definition = definition, level = 5, exp = 42,
            stats = new UnitStats { maxHP = 45, attack = 12 }, currentHP = 99 };
        member.EnsureInitialised();
        Assert.That(member.level, Is.EqualTo(5));
        Assert.That(member.exp, Is.EqualTo(42));
        Assert.That(member.stats.attack, Is.EqualTo(12));
        Assert.That(member.currentClass, Is.Null, "An intentional classless state is not reseeded.");
        Assert.That(member.currentHP, Is.EqualTo(45));
        member.currentHP = 4;
        member.FullHeal();
        Assert.That(member.currentHP, Is.EqualTo(45));
    }

    [Test]
    public void ApplyToUsesOwnedHPAndReadBackNeverChangesOwnedHP()
    {
        var member = new PartyMember { definition = definition, level = 3, exp = 70,
            currentClass = cls, currentHP = 9, stats = new UnitStats { maxHP = 40, currentHP = 999, attack = 11 } };
        member.ApplyTo(unit);
        Assert.That(unit.currentHP, Is.EqualTo(9));
        Assert.That(unit.maxHP, Is.EqualTo(40));
        Assert.That(unit.definition, Is.SameAs(definition));
        Assert.That(unit.currentClass, Is.SameAs(cls));
        Assert.That(unit.currentLevel, Is.EqualTo(3));
        Assert.That(unit.currentExp, Is.EqualTo(70));
        unit.currentHP = 2;
        unit.currentLevel = 4;
        unit.currentExp = 5;
        unit.stats.attack = 13;
        unit.currentClass = null;
        member.ReadBackFrom(unit);
        Assert.That(member.currentHP, Is.EqualTo(9));
        Assert.That(member.level, Is.EqualTo(4));
        Assert.That(member.exp, Is.EqualTo(5));
        Assert.That(member.stats, Is.EqualTo(unit.stats));
        Assert.That(member.currentClass, Is.Null);
    }

    [Test]
    public void SnapshotRestoresAllFieldsIncludingTrueDeathAndBenchedState()
    {
        var member = new PartyMember { definition = definition, level = 4, exp = 65,
            currentClass = cls, currentHP = 0, stats = new UnitStats { maxHP = 45, currentHP = 123 },
            isDead = true, inActiveParty = false };
        var snapshot = member.CaptureSnapshot();
        member.definition = null;
        member.level = 9;
        member.exp = 99;
        member.currentClass = null;
        member.currentHP = 10;
        member.stats = UnitStats.Default;
        member.isDead = false;
        member.inActiveParty = true;
        member.RestoreSnapshot(snapshot);
        Assert.That(member.CaptureSnapshot(), Is.EqualTo(snapshot));
    }
}
