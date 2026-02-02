using System;
using System.Collections.Generic;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.Tests.Helpers;

/// <summary>
/// Factory methods for creating test manifests and fixture data.
/// </summary>
public static class ManifestFixtures
{
    /// <summary>
    /// Creates a minimal valid v2 manifest with required fields populated.
    /// </summary>
    public static ModpackManifest CreateMinimalManifest(string name = "TestMod", int loadOrder = 100)
    {
        return new ModpackManifest
        {
            ManifestVersion = 2,
            Name = name,
            Version = "1.0.0",
            Author = "TestAuthor",
            Description = "A test modpack",
            LoadOrder = loadOrder,
            CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            SecurityStatus = SecurityStatus.SourceVerified
        };
    }

    /// <summary>
    /// Creates a manifest with patches containing various value types.
    /// </summary>
    public static ModpackManifest CreateManifestWithPatches(
        string name = "PatchMod",
        int loadOrder = 100,
        Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>? patches = null)
    {
        var manifest = CreateMinimalManifest(name, loadOrder);

        if (patches != null)
        {
            manifest.Patches = patches;
        }
        else
        {
            manifest.Patches = CreateSamplePatches();
        }

        return manifest;
    }

    /// <summary>
    /// Creates sample patches with int, float, bool, and string values.
    /// </summary>
    public static Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> CreateSamplePatches()
    {
        var json = """
        {
            "UnitTemplate": {
                "Soldier": {
                    "health": 100,
                    "speed": 3.5,
                    "isFlying": false,
                    "displayName": "Infantry"
                }
            }
        }
        """;

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>(json)!;
    }

    /// <summary>
    /// Creates a patch set from a JSON string (deserializes into the nested dictionary structure).
    /// </summary>
    public static Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> CreatePatchSetFromJson(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>(json)!;
    }

    /// <summary>
    /// Returns a v1 legacy manifest JSON string (uses "templates" instead of "patches").
    /// </summary>
    public static string GetV1LegacyJson()
    {
        return """
        {
            "name": "LegacyMod",
            "author": "OldAuthor",
            "version": "0.9.0",
            "description": "A legacy v1 modpack",
            "templates": {
                "UnitTemplate": {
                    "Archer": {
                        "range": 5,
                        "damage": 12
                    }
                }
            }
        }
        """;
    }

    /// <summary>
    /// Returns a v1 legacy manifest JSON string that includes assets.
    /// </summary>
    public static string GetV1LegacyJsonWithAssets()
    {
        return """
        {
            "name": "LegacyAssetMod",
            "author": "OldAuthor",
            "version": "0.8.0",
            "templates": {
                "BuildingTemplate": {
                    "Barracks": {
                        "trainSpeed": 2
                    }
                }
            },
            "assets": {
                "sprites/unit.png": "custom_unit.png",
                "audio/hit.wav": "custom_hit.wav"
            }
        }
        """;
    }
}
