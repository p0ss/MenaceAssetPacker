# Tile Effects System

## Overview

The Tile Effects system manages persistent effects on map tiles such as fire, smoke, bleed-out markers, ammo crates, and recoverable objects. Effects are handler-based with lifecycle callbacks (OnEnter, OnLeave, OnRoundStart) and can trigger skills, apply status effects, or interact with objectives.

## Architecture

```
TileEffectHandler (base handler)
├── Tile Tile                                    // +0x10 (assigned tile)
├── int RoundsElapsed                            // +0x18 (increments each round)
├── int Delay                                    // +0x1C (initial spawn delay)
├── GameObject VisualInstance                    // +0x20 (prefab instance)
└── Virtual methods: OnAdded, OnRemoved, OnEnter, OnLeave, OnRoundStart, OnTurnEnd

TileEffectTemplate (base template)
├── DataTemplate base                            // +0x00 - 0x77
├── LocalizedLine Title                          // +0x78 (display name)
├── LocalizedMultiLine Description               // +0x80 (tooltip text)
├── bool HasDuration                             // +0x8C (lifetime limited)
├── int Duration                                 // +0x90 (rounds, default: 1)
├── GameObject Prefab                            // +0x98 (visual prefab)
├── int VisualCount                              // +0xA0 (particle count, default: 1)
├── float DestroyDelay                           // +0xA8 (cleanup delay, default: 20.0f)
└── bool BlocksLineOfSight                       // +0xAC (blocks LOS when true)

Tile (effect storage)
└── List<TileEffectHandler> Effects              // +0x68
```

## TileEffectHandler Base Class

### Field Layout

```c
public class TileEffectHandler {
    // Object header                              // +0x00 - 0x0F
    Tile Tile;                                    // +0x10 (assigned tile reference)
    int RoundsElapsed;                            // +0x18 (rounds since spawned)
    int Delay;                                    // +0x1C (delay before activation)
    GameObject VisualInstance;                    // +0x20 (instantiated prefab)
}
```

### Key Methods

```c
// Constructor
void TileEffectHandler.ctor(int delay);  // @ 180697fb0

// Tile assignment
void AssignToTile(Tile tile);  // @ 1806978a0
void RemoveFromTile();  // @ 180697b90

// Lifecycle
void StartRound(int roundNumber);  // @ 180697e40
int GetLifetimeLeft();  // @ 180697b00

// Display
void AppendTooltipData(TooltipData data);  // @ 1806977f0
void Refresh();  // @ 180697b70

// Virtual callbacks (overridden by subclasses)
virtual void OnAdded();
virtual void OnRemoved();
virtual void OnEnter(Entity entity);
virtual void OnLeave(Entity entity);
virtual void OnRoundStart();
virtual void OnTurnEnd();
virtual void OnMovementFinished(Entity entity);
virtual TileEffectTemplate GetTemplate();  // vtable +0x178
```

### AssignToTile Flow

```c
// @ 1806978a0
void AssignToTile(Tile tile) {
    this.Tile = tile;  // +0x10

    // Subscribe to round start events
    TacticalManager.Instance.OnRoundStart += StartRound;

    // Get template
    TileEffectTemplate template = GetTemplate();  // vtable +0x178

    // Spawn visual prefab after delay
    if (template.Prefab != null) {  // +0x98
        Schedule.Delay(this.Delay, () => {  // +0x1C
            // Instantiate prefab at tile position
        });
    }

    // Block LOS if configured
    if (template.BlocksLineOfSight) {  // +0xAC
        tile.BlockLineOfSight();
    }

    // Call OnAdded callback
    OnAdded();  // vtable +0x198

    // Trigger OnEnter if actor present
    if (tile.HasActor()) {
        OnEnter(tile.GetActor());  // vtable +0x1C8
    }
}
```

### StartRound Flow

```c
// @ 180697e40
void StartRound(int roundNumber) {
    RoundsElapsed++;  // +0x18

    TileEffectTemplate template = GetTemplate();

    // Check if effect expired
    if (template.HasDuration && RoundsElapsed >= template.Duration) {  // +0x8C, +0x90
        Tile.RemoveEffect(this);
        return;
    }

    // Call virtual OnRoundStart
    OnRoundStart();  // vtable +0x1E8
}
```

## Effect Handler Types

### ApplySkillTileEffectHandler

Applies a skill when entities enter or on round/turn events.

```c
public class ApplySkillTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    ApplySkillTileEffectTemplate Template;        // +0x28
    Skill SkillInstance;                          // +0x30 (created from template)
}

public class ApplySkillTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    SkillTemplate Skill;                          // +0xC0 (skill to apply)
    SoundID ApplySound;                           // +0xC8
    bool SingleUse;                               // +0xD0 (remove after one use)
    ITacticalCondition Condition;                 // +0xD8 (target filter)
}
```

**Key Methods:**
```c
void ApplySkillTileEffectHandler.ctor(int delay, ApplySkillTileEffectTemplate template);  // @ 180684f30
void ApplySkill(Entity target);  // @ 1806848a0
bool IsValidTarget(Entity target);  // @ 180684bd0
void ForceTrigger();  // @ 180684ae0

// Callbacks
void OnAdded();  // @ 180684d20
void OnEnter(Entity entity);  // @ 180684dd0
void OnLeave(Entity entity);  // @ 180684e00
void OnRoundStart();  // @ 180684e60
void OnTurnEnd();  // @ 180684e90
void OnTurnStart();  // @ 180684ec0
void OnMovementFinished(Entity entity);  // @ 180684e30
```

---

### ApplyStatusEffectTileEffectHandler

Applies status effect skills based on tag filtering.

```c
public class ApplyStatusEffectTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    ApplyStatusEffectTileEffectTemplate Template; // +0x28
}

public class ApplyStatusEffectTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    SkillTemplate StatusEffect;                   // +0xB8 (skill to apply)
    List<TagTemplate> RequiredTags;               // +0xC0 (target must have)
    List<TagTemplate> ExcludedTags;               // +0xC8 (target must not have)
}
```

**Key Methods:**
```c
void ApplyStatusEffectTileEffectHandler.ctor(int delay, ApplyStatusEffectTileEffectTemplate template);  // @ 180685720
void OnEnter(Entity entity);  // @ 1806853a0
void OnLeave(Entity entity);  // @ 1806855b0
void AppendTooltipData(TooltipData data);  // @ 180685270
```

**OnEnter Flow:**
```c
// @ 1806853a0
void OnEnter(Entity entity) {
    if (Template.StatusEffect == null) return;

    Skill skill = Template.StatusEffect.CreateSkill();

    // Check required tags
    if (Template.RequiredTags.Count > 0) {
        if (!entity.HasAnyOfTheseTags(Template.RequiredTags)) {
            return;  // Entity lacks required tags
        }
    }

    // Check excluded tags
    if (Template.ExcludedTags.Count > 0) {
        if (entity.HasAnyOfTheseTags(Template.ExcludedTags)) {
            return;  // Entity has excluded tag
        }
    }

    // Apply skill
    entity.GetSkills().Add(skill);
    TacticalManager.Instance.InvokeOnSkillAdded(entity, skill, fromEffect: true);
}
```

---

### BleedOutTileEffectHandler

Manages bleeding out/downed unit mechanics.

```c
public class BleedOutTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    BleedOutTileEffectTemplate Template;          // +0x28
    BaseUnitLeader DownedLeader;                  // +0x30 (the bleeding unit)
    BleedingWorldSpaceIcon UIIcon;                // +0x38 (rounds remaining display)
}

public class BleedOutTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    int RoundsToBleedOut;                         // +0xB8 (turns until death)
    SoundID BleedingSound;                        // +0xBC (tick sound)
    SoundID StabilizedSound;                      // +0xC4 (rescue sound)
    SoundID DiedSound;                            // +0xCC (death sound)
}
```

**Key Methods:**
```c
void BleedOutTileEffectHandler.ctor(int delay, BleedOutTileEffectTemplate template);  // @ 180686450
void OnAdded();  // @ 180685910
void OnRemoved();  // @ 180685d10
void OnRoundStart();  // @ 180685fc0
void OnMissionPreFinished();  // @ 180685c10
void StabilizeLeader(Actor stabilizer, bool removeEffect, bool playSound);  // @ 180686240
```

**OnRoundStart Flow:**
```c
// @ 180685fc0
void OnRoundStart() {
    int roundsRemaining = Template.RoundsToBleedOut - RoundsElapsed;  // +0xB8, +0x18

    if (roundsRemaining < 1) {
        // Unit dies from blood loss
        DownedLeader.Die();  // +0x30
        PlaySound(Template.DiedSound);  // +0xCC
        Tile.RemoveEffect(this);
        return;
    }

    // Update bleed counter
    DownedLeader.BleedRoundsRemaining = roundsRemaining;  // offset 0x78
    PlaySound(Template.BleedingSound);  // +0xBC
    UIIcon.SetText(roundsRemaining.ToString());  // +0x38

    TacticalManager.Instance.InvokeOnBleedingOut(DownedLeader, roundsRemaining);
}
```

**StabilizeLeader Flow:**
```c
// @ 180686240
void StabilizeLeader(Actor stabilizer, bool removeEffect, bool playSound) {
    if (playSound) {
        PlaySound(Template.StabilizedSound);  // +0xC4
    }

    DownedLeader.SetHealthStatus(HealthStatus.Injured);  // +0x30
    DownedLeader.Statistics.TrackStabilized(DownedLeader, stabilizer);

    if (removeEffect) {
        Tile.RemoveEffect(this);
    }

    TacticalManager.Instance.InvokeOnStabilized(DownedLeader, stabilizer);
}
```

---

### RecoverableObjectTileEffectHandler

Pickable objects like dropped weapons or objectives.

```c
public class RecoverableObjectTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    RecoverableObjectTileEffectTemplate Template; // +0x28
    bool IsFromEnemy;                             // +0x30 (enemy dropped)
    RecoverObject ObjectiveRef;                   // +0x38 (objective link)
}

public class RecoverableObjectTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    SkillTemplate PickupSkill;                    // +0xB8 (skill granted on pickup)
    SoundID PickupSound;                          // +0xC0
    bool CanPlayerPickup;                         // +0xC8
    bool CanEnemyPickup;                          // +0xC9
}
```

**Key Methods:**
```c
void RecoverableObjectTileEffectHandler.ctor(int delay, RecoverableObjectTileEffectTemplate template);  // @ 180694480
bool CanPickup(Entity entity);  // @ 180694010
bool TryPickup(Entity entity);  // @ 180694170
void OnAdded();  // @ 180694110
void OnMovementFinished(Entity entity);  // @ 180694160
```

---

### RefillAmmoTileEffectHandler

Ammo crate mechanics for restoring ammunition.

```c
public class RefillAmmoTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    RefillAmmoTileEffectTemplate Template;        // +0x28
    int FactionFilter;                            // +0x30 (0 = all factions)
}

public class RefillAmmoTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    SoundID PickupSound;                          // +0xB8
    int GrenadeRefill;                            // +0xC0 (grenades to restore)
    int AmmoRefill;                               // +0xC4 (ammo to restore)
    SkillTemplate SpecificSkill;                  // +0xC8 (optional: refill specific skill)
}
```

**TryPickup Flow:**
```c
// @ 180694670
void TryPickup(Actor actor) {
    // Check faction filter
    if (FactionFilter != 0 && actor.Faction != FactionFilter) {  // +0x30, +0x4C
        return;
    }

    // Check if actor can receive ammo
    if (!actor.IsAlive || actor.GetSkills() == null) {
        return;
    }

    // Refill ammo
    actor.RefillAmmo(
        Template.GrenadeRefill,   // +0xC0
        Template.AmmoRefill,      // +0xC4
        Template.SpecificSkill    // +0xC8
    );

    PlaySound(Template.PickupSound);  // +0xB8
    actor.HUD.ShowDropDownText("Resupplied");

    Tile.RemoveEffect(this);
}
```

---

### AddItemTileEffectHandler

Grants items when entities enter tile.

```c
public class AddItemTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    AddItemTileEffectTemplate Template;           // +0x28
}

public class AddItemTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    BaseItemTemplate ItemToAdd;                   // +0xB8 (item granted)
    SoundID PickupSound;                          // +0xC0
}
```

---

### SpawnObjectTileEffectHandler

Spawns environmental objects on tiles.

```c
public class SpawnObjectTileEffectHandler : TileEffectHandler {
    // TileEffectHandler fields...
    SpawnObjectTileEffectTemplate Template;       // +0x28
}

public class SpawnObjectTileEffectTemplate : TileEffectTemplate {
    // TileEffectTemplate fields...
    GameObject ObjectPrefab;                      // +0xB8 (object to spawn)
    // Additional spawn configuration...
}
```

## Tile Effect Management

### Tile.RemoveEffect

```c
// @ 180682730
void Tile.RemoveEffect(TileEffectHandler effect) {
    effect.RemoveFromTile();  // Cleanup handler

    Effects.Remove(effect);  // +0x68
    UpdateTileEffectFlags();  // Refresh tile state
}
```

### TileEffectHandler.RemoveFromTile

```c
// @ 180697b90
void RemoveFromTile() {
    // Unsubscribe from events
    TacticalManager.Instance.OnRoundStart -= StartRound;

    // Call OnLeave if actor present
    if (Tile.HasActor()) {
        OnLeave(Tile.GetActor());
    }

    // Call OnRemoved callback
    OnRemoved();

    TileEffectTemplate template = GetTemplate();

    // Destroy visual with configured delay
    if (VisualInstance != null) {  // +0x20
        // Stop particle systems
        foreach (ParticleSystem ps in VisualInstance.GetComponentsInChildren<ParticleSystem>()) {
            ps.Stop();
        }
        Object.Destroy(VisualInstance, template.DestroyDelay);  // +0xA8
        VisualInstance = null;
    }

    // Unblock LOS if was blocking
    if (template.BlocksLineOfSight) {  // +0xAC
        Tile.UnblockLineOfSight();
    }

    Tile = null;  // +0x10
}
```

## Effect Spawning

### SpawnTileEffectHandler (Skill Effect)

Creates tile effects from skill usage.

```c
// @ 18071dbe0
void SpawnTileEffectHandler.Apply(SkillContext context) {
    SpawnTileEffect effect = (SpawnTileEffect)GetEffect();

    Tile targetTile = context.TargetTile;
    int delay = GetDelay();  // From skill timing

    // Create handler from template
    TileEffectTemplate template = effect.TileEffectTemplate;  // +0xB8
    TileEffectHandler handler = template.CreateHandler(delay);

    // Add to tile
    targetTile.AddEffect(handler);
}
```

## Modding Hooks

### Intercept Effect Application

```csharp
[HarmonyPatch(typeof(TileEffectHandler), "AssignToTile")]
class EffectAssignPatch {
    static void Prefix(TileEffectHandler __instance, Tile tile) {
        Logger.Msg($"Effect {__instance.GetType().Name} assigned to tile at {tile.Position}");
    }
}
```

### Modify Bleed Duration

```csharp
[HarmonyPatch(typeof(BleedOutTileEffectHandler), "OnRoundStart")]
class BleedDurationPatch {
    static void Prefix(BleedOutTileEffectHandler __instance) {
        // Give extra rounds to bleed
        var template = Traverse.Create(__instance).Field("Template").GetValue<object>();
        int rounds = Traverse.Create(template).Field("RoundsToBleedOut").GetValue<int>();
        Traverse.Create(template).Field("RoundsToBleedOut").SetValue(rounds + 1);
    }
}
```

### Custom Pickup Logic

```csharp
[HarmonyPatch(typeof(RecoverableObjectTileEffectHandler), "CanPickup")]
class PickupPatch {
    static void Postfix(ref bool __result, Entity entity) {
        // Allow all entities to pickup
        if (DebugSettings.AllowAllPickups) {
            __result = true;
        }
    }
}
```

### Add Custom Tile Effect

```csharp
[HarmonyPatch(typeof(Tile), "AddEffect")]
class CustomEffectPatch {
    static void Postfix(Tile __instance, TileEffectHandler handler) {
        // Track all fire effects
        if (handler is ApplySkillTileEffectHandler skillHandler) {
            if (skillHandler.Template.name.Contains("Fire")) {
                FireTracker.RegisterFire(__instance.Position);
            }
        }
    }
}
```

## Key Constants

```c
// Default template values
const int DEFAULT_DURATION = 1;
const int DEFAULT_VISUAL_COUNT = 1;
const float DEFAULT_DESTROY_DELAY = 20.0f;  // 0x41A00000

// Health status values
const int HEALTH_INJURED = 2;

// Faction values for RefillAmmo
const int FACTION_ANY = 0;
const int FACTION_NEUTRAL = 3;
```

## Related Classes

- **Tile**: Map tile containing effects list at +0x68
- **TacticalManager**: Round event dispatch, skill notifications
- **SkillTemplate**: Used by ApplySkill/ApplyStatusEffect handlers
- **BaseUnitLeader**: Target of BleedOut effect
- **RecoverObject**: Objective system integration
- **BleedingWorldSpaceIcon**: UI for bleed countdown

