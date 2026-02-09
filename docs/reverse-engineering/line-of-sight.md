# Line of Sight & Visibility System

## Overview

The Line of Sight (LOS) and Visibility system controls what units can see, how detection works, and manages fog of war. It uses ray-casting between tiles with blockers (structures, cover, effects) and combines with detection/concealment stats to determine visibility.

## Architecture

```
Visibility Flow:
Actor → Vision Range → LOS Check → Detection vs Concealment → Visibility State

Tile Visibility (per-tile data)
├── ulong VisibilityMask                         // +0x58 (bit per faction)
├── bool IsBlockingLOS                           // +0x60
└── int Flags                                    // +0x1C (bit 11 = has LOS blocker)

Actor Visibility
├── int VisibilityState                          // +0x90 (Visibility enum)
├── bool FirstTimeVisible                        // +0x16D
└── bool IsDetectedByPlayer                      // derived

EntityProperties (vision stats)
├── int BaseVision                               // +0xC4 (vision range)
├── float VisionMult                             // +0xC8 (vision multiplier)
├── int BaseDetection                            // +0xCC (detection stat)
└── float DetectionMult                          // +0xD0 (detection multiplier)

Map (fog of war)
├── bool UseFogOfWar                             // +0x38
└── TileHighlighter                              // +0x98 (revealed tiles)
```

## Visibility Enum

```c
enum Visibility {
    Unknown = 0,    // Never seen
    Visible = 1,    // Currently visible
    Hidden = 2,     // Was seen, now hidden (fog of war)
    Detected = 3    // Detected but not directly visible
}
```

## Core LOS Algorithm

### Tactical.LineOfSight.HasLineOfSight

The main ray-tracing function that checks LOS between two tiles.

```c
// @ 18051df40
bool HasLineOfSight(Tile fromTile, Tile toTile, byte flags) {
    // Same tile = always visible
    if (fromTile == toTile) return true;

    // Check if target is LOS blocker (bit 11 in flags at +0x1C)
    bool targetHasBlocker = (toTile.Flags & 0x800) != 0;

    // Skip blocker check for certain flag combinations
    if ((flags & 1) != 0 && !targetHasBlocker) {
        // Target isn't a blocker, proceed
    } else {
        // Check if target blocks LOS
        if (toTile.IsBlockingLineOfSight()) {  // +0x60
            if (!toTile.IsEmpty()) {
                return false;  // Blocked by structure/cover
            }
        }
    }

    // Direct neighbors always have LOS (unless blocker check fails)
    if (fromTile.IsDirectNeighbor(toTile)) {
        if (targetHasBlocker && (flags & 4) == 0) {
            // Check if line overlaps blocker geometry
            Vector2 fromPos = fromTile.GetPos();
            Vector2 toPos = toTile.GetPos();
            return !toTile.IsOverlappingWithLineOfSightBlocker(fromPos, toPos);
        }
        return true;
    }

    // Calculate ray direction
    Vector2 fromPos = fromTile.GetPos();
    Vector2 toPos = toTile.GetPos();
    float dx = toPos.x - fromPos.x;
    float dy = toPos.y - fromPos.y;
    float length = sqrt(dx * dx + dy * dy);

    if (length > EPSILON) {
        dx /= length;
        dy /= length;
    }

    // Get structure at target (for multi-tile structures)
    Structure targetStructure = null;
    if ((flags & 1) == 0) {
        Entity entity = toTile.GetEntity();
        if (entity is Structure s) {
            targetStructure = s;
        }
    }

    Map map = TacticalManager.Instance.Map;
    float tileSize = TILE_SIZE;  // ~0.5
    int iterations = 0;
    Tile currentTile = fromTile;

    // Ray march through tiles
    while (currentTile != toTile) {
        iterations++;
        if (iterations > 1000) {
            Debug.LogError($"LOS infinite loop: {fromTile} → {toTile}");
            return false;
        }

        // Step along ray
        fromPos.x += dx * 2;
        fromPos.y += dy * 2;

        // Get tile at new position
        Tile nextTile = map.GetTile((int)(fromPos.x * tileSize), (int)(fromPos.y * tileSize));

        if (nextTile == currentTile) continue;  // Same tile
        if (nextTile == null) return false;      // Off map

        // Early exit for target with flags
        if ((flags & 1) != 0 && nextTile == toTile) {
            return true;
        }

        currentTile = nextTile;

        // Check if this tile is blocked by target structure (skip multi-tile structures)
        if (targetStructure != null) {
            if (targetStructure.IsTileLoSBlockedByThisStructure(nextTile)) {
                continue;  // Part of same structure, skip
            }
        }

        // Check if tile blocks LOS
        if (nextTile.IsBlockingLineOfSight()) {  // +0x60
            return false;
        }

        // Check for LOS blocker overlap
        if ((nextTile.Flags & 0x800) != 0 && (flags & 4) == 0) {
            if (nextTile.IsOverlappingWithLineOfSightBlocker(fromPos, toPos)) {
                return false;
            }
        }
    }

    return true;
}
```

### Tile.HasLineOfSightTo

Wrapper that checks LOS bidirectionally.

```c
// @ 180681d70
bool Tile.HasLineOfSightTo(Tile other, byte flags) {
    // Check if entity on this tile has special vision (sniper scope, etc.)
    if (this.Entity != null) {
        Entity entity = this.Entity.GetOwner();
        if (entity != null) {
            EntityProperties props = entity.GetProperties();
            if (props.VisionType == 4) {  // +0xC8 = 4
                flags |= 4;  // Ignore certain blockers
            }
        }
    }

    // Check both directions (asymmetric LOS can occur)
    if (LineOfSight.HasLineOfSight(this, other, flags)) {
        return true;
    }
    return LineOfSight.HasLineOfSight(other, this, flags);
}
```

### Actor.HasLineOfSightTo

Full visibility check including detection/concealment.

```c
// @ 1805dfa10
bool Actor.HasLineOfSightTo(Entity target, bool wasDetected, Tile fromTile, Tile toTile) {
    // Check if this actor is suppressed (can't see)
    if (this.IsSuppressed) {
        Entity vehicle = this.Vehicle;
        if (vehicle != null && vehicle.IsFullyClosed()) {
            return false;
        }
    }

    // Get tiles if not provided
    if (fromTile == null) {
        fromTile = this.GetTile();
    }
    if (toTile == null) {
        toTile = target.GetTile();
    }

    if (fromTile == null || toTile == null) return false;
    if (target == this) return true;

    // Basic LOS check
    if (!fromTile.HasLineOfSightTo(toTile, 0)) {
        return false;
    }

    // Get distance
    int distance = fromTile.GetDistanceTo(toTile);

    // Get vision stats
    EntityProperties myProps = this.GetProperties();
    int vision = myProps.GetVision();  // +0xC4 * +0xC8

    // Check if target is marked/painted (always visible)
    if (target is Actor targetActor && targetActor.IsMarked) {  // +0x1AC
        return distance <= vision;
    }

    // Calculate cover concealment
    int coverConcealment = 0;
    if (!wasDetected && fromTile != target.GetTile()) {
        if (distance > 1) {
            // Get cover between target and attacker
            Direction dir = toTile.GetDirectionTo(fromTile);
            int coverLevel = toTile.GetCover(dir, target, 0, true);
            coverConcealment = Config.CoverConcealmentValues[coverLevel];  // +0xA0
        }
    } else {
        // Target is on structure - use structure's concealment
        Entity tileEntity = toTile.GetEntity();
        if (tileEntity != null) {
            EntityProperties props = tileEntity.GetProperties();
            if (props.Concealment != null) {  // +0xE0
                coverConcealment = props.Concealment.Value;  // +0x64
                if (distance < 2) {
                    coverConcealment = 0;  // Close range ignores structure concealment
                }
            }
        }
    }

    // Get detection stat
    int detection = myProps.GetDetection();  // +0xCC * +0xD0

    // Get target's concealment
    EntityProperties targetProps = target.GetProperties();
    int concealment = targetProps.GetConcealment();  // +0xD4 * +0xD8

    // Calculate effective vision range
    // Formula: effectiveVision = baseVision - max(0, coverConcealment + concealment - detection)
    int penalty = coverConcealment + concealment - detection;
    if (penalty < 0) penalty = 0;

    int effectiveVision = vision - penalty;
    if (effectiveVision < 1) effectiveVision = 1;

    return distance <= effectiveVision;
}
```

## Tile Visibility Management

### Tile.AddVisibility

```c
// @ 1806805d0
void Tile.AddVisibility(int factionId) {
    // Faction 1 and 2 are player factions (both set)
    if (factionId == 1 || factionId == 2) {
        this.VisibilityMask |= 6;  // +0x58: Set bits 1 and 2
        this.SetFlag(8, false);     // Clear fog flag
        return;
    }

    // Other factions: set single bit
    this.VisibilityMask |= (1L << factionId);  // +0x58
}
```

### Tile.ClearVisibility

```c
// @ 1806809b0
void Tile.ClearVisibility() {
    this.VisibilityMask = 0;  // +0x58
    this.SetFlag(8, true);     // Set fog flag
}
```

### Tile.IsBlockingLineOfSight

```c
// @ 180681e50
bool Tile.IsBlockingLineOfSight() {
    return this.BlocksLOS;  // +0x60, simple boolean
}
```

## Map Visibility Update

### Map.UpdateVisibility

Called when an actor's vision needs to be applied to the map.

```c
// @ 18062f9a0
void Map.UpdateVisibility(Tile centerTile, int factionId, int visionRange, Actor actor) {
    if (centerTile == null) return;

    // Calculate bounds
    float radius = sqrt((visionRange + 1) * (visionRange + 1));
    int maxX = min(centerTile.X + radius, this.Width - 1);
    int maxY = min(centerTile.Y + radius, this.Height - 1);
    int minX = max(centerTile.X - radius, 0);
    int minY = max(centerTile.Y - radius, 0);

    TacticalManager tm = TacticalManager.Instance;
    TileHighlighter highlighter = TacticalState.Instance.TileHighlighter;  // +0x98

    // Iterate tiles in range
    for (int x = minX; x <= maxX; x++) {
        for (int y = minY; y <= maxY; y++) {
            Tile tile = this.Tiles[x, y];

            // Check LOS
            if (!centerTile.HasLineOfSightTo(tile, 1)) {
                continue;
            }

            // Check distance
            int distance = centerTile.GetDistanceTo(tile);
            if (distance > visionRange) {
                continue;
            }

            // Mark tile visible
            tile.AddVisibility(factionId);

            // Update fog of war visuals for player
            if (factionId == 1 || factionId == 2) {
                highlighter.Unset(tile, 0);

                // Handle multi-tile structures
                if (tile.HasStructure()) {
                    Entity entity = tile.GetEntity();
                    foreach (EntitySegment segment in entity.Segments) {
                        segment.Tile.AddVisibility(factionId);
                        highlighter.Unset(segment.Tile, 0);
                    }
                }
            }

            // Check for entities on tile
            if (!tile.IsEmpty()) {
                Entity entity = tile.GetEntity();

                if (entity.IsAlive()) {
                    bool wasDetected = entity.ToActor()?.IsDetectedByFaction(factionId) ?? false;

                    // Full actor LOS check (with detection/concealment)
                    if (actor.HasLineOfSightTo(entity, wasDetected, centerTile, null)) {
                        // Notify entity it was seen
                        entity.OnSeenBy(actor);

                        // Add to faction's known enemies
                        if (factionId > 1 && entity.IsAlive() && !entity.IsAlliedWith(factionId)) {
                            Faction faction = tm.GetFaction(factionId);
                            Actor seenActor = entity.ToActor();
                            int detectDuration = AIConfig.Instance.DetectDuration;  // +0x1C
                            faction.AddDetectedActor(seenActor, detectDuration);
                        }

                        // Check vehicle passengers
                        if (entity.HasDriver) {
                            Entity driver = entity.Driver;
                            if (driver.IsAlive()) {
                                // Repeat detection for driver
                                if (actor.HasLineOfSightTo(driver, wasDetected, centerTile, null)) {
                                    driver.OnSeenBy(actor);
                                    // ... add to known enemies
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    actor.WasVisibilityCalculated = false;  // +0x1AD
}
```

### TacticalManager.RecalculateVisibilityBasedOnVision

Recalculates all visibility when needed.

```c
// @ 1806747f0
void TacticalManager.RecalculateVisibilityBasedOnVision() {
    Map map = this.Map;  // +0x28
    map.ClearVisibility();

    Faction[] factions = this.Factions;  // +0xA8

    // Update visibility for faction 1 (player faction 1)
    foreach (Actor actor in factions[1].Actors) {
        if (!actor.IsSuppressed) {
            Entity vehicle = actor.Vehicle;
            if (vehicle == null || !vehicle.IsFullyClosed()) {
                map.UpdateVisibilityForActor(actor);
            }
        }
    }

    // Update visibility for faction 2 (player faction 2)
    foreach (Actor actor in factions[2].Actors) {
        if (!actor.IsSuppressed) {
            Entity vehicle = actor.Vehicle;
            if (vehicle == null || !vehicle.IsFullyClosed()) {
                map.UpdateVisibilityForActor(actor);
            }
        }
    }
}
```

## Fog of War

### Map.SetUseFogOfWar

```c
// @ 18062eda0
void Map.SetUseFogOfWar(bool enabled) {
    if (enabled == this.UseFogOfWar) return;  // +0x38

    this.UseFogOfWar = enabled;
    this.ClearVisibility();

    if (enabled) {
        // Recalculate visibility for all player actors
        Faction playerFaction = TacticalManager.Instance.GetPlayerFaction();

        foreach (Actor actor in playerFaction.Actors) {
            if (!actor.IsSuppressed) {
                Entity vehicle = actor.Vehicle;
                if (vehicle == null || !vehicle.IsFullyClosed()) {
                    Tile tile = actor.GetTile();
                    int faction = actor.FactionId;  // +0x4C
                    int vision = actor.GetProperties().GetVision();
                    this.UpdateVisibility(tile, faction, vision, actor);
                }
            }
        }
    }
}
```

## Actor Visibility State

### Actor.SetVisibility

```c
// @ 1805e7cf0
void Actor.SetVisibility(int newState) {
    if (newState == this.VisibilityState) return;  // +0x90

    this.VisibilityState = newState;  // +0x90

    Debug.Log($"{this.Name} visibility: {newState}");

    if (newState == Visibility.Visible) {  // 1
        if (!this.FirstTimeVisible) {  // +0x16D
            this.FirstTimeVisible = true;

            // Remove "?" icon
            if (this.UnknownIcon != null) {  // +0xE8
                this.UnknownIcon.RemoveFromHUD();
            }
            this.UnknownIcon = null;

            this.UpdateAveragePosition();

            // Slow down time when enemy spotted (if setting enabled)
            if (this.IsOnMap && !this.IsPlayerControlled) {
                float timeScale = TacticalState.Instance.SpottedTimeScale;  // +0x28
                int setting = PlayerSettings.Get(SpottedSlowdownSetting);  // 0xB
                this.SetTimeScale(setting * timeScale);
            }

            TacticalManager.Instance.InvokeOnVisibleToPlayer(this);
        }
    } else if (newState == Visibility.Hidden) {  // 2
        this.OnHiddenToPlayer();
    }

    // Update all elements
    foreach (Element element in this.Elements) {  // +0x20
        element.ChangeVisibilityToPlayer(newState);
    }
}
```

## Vision Stat Calculation

### EntityProperties.GetVision

```c
// @ 18060c7b0
int EntityProperties.GetVision() {
    int baseVision = this.BaseVision;  // +0xC4
    float mult = FloatExtensions.Clamped(this.VisionMult);  // +0xC8

    float result = floor(mult * baseVision);
    return max(0, (int)result);
}
```

### EntityProperties.GetDetection

```c
// @ 18060bd90
int EntityProperties.GetDetection() {
    int baseDetection = this.BaseDetection;  // +0xCC
    float mult = FloatExtensions.Clamped(this.DetectionMult);  // +0xD0

    float result = floor(mult * baseDetection);
    return max(0, (int)result);
}
```

## Cover Concealment

Cover provides concealment that reduces detection range:

```c
// Config.CoverConcealmentValues array (at +0xA0)
// Index = cover level (0-3), Value = concealment bonus

// Example values (may vary):
// 0 = No cover:    0 concealment
// 1 = Low cover:   1 concealment
// 2 = Half cover:  2 concealment
// 3 = Full cover:  3 concealment
```

## Modding Hooks

### Modify Vision Range

```csharp
[HarmonyPatch(typeof(EntityProperties), "GetVision")]
class VisionRangePatch {
    static void Postfix(ref int __result, EntityProperties __instance) {
        // Increase vision range by 2 for all units
        __result += 2;
    }
}
```

### Override LOS Check

```csharp
[HarmonyPatch(typeof(LineOfSight), "HasLineOfSight")]
class LOSPatch {
    static void Postfix(ref bool __result, Tile fromTile, Tile toTile) {
        // Always have LOS for debugging
        if (DebugSettings.IgnoreLOS) {
            __result = true;
        }
    }
}
```

### Intercept Visibility Change

```csharp
[HarmonyPatch(typeof(Actor), "SetVisibility")]
class VisibilityChangePatch {
    static void Postfix(Actor __instance, int newState) {
        if (newState == 1) {  // Visible
            Logger.Msg($"Enemy spotted: {__instance.Name}");
        }
    }
}
```

### Modify Detection Formula

```csharp
[HarmonyPatch(typeof(Actor), "HasLineOfSightTo")]
class DetectionPatch {
    static void Prefix(Actor __instance, ref bool wasDetected) {
        // Treat all enemies as already detected (easier detection)
        wasDetected = true;
    }
}
```

### Disable Fog of War

```csharp
[HarmonyPatch(typeof(Map), "SetUseFogOfWar")]
class FogOfWarPatch {
    static bool Prefix(ref bool enabled) {
        if (DebugSettings.DisableFogOfWar) {
            enabled = false;
        }
        return true;
    }
}
```

## Key Constants

```c
// Visibility states
const int VISIBILITY_UNKNOWN = 0;
const int VISIBILITY_VISIBLE = 1;
const int VISIBILITY_HIDDEN = 2;
const int VISIBILITY_DETECTED = 3;

// Tile flags
const uint FLAG_HAS_LOS_BLOCKER = 0x800;  // Bit 11 at +0x1C
const uint FLAG_FOG = 8;                   // Fog of war flag

// Player faction IDs
const int FACTION_PLAYER_1 = 1;
const int FACTION_PLAYER_2 = 2;

// LOS iteration limit (prevents infinite loops)
const int MAX_LOS_ITERATIONS = 1000;

// EntityProperties offsets
const int OFFSET_BASE_VISION = 0xC4;
const int OFFSET_VISION_MULT = 0xC8;
const int OFFSET_BASE_DETECTION = 0xCC;
const int OFFSET_DETECTION_MULT = 0xD0;
const int OFFSET_CONCEALMENT = 0xD4;
const int OFFSET_CONCEALMENT_MULT = 0xD8;
```

## Related Classes

- **TacticalManager**: Manages visibility recalculation
- **Map**: Contains fog of war state, tile visibility
- **Tile**: Stores per-tile visibility mask, blocking state
- **Actor**: Has visibility state, detection tracking
- **EntityProperties**: Vision, detection, concealment stats
- **Faction**: Tracks detected enemies
- **TileHighlighter**: Visual fog of war rendering
- **Structure**: Multi-tile LOS blocking

