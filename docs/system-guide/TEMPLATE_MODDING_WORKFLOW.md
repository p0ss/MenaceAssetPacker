# Template Modding Workflow

Guide for editing game templates (weapons, armor, entities, accessories) using the Menace Modkit.

## Overview

The template modding system allows you to modify game data without touching game files directly:

1. **Extract** — DataExtractor mod pulls template data from game memory
2. **Edit** — Stats Editor in the desktop app for visual editing
3. **Deploy** — One-click deployment compiles and installs your modpack
4. **Runtime** — ModpackLoader applies patches when the game starts

## Quick Start (GUI Workflow)

### 1. First-Time Setup

Launch the Menace Modkit app:

```bash
dotnet run --project src/Menace.Modkit.App
```

On first launch, the app will:
1. Detect your game installation
2. Install MelonLoader if needed
3. Deploy the DataExtractor mod
4. Prompt you to launch the game once to extract templates

After extraction completes, you'll have full access to the Stats Editor.

### 2. Create a Modpack

1. Go to **Modpacks** tab
2. Click **Create New Modpack**
3. Enter name, author, version
4. Your modpack is created in the staging directory

### 3. Edit Templates

1. Go to **Stats Editor** tab
2. Select a template type (WeaponTemplate, ArmorTemplate, etc.)
3. Select an instance from the list
4. Edit values in the right panel
5. Changes are shown in yellow (modified) vs vanilla values
6. Click **Save Changes** to write to your modpack

### 4. Deploy

1. Go to **Modpacks** tab
2. Toggle your modpack to "Deploy"
3. Click **Deploy All**
4. The app compiles your changes into a runtime-ready format

### 5. Play

Launch the game normally. ModpackLoader automatically:
- Discovers your modpack in the `Mods/` folder
- Applies template patches to game objects
- Logs results to `MelonLoader/Latest.log`

## Modpack Structure

```
MyModpack/
├── modpack.json          # Manifest
├── stats/                # Template patches (JSON)
│   ├── WeaponTemplate.json
│   └── EntityTemplate.json
├── assets/               # Asset replacements
└── src/                  # C# source files (optional)
```

### Manifest Format (V2)

```json
{
  "manifestVersion": 2,
  "name": "My Modpack",
  "version": "1.0.0",
  "author": "YourName",
  "loadOrder": 100,
  "patches": {
    "WeaponTemplate": {
      "Pistol_Basic": {
        "Damage": 50,
        "Accuracy": 95
      }
    },
    "ArmorTemplate": {
      "HeavyArmor": {
        "Armor": 150
      }
    }
  }
}
```

### Patch Structure

Patches use a nested structure: `templateType → instanceName → field → value`

```json
{
  "patches": {
    "WeaponTemplate": {
      "weapon_assault_rifle": {
        "Damage": 45.0,
        "AccuracyBonus": 10.0,
        "MinRange": 2,
        "MaxRange": 8
      }
    }
  }
}
```

## Supported Field Types

| Type | Example | Notes |
|------|---------|-------|
| `int` | `"Damage": 50` | Integer values |
| `float` | `"Accuracy": 95.5` | Decimal values |
| `bool` | `"IsAutomatic": true` | Boolean flags |
| `enum` | `"WeaponType": 2` | Written as integers |
| `string` | Limited support | Requires IL2CPP string handling |

## Template Types

Common template types you can modify:

| Type | Controls |
|------|----------|
| `WeaponTemplate` | Damage, accuracy, range, fire rate |
| `ArmorTemplate` | Defense values, durability, resistances |
| `EntityTemplate` | Unit stats, hitpoints, movement |
| `AccessoryTemplate` | Equipment bonuses, stat modifiers |
| `BoolPlayerSettingTemplate` | Game settings, dev options |

## Load Order and Conflicts

- Lower `loadOrder` values load first (default: 100)
- When multiple modpacks modify the same field, last one wins
- The app's **Conflict Detection** highlights overlapping changes before deployment

## Dev Console Verification

Press backtick (`~`) in-game to open the Dev Console:

```
# List all weapons
find WeaponTemplate

# Inspect a specific template
template WeaponTemplate weapon_assault_rifle

# View recent errors
errors
```

## Troubleshooting

### Changes not appearing in-game

1. Check `MelonLoader/Latest.log` for errors
2. Verify modpack is deployed (check `Mods/` folder)
3. Ensure template and field names match exactly (case-sensitive)
4. Some templates load in later scenes — restart the game fully

### JSON syntax errors

- Use a JSON validator (jsonlint.com)
- Check for trailing commas
- Ensure strings are quoted

### Finding template/field names

1. Use the Stats Editor to browse available templates
2. Check `UserData/ExtractedData/` for raw JSON exports
3. Use `templates <type>` command in Dev Console

---

## Advanced: Internal Architecture

### Current Approach

The modkit uses **dynamic reflection** for extracting template data at runtime. This approach:
- Automatically discovers fields without hardcoded offsets
- Handles game updates without regenerating extraction code
- Works with IL2CPP's runtime type information

For deeper reverse engineering (memory layouts, native code), the modkit integrates with **Ghidra** via MCP.

### Legacy IL2CPP Dump Tools

The `tools/il2cpp-dump-legacy/` directory contains Python scripts that parse IL2CPP dump files to generate extraction code. These are **preserved as a fallback** for situations where:
- Dynamic reflection fails for specific types
- Ghidra is unavailable
- You need to understand structure from a fresh IL2CPP dump

See `tools/il2cpp-dump-legacy/README.md` for usage instructions.

These scripts are primarily for modkit developers and are not needed for normal modding workflows.
