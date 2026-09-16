# 02 · Class diagram — units, stats, and authored data

The combat domain model: what a unit *is* (template vs. live instance) and what it carries.

```mermaid
classDiagram
    direction LR

    class UnitDefinition {
        <<ScriptableObject>>
        +string unitName
        +GameObject prefab
        +UnitStats baseStats
        +WeaponData equippedWeapon
        +SpecialAttackData equippedSpecial
        +int AttackRange
        +bool IsSpawnable
        +ApplyTo(Unit unit)
        +OnAfterDeserialize()
    }

    class Unit {
        <<MonoBehaviour>>
        +string unitName
        +Team team
        +UnitStats stats
        +WeaponData equippedWeapon
        +SpecialAttackData equippedSpecial
        +Vector2Int Cell
        +bool HasActed
        +bool IsAlive
        +int currentBurstPips
        +event OnHPChanged
        +event OnDied
        +SnapToGrid()
        +PlaceAt(Vector2Int cell) bool
        +TryWarpTo(Vector2Int cell, bool allowNudge) bool
        +RemoveFromGrid()
        +MoveAlong(List~Vector2Int~ path) IEnumerator
        +ApplyDamage(int damage)
        +int TotalAttackPower
        +int TotalHitRate
        +int TotalAvoidRate
        +int TotalCritRate
        +GainBurstPip(int amount)
        +ConsumeBurst()
        +CanAttack(Unit target) bool
        +CanAttackFrom(Vector2Int myCell, Unit target, Vector2Int targetCell) bool
        +PreviewAttack(Unit target) AttackForecast
        +Attack(Unit target) string
        -Die()
    }

    class UnitStats {
        <<struct>>
        +int maxHP
        +int currentHP
        +int attack
        +int defense
        +int resistance
        +int speed
        +int skill
        +int luck
        +int moveRange
        +UnitStats Default$
    }

    class WeaponData {
        <<ScriptableObject>>
        +string weaponName
        +int might
        +int baseHit
        +int baseCrit
        +int weight
        +int minRange
        +int maxRange
        +DamageCategory damageType
    }

    class SpecialAttackData {
        <<ScriptableObject>>
        +string techniqueName
        +int pipCost
        +float damageMultiplier
        +bool bypassesArmor
        +bool preventsCounter
    }

    class AttackForecast {
        <<struct>>
        +int CriticalMultiplier$
        +Unit attacker
        +Unit target
        +int damage
        +int hitChance
        +int critChance
        +int attackerStrikes
        +int counterStrikes
        +bool targetCounters
        +bool targetDies
        +Damage(Unit a, Unit d, SpecialAttackData s)$ int
        +HitChance(Unit a, Unit d)$ int
        +CritChance(Unit a, Unit d, bool special)$ int
        +Doubles(Unit a, Unit d)$ bool
        +Calculate(Unit a, Unit t, Vector2Int fromCell, bool special)$ AttackForecast
    }

    class PartyMember {
        +UnitDefinition definition
        +int currentHP
        +bool inActiveParty
        +bool isDead
        +bool IsDeployable
        +FullHeal()
    }

    class Team {
        <<enumeration>>
        Player
        Enemy
    }

    class DamageCategory {
        <<enumeration>>
        Physical
        Special
    }

    UnitDefinition *-- UnitStats : baseStats
    Unit *-- UnitStats : stats
    UnitDefinition o-- WeaponData
    UnitDefinition o-- SpecialAttackData
    Unit o-- WeaponData
    Unit o-- SpecialAttackData
    UnitDefinition ..> Unit : ApplyTo() configures
    UnitDefinition --> "prefab" Unit
    PartyMember --> UnitDefinition
    WeaponData --> DamageCategory
    Unit --> Team
    Unit ..> AttackForecast : creates
    AttackForecast --> "2" Unit
```

## Reading notes

- **Template vs. instance:** `UnitDefinition` (asset, shared) → `PartyMember` (save-game state, persists across scenes) → `Unit` (scene object, lives for one battle). Keep this three-layer split explicit; it's what lets HP persist between fights.
- `*--` (composition) = `UnitStats` is copied by value. `o--` (aggregation) = weapons/specials are shared asset references — never mutate them at runtime.
- Legacy proxy properties (`maxHP`, `attack`, `defense`, … on both `Unit` and `UnitDefinition`) are omitted on purpose. If you're keeping them, mark them `[Obsolete]` so the diagram and the code agree.