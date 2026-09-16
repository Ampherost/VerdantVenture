using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BattleExitRouterTests
{
    private EncounterData encounter;

    [SetUp]
    public void SetUp()
    {
        encounter = ScriptableObject.CreateInstance<EncounterData>();
        encounter.returnSceneName = "EncounterReturn";
        encounter.gameOverSceneName = "GameOver";
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(encounter);

    [TestCase(DefeatAction.ReturnToOverworld, ExitRoute.ReturnToOverworld, "EncounterReturn")]
    [TestCase(DefeatAction.RetryBattle, ExitRoute.Retry, "CurrentBattle")]
    [TestCase(DefeatAction.LoadGameOverScene, ExitRoute.GameOver, "GameOver")]
    public void DefeatFollowsConfiguredAction(DefeatAction action, ExitRoute route, string scene)
    {
        encounter.onDefeat = action;
        AssertDecision(false, route, scene);
    }

    [TestCase(DefeatAction.ReturnToOverworld)]
    [TestCase(DefeatAction.RetryBattle)]
    [TestCase(DefeatAction.LoadGameOverScene)]
    public void VictoryAlwaysReturnsToOverworld(DefeatAction action)
    {
        encounter.onDefeat = action;
        AssertDecision(true, ExitRoute.ReturnToOverworld, "EncounterReturn");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void EmptyGameOverNameFallsBackToReturn(string scene)
    {
        encounter.onDefeat = DefeatAction.LoadGameOverScene;
        encounter.gameOverSceneName = scene;
        AssertDecision(false, ExitRoute.ReturnToOverworld, "EncounterReturn");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void EmptyEncounterReturnUsesGameDataReturn(string scene)
    {
        encounter.returnSceneName = scene;
        AssertDecision(false, ExitRoute.ReturnToOverworld, "SavedReturn");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void EmptyReturnNamesUseOverworldScene(string scene)
    {
        encounter.returnSceneName = scene;
        LogAssert.Expect(LogType.Warning,
            "[BattleExitRouter] No return scene recorded — defaulting to OverworldScene.");
        var decision = BattleExitRouter.Decide(false, encounter, "CurrentBattle", scene);
        Assert.That(decision.Route, Is.EqualTo(ExitRoute.ReturnToOverworld));
        Assert.That(decision.SceneName, Is.EqualTo("OverworldScene"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StandaloneUsesSavedReturn(bool victory)
    {
        var decision = BattleExitRouter.Decide(victory, null, "CurrentBattle", "SavedReturn");
        Assert.That(decision.Route, Is.EqualTo(ExitRoute.ReturnToOverworld));
        Assert.That(decision.SceneName, Is.EqualTo("SavedReturn"));
    }

    [Test]
    public void StandaloneWithoutGameDataUsesDefault()
    {
        LogAssert.Expect(LogType.Warning,
            "[BattleExitRouter] No return scene recorded — defaulting to OverworldScene.");
        var decision = BattleExitRouter.Decide(false, null, "CurrentBattle", null);
        Assert.That(decision.Route, Is.EqualTo(ExitRoute.ReturnToOverworld));
        Assert.That(decision.SceneName, Is.EqualTo("OverworldScene"));
    }

    private void AssertDecision(bool victory, ExitRoute route, string scene)
    {
        var decision = BattleExitRouter.Decide(victory, encounter, "CurrentBattle", "SavedReturn");
        Assert.That(decision.Route, Is.EqualTo(route));
        Assert.That(decision.SceneName, Is.EqualTo(scene));
    }
}
