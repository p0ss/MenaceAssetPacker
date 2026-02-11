using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Security/trust tier for a modpack
/// </summary>
public enum SecurityStatus
{
    Unreviewed,
    SourceVerified,
    SourceWithWarnings,
    UnverifiedBinary
}

/// <summary>
/// Repository hosting platform type for update checking
/// </summary>
public enum RepositoryType
{
    None,
    GitHub,
    // TODO: Future adapters
    // Nexus,      // Requires user API key
    // GameBanana, // Public API available
    // ModDB,      // RSS feed only
    // Thunderstore,
    // Custom      // Generic version.json endpoint
}

/// <summary>
/// Modpack manifest v2. Replaces the legacy ModpackInfo model.
/// Supports all fields needed for the asset bundle pipeline:
/// metadata, load ordering, dependencies, code, patches, bundles, assets, and security.
/// </summary>
public class ModpackManifest
{
    // -- Schema version --
    public int ManifestVersion { get; set; } = 2;

    // -- Metadata --
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }

    // -- Ordering & dependencies --
    public int LoadOrder { get; set; } = 100;
    public List<string> Dependencies { get; set; } = new();

    // -- Code --
    public CodeManifest Code { get; set; } = new();

    // -- Data patches (template type → instance name → field → value) --
    public Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> Patches { get; set; } = new();

    // -- Asset bundles --
    public List<string> Bundles { get; set; } = new();

    // -- Legacy-format asset replacements --
    public Dictionary<string, string> Assets { get; set; } = new();

    // -- Security --
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SecurityStatus SecurityStatus { get; set; } = SecurityStatus.Unreviewed;

    // -- Repository / Updates --
    /// <summary>
    /// Repository type for update checking. Auto-detected from URL if not specified.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RepositoryType RepositoryType { get; set; } = RepositoryType.None;

    /// <summary>
    /// Repository URL (e.g., "https://github.com/user/repo").
    /// Used for update checking and linking to the mod's home page.
    /// </summary>
    public string? RepositoryUrl { get; set; }

    // -- Runtime only (not serialized) --
    [JsonIgnore]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Parsed dependencies (lazy, from the Dependencies string list)
    /// </summary>
    [JsonIgnore]
    public List<ModpackDependency> ParsedDependencies =>
        (Dependencies ?? new List<string>()).Select(d =>
        {
            ModpackDependency.TryParse(d, out var dep);
            return dep;
        }).Where(d => d != null).ToList()!;

    public bool HasCode => Code?.HasAnyCode ?? false;
    public bool HasPatches => Patches?.Count > 0;
    public bool HasBundles => Bundles?.Count > 0;
    public bool HasAssets => Assets?.Count > 0;

    // ---------------------------------------------------------------
    // Serialization
    // ---------------------------------------------------------------

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializeOptions);

    public static ModpackManifest FromJson(string json) =>
        JsonSerializer.Deserialize<ModpackManifest>(json, DeserializeOptions) ?? new ModpackManifest();

    // ---------------------------------------------------------------
    // V1 migration
    // ---------------------------------------------------------------

    /// <summary>
    /// Detect whether a JSON string is a v1 (legacy) manifest and migrate it.
    /// V1 manifests have no "manifestVersion" field or have "templates" instead of "patches".
    /// Returns null if no manifest file exists or it cannot be parsed.
    /// </summary>
    public static ModpackManifest? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        var node = JsonNode.Parse(json);
        if (node == null)
            return null;

        var obj = node.AsObject();

        var version = (int?)(obj["manifestVersion"] ?? obj["ManifestVersion"]) ?? 0;
        if (version >= 2)
        {
            var manifest = FromJson(json);
            manifest.Path = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            return manifest;
        }

        // V1 migration
        return MigrateFromLegacy(obj, System.IO.Path.GetDirectoryName(filePath) ?? string.Empty);
    }

    /// <summary>
    /// Migrate a v1 manifest JSON object to v2 ModpackManifest.
    /// </summary>
    private static ModpackManifest MigrateFromLegacy(JsonObject obj, string directory)
    {
        var manifest = new ModpackManifest
        {
            ManifestVersion = 2,
            Path = directory,
            Name = GetString(obj, "Name", "name") ?? System.IO.Path.GetFileName(directory),
            Author = GetString(obj, "Author", "author") ?? string.Empty,
            Description = GetString(obj, "Description", "description") ?? string.Empty,
            Version = GetString(obj, "Version", "version") ?? "1.0.0"
        };

        // Parse dates
        var created = GetString(obj, "CreatedDate", "createdDate");
        if (created != null && DateTime.TryParse(created, out var cd))
            manifest.CreatedDate = cd;

        var modified = GetString(obj, "ModifiedDate", "modifiedDate");
        if (modified != null && DateTime.TryParse(modified, out var md))
            manifest.ModifiedDate = md;

        // Migrate "templates" → "patches"
        var templates = (obj["templates"] ?? obj["Templates"]) as JsonObject;
        if (templates != null)
        {
            foreach (var templateKvp in templates)
            {
                if (templateKvp.Value is JsonObject instances)
                {
                    var patchInstances = new Dictionary<string, Dictionary<string, JsonElement>>();
                    foreach (var instKvp in instances)
                    {
                        if (instKvp.Value != null)
                        {
                            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                                instKvp.Value.ToJsonString()) ?? new();
                            patchInstances[instKvp.Key] = fields;
                        }
                    }
                    if (patchInstances.Count > 0)
                        manifest.Patches[templateKvp.Key] = patchInstances;
                }
            }
        }

        // Migrate assets
        var assets = (obj["assets"] ?? obj["Assets"]) as JsonObject;
        if (assets != null)
        {
            foreach (var kvp in assets)
            {
                if (kvp.Value != null)
                    manifest.Assets[kvp.Key] = kvp.Value.ToString();
            }
        }

        return manifest;
    }

    private static string? GetString(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            var val = obj[key];
            if (val != null)
                return val.ToString();
        }
        return null;
    }

    /// <summary>
    /// Save this manifest to modpack.json in the manifest's directory.
    /// </summary>
    public void SaveToFile()
    {
        if (string.IsNullOrEmpty(Path))
            throw new InvalidOperationException("Manifest path is not set");

        var filePath = System.IO.Path.Combine(Path, "modpack.json");
        ModifiedDate = DateTime.Now;
        File.WriteAllText(filePath, ToJson());
    }

    /// <summary>
    /// Save this manifest to a specific file path.
    /// </summary>
    public void SaveToFile(string filePath)
    {
        ModifiedDate = DateTime.Now;
        File.WriteAllText(filePath, ToJson());
    }
}
