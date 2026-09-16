using UnityEngine;

public enum ExitRoute { ReturnToOverworld, Retry, GameOver }

public struct ExitDecision
{
    public ExitRoute Route;
    public string SceneName;
}

/// <summary>Chooses an exit without loading scenes or changing the pending encounter.</summary>
public static class BattleExitRouter
{
    public static ExitDecision Decide(bool victory, EncounterData encounter,
        string currentSceneName, string returnSceneName)
    {
        DefeatAction defeatAction = encounter != null
            ? encounter.onDefeat : DefeatAction.ReturnToOverworld;
        if (!victory && defeatAction == DefeatAction.RetryBattle)
            return new ExitDecision { Route = ExitRoute.Retry, SceneName = currentSceneName };
        if (!victory && defeatAction == DefeatAction.LoadGameOverScene &&
            encounter != null && !string.IsNullOrWhiteSpace(encounter.gameOverSceneName))
            return new ExitDecision { Route = ExitRoute.GameOver, SceneName = encounter.gameOverSceneName };
        return new ExitDecision
        {
            Route = ExitRoute.ReturnToOverworld,
            SceneName = ResolveReturnScene(encounter, returnSceneName)
        };
    }

    private static string ResolveReturnScene(EncounterData encounter, string returnSceneName)
    {
        if (encounter != null && !string.IsNullOrWhiteSpace(encounter.returnSceneName))
            return encounter.returnSceneName;
        if (!string.IsNullOrWhiteSpace(returnSceneName))
            return returnSceneName;
        Debug.LogWarning("[BattleExitRouter] No return scene recorded — defaulting to OverworldScene.");
        return "OverworldScene";
    }
}
