# Suppression & Morale System

## Overview

Menace uses a suppression and morale system to model psychological effects in combat. Units can become suppressed from incoming fire and may break morale under sustained pressure.

## States

### SuppressionState Enum
```c
public enum SuppressionState {
    None = 0,       // Normal state
    Suppressed = 1, // Taking cover, reduced effectiveness
    PinnedDown = 2  // Severely impaired, cannot act
}
```

### MoraleState Enum
```c
public enum MoraleState {
    Fleeing = 1,   // Routing, attempting to escape
    Wavering = 2,  // Shaky, may break
    Neutral = 3    // Normal morale
}
```

## Actor Class Fields

The `Actor` class (extends `Entity`) contains the core suppression/morale state:

| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x15C | float | m_Suppression | Current suppression value |
| +0x160 | float | m_Morale | Current morale value |
| +0xD4 | MoraleState | m_LastMoraleState | Previous morale state |

## Key Methods

### Actor.GetSuppression (0x5DF7B0)
Returns the raw suppression value.

### Actor.GetSuppressionPct (0x5DF710)
Returns suppression as a percentage (0.0 - 1.0).

### Actor.GetSuppressionState (0x5DF730)
```c
SuppressionState GetSuppressionState(float additionalPct = 0) {
    float suppressionPct = GetSuppressionPct() + additionalPct;
    // Thresholds determined by TacticalConfig
    if (suppressionPct >= pinnedThreshold) return SuppressionState.PinnedDown;
    if (suppressionPct >= suppressedThreshold) return SuppressionState.Suppressed;
    return SuppressionState.None;
}
```

### Actor.ApplySuppression (0x5DDDA0)
```c
void ApplySuppression(float value, bool direct, Entity suppressor, Skill skill) {
    // Applies suppression damage to the actor
    // 'direct' flag determines if suppression resistance is applied
    // Updates suppression state and potentially triggers morale effects
}
```

### Actor.SetSuppression (0x5E76D0)
Directly sets suppression value.

### Actor.ChangeSuppressionAndUpdateAP (0x5DE3B0)
Changes suppression and updates action points accordingly (suppressed units have reduced AP).

### Actor.GetMorale (0x5DF5C0)
Returns the raw morale value.

### Actor.GetMoralePct (0x5DF4A0)
Returns morale as a percentage.

### Actor.GetMoraleMax (0x5DF3E0, 0x5DF330)
Returns maximum morale, optionally adjusted by hitpoints percentage.

### Actor.GetMoraleState (virtual, 0x5DF4D0)
```c
MoraleState GetMoraleState() {
    float moralePct = GetMoralePct();
    // Thresholds from config
    if (moralePct <= fleeingThreshold) return MoraleState.Fleeing;
    if (moralePct <= waveringThreshold) return MoraleState.Wavering;
    return MoraleState.Neutral;
}
```

## Entity Base Class

The `Entity` class provides the base interface:

| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x54 | int | m_Hitpoints | Current HP |
| +0x58 | int | m_HitpointsMax | Maximum HP |
| +0x5C | int | m_ArmorDurability | Current armor durability |
| +0x60 | int | m_ArmorDurabilityMax | Maximum armor durability |

### Entity.ApplySuppression (virtual, 0x610A70)
Base virtual method for suppression application.

### Entity.GetSuppressionState (virtual, 0x611950)
Base virtual method returning SuppressionState.None for non-Actor entities.

## EntityProperties - Suppression Related

From the ArmorTemplate analysis, these EntityProperties offsets relate to suppression:

| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xc4 | int | MoraleBase | Base morale value |
| +0xc8 | float | MoraleMult | Morale multiplier |
| +0xcc | int | SuppressionResist | Base suppression resistance |
| +0xd0 | float | SuppressionResistMult | Suppression resistance mult |
| +0xd4 | int | Unknown | Modified by armor |
| +0xd8 | float | UnknownMult | Modified by armor |

## Tactical Actions

### AssignSuppressionToEntityAction (TypeDefIndex: 2652)
```c
public class AssignSuppressionToEntityAction : TacticalAction {
    private readonly SuppressionState m_SuppressionState;  // +0x10

    // Forces a specific suppression state on entity
    void Execute() {
        AssignSuppression();
    }
}
```

### AssignMoraleToEntityAction (TypeDefIndex: 1650)
```c
public class AssignMoraleToEntityAction : TacticalAction {
    private readonly MoraleState m_MoraleState;  // +0x10

    void Execute() {
        AssignMorale();
    }
}
```

## TacticalConfig Fields

The TacticalConfig contains suppression-related settings:

| Field | Type | Notes |
|-------|------|-------|
| SuppressionImpactMult | float | Multiplier for suppression effects (+0x1D0) |
| (thresholds) | float | State transition thresholds |

## Conversation Triggers

Suppression/morale state changes can trigger conversations:
- `ConversationTriggerType.EntitySuppressed = 14`
- `ConversationTriggerType.EntityMoraleRecovered = 44`

## Role Requirements

Conversations can require specific suppression/morale states:

```c
public class SuppressionRoleRequirement : BaseRoleRequirement {
    public SuppressionState State;  // +0x14
}

public class MoraleRoleRequirement : BaseRoleRequirement {
    public MoraleState State;  // +0x14
}
```

## Statistics Tracking

The game tracks suppression statistics:
- `UnitStatistic.SuppressionDealt = 23`
- `UnitStatistic.SuppressionReceived = 24`

## UI Elements

HUD elements for displaying suppression/morale:
- `m_SuppressionBar` - Progress bar showing suppression level
- `SuppressedIcon` - Icon when suppressed
- `PinnedDownIcon` - Icon when pinned down
- `StanceSuppressedIcon` - Stance indicator for suppression

## Gameplay Effects

When suppressed:
1. **Action Point Reduction**: `ChangeSuppressionAndUpdateAP` reduces available AP
2. **Movement Restriction**: PinnedDown state prevents most actions
3. **Accuracy Penalty**: Suppressed units have reduced accuracy
4. **Cover Seeking**: AI prioritizes getting to cover when suppressed

When morale breaks:
1. **Fleeing State**: Unit attempts to leave combat area
2. **Wavering State**: Unit has reduced effectiveness
3. **Recovery**: Morale can recover over time or via abilities

## Tag Types

- `TagType.SUPPRESSIVE = 5` - Tag for weapons/skills that cause suppression
