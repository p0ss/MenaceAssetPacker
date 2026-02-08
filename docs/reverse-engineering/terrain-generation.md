# Terrain Generation System

## Overview

Menace uses a two-layer terrain generation system:
1. **MapMagic** - Third-party Unity terrain tool for base terrain (heightmap, textures, details)
2. **Menace.Tactical.Mapgen** - Custom chunk-based system for tactical gameplay elements (buildings, cover, roads, props)

## Architecture

```
Map.GenerateMap() - Main entry point (coroutine with 12 states)
│
├── MapMagic Terrain Generation
│   ├── TerrainData (heightmap, splat maps)
│   ├── Detail layers (grass/vegetation density)
│   └── Surface types per tile
│
├── Chunk-Based Layout
│   ├── ChunkLayouter - Assigns chunk positions
│   ├── ChunkGenerator - Creates chunk blueprints
│   └── ChunkBlueprint - Stores entity placements
│
└── Post-Processing
    ├── RoadGenerator - Creates road network
    ├── CoverGenerator - Places half-cover objects
    ├── LampGenerator - Street lighting
    ├── EnvironmentFeatureGenerator - Terrain features
    └── EnvironmentPropGenerator - Decorative props
```

## Map.GenerateMap Coroutine States

The main map generation is a 12-state coroutine:

| State | Description |
|-------|-------------|
| 0 | Wait for MapMagic closest terrain tile |
| 1 | Wait for MapMagic to finish generating |
| 2 | Apply blocked tiles and surface types from MapMagic output |
| 3 | Run BaseMapGenerator.OnLayoutPass for each generator |
| 4 | Capture terrain detail layers |
| 5 | Run BaseMapGenerator.OnFirstPass for each generator |
| 6 | Run BaseMapGenerator.OnSecondPass for each generator |
| 7 | Update elevation, isolated tiles, clear vegetation under entities |
| 8 | Apply detail layers, init pathfinding, update cover |
| 9-10 | Wait for any additional generation, deform terrain around embedded objects |
| 11 | Finalize map, call completion callback |

## ChunkGenerator - Core Building Placer

ChunkGenerator is responsible for placing chunk templates (buildings, structures) onto the map.

### Key Methods

```c
// Create a blueprint for spawning a chunk at given position
ChunkBlueprint CreateBlueprint(ChunkTemplate template, PseudoRandom random);

// Layout pass - find where this chunk can fit
bool OnLayoutPass(Map map, PseudoRandom random);

// First pass - spawn the blueprint contents
void OnFirstPass(Map map);

// Second pass - additional processing
void OnSecondPass(Map map);
```

### ChunkGenerator Fields

```c
public class ChunkGenerator : BaseMapGenerator {
    // +0x10: Position on map
    // +0x18: offset
    // +0x20: RectInt area (x, y, width, height)
    // +0x30: PseudoRandom m_Random
    // +0x38: ChunkConfig m_Config
    // +0x40: ChunkBlueprint m_Blueprint
    // +0x48: MissionMapgenConfig m_MissionConfig
    // +0x50: (unused)
    // +0x58: LampGenerator m_LampGenerator
    // +0x60: (unused)
    // +0x64: Direction m_Rotation (0, 2, 4, 6 for N/E/S/W)
    // +0x68: int m_RecursionDepth
    // +0x6c: int m_IsPreview
    // +0x70: Stopwatch m_Stopwatch
}
```

### Generation Flow

```
OnLayoutPass()
├── Get ChunkTemplate from ChunkConfig
├── Determine random rotation (0, 90, 180, 270 degrees)
├── ChunkLayouter.TryGetArea() - Find valid position
└── Store area bounds

OnFirstPass()
├── SpawnBlueprint() - Instantiate entities
├── RoadGenerator.Build() - If not preview mode
├── LampGenerator.Reserve() + Build()
└── CoverGenerator.Reserve() + Build()

OnSecondPass()
└── Additional processing
```

## ChunkBlueprint - Placement Data

ChunkBlueprint stores all placement data for a chunk before spawning.

### ChunkBlueprint Fields

```c
public class ChunkBlueprint {
    int Width;                          // +0x10
    int Height;                         // +0x14
    Vector2Int LocalOffset;             // +0x18
    int Rotation;                       // +0x20
    ChunkTile[,] Tiles;                 // +0x28 - Per-tile data
    ChunkTileFlags[,] Flags;            // +0x30 - Per-tile flags
    // +0x38: (unused)
    List<EntityBlueprint> Entities;     // +0x40 - Entities to spawn
    List<FixedPrefabEntry> Prefabs;     // +0x48 - Fixed prefabs
    List<FixedTileEffect> TileEffects;  // +0x50 - Tile effects
    List<FixedSurfaceType> SurfaceTypes; // +0x58 - Surface overrides
    List<ChunkRect> SubChunks;          // +0x60 - Nested chunk rects
}
```

### ChunkTileFlags

```c
[Flags]
enum ChunkTileFlags {
    Occupied = 0x01,      // Tile has something on it
    Road = 0x02,          // Tile is part of road
    // Additional flags for cover, spawn restrictions, etc.
}
```

### Key Methods

```c
// Check if a rect can fit at position
bool CanFit(int x, int y, int width, int height);

// Check if tile is occupied
bool IsOccupied(int x, int y);

// Get flags for tile
ChunkTileFlags GetFlag(int x, int y);

// Find free space of given size
bool TryFindFreeSpace(int width, int height, out Vector2Int pos);

// Convert local to global tile position
Vector2Int ToGlobalTilePos(int localX, int localY);
```

## ChunkTemplate - Blueprint Definition

ChunkTemplate defines what can spawn in a chunk (buildings, entities, props).

### ChunkTemplate Fields

```c
public class ChunkTemplate : ScriptableObject {
    Vector2Int Size;                    // +0x58 - Chunk size in tiles
    GameObject FloorPrefab;             // +0x60 - Floor decoration
    ChunkSpawnMode SpawnMode;           // +0x68 - Block or Scatter
    List<ChunkEntry> Entries;           // +0x70 - Block entries
    List<RandomChunkEntry> RandomEntries; // +0x78 - Random scatter entries
    List<FixedPrefabEntry> FixedPrefabs; // +0x80 - Always-spawn prefabs
    int MaxRandomSpawns;                // +0x8c - Max random entries
    int MinRandomSpawns;                // +0x88 - Min random entries
    List<RoadConfig> RoadConnections;   // +0x90 - Road exit points
    bool AllowRotation;                 // +0x78 - Can rotate 90 degrees
}
```

### ChunkSpawnMode

```c
enum ChunkSpawnMode {
    Block = 0,    // Place entries at fixed positions
    Scatter = 1,  // Randomly scatter entries
}
```

## RoadGenerator - Street Network

RoadGenerator creates connected road networks across the map.

### Key Methods

```c
// Build roads on the map
void Build(Map map, PseudoRandom random);

// Register a blueprint's road connections
void RegisterBlueprint(ChunkBlueprint blueprint);

// Reserve road tiles
void ReserveRoads();

// Connect road junctions
void TryConnectJunctions();

// Extend roads to map border
void ReserveRoadsToMapBorder();
```

### Road Generation Flow

```
Build()
├── FindJunctions() - Identify road connection points
├── TryConnectJunctions() - Connect nearby junctions
├── ReserveRoadsToMapBorder() - Extend to edges
└── SpawnDecoration() - Add road decorations
```

## CoverGenerator - Tactical Cover

CoverGenerator places half-cover objects for tactical gameplay.

### Key Methods

```c
// Reserve cover positions from blueprint
void Reserve(ChunkBlueprint blueprint);

// Build cover objects
void Build(Map map);

// Create half-cover at position
void CreateHalfCover(Map map, Tile tile, Direction dir, GameObject prefab);
```

### CoverGenerator.ReservedSpace

```c
struct ReservedSpace {
    Vector2Int Position;     // +0x00
    // +0x08: Direction or flags
    // +0x10: (additional data)
    List<GameObject> Prefabs; // +0x58 - Possible cover prefabs
}
```

## EnvironmentFeatureGenerator

Generates terrain features like rocks, debris, craters.

### Key Methods

```c
// Generate all features for the map
void GenerateFeatures(Map map);

// Spawn a feature as prefab
void SpawnEnvironmentFeatureAsPrefab(Tile tile, EnvironmentFeatureTemplate template);

// Spawn details (grass/small props)
void AttemptToSpawnDetails(Tile tile);
```

## EnvironmentPropGenerator

Spawns decorative props and vegetation.

### Key Methods

```c
// Spawn environment props
void SpawnEnvironmentProps(Map map, PseudoRandom random);

// Spawn detail objects
void SpawnDetails(Tile tile);

// Check if prop can fit
bool CanFit(int x, int y, int width, int height);

// Reserve space for prop
void AttemptToReserve(int x, int y, int width, int height);
```

## ChunkLayouter - Position Assignment

ChunkLayouter determines where chunks can be placed on the map.

### Key Methods

```c
// Try to get an area for a chunk
bool TryGetArea(Vector2Int minPos, Vector2Int maxPos, int width, int height, out RectInt area);

// Check if chunk rect is valid
bool IsValidChunkRect(RectInt rect);
```

## BaseMapGenerator - Abstract Base

All map generators inherit from BaseMapGenerator.

### Virtual Methods

```c
public abstract class BaseMapGenerator {
    // Initialize the generator
    virtual void Init(MissionMapgenConfig config);

    // Clear any state
    virtual void Clear();

    // Set position offset
    virtual void SetOffset(Vector2Int offset);

    // Layout pass - determine positions
    virtual void OnLayoutPass(MissionContext context, Map map, ref MapgenProgress progress);

    // First pass - main generation
    virtual void OnFirstPass(MissionContext context, Map map, ref MapgenProgress progress);

    // Second pass - post-processing
    virtual void OnSecondPass(MissionContext context, Map map, ref MapgenProgress progress);
}
```

## MissionMapgenConfig

Configuration for map generation from mission definition.

```c
public class MissionMapgenConfig {
    List<BaseMapGenerator> Generators;  // +0x28 - All generators to run
    BiomeTemplate Biome;                // (via mission)
    int Seed;                           // +0x24 - Random seed
    // Additional mission-specific settings
}
```

## Integration with MapMagic

MapMagic handles base terrain generation:

```c
// MapMagic terrain output nodes
TileMapOutput - Blocked tile data
SurfaceTypeOutput - Surface type per tile

// Access in Menace
Map.m_BlockedTiles = TileMapOutput.Result;      // +0x28
Map.m_SurfaceTypes = SurfaceTypeOutput.Result;  // +0x30
```

### Terrain Data Flow

```
MapMagicObject.StartGenerate()
    ↓
TerrainTile generated with:
    - Heightmap
    - Splat maps (textures)
    - Detail layers (grass density)
    ↓
TileMapOutput.Result → Blocked tiles matrix
    ↓
Map.GenerateMap() reads matrices
    ↓
Apply to Tile.SetBlocked(), Tile.SurfaceType
```

## SpawnBlueprint - Entity Instantiation

SpawnBlueprint creates actual game entities from the blueprint.

### Entity Types Spawned

| Type | Class | Description |
|------|-------|-------------|
| Structure (1) | `Structure` | Buildings, walls |
| Other | `TransientActor` | Props, decorations |

### Spawn Process

```c
void SpawnBlueprint(ChunkBlueprint blueprint, Map map) {
    // Spawn entities
    foreach (EntityBlueprint entity in blueprint.Entities) {
        Vector2Int pos = entity.GetSpawnPos() + offset;
        Tile tile = map.GetTile(pos);

        if (entity.Template.ActorType == 1) {
            // Structure
            var structure = new Structure();
            structure.Rotation = ApplySpawnRotation(rotation, entity.Rotation);
            Entity.Create(structure, template, tile);
        } else {
            // TransientActor
            var actor = new TransientActor();
            Entity.Create(actor, template, tile);
        }
    }

    // Spawn fixed prefabs
    foreach (FixedPrefabEntry prefab in blueprint.Prefabs) {
        Tile tile = map.GetTile(prefab.Position + offset);
        Quaternion rot = Quaternion.Euler(0, rotation * 45, 0);
        GameObject.Instantiate(prefab.Prefab, tile.Position, rot);
    }

    // Apply tile effects
    foreach (FixedTileEffect effect in blueprint.TileEffects) {
        Tile tile = map.GetTile(effect.Position + offset);
        tile.AddEffect(effect.Effect.Create());
    }

    // Override surface types
    foreach (FixedSurfaceType surface in blueprint.SurfaceTypes) {
        // Apply surface type to tile range
    }
}
```

## Modding Terrain Generation

### Adding Custom Chunks

1. Create ChunkTemplate ScriptableObject
2. Define entries (buildings, props)
3. Add to mission's mapgen template list

### Hooking Generation

```csharp
[HarmonyPatch(typeof(ChunkGenerator), "OnFirstPass")]
class CustomChunkPatch {
    static void Postfix(ChunkGenerator __instance, Map map) {
        // Add custom processing after chunk spawns
        var blueprint = __instance.m_Blueprint;
        Logger.Msg($"Spawned chunk with {blueprint.Entities.Count} entities");
    }
}
```

### Modifying Road Generation

```csharp
[HarmonyPatch(typeof(RoadGenerator), "Build")]
class RoadPatch {
    static void Prefix(RoadGenerator __instance, PseudoRandom random) {
        // Modify road generation parameters
    }
}
```

### Custom Map Generators

Extend BaseMapGenerator and add to MissionMapgenConfig.Generators:

```csharp
public class CustomGenerator : BaseMapGenerator {
    public override void OnFirstPass(MissionContext ctx, Map map, ref MapgenProgress progress) {
        // Custom generation logic
    }
}
```

## Key Constants

```c
// Tile size
const float TILE_SIZE = 8.0f;

// Default detail resolution per tile
const int DETAILS_PER_TILE = 8;

// Road connection directions
enum Direction {
    North = 0,
    East = 2,
    South = 4,
    West = 6
}
```

## Seed-Based Generation

All generation uses seeded randomness for reproducibility:

```c
// From MissionMapgenConfig
int seed = config.Seed;

// Each generator gets derived seed
PseudoRandom random = new PseudoRandom(seed + generatorIndex);

// Chunk generation
random.Next(4);  // Random rotation (0-3)
random.NextWeighted(entries);  // Weighted random selection
```

## Performance Considerations

- Map generation is coroutine-based for incremental loading
- Each state yields, allowing UI updates
- Heavy operations split across multiple passes
- Stopwatch tracks per-generator timing
- MapMagic terrain generates asynchronously

