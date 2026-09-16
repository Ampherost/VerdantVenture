using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the battle, then takes it apart again. Put one of these in the combat scene.
///
/// EXECUTION ORDER is the whole trick here, so it's set explicitly to -100:
///
///   Awake (-100)  spawn units. Must beat TurnManager.Awake (0), which sweeps the scene
///                 with FindObjectsByType<Unit> to build its roster. Anything spawned
///                 after that sweep would never take a turn.
///
///   Start (-100)  set unit positions. Must beat Unit.Start (0) -> SnapToGrid(), which
///                 reads transform.position to work out which cell to claim. By Start,
///                 every Awake has run, so GridManager has finished aligning itself to the
///                 floor tilemap and CellToWorld gives the right answer.
///
/// We deliberately do NOT call Unit.PlaceAt ourselves: SnapToGrid would then see the cell
/// as occupied (by the very unit standing on it) and nudge the unit off it.
///
/// With no encounter launched — i.e. you pressed Play directly in the combat scene — this
/// leaves whatever units you hand-placed alone, so the scene still works standalone.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BattleRunner : MonoBehaviour
{
    public static BattleRunner Instance { get; private set; }

    [Header("Testing")]
    [Tooltip("Used only when you press Play in this scene directly with no encounter " +
             "launched. Leave empty to keep the units you hand-placed in the scene.")]
    public EncounterData debugEncounter;

    [Header("Return Flow")]
    [Tooltip("Head back automatically once the battle resolves. Turn this off if you'd " +
             "rather CombatHUD's result panel wait for a Continue button — wire that " +
             "button to BattleRunner.ReturnNow().")]
    public bool autoReturn = true;

    [Tooltip("Beat before leaving, so the player can read the result banner.")]
    [Min(0f)] public float returnDelaySeconds = 2.5f;

    private EncounterData encounter;

    private Deployment deployment = new Deployment();

    private bool resultsWritten;
    private bool leaving;

    // ---- Setup ----

    private void Awake()
    {
        Instance = this;

        encounter = BattleLauncher.Pending != null ? BattleLauncher.Pending : debugEncounter;

        if (encounter == null)
        {
            Debug.Log("[BattleRunner] No encounter active — running whatever units are already " +
                      "in the scene. (This is normal when you press Play in the combat scene.)");
            return;
        }

        deployment = BattleSpawner.Spawn(encounter, GameData.Instance, this);
        ObjectiveFactory.Install(encounter, deployment, transform);
    }

    private void Start()
    {
        BattleSpawner.Place(deployment, GridManager.Instance);

        if (TurnManager.Instance == null)
        {
            Debug.LogError("[BattleRunner] No TurnManager in this scene — the battle can never " +
                           "end, so we'd never return to the overworld.", this);
            return;
        }

        TurnManager.Instance.OnCombatEnd += HandleCombatEnd;

        // Vanishingly unlikely, but a zero-enemy encounter can resolve before we subscribe.
        if (TurnManager.Instance.CombatOver)
            HandleCombatEnd(TurnManager.Instance.Winner);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnCombatEnd -= HandleCombatEnd;

        if (Instance == this) Instance = null;
    }

    // ---- Results ----

    private void HandleCombatEnd(Team? winner)
    {
        if (resultsWritten) return;
        resultsWritten = true;

        BattleLauncher.RecordResult(encounter, winner);
        GameData data = GameData.Instance;
        if (data != null)
            PartyResultWriter.Write(deployment.deployed, winner == Team.Player, new PartyRules
            {
                permadeath = data.permadeath,
                reviveHP = data.reviveHP,
                healAfterBattle = data.healAfterBattle
            }, data.HealAll);

        // Nothing else happens here — CombatHUD's buttons drive what comes next.
    }

    /// <summary>Restart this battle from the party's pre-battle HP. Backs the Retry button.</summary>
    public void Retry()
    {
        if (leaving) return;
        leaving = true;

        PartyResultWriter.Restore(deployment.hpBeforeBattle);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---- Leaving ----

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelaySeconds);
        ReturnNow();
    }

    /// <summary>
    /// Leave the battle. Public so a Continue button on the result panel can call it when
    /// autoReturn is off.
    /// </summary>
    public void ReturnNow()
    {
        if (leaving) return;
        if (!resultsWritten)
        {
            Debug.LogWarning("[BattleRunner] ReturnNow was called before the battle resolved.");
            return;
        }
        leaving = true;

        GameData data = GameData.Instance;
        ExitDecision decision = BattleExitRouter.Decide(BattleLauncher.LastWinner == Team.Player,
            encounter, SceneManager.GetActiveScene().name, data != null ? data.returnSceneName : null);
        if (decision.Route == ExitRoute.Retry)
            PartyResultWriter.Restore(deployment.hpBeforeBattle);
        else
            BattleLauncher.ClearPending();
        SceneManager.LoadScene(decision.SceneName);
    }
}
