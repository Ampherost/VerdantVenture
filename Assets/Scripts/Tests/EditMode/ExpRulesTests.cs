using NUnit.Framework;

public class ExpRulesTests
{
    [TestCase(5, 5, 10)]
    [TestCase(10, 5, 1)]
    [TestCase(5, 10, 25)]
    public void HitExp_ScalesByLevelDifferenceWithMinimum(int attacker, int defender, int expected)
    {
        Assert.That(ExpRules.ForHit(attacker, defender), Is.EqualTo(expected));
    }

    [TestCase(5, 5, 30)]
    [TestCase(10, 5, 5)]
    [TestCase(5, 10, 60)]
    public void KillExp_UsesDoubleLevelScalingWithMinimum(int attacker, int defender, int expected)
    {
        Assert.That(ExpRules.ForKill(attacker, defender), Is.EqualTo(expected));
    }

    [Test]
    public void Exchange_TwoHitsAndKillAddsBothHitAwardsAndKillBonus()
    {
        Assert.That(ExpRules.ForExchange(5, 5, 2, true), Is.EqualTo(50));
        Assert.That(ExpRules.ForExchange(5, 10, 2, true), Is.EqualTo(110));
    }

    [Test]
    public void Exchange_HitsWithoutKillOnlyAwardHitExp()
    {
        Assert.That(ExpRules.ForExchange(5, 5, 2, false), Is.EqualTo(20));
    }

    [Test]
    public void Exchange_WhiffAwardsFlatMinimumRegardlessOfLevelDifference()
    {
        Assert.That(ExpRules.ForExchange(1, 20, 0, false), Is.EqualTo(ExpRules.MinHitExp));
        Assert.That(ExpRules.ForExchange(20, 1, 0, false), Is.EqualTo(ExpRules.MinHitExp));
    }

    [Test]
    public void Exchange_LargeLevelsAndHitCountsDoNotOverflow()
    {
        Assert.That(ExpRules.ForHit(int.MaxValue, int.MinValue), Is.EqualTo(ExpRules.MinHitExp));
        Assert.That(ExpRules.ForKill(int.MinValue, int.MaxValue), Is.EqualTo(int.MaxValue));
        Assert.That(ExpRules.ForExchange(1, int.MaxValue, int.MaxValue, true), Is.EqualTo(int.MaxValue));
    }
}
