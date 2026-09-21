# 06 · State machines

## CombatController input state

`private enum State { Idle, UnitSelected, AwaitingTarget }` plus `inputLocked`.
Moving and Resolving below are coroutine activities, not enum values. Both set
`TurnManager.IsPlayerActionInProgress` and clear it with `inputLocked` in `finally`.
The HUD and `EndPhaseEarly(Player)` use `CanEndPlayerPhase` to block End Turn during them.

```mermaid
stateDiagram-v2
    state AfterArrival <<choice>>
    [*] --> Idle
    Idle --> UnitSelected : click own unit that has not acted
    UnitSelected --> Idle : right-click or invalid selection
    UnitSelected --> UnitSelected : select another available player unit
    UnitSelected --> Moving : click reachable cell with a path
    UnitSelected --> Idle : no path found
    UnitSelected --> AfterArrival : click own cell / StayInPlace
    UnitSelected --> Resolving : click enemy already in range
    Moving --> AfterArrival : active player phase, actor alive and still selected
    AfterArrival --> AwaitingTarget : targets in range
    AfterArrival --> Idle : no targets / FinishUnitTurn
    AwaitingTarget --> Resolving : click valid target
    AwaitingTarget --> Idle : right-click or invalid target / FinishUnitTurn
    Resolving --> Idle : pause ends, active player phase / FinishUnitTurn
    UnitSelected --> Idle : Update observes phase change / Deselect
    AwaitingTarget --> Idle : Update observes phase change / Deselect
    Moving --> Idle : phase changed / Update deselects
    Resolving --> Idle : phase changed / Update deselects
    Idle --> CombatEnded : Update observes CombatOver
    UnitSelected --> CombatEnded : Update observes CombatOver
    AwaitingTarget --> CombatEnded : Update observes CombatOver
    Moving --> CombatEnded : Update observes CombatOver
    Resolving --> CombatEnded : Update observes CombatOver
    CombatEnded --> [*]
```

`Update` ignores gameplay input until combat starts, outside player phase, while locked,
or over UI. Hover panels can still update during either team's active phase. Combat-end
cleanup runs once and hides panels. A movement coroutine also stops without `AfterArrival`
if its manager/actor disappears, the actor dies, or selection changes.

## TurnManager battle lifecycle

```mermaid
stateDiagram-v2
    state InitialCheck <<choice>>
    [*] --> NotStarted
    NotStarted --> InitialCheck : Start yields one frame, then CombatStarted = true
    InitialCheck --> CombatOver : CheckCombatEnd resolves
    InitialCheck --> ActiveBattle : unresolved / BeginPhase(Player)

    state ActiveBattle {
        [*] --> PlayerPhase
        PlayerPhase --> EnemyPhase : all acted or accepted EndPhaseEarly(Player)
        EnemyPhase --> PlayerPhase : all acted or EndPhaseEarly(Enemy) / increment round
        PlayerPhase --> EnemyPhase : nobody can act / skip
        EnemyPhase --> PlayerPhase : nobody can act / skip and increment round
    }

    ActiveBattle --> CombatOver : CheckCombatEnd resolves or skip limit reached
    CombatOver --> [*] : OnCombatEnd(winner)
```

Each `BeginPhase` checks combat end before entering its skip loop. It resets the acting
team's living units, then skips to the other team if none can act. After four consecutive
skips, another unactable phase ends in a draw. Skipped phases do not publish `OnPhaseStart`;
leaving an enemy phase increments `RoundNumber` and publishes `OnRoundStart`.
`CanStillAct` requires a living unit on the grid, on the acting team, that has not acted.

Win-condition evaluation is a decision flow that runs **before** entering `CombatOver`:

```mermaid
flowchart TD
    Check["CheckCombatEnd()"] --> Over{"Already CombatOver?"}
    Over -- Yes --> True["Return true"]
    Over -- No --> Started{"CombatStarted?"}
    Started -- No --> False["Return false"]
    Started -- Yes --> Objectives{"First resolved active objective<br/>in ascending priority order?"}
    Objectives -- Yes --> ObjectiveWinner["EndCombat(objective winner)"]
    Objectives -- No --> Wipe{"autoEndWhenTeamWipedOut?"}
    Wipe -- No --> False
    Wipe -- Yes --> Alive{"Living teams?"}
    Alive -- Both --> False
    Alive -- "Player only" --> PlayerWins["EndCombat(Player)"]
    Alive -- "Enemy only" --> EnemyWins["EndCombat(Enemy)"]
    Alive -- Neither --> Draw["EndCombat(null)"]
    ObjectiveWinner --> True
    PlayerWins --> True
    EnemyWins --> True
    Draw --> True
```

`NotifyUnitDied` publishes `OnUnitDied` immediately. During a combat exchange it defers
its end check until the outermost `EndExchange`, after EXP and level gains. Direct
`CheckCombatEnd` calls (including roster removal and phase start) are not deferred.
