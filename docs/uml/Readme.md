# IndieGame — UML & architecture docs

Diagrams are written in [Mermaid](https://mermaid.js.org/), so they live in the repo as text,
render directly on GitHub, and show up in PR diffs next to the code they describe.
Generated from `russell-dev` @ `43cdf7c` (Sept 2026).

| # | Diagram | Type | Use it when… |
|---|---------|------|--------------|
| 01 | [Architecture overview](01-architecture-overview.md) | Module / dependency | adding a new script; deciding where it goes |
| 02 | [Units, stats & data](02-class-units-and-stats.md) | Class | touching stats, weapons, specials, party |
| 03 | [Battle orchestration](03-class-combat-flow.md) | Class | touching turns, objectives, controllers, AI |
| 04 | [Encounter round-trip](04-sequence-encounter-loop.md) | Sequence | changing scene flow, save/return, results |
| 05 | [Player attack](05-sequence-player-attack.md) | Sequence | changing combat rules or input |
| 06 | [State machines](06-state-machines.md) | State | changing input handling or phase logic |

---

## How to work with these (the process)

**1. Diagram before you code, not after.** For any feature bigger than a bug fix:

1. Sketch the change on the relevant diagram first (new class, new arrow, new message).
2. Check the arrows against the rules in [01](01-architecture-overview.md). If an arrow points the wrong way, redesign now — it's free here and expensive later.
3. Write the code.
4. Commit the diagram change **in the same PR**. A reviewer should be able to read the diagram diff and understand the design change.

**2. Pick the right diagram for the question.**

- *"What is it?"* → class diagram (structure, ownership).
- *"What happens when…?"* → sequence diagram (runtime order across objects).
- *"What can it be doing right now?"* → state diagram (anything with an `enum State` or a pile of `bool` flags).
- *"Where does it live / what may it depend on?"* → the overview.

**3. Keep them honest, keep them small.**

- Show public API and the fields that define relationships. Skip Inspector-only tuning fields (colors, delays).
- One diagram ≈ one screen. If it grows past ~15 boxes, split it.
- If the diagram and code disagree, that's a bug in one of them — fix it in the same PR.

**4. Notation cheat-sheet (Mermaid class diagrams)**

| Syntax | Meaning | Example in this project |
|--------|---------|------------------------|
| `A <\|-- B` | B inherits A | `CombatObjective <\|-- DefeatBossObjective` |
| `A *-- B` | A owns B by value (lifetime tied) | `Unit *-- UnitStats` |
| `A o-- B` | A references a shared B | `Unit o-- WeaponData` |
| `A --> B` | A holds a reference to B | `PartyMember --> UnitDefinition` |
| `A ..> B` | A uses/calls B (no stored field) | `CombatController ..> TurnManager` |
| `<<interface>>`, `<<abstract>>` | stereotype | `IInteractable`, `CombatObjective` |
| `+` / `-` / `$` / `*` | public / private / static / abstract | `+Instance$`, `IsResolved()*` |

**5. Tooling**

- GitHub renders these automatically in `.md` files.
- VS Code / Rider: install a Mermaid preview extension.
- Quick editing: paste a block into <https://mermaid.live>.
- Optional CI check: `npx @mermaid-js/mermaid-cli -i docs/uml/02-class-units-and-stats.md -o /tmp/out.md` fails on syntax errors.

---

## What drawing the diagrams revealed

These are the design issues the diagrams surface. They're candidates for your planning backlog,
roughly in priority order.

### 1. Combat resolution stays in the combat core (resolved)
`Unit.Attack()` delegates to `CombatResolver.ResolveCombat()`, with a supplied random roll.
Combat math no longer depends on the scene orchestrator.

### 2. Battle orchestration is split into focused classes (resolved)
`BattleRunner` handles lifecycle timing, results/exit coordination, and scene loads.
`BattleSpawner` builds and positions a `Deployment`; `ObjectiveFactory` installs objectives;
`PartyResultWriter` applies supplied party rules; `BattleExitRouter` chooses the exit.
See diagrams 03 and 04 for the current class and sequence relationships.

### 3. Two turn controllers, no shared abstraction
`CombatController` (player) and `EnemyPhaseController` (AI) each implement
"move along path → maybe attack → `NotifyUnitActed`" independently. When specials get a player
input path (open item from the stat-system review), you'll have to add it twice.
**Suggestion:** a small command layer — `UnitAction` with `MoveAction` / `AttackAction` /
`WaitAction` — that both controllers produce and one executor runs. That's a textbook class
diagram to draw *before* writing it.

### 4. Singletons everywhere
`GridManager.Instance` and `TurnManager.Instance` are reached from 6+ classes, including `Unit`.
It works, but it hides dependencies (they don't show up in constructors or fields — only in the
diagrams). Not urgent; at minimum, cache them in `Awake` so dependencies are visible at the top of
each class.

### 5. Implicit states in `CombatController`
The `State` enum has three values, but "moving" and "resolving attack" are represented by
`inputLocked`. See [06](06-state-machines.md). Making them explicit states removes flag checks from `Update()`.

### 6. Scene names are hard-coded strings in several places
`SceneManagerScript`, `CombatHUD` (`"MainMenuScene"`), and `EncounterData` defaults all spell
scene names out by hand. A single `SceneNames` static class of constants prevents silent typos.

### 7. Dead / unfinished code
- `WeaponData.weight` — never read (carried over from the stat review).
- `PlayerScript` — 18-line placeholder; delete or give it a job.
- Legacy stat proxies (`maxHP`, `attack`, `defense`… on `Unit` and `UnitDefinition`) — mark `[Obsolete]` and migrate callers.

---

## Suggested next diagrams

- **Burst / special attack flow** (sequence) — design the player input path for specials before building it.
- **Save / load** (class) — `GameData` + `PartyMember` now retain level, EXP, grown stats, class and separately owned HP in memory across battles. `level == 0` initializes legacy members; retry snapshots restore complete pre-battle state. Disk serialization is still future work; do not serialize `stats.currentHP` as a second HP owner.
- **Enemy AI decision** (activity/flowchart) — `EnemyPhaseController.TakeEnemyTurn`, before adding more AI behaviours.
- **Dialogue** (state) — `DialogueManager` open → lines → choice → closed.
