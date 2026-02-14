# AICoordination

The `AICoordination` class provides SDK support for coordinated AI behavior mods (Combined Arms-style). It handles:

- Turn state tracking (per faction)
- Unit classification (suppressor vs damage dealer, formation bands)
- Safe tile score modification
- Agent selection scoring helpers

## Threading Model

| Hook Point | Threading | Write Safety |
|------------|-----------|--------------|
| `OnTurnStart` | Single-threaded | Full write access |
| `GetScoreMultForPickingThisAgent` | Parallel | Read-only shared state |
| `PostProcessTileScores` | Parallel per-agent | Per-agent writes only |
| `Execute` | Sequential | Full write access |

## Quick Start

```csharp
public class MyCoordinationMod : IModpackPlugin
{
    private static AICoordination.CoordinationConfig _config;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _config = new AICoordination.CoordinationConfig
        {
            EnableSequencing = true,
            EnableFocusFire = true,
            EnableCenterOfForces = true,
            EnableFormationDepth = true
        };

        // Patches applied in OnSceneLoaded...
    }

    // Hook: AIFaction.OnTurnStart (SAFE)
    static void OnTurnStart_Postfix(object __instance)
    {
        var faction = new GameObj(((Il2CppObjectBase)__instance).Pointer);
        int factionIndex = AICoordination.GetFactionIndex(faction);
        AICoordination.InitializeTurnState(factionIndex);
    }

    // Hook: Agent.GetScoreMultForPickingThisAgent (READ-ONLY)
    static void GetScoreMult_Postfix(object __instance, ref float __result)
    {
        var agent = new GameObj(((Il2CppObjectBase)__instance).Pointer);
        var actor = agent.ReadObj("m_Actor");
        int factionIndex = AICoordination.GetAgentFactionIndex(agent);
        var state = AICoordination.GetFactionState(factionIndex);

        __result *= AICoordination.CalculateAgentScoreMultiplier(actor, state, _config);
    }

    // Hook: Agent.PostProcessTileScores (PER-AGENT WRITES ONLY)
    static void PostProcessTileScores_Postfix(object __instance)
    {
        var agent = new GameObj(((Il2CppObjectBase)__instance).Pointer);
        int factionIndex = AICoordination.GetAgentFactionIndex(agent);
        var state = AICoordination.GetFactionState(factionIndex);

        AICoordination.ApplyTileScoreModifiers(agent, state, _config);
    }

    // Hook: Agent.Execute (SAFE)
    static void Execute_Postfix(object __instance)
    {
        var agent = new GameObj(((Il2CppObjectBase)__instance).Pointer);
        int factionIndex = AICoordination.GetAgentFactionIndex(agent);
        AICoordination.RecordAgentExecution(agent, factionIndex);
    }
}
```

## Classes

### FactionTurnState

Per-faction state tracked during a turn.

| Property | Type | Description |
|----------|------|-------------|
| `FactionIndex` | int | Faction this state belongs to |
| `HasSuppressorActed` | bool | Whether a suppressor has acted |
| `HasDamageDealerActed` | bool | Whether a damage dealer has acted |
| `ActedCount` | int | Number of units that have acted |
| `TargetedTiles` | HashSet<IntPtr> | Tiles targeted by attacks |
| `AllyPositions` | List<(int x, int z)> | Ally positions at turn start |
| `AllyCentroid` | (float x, float z) | Pre-computed ally centroid |
| `EnemyPositions` | List<(int x, int z)> | Enemy positions at turn start |
| `EnemyCentroid` | (float x, float z) | Pre-computed enemy centroid |

**Note:** The game uses X/Z coordinates for the horizontal plane, not X/Y.
| `CustomData` | Dictionary<string, object> | Mod-specific storage |

### CoordinationConfig

Configuration for coordination features.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableSequencing` | bool | true | Suppressors act before damage dealers |
| `SuppressorPriorityBoost` | float | 1.5 | Multiplier for suppressor selection |
| `DamageDealerPenalty` | float | 0.7 | Multiplier for damage dealer selection |
| `EnableFocusFire` | bool | true | Prioritize units targeting same enemy |
| `FocusFireBoost` | float | 1.4 | Multiplier for focus fire |
| `EnableCenterOfForces` | bool | true | Bonus for tiles near ally centroid |
| `CenterOfForcesWeight` | float | 3.0 | Utility bonus weight |
| `CenterOfForcesMaxRange` | float | 12 | Max range for CoF bonus |
| `CenterOfForcesMinAllies` | int | 2 | Minimum allies for CoF |
| `EnableFormationDepth` | bool | true | Position units by role |
| `FormationDepthWeight` | float | 2.5 | Depth bonus weight |
| `FormationDepthMaxRange` | float | 18 | Max range for depth calc |
| `FrontlineFraction` | float | 0.33 | Frontline band size |
| `MidlineFraction` | float | 0.34 | Midline band size |
| `FormationDepthMinEnemies` | int | 1 | Minimum enemies for depth |

### UnitRole

Unit role classification.

| Value | Description |
|-------|-------------|
| `Unknown` | Could not classify |
| `Suppressor` | Prefers suppression over damage |
| `DamageDealer` | Prefers damage over suppression |
| `Balanced` | Similar suppression and damage |
| `Support` | Low combat, high move |
| `Defensive` | High safety scale |

### FormationBand

Formation depth band.

| Value | Description |
|-------|-------------|
| `Frontline` | Close to enemies (assault units) |
| `Midline` | Medium distance (support/suppression) |
| `Backline` | Far from enemies (snipers, defensive) |

## Methods

### State Management

#### GetFactionState

```csharp
public static FactionTurnState GetFactionState(int factionIndex)
```

Get or create faction state. Thread-safe.

#### ResetFactionState

```csharp
public static void ResetFactionState(int factionIndex)
```

Reset faction state for a new turn.

#### InitializeTurnState

```csharp
public static void InitializeTurnState(int factionIndex)
```

Initialize state at turn start. Collects positions, computes centroids.

### Unit Classification

#### ClassifyUnit

```csharp
public static UnitRole ClassifyUnit(GameObj actor)
```

Classify unit role based on RoleData weights.

#### IsSuppressor / IsDamageDealer

```csharp
public static bool IsSuppressor(GameObj actor)
public static bool IsDamageDealer(GameObj actor)
```

Quick checks for unit type.

#### ClassifyFormationBand

```csharp
public static FormationBand ClassifyFormationBand(GameObj actor)
```

Determine which formation band a unit belongs to.

### Scoring

#### CalculateAgentScoreMultiplier

```csharp
public static float CalculateAgentScoreMultiplier(
    GameObj actor,
    FactionTurnState state,
    CoordinationConfig config)
```

Calculate score multiplier for agent selection. Applies sequencing and focus fire rules.

### Tile Modification

#### ApplyTileScoreModifiers

```csharp
public static void ApplyTileScoreModifiers(
    GameObj agent,
    FactionTurnState state,
    CoordinationConfig config)
```

Modify tile scores for Center of Forces and Formation Depth. Safe to call from `PostProcessTileScores` because each agent has its own tiles.

### Execution Tracking

#### RecordAgentExecution

```csharp
public static void RecordAgentExecution(GameObj agent, int factionIndex)
```

Record that an agent executed. Updates suppressor/damage dealer acted flags and targeted tiles.

### Helpers

#### GetFactionIndex

```csharp
public static int GetFactionIndex(GameObj aiFaction)
```

Get faction index from AIFaction object.

#### GetAgentFactionIndex

```csharp
public static int GetAgentFactionIndex(GameObj agent)
```

Get faction index from Agent object.

#### GetActorTilePosition

```csharp
public static (int x, int z) GetActorTilePosition(GameObj actor)
```

Get an actor's current tile position (X/Z coordinates).

## Console Commands

| Command | Usage | Description |
|---------|-------|-------------|
| `aicoord` | `aicoord [faction]` | Show coordination state |
| `aiclassify` | `aiclassify [actor]` | Classify actor role and band |

## Example: Custom Coordination Rule

Add a custom rule that boosts flanking units:

```csharp
static void GetScoreMult_Postfix(object __instance, ref float __result)
{
    var agent = new GameObj(((Il2CppObjectBase)__instance).Pointer);
    var actor = agent.ReadObj("m_Actor");
    int factionIndex = AICoordination.GetAgentFactionIndex(agent);
    var state = AICoordination.GetFactionState(factionIndex);

    // Standard coordination multiplier
    __result *= AICoordination.CalculateAgentScoreMultiplier(actor, state, _config);

    // Custom: Boost units on the flanks
    var pos = AICoordination.GetActorTilePosition(actor);
    float dx = pos.x - state.EnemyCentroid.x;
    float dz = pos.z - state.EnemyCentroid.z;

    // Check if unit is perpendicular to enemy centroid (flanking)
    float angle = MathF.Atan2(dz, dx);
    bool isFlanking = MathF.Abs(MathF.Sin(angle)) > 0.7f;

    if (isFlanking)
        __result *= 1.3f; // Boost flanking units
}
```

## Migration from Original CombinedArms

The SDK version replaces ~1500 lines of raw IL2CPP code with ~100 lines:

| Original | SDK |
|----------|-----|
| Manual IL2CPP offset resolution | `GameObj.ReadObj()`, `AI.GetRoleData()` |
| Manual dictionary iteration | `GameDict` wrapper |
| Crash-prone memory access | Safe SDK wrappers with error handling |
| Complex threading guards | Built-in safety checks |

See `CombinedArmsSDK.cs` for the complete drop-in replacement.

## See Also

- [AI](ai.md) - Core AI inspection and modification
- [TileMap](tile-map.md) - Tile operations
- [EntitySpawner](entity-spawner.md) - Entity queries
