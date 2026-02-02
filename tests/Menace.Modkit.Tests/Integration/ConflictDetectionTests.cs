using System.Collections.Generic;
using System.Text.Json;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.Tests.Helpers;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 1: verifies ConflictDetector.DetectFieldConflicts(), DetectDllConflicts(), DetectDependencyIssues().
/// </summary>
public class ConflictDetectionTests
{
    private readonly ConflictDetector _detector = new();

    [Fact]
    public void DetectFieldConflicts_SameField_ReturnsConflict()
    {
        using var tmp = new TemporaryDirectory();
        var modA = CreateModpackWithPatch(tmp, "ModA", 100, "UnitTemplate", "Soldier", "health", 100);
        var modB = CreateModpackWithPatch(tmp, "ModB", 200, "UnitTemplate", "Soldier", "health", 150);

        var conflicts = _detector.DetectFieldConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Single(conflicts);
        Assert.Equal("UnitTemplate", conflicts[0].TemplateType);
        Assert.Equal("Soldier", conflicts[0].InstanceName);
        Assert.Equal("health", conflicts[0].FieldName);
        Assert.Equal("ModB", conflicts[0].Winner); // last in load order wins
        Assert.Equal(2, conflicts[0].ConflictingMods.Count);
    }

    [Fact]
    public void DetectFieldConflicts_NoOverlap_ReturnsEmpty()
    {
        using var tmp = new TemporaryDirectory();
        var modA = CreateModpackWithPatch(tmp, "ModA", 100, "UnitTemplate", "Soldier", "health", 100);
        var modB = CreateModpackWithPatch(tmp, "ModB", 200, "UnitTemplate", "Archer", "range", 5);

        var conflicts = _detector.DetectFieldConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectFieldConflicts_WithStatsFiles_DetectsConflicts()
    {
        using var tmp = new TemporaryDirectory();

        // ModA has patches in stats/*.json file
        var modADir = tmp.CreateSubdirectory("ModA");
        var statsJson = """
        {
            "Soldier": {
                "health": 120
            }
        }
        """;
        var modAStatsDir = Path.Combine(modADir, "stats");
        Directory.CreateDirectory(modAStatsDir);
        File.WriteAllText(Path.Combine(modAStatsDir, "UnitTemplate.json"), statsJson);

        var modA = new ModpackManifest
        {
            Name = "ModA",
            LoadOrder = 100,
            Path = modADir
        };

        // ModB has the same field in manifest patches
        var modBDir = tmp.CreateSubdirectory("ModB");
        var modB = CreateModpackWithPatch(tmp, "ModB", 200, "UnitTemplate", "Soldier", "health", 200);
        modB.Path = modBDir;

        var conflicts = _detector.DetectFieldConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Single(conflicts);
        Assert.Equal("health", conflicts[0].FieldName);
    }

    [Fact]
    public void DetectFieldConflicts_WinnerIsLastByLoadOrder()
    {
        using var tmp = new TemporaryDirectory();
        var modA = CreateModpackWithPatch(tmp, "ModA", 10, "T", "I", "f", 1);
        var modB = CreateModpackWithPatch(tmp, "ModB", 20, "T", "I", "f", 2);
        var modC = CreateModpackWithPatch(tmp, "ModC", 30, "T", "I", "f", 3);

        var conflicts = _detector.DetectFieldConflicts(new List<ModpackManifest> { modA, modB, modC });

        Assert.Single(conflicts);
        Assert.Equal("ModC", conflicts[0].Winner);
        Assert.Equal(3, conflicts[0].ConflictingMods.Count);
        // Verify sorted by load order
        Assert.Equal("ModA", conflicts[0].ConflictingMods[0].ModpackName);
        Assert.Equal("ModB", conflicts[0].ConflictingMods[1].ModpackName);
        Assert.Equal("ModC", conflicts[0].ConflictingMods[2].ModpackName);
    }

    [Fact]
    public void DetectDllConflicts_SameAssemblyName_ReturnsConflict()
    {
        var modA = ManifestFixtures.CreateMinimalManifest("ModA");
        modA.Code.PrebuiltDlls = new List<string> { "libs/MyMod.dll" };

        var modB = ManifestFixtures.CreateMinimalManifest("ModB");
        modB.Code.PrebuiltDlls = new List<string> { "plugins/MyMod.dll" };

        var conflicts = _detector.DetectDllConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Single(conflicts);
        Assert.Equal("MyMod", conflicts[0].AssemblyName);
        Assert.Contains("ModA", conflicts[0].ModpackNames);
        Assert.Contains("ModB", conflicts[0].ModpackNames);
    }

    [Fact]
    public void DetectDllConflicts_NoOverlap_ReturnsEmpty()
    {
        var modA = ManifestFixtures.CreateMinimalManifest("ModA");
        modA.Code.PrebuiltDlls = new List<string> { "libs/Alpha.dll" };

        var modB = ManifestFixtures.CreateMinimalManifest("ModB");
        modB.Code.PrebuiltDlls = new List<string> { "libs/Beta.dll" };

        var conflicts = _detector.DetectDllConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectDllConflicts_CaseInsensitive()
    {
        var modA = ManifestFixtures.CreateMinimalManifest("ModA");
        modA.Code.PrebuiltDlls = new List<string> { "libs/MyMod.dll" };

        var modB = ManifestFixtures.CreateMinimalManifest("ModB");
        modB.Code.PrebuiltDlls = new List<string> { "libs/mymod.dll" };

        var conflicts = _detector.DetectDllConflicts(new List<ModpackManifest> { modA, modB });

        Assert.Single(conflicts);
    }

    [Fact]
    public void DetectDependencyIssues_Missing_ReturnsIssue()
    {
        var mod = ManifestFixtures.CreateMinimalManifest("MyMod");
        mod.Dependencies = new List<string> { "MissingDep >= 1.0" };

        var issues = _detector.DetectDependencyIssues(new List<ModpackManifest> { mod });

        Assert.Single(issues);
        Assert.Equal(DependencyIssueSeverity.Missing, issues[0].Severity);
        Assert.Equal("MyMod", issues[0].ModpackName);
    }

    [Fact]
    public void DetectDependencyIssues_IncompatibleVersion_ReturnsIssue()
    {
        var dep = ManifestFixtures.CreateMinimalManifest("BaseMod");
        dep.Version = "1.0.0";

        var mod = ManifestFixtures.CreateMinimalManifest("MyMod");
        mod.Dependencies = new List<string> { "BaseMod >= 2.0.0" };

        var issues = _detector.DetectDependencyIssues(new List<ModpackManifest> { dep, mod });

        Assert.Single(issues);
        Assert.Equal(DependencyIssueSeverity.IncompatibleVersion, issues[0].Severity);
    }

    [Fact]
    public void DetectDependencyIssues_Satisfied_ReturnsEmpty()
    {
        var dep = ManifestFixtures.CreateMinimalManifest("BaseMod");
        dep.Version = "3.0.0";

        var mod = ManifestFixtures.CreateMinimalManifest("MyMod");
        mod.Dependencies = new List<string> { "BaseMod >= 2.0.0" };

        var issues = _detector.DetectDependencyIssues(new List<ModpackManifest> { dep, mod });

        Assert.Empty(issues);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static ModpackManifest CreateModpackWithPatch(
        TemporaryDirectory tmp, string name, int loadOrder,
        string templateType, string instanceName, string fieldName, int value)
    {
        var json = $$"""
        {
            "{{templateType}}": {
                "{{instanceName}}": {
                    "{{fieldName}}": {{value}}
                }
            }
        }
        """;
        var patches = ManifestFixtures.CreatePatchSetFromJson(json);
        var dir = tmp.CreateSubdirectory(name);

        return new ModpackManifest
        {
            Name = name,
            LoadOrder = loadOrder,
            Path = dir,
            Patches = patches
        };
    }
}
