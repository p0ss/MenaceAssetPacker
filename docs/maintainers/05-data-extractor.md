# Menace.DataExtractor - Maintainer Documentation

## Overview

**Menace.DataExtractor** is a MelonLoader mod that extracts game template data from a running game instance and serializes it to JSON files. It enables modders to access game data via the modkit's Data tab.

**Location:** `src/Menace.DataExtractor/`

## Architecture

### Core Concept

The tool uses IL2CPP runtime reflection and direct memory reading to extract template objects from the running game. It operates in two modes:

1. **Full Extraction** - Clean extraction triggered on game update or first run
2. **Additive Extraction** (F11 hotkey) - Merges new templates with existing data

### Framework
- **Target:** .NET 6.0 MelonLoader mod
- **Approach:** Direct memory reads via Marshal (avoids property getter crashes)
- **Schema:** Embedded `schema.json` for field offset mappings
- **Logging:** MelonLogger + DevConsole integration

## Entry Points

**Location:** `DataExtractorMod.cs`

### OnInitializeMelon() (line 149)
- Sets up output paths, loads embedded schema
- Detects extraction need via game fingerprint
- Registers command handlers

### OnUpdate() (line 548)
- Polls for F11 key (additive manual extraction)
- Manages extraction dialog timing
- Registers console commands

### OnGUI() (line 587)
- Renders in-game extraction dialog
- Three-button UI: "Extract Now", "Remind Next Time", "Don't Ask Again"

### Public Static Methods
```csharp
public static void TriggerExtraction(bool force)  // DevConsole integration
public static string GetExtractionStatus()        // Status getter
```

### Console Commands
- `extract [force]` - Start full extraction
- `extractstatus` - Display extraction status

## Three-Phase Extraction Pipeline

### Phase 1: ExtractTypePhase1 (line 1897)

Extracts **primitive properties only**:
- int, float, bool, string, enum, Vector2/3/4
- Uses direct memory reads via IL2CPP field offsets
- Populates `InstanceContext.Data` with m_ID, name, and simple values

### Phase 2: FillReferenceProperties (line 2478)

Fills **reference/complex properties**:
- Localization strings (m_DefaultTranslation)
- Unity asset references (Sprites, Textures)
- Nested template objects
- IL2CPP List<T> and array types
- Polymorphic effect handlers

**GC Detection:** Skips objects garbage collected between phases.

### Phase 3: Name Fixing

Updates `inst.Name` and `data["m_ID"]` from Unity object names if extraction gave "unknown_*".

## Key Components

### PrepareExtraction (line 1634)
- Discovers all concrete template types in Assembly-CSharp
- Filters out abstract types
- Caches loader methods
- Performs readiness check on sample type

### LoadTypeObjects (line 1759)

Four strategies in order:
1. `DataTemplateLoader.GetAll<T>()` - Primary
2. `ConversationTemplate.LoadAllUncached()` - Special case
3. `Resources.LoadAll(basePath)` - Fallback
4. `Resources.FindObjectsOfTypeAll<T>()` - Final fallback

### EventHandlerParser

**Location:** `EventHandlerParser.cs` (313 lines)

Parses polymorphic SkillEventHandlerTemplate subclasses:
- `DetectHandlerType()` - Get IL2CPP class name
- `GetSchema()` - Look up schema by name
- `ParseHandler()` - Extract fields using schema

### Memory Safety Layer

Direct Read Methods (No property getters - avoids SIGSEGV):
- `TryReadFieldDirect` (line 2196)
- `ReadFloat`, `ReadDoubleValidated` (line 2219)
- All reads use cached IL2CPP class pointer

Stability Checks:
- `IsUnityObjectAliveWithClass` - Check m_CachedPtr != 0
- `WasCollected` property - Detect GC'd objects
- GC skip threshold: Max 1% objects can be GC'd

## Data Flow

### Extraction Pipeline

```
OnInitializeMelon()
  └─> LoadEmbeddedSchema()
  └─> ComputeGameFingerprint()
  └─> DetermineExtractionReason()
  └─> Show dialog or set auto-extraction flag

User triggers extraction
  └─> RunExtractionCoroutine()
      ├─> Wait for stable scene (3-120 seconds)
      ├─> PrepareExtraction()
      │
      ├─> PASS 1: Types with Resource paths
      │   └─> For each type:
      │       ├─> PHASE 1: ExtractTypePhase1()
      │       ├─> PHASE 2: FillReferenceProperties()
      │       ├─> PHASE 3: Fix unknown names
      │       └─> SaveSingleTemplateType()
      │
      ├─> PASS 2: Loose templates (no Resource path)
      │
      └─> Check GC% and save fingerprint
```

### Data Structures

```csharp
// InstanceContext (line 1470)
Dictionary<string, object> Data  // Field name -> value
object CastObj                   // TryCast<T> result
IntPtr Pointer                   // IL2CPP native pointer
string Name                      // Display name

// TypeContext (line 1479)
Type TemplateType
List<InstanceContext> Instances
```

### JSON Output Format

```json
// Per Template Type (e.g., WeaponTemplate.json)
[
  {
    "name": "WeaponID123",
    "m_ID": "WeaponID123",
    "m_InstanceID": 12345,
    "Damage": 45,
    "Weight": 5.2,
    "Icon": "sprite_weapon_001",
    "SkillEventHandlers": [
      {
        "_type": "DealDamageHandler",
        "damageMultiplier": 1.5
      }
    ]
  }
]
```

## Configuration & Fingerprinting

### Output Directory

Path: `{GameRoot}/Mods/UserData/ExtractedData/`

Files:
- `*.json` - Template data files
- `_extraction_fingerprint.txt` - Game state hash
- `_extraction_debug.log` - Detailed logs
- `_dont_ask_extraction.flag` - User preference
- `_force_extraction.flag` - Signal from modkit

### Game Fingerprint

Computed from (line 875):
- GameAssembly.dll file size + modification time
- OR globalgamemanagers (fallback)
- Extractor version

Format: `Extractor|{version}|GameAssembly|{size}|{timestamp}`

## TODOs and Known Issues

### Issues

1. **GC Stability** (lines 1412-1430)
   - Extraction can lose objects during scene transitions
   - Handling: Tracks GC-skipped instances, warns if >1%
   - User must extract from stable screens

2. **Float/Double Mismatch** (lines 2213-2219)
   - IL2CPP metadata sometimes reports float as double
   - Workaround: Detects by checking gap to next field offset

3. **ConversationTemplate Special Case** (lines 1785-1806)
   - Doesn't load via standard DataTemplateLoader.GetAll<T>
   - Falls back to ConversationTemplate.LoadAllUncached()

4. **Unity .name Property Crash** (line 3758)
   - Can SIGSEGV on some objects
   - Protected by try-catch, last-resort only

### Potential Inconsistencies

1. **Fingerprint Validation** (lines 911-933)
   - Only checks if at least 3 JSON files exist
   - No verification of file contents or schema version

2. **Schema Field Offset Validation** (line 2774)
   - Hard cap at 0x10000 bytes (64KB)
   - Assumes offsets > 0x10000 are corrupted

## Dependencies

### Build Dependencies
- MelonLoader.dll
- Il2CppInterop.Runtime.dll
- UnityEngine modules
- Assembly-CSharp.dll
- Newtonsoft.Json 13.0.3

### Internal Dependencies
- **schema.json** (embedded resource)
- **DataTemplateLoader** (game's loading utility)
- **DevConsole** (optional, from ModpackLoader)

## Known Limitations

1. **No recursive template extraction** - Standalone types referenced by name only
2. **Array element cap** - Arrays capped at 100-500 items
3. **Depth limit** - Nested objects limited to depth 8
4. **No dynamic schema** - Schema must be pre-computed
5. **Scene-dependent data** - Some templates only exist in specific scenes
6. **Single translation** - Only reads m_DefaultTranslation

## Maintainer Actions

- Monitor GC skip % if extraction behavior changes
- Update schema.json if new template types are added
- Test with each game update (fingerprint invalidation)
- Keep stable scene list current if new game states introduced
