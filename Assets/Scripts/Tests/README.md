# Gameplay tests

## Run in Unity

1. Let Unity finish importing and compiling the scripts.
2. Open **Window > General > Test Runner**.
3. Select **EditMode**, then **Run All** for data, arithmetic and orchestration helper tests.
4. Select **PlayMode**, then **Run All** for combat, objectives and battle exit guards.
5. Select a failure to read its assertion and stack trace.

The combat tests create a grid, two units, a turn manager, and an input controller in the
test runner's scene. They need no game scene, prefabs, mouse input, or Inspector wiring.
Each test destroys its objects afterward. Do not enter Play Mode manually first.

## EditMode coverage

- Stat and growth addition sums every field, preserves negatives and accepts a default struct as zero.
- Personal growth defaults survive importing a legacy asset without the field; explicit zero growths stay zero.
- Class growth helpers accept every null combination; caps and max level have classless defaults.
- Swapping a unit's class changes combined growths without mutating the definition's personal growths.
- Class defaults, template-to-unit class wiring and authored starter class references are checked.
- Party results cover healing, revival, permadeath and retry HP restoration; exit routing covers victory and defeat destinations.

Growth import tests create temporary assets and remove them afterward. Class asset tests read the
committed starter assets; they do not rewrite them.

## PlayMode coverage

- Progression supports bare units, multiple levels per EXP award, result payloads, class caps,
  max-level guards, excess-EXP discard and non-positive awards. `ApplyTo` resets progression.
- Injected growth rolls verify stats are applied before `OnHPChanged`, followed by
  `OnLevelUp(Unit, LevelUpResult)`. Growth RNG never consumes combat roll queues;
  the existing `Resolver_*` tests still verify those queues are drained exactly.
- Damage subtracts defense, with a minimum of one damage.
- Forecasts do not change HP and match the resolved attack and counterattack.
- Lethal damage prevents counters, frees the occupied tile, and ends the battle.
- Friendly, distant, and off-grid targets cannot be attacked.
- End Turn is rejected during actual movement and attack coroutines; normal turn
  progression resumes after the action completes.

The movement and attack-lock tests also check the same `CanEndPlayerPhase` property used by the HUD.
They do not simulate a UI click or verify the button's scene wiring.

## Adding the next test

Name tests for player-visible behavior: `Retry_RestoresPreBattleHP`, for example.
Arrange a small scenario, perform a real action, and assert its observable outcome.
For a bug fix, first reproduce the bug with a failing test, then fix it and rerun.
Use `[Test]` for synchronous behavior and `[UnityTest]` with `IEnumerator` when
movement, frames, or coroutines matter. Keep waits bounded so bugs cannot hang the suite.

Useful next coverage: blocked pathfinding, scene-level retry restoration, and persistent
encounter completion. Add scene integration tests separately to check prefab and UI wiring.

## Assembly setup

`Verdaneth.Gameplay.asmdef` makes gameplay code referenceable by the test assemblies.
Its references cover Input System, TextMesh Pro, and Unity UI. Existing script GUIDs
are unchanged. The nested `Verdaneth.EditModeTests` (Editor-only) and
`Verdaneth.PlayModeTests` assemblies both reference gameplay and are marked `TestAssemblies`,
keeping tests and NUnit out of normal player builds. No package installation is required.

The controller tests use reflection only to dispatch its private input commands.
They never set the action lock or skip the real action. If commands are renamed,
update the test helper calls; a future public command API could remove this reflection.
