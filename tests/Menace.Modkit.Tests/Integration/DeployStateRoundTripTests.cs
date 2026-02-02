using System;
using System.Collections.Generic;
using Menace.Modkit.App.Models;
using Menace.Modkit.Tests.Helpers;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 2 persistence: verifies DeployState.SaveTo()/LoadFrom() round-trip.
/// </summary>
public class DeployStateRoundTripTests
{
    [Fact]
    public void SaveTo_ThenLoadFrom_RoundTrips()
    {
        using var tmp = new TemporaryDirectory();
        var filePath = Path.Combine(tmp.Path, "deploy-state.json");

        var original = new DeployState
        {
            LastDeployTimestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            DeployedFiles = new List<string> { "ModA/modpack.json", "ModA/stats/Unit.json" },
            DeployedModpacks = new List<DeployedModpack>
            {
                new()
                {
                    Name = "ModA",
                    Version = "1.0.0",
                    ContentHash = "ABCDEF1234567890",
                    LoadOrder = 100,
                    SecurityStatus = SecurityStatus.SourceVerified
                },
                new()
                {
                    Name = "ModB",
                    Version = "2.0.0",
                    ContentHash = "0987654321FEDCBA",
                    LoadOrder = 200,
                    SecurityStatus = SecurityStatus.UnverifiedBinary
                }
            }
        };

        original.SaveTo(filePath);
        var loaded = DeployState.LoadFrom(filePath);

        Assert.Equal(original.DeployedModpacks.Count, loaded.DeployedModpacks.Count);
        Assert.Equal(original.DeployedFiles.Count, loaded.DeployedFiles.Count);

        Assert.Equal("ModA", loaded.DeployedModpacks[0].Name);
        Assert.Equal("1.0.0", loaded.DeployedModpacks[0].Version);
        Assert.Equal("ABCDEF1234567890", loaded.DeployedModpacks[0].ContentHash);
        Assert.Equal(100, loaded.DeployedModpacks[0].LoadOrder);
        Assert.Equal(SecurityStatus.SourceVerified, loaded.DeployedModpacks[0].SecurityStatus);

        Assert.Equal("ModB", loaded.DeployedModpacks[1].Name);
        Assert.Equal(SecurityStatus.UnverifiedBinary, loaded.DeployedModpacks[1].SecurityStatus);

        Assert.Equal(original.DeployedFiles, loaded.DeployedFiles);
    }

    [Fact]
    public void LoadFrom_NonexistentFile_ReturnsDefault()
    {
        using var tmp = new TemporaryDirectory();
        var filePath = Path.Combine(tmp.Path, "missing", "deploy-state.json");

        var loaded = DeployState.LoadFrom(filePath);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.DeployedModpacks);
        Assert.Empty(loaded.DeployedFiles);
    }

    [Fact]
    public void LoadFrom_CorruptJson_ReturnsDefault()
    {
        using var tmp = new TemporaryDirectory();
        var filePath = tmp.WriteFile("deploy-state.json", "this is not valid json {{{");

        var loaded = DeployState.LoadFrom(filePath);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.DeployedModpacks);
        Assert.Empty(loaded.DeployedFiles);
    }

    [Fact]
    public void SecurityStatus_SerializesAsString()
    {
        using var tmp = new TemporaryDirectory();
        var filePath = Path.Combine(tmp.Path, "deploy-state.json");

        var state = new DeployState
        {
            DeployedModpacks = new List<DeployedModpack>
            {
                new()
                {
                    Name = "Mod",
                    SecurityStatus = SecurityStatus.SourceWithWarnings
                }
            }
        };

        state.SaveTo(filePath);

        // Read raw JSON and verify enum is a string, not an integer
        var rawJson = File.ReadAllText(filePath);
        Assert.Contains("\"SourceWithWarnings\"", rawJson);
        Assert.DoesNotContain("\"2\"", rawJson); // enum ordinal should not appear

        // Also verify it round-trips
        var loaded = DeployState.LoadFrom(filePath);
        Assert.Equal(SecurityStatus.SourceWithWarnings, loaded.DeployedModpacks[0].SecurityStatus);
    }
}
