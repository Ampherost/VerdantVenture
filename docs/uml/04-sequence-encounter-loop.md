# 04 · Sequence — overworld → battle → overworld

The encounter round-trip, including setup order and guarded automatic/manual exits.

```mermaid
sequenceDiagram
    autonumber
    actor Player
    participant ET as EncounterTrigger
    participant BL as BattleLauncher
    participant GD as GameData
    participant PM as PartyMember
    participant SM as Unity SceneManager
    participant BR as BattleRunner
    participant BS as BattleSpawner
    participant D as Deployment
    participant OF as ObjectiveFactory
    participant TM as TurnManager
    participant U as Unit
    participant PW as PartyResultWriter
    participant ER as BattleExitRouter
    participant HUD as CombatHUD
    participant OP as OverworldPlayerPlacer

    Player->>ET: dialogue choice → Begin()
    ET->>BL: Begin(encounter)
    BL->>BL: encounter.Validate(), abort if null or invalid
    BL->>BL: set Pending, clear HasResult and LastWinner
    opt GameData exists
        BL->>GD: SetReturnPoint(scene, playerPos)
    end
    BL->>SM: LoadScene(combatSceneName)

    Note over BR,U: Combat scene loads, BattleRunner execution order is -100
    BR->>BL: Awake: read Pending (else debugEncounter)
    alt encounter exists
        BR->>BS: Spawn(encounter, GameData.Instance)
        BS->>U: deactivate/destroy hand-placed units, instantiate party/enemies
        BS->>PM: EnsureInitialised via IsDeployable, ApplyTo(unit)
        PM->>U: template identity/equipment, grown stats/level/EXP/class, then owned HP
        BS->>D: record party mapping, complete member snapshots, placement, Boss
        BS->>U: enemies: resolve override/default class, expected gains for level - 1, full HP
        BS-->>BR: Deployment
        BR->>OF: Install(encounter, deployment, transform)
        OF->>D: read Boss for boss objective
        OF->>OF: create inactive object, add component, configure, activate
        opt TurnManager already exists
            OF->>TM: RegisterObjective(objective)
        end
    else standalone scene
        Note over BR,U: Leave hand-placed units alone
    end
    TM->>TM: Awake: sweep active units and objectives
    BR->>BS: Start: Place(deployment, GridManager.Instance)
    BS->>U: set transform.position (never PlaceAt)
    BR->>TM: subscribe OnCombatEnd, handle already-finished battle
    U->>U: Start → SnapToGrid()
    TM->>TM: next frame: BeginPhase(Player)
    loop until CheckCombatEnd resolves
        Note over TM: Player phase / Enemy phase (see 05)
    end

    TM-->>BR: OnCombatEnd(winner)
    BR->>BR: resultsWritten guard
    BR->>BL: RecordResult(encounter, winner)
    opt GameData exists
        BR->>GD: read permadeath, reviveHP, healAfterBattle
        BR->>PW: Write(deployed, victory, PartyRules, HealAll callback)
        PW->>PM: ReadBackFrom(unit): stats/level/EXP/class before casualty rules
        PW->>PM: write HP/death, revive against grown MaxHP
        opt victory and healAfterBattle
            PW->>GD: HealAll callback
        end
    end
    opt autoReturn enabled
        BR->>BR: start ReturnAfterDelay()
    end
    TM-->>HUD: OnCombatEnd(winner) → show result panel

    alt explicit HUD Retry
        Player->>HUD: press Retry
        HUD->>BR: Retry()
        BR->>BR: leaving guard, set leaving
        BR->>PW: Restore(snapshotBeforeBattle), restoring all fields including isDead
        BR->>SM: reload current scene, retain Pending
    else Continue or automatic return
        alt HUD Continue
            Player->>HUD: press Continue
            HUD->>BR: ReturnNow()
        else autoReturn enabled
            BR->>BR: delay expires → ReturnNow()
        end
        Note over BR: ReturnNow stops if leaving or unresolved, otherwise sets leaving
        BR->>ER: Decide(victory, encounter, currentScene, savedReturnScene)
        ER-->>BR: ExitDecision (Route, SceneName)
        alt Retry (defeat + RetryBattle)
            BR->>PW: Restore(snapshotBeforeBattle), restoring all fields including isDead
            Note over BL: Pending is retained
        else GameOver or ReturnToOverworld
            BR->>BL: ClearPending()
        end
        BR->>SM: LoadScene(decision.SceneName)
    end
    Note over BR,SM: A later Continue/delay call stops at leaving, no second load
    opt loaded scene has OverworldPlayerPlacer and matches saved return point
        OP->>GD: Start: read hasReturnPoint, returnSceneName, returnPosition
        OP->>OP: set player position to returnPosition + offset
        OP->>GD: ClearReturnPoint()
    end
```

`BattleExitRouter` chooses Retry only for a defeat with `RetryBattle`. Defeat with
`LoadGameOverScene` uses GameOver only if its name is nonblank. All other cases return
using the encounter's return scene, then GameData's saved return scene, then `OverworldScene`.
Victory ignores the defeat action. A draw follows the defeat path.

Automatic return starts only after results are recorded and written. With `autoReturn` off,
the HUD drives the exit. Both `Retry()` and `ReturnNow()` set `leaving` before any scene load.
`ReturnAfterDelay` uses scaled time (`WaitForSeconds`), as before.

The diagram follows a valid launch; invalid encounters stop before setting Pending or loading
a scene. `OverworldPlayerPlacer` consumes a return point only in its saved scene.
The HUD shows Continue on victory and Retry/Main Menu on defeat or draw. The return router's
defeat branches are reached by automatic return or another caller of `ReturnNow()`.
