# Template Modding Workflow

Complete guide for extracting, modifying, and injecting game template data.

## Overview

The template modding system consists of three parts:
1. **Extraction** - Read template data from game memory
2. **Modification** - Edit values in modpacks
3. **Injection** - Write modified values back into game memory

## Tools

### 1. `generate_all_templates.py`
Generates extraction code for all template types, including inherited fields.

```bash
python3 generate_all_templates.py
```

**Output:** `generated_extraction_code.cs` with `ExtractTemplateDataDirect()` method

**Features:**
- Recursively collects fields from base classes
- Handles inheritance chains (e.g., AccessoryTemplate → ItemTemplate → BaseItemTemplate)
- All 72 template types now include inherited fields
- 861 total fields extracted

### 2. `generate_injection_code.py`
Generates injection code to write modified values back into memory.

```bash
python3 generate_injection_code.py
```

**Output:** `generated_injection_code.cs` with `ApplyTemplateModifications()` method

**Features:**
- Mirrors extraction code with `Marshal.WriteXXX()` calls
- Handles type conversions automatically
- Only writes fields present in modifications dictionary
- Logs each modification applied

## Complete Workflow

### Step 1: Generate Code

```bash
# Generate extraction code (includes inherited fields)
python3 generate_all_templates.py

# Generate injection code
python3 generate_injection_code.py
```

### Step 2: Update DataExtractor Mod

Copy the `ExtractTemplateDataDirect()` method from `generated_extraction_code.cs` into:
```
src/Menace.DataExtractor/DataExtractorMod.cs
```

This extracts all template data to JSON files.

### Step 3: Create Mod Loader

Create a new MelonLoader mod that:
1. Loads modpack JSON files from `Mods/` directory
2. Finds template objects in game
3. Calls `ApplyTemplateModifications()` to inject changes

Copy the `ApplyTemplateModifications()` method from `generated_injection_code.cs`.

Example mod loader structure:

```csharp
public class ModpackLoaderMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Loading modpacks...");

        // Load all modpack JSON files
        var modpacks = LoadModpacksFromDirectory("Mods");

        // Wait for game to load templates
        HookIntoGameTemplateLoad();
    }

    private void ApplyModpack(string modpackName, Dictionary<string, object> templateMods)
    {
        foreach (var (templateName, modifications) in templateMods)
        {
            var template = FindTemplate(templateName);
            if (template != null)
            {
                ApplyTemplateModifications(template, template.GetType(), modifications);
            }
        }
    }

    // ... generated ApplyTemplateModifications() method here ...
}
```

### Step 4: Create Modpack

Modpacks are JSON files that specify template modifications:

```json
{
  "name": "Dev Mode Enabler",
  "version": "1.0.0",
  "author": "YourName",
  "templates": {
    "BoolPlayerSettingTemplate": {
      "DevAutoCameraFocus": {
        "DefaultValue": true
      },
      "DevAutoPauseAfterPlayerTurn": {
        "DefaultValue": true
      }
    },
    "WeaponTemplate": {
      "Pistol_Basic": {
        "Damage": 50,
        "Accuracy": 95
      }
    }
  }
}
```

### Step 5: Test

1. Build DataExtractor with updated extraction code
2. Run game - extracts templates to JSON
3. Create modpack JSON with modifications
4. Build ModpackLoader with injection code
5. Run game - modifications applied!

## Inheritance Handling

### Problem
Many templates inherit fields from base classes:
- `AccessoryTemplate` → `ItemTemplate` → `BaseItemTemplate`
- `BoolPlayerSettingTemplate` → `BasePlayerSettingTemplate`

Previously, extraction only got fields directly on the template class.

### Solution
`generate_all_templates.py` now recursively collects all fields:

```python
def collect_all_fields(content, class_name, visited=None):
    # Parse current class
    class_info = parse_class_from_dump(content, class_name)

    # Collect base class fields first
    if class_info['base']:
        base_fields = collect_all_fields(content, class_info['base'], visited)
        all_fields.extend(base_fields)

    # Then add current class fields
    all_fields.extend(class_info['fields'])

    return all_fields
```

**Result:**
- AccessoryTemplate now has 14+ fields (was 0)
- BoolPlayerSettingTemplate now has 3 fields (was 2)
- All templates properly expose inherited properties

## Field Types

### Supported Types
- `int`, `float`, `double` - Numeric values
- `bool` - Boolean flags
- `byte`, `short`, `long` - Other integer types
- Enums (read/written as int32)

### Complex Types (TODO)
- `string` - Requires IL2CPP string handling
- `List<T>` - Requires array pointer dereferencing
- `Sprite`, `GameObject` - Unity asset references
- Embedded structs - Requires recursive reading

## Dev Mode Example

To enable developer settings:

**1. Find the settings in extracted data:**
```json
// BoolPlayerSettingTemplate_DevAutoCameraFocus.json
{
  "name": "DevAutoCameraFocus",
  "Type": 5,
  "DefaultValue": false
}
```

**2. Create modpack to enable it:**
```json
{
  "name": "Dev Settings",
  "templates": {
    "BoolPlayerSettingTemplate": {
      "DevAutoCameraFocus": {
        "DefaultValue": true
      },
      "DevAutoPauseAfterPlayerTurn": {
        "DefaultValue": true
      }
    }
  }
}
```

**3. Modpack loader applies:**
```csharp
// Finds template object and writes:
Marshal.WriteByte(ptr + 0x8C, 1); // true
```

## File Locations

```
MenaceAssetPacker/
├── il2cpp_dump/
│   └── dump.cs                          # IL2CPP structure dump
├── generate_all_templates.py            # Generates extraction code
├── generate_injection_code.py           # Generates injection code
├── generated_extraction_code.cs         # Output: extraction method
├── generated_injection_code.cs          # Output: injection method
└── src/
    └── Menace.DataExtractor/
        └── DataExtractorMod.cs          # Add extraction method here

Menace Demo/
├── Mods/
│   ├── Menace.DataExtractor.dll        # Extraction mod
│   ├── Menace.ModpackLoader.dll        # Injection mod (to be created)
│   └── MyModpack/
│       └── modpack.json                # Template modifications
└── UserData/
    └── ExtractedData/
        └── WeaponTemplate_*.json       # Extracted template data
```

## Performance

- **Extraction:** ~10 seconds (72 templates × ~12 instances each)
- **Injection:** < 1 second (only modified templates)
- **Memory:** Direct pointer writes, no Unity API overhead

## Limitations

1. **Unity Asset References** - Cannot easily redirect Sprite/GameObject references
2. **String Fields** - Need IL2CPP string creation (complex)
3. **Collections** - Arrays/Lists require pointer dereferencing
4. **Validation** - No built-in validation of modified values

## Asset Injection

Asset replacement requires hooking Unity's asset loading system to swap out textures, sprites, audio, etc.

### Asset Loading Hook

```csharp
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string))]
public class ResourceLoadPatch
{
    static void Postfix(string path, ref UnityEngine.Object __result)
    {
        // Check if we have a replacement asset for this path
        var replacementPath = GetReplacementAssetPath(path);
        if (replacementPath != null)
        {
            __result = LoadReplacementAsset(replacementPath, __result.GetType());
        }
    }
}
```

### Modpack Asset Structure

```
Mods/
└── MyModpack/
    ├── modpack.json                 # Template modifications
    └── Assets/
        ├── Sprite/
        │   └── weapon_pistol.png    # Replacement sprite
        ├── Texture2D/
        │   └── ui_background.png
        └── AudioClip/
            └── gunshot.wav
```

### Asset Manifest

Add asset replacements to modpack.json:

```json
{
  "name": "My Modpack",
  "assets": {
    "Sprite/weapon_pistol": "Assets/Sprite/weapon_pistol.png",
    "Texture2D/ui_background": "Assets/Texture2D/ui_background.png",
    "AudioClip/gunshot": "Assets/AudioClip/gunshot.wav"
  },
  "templates": {
    // ... template modifications ...
  }
}
```

### Asset Loader Implementation

```csharp
private Dictionary<string, string> _assetReplacements = new();

private void LoadModpackAssets(string modpackPath, Dictionary<string, string> assetManifest)
{
    foreach (var (assetPath, replacementFile) in assetManifest)
    {
        var fullPath = Path.Combine(modpackPath, replacementFile);
        if (File.Exists(fullPath))
        {
            _assetReplacements[assetPath] = fullPath;
            LoggerInstance.Msg($"Registered asset replacement: {assetPath}");
        }
    }
}

private UnityEngine.Object LoadReplacementAsset(string filePath, Type assetType)
{
    if (assetType == typeof(Sprite) || assetType == typeof(Texture2D))
    {
        return LoadTexture(filePath);
    }
    else if (assetType == typeof(AudioClip))
    {
        return LoadAudioClip(filePath);
    }
    // ... other asset types ...

    return null;
}

private Texture2D LoadTexture(string filePath)
{
    var bytes = File.ReadAllBytes(filePath);
    var texture = new Texture2D(2, 2);
    ImageConversion.LoadImage(texture, bytes);
    return texture;
}
```

### Template Field Asset References

Some template fields reference Unity assets by pointer. To fully replace assets:

1. **Extract asset GUIDs** from templates
2. **Load replacement assets** from modpack
3. **Update template pointers** to new assets

Example with WeaponTemplate.Icon:

```csharp
// In ApplyTemplateModifications, for Sprite fields:
if (modifications.ContainsKey("Icon") && modifications["Icon"] is Sprite newSprite)
{
    // Get IL2CPP pointer to the Sprite object
    if (newSprite is Il2CppObjectBase spriteObj)
    {
        IntPtr spritePtr = spriteObj.Pointer;
        Marshal.WriteIntPtr(ptr + 0x90, spritePtr);
        LoggerInstance.Msg($"Replaced Icon sprite pointer");
    }
}
```

### AssetBundle Support

For more complex mods, use AssetBundles:

```csharp
private Dictionary<string, AssetBundle> _loadedBundles = new();

private void LoadModpackBundles(string modpackPath)
{
    var bundlePath = Path.Combine(modpackPath, "assets.bundle");
    if (File.Exists(bundlePath))
    {
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        _loadedBundles[modpackPath] = bundle;
    }
}

[HarmonyPatch(typeof(AssetBundle), nameof(AssetBundle.LoadFromFile))]
public class AssetBundlePatch
{
    static void Postfix(string path, ref AssetBundle __result)
    {
        // Intercept and add modded assets to the bundle
    }
}
```

## Complete Modpack Structure

```
MyModpack/
├── modpack.json                # Manifest
├── Assets/                     # Loose asset files
│   ├── Sprites/
│   ├── Textures/
│   ├── Audio/
│   └── Models/
├── assets.bundle              # Optional: Unity AssetBundle
└── README.md                  # Mod description
```

## Asset Injection Workflow

1. **User creates modpack** in modkit UI
2. **User replaces assets** via asset browser
3. **Assets copied** to modpack Assets/ folder
4. **Manifest updated** with asset replacement entries
5. **ModpackLoader loads** modpack at game startup
6. **Asset hooks intercept** Unity's asset loading
7. **Replacement assets returned** instead of originals

## Future Enhancements

1. **Hot Reload** - Apply asset/template changes without game restart
2. **String Modification** - Implement IL2CPP string creation for text mods
3. **Collection Support** - Handle List<T> and arrays in templates
4. **Validation System** - Check modified values against game constraints
5. **Asset Preview** - Preview replaced assets in modkit before deployment
6. **Dependency Resolution** - Handle mods that depend on other mods
