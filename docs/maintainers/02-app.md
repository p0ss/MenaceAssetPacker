# Menace.Modkit.App - Maintainer Documentation

## Overview

**Menace.Modkit.App** is the main desktop GUI application for the Menace modkit. It provides a comprehensive interface for modpack management, game data editing, asset browsing, and deployment.

**Location:** `src/Menace.Modkit.App/`

## Architecture

### Framework & Pattern
- **UI Framework:** Avalonia 11.3.7 (cross-platform)
- **Pattern:** MVVM using ReactiveUI
- **DI:** Microsoft.Extensions.DependencyInjection
- **Runtime:** .NET 10.0

### Startup Flow
1. `Program.Main()` registers custom Roslyn assembly resolver
2. `BuildAvaloniaApp()` initializes Avalonia with ReactiveUI support
3. `App.Initialize()` sets up ServiceCollection with `AddMenaceModkitCore()`
4. `OnFrameworkInitializationCompleted()` checks setup status (15-second timeout)
5. Shows SetupWindow if needed, otherwise shows MainWindow

## Views and ViewModels

| View | ViewModel | Purpose | Lines |
|------|-----------|---------|-------|
| MainWindow | MainViewModel | Root navigation hub | 193 |
| HomeView | HomeViewModel | Welcome/splash screen | - |
| (Modpacks Panel) | ModpacksViewModel | Load order, deployment | 1,086 |
| (Saves Panel) | SaveEditorViewModel | Save file editing | 601 |
| (Loader Settings) | LoaderSettingsViewModel | Game path, logs | - |
| (Stats Editor) | StatsEditorViewModel | Template editing | 2,622 |
| (Asset Browser) | AssetBrowserViewModel | Model/texture browsing | 1,639 |
| (Code Editor) | CodeEditorViewModel | Lua script editor | 635 |
| (Docs) | DocsViewModel | Markdown docs viewer | 705 |
| (Tool Settings) | ToolSettingsViewModel | Extraction, caching | 990 |
| SetupView | SetupViewModel | Component install | 727 |

## Navigation System

### Two-Level Navigation

**Main Sections** (`MainViewModel.CurrentSection`):
- Home
- ModLoader
- ModdingTools

**Sub-Sections** (`MainViewModel.CurrentSubSection`):
- ModLoader: "Load Order", "Saves", "Settings"
- ModdingTools: "Data", "Assets", "Code", "Docs", "Settings"

### Cross-Tab Navigation

```csharp
// Example: Navigate from modpack to stats editor
ModpacksViewModel.NavigateToStatsEntry(modpackName, templateType, instanceName)
  → MainViewModel.NavigateToModdingTools()
  → MainViewModel.NavigateTo(StatsEditor, "Data")
  → StatsEditor.NavigateToEntry(modpackName, templateType, instanceName)
```

## Key Services

### ModpackManager
**Location:** `Services/ModpackManager.cs` (1,176 lines)

Central modpack staging management:
- Staging directory: `~/Documents/MenaceModkit/staging/`
- Validates modpack names (regex: `^[a-zA-Z0-9_\-. ]+$`, max 64 chars)
- Seeds bundled runtime DLLs and modpacks
- Auto-migrates legacy ModpackInfo v1 to ModpackManifest v2

### DeployManager
**Location:** `Services/DeployManager.cs` (362+ lines)

Deployment pipeline:
1. Deploy runtime DLLs
2. Compile source code (if present)
3. Deploy modpack assets
4. Compile merged asset bundles
5. Clean removed mods

### CompilationService
**Location:** `Services/CompilationService.cs` (198+ lines)

Roslyn-based C# compilation:
- Targets net6.0 (MelonLoader runtime)
- Parses source files → creates SyntaxTrees
- Loads reference assemblies
- SecurityScanner integration for source validation

### AppSettings
**Location:** `Services/AppSettings.cs`

Persists to `~/.config/MenaceModkit/settings.json`:
- GameInstallPath
- ExtractedAssetsPath
- EnableDeveloperTools
- EnableMcpServer
- ExtractionSettings

### UIStateService & UIHttpServer
- `UIStateService` - Exposes UI state to MCP/AI (writes to `~/.menace-modkit/ui-state.json`)
- `UIHttpServer` - HTTP API on port 21421 for automation

## State Management

### StatsEditorViewModel State (Complex)

```
StatsEditorViewModel
├── TreeNodes: ObservableCollection<TreeNodeViewModel>
├── SelectedNode: TreeNodeViewModel
├── _pendingChanges: Dictionary<string, Dictionary<string, object?>>
├── _stagingOverrides: Dictionary<string, Dictionary<string, object?>>
├── _pendingRemovals: Dictionary<string, HashSet<string>>
├── _cloneDefinitions: Dictionary<string, string>
├── _userEditedFields: HashSet<string>
└── _suppressPropertyUpdates: bool
```

The StatsEditor implements a sophisticated multi-layer change tracking system:
- `_userEditedFields` - Tracks only user-edited fields (not render-triggered)
- `_suppressPropertyUpdates` - Flag to skip initial TextBox renders
- Prevents false diffs from UI render cycles

### Conflict Detection

LoadOrderViewModel runs ConflictDetector:
- **FieldConflict**: Same template instance edited by multiple modpacks
- **DllConflict**: Multiple modpacks provide same .dll
- **DependencyIssue**: Unmet or circular dependencies

## ISearchableViewModel Interface

**Location:** `ViewModels/ISearchableViewModel.cs:1-38`

```csharp
interface ISearchableViewModel {
    string SearchText { get; set; }
    bool IsSearching { get; }  // true when SearchText.Length >= 3
    ObservableCollection<SearchResultItem> SearchResults { get; }
    SortOption CurrentSortOption { get; set; }
    void SelectSearchResult();
    void SelectAndExitSearch();
    void FocusSelectedInTree();
}
```

## Dependencies

### NuGet Packages
- Avalonia 11.3.7: Core UI
- Avalonia.ReactiveUI 11.3.7: Reactive bindings
- Avalonia.AvaloniaEdit 11.4.1: Code editor control
- Microsoft.CodeAnalysis.CSharp 4.14.0: Roslyn compilation
- Newtonsoft.Json 13.0.3
- SharpGLTF.Core 1.0.6: GLB/GLTF models

### Project References
- Menace.Modkit.Core
- ModkitVersion.cs (shared file)

## TODOs and Known Issues

### Explicit TODOs

1. **ModpackManifest.cs:29** - Future repository adapters
   ```csharp
   // TODO: Future adapters - Nexus, GameBanana, ModDB, Thunderstore, Custom
   ```

2. **ModUpdateChecker.cs:91, 328** - Platform-specific detection incomplete
   ```csharp
   // TODO: Add adapters for other platforms
   ```

3. **BulkEditorPanel.axaml.cs:574** - Complex type editor modal
   ```csharp
   // TODO: Open modal editor for complex types
   ```

4. **ToolSettingsViewModel.cs:468** - Cache info dialog
   ```csharp
   // TODO: Show detailed cache information dialog
   ```

### Potential Issues

1. **Asset Ripper Integration** (Program.cs:175)
   - Kills AssetRipper.GUI.Free processes on exit without confirmation

2. **Setup Timeout** (App.axaml.cs:73)
   - 15-second hardcoded timeout for setup check

3. **Roslyn Assembly Loading** (Program.cs:42-66)
   - Fragile assembly name matching for Roslyn DLLs
   - Could break with version updates

4. **Thread Safety**
   - UIStateService writes JSON periodically without locking
   - No concurrent modification protection in ObservableCollections

## Resource Organization

```
Menace.Modkit.App/
├── Assets/           # Images, icons
├── Styles/           # XAML themes
├── Views/            # XAML markup + codebehind
├── ViewModels/       # C# logic
├── Models/           # Data models
├── Services/         # Business logic
├── Controls/         # Custom Avalonia controls
├── Converters/       # Value converters
└── Extensions/       # Helper methods
```

## Logging

ModkitLog service provides structured logging with categories:
- `[App]` - Application lifecycle
- `[DeployManager]` - Deployment operations
- `[CompilationService]` - Compilation diagnostics
- `[StatsEditor]` - Editor operations
