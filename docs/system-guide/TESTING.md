# Testing

The Menace Modkit uses xUnit for automated testing. Tests run without the game installed, using mocks for IL2CPP-specific behavior.

## Running Tests

### All Tests

```bash
# Run all test suites
dotnet test

# With verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Individual Suites

```bash
# App tests (manifests, security, conflicts)
dotnet test tests/Menace.Modkit.Tests

# SDK tests (GameType, GameObj, REPL)
dotnet test tests/Menace.ModpackLoader.Tests
```

### Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~SecurityScannerTests"
dotnet test --filter "FullyQualifiedName~RuntimeCompilerTests"
```

## Test Suites

### Menace.Modkit.Tests

Tests for the desktop application and core services.

| Test Class | Coverage |
|------------|----------|
| `ManifestRoundTripTests` | Modpack manifest serialization/deserialization |
| `DependencyParsingTests` | Dependency version parsing (`SomeMod>=1.0`) |
| `PatchMergeTests` | Merging patches across multiple modpacks |
| `ConflictDetectionTests` | Detecting overlapping modifications |
| `SecurityScannerTests` | Flagging dangerous APIs in mod code |
| `DeployStateRoundTripTests` | Deployment state persistence |
| `RuntimeManifestCompatTests` | V1/V2 manifest compatibility |
| `ReferenceResolverTests` | Game assembly reference resolution |
| `PluginManifestTests` | Plugin DLL manifest handling |
| `TypetreeCacheServiceTests` | Unity typetree caching |

#### Key Test Areas

**Manifest Handling:**
```csharp
[Fact]
public void RoundTrip_PreservesAllFields()
{
    var manifest = new ModpackManifest {
        Name = "Test",
        Version = "1.0.0",
        Patches = new() { ["WeaponTemplate"] = ... }
    };

    var json = JsonConvert.SerializeObject(manifest);
    var restored = JsonConvert.DeserializeObject<ModpackManifest>(json);

    Assert.Equal(manifest.Name, restored.Name);
    Assert.Equal(manifest.Patches.Count, restored.Patches.Count);
}
```

**Security Scanning:**
```csharp
[Fact]
public void DetectsFileSystemAccess()
{
    var code = "File.WriteAllText(\"x\", \"y\");";
    var result = SecurityScanner.Scan(code);

    Assert.Contains(result.Warnings, w => w.Category == "FileSystem");
}
```

**Conflict Detection:**
```csharp
[Fact]
public void DetectsOverlappingPatches()
{
    var mod1 = new ModpackManifest { Patches = WeaponDamage(50) };
    var mod2 = new ModpackManifest { Patches = WeaponDamage(100) };

    var conflicts = ConflictDetector.FindConflicts(mod1, mod2);

    Assert.Single(conflicts);
    Assert.Equal("WeaponTemplate.Pistol.Damage", conflicts[0].Path);
}
```

### Menace.ModpackLoader.Tests

Tests for the runtime SDK used by mod developers.

| Test Class | Coverage |
|------------|----------|
| `GameTypeTests` | IL2CPP type lookup and caching |
| `GameObjTests` | Safe wrapper for IL2CPP objects |
| `GameStateTests` | Scene tracking, delayed execution |
| `GameCollectionTests` | IL2CPP collection iteration |
| `ModErrorTests` | Error reporting, rate limiting, deduplication |
| `RuntimeCompilerTests` | Roslyn C# compilation |
| `RuntimeReferenceResolverTests` | Assembly reference resolution for REPL |
| `ConsoleEvaluatorTests` | C# expression evaluation |

#### Key Test Areas

**GameType Resolution:**
```csharp
[Fact]
public void FindType_CachesResult()
{
    var type1 = GameType.Find("WeaponTemplate");
    var type2 = GameType.Find("WeaponTemplate");

    Assert.Same(type1, type2);
}
```

**Error Rate Limiting:**
```csharp
[Fact]
public void RateLimits_ExcessiveErrors()
{
    for (int i = 0; i < 20; i++)
        ModError.Report("TestMod", "Same error");

    var errors = ModError.GetErrors("TestMod");
    Assert.True(errors.Count <= 10); // Rate limited
}
```

**REPL Compilation:**
```csharp
[Fact]
public void Compiles_SimpleExpression()
{
    var compiler = new RuntimeCompiler(TestReferences.Minimal);
    var result = compiler.Compile("1 + 1");

    Assert.True(result.Success);
    Assert.Empty(result.Errors);
}
```

## Test Infrastructure

### Stubs

IL2CPP types are stubbed for testing without the game:

```
tests/stubs/
├── Stubs.MelonLoader/     # MelonLogger stubs
└── Stubs.Il2CppInterop/   # IL2CPP, Il2CppObjectBase stubs
```

### Helpers

```
tests/Menace.Modkit.Tests/Helpers/
├── TemporaryDirectory.cs    # Auto-cleanup temp dirs
├── ManifestFixtures.cs      # Sample manifest data
└── RuntimeModpackMirror.cs  # Simulates runtime loading
```

### Test References

For REPL tests, minimal assembly references:

```csharp
public static class TestReferences
{
    public static IReadOnlyList<MetadataReference> Minimal => new[] {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
    };
}
```

## Writing New Tests

### Modkit App Tests

```csharp
namespace Menace.Modkit.Tests.Integration;

public class MyFeatureTests
{
    [Fact]
    public void MyFeature_DoesExpectedThing()
    {
        // Arrange
        var input = CreateTestInput();

        // Act
        var result = MyService.Process(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("input1", "expected1")]
    [InlineData("input2", "expected2")]
    public void MyFeature_HandlesVariousInputs(string input, string expected)
    {
        var result = MyService.Process(input);
        Assert.Equal(expected, result);
    }
}
```

### SDK Tests

```csharp
namespace Menace.ModpackLoader.Tests.SDK;

public class MySDKFeatureTests
{
    [Fact]
    public void Feature_WorksWithStubs()
    {
        // SDK tests use stubs instead of real IL2CPP
        var obj = new MockGameObj();

        var result = GameQuery.Process(obj);

        Assert.True(result.Success);
    }
}
```

## CI Integration

Tests are designed to run in CI without game installation:

```yaml
# Example GitHub Actions
- name: Run Tests
  run: dotnet test --configuration Release --logger trx
```

No special setup required — all dependencies are mocked.

## Coverage Areas

### What's Tested

- Manifest parsing and serialization
- Patch merging logic
- Conflict detection algorithms
- Security scanning rules
- Dependency version parsing
- SDK API contracts
- REPL compilation pipeline
- Error handling and rate limiting

### What's Not Tested (Requires Game)

- Actual IL2CPP type resolution
- Runtime template patching
- Asset loading and replacement
- Harmony patches
- Unity object interactions

These require manual testing with the game installed.

## Manual Testing

For features requiring the game:

1. Build the modkit: `dotnet build`
2. Deploy a test modpack
3. Launch game with MelonLoader
4. Check `MelonLoader/Latest.log` for results
5. Use Dev Console (`~`) to inspect runtime state

### Dev Console Commands for Testing

```
# Verify templates loaded
templates WeaponTemplate

# Check for errors
errors

# Inspect specific object
inspect WeaponTemplate Pistol_Basic

# View current scene
scene
```

## Troubleshooting Tests

### Tests Fail to Build

```bash
# Restore dependencies
dotnet restore

# Clean and rebuild
dotnet clean && dotnet build
```

### Stub Type Conflicts

If IL2CPP stubs conflict with real types:

1. Check `Stubs.*` projects don't reference real assemblies
2. Verify test project references stubs, not game DLLs

### REPL Tests Fail

REPL tests require Roslyn packages:

```bash
# Verify packages restored
dotnet restore tests/Menace.ModpackLoader.Tests
```
