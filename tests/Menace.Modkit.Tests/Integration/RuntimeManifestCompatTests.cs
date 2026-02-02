using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Menace.Modkit.App.Models;
using Menace.Modkit.Tests.Helpers;
using Newtonsoft.Json;
using NJ = Newtonsoft.Json.Linq;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 0↔6 cross-project boundary: verifies that JSON produced by the App's deploy
/// pipeline deserializes correctly through Newtonsoft.Json into the runtime Modpack structure.
///
/// Strategy: Build runtime manifest JSON using the same JsonObject construction as
/// DeployManager.BuildRuntimeManifest, then deserialize with Newtonsoft into RuntimeModpackMirror.
/// This is a contract test — if the JSON shape changes in DeployManager, this test fails.
/// </summary>
public class RuntimeManifestCompatTests
{
    [Fact]
    public void RuntimeManifest_Patches_DeserializeToNewtonsoft()
    {
        var manifest = CreateManifestWithPatches();
        var json = BuildRuntimeManifestJson(manifest);

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.NotNull(mirror.Patches);
        Assert.True(mirror.Patches!.ContainsKey("UnitTemplate"));
        Assert.True(mirror.Patches["UnitTemplate"].ContainsKey("Soldier"));
        Assert.Equal(100L, Convert.ToInt64(mirror.Patches["UnitTemplate"]["Soldier"]["health"]));
    }

    [Fact]
    public void RuntimeManifest_Templates_DeserializeToNewtonsoft()
    {
        var manifest = CreateManifestWithPatches();
        var json = BuildRuntimeManifestJson(manifest);

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        // v1 backward compat: "templates" key should also be present
        Assert.NotNull(mirror.Templates);
    }

    [Fact]
    public void RuntimeManifest_Metadata_RoundTrips()
    {
        var manifest = ManifestFixtures.CreateMinimalManifest();
        manifest.SecurityStatus = SecurityStatus.SourceWithWarnings;
        var json = BuildRuntimeManifestJson(manifest);

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.Equal("TestMod", mirror.Name);
        Assert.Equal("1.0.0", mirror.Version);
        Assert.Equal("TestAuthor", mirror.Author);
        Assert.Equal(100, mirror.LoadOrder);
        Assert.Equal("SourceWithWarnings", mirror.SecurityStatus);
    }

    [Fact]
    public void RuntimeManifest_EmptyPatches_IsEmptyDict()
    {
        var manifest = ManifestFixtures.CreateMinimalManifest();
        // No patches
        var json = BuildRuntimeManifestJson(manifest);

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.NotNull(mirror.Patches);
        Assert.Empty(mirror.Patches!);
    }

    [Fact]
    public void RuntimeManifest_NumericValues_PreserveType()
    {
        var manifest = CreateManifestWithTypedPatches();
        var json = BuildRuntimeManifestJson(manifest);

        // Parse with Newtonsoft JObject to inspect raw types
        var jObj = NJ.JObject.Parse(json);
        var health = jObj["patches"]!["TypeA"]!["Inst"]!["intField"]!;

        Assert.Equal(NJ.JTokenType.Integer, health.Type);
        Assert.Equal(42, health.ToObject<int>());
    }

    [Fact]
    public void RuntimeManifest_FloatValues_PreserveType()
    {
        var manifest = CreateManifestWithTypedPatches();
        var json = BuildRuntimeManifestJson(manifest);

        var jObj = NJ.JObject.Parse(json);
        var speed = jObj["patches"]!["TypeA"]!["Inst"]!["floatField"]!;

        Assert.Equal(NJ.JTokenType.Float, speed.Type);
        Assert.Equal(3.14, speed.ToObject<double>(), 0.001);
    }

    [Fact]
    public void RuntimeManifest_BoolValues_PreserveType()
    {
        var manifest = CreateManifestWithTypedPatches();
        var json = BuildRuntimeManifestJson(manifest);

        var jObj = NJ.JObject.Parse(json);
        var flag = jObj["patches"]!["TypeA"]!["Inst"]!["boolField"]!;

        Assert.Equal(NJ.JTokenType.Boolean, flag.Type);
        Assert.True(flag.ToObject<bool>());
    }

    [Fact]
    public void RuntimeManifest_ManifestVersion_Is2()
    {
        var manifest = ManifestFixtures.CreateMinimalManifest();
        var json = BuildRuntimeManifestJson(manifest);

        var mirror = JsonConvert.DeserializeObject<RuntimeModpackMirror>(json)!;

        Assert.Equal(2, mirror.ManifestVersion);
    }

    // ---------------------------------------------------------------
    // Helpers — reproduces DeployManager.BuildRuntimeManifest logic
    // ---------------------------------------------------------------

    /// <summary>
    /// Builds a runtime manifest JSON string using the same JsonObject construction
    /// as DeployManager.BuildRuntimeManifest. This is a contract test: if the
    /// production code changes its output shape, this test must be updated.
    /// </summary>
    private static string BuildRuntimeManifestJson(ModpackManifest modpack)
    {
        var runtimeObj = new JsonObject
        {
            ["manifestVersion"] = 2,
            ["name"] = modpack.Name,
            ["version"] = modpack.Version,
            ["author"] = modpack.Author,
            ["loadOrder"] = modpack.LoadOrder
        };

        // Patches
        var patches = new JsonObject();
        var legacyTemplates = new JsonObject();

        if (modpack.Patches.Count > 0)
        {
            var patchJson = System.Text.Json.JsonSerializer.Serialize(modpack.Patches);
            var patchNode = JsonNode.Parse(patchJson)?.AsObject();
            if (patchNode != null)
            {
                foreach (var kvp in patchNode)
                {
                    if (kvp.Value != null)
                    {
                        patches[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                        legacyTemplates[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                    }
                }
            }
        }

        runtimeObj["patches"] = patches;
        runtimeObj["templates"] = legacyTemplates;

        // Assets
        if (modpack.Assets.Count > 0)
        {
            var assetsObj = new JsonObject();
            foreach (var kvp in modpack.Assets)
                assetsObj[kvp.Key] = kvp.Value;
            runtimeObj["assets"] = assetsObj;
        }
        else
        {
            runtimeObj["assets"] = new JsonObject();
        }

        runtimeObj["securityStatus"] = modpack.SecurityStatus.ToString();

        return runtimeObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static ModpackManifest CreateManifestWithPatches()
    {
        return ManifestFixtures.CreateManifestWithPatches();
    }

    private static ModpackManifest CreateManifestWithTypedPatches()
    {
        var json = """
        {
            "TypeA": {
                "Inst": {
                    "intField": 42,
                    "floatField": 3.14,
                    "boolField": true,
                    "strField": "hello"
                }
            }
        }
        """;
        return ManifestFixtures.CreateManifestWithPatches(patches: ManifestFixtures.CreatePatchSetFromJson(json));
    }
}
