using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ClassDefinitionTests
{
    private readonly List<Object> objects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }

    [Test]
    public void NewClass_HasPermissiveCapsAndDefaultLevelingData()
    {
        var cls = Create<ClassDefinition>();
        Assert.That(cls.className, Is.EqualTo("Class"));
        Assert.That(cls.tier, Is.EqualTo(ClassTier.Base));
        Assert.That(cls.maxLevel, Is.EqualTo(20));
        Assert.That(cls.promotionLevel, Is.EqualTo(10));
        Assert.That(cls.statCaps, Is.EqualTo(new UnitStats
        {
            maxHP = 99, attack = 99, defense = 99, resistance = 99,
            speed = 99, skill = 99, luck = 99, currentHP = 0, moveRange = 0
        }));
        Assert.That(cls.growthModifiers, Is.EqualTo(default(StatGrowths)));
        Assert.That(cls.promotionBonuses, Is.EqualTo(default(UnitStats)));
        Assert.That(cls.promotionOptions, Is.Empty);
    }

    [Test]
    public void ClassSwap_ChangesCombinedGrowthsWithoutChangingPersonalGrowths()
    {
        var definition = Create<UnitDefinition>();
        var first = Create<ClassDefinition>();
        var second = Create<ClassDefinition>();
        first.growthModifiers = new StatGrowths { attack = 15, speed = -5 };
        second.growthModifiers = new StatGrowths { attack = -10, speed = 20 };
        definition.defaultClass = first;
        string before = JsonUtility.ToJson(definition.personalGrowths);
        Unit unit = CreateUnit();
        definition.ApplyTo(unit);

        StatGrowths initial = ClassDefinition.CombinedGrowths(definition, unit.currentClass);
        unit.currentClass = second;
        StatGrowths swapped = ClassDefinition.CombinedGrowths(definition, unit.currentClass);

        Assert.That(initial.attack, Is.EqualTo(55));
        Assert.That(initial.speed, Is.EqualTo(35));
        Assert.That(swapped.attack, Is.EqualTo(30));
        Assert.That(swapped.speed, Is.EqualTo(60));
        Assert.That(JsonUtility.ToJson(definition.personalGrowths), Is.EqualTo(before));
        Assert.That(definition.defaultClass, Is.SameAs(first));
    }

    [Test]
    public void ApplyTo_CopiesClassReferenceWithoutApplyingPromotionBonuses()
    {
        var definition = Create<UnitDefinition>();
        var cls = Create<ClassDefinition>();
        cls.className = "Vanguard";
        cls.promotionBonuses = new UnitStats { attack = 50, maxHP = 50 };
        definition.defaultClass = cls;
        Unit unit = CreateUnit();

        definition.ApplyTo(unit);

        Assert.That(unit.currentClass, Is.SameAs(cls));
        Assert.That(unit.ClassName, Is.EqualTo("Vanguard"));
        Assert.That(unit.stats.attack, Is.EqualTo(definition.baseStats.attack));
        Assert.That(unit.maxHP, Is.EqualTo(definition.baseStats.maxHP));

        definition.defaultClass = null;
        definition.ApplyTo(unit);
        Assert.That(unit.currentClass, Is.Null, "A classless template must clear a stale class.");
        Assert.That(unit.ClassName, Is.EqualTo("—"));
        Assert.DoesNotThrow(() => definition.ApplyTo(null));
    }

    [Test]
    public void AuthoredClasses_HaveValidPromotionReferencesAndCaps()
    {
        var squire = AssetDatabase.LoadAssetAtPath<ClassDefinition>("Assets/Data/Classes/Squire.asset");
        var vanguard = AssetDatabase.LoadAssetAtPath<ClassDefinition>("Assets/Data/Classes/Vanguard.asset");
        var tracker = AssetDatabase.LoadAssetAtPath<ClassDefinition>("Assets/Data/Classes/Tracker.asset");
        Assert.That(squire, Is.Not.Null);
        Assert.That(vanguard, Is.Not.Null);
        Assert.That(tracker, Is.Not.Null);
        Assert.That(squire.promotionOptions, Is.EqualTo(new[] { vanguard, tracker }));
        Assert.That(vanguard.promotionOptions, Is.Empty);
        Assert.That(tracker.promotionOptions, Is.Empty);
        foreach (var cls in new[] { squire, vanguard, tracker })
            Assert.That(cls.statCaps, Is.EqualTo(UnitStats.MaxCaps));
    }

    [Test]
    public void EveryAuthoredUnitDefinition_HasADefaultClass()
    {
        string[] guids = AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets" });
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            Assert.That(definition.defaultClass, Is.Not.Null, path);
        }
    }

    private T Create<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        objects.Add(asset);
        return asset;
    }

    private Unit CreateUnit()
    {
        var root = new GameObject("Class test unit");
        root.SetActive(false);
        objects.Add(root);
        return root.AddComponent<Unit>();
    }
}
