# Skills & Effects System

## Overview

The skill system in Menace is built around `SkillTemplate` assets that define abilities, and `SkillEventHandler` classes that implement the runtime behavior.

## SkillTemplate Structure

```c
public class SkillTemplate : DataTemplate {
    // Display
    public LocalizedLine Title;           // +0x78
    public LocalizedMultiLine Description; // +0x80
    public LocalizedMultiLine ShortDescription; // +0x88
    public Sprite Icon;                   // +0x90
    public Sprite IconDisabled;           // +0x98

    // Classification
    public SkillType Type;                // +0xA0
    public List<TagTemplate> Tags;        // +0xA8
    public SkillOrder Order;              // +0xB0

    // Cost & Limits
    public int ActionPointCost;           // +0xB4
    public bool IsLimitedUses;            // +0xB8
    public int Uses;                      // +0xBC
    public SkillUsesDisplayTemplate UsesDisplayTemplate; // +0xC0

    // Activation
    public bool IsActive;                 // +0xC8
    public bool HideApCosts;              // +0xC9
    public KeyBindPlayerSetting KeyBind;  // +0xCC
    public ExecutingElementType ExecutingElement; // +0xD0
    public AnimationType AnimationType;   // +0xD4
    public AimingType AimingType;         // +0xD8

    // Targeting
    public bool IsTargeted;               // +0xE4
    public CursorType TargetingCursor;    // +0xE8
    public SkillTarget TargetsAllowed;    // +0xEC
    public bool KeepSelectedIfStillUsable; // +0xF0
    public bool IsLineOfFireNeeded;       // +0xF1

    // Combat flags (from earlier analysis)
    public byte AlwaysHits;               // +0xF3
    public byte IgnoresCoverAtRange;      // +0x100
}
```

## SkillEventHandler Architecture

Base class for all skill effect handlers:

```c
public abstract class SkillEventHandler {
    public Skill ParentSkill;  // +0x10

    // Lifecycle methods
    virtual void OnAdded();
    virtual void OnRefresh();
    virtual void OnRemoved();
    virtual void OnUpdate(EntityProperties properties);

    // Usage methods
    virtual bool OnBeforeUse(Actor user, Tile targetTile);
    virtual void OnUse(Actor user, Tile targetTile, ref bool applyToTile);
    virtual void OnAfterUse();

    // Application methods
    virtual void OnBeforeApply(Actor user, Tile userTile, Tile targetTile, Element element, ref float delay);
    virtual void OnApply(Actor user, Tile userTile, Tile targetTile, Tile centerTile, Element element, bool isHit);

    // Event handlers
    virtual void OnRoundStart();
    virtual void OnRoundEnd();
    virtual void OnTurnStart();
    virtual void OnTurnEnd();
    virtual void OnAnySkillAppliedByElement(Skill skill, Element element, SkillApplyEvent event);

    // State queries
    virtual bool IsHidden();
    virtual bool IsEnabled();
    virtual bool IsUsable();
    virtual bool IsApplicableTo(Tile targetTile, Entity overrideTarget);

    // Helper methods
    protected Actor GetActor();
    protected Entity GetEntity();
    protected IEntityProperties GetOwner();
}
```

## Key Skill Effect Types

### Damage Effect

```c
public class Damage : SkillEventHandlerTemplate {
    // Target selection
    public bool IsAppliedOnlyToPassengers;  // +0x58
    public int ElementsHit;                  // +0x5C (0-9)
    public float ElementsHitPercentage;      // +0x60 (0-1)

    // Damage values
    public float DamageFlatAmount;           // +0x64 (0-1000)
    public float DamagePctCurrentHitpoints;  // +0x68 (0-1)
    public float DamagePctCurrentHitpointsMin; // +0x6C

    // Additional damage modifiers...
}
```

### Hitchance Effect

```c
public class Hitchance : SkillEventHandlerTemplate {
    public float AccuracyBonus;      // +0x58 - Added to BaseAccuracy
    public float AccuracyMult;       // +0x5c - Multiplied with AccuracyMult
    public float DropoffBonus;       // +0x60 - Added to AccuracyDropoffBase
    public float DropoffMult;        // +0x64 - Multiplied with AccuracyDropoffMult
}

// Applied via:
void ApplyToEntityProperties(EntityProperties props) {
    props.BaseAccuracy += AccuracyBonus;           // +0x68
    AddMult(props.AccuracyMult, AccuracyMult);     // +0x6c
    props.AccuracyDropoffBase += DropoffBonus;     // +0x70
    AddMult(props.AccuracyDropoffMult, DropoffMult); // +0x74
}
```

### Other Effect Types

From the class definitions found:
- `AddSkillToEntitiesEffect` - Grants skills to entities
- `SpawnTileEffect` - Creates tile effects
- `SpawnEntityEffectHandler` - Spawns entities
- `MoraleOverMaxEffect` - Morale manipulation
- `IgnoreDamageEffectHandler` - Damage immunity
- `CooldownEffectHandler` - Skill cooldown management
- `GainEffectOnSkillUse` - Reactive effect gain
- `JetPackHandler` - Movement ability
- `ScannerHandler` - Detection ability

## Skill Flow

### Skill Usage Flow
```
1. Player selects skill
2. IsUsable() checked for all handlers
3. OnBeforeUse() called - can cancel
4. OnUse() called - main execution
5. For each target tile/element:
   a. OnBeforeApply() - pre-application
   b. Hit check performed
   c. OnApply() - effect application
6. OnAfterUse() - cleanup
```

### Effect Resolution
```
1. Build EntityProperties from base stats
2. For each active skill effect:
   a. OnUpdate(properties) modifies stats
3. Apply template modifiers (weapon, armor)
4. Calculate final values
5. Apply to target
```

## SkillContainer

Manages skills for an entity:

```c
public class SkillContainer {
    // Builds properties combining all active effects
    EntityProperties BuildPropertiesForUse(skill, source, target);
    EntityProperties BuildPropertiesForDefense(container, attacker, source, target);
}
```

## Key Methods (from earlier decompilation)

### Skill.GetHitchance (0x1806dba90)
Calculates hit chance using:
- EntityProperties.GetAccuracy()
- Skill.GetCoverMult()
- Distance penalty from ideal range
- Defender's dodge modifier

### SkillTemplate.IsIgnoringCoverInsideForTarget
Checks if skill ignores cover for a specific target.

## Skill Tags

Skills can have tags that modify behavior:
- `TagType.SUPPRESSIVE = 5` - Causes suppression damage
- Various other combat modifiers

## Skill Types

```c
public enum SkillType {
    // Attack types
    // Movement types
    // Support types
    // etc.
}
```

## Skill Targeting

```c
public enum SkillTarget {
    // Self
    // Ally
    // Enemy
    // Tile
    // etc.
}

public enum AimingType {
    // Different aiming modes
}
```

## Creating Custom Effects

Effects are created via factory methods:

```c
// Example from Damage.Create (0x180703500)
DamageHandler Create(Damage template) {
    var handler = new DamageHandler();
    handler.ParentSkill = ...;
    handler.Template = template;  // +0x18
    return handler;
}
```

## Effect Application via ApplyToEntityProperties

Many effects modify EntityProperties directly:

| Effect Type | Modifies |
|-------------|----------|
| Hitchance | Accuracy, accuracy dropoff |
| Damage | Damage, penetration |
| Armor buffs | Armor values, resistances |
| Movement | Movement points, speed |
| Morale | Morale, suppression resist |

## Handler Interfaces

Special interfaces for specific behaviors:
- `ISkillEffectiveRangeProvider` - Custom range calculation
- `ISkillDelayedEffectHandler` - Delayed effect execution
- `IExpectedDamageContributor` - Damage preview calculation
- `IExpectedSuppressionContributor` - Suppression preview
