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
        +StatGrowths personalGrowths
        +ClassDefinition defaultClass
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
        +ClassDefinition currentClass
        +string ClassName
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
        +UnitStats MaxCaps$
    }

    class StatGrowths {
        <<struct>>
        +int hp
        +int attack
        +int defense
        +int resistance
        +int speed
        +int skill
        +int luck
        +StatGrowths Default$
        +StatGrowths Zero$
    }

    class ClassDefinition {
        <<ScriptableObject>>
        +int DefaultMaxLevel$
        +string className
        +ClassTier tier
        +int maxLevel
        +int promotionLevel
        +StatGrowths growthModifiers
        +UnitStats promotionBonuses
        +UnitStats statCaps
        +List~ClassDefinition~ promotionOptions
        +CombinedGrowths(UnitDefinition def, ClassDefinition cls)$ StatGrowths
        +CapsOf(ClassDefinition cls)$ UnitStats
        +MaxLevelOf(ClassDefinition cls)$ int
    }

    class ClassTier {
        <<enumeration>>
        Base
        Intermediate
        Advanced
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
    UnitDefinition *-- StatGrowths : personalGrowths
    UnitDefinition o-- ClassDefinition : defaultClass
    Unit o-- ClassDefinition : currentClass
    ClassDefinition *-- StatGrowths : growthModifiers
    ClassDefinition *-- UnitStats : promotionBonuses / statCaps
    ClassDefinition o-- "0..*" ClassDefinition : promotionOptions
    ClassDefinition --> ClassTier
    ClassDefinition ..> UnitDefinition : CombinedGrowths reads personalGrowths
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
- `*--` (composition) = `UnitStats` and `StatGrowths` are copied by value. Both support field-wise addition without clamping. `o--` (aggregation) = weapons/specials are shared asset references — never mutate them at runtime.
- Personal growths are percentage data only: values above 100 are legal, and negative values are preserved until roll time. No growth rolling is implemented yet.
- Classes hold authored growth modifiers, caps, tiers and promotion options only. `ApplyTo` copies the default class reference onto the unit; changing `currentClass` does not change personal growths or apply promotion bonuses. Promotion and leveling are not implemented yet.
- `UnitStats.MaxCaps` sets the seven combat ceilings to 99; current HP and movement are never capped and their cap fields are zero. A zero combat cap is reserved to mean uncapped when level-up resolution is added.
- Legacy proxy properties (`maxHP`, `attack`, `defense`, … on both `Unit` and `UnitDefinition`) are omitted on purpose. If you're keeping them, mark them `[Obsolete]` so the diagram and the code agree.
