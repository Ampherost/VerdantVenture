# 05 · Sequence — one player unit's turn (move + attack)

```mermaid
sequenceDiagram
    autonumber
    actor Player
    participant CC as CombatController
    participant GM as GridManager
    participant A as Unit (attacker)
    participant AF as AttackForecast
    participant FP as BattleForecastPanel
    participant CR as CombatResolver (static)
    participant ER as ExpRules (static)
    participant T as Unit (target)
    participant TM as TurnManager
    participant BR as BattleRunner
    participant PW as PartyResultWriter

    Player->>CC: click own unit
    CC->>GM: GetReachableCells(cell, moveRange)
    CC->>CC: ShowMoveHighlights · state = UnitSelected

    Player->>CC: click destination
    CC->>GM: GetPath(from, to)
    Note over CC,GM: A null path deselects and stops movement
    CC->>TM: IsPlayerActionInProgress = true; lock input
    CC->>A: MoveAlong(path)
    A->>GM: MoveUnit(from, to, this)
    CC->>TM: finally: IsPlayerActionInProgress = false; unlock input
    Note over CC,A: Continue only if still player phase, combat active, acting unit alive and selected
    CC->>CC: AfterArrival → FindTargetsInRange · state = AwaitingTarget

    Player->>CC: hover enemy
    CC->>A: PreviewAttack(target)
    A->>AF: Calculate(attacker, target, cell)
    AF-->>CC: forecast
    CC->>FP: Show(forecast)

    Player->>CC: click enemy
    CC->>TM: IsPlayerActionInProgress = true; lock input
    CC->>A: Attack(target)
    A->>CR: ResolveCombat(attacker, target, randomRoll)
    opt TurnManager exists
        CR->>TM: BeginExchange()
    end
    CR->>A: GainBurstPip(1) for initiation
    loop each strike (attacker, counter, doubles)
        CR->>AF: HitChance / CritChance / Damage
        Note over CR,T: Counter reverses striker and recipient; count successful hits per side
        opt strike hits
            Note over CR,A: Before lethal damage, the striker gains a KO burst pip
            CR->>T: ApplyDamage(dmg)
            T-->>T: OnHPChanged (health bar, info panel)
            opt HP reaches 0 and TurnManager exists
                T->>T: OnDied; RemoveFromGrid; deactivate
                T->>TM: NotifyUnitDied(this)
                TM->>TM: OnUnitDied immediately; combatEndPending = true
            end
        end
    end
    opt no TurnManager or combat not already over
        loop each surviving player (initiator, then defender)
            CR->>ER: ForExchange(pre-exchange levels, hits, killed)
            ER-->>CR: EXP (whiff = MinHitExp)
            CR->>CR: append EXP line
            CR->>A: GainExp(exp, growthRoll)
            Note over CR,A: Recipient is the eligible player; growth RNG is separate from combat RNG
            A-->>CR: List of LevelUpResult
            CR->>CR: append one line per level-up
        end
    end
    opt TurnManager exists (finally, including on exception)
        CR->>TM: EndExchange()
        opt outermost exchange and combatEndPending
            TM->>TM: CheckCombatEnd()
            opt combat ends
                TM->>BR: OnCombatEnd(winner)
                BR->>PW: Write(deployed, victory, rules, heal)
                Note over BR,PW: Includes winning-blow EXP and level gains
            end
        end
    end
    CR-->>A: combat log string
    A-->>CC: combat log string
    CC->>CC: wait 0.4 seconds
    CC->>TM: finally: IsPlayerActionInProgress = false; unlock input
    alt manager exists, combat active, still player phase
        CC->>CC: FinishUnitTurn(): clear selection / highlights
        CC->>TM: NotifyUnitActed(selected actor if still present)
        TM->>TM: all acted? → EndPhase() → BeginPhase(Enemy)
    else combat ended or phase changed
        Note over CC: Exit coroutine; Update handles cleanup
    end
```

The HUD polls `CanEndPlayerPhase`; the End Turn action is blocked during movement and the
attack pause. This sequence shows an ordinary attack. Special overloads exist in `Unit`,
but the player and enemy controllers do not currently request them.
