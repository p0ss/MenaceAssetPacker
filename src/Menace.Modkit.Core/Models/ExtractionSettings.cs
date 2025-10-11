using System.Text.Json.Serialization;

namespace Menace.Modkit.Core.Models;

/// <summary>
/// User-configurable extraction settings
/// </summary>
public class ExtractionSettings
{
    /// <summary>
    /// Automatically update extraction when game version changes
    /// </summary>
    public bool AutoUpdateOnGameChange { get; set; } = true;

    /// <summary>
    /// Keep full IL2CPP dump (35MB) instead of just templates (500KB)
    /// </summary>
    public bool KeepFullIL2CppDump { get; set; } = false;

    /// <summary>
    /// Asset ripper profile to use
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssetRipperProfileType AssetProfile { get; set; } = AssetRipperProfileType.Standard;

    /// <summary>
    /// Custom asset ripper profile (if AssetProfile is Custom)
    /// </summary>
    public AssetRipperProfile? CustomAssetProfile { get; set; }

    /// <summary>
    /// Cache extracted data and assets
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Show extraction progress notifications
    /// </summary>
    public bool ShowExtractionProgress { get; set; } = true;

    /// <summary>
    /// Get the active asset ripper profile
    /// </summary>
    public AssetRipperProfile GetActiveProfile()
    {
        return AssetProfile switch
        {
            AssetRipperProfileType.Essential => AssetRipperProfile.Profiles.Essential,
            AssetRipperProfileType.Standard => AssetRipperProfile.Profiles.Standard,
            AssetRipperProfileType.Complete => AssetRipperProfile.Profiles.Complete,
            AssetRipperProfileType.Custom => CustomAssetProfile ?? AssetRipperProfile.Profiles.Standard,
            _ => AssetRipperProfile.Profiles.Standard
        };
    }
}

/// <summary>
/// Predefined asset ripper profile types
/// </summary>
public enum AssetRipperProfileType
{
    /// <summary>
    /// Essential assets only (Sprites, Textures, Audio, Text) - Fastest
    /// </summary>
    Essential,

    /// <summary>
    /// Standard modding assets (Essential + Meshes, Shaders, VFX, etc.) - Recommended
    /// </summary>
    Standard,

    /// <summary>
    /// Everything including Unity internals - Slowest
    /// </summary>
    Complete,

    /// <summary>
    /// User-defined custom profile
    /// </summary>
    Custom
}
