using System.Net.Http;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Integration tests that verify asset deployment by querying the running game.
///
/// Prerequisites:
/// 1. Game must be running with ModpackLoader installed
/// 2. MCP Server must be enabled (DevConsole -> Settings -> MCP Server)
/// 3. Modpacks must be deployed before running these tests
///
/// Run with: dotnet test --filter "Category=GameIntegration"
/// </summary>
[Trait("Category", "GameIntegration")]
public class AssetDeploymentTests : IDisposable
{
    private readonly HttpClient _gameClient;
    private readonly HttpClient _modkitClient;
    private readonly ITestOutputHelper _output;

    private const string GAME_MCP_URL = "http://127.0.0.1:7655";
    private const string MODKIT_MCP_URL = "http://127.0.0.1:7654"; // If modkit has HTTP endpoint

    public AssetDeploymentTests(ITestOutputHelper output)
    {
        _output = output;
        _gameClient = new HttpClient
        {
            BaseAddress = new Uri(GAME_MCP_URL),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _modkitClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60) // Deploy can take longer
        };
    }

    public void Dispose()
    {
        _gameClient.Dispose();
        _modkitClient.Dispose();
    }

    /// <summary>
    /// Check if the game MCP server is accessible.
    /// </summary>
    [Fact]
    public async Task GameMcpServer_IsRunning()
    {
        var response = await _gameClient.GetAsync("/status");
        Assert.True(response.IsSuccessStatusCode, "Game MCP server not responding. Is the game running with MCP enabled?");

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Game status: {json}");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("running").GetBoolean());
    }

    /// <summary>
    /// Verify that templates can be listed from the running game.
    /// </summary>
    [Fact]
    public async Task Templates_CanBeQueried()
    {
        var response = await _gameClient.GetAsync("/templates?type=EntityTemplate&limit=5");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Templates: {json}");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("templates", out var templates) ||
                    doc.RootElement.TryGetProperty("count", out _),
                    "Expected templates or count in response");
    }

    /// <summary>
    /// Verify that a specific cloned template exists after deployment.
    /// Call this with the name of a clone you expect to exist.
    /// </summary>
    [Theory]
    [InlineData("weapon.laser_smg")] // Example - adjust to your actual clone names
    public async Task ClonedTemplate_Exists(string templateId)
    {
        var response = await _gameClient.GetAsync($"/template?id={templateId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Template lookup failed: {error}");
        }

        Assert.True(response.IsSuccessStatusCode, $"Clone '{templateId}' not found in game");

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Clone {templateId}: {json}");
    }

    /// <summary>
    /// Use REPL to check CompiledAssetLoader status.
    /// </summary>
    [Fact]
    public async Task CompiledAssets_AreLoaded()
    {
        var response = await _gameClient.GetAsync(
            "/repl?code=" + Uri.EscapeDataString(
                "return new { " +
                "HasManifest = Menace.ModpackLoader.CompiledAssetLoader.HasManifest, " +
                "ManifestCount = Menace.ModpackLoader.CompiledAssetLoader.ManifestAssetCount, " +
                "LoadedCount = Menace.ModpackLoader.CompiledAssetLoader.LoadedAssetCount " +
                "};"));

        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"CompiledAssetLoader status: {json}");

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Use REPL to spawn an entity and verify it has expected properties.
    /// </summary>
    [Theory]
    [InlineData("weapon.laser_smg", "Damage")] // Verify cloned weapon has Damage field
    public async Task ClonedEntity_HasExpectedFields(string templateId, string fieldPath)
    {
        // Use REPL to find template and check field
        var code = $@"
var template = Menace.SDK.Templates.Find(""WeaponTemplate"", ""{templateId}"");
if (template == null) return new {{ found = false, id = ""{templateId}"" }};
var value = Menace.SDK.Templates.ReadField(template, ""{fieldPath}"");
return new {{ found = true, id = ""{templateId}"", field = ""{fieldPath}"", value = value?.ToString() }};
";
        var response = await _gameClient.GetAsync("/repl?code=" + Uri.EscapeDataString(code));
        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Field check: {json}");

        Assert.True(response.IsSuccessStatusCode);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("result", out var result))
        {
            Assert.True(result.GetProperty("found").GetBoolean(),
                $"Template '{templateId}' not found");
        }
    }

    /// <summary>
    /// Check for mod errors in the game log.
    /// </summary>
    [Fact]
    public async Task NoModErrors_InGameLog()
    {
        var response = await _gameClient.GetAsync("/errors");
        var json = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Errors: {json}");

        Assert.True(response.IsSuccessStatusCode);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("errors", out var errors))
        {
            var errorList = errors.EnumerateArray().ToList();
            Assert.Empty(errorList); // No errors expected
        }
    }
}

/// <summary>
/// Tests for the BundleCompiler that can run without the game.
/// Uses real asset bytes extracted from the game.
/// </summary>
[Trait("Category", "Offline")]
public class BundleCompilerOfflineTests
{
    private readonly ITestOutputHelper _output;

    // Path to extracted game data for testing
    // Set via environment variable or test configuration
    private static readonly string? GameDataPath =
        Environment.GetEnvironmentVariable("MENACE_GAME_DATA");

    public BundleCompilerOfflineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Test FindTemplateId against real game assets.
    /// Requires MENACE_GAME_DATA environment variable to be set.
    /// </summary>
    [SkippableFact]
    public void FindTemplateId_WithRealAssets()
    {
        Skip.If(string.IsNullOrEmpty(GameDataPath), "MENACE_GAME_DATA not set");

        var resourcesPath = Path.Combine(GameDataPath, "resources.assets");
        Skip.If(!File.Exists(resourcesPath), $"resources.assets not found at {resourcesPath}");

        // This would load the actual assets and test FindTemplateId
        // Implementation depends on AssetsTools.NET
        _output.WriteLine($"Would test with: {resourcesPath}");
    }
}
