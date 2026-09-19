using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BattleRunnerExitGuardTests
{
    private GameObject root;
    private BattleRunner runner;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Exit guard test");
        runner = root.AddComponent<BattleRunner>();
        runner.enabled = false; // These synchronous tests do not start a combat scene.
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void ContinueBeforeResolutionDoesNotStartLeaving()
    {
        LogAssert.Expect(LogType.Warning, "[BattleRunner] ReturnNow was called before the battle resolved.");
        runner.ReturnNow();
        Assert.That(typeof(BattleRunner).GetField("leaving", PrivateInstance).GetValue(runner), Is.False);
    }

    [Test]
    public void DelayCompletionAndButtonsDoNothingAfterExitHasStarted()
    {
        var member = new PartyMember { currentHP = 2 };
        var deployment = (Deployment)typeof(BattleRunner).GetField("deployment", PrivateInstance).GetValue(runner);
        deployment.snapshotBeforeBattle[member] = new PartyMember { currentHP = 12 }.CaptureSnapshot();
        var delay = (IEnumerator)typeof(BattleRunner).GetMethod("ReturnAfterDelay", PrivateInstance)
            .Invoke(runner, null);
        Assert.That(delay.MoveNext(), Is.True);
        Assert.That(delay.Current, Is.TypeOf<WaitForSeconds>());

        // Simulate a HUD exit having started while the automatic return is waiting.
        typeof(BattleRunner).GetField("resultsWritten", PrivateInstance).SetValue(runner, true);
        typeof(BattleRunner).GetField("leaving", PrivateInstance).SetValue(runner, true);
        Assert.That(delay.MoveNext(), Is.False);
        runner.ReturnNow();
        runner.Retry();
        Assert.That(member.currentHP, Is.EqualTo(2), "A second exit must not even restore party HP.");
        LogAssert.NoUnexpectedReceived();
    }
}
