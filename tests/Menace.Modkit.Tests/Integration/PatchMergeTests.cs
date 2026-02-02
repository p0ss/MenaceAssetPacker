using System.Collections.Generic;
using System.Text.Json;
using Menace.Modkit.Core.Bundles;
using Menace.Modkit.Tests.Helpers;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 5 core: verifies MergedPatchSet.MergePatchSets() last-wins semantics.
/// This is the critical transform that feeds both the bundle compiler and runtime JSON.
/// </summary>
public class PatchMergeTests
{
    [Fact]
    public void MergePatchSets_SingleSet_PassThrough()
    {
        var patches = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 100 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { patches });

        var fields = merged.GetInstancePatch("UnitTemplate", "Soldier");
        Assert.NotNull(fields);
        Assert.Equal(100, fields!["health"].GetInt32());
    }

    [Fact]
    public void MergePatchSets_TwoSets_LastWins()
    {
        var set1 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 100 }
            }
        }
        """);

        var set2 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 200 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { set1, set2 });

        var fields = merged.GetInstancePatch("UnitTemplate", "Soldier");
        Assert.NotNull(fields);
        Assert.Equal(200, fields!["health"].GetInt32());
    }

    [Fact]
    public void MergePatchSets_DisjointFields_BothPreserved()
    {
        var set1 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 100 }
            }
        }
        """);

        var set2 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "speed": 5 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { set1, set2 });

        var fields = merged.GetInstancePatch("UnitTemplate", "Soldier");
        Assert.NotNull(fields);
        Assert.Equal(100, fields!["health"].GetInt32());
        Assert.Equal(5, fields["speed"].GetInt32());
    }

    [Fact]
    public void MergePatchSets_DisjointTemplateTypes_BothPreserved()
    {
        var set1 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 100 }
            }
        }
        """);

        var set2 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "BuildingTemplate": {
                "Barracks": { "buildTime": 30 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { set1, set2 });

        Assert.Contains("UnitTemplate", merged.GetTemplateTypes());
        Assert.Contains("BuildingTemplate", merged.GetTemplateTypes());
        Assert.NotNull(merged.GetInstancePatch("UnitTemplate", "Soldier"));
        Assert.NotNull(merged.GetInstancePatch("BuildingTemplate", "Barracks"));
    }

    [Fact]
    public void MergePatchSets_DisjointInstances_BothPreserved()
    {
        var set1 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 100 }
            }
        }
        """);

        var set2 = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Archer": { "range": 8 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { set1, set2 });

        Assert.NotNull(merged.GetInstancePatch("UnitTemplate", "Soldier"));
        Assert.NotNull(merged.GetInstancePatch("UnitTemplate", "Archer"));
        Assert.Equal(100, merged.GetInstancePatch("UnitTemplate", "Soldier")!["health"].GetInt32());
        Assert.Equal(8, merged.GetInstancePatch("UnitTemplate", "Archer")!["range"].GetInt32());
    }

    [Fact]
    public void MergePatchSets_ThreeSets_FinalWins()
    {
        var set1 = ManifestFixtures.CreatePatchSetFromJson("""{ "T": { "I": { "f": 1 } } }""");
        var set2 = ManifestFixtures.CreatePatchSetFromJson("""{ "T": { "I": { "f": 2 } } }""");
        var set3 = ManifestFixtures.CreatePatchSetFromJson("""{ "T": { "I": { "f": 3 } } }""");

        var merged = MergedPatchSet.MergePatchSets(new[] { set1, set2, set3 });

        Assert.Equal(3, merged.GetInstancePatch("T", "I")!["f"].GetInt32());
    }

    [Fact]
    public void MergePatchSets_EmptyInput_ReturnsEmpty()
    {
        var merged = MergedPatchSet.MergePatchSets(
            new List<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>());

        Assert.Empty(merged.Patches);
    }

    [Fact]
    public void MergePatchSets_MixedValueTypes_Preserved()
    {
        var patches = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "TypeA": {
                "Inst": {
                    "intVal": 42,
                    "floatVal": 2.718,
                    "strVal": "text",
                    "boolVal": true
                }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { patches });

        var fields = merged.GetInstancePatch("TypeA", "Inst")!;
        Assert.Equal(42, fields["intVal"].GetInt32());
        Assert.Equal(2.718, fields["floatVal"].GetDouble(), 0.001);
        Assert.Equal("text", fields["strVal"].GetString());
        Assert.True(fields["boolVal"].GetBoolean());
    }

    [Fact]
    public void GetInstancePatch_Existing_ReturnsFields()
    {
        var patches = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 50 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { patches });

        var fields = merged.GetInstancePatch("UnitTemplate", "Soldier");
        Assert.NotNull(fields);
        Assert.Single(fields!);
        Assert.Equal(50, fields["health"].GetInt32());
    }

    [Fact]
    public void GetInstancePatch_NonExistent_ReturnsNull()
    {
        var patches = ManifestFixtures.CreatePatchSetFromJson("""
        {
            "UnitTemplate": {
                "Soldier": { "health": 50 }
            }
        }
        """);

        var merged = MergedPatchSet.MergePatchSets(new[] { patches });

        Assert.Null(merged.GetInstancePatch("UnitTemplate", "NonExistent"));
        Assert.Null(merged.GetInstancePatch("NonExistentType", "Soldier"));
    }
}
