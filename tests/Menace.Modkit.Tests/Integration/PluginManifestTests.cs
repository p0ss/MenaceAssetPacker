using Menace.Modkit.Tests.Helpers;
using Newtonsoft.Json;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Verifies that code-only modpack manifests (with prebuiltDlls) round-trip
/// correctly through Newtonsoft.Json deserialization, matching the runtime path.
/// </summary>
public class PluginManifestTests
{
    private const string SourceCodeManifestJson = """
        {
            "manifestVersion": 2,
            "name": "BalanceMod",
            "version": "1.0.0",
            "author": "Menace Modkit",
            "description": "Balance tweaks",
            "loadOrder": 50,
            "patches": {},
            "assets": {},
            "code": {
                "sources": ["src/BalanceModPlugin.cs"],
                "references": ["MelonLoader", "0Harmony", "Il2CppInterop.Runtime", "Menace.ModpackLoader"],
                "prebuiltDlls": []
            },
            "securityStatus": "SourceVerified"
        }
        """;

    [Fact]
    public void SourceCodeManifest_Deserializes_WithSources()
    {
        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(SourceCodeManifestJson)!;

        Assert.NotNull(mirror.Code);
        Assert.NotNull(mirror.Code!.Sources);
        Assert.Single(mirror.Code.Sources!);
        Assert.Equal("src/BalanceModPlugin.cs", mirror.Code.Sources![0]);
    }

    [Fact]
    public void SourceCodeManifest_Deserializes_WithReferences()
    {
        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(SourceCodeManifestJson)!;

        Assert.NotNull(mirror.Code);
        Assert.NotNull(mirror.Code!.References);
        Assert.Equal(4, mirror.Code.References!.Count);
        Assert.Contains("Menace.ModpackLoader", mirror.Code.References);
    }

    [Fact]
    public void SourceCodeManifest_HasCode_ReturnsTrue()
    {
        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(SourceCodeManifestJson)!;

        Assert.True(mirror.HasCode);
    }

    [Fact]
    public void SourceCodeManifest_Metadata_RoundTrips()
    {
        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(SourceCodeManifestJson)!;

        Assert.Equal(2, mirror.ManifestVersion);
        Assert.Equal("BalanceMod", mirror.Name);
        Assert.Equal("1.0.0", mirror.Version);
        Assert.Equal("Menace Modkit", mirror.Author);
        Assert.Equal(50, mirror.LoadOrder);
        Assert.Equal("SourceVerified", mirror.SecurityStatus);
    }

    [Fact]
    public void ManifestWithoutCode_HasCode_ReturnsFalse()
    {
        var json = """
            {
                "manifestVersion": 2,
                "name": "DataOnlyMod",
                "version": "1.0.0",
                "patches": {},
                "assets": {}
            }
            """;

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.False(mirror.HasCode);
    }

    [Fact]
    public void ManifestWithEmptyCode_HasCode_ReturnsFalse()
    {
        var json = """
            {
                "manifestVersion": 2,
                "name": "EmptyCodeMod",
                "version": "1.0.0",
                "code": {
                    "sources": [],
                    "references": [],
                    "prebuiltDlls": []
                }
            }
            """;

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.False(mirror.HasCode);
    }
}
