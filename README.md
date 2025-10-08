# Menace Modkit

A modkit for extracting and modifying game data from MENACE Demo (Unity IL2CPP).

## Overview

This modkit provides two main capabilities:
1. **Data Extraction** - Extract ScriptableObject templates to JSON files
2. **Stat Modification** - Load modified JSON data back into the game at runtime

## Projects

### Menace.DataExtractor

Extracts game templates using direct IL2CPP memory reading.

**Supported Templates:**
- WeaponTemplate (damage, range, accuracy, penetration)
- ArmorTemplate (armor, durability, stat bonuses)
- AccessoryTemplate (same structure as armor)
- EntityTemplate (unit stats, elements, army cost, properties)

**Output:** `~/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData/*.json`

### Menace.StatModifier

Loads modified template data from JSON and applies runtime patches using Harmony.

**Modified Data Path:** `~/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ModifiedData/*.json`

## MVP Modding Workflow

### 1. Extract Data

Build and deploy the DataExtractor mod:
```bash
dotnet build src/Menace.DataExtractor
cp src/Menace.DataExtractor/bin/Debug/net6.0/Menace.DataExtractor.dll \
   ~/.steam/debian-installation/steamapps/common/Menace\ Demo/Mods/
```

Launch the game once to extract templates. Check:
```
~/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData/
```

### 2. Modify Data

Copy extracted JSON to ModifiedData directory:
```bash
mkdir -p ~/.steam/debian-installation/steamapps/common/Menace\ Demo/UserData/ModifiedData
cp ~/.steam/debian-installation/steamapps/common/Menace\ Demo/UserData/ExtractedData/WeaponTemplate.json \
   ~/.steam/debian-installation/steamapps/common/Menace\ Demo/UserData/ModifiedData/
```

Edit the JSON file to change stats. Example:
```json
[
  {
    "name": "mod_weapon.heavy.cannon_long",
    "MinRange": 1,
    "IdealRange": 7,
    "MaxRange": 11,
    "AccuracyBonus": 0.0,
    "AccuracyDropoff": -5.0,
    "Damage": 999.0,
    "DamageDropoff": -1.5,
    "ArmorPenetration": 500.0,
    "ArmorPenetrationDropoff": -2.0
  }
]
```

**Important:** Only include templates you want to modify. The mod will fall back to original values for anything not in ModifiedData.

### 3. Apply Modifications

Build and deploy the StatModifier mod:
```bash
dotnet build src/Menace.StatModifier
cp src/Menace.StatModifier/bin/Debug/net6.0/Menace.StatModifier.dll \
   ~/.steam/debian-installation/steamapps/common/Menace\ Demo/Mods/
```

Launch the game. The StatModifier will:
1. Load modified JSON files
2. Apply Harmony patches to property getters
3. Return modified values when game code reads template properties

Check the MelonLoader log for confirmation:
```bash
tail -f ~/.steam/debian-installation/steamapps/common/Menace\ Demo/MelonLoader/Latest.log
```

You should see:
```
Menace Stat Modifier v1.0.0
Loaded 1 modified templates:
  - 1 weapons
  - 0 armor
  - 0 accessories
  - 0 entities
✓ Applied Harmony patches
```

### 4. Test In-Game

Start a battle with units using modified weapons/armor to see your changes in effect.

## Technical Details

### Direct Memory Reading

The DataExtractor uses IL2CPP memory offsets from the dump file to read ScriptableObject fields directly:

```csharp
// Example: WeaponTemplate.Damage at offset 0x150
data["Damage"] = BitConverter.ToSingle(
    BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x150)), 0);
```

This approach avoids IL2CPP casting/construction which causes thread blocking under Proton/Wine.

### Harmony Patching

The StatModifier patches property getters with Prefix patches:

```csharp
[HarmonyPatch("WeaponTemplate", "get_Damage")]
[HarmonyPrefix]
public static bool GetDamage(UnityEngine.Object __instance, ref float __result)
{
    if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["Damage"] != null)
    {
        __result = data["Damage"].Value<float>();
        return false; // Skip original method
    }
    return true; // Run original method
}
```

## Building

Requirements:
- .NET 6.0 SDK
- MelonLoader installed in game directory
- IL2CPP assemblies from game

```bash
dotnet build
```

## Troubleshooting

**Game crashes on launch:**
- Check MelonLoader log for errors
- Ensure Harmony patches aren't conflicting with other mods
- Verify JSON syntax in ModifiedData files

**Stats not changing:**
- Verify template names match exactly (case-sensitive)
- Check that field names match extracted JSON
- Ensure StatModifier loaded after DataExtractor in MelonLoader

**Extraction returns empty data:**
- Check IL2CPP offsets match your game version
- Verify game is fully loaded before extraction attempts

## Future Enhancements

- Add support for more template types (perks, skills, effects, vehicles)
- Support for nested EntityProperties modifications
- Adding new templates (requires asset bundle creation)
- GUI tool for editing templates
