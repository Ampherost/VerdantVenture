using System;
using NUnit.Framework;

public class LevelUpResolverTests
{
    [Test]
    public void ZeroGrowths_ProduceNoGainsForAnyValidRoll()
    {
        for (int roll = 1; roll <= 100; roll++)
            Assert.That(LevelUpResolver.RollGains(UnitStats.Default, default, default, () => roll),
                Is.EqualTo(default(UnitStats)));
    }

    [Test]
    public void HundredPercentGrowths_GuaranteeOnePointEvenOnRoll100()
    {
        Assert.That(LevelUpResolver.RollGains(default, Growths(100), default, () => 100),
            Is.EqualTo(Gains(1)));
    }

    [Test]
    public void HundredFiftyPercentGrowths_GrantTwoOn50AndOneOn51()
    {
        Assert.That(LevelUpResolver.RollGains(default, Growths(150), default, () => 50),
            Is.EqualTo(Gains(2)));
        Assert.That(LevelUpResolver.RollGains(default, Growths(150), default, () => 51),
            Is.EqualTo(Gains(1)));
    }

    [Test]
    public void NegativeGrowths_AreTreatedAsZero()
    {
        Assert.That(LevelUpResolver.RollGains(default, Growths(-20), default, () => 1),
            Is.EqualTo(default(UnitStats)));
    }

    [Test]
    public void CappedStats_GainNothingAtCapAndAtMostOneWhenOneBelow()
    {
        Assert.That(LevelUpResolver.RollGains(Gains(10), Growths(100), Gains(10), () => 1),
            Is.EqualTo(default(UnitStats)));
        Assert.That(LevelUpResolver.RollGains(Gains(9), Growths(300), Gains(10), () => 1),
            Is.EqualTo(Gains(1)));
    }

    [Test]
    public void ZeroCaps_DoNotClampAnyCombatStat()
    {
        Assert.That(LevelUpResolver.RollGains(Gains(100), Growths(300), default, () => 100),
            Is.EqualTo(Gains(3)));
    }

    [Test]
    public void HPGain_IsCopiedToCurrentHPAndMovementNeverGrowsRegardlessOfTheirCaps()
    {
        int calls = 0;
        var caps = new UnitStats { currentHP = 1, moveRange = 1 };
        UnitStats gains = LevelUpResolver.RollGains(UnitStats.Default, new StatGrowths { hp = 300 }, caps,
            () => { calls++; return 100; });

        Assert.That(gains, Is.EqualTo(new UnitStats { maxHP = 3, currentHP = 3 }));
        Assert.That(gains.moveRange, Is.Zero);
        Assert.That(calls, Is.EqualTo(7), "Only the seven combat stats consume rolls.");
    }

    [Test]
    public void ExpectedGains_TenLevelsRound45PercentToFiveAnd44PercentToFour()
    {
        Assert.That(LevelUpResolver.ExpectedGains(default, Growths(45), default, 10), Is.EqualTo(Gains(5)));
        Assert.That(LevelUpResolver.ExpectedGains(default, Growths(44), default, 10), Is.EqualTo(Gains(4)));
    }

    [Test]
    public void RollsOutsideOneThroughHundred_ThrowArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelUpResolver.RollGains(default, Growths(150), default, () => 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelUpResolver.RollGains(default, Growths(150), default, () => 101));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LevelUpResolver.RollGains(default, default, default, () => -1));
    }

    [Test]
    public void StatsAlreadyAboveCap_AreNeverReduced()
    {
        Assert.That(LevelUpResolver.RollGains(Gains(20), Growths(100), Gains(10), () => 1),
            Is.EqualTo(default(UnitStats)));
        Assert.That(LevelUpResolver.ExpectedGains(Gains(20), Growths(100), Gains(10), 10),
            Is.EqualTo(default(UnitStats)));
    }

    [Test]
    public void ExpectedGains_UseEachFieldsGrowthAndCapAndCopyOnlyHPToCurrentHP()
    {
        var growths = new StatGrowths { hp = 150, attack = 100, defense = 200, resistance = 300, speed = 400, skill = 500, luck = 600 };
        var caps = new UnitStats { maxHP = 12, attack = 11, defense = 13, resistance = 14, speed = 15, skill = 16, luck = 0, currentHP = 1, moveRange = 1 };
        var expected = new UnitStats { maxHP = 2, currentHP = 2, attack = 1, defense = 3, resistance = 4, speed = 5, skill = 6, luck = 60 };
        Assert.That(LevelUpResolver.ExpectedGains(Gains(10), growths, caps, 10), Is.EqualTo(expected));
    }

    [Test]
    public void ExpectedGains_NegativeGrowthAndNonPositiveLevelsProduceZero()
    {
        Assert.That(LevelUpResolver.ExpectedGains(default, Growths(-20), default, 10), Is.EqualTo(default(UnitStats)));
        Assert.That(LevelUpResolver.ExpectedGains(default, Growths(100), default, 0), Is.EqualTo(default(UnitStats)));
        Assert.That(LevelUpResolver.ExpectedGains(default, Growths(100), default, -1), Is.EqualTo(default(UnitStats)));
    }

    [Test]
    public void NullRng_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LevelUpResolver.RollGains(default, default, default, null));
    }

    private static StatGrowths Growths(int value) => new StatGrowths
    {
        hp = value, attack = value, defense = value, resistance = value,
        speed = value, skill = value, luck = value
    };

    private static UnitStats Gains(int value) => new UnitStats
    {
        maxHP = value, currentHP = value, attack = value, defense = value,
        resistance = value, speed = value, skill = value, luck = value, moveRange = 0
    };
}
