# Menace.Modkit.Cli - Maintainer Documentation

## Overview

**Menace.Modkit.Cli** is a command-line interface application for the Menace modkit. It provides a minimal, focused tool that currently implements a single command: `cache-typetrees`.

**Location:** `src/Menace.Modkit.Cli/`

## Architecture

### Design Patterns
- **Framework:** Spectre.Console.Cli for modern CLI UX
- **DI:** Microsoft Extensions DependencyInjection via TypeRegistrar adapter
- **Commands:** AsyncCommand pattern with typed settings

### Directory Structure

```
src/Menace.Modkit.Cli/
├── Program.cs                    # Entry point & CLI bootstrap
├── Menace.Modkit.Cli.csproj     # Project configuration
├── Commands/
│   └── CacheTypetreeCommand.cs  # Typetree caching command
└── Infrastructure/
    └── TypeRegistrar.cs         # DI adapter for Spectre.Console.Cli
```

## Entry Point

**Location:** `Program.cs:1-34`

```csharp
static async Task<int> RunAsync(string[] args)
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddMenaceModkitCore();

    var registrar = new TypeRegistrar(services);
    var app = new CommandApp(registrar);

    app.Configure(config =>
    {
        config.SetApplicationName("menace-modkit");
        config.AddCommand<CacheTypetreeCommand>("cache-typetrees")
            .WithDescription("Builds a typetree cache from a Menace installation.");
    });

    return await app.RunAsync(args);
}
```

## Commands

### cache-typetrees

**Location:** `Commands/CacheTypetreeCommand.cs:1-73`

Builds a typetree cache from a Menace installation by extracting Unity type metadata.

**Usage:**
```bash
menace-modkit cache-typetrees --source <PATH> --output <PATH> [OPTIONS]
```

**Options:**

| Option | Alias | Type | Required | Description |
|--------|-------|------|----------|-------------|
| `--source` | `-s` | DIRECTORY | Yes | Path to game installation |
| `--output` | `-o` | DIRECTORY | Yes | Destination for cache |
| `--game-version` | - | VERSION | No | Game version for manifest |
| `--unity-version` | - | VERSION | No | Unity version for manifest |

**Implementation:**

```csharp
public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
{
    var request = new TypetreeCacheRequest(
        SourcePath: settings.Source!,
        OutputPath: settings.Output!,
        GameVersion: settings.GameVersion,
        UnityVersion: settings.UnityVersion
    );

    var result = await _cacheBuilder.BuildAsync(request);
    AnsiConsole.MarkupLine($"[green]✓[/] Typetree cache created: {result.ManifestPath}");
    return 0;
}
```

**Return Codes:**
- `0` - Success
- `-1` - Command execution failure
- `-99` - Fatal unhandled exception

## DI Infrastructure

### TypeRegistrar

**Location:** `Infrastructure/TypeRegistrar.cs:6-34`

Bridges Microsoft's IServiceCollection with Spectre.Console.Cli's ITypeRegistrar:

```csharp
internal sealed class TypeRegistrar : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(_services.BuildServiceProvider());
    public void Register(Type service, Type implementation)
        => _services.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object instance)
        => _services.AddSingleton(service, instance);
    public void RegisterLazy(Type service, Func<object> factory)
        => _services.AddSingleton(service, _ => factory());
}
```

### TypeResolver

**Location:** `Infrastructure/TypeRegistrar.cs:36-54`

```csharp
internal sealed class TypeResolver : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type)
        => type == null ? null : _provider.GetService(type);
    public void Dispose()
        => (_provider as IDisposable)?.Dispose();
}
```

All services registered with **singleton** lifetime.

## Dependencies

### NuGet Packages
- `Microsoft.Extensions.Hosting` (8.0.0) - DI and hosting
- `Spectre.Console` (0.48.0) - Rich console UI
- `Spectre.Console.Cli` (0.48.0) - Command-line parsing

### Project References
- `Menace.Modkit.Core` - Core services

### Services from Core
- `ITypetreeCacheBuilder` → `TypetreeCacheService`
- `IUnityVersionDetector` → `UnityVersionDetector`

## Command Execution Flow

```
Program.cs::RunAsync()
    ↓
ServiceCollection setup + AddMenaceModkitCore()
    ↓
TypeRegistrar wraps ServiceCollection
    ↓
CommandApp created with TypeRegistrar
    ↓
app.RunAsync(args) [Spectre handles parsing]
    ↓
CacheTypetreeCommand::Validate() [Pre-execution]
    ↓
CacheTypetreeCommand::ExecuteAsync() [Main logic]
    ↓
ITypetreeCacheBuilder.BuildAsync() [Core service]
    ↓
Return exit code
```

## Inconsistencies & Notes

### Console Output vs Logging
**Issue:** `TypetreeCacheService.BuildAsync()` uses `Console.WriteLine()` directly instead of ILogger.

```csharp
// TypetreeCacheService.cs
Console.WriteLine($"[TypetreeCache] Source directory: {normalizedSource}");
Console.WriteLine($"[TypetreeCache]   ❌ Failed to load file");
```

**Impact:** Cannot redirect logs to file or control verbosity.
**Recommendation:** Inject `ILogger<TypetreeCacheService>`.

### Hard-coded Tool Version
**Location:** `TypetreeCacheService.cs:162`

```csharp
ToolVersion = "0.1.0-dev",
```

**Recommendation:** Use assembly version attributes.

### Exit Code Documentation
- `CacheTypetreeCommand` returns `-1` on failure
- `Program.cs` returns `-99` on fatal exception

**Recommendation:** Document standard exit codes.

## Adding New Commands

1. Create new file: `Commands/NewCommand.cs`
   ```csharp
   internal sealed class NewCommand : AsyncCommand<NewCommand.Settings>
   {
       // Implementation
   }
   ```

2. Register in `Program.cs`:
   ```csharp
   config.AddCommand<NewCommand>("new-command")
       .WithDescription("...");
   ```

3. Add corresponding Core service if needed

## Metrics

| File | Lines | Purpose |
|------|-------|---------|
| Program.cs | 34 | Entry point |
| CacheTypetreeCommand.cs | 73 | Command impl |
| TypeRegistrar.cs | 54 | DI adapter |
| **Total** | **161** | CLI code |

The CLI is intentionally minimal - heavy lifting is delegated to Core library.
