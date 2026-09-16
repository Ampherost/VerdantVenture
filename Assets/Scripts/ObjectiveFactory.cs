using UnityEngine;

public static class ObjectiveFactory
{
    /// <summary>
    /// Build the win condition for this encounter, if it needs one beyond the default
    /// "kill everything" rule TurnManager already applies.
    ///
    /// Objectives are created on an inactive child object first: AddComponent runs Awake
    /// immediately on an active object, and DefeatBossObjective disables itself when it
    /// wakes up with no boss assigned. Building it inactive lets us fill the field before
    /// its Awake ever runs.
    /// </summary>
    public static void Install(EncounterData encounter, Deployment deployment, Transform parent)
    {
        switch (encounter.victory)
        {
            case VictoryCondition.DefeatBoss:
                {
                    Unit boss = deployment.Boss;
                    if (boss == null)
                    {
                        Debug.LogError("[ObjectiveFactory] Defeat Boss encounter, but the boss didn't " +
                                       "spawn. Falling back to routing every enemy.", parent);
                        return;
                    }

                    var obj = CreateObjective<DefeatBossObjective>(parent, "Objective_DefeatBoss",
                        o => { o.boss = boss; o.winsWhenBossFalls = Team.Player; });
                    Register(obj);
                    break;
                }

            case VictoryCondition.SurviveRounds:
                {
                    var obj = CreateObjective<SurviveRoundsObjective>(parent, "Objective_Survive",
                        o => { o.rounds = encounter.roundsToSurvive; o.survivingTeam = Team.Player; });
                    Register(obj);
                    break;
                }

            case VictoryCondition.RoutEnemies:
            default:
                // TurnManager.autoEndWhenTeamWipedOut already covers this.
                break;
        }
    }

    private static T CreateObjective<T>(Transform parent, string objectName, System.Action<T> configure)
        where T : CombatObjective
    {
        var go = new GameObject(objectName);
        go.SetActive(false);                 // keeps Awake from firing early
        go.transform.SetParent(parent, false);

        T objective = go.AddComponent<T>();
        configure?.Invoke(objective);

        go.SetActive(true);                  // now Awake runs, with fields filled in
        return objective;
    }

    private static void Register(CombatObjective objective)
    {
        if (objective != null && TurnManager.Instance != null)
            TurnManager.Instance.RegisterObjective(objective);
    }
}
