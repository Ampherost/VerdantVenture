using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class StatAdditionTests
{
    [TestCase(1)]
    [TestCase(-1)]
    public void GrowthAddition_SumsEveryFieldWithoutClamping(int sign)
    {
        var a = new StatGrowths
        {
            hp = 60, attack = 40, defense = 30, resistance = 20,
            speed = 50, skill = 70, luck = 10
        };
        var b = new StatGrowths
        {
            hp = sign * 101, attack = sign * 102, defense = sign * 103,
            resistance = sign * 104, speed = sign * 105, skill = sign * 106, luck = sign * 107
        };
        var expected = sign > 0
            ? new StatGrowths { hp = 161, attack = 142, defense = 133, resistance = 124, speed = 155, skill = 176, luck = 117 }
            : new StatGrowths { hp = -41, attack = -62, defense = -73, resistance = -84, speed = -55, skill = -36, luck = -97 };

        Assert.That(a + b, Is.EqualTo(expected));
        Assert.That(a.hp, Is.EqualTo(60), "Adding value types must not mutate either input.");
        Assert.That(b.hp, Is.EqualTo(sign * 101));
    }

    [Test]
    public void GrowthAddition_DefaultStructIsIdentity()
    {
        var a = new StatGrowths { hp = 150, attack = -20, defense = 3, resistance = 4, speed = 5, skill = 6, luck = 7 };
        Assert.That(a + default(StatGrowths), Is.EqualTo(a));
        Assert.That(default(StatGrowths) + a, Is.EqualTo(a));
    }

    [TestCase(1)]
    [TestCase(-1)]
    public void StatAddition_SumsEveryFieldIncludingCurrentHPAndMovement(int sign)
    {
        var a = new UnitStats
        {
            maxHP = 20, currentHP = 11, attack = 5, defense = 2, resistance = 3,
            speed = 4, skill = 6, luck = 7, moveRange = 8
        };
        var b = new UnitStats
        {
            maxHP = sign * 31, currentHP = sign * 32, attack = sign * 33,
            defense = sign * 34, resistance = sign * 35, speed = sign * 36,
            skill = sign * 37, luck = sign * 38, moveRange = sign * 39
        };
        var expected = sign > 0
            ? new UnitStats { maxHP = 51, currentHP = 43, attack = 38, defense = 36, resistance = 38, speed = 40, skill = 43, luck = 45, moveRange = 47 }
            : new UnitStats { maxHP = -11, currentHP = -21, attack = -28, defense = -32, resistance = -32, speed = -32, skill = -31, luck = -31, moveRange = -31 };

        Assert.That(a + b, Is.EqualTo(expected));
        Assert.That(a.currentHP, Is.EqualTo(11));
        Assert.That(b.moveRange, Is.EqualTo(sign * 39));
    }

    [Test]
    public void StatAddition_DefaultStructIsIdentity()
    {
        var a = new UnitStats
        {
            maxHP = 30, currentHP = -2, attack = 5, defense = 3, resistance = 4,
            speed = 6, skill = 7, luck = 8, moveRange = -1
        };
        Assert.That(a + default(UnitStats), Is.EqualTo(a));
        Assert.That(default(UnitStats) + a, Is.EqualTo(a));
    }

    [Test]
    public void GrowthDefault_HasTheSpecifiedPercentages()
    {
        Assert.That(StatGrowths.Default, Is.EqualTo(new StatGrowths
        {
            hp = 60, attack = 40, defense = 30, resistance = 30,
            speed = 40, skill = 40, luck = 30
        }));
    }

    [Test]
    public void NewDefinition_StartsWithDefaultGrowths()
    {
        var definition = ScriptableObject.CreateInstance<UnitDefinition>();
        try
        {
            Assert.That(definition.personalGrowths, Is.EqualTo(StatGrowths.Default));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DefinitionImport_MissingGrowthsDefaultButAuthoredZeroGrowthsStayZero(bool omitGrowths)
    {
        string path = "Assets/StatGrowthsImportTest_" + System.Guid.NewGuid().ToString("N") + ".asset";
        try
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.personalGrowths = default(StatGrowths);
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            if (omitGrowths)
            {
                string yaml = System.IO.File.ReadAllText(path);
                string legacyYaml = System.Text.RegularExpressions.Regex.Replace(
                    yaml, @"(?m)^  personalGrowths:\r?\n(?:    [^\r\n]*\r?\n)*", "");
                Assert.That(legacyYaml, Is.Not.EqualTo(yaml), "The fixture must remove the serialized growth field.");
                System.IO.File.WriteAllText(path, legacyYaml);
            }
            Resources.UnloadAsset(definition);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            Assert.That(imported.personalGrowths,
                Is.EqualTo(omitGrowths ? StatGrowths.Default : default(StatGrowths)));
        }
        finally
        {
            AssetDatabase.DeleteAsset(path);
        }
    }
}
