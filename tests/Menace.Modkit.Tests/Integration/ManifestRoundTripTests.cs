using System.Text.Json;
using Menace.Modkit.App.Models;
using Menace.Modkit.Tests.Helpers;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 0 foundation: verifies ModpackManifest serialization round-trips
/// and v1→v2 migration through LoadFromFile.
/// </summary>
public class ManifestRoundTripTests
{
    [Fact]
    public void ToJson_ThenFromJson_PreservesAllFields()
    {
        var original = ManifestFixtures.CreateManifestWithPatches();
        original.Dependencies = new List<string> { "BaseMod >= 1.0", "CoreLib" };
        original.Bundles = new List<string> { "units.bundle" };
        original.Assets = new Dictionary<string, string> { ["sprite.png"] = "custom.png" };
        original.Code = new CodeManifest
        {
            Sources = new List<string> { "src/Main.cs" },
            References = new List<string> { "MelonLoader" },
            PrebuiltDlls = new List<string> { "libs/Helper.dll" }
        };

        var json = original.ToJson();
        var restored = ModpackManifest.FromJson(json);

        Assert.Equal(original.ManifestVersion, restored.ManifestVersion);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.Author, restored.Author);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.LoadOrder, restored.LoadOrder);
        Assert.Equal(original.SecurityStatus, restored.SecurityStatus);
        Assert.Equal(original.Dependencies, restored.Dependencies);
        Assert.Equal(original.Bundles, restored.Bundles);
        Assert.Equal(original.Assets, restored.Assets);
        Assert.Equal(original.Code.Sources, restored.Code.Sources);
        Assert.Equal(original.Code.References, restored.Code.References);
        Assert.Equal(original.Code.PrebuiltDlls, restored.Code.PrebuiltDlls);

        // Verify patch structure survived
        Assert.Equal(original.Patches.Count, restored.Patches.Count);
        foreach (var (templateType, instances) in original.Patches)
        {
            Assert.True(restored.Patches.ContainsKey(templateType));
            foreach (var (instanceName, fields) in instances)
            {
                Assert.True(restored.Patches[templateType].ContainsKey(instanceName));
                foreach (var (fieldName, value) in fields)
                {
                    Assert.True(restored.Patches[templateType][instanceName].ContainsKey(fieldName));
                    Assert.Equal(value.ToString(), restored.Patches[templateType][instanceName][fieldName].ToString());
                }
            }
        }
    }

    [Fact]
    public void ToJson_UsesCamelCaseKeys()
    {
        var manifest = ManifestFixtures.CreateMinimalManifest();
        var json = manifest.ToJson();

        Assert.Contains("\"manifestVersion\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"loadOrder\"", json);
        Assert.Contains("\"securityStatus\"", json);
        // Should NOT contain PascalCase keys
        Assert.DoesNotContain("\"ManifestVersion\"", json);
        Assert.DoesNotContain("\"LoadOrder\"", json);
    }

    [Fact]
    public void FromJson_WithCamelCaseKeys_DeserializesCorrectly()
    {
        var json = """
        {
            "manifestVersion": 2,
            "name": "CamelTest",
            "version": "2.0.0",
            "author": "Auth",
            "loadOrder": 50,
            "securityStatus": "SourceVerified"
        }
        """;

        var manifest = ModpackManifest.FromJson(json);

        Assert.Equal(2, manifest.ManifestVersion);
        Assert.Equal("CamelTest", manifest.Name);
        Assert.Equal("2.0.0", manifest.Version);
        Assert.Equal(50, manifest.LoadOrder);
        Assert.Equal(SecurityStatus.SourceVerified, manifest.SecurityStatus);
    }

    [Fact]
    public void LoadFromFile_V2Manifest_LoadsDirectly()
    {
        using var tmp = new TemporaryDirectory();
        var manifest = ManifestFixtures.CreateManifestWithPatches();
        var json = manifest.ToJson();
        var filePath = tmp.WriteFile("modpack.json", json);

        var loaded = ModpackManifest.LoadFromFile(filePath);

        Assert.Equal(2, loaded.ManifestVersion);
        Assert.Equal(manifest.Name, loaded.Name);
        Assert.True(loaded.HasPatches);
    }

    [Fact]
    public void LoadFromFile_V1Manifest_MigratesToV2()
    {
        using var tmp = new TemporaryDirectory();
        var v1Json = ManifestFixtures.GetV1LegacyJson();
        var filePath = tmp.WriteFile("modpack.json", v1Json);

        var loaded = ModpackManifest.LoadFromFile(filePath);

        Assert.Equal(2, loaded.ManifestVersion);
        Assert.Equal("LegacyMod", loaded.Name);
        Assert.Equal("OldAuthor", loaded.Author);
        // "templates" key should have been migrated to Patches
        Assert.True(loaded.HasPatches);
        Assert.True(loaded.Patches.ContainsKey("UnitTemplate"));
        Assert.True(loaded.Patches["UnitTemplate"].ContainsKey("Archer"));
        Assert.Equal(5, loaded.Patches["UnitTemplate"]["Archer"]["range"].GetInt32());
        Assert.Equal(12, loaded.Patches["UnitTemplate"]["Archer"]["damage"].GetInt32());
    }

    [Fact]
    public void LoadFromFile_V1WithAssets_MigratesAssets()
    {
        using var tmp = new TemporaryDirectory();
        var v1Json = ManifestFixtures.GetV1LegacyJsonWithAssets();
        var filePath = tmp.WriteFile("modpack.json", v1Json);

        var loaded = ModpackManifest.LoadFromFile(filePath);

        Assert.Equal(2, loaded.ManifestVersion);
        Assert.True(loaded.HasAssets);
        Assert.Equal("custom_unit.png", loaded.Assets["sprites/unit.png"]);
        Assert.Equal("custom_hit.wav", loaded.Assets["audio/hit.wav"]);
        // Templates also migrated
        Assert.True(loaded.HasPatches);
        Assert.True(loaded.Patches.ContainsKey("BuildingTemplate"));
    }

    [Fact]
    public void LoadFromFile_MissingFile_ReturnsDefault()
    {
        using var tmp = new TemporaryDirectory();
        var filePath = Path.Combine(tmp.Path, "nonexistent", "modpack.json");

        var loaded = ModpackManifest.LoadFromFile(filePath);

        Assert.Equal(2, loaded.ManifestVersion);
        Assert.Equal(string.Empty, loaded.Name);
        Assert.False(loaded.HasPatches);
    }

    [Fact]
    public void SaveToFile_ThenLoadFromFile_RoundTrips()
    {
        using var tmp = new TemporaryDirectory();
        var original = ManifestFixtures.CreateManifestWithPatches();
        original.Path = tmp.Path;
        original.Dependencies = new List<string> { "Dep1 >= 2.0" };

        original.SaveToFile();
        var loaded = ModpackManifest.LoadFromFile(Path.Combine(tmp.Path, "modpack.json"));

        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(original.Author, loaded.Author);
        Assert.Equal(original.Dependencies, loaded.Dependencies);
        Assert.True(loaded.HasPatches);
    }

    [Fact]
    public void Patches_JsonElementValues_SurviveRoundTrip()
    {
        var json = """
        {
            "TypeA": {
                "Instance1": {
                    "intField": 42,
                    "floatField": 3.14,
                    "boolField": true,
                    "stringField": "hello"
                }
            }
        }
        """;

        var patches = ManifestFixtures.CreatePatchSetFromJson(json);
        var manifest = ManifestFixtures.CreateManifestWithPatches(patches: patches);

        var serialized = manifest.ToJson();
        var restored = ModpackManifest.FromJson(serialized);

        var fields = restored.Patches["TypeA"]["Instance1"];
        Assert.Equal(JsonValueKind.Number, fields["intField"].ValueKind);
        Assert.Equal(42, fields["intField"].GetInt32());
        Assert.Equal(JsonValueKind.Number, fields["floatField"].ValueKind);
        Assert.Equal(3.14, fields["floatField"].GetDouble(), 0.001);
        Assert.Equal(JsonValueKind.True, fields["boolField"].ValueKind);
        Assert.Equal(JsonValueKind.String, fields["stringField"].ValueKind);
        Assert.Equal("hello", fields["stringField"].GetString());
    }
}
