# TileEffects

`Menace.SDK.TileEffects` -- Static class for tile effect operations including fire, smoke, ammo crates, and other tile-based effects.

## Overview

The TileEffects SDK provides safe access to spawning, querying, and removing tile effects during tactical gameplay. Tile effects are persistent environmental hazards or objects that occupy map tiles, such as fire, smoke grenades, ammo crates, and bleed-out markers.

This is based on the game's `TileEffectHandler` system with support for various effect types including:
- `ApplySkillTileEffectHandler` - Skill-triggered effects
- `BleedOutTileEffectHandler` - Bleeding unit markers
- `RefillAmmoTileEffectHandler` - Ammo resupply crates
- Fire and smoke effects

## Methods

### GetEffects

```csharp
public static List<EffectInfo> GetEffects(int x, int y)
public static List<EffectInfo> GetEffects(GameObj tile)
```

Get all effects currently on a tile.

**Parameters:**
- `x`, `y` - Tile coordinates
- `tile` - Tile game object

**Returns:** List of `EffectInfo` objects describing each effect on the tile.

### HasEffects

```csharp
public static bool HasEffects(int x, int y)
public static bool HasEffects(GameObj tile)
```

Check if a tile has any effects.

**Returns:** `true` if the tile has one or more effects.

### HasEffectType

```csharp
public static bool HasEffectType(int x, int y, string typeNameContains)
```

Check if a tile has a specific effect type by name pattern. Matches against both the effect type name and template name (case-insensitive).

**Parameters:**
- `x`, `y` - Tile coordinates
- `typeNameContains` - Substring to match in the effect type or template name

**Returns:** `true` if a matching effect is found.

### IsOnFire

```csharp
public static bool IsOnFire(int x, int y)
```

Check if a tile is on fire.

**Returns:** `true` if the tile has a fire effect.

### HasSmoke

```csharp
public static bool HasSmoke(int x, int y)
```

Check if a tile has smoke.

**Returns:** `true` if the tile has a smoke effect.

### HasAmmo

```csharp
public static bool HasAmmo(int x, int y)
```

Check if a tile has an ammo crate or refill effect.

**Returns:** `true` if the tile has an ammo/refill effect.

### HasBleedingUnit

```csharp
public static bool HasBleedingUnit(int x, int y)
```

Check if a tile has a bleeding out unit marker.

**Returns:** `true` if a bleed-out effect is present.

### ClearEffects

```csharp
public static int ClearEffects(int x, int y)
public static int ClearEffects(GameObj tile)
```

Remove all effects from a tile.

**Parameters:**
- `x`, `y` - Tile coordinates
- `tile` - Tile game object

**Returns:** Number of effects removed.

### SpawnEffect

```csharp
public static bool SpawnEffect(int x, int y, string templateName, int delay = 0)
public static bool SpawnEffect(GameObj tile, string templateName, int delay = 0)
```

Spawn a tile effect by template name.

**Parameters:**
- `x`, `y` - Tile coordinates
- `tile` - Tile game object
- `templateName` - Name of the effect template (e.g., "Fire", "Smoke")
- `delay` - Optional delay in rounds before the effect activates (default: 0)

**Returns:** `true` if the effect was spawned successfully.

### GetAvailableEffectTemplates

```csharp
public static string[] GetAvailableEffectTemplates()
```

Get all effect templates available in the game.

**Returns:** Sorted array of template names that can be used with `SpawnEffect`.

## Types

### EffectInfo

Information about a tile effect.

```csharp
public class EffectInfo
{
    public string TypeName { get; set; }       // Effect handler type name
    public string TemplateName { get; set; }   // Template name used to create the effect
    public int RoundsElapsed { get; set; }     // Number of rounds since effect started
    public int Duration { get; set; }          // Total duration in rounds (0 = permanent)
    public int RoundsRemaining { get; set; }   // Rounds until effect expires
    public bool BlocksLOS { get; set; }        // Whether effect blocks line of sight
    public IntPtr Pointer { get; set; }        // Native pointer to effect handler
}
```

## Examples

### Checking for hazards before movement

```csharp
int targetX = 5;
int targetY = 10;

if (TileEffects.IsOnFire(targetX, targetY))
{
    DevConsole.Log("Warning: Target tile is on fire!");
}

if (TileEffects.HasSmoke(targetX, targetY))
{
    DevConsole.Log("Tile has smoke - visibility reduced");
}
```

### Listing effects on a tile

```csharp
var effects = TileEffects.GetEffects(x, y);
foreach (var effect in effects)
{
    var duration = effect.Duration > 0
        ? $" ({effect.RoundsRemaining} rounds left)"
        : " (permanent)";
    DevConsole.Log($"Effect: {effect.TemplateName}{duration}");

    if (effect.BlocksLOS)
        DevConsole.Log("  - Blocks line of sight");
}
```

### Spawning fire on a tile

```csharp
// Spawn fire immediately
bool success = TileEffects.SpawnEffect(x, y, "Fire");
if (success)
    DevConsole.Log($"Fire started at ({x}, {y})");

// Spawn smoke with 1 round delay
TileEffects.SpawnEffect(x, y, "Smoke", delay: 1);
```

### Clearing tile effects

```csharp
// Clear all effects (fire, smoke, etc.) from a tile
int cleared = TileEffects.ClearEffects(x, y);
DevConsole.Log($"Removed {cleared} effects from tile");
```

### Finding available effect templates

```csharp
var templates = TileEffects.GetAvailableEffectTemplates();
DevConsole.Log($"Available effect templates ({templates.Length}):");
foreach (var template in templates)
{
    DevConsole.Log($"  - {template}");
}
```

### Checking for ammo resupply

```csharp
var actor = TacticalController.GetActiveActor();
var pos = EntityMovement.GetPosition(actor);

if (TileEffects.HasAmmo((int)pos.x, (int)pos.y))
{
    DevConsole.Log("Standing on ammo crate - refill available");
}
```

### Custom effect type check

```csharp
// Check for any poison-related effect
if (TileEffects.HasEffectType(x, y, "Poison"))
{
    DevConsole.Log("Tile has poison effect!");
}

// Check for any healing effect
if (TileEffects.HasEffectType(x, y, "Heal"))
{
    DevConsole.Log("Tile provides healing");
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

- `effects <x> <y>` - List all effects on a tile at the specified coordinates
- `hasfire <x> <y>` - Check if a tile is on fire
- `hassmoke <x> <y>` - Check if a tile has smoke
- `cleareffects <x> <y>` - Remove all effects from a tile
- `spawneffect <x> <y> <template>` - Spawn an effect on a tile using the specified template name
- `effecttypes` - List all available effect templates (shows first 30)

### Console Command Examples

```
> effects 10 15
Effects on (10, 15):
  FireTileEffectHandler: Fire (2 rounds left)

> hasfire 10 15
Tile (10, 15) on fire: True

> cleareffects 10 15
Cleared 1 effects from (10, 15)

> spawneffect 12 8 Smoke
Spawned 'Smoke' at (12, 8)

> effecttypes
Effect templates (15):
  AcidCloud
  AmmoCrate
  Fire
  FireLarge
  HealingZone
  Poison
  Smoke
  SmokeGrenade
  ...
```
