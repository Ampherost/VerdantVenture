# Issue 4 pre-change reference audit

Searched repository source and docs with `rg -n 'MaxHP|definition\.maxHP|definition\.baseStats|hpBeforeBattle'` from the project root. Ignored Unity caches and Git internals are excluded. These are all 29 matching lines, recorded before implementation; line numbers refer to that baseline.

| File | Line | Existing purpose | Disposition |
|---|---:|---|---|
| Assets/Scripts/GameData.cs | 26 | `MaxHP` reads `definition.maxHP` | Read grown stats instead |
| Assets/Scripts/GameData.cs | 35 | Initialize negative HP using `MaxHP` | Seed progression first |
| Assets/Scripts/GameData.cs | 36 | Clamp HP using `MaxHP` | Clamp against grown stats |
| Assets/Scripts/GameData.cs | 42 | Full-heal using `MaxHP` | Initialize before healing |
| Assets/Scripts/Deployment.cs | 8 | Declare HP-only snapshot dictionary | Replace with member snapshots |
| Assets/Scripts/BattleSpawner.cs | 57 | Capture pre-battle HP | Capture complete member state |
| Assets/Scripts/BattleRunner.cs | 96 | Explicit Retry restores HP | Pass snapshot dictionary |
| Assets/Scripts/BattleRunner.cs | 118 | ReturnNow retry route restores HP | Pass same snapshot dictionary |
| Assets/Scripts/PartyResultWriter.cs | 40 | Revive HP clamped to member MaxHP | Read back growth before this clamp |
| Assets/Scripts/PartyResultWriter.cs | 50 | HP-only Restore signature | Accept member snapshots |
| Assets/Scripts/PartyResultWriter.cs | 52 | Iterate HP-only snapshots | Restore complete snapshots |
| Assets/Scripts/Tests/EditMode/PartyResultWriterTests.cs | 20 | Configure fixture template HP | Retain template setup |
| Assets/Scripts/Tests/PlayMode/BattleRunnerExitGuardTests.cs | 37 | Seed HP-only retry fixture | Seed complete snapshot |
| Assets/Scripts/Tests/EditMode/ClassDefinitionTests.cs | 119 | Assert template attack copied | Retain template test |
| Assets/Scripts/Tests/EditMode/ClassDefinitionTests.cs | 120 | Assert template maxHP copied | Retain template test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 253 | Assert migrated template maxHP | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 254 | Assert migrated template currentHP | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 255 | Assert migrated attack | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 256 | Assert migrated defense | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 257 | Assert migrated resistance | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 258 | Assert migrated movement | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 265 | Unit mutation leaves template attack unchanged | Retain value-copy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 268 | Edit template attack after migration | Retain legacy test |
| Assets/Scripts/Tests/PlayMode/CombatTests.cs | 270 | Re-deserialization preserves template edit | Retain legacy test |
| docs/uml/03-class-combat-flow.md | 24 | Diagram HP dictionary | Show snapshot dictionary |
| docs/uml/03-class-combat-flow.md | 46 | Diagram restore signature | Show snapshots |
| docs/uml/03-class-combat-flow.md | 207 | Explain HP snapshot ownership | Explain complete snapshot ownership |
| docs/uml/04-sequence-encounter-loop.md | 75 | Explicit Retry sequence restores HP | Restore complete state including saved death flag |
| docs/uml/04-sequence-encounter-loop.md | 88 | Automatic/Continue retry sequence restores HP | Restore same complete state |

## Retry route trace

- Explicit: `BattleRunner.Retry → PartyResultWriter.Restore → SceneManager.LoadScene`.
- Delayed: `ReturnAfterDelay → ReturnNow → BattleExitRouter.Decide → ExitRoute.Retry → PartyResultWriter.Restore → SceneManager.LoadScene`.
- Continue: enters the same `ReturnNow` route directly.

The routes already used the same restore method. Only the supplied dictionary and restored payload needed to change. Restore now restores the saved `isDead` value instead of unconditionally clearing it.

## HP ownership

`PartyMember.currentHP` is the only persistent HP owner. `PartyMember.stats.currentHP` is dead data. Initialization uses serialized level 0, seeds progression first, then resolves negative HP and clamps to grown MaxHP. ApplyTo copies grown stats before owned HP. ReadBackFrom copies stats, level, EXP and class without changing owned HP. The result writer performs readback before its HP/casualty rules.
