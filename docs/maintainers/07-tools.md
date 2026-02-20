# Tools Directory - Maintainer Documentation

## Overview

The `tools/` directory contains supporting tools for the Menace modkit, including Python scripts for schema generation, data extraction, and validation, plus diagnostic and utility scripts.

**Location:** `tools/`

## Tool Inventory

### Primary Python Tools

| Tool | Purpose | Size |
|------|---------|------|
| `generate_schema.py` | Parse IL2CPP dump → schema.json | 18.4 KB |
| `generate_sdk.py` | API analysis & C# SDK generation | 21.1 KB |
| `extract_conversations.py` | Extract dialogue from game binary | 17.0 KB |
| `diff_schemas.py` | Compare schemas between versions | 8.0 KB |
| `validate_extraction.py` | Validate extracted template data | 10.8 KB |
| `match_asset_references.py` | Match assets to AssetRipper exports | 5.1 KB |
| `update_generated.py` | Orchestrator for code regeneration | 1.7 KB |

### Shell/Batch Scripts

| Script | Purpose | Platforms |
|--------|---------|-----------|
| `doctor.sh` | System dependency diagnostics | Linux/Unix |
| `doctor.bat` | Windows launcher for doctor.ps1 | Windows |
| `doctor.ps1` | PowerShell diagnostics (detailed) | Windows |

### Other Tools

| Tool | Type | Purpose |
|------|------|---------|
| `data-browser.html` | Browser-based | Interactive data explorer |
| `TwitchServer/` | .NET 8 ASP.NET | Twitch chat integration |
| `il2cpp-dump-legacy/` | Deprecated | Legacy extraction scripts |

## Primary Tools Detail

### generate_schema.py

Parses IL2CPP dump.cs files and generates `schema.json`.

**Key Functions:**
- `compute_file_hash()` - SHA-256 for version tracking
- `parse_all_enums()` - Extract enum definitions
- `parse_all_templates()` - Find template classes
- `collect_all_fields()` - Recursively gather inherited fields
- `classify_field()` - Categorize fields (primitive, string, enum, unity_asset, etc.)

**Usage:**
```bash
python generate_schema.py [dump_path] [output_path]
python generate_schema.py il2cpp_dump/dump.cs generated/schema.json
```

**Output:**
```json
{
  "version": "1.0.0",
  "dump_hash": "sha256...",
  "enums": { },
  "structs": { },
  "embedded_classes": { },
  "templates": { },
  "inheritance": { }
}
```

### generate_sdk.py

Analyzes IL2CPP dump for API surface and optionally generates C# wrappers.

**Usage:**
```bash
python generate_sdk.py --analyze              # Print stats
python generate_sdk.py --manifest             # Generate JSON manifest
python generate_sdk.py --generate             # Generate C# SDK
python generate_sdk.py --namespace Menace.Tactical.AI  # Filter
```

**Output:**
- `generated/sdk/api_manifest.json`
- `generated/sdk/GeneratedSDK.cs`

### extract_conversations.py

Extracts dialogue from game binary using `strings` command.

**Extraction Types:**
1. **Localization entries** - Story, conversations, tactical barks
2. **Tactical bark nodes** - Pipe-delimited JSON dialogue

**Usage:**
```bash
python extract_conversations.py <game_data_dir> [output_dir]
```

**Output:**
- `story_events.json`
- `tactical_barks_grouped.json`
- `all_dialogue_flat.json`

### diff_schemas.py

Compares two schema.json files for breaking changes.

**Severity Levels:**
- `CRIT` - Offset/size changes requiring regeneration
- `WARN` - Type/enum changes
- `ADD`/`DEL`/`CHG` - Structural changes

**Usage:**
```bash
python diff_schemas.py old_schema.json new_schema.json
```

**Exit Codes:**
- 0 = No changes
- 1 = Warnings only
- 2 = Critical changes

### validate_extraction.py

Validates quality of extracted template data.

**Checks:**
- Template coverage (non-abstract templates extracted?)
- Instance naming (proper names vs `unknown_N`)
- Field coverage and type validation
- Garbage float detection (> 1e10)

**Usage:**
```bash
python validate_extraction.py --schema generated/schema.json --data ~/.steam/.../ExtractedData
```

### update_generated.py

Orchestrator that regenerates all code after schema changes.

**Pipeline:**
1. Calls `il2cpp-dump-legacy/generate_injection_code.py --from-schema`
2. Calls `generate_sdk.py`
3. Outputs reminder to rebuild ModpackLoader

**Usage:**
```bash
python tools/update_generated.py
```

## Diagnostic Scripts

### doctor.sh / doctor.ps1

Multi-platform diagnostic checking prerequisites.

**Checks:**
- .NET SDK version (6.x, 8.x, 10.x)
- Game installation path
- MelonLoader installation
- Il2CppAssemblies generation (>50 assemblies)
- Mods folder creation
- (Linux) Proton/Wine setup
- (Windows) Visual C++ Redistributable

**Usage:**
```bash
./tools/doctor.sh                # Linux/Mac
powershell -ExecutionPolicy Bypass -File tools/doctor.ps1  # Windows
```

## data-browser.html

Standalone HTML5 application for exploring extracted game data.

**Features:**
- Loads ExtractedData folder
- Lists all template types (sorted by importance)
- Filter/search across instances
- Export to ModifiedData format

**Important Templates Highlighted:**
- WeaponTemplate, ArmorTemplate, AccessoryTemplate
- EntityTemplate, VehicleItemTemplate
- PerkTemplate, SkillTemplate

**Usage:**
1. Open `data-browser.html` in browser
2. Select UserData/ExtractedData folder
3. Browse and export

## TwitchServer/

.NET 8 ASP.NET web service for Twitch chat integration.

**Components:**
- `Program.cs` - Web app (port 7654)
- `TwitchIrcClient.cs` - IRC protocol handler
- `DraftPool.cs` - Viewer draft pool
- `MessageStore.cs` - Message history

**REST API:**
```
GET  /api/status              # Connected status
GET  /api/draft               # List viewers in pool
POST /api/draft/pick          # Pick random viewer
GET  /api/messages/{username} # Get messages
POST /api/connect             # Connect to channel
POST /api/disconnect          # Disconnect
```

**Purpose:** Enables modpacks to read Twitch chat for game decisions.

## il2cpp-dump-legacy/

**Status:** DEPRECATED - Kept for fallback only

Scripts:
- `generate_all_templates.py` - Old code gen
- `generate_injection_code.py` - Marshal.WriteXXX generation
- `generate_offset_code.py` - Field offset constants
- `extract_template_dump.py` - Minimal dump extraction
- `build_template_hierarchy.py` - Inheritance analysis

## Usage Workflows

### Data Extraction Workflow
```
1. Game Update → Extract dump.cs with Il2CppDumper
   └─> python generate_schema.py dump.cs schema.json

2. Validate schema
   └─> python diff_schemas.py old.json new.json

3. Run DataExtractor mod in game

4. Validate extracted data
   └─> python validate_extraction.py --schema schema.json

5. Generate code
   └─> python update_generated.py
   └─> Rebuild ModpackLoader
```

### Pre-Deployment Diagnostics
```bash
./tools/doctor.sh  # Check prerequisites
```

### Schema Migration
```bash
python tools/diff_schemas.py schema_v25.json schema_v26.json
# If CRIT changes: update offsets, regenerate, rebuild
```

## Dependencies

### Python 3 (Pure Standard Library)
- argparse, json, subprocess, re, pathlib, hashlib, collections, dataclasses

### External Tools
- `strings` command (for extract_conversations.py)

### .NET
- .NET 8.0+ SDK (for TwitchServer)

## TODOs and Issues

### TODOs Found

| File | Line | Issue |
|------|------|-------|
| `generate_sdk.py` | 503 | Check m_CachedPtr offset |
| `generate_offset_code.py` | 67, 85, 99 | Complex type handling |

### Potential Issues

1. **Binary String Extraction** (extract_conversations.py)
   - May capture truncated/garbage strings
   - Mitigation: Deduplication + truncation detection

2. **Float Garbage Detection** (validate_extraction.py:181)
   - Threshold 1e10 may be too strict

3. **TwitchServer Security**
   - OAuth tokens stored in plaintext
   - No rate limiting on IRC parsing

4. **data-browser.html Limitations**
   - Client-side only, loads all data to browser memory
   - Large datasets could cause slowdown

### Inconsistencies

1. **Path Assumptions** - Hardcoded Linux/Windows paths
2. **Schema Versioning** - Static "1.0.0" version string

## Relationships to C# Projects

| Tool | Consumes From | Produces For |
|------|---------------|--------------|
| `generate_schema.py` | IL2CPP dump.cs | All other tools |
| `generate_sdk.py` | IL2CPP dump.cs | Menace.Modkit.Core |
| `validate_extraction.py` | schema.json + ExtractedData | QA/CI |
| `update_generated.py` | schema.json | Menace.ModpackLoader |
| `TwitchServer` | Twitch IRC | Modpack event systems |

## Metrics

- **Total Python Code:** ~82 KB
- **Active Tools:** 7 main + 3 diagnostic + 1 HTML UI
- **Lines of Code (Python):** ~2,500
- **External Dependencies:** 0 (pure Python standard library)
