# 03 · Class diagram — battle orchestration

Who runs a battle, who decides it's over, and who reacts.

```mermaid
classDiagram
    direction TB

    class BattleRunner {
        <<MonoBehaviour · order -100>>
        +BattleRunner Instance$
        +EncounterData debugEncounter
        +bool autoReturn
        -Dictionary~Unit,PartyMember~ deployed
        +ResolveCombat(Unit attacker, Unit defender, bool useSpecial)$ string
        -SpawnParty()
        -SpawnEnemies()
        -InstallObjective()
        -HandleCombatEnd(Team? winner)
        -WriteResultsToParty(bool victory)
        +Retry()
        +ReturnNow()
    }

    class TurnManager {
        <<MonoBehaviour>>
        +TurnManager Instance$
        +Team CurrentPhase
        +int RoundNumber
        +bool CombatStarted
        +bool CombatOver
        +Team? Winner
        +event OnPhaseStart
        +event OnRoundStart
        +event OnUnitDied
        +event OnCombatEnd
        -List~Unit~ allUnits
        -CombatObjective[] objectives
        +RegisterUnit(Unit u)
        +UnregisterUnit(Unit u)
        +UnitsOnTeam(Team t) IEnumerable~Unit~
        +NotifyUnitActed(Unit u)
        +NotifyUnitDied(Unit u)
        +EndPhaseEarly(Team t)
        +CheckCombatEnd() bool
        +RegisterObjective(CombatObjective o)
        -BeginPhase(Team t)
        -EndPhase()
        -EndCombat(Team? winner)
    }

    class GridManager {
        <<MonoBehaviour>>
        +GridManager Instance$
        -Unit[,] occupants
        +WorldToCell(Vector3 p) Vector2Int
        +CellToWorld(Vector2Int c) Vector3
        +IsWalkable(Vector2Int c) bool
        +GetUnitAt(Vector2Int c) Unit
        +MoveUnit(Vector2Int from, Vector2Int to, Unit u)
        +GetPath(...) List~Vector2Int~
        +GetDistanceField(...) Dictionary
        +GetReachableCells(Vector2Int start, int range) HashSet
    }

    class CombatObjective {
        <<abstract>>
        +int priority
        +IsResolved(TurnManager t, out Team? winner)* bool
        +Describe() string
    }
    class DefeatBossObjective {
        +Unit boss
        +Team winsWhenBossFalls
    }
    class SurviveRoundsObjective {
        +int rounds
        +Team survivingTeam
    }

    class CombatController {
        <<MonoBehaviour · player input>>
        -State state
        -Unit selectedUnit
        -HandleLeftClick(Vector2Int c)
        -Select(Unit u)
        -MoveSelectedTo(Vector2Int dest) IEnumerator
        -ResolveAttack(Unit a, Unit t) IEnumerator
        -FinishUnitTurn()
    }

    class EnemyPhaseController {
        <<MonoBehaviour · AI>>
        +float actionDelay
        -HandlePhaseStart(Team t)
        -RunEnemyPhase() IEnumerator
        -TakeEnemyTurn(Unit e) IEnumerator
        -FindNearestPlayer(Unit e) Unit
        -FindBestApproachCell(Unit e, Unit t) Vector2Int
    }

    class CombatHUD {
        <<MonoBehaviour · UI>>
        -HandlePhaseStart(Team t)
        -HandleRoundStart(int r)
        -HandleCombatEnd(Team? w)
        -OnEndTurnPressed()
    }

    class EncounterData {
        <<ScriptableObject>>
        +List~EnemySpawn~ enemies
        +List~Vector2Int~ playerSpawnCells
        +VictoryCondition victory
        +DefeatAction onDefeat
        +Validate() bool
        +FindBossSpawn() EnemySpawn
    }

    CombatObjective <|-- DefeatBossObjective
    CombatObjective <|-- SurviveRoundsObjective

    BattleRunner --> EncounterData : reads
    BattleRunner ..> CombatObjective : creates
    BattleRunner ..> TurnManager : RegisterUnit / RegisterObjective
    BattleRunner ..> GridManager : placement
    TurnManager o-- "0..*" CombatObjective
    TurnManager o-- "0..*" Unit
    GridManager o-- "0..*" Unit : occupants

    CombatController ..> TurnManager : NotifyUnitActed
    CombatController ..> GridManager : paths, reachable
    EnemyPhaseController ..> TurnManager : NotifyUnitActed
    EnemyPhaseController ..> GridManager : distance fields

    TurnManager ..> EnemyPhaseController : OnPhaseStart
    TurnManager ..> CombatHUD : OnPhaseStart / OnRoundStart / OnCombatEnd
    TurnManager ..> BattleRunner : OnCombatEnd
    CombatHUD ..> BattleRunner : Retry / ReturnNow
```

## Reading notes

- Dashed arrows labelled with an event name point from **publisher → subscriber**. Those are the "good" couplings: `TurnManager` doesn't know who's listening.
- `CombatController` and `EnemyPhaseController` are two *controllers of the same kind* (one per team) but share no abstraction. They each re-implement "move along path, then maybe attack, then NotifyUnitActed". That's the best candidate for a shared interface — see README.