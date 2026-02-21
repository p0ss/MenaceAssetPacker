namespace Menace.Modkit.App.Models;

/// <summary>
/// Wraps an asset field value with metadata about the asset type,
/// resolved name, file path, and thumbnail support.
/// Used by the Stats Editor to render asset fields differently from primitives.
/// </summary>
public class AssetPropertyValue
{
    /// <summary>The property/field name (e.g., "IconEquipment")</summary>
    public string FieldName { get; set; } = "";

    /// <summary>The Unity asset type (e.g., "Sprite", "Texture2D", "Material", "Mesh")</summary>
    public string AssetType { get; set; } = "";

    /// <summary>The resolved asset name (e.g., "weapon_cannon_icon"), or null if unresolved</summary>
    public string? AssetName { get; set; }

    /// <summary>The original JSON value (could be string like "(Sprite)", null, or actual name)</summary>
    public object? RawValue { get; set; }

    /// <summary>Path to the asset file in AssetRipper output, if found</summary>
    public string? AssetFilePath { get; set; }

    /// <summary>Whether this asset type supports thumbnail preview (Sprite, Texture2D)</summary>
    public bool HasThumbnail => AssetFilePath != null && (AssetType == "Sprite" || AssetType == "Texture2D");

    /// <summary>Path to the PNG thumbnail for preview</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Whether the asset name was actually resolved (vs placeholder like "(Sprite)")</summary>
    public bool IsResolved => !string.IsNullOrEmpty(AssetName) && !IsPlaceholder(AssetName);

    /// <summary>Display string for the UI</summary>
    public string DisplayText
    {
        get
        {
            if (RawValue == null) return "null";
            if (IsResolved) return AssetName!;
            return "(unresolved)";
        }
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        // Matches patterns like "(Sprite)", "(Texture2D)", "(Material)", etc.
        return value.StartsWith("(") && value.EndsWith(")");
    }

    /// <summary>
    /// Create a deep copy of this AssetPropertyValue.
    /// </summary>
    public AssetPropertyValue Clone()
    {
        return new AssetPropertyValue
        {
            FieldName = FieldName,
            AssetType = AssetType,
            AssetName = AssetName,
            RawValue = RawValue,
            AssetFilePath = AssetFilePath,
            ThumbnailPath = ThumbnailPath
        };
    }
}
