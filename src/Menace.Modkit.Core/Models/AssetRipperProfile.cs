using System.Collections.Generic;

namespace Menace.Modkit.Core.Models;

/// <summary>
/// Defines what assets to rip from the game
/// </summary>
public class AssetRipperProfile
{
    /// <summary>
    /// Profile name
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Asset types to include in /Assets/
    /// </summary>
    public List<string> IncludedAssetTypes { get; set; } = new()
    {
        "AudioClip",
        "Cubemap",
        "Font",
        "Mesh",
        "PrefabHierarchyObject",
        "resources",
        "Shader",
        "Sprite",
        "TextAsset",
        "Texture2D",
        "Texture3D",
        "TerrainData",
        "VisualEffectAsset"
    };

    /// <summary>
    /// Script namespaces to include (in /Scripts/Assembly-CSharp/)
    /// </summary>
    public List<string> IncludedScriptNamespaces { get; set; } = new()
    {
        "Menace"
    };

    /// <summary>
    /// Whether to rip assemblies (DLLs)
    /// </summary>
    public bool IncludeAssemblies { get; set; } = false;

    /// <summary>
    /// Whether to rip Unity engine scripts
    /// </summary>
    public bool IncludeUnityScripts { get; set; } = false;

    /// <summary>
    /// Whether to rip Unity config files
    /// </summary>
    public bool IncludeUnityConfig { get; set; } = false;

    /// <summary>
    /// Predefined profiles
    /// </summary>
    public static class Profiles
    {
        /// <summary>
        /// Essential modding assets only (fast)
        /// </summary>
        public static AssetRipperProfile Essential => new()
        {
            Name = "Essential",
            IncludedAssetTypes = new()
            {
                "Sprite",
                "Texture2D",
                "AudioClip",
                "TextAsset"
            },
            IncludedScriptNamespaces = new() { "Menace" },
            IncludeAssemblies = false,
            IncludeUnityScripts = false,
            IncludeUnityConfig = false
        };

        /// <summary>
        /// All useful modding assets (recommended)
        /// </summary>
        public static AssetRipperProfile Standard => new()
        {
            Name = "Standard",
            IncludedAssetTypes = new()
            {
                "AudioClip",
                "Cubemap",
                "Font",
                "Mesh",
                "PrefabHierarchyObject",
                "resources",
                "Shader",
                "Sprite",
                "TextAsset",
                "Texture2D",
                "Texture3D",
                "TerrainData",
                "VisualEffectAsset"
            },
            IncludedScriptNamespaces = new() { "Menace" },
            IncludeAssemblies = false,
            IncludeUnityScripts = false,
            IncludeUnityConfig = false
        };

        /// <summary>
        /// Everything including Unity internals (slow, advanced)
        /// </summary>
        public static AssetRipperProfile Complete => new()
        {
            Name = "Complete",
            IncludedAssetTypes = new() { "*" }, // All
            IncludedScriptNamespaces = new() { "*" }, // All
            IncludeAssemblies = true,
            IncludeUnityScripts = true,
            IncludeUnityConfig = true
        };
    }

    /// <summary>
    /// Check if an asset type should be included
    /// </summary>
    public bool ShouldIncludeAssetType(string assetType)
    {
        return IncludedAssetTypes.Contains("*") ||
               IncludedAssetTypes.Contains(assetType);
    }

    /// <summary>
    /// Check if a script namespace should be included
    /// </summary>
    public bool ShouldIncludeScript(string scriptPath)
    {
        if (IncludedScriptNamespaces.Contains("*"))
            return true;

        foreach (var ns in IncludedScriptNamespaces)
        {
            if (scriptPath.Contains($"/{ns}/", System.StringComparison.OrdinalIgnoreCase) ||
                scriptPath.Contains($"/{ns}.", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
