# Menace Modkit Architecture

## Overview

The Menace Modkit is a modding toolchain for the game "Menace" that allows users to extract, view, and modify game data without directly editing game files. The architecture consists of several interconnected components that work together to provide a complete modding workflow.

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Interface Layer                      │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Menace.Modkit.App (Avalonia Desktop App - .NET 10)       │ │
│  │  ├─ Views: Stats Editor, Asset Manager, Settings          │ │
│  │  ├─ ViewModels: ReactiveUI + MVVM Pattern                 │ │
│  │  └─ Services: AssetRipperService, ModpackManager           │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Core Library Layer                          │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Menace.Modkit.Core (.NET 10)                             │ │
│  │  ├─ Models: DataTemplate, DynamicDataTemplate             │ │
│  │  ├─ Services: DataTemplateLoader                          │ │
│  │  └─ Models: ModpackManager, AppSettings                   │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Game Integration Layer                       │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │  MelonLoader         │  │  AssetRipper                     │ │
│  │  (IL2CPP Mod Loader) │  │  (Unity Asset Extractor)         │ │
│  │                      │  │                                  │ │
│  │  ├─ IL2CPP Interop   │  │  ├─ HTTP Server (Port 5734)     │ │
│  │  ├─ Unity 6 Support  │  │  ├─ /LoadFolder endpoint        │ │
│  │  └─ Mod Management   │  │  └─ /Export/PrimaryContent      │ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
│             │                            │                       │
│             ▼                            │                       │
│  ┌──────────────────────┐               │                       │
│  │  DataExtractor Mod   │               │                       │
│  │  (.NET 6 MelonMod)   │               │                       │
│  │                      │               │                       │
│  │  ├─ Template         │               │                       │
│  │  │   Extraction       │               │                       │
│  │  ├─ JSON             │               │                       │
│  │  │   Serialization   │               │                       │
│  │  └─ Output to        │               │                       │
│  │      UserData/       │               │                       │
│  │      ExtractedData/  │               │                       │
│  └──────────────────────┘               │                       │
└───────────────────────────────────────┼─────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Game Layer                               │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Menace.exe (Unity 6000.0.56f1 + IL2CPP)                  │ │
│  │                                                             │ │
│  │  ├─ GameAssembly.dll (IL2CPP Compiled C# → C++)           │ │
│  │  ├─ Assembly-CSharp (Game Logic)                          │ │
│  │  ├─ ScriptableObjects (Templates, Configs)                │ │
│  │  └─ Menace_Data/ (Unity Assets, Prefabs, Textures)        │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. Menace.Modkit.App (Desktop Application)

**Technology:** Avalonia 11.3.7, .NET 10, ReactiveUI
**Purpose:** Main user interface for the modding toolkit

#### Key Components:

- **Views (UI Layer)**
  - `StatsEditorView.axaml.cs`: Stats editor with vanilla/modified side-by-side comparison
  - `MainWindow.axaml.cs`: Main application window and navigation

- **ViewModels (MVVM Pattern)**
  - `StatsEditorViewModel.cs`: Manages template data, tree navigation, property editing
  - Uses ReactiveUI for property change notifications

- **Services**
  - `AssetRipperService.cs`: HTTP client for AssetRipper integration
  - `ModpackManager.cs`: Manages vanilla data location and modpack staging
  - `ModLoaderInstaller.cs`: Automates MelonLoader and DataExtractor installation

#### Data Flow:
```
User Action → ViewModel Command → Service Call → Update Model → Notify View
```

#### Auto-Deployment:
The app includes bundled dependencies that are automatically copied to the output directory:
```xml
<ItemGroup>
  <None Include="..\\..\\third_party\\bundled\\**\\*.*">
    <Link>third_party\\bundled\\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**Bundled Components:**
- `third_party/bundled/AssetRipper/AssetRipper.GUI.Free` - Unity asset extraction tool
- `third_party/bundled/DataExtractor/Menace.DataExtractor.dll` - Game data extraction mod
- `third_party/bundled/MelonLoader/` - IL2CPP mod loader (if bundled)

---

### 2. Menace.Modkit.Core (Core Library)

**Technology:** .NET 10
**Purpose:** Shared business logic and data models

#### Key Classes:

**Models:**
- `DataTemplate`: Base class for all game templates
- `DynamicDataTemplate`: Handles arbitrary JSON-based templates using System.Text.Json
- `TreeNodeViewModel`: Hierarchical navigation for template browser

**Services:**
- `DataTemplateLoader`: Loads JSON templates from extracted game data
  - Scans `~/.steam/.../Menace Demo/UserData/ExtractedData/*.json`
  - Deserializes to `DynamicDataTemplate` instances
  - Builds hierarchical tree based on template naming (e.g., `weapon.assault_rifle.tier1`)

**Data Storage:**
```
~/.steam/debian-installation/steamapps/common/Menace Demo/
├── UserData/
│   └── ExtractedData/           ← Vanilla game data (JSON)
│       ├── WeaponTemplate.json
│       ├── EntityTemplate.json
│       ├── ArmorTemplate.json
│       └── ... (70+ template types)
├── Mods/                        ← MelonLoader mods
│   └── Menace.DataExtractor.dll
└── MelonLoader/                 ← Mod loader framework
```

---

### 3. MelonLoader (IL2CPP Mod Loader)

**Version:** 0.7.2-ci.2388
**Purpose:** Enables C# mod development for IL2CPP Unity games

#### How It Works:

1. **Game Launch Interception**
   - MelonLoader injects into the game process at startup
   - Hooks IL2CPP runtime before game initialization

2. **IL2CPP Assembly Generation**
   - Uses **Cpp2IL** to decompile IL2CPP binaries back to .NET assemblies
   - Generates interop wrappers using **Il2CppInterop**
   - Creates managed proxies for IL2CPP types

3. **Mod Loading**
   - Scans `Mods/` directory for DLL files
   - Loads mods that inherit from `MelonMod` base class
   - Calls mod lifecycle hooks: `OnInitializeMelon()`, `OnApplicationQuit()`

#### IL2CPP Interop Layer:
```csharp
// IL2CPP (C++) ←→ Il2CppInterop ←→ C# Mod Code
UnityEngine.Object (IL2CPP)
    ↓ Il2CppInterop generates wrapper
UnityEngine.Object (Managed Wrapper)
    ↓ Your mod can use this
Resources.FindObjectsOfTypeAll<WeaponTemplate>()
```

**Key Directories:**
```
MelonLoader/
├── Dependencies/
│   ├── Il2CppAssemblyGenerator/
│   │   ├── Cpp2IL/              ← Decompiler
│   │   └── cpp2il_out/          ← Generated DLLs
│   ├── Il2CppInterop/           ← Interop wrappers
│   └── SupportModules/
└── Latest.log                   ← Mod loader log file
```

---

### 4. Menace.DataExtractor (MelonLoader Mod)

**Technology:** .NET 6, MelonLoader, Newtonsoft.Json
**Purpose:** Extracts game template data at runtime

#### How It Works:

```csharp
[assembly: MelonInfo(typeof(DataExtractorMod), "Menace Data Extractor", "6.0.2", "MenaceModkit")]
[assembly: MelonGame(null, null)]  // Compatible with any game
```

**Execution Flow:**

1. **Initialization** (`OnInitializeMelon()`)
   - Set output path: `{GameDir}/UserData/ExtractedData/`
   - Wait for game to fully load (5 seconds delay)

2. **Template Discovery**
   ```csharp
   var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
       .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

   var templateTypes = gameAssembly.GetTypes()
       .Where(t => t.Name.EndsWith("Template") && !t.IsAbstract)
       .ToList();
   ```

3. **Object Extraction**
   ```csharp
   var il2cppType = Il2CppType.From(templateType);
   var objects = Resources.FindObjectsOfTypeAll(il2cppType);
   ```

4. **Data Serialization**
   - Uses schema-driven extraction with embedded `schema.json`
   - Reads field offsets and types from schema for IL2CPP-safe extraction
   - Includes extraction fingerprint checks to skip unnecessary re-runs

5. **JSON Output**
   ```csharp
   var json = JsonConvert.SerializeObject(templates, Formatting.Indented,
       new JsonSerializerSettings {
           ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
           MaxDepth = 10
       });
   File.WriteAllText($"{_outputPath}/{templateType.Name}.json", json);
   ```

**Auto-Deployment:**
The modkit installer automatically:
1. Checks if MelonLoader is installed (`version.dll` presence)
2. Downloads and installs MelonLoader if missing
3. Copies `Menace.DataExtractor.dll` to `Mods/` folder
4. Provides "Launch Game to Update Data" button for re-extraction after game updates

---

### 5. IL2CPP Dump & Memory Offsets

**Problem:** IL2CPP compiles C# to C++, making direct memory access necessary for complex data extraction.

#### What is IL2CPP?

Unity's **IL2CPP** (Intermediate Language to C++) is a compilation technology that:
- Converts .NET IL bytecode to C++ source code
- Compiles C++ to native machine code
- Improves performance and code obfuscation
- Makes traditional .NET reflection limited

#### Memory Layout:

```
IL2CPP Object in Memory:
+0x00: Object header (Il2CppObject*) - 0x10 bytes
+0x10: Unity Object base fields (m_CachedPtr, hideFlags, etc.)
+0x78: First game-specific field
+0x13C: WeaponTemplate.MinRange (int32)
+0x140: WeaponTemplate.IdealRange (int32)
+0x144: WeaponTemplate.MaxRange (int32)
...
```

**IMPORTANT:** Offsets in dump.cs include the 0x10 byte object header. When reading embedded value types (structs stored inline, not as pointers), subtract 0x10 from all field offsets since the embedded struct has no object header.

#### When to Update IL2CPP Dump:

**Triggers requiring new dump:**
- Game updates to a different Unity version
- DataExtractor showing garbage values (huge floats like `2.028696E+32`)
- Stats showing pointer references (`pooledPtr`, `myGcHandle`) instead of actual data
- IL2CPP interop errors in MelonLoader logs

**Update frequency:** Likely needed for each major game update that changes Unity version or core game assemblies.

#### IL2CPP Dump Generation Process:

**Prerequisites:**
```bash
# Set up paths (adjust to your environment)
export IL2CPP_DUMPER_DIR="$HOME/Il2CppDumper"
export MODKIT_DIR="$HOME/MenaceAssetPacker"  # or wherever you cloned the modkit

# 1. Install Il2CppDumper (one-time setup)
cd "$IL2CPP_DUMPER_DIR"
git clone https://github.com/Perfare/Il2CppDumper.git
cd Il2CppDumper

# 2. Build Il2CppDumper
dotnet build -c Release
```

**Generate New Dump (when game updates):**

```bash
# 1. Navigate to Il2CppDumper
cd "$IL2CPP_DUMPER_DIR/Il2CppDumper/Il2CppDumper/bin/Release/net8.0/"

# 2. Copy game files to a temporary location (to avoid permission issues)
# Adjust GAME_DIR to your Steam library location
GAME_DIR="$HOME/.steam/steam/steamapps/common/Menace"
cp "$GAME_DIR/GameAssembly.so" /tmp/
cp "$GAME_DIR/Menace_Data/il2cpp_data/Metadata/global-metadata.dat" /tmp/

# 3. Run Il2CppDumper
./Il2CppDumper /tmp/GameAssembly.so /tmp/global-metadata.dat /tmp/dump_output/

# 4. Copy the generated dump.cs to the modkit project
cp /tmp/dump_output/dump.cs "$MODKIT_DIR/il2cpp_dump/"

# 5. Check Unity version in the dump
head -n 20 "$MODKIT_DIR/il2cpp_dump/dump.cs"
# Should show: // Unity 6000.0.56f1 (or current version)
```

**Extract Field Offsets from dump.cs:**

Search for class definitions in dump.cs to find field offsets:

```bash
# Example: Find WeaponTemplate offsets
grep -A 30 "class WeaponTemplate" il2cpp_dump/dump.cs

# Example: Find EntityProperties offsets
grep -A 100 "class EntityProperties" il2cpp_dump/dump.cs
```

**Key Offset Patterns:**

```csharp
// From dump.cs:
public class WeaponTemplate  // TypeDefIndex: 2561
{
    // Fields
    public int MinRange; // 0x13C  ← Use this offset directly
    public int IdealRange; // 0x140
    public float Damage; // 0x150
    ...
}

public class EntityTemplate  // TypeDefIndex: 2738
{
    // Fields
    public int ElementsMin; // 0xA0
    public int ElementsMax; // 0xA4
    public EntityProperties Properties; // 0x2E0  ← Embedded struct
    ...
}

public class EntityProperties  // TypeDefIndex: 2740
{
    // Fields
    public int MaxElements; // 0x10  ← Includes object header
    public int HitpointsPerElement; // 0x14
    public int Armor; // 0x1C
    ...
}
```

**Update DataExtractorMod.cs with New Offsets:**

1. For direct fields (reference types or primitives):
   ```csharp
   data["MinRange"] = Marshal.ReadInt32(ptr + 0x13C);  // Use offset as-is
   ```

2. For embedded value types (structs stored inline):
   ```csharp
   // EntityProperties embedded at EntityTemplate+0x2E0
   IntPtr propsBase = ptr + 0x2E0;

   // SUBTRACT 0x10 from dump.cs offsets for embedded structs
   props["HitpointsPerElement"] = Marshal.ReadInt32(propsBase + 0x14 - 0x10);
   props["Armor"] = Marshal.ReadInt32(propsBase + 0x1C - 0x10);
   ```

**Why subtract 0x10?**
- dump.cs offsets assume the struct is a standalone object with a 0x10 byte IL2CPP header
- When embedded inline in another object, the struct has no header
- Therefore, all field offsets are 0x10 bytes earlier than dump.cs indicates

**Rebuild and Deploy:**

```bash
cd "$MODKIT_DIR"

# Build with updated offsets
dotnet build src/Menace.DataExtractor

# Deploy to game (adjust GAME_DIR to your Steam library location)
GAME_DIR="$HOME/.steam/steam/steamapps/common/Menace"
cp -f src/Menace.DataExtractor/bin/Debug/net6.0/Menace.DataExtractor.dll \
      "$GAME_DIR/Mods/"

# Test by launching game and checking extracted JSON
```

**Verification:**

```bash
# Check extracted data for correct values
cat "$HOME/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData/WeaponTemplate.json" | head -n 20

# Should show actual game stats, not huge garbage values or pointer references
# Example good output:
# "MinRange": 1,
# "Damage": 165.0,
# "ArmorPenetration": 150.0

# Example bad output (means offsets are wrong):
# "Accuracy": 2.028696E+32,
# "pooledPtr": { ... },
```

**Tools:**
1. **Il2CppDumper** (https://github.com/Perfare/Il2CppDumper)
   - Generates field offset dumps
   - Produces `dump.cs` with memory layout information
   - Required for each game update that changes Unity version

2. **Cpp2IL** (Used by MelonLoader automatically)
   - Decompiles `GameAssembly.dll` back to .NET DLLs
   - Location: `MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/`
   - Output: `cpp2il_out/Assembly-CSharp.dll` (managed proxy)

**Current Dump:**
- Location: `/il2cpp_dump/dump.cs`
- Unity Version: 6000.0.56f1
- Size: 874,630 lines
- Generated: 2025-10-10 (when game updated to Unity 6)

**Offset Usage Example:**
```csharp
// Direct memory reading (requires correct offsets)
if (templateType.Name == "WeaponTemplate")
{
    // Reference type fields - use offsets directly
    data["MinRange"] = Marshal.ReadInt32(ptr + 0x13C);
    data["IdealRange"] = Marshal.ReadInt32(ptr + 0x140);
    data["MaxRange"] = Marshal.ReadInt32(ptr + 0x144);
    data["Damage"] = BitConverter.ToSingle(
        BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x150)), 0);
}
else if (templateType.Name == "EntityTemplate")
{
    // Embedded value type - subtract 0x10 from all offsets
    IntPtr propsBase = ptr + 0x2E0;
    props["HitpointsPerElement"] = Marshal.ReadInt32(propsBase + 0x14 - 0x10);
    props["Armor"] = Marshal.ReadInt32(propsBase + 0x1C - 0x10);
}
```

---

### 6. AssetRipper Integration

**Version:** Latest (bundled in `third_party/`)
**Purpose:** Extract Unity assets (textures, prefabs, meshes, audio)

#### How It Works:

1. **Server Mode**
   ```csharp
   var startInfo = new ProcessStartInfo
   {
       FileName = assetRipperPath,
       Arguments = $"--launch-browser=false --port={_port}",
       RedirectStandardOutput = true,
       UseShellExecute = false
   };
   Process.Start(startInfo);
   ```

2. **HTTP API Endpoints**
   - `POST http://localhost:5734/LoadFolder`
     - Body: `Path={gameDataPath}`
     - Loads Unity asset bundles from `Menace_Data/`

   - `POST http://localhost:5734/Export/PrimaryContent`
     - Body: `Path={outputPath}`
     - Exports assets to specified directory

3. **Export Output**
   ```
   out2/assets/
   ├── Texture2D/        ← Extracted textures (PNG)
   ├── Prefab/           ← Unity prefabs (YAML)
   ├── Mesh/             ← 3D meshes
   ├── Material/         ← Materials
   └── AudioClip/        ← Sound files
   ```

4. **Integration in Modkit**
   - `AssetRipperService.cs` manages the AssetRipper process
   - HTTP client with 10-minute timeout for large exports
   - Automatic cleanup on completion

**Finding AssetRipper:**
```csharp
var bundledAssetRipper = Path.Combine(
    AppContext.BaseDirectory,
    "third_party", "bundled", "AssetRipper", "AssetRipper.GUI.Free");

// Make executable on Linux
Process.Start("chmod", $"+x \"{bundledAssetRipper}\"");
```

---

## Workflows

### 1. First-Time Setup Workflow

```
User launches Modkit
    ↓
Modkit checks for vanilla data
    ↓
[NOT FOUND]
    ↓
Modkit displays onboarding screen
    ↓
User clicks "Auto Setup"
    ↓
ModLoaderInstaller checks for MelonLoader
    ↓
[NOT INSTALLED]
    ↓
Download MelonLoader → Install to game dir
    ↓
Copy DataExtractor.dll to Mods/
    ↓
User clicks "Launch Game to Update Data"
    ↓
Game launches with MelonLoader
    ↓
DataExtractor mod runs at game startup
    ↓
Extracts 70+ template types to JSON
    ↓
User closes game
    ↓
User clicks "Refresh" in modkit
    ↓
Modkit loads extracted JSON data
    ↓
Stats Editor now functional
```

### 2. Stats Editing Workflow

```
User opens Stats Editor
    ↓
StatsEditorViewModel loads templates
    ↓
DataTemplateLoader reads JSON files
    ↓
Builds hierarchical tree (TreeNodeViewModel)
    ↓
User selects template (e.g., "weapon.assault_rifle.tier1")
    ↓
ViewModel loads vanilla and modified properties
    ↓
UI displays side-by-side comparison
    ↓
User edits modified properties (damage, range, etc.)
    ↓
Changes saved to staging directory
    ↓
Modpack builder packages changes
    ↓
User installs modpack to game
```

### 3. Game Update Workflow

```
Game receives update (new Unity version, new content)
    ↓
Memory offsets may have changed
    ↓
User clicks "Launch Game to Update Data"
    ↓
DataExtractor re-runs with updated game
    ↓
Extracts latest template data
    ↓
User clicks "Refresh" in modkit
    ↓
Modkit loads updated vanilla data
    ↓
Stats Editor updated with new baseline
```

---

## Current Known Issues

### Issue 1: Stats Showing Pointer References

**Symptom:**
```json
{
  "name": "mod_weapon.heavy.cannon_long",
  "pooledPtr": { "value": 1949509824 },
  "myGcHandle": { "value": 958973976 }
}
```
Expected: `MinRange`, `Damage`, `ArmorPenetration`, etc.

**Root Cause:**
- IL2CPP wrapper classes don't expose actual game properties via reflection
- Only IL2CPP internal fields (`pooledPtr`, `myGcHandle`) are visible
- Need direct memory reading with correct Unity 6 offsets

**Solution Approaches:**
1. ~~Reflection-based extraction~~ ❌ Doesn't work with IL2CPP
2. ~~`Marshal.OffsetOf()`~~ ❌ Returns "unknown" for IL2CPP wrappers
3. **Direct memory reading with IL2CPP dump** ✅ (Needs Unity 6 offsets)
4. Alternative: Use Il2CppDumper to regenerate offset map

---

## Technology Stack Summary

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| Desktop App | Avalonia | 11.3.7 | Cross-platform UI |
| App Framework | .NET | 10.0 | Modern C# features |
| Core Library | .NET | 10.0 | Shared business logic |
| MelonLoader Mods | .NET | 6.0 | Game/MelonLoader compatibility |
| UI Pattern | ReactiveUI | Latest | MVVM with observables |
| Mod Loader | MelonLoader | 0.7.2-ci.2388 | IL2CPP mod framework |
| IL2CPP Interop | Il2CppInterop | 1.5.0 | Managed ↔ IL2CPP bridge |
| Decompiler | Cpp2IL | 2022.1.0 | IL2CPP to .NET |
| Asset Extractor | AssetRipper | 1.3.4-patched | Unity asset export |
| Game Engine | Unity | 6000.0.56f1 | Game runtime |
| Serialization | System.Text.Json | .NET 10 | Template JSON |
| Mod Serialization | Newtonsoft.Json | 13.0.3 | DataExtractor output |

---

## File Structure

```
MenaceAssetPacker/
├── src/
│   ├── Menace.Modkit.App/              ← Desktop application (.NET 10)
│   │   ├── Views/
│   │   │   ├── StatsEditorView.axaml.cs
│   │   │   └── MainWindow.axaml.cs
│   │   ├── ViewModels/
│   │   │   └── StatsEditorViewModel.cs
│   │   ├── Services/
│   │   │   ├── AssetRipperService.cs
│   │   │   ├── ModpackManager.cs
│   │   │   └── ModLoaderInstaller.cs
│   │   └── Assets/
│   │       └── icon.jpg
│   │
│   ├── Menace.Modkit.Core/             ← Shared library (.NET 10)
│   │   ├── Models/
│   │   │   ├── DataTemplate.cs
│   │   │   └── DynamicDataTemplate.cs
│   │   └── Services/
│   │       └── DataTemplateLoader.cs
│   │
│   └── Menace.DataExtractor/           ← MelonLoader mod (.NET 6)
│       ├── DataExtractorMod.cs
│       ├── OffsetDumper.cs
│       └── Menace.DataExtractor.csproj
│
├── third_party/
│   └── bundled/
│       ├── AssetRipper/
│       │   └── AssetRipper.GUI.Free    ← Unity asset extractor
│       ├── DataExtractor/
│       │   └── Menace.DataExtractor.dll
│       └── MelonLoader/                (optional bundling)
│
├── il2cpp_dump/
│   └── dump.cs                         ← Historic IL2CPP memory layout (33MB)
│
├── ARCHITECTURE.md                     ← This file
└── README.md
```

---

## Development Notes

### Building the Modkit

```bash
# Build everything
dotnet build

# Build specific components
dotnet build src/Menace.Modkit.App -c Release
dotnet build src/Menace.DataExtractor -c Release

# Run the app
dotnet run --project src/Menace.Modkit.App
```

### Installing DataExtractor Manually

```bash
# Copy to game mods folder
cp src/Menace.DataExtractor/bin/Release/net6.0/Menace.DataExtractor.dll \
   ~/.steam/debian-installation/steamapps/common/Menace\ Demo/Mods/

# Copy to bundled resources
cp src/Menace.DataExtractor/bin/Release/net6.0/Menace.DataExtractor.dll \
   third_party/bundled/DataExtractor/
```

### Debugging DataExtractor

Check MelonLoader logs:
```bash
tail -f ~/.steam/debian-installation/steamapps/common/Menace\ Demo/MelonLoader/Latest.log
```

Look for:
- `[Menace_Data_Extractor]` log entries
- Extraction success: `✓ Extraction completed successfully`
- Errors: Warnings and exception stack traces

---

## Future Improvements

1. **Automated IL2CPP Offset Generation**
   - Detect Unity version changes
   - Auto-regenerate IL2CPP dump
   - Update memory offsets dynamically

2. **Modpack Format**
   - Define modpack file structure
   - Implement mod conflict resolution
   - Support mod dependencies

3. **Visual Asset Editor**
   - Integrate AssetRipper GUI
   - Texture replacement workflow
   - Prefab modification

4. **Steam Workshop Integration**
   - Publish/subscribe to mods
   - Automatic mod updates
   - Community ratings

---

## Support & Troubleshooting

### Common Issues

**"Vanilla Game Data Not Found"**
- Run Auto Setup to install MelonLoader and DataExtractor
- Launch game once to extract data
- Click Refresh in Stats Editor

**"Stats showing pooledPtr instead of values"**
- This is the current known issue
- Memory offsets need updating for Unity 6
- Workaround: Check JSON files directly in UserData/ExtractedData/

**"AssetRipper status: notfound"**
- Fixed in latest version (endpoint corrected to `/Export/PrimaryContent`)
- Ensure AssetRipper binary is executable: `chmod +x AssetRipper.GUI.Free`

---

## Credits

- **MelonLoader**: [https://melonwiki.xyz/](https://melonwiki.xyz/)
- **Il2CppInterop**: [https://github.com/BepInEx/Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- **Cpp2IL**: [https://github.com/SamboyCoding/Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)
- **AssetRipper**: [https://github.com/AssetRipper/AssetRipper](https://github.com/AssetRipper/AssetRipper)
- **Avalonia UI**: [https://avaloniaui.net/](https://avaloniaui.net/)
