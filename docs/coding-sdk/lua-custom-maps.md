# Lua Custom Maps API

This guide covers the Lua API for creating and controlling custom map generation in Menace.

## Overview

The Custom Maps system allows modders to:
- Control procedural map generation parameters (seed, size)
- Configure individual generators (chunks, props, lakes, etc.)
- Specify which prefabs appear on maps
- Create map presets that can be activated via console or script

## Quick Start

```lua
-- Create a simple custom map with a fixed seed
local my_map = maps.create("my_first_map")
    :name("My First Map")
    :seed(12345)
    :size(50)
    :build()

-- Register and activate it
maps.register(my_map)
maps.set_active("my_first_map")

-- Next mission will use this map configuration
```

## Maps API Reference

### Listing and Querying Maps

```lua
-- List all registered maps
local all_maps = maps.list()
for i, map in ipairs(all_maps) do
    print(map.id, map.name)
end

-- Get a specific map by ID
local map = maps.get("my_map_id")
if map then
    print("Found:", map.name)
end

-- Check how many maps are registered
print("Total maps:", maps.count())
```

### Activating Maps

```lua
-- Set a map as active (will be used for next mission)
maps.set_active("my_map_id")

-- Check if there's an active override
if maps.has_active() then
    local active = maps.get_active()
    print("Active map:", active.name)
end

-- Clear the active override (return to normal generation)
maps.clear_active()
```

### Quick Play Functions

```lua
-- Play with a specific seed (creates temporary map config)
maps.play_with_seed(42)

-- Play with a specific map size
maps.play_with_size(60)

-- Play with both seed and size
maps.play_with(42, 60)
```

### Loading Maps from Files

```lua
-- Load all .json map configs from a directory
local count = maps.load_directory("/path/to/custom_maps")
print("Loaded", count, "maps")
```

## Creating Maps with the Builder

The builder API provides a fluent interface for creating map configurations.

### Basic Properties

```lua
local map = maps.create("unique_id")
    :name("Display Name")
    :author("Your Name")
    :description("A custom map with specific settings")
    :version("1.0.0")
    :seed(12345)           -- Fixed seed for reproducible maps
    :size(42)              -- Map size (default is 42)
    :build()
```

### Difficulty Layers

Maps can be assigned to specific difficulty layers. They'll appear in the mission pool for those difficulties.

```lua
local map = maps.create("hard_map")
    :name("Brutal Arena")
    :layers({"Layer3", "Layer4", "Layer5"})  -- Hard/Extreme difficulties
    :weight(1.5)  -- Higher weight = more likely to be selected
    :build()
```

Valid layers: `"Layer1"` through `"Layer5"` (Easy to Extreme)

### Disabling Generators

You can completely disable specific map generators:

```lua
local sparse_map = maps.create("sparse")
    :name("Sparse Wasteland")
    :disable("PropGenerator")           -- No decorative props
    :disable("EnvironmentPropGenerator") -- No environment props
    :disable("LakeGenerator")           -- No lakes
    :build()
```

Available generators:
- `ChunkGenerator` - Buildings and structures
- `PropGenerator` - Decorative props (rocks, debris)
- `EnvironmentFeatureGenerator` - Grass/foliage details
- `EnvironmentPropGenerator` - Props around structures
- `LakeGenerator` - Water bodies
- `CoverGenerator` - Tactical cover objects
- `RoadGenerator` - Road networks
- `LampGenerator` - Street lighting
- `CableGenerator` - Power cables between structures

### Configuring Generator Properties

Each generator has properties you can override:

```lua
local map = maps.create("dense_city")
    :name("Dense Urban")
    -- Configure PropGenerator
    :generator("PropGenerator", {
        _count = 100,        -- More props
        _scaleMin = 0.5,
        _scaleMax = 1.2
    })
    -- Configure LampGenerator
    :generator("LampGenerator", {
        _minDistance = 3,    -- Lamps closer together
        _maxDistance = 6,
        _radius = 12         -- Brighter lights
    })
    :build()
```

See the Generator Properties section below for all available properties.

## Assets API

The assets API lets you browse and search available prefabs for use in custom maps.

### Browsing Assets

```lua
-- List all asset categories
local categories = assets.categories()
for i, cat in ipairs(categories) do
    print(cat)
end

-- List prefabs in a category
local buildings = assets.list("Buildings")
for i, prefab in ipairs(buildings) do
    print(prefab)
end

-- List all prefabs (no category filter)
local all_prefabs = assets.list()

-- Search prefabs by name pattern
local bunkers = assets.search("bunker")
for i, prefab in ipairs(bunkers) do
    print(prefab)
end

-- Count prefabs
print("Total prefabs:", assets.count())
print("Buildings:", assets.count("Buildings"))
```

### Using Custom Prefabs in Maps

```lua
-- Create a map with specific building prefabs
local map = maps.create("military_base")
    :name("Military Compound")
    :generator("ChunkGenerator", {
        -- Properties here
    }, {
        -- Prefab arrays
        chunkTemplates = {
            "Buildings/MilitaryBase",
            "Buildings/Bunker",
            "Buildings/Watchtower"
        }
    })
    :build()
```

## Generator Properties Reference

### PropGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_sizeMin` | int | 1-10 | 1 | Minimum prop size |
| `_sizeMax` | int | 1-10 | 1 | Maximum prop size |
| `_spacingMin` | int | 1-10 | 1 | Minimum tiles between props |
| `_spacingMax` | int | 1-10 | 2 | Maximum tiles between props |
| `_count` | int | 0-200 | 50 | Target number of props |
| `_scatterCount` | int | 1-50 | 12 | Scatter positions |
| `_margin` | int | 0-10 | 2 | Edge margin from border |
| `_scaleMin` | float | 0.1-1.0 | 0.66 | Minimum scale |
| `_scaleMax` | float | 0.5-2.0 | 1.0 | Maximum scale |

### EnvironmentPropGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_density` | float | 0.0-2.0 | 1.0 | Base spawn density |
| `_edgeDensityBonus` | float | 0.0-50.0 | 10.0 | Extra density at edges |
| `_maxPerTile` | int | 1-10 | 2 | Max props per tile |
| `_scaleMin` | float | 0.1-1.0 | 0.4 | Minimum scale |
| `_scaleMax` | float | 0.5-2.0 | 0.9 | Maximum scale |
| `_blockMovement` | bool | - | false | Block movement |

### LakeGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_spawnChance` | int | 0-100 | 100 | Spawn probability (%) |
| `_padding` | int | 0-10 | 0 | Edge padding |
| `_minSize` | float | 1.0-10.0 | 2.0 | Minimum radius |
| `_maxSize` | float | 2.0-20.0 | 4.0 | Maximum radius |

### RoadGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_surfaceType` | int | 0-10 | 3 | Road material type |
| `_decorationChance` | int | 0-100 | 50 | Decoration chance (%) |
| `_roadWidth` | int | 1-5 | 3 | Width in tiles |
| `_scale` | float | 0.5-2.0 | 1.0 | Decal scale |

### LampGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_minDistance` | int | 1-20 | 5 | Min tiles between lamps |
| `_maxDistance` | int | 5-30 | 10 | Max tiles between lamps |
| `_edgeDistance` | int | 0-5 | 1 | Distance from edges |
| `_maxPerChunk` | int | 1-100 | 99 | Max lamps per chunk |
| `_radius` | int | 1-20 | 8 | Light radius |

### CableGenerator
| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_spawnChance` | int | 0-100 | 55 | Cable spawn chance (%) |
| `_maxCablesPerStructure` | int | 0-10 | 3 | Max cables per building |

## JSON Map Format

Maps can also be defined as JSON files in your modpack's `custom_maps/` directory:

```json
{
    "id": "desert_outpost",
    "name": "Desert Outpost",
    "author": "Modder",
    "version": "1.0.0",
    "description": "A sparse desert map with military structures",
    "seed": 42,
    "mapSize": 50,
    "layers": ["Layer2", "Layer3"],
    "weight": 1.0,
    "disabledGenerators": ["LakeGenerator"],
    "generators": {
        "PropGenerator": {
            "enabled": true,
            "properties": {
                "_count": 30,
                "_scaleMax": 0.8
            }
        }
    }
}
```

## Console Commands

The following console commands are available for testing:

| Command | Usage | Description |
|---------|-------|-------------|
| `custommaps` | `custommaps` | List all registered maps |
| `setmap` | `setmap <id>` | Set active map override |
| `clearmap` | `clearmap` | Clear active override |
| `mapinfo` | `mapinfo <id>` | Show detailed map info |
| `loadmaps` | `loadmaps <path>` | Load maps from directory |
| `listassets` | `listassets [category]` | List available assets |
| `searchassets` | `searchassets <query>` | Search for assets |

## Examples

### Arena Map (No Cover)

```lua
local arena = maps.create("arena")
    :name("Open Arena")
    :description("No cover, nowhere to hide")
    :size(30)
    :disable("CoverGenerator")
    :disable("PropGenerator")
    :disable("EnvironmentPropGenerator")
    :build()

maps.register(arena)
```

### Dense Urban Combat

```lua
local urban = maps.create("urban_hell")
    :name("Urban Combat Zone")
    :size(60)
    :generator("ChunkGenerator", {
        _roads = true
    })
    :generator("LampGenerator", {
        _minDistance = 4,
        _maxDistance = 8
    })
    :generator("CableGenerator", {
        _spawnChance = 80
    })
    :build()

maps.register(urban)
```

### Reproducible Test Map

```lua
-- Same seed + size = identical map every time
local test_map = maps.create("test_seed_42")
    :name("Test Map (Seed 42)")
    :seed(42)
    :size(42)
    :build()

maps.register(test_map)
maps.set_active("test_seed_42")
```

## Tips

1. **Testing seeds**: Use `maps.play_with_seed(n)` to quickly test different seeds
2. **JSON for distribution**: Use JSON files for maps you want to share; Lua for dynamic generation
3. **Layer weights**: Higher weight values make maps more likely to appear in random selection
4. **Generator dependencies**: Some generators depend on others (e.g., LampGenerator needs ChunkGenerator)
5. **Performance**: Very large maps (size > 80) may cause performance issues
