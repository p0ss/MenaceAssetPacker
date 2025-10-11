using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Base class for all game data templates extracted from MENACE
/// </summary>
public class DataTemplate
{
    /// <summary>
    /// Unique identifier (e.g., "mod_weapon.heavy.cannon_long")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown to users
    /// </summary>
    [JsonPropertyName("DisplayTitle")]
    public string? DisplayTitle { get; set; }

    /// <summary>
    /// Short display name
    /// </summary>
    [JsonPropertyName("DisplayShortName")]
    public string? DisplayShortName { get; set; }

    /// <summary>
    /// Full description text
    /// </summary>
    [JsonPropertyName("DisplayDescription")]
    public string? DisplayDescription { get; set; }

    /// <summary>
    /// Whether this template has an icon
    /// </summary>
    [JsonPropertyName("HasIcon")]
    public bool HasIcon { get; set; }

    /// <summary>
    /// Get the best available display name
    /// </summary>
    public string GetDisplayName() => DisplayTitle ?? DisplayShortName ?? Name;

    /// <summary>
    /// Parse the hierarchical name into parts (e.g., "commodity.civilian_supplies" -> ["commodity", "civilian_supplies"])
    /// </summary>
    public string[] GetNameParts() => Name.Split('.', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Dynamic template that preserves all JSON properties
/// </summary>
public class DynamicDataTemplate : DataTemplate
{
    private readonly JsonElement _jsonElement;

    public DynamicDataTemplate(string name, JsonElement jsonElement)
    {
        Name = name;
        _jsonElement = jsonElement;

        // Populate base properties if they exist
        if (_jsonElement.TryGetProperty("DisplayTitle", out var displayTitle))
            DisplayTitle = displayTitle.GetString();

        if (_jsonElement.TryGetProperty("DisplayShortName", out var displayShortName))
            DisplayShortName = displayShortName.GetString();

        if (_jsonElement.TryGetProperty("DisplayDescription", out var displayDescription))
            DisplayDescription = displayDescription.GetString();

        if (_jsonElement.TryGetProperty("HasIcon", out var hasIcon))
            HasIcon = hasIcon.GetBoolean();
    }

    /// <summary>
    /// Get the full JSON element with all properties
    /// </summary>
    public JsonElement GetJsonElement() => _jsonElement;
}
