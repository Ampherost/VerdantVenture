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
        +UnitDefinition definition
        +Team team
        +UnitStats stats
        +ClassDefinition currentClass
        +string ClassName
        +int currentLevel
        +int currentExp
        +StatGrowths Growths
        +UnitStats StatCaps
        +int MaxLevel
        +bool AtMaxLevel
        +event OnLevelUp
        +GainExp(int amount, Func~int~ rng) List~LevelUpResult~
        +LevelUp(Func~int~ rng) LevelUpResult?
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

    class LevelUpResult {
        <<readonly struct>>
        +int Level
        +UnitStats Gains
    }

    class LevelUpResolver {
        <<static>>
        +int ExpPerLevel$
        +RollGains(UnitStats current, StatGrowths growths, UnitStats caps, Func~int~ rng)$ UnitStats
        +ExpectedGains(UnitStats current, StatGrowths growths, UnitStats caps, int levels)$ UnitStats
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
        +int level
        +int exp
        +UnitStats stats
        +ClassDefinition currentClass
        +bool inActiveParty
        +bool isDead
        +int MaxHP
        +bool IsDeployable
        +EnsureInitialised()
        +ApplyTo(Unit unit)
        +ReadBackFrom(Unit unit)
        +CaptureSnapshot() Snapshot
        +RestoreSnapshot(Snapshot snapshot)
        +FullHeal()
    }

    class Snapshot {
        <<readonly struct · PartyMember.Snapshot>>
        +UnitDefinition definition
        +int currentHP
        +int level
        +int exp
        +UnitStats stats
        +ClassDefinition currentClass
        +bool isDead
        +bool inActiveParty
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
    Unit --> UnitDefinition : definition
    Unit ..> LevelUpResolver : resolves gains
    Unit ..> LevelUpResult : returns / OnLevelUp payload
    LevelUpResult *-- UnitStats : Gains
    Unit ..> ClassDefinition : growths / caps / max level helpers
    LevelUpResolver ..> UnitStats
    LevelUpResolver ..> StatGrowths
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
    PartyMember *-- UnitStats : grown stats
    PartyMember o-- ClassDefinition : currentClass
    PartyMember ..> Unit : ApplyTo / ReadBackFrom
    PartyMember ..> Snapshot : capture / restore
    Snapshot *-- UnitStats : saved stats
    WeaponData --> DamageCategory
    Unit --> Team
    Unit ..> AttackForecast : creates
    AttackForecast --> "2" Unit
```

## Reading notes

- **Template vs. instance:** `UnitDefinition` (asset, shared) → `PartyMember` (save-game state, persists across scenes) → `Unit` (scene object, lives for one battle). Keep this three-layer split explicit; it's what lets HP persist between fights.
- `*--` (composition) = `UnitStats` and `StatGrowths` are copied by value. Both support field-wise addition without clamping. `o--` (aggregation) = weapons/specials are shared asset references — never mutate them at runtime.
- Personal growths above 100 grant guaranteed points plus a remainder chance; negative growths become zero at roll time. `LevelUpResolver` uses injected rolls in [1,100], separate from combat's [0,99]. Expected gains round midpoint values away from zero.
- Classes hold authored growth modifiers, caps, tiers and promotion options only. `ApplyTo` sets the definition and default class and resets level/EXP to 1/0. Changing `currentClass` does not change personal growths or apply promotion bonuses. Promotion is not implemented.
- Starter assets: Oshawott and Piplup default to Base-tier Squire (zero modifiers/bonuses), whose promotion options are Intermediate-tier Vanguard and Tracker. All three use max level 20, promotion level 10 and `MaxCaps`. Vanguard adds HP/ATK/DEF growth +10 and SPD -5, with promotion bonuses HP +3/ATK +2/DEF +2. Tracker adds SPD +15/SKL +10/LCK +5/DEF -5 growth, with promotion bonuses SPD +2/SKL +2. All other modifiers and bonuses are zero.
- `UnitStats.MaxCaps` sets the seven combat ceilings to 99. Zero combat caps mean uncapped; an already over-cap stat never loses points. Current HP and movement caps are ignored; HP gains increase current HP by the same amount and movement never grows.
- `Unit.Progression` is part of the same `Unit` class. Its growths/caps/max-level properties use the null-safe class helpers. Bare units have zero growths and a level limit of 20.
- `GainExp` returns a result for each level gained and discards excess EXP on reaching the limit. Direct `LevelUp` returns null without side effects at max level. Successful levels apply stats, fire `OnHPChanged`, then `OnLevelUp(Unit, LevelUpResult)`; no UI subscribes here. Combat EXP awards are separate work.
- `PartyMember.level == 0` is the serialized initialization sentinel. Seed stats, class and level before resolving HP -1. `MaxHP` reads grown stats. `PartyMember.currentHP` is the sole persistent HP owner; its `stats.currentHP` is dead data. `ApplyTo` copies grown stats before overwriting live HP, while `ReadBackFrom` copies progression only and never writes member HP.
- Snapshots copy all member fields, including death/active-party flags and class, so retry restores the original state exactly. Progression persists in memory across battles; disk serialization remains future work.
- Legacy proxy properties (`maxHP`, `attack`, `defense`, … on both `Unit` and `UnitDefinition`) are omitted on purpose. If you're keeping them, mark them `[Obsolete]` so the diagram and the code agree.
