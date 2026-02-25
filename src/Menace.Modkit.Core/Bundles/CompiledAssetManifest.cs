using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Manifest of all compiled assets written by BundleCompiler.
/// Used at runtime to enumerate and load assets via Resources.Load().
/// </summary>
public class CompiledAssetManifest
{
    public const string ManifestFileName = "asset-manifest.json";

    /// <summary>
    /// Version of the manifest format. Increment when structure changes.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// When the manifest was created.
    /// </summary>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// List of all compiled assets.
    /// </summary>
    public List<CompiledAssetEntry> Assets { get; set; } = new();

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

    public static CompiledAssetManifest? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CompiledAssetManifest>(json, DeserializeOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SaveToFile(string directory)
    {
        var path = Path.Combine(directory, ManifestFileName);
        File.WriteAllText(path, ToJson());
    }

    public static CompiledAssetManifest? LoadFromFile(string directory)
    {
        var path = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return FromJson(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// A single compiled asset entry in the manifest.
/// </summary>
public class CompiledAssetEntry
{
    /// <summary>
    /// Display name of the asset (e.g., "weapon.my_rifle").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unity type name (e.g., "Texture2D", "AudioClip", "Mesh", "Material", "GameObject", "MonoBehaviour").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Resource path for Resources.Load() (e.g., "data/templates/weapons/weapon.my_rifle").
    /// </summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Asset category for grouping (e.g., "clone", "texture", "audio", "model", "prefab", "sprite").
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssetCategory Category { get; set; }

    /// <summary>
    /// For clones: the source template name this was cloned from.
    /// </summary>
    public string? SourceTemplate { get; set; }

    /// <summary>
    /// For clones: the template type (e.g., "WeaponTemplate").
    /// </summary>
    public string? TemplateType { get; set; }
}

/// <summary>
/// Category of compiled asset.
/// </summary>
public enum AssetCategory
{
    Clone,
    Texture,
    Sprite,
    Audio,
    Mesh,
    Material,
    Prefab
}
