# 04 · Sequence — overworld → battle → overworld

The full encounter round-trip, including the execution-order trick in `BattleRunner`.

```mermaid
sequenceDiagram
    autonumber
    actor Player
    participant PI as PlayerInteractions
    participant NPC as NPCScript
    participant DM as DialogueManager
    participant ET as EncounterTrigger
    participant BL as BattleLauncher (static)
    participant GD as GameData
    participant SM as Unity SceneManager
    participant BR as BattleRunner
    participant TM as TurnManager
    participant U as Unit
    participant HUD as CombatHUD

    Player->>PI: press E near NPC
    PI->>NPC: Interact(player)
    NPC->>DM: ShowDialogue(lines, choice)
    Player->>DM: pick "Yes"
    DM->>ET: onOptionA → Begin()
    ET->>BL: Begin(encounter)
    BL->>BL: encounter.Validate()
    BL->>GD: SetReturnPoint(scene, playerPos)
    BL->>SM: LoadScene(combatSceneName)

    Note over BR,U: Combat scene loads
    BR->>BL: read Pending
    BR->>GD: read Party
    BR->>U: SpawnParty() / SpawnEnemies()  (Awake, order -100)
    TM->>TM: Awake: sweep scene for Units
    BR->>U: set positions  (Start, order -100)
    U->>U: Start → SnapToGrid()
    BR->>TM: RegisterObjective(...)
    TM-->>TM: next frame: BeginPhase(Player)

    loop until CheckCombatEnd()
        Note over TM: Player phase / Enemy phase (see 05)
    end

    TM-->>BR: OnCombatEnd(winner)
    BR->>BL: RecordResult(encounter, winner)
    BR->>GD: WriteResultsToParty(victory)
    TM-->>HUD: OnCombatEnd(winner) → show result panel

    Player->>HUD: press Continue
    HUD->>BR: ReturnNow()
    alt victory, or defeat with ReturnToOverworld
        BR->>BL: ClearPending()
        BR->>SM: LoadScene(returnScene)
        Note over GD: OverworldPlayerPlacer moves player to returnPosition
        Note over ET: Awake: Cleared = (LastEncounter == encounter && victory)
    else defeat with RetryBattle
        BR->>BR: RestorePreBattleHP()
        BR->>SM: reload combat scene
    else defeat with LoadGameOverScene
        BR->>BL: ClearPending()
        BR->>SM: LoadScene(gameOverSceneName)
    end
```

`autoReturn` and `ReturnAfterDelay()` exist in `BattleRunner` but nothing calls them, so the only
way out of a battle is a HUD button. Either wire them up or delete them.