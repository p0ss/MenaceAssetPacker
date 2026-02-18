using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Column preferences for a specific template type or asset folder.
/// </summary>
public sealed class ColumnPreferences
{
    /// <summary>
    /// List of visible column field names in display order.
    /// </summary>
    [JsonPropertyName("visibleColumns")]
    public List<string> VisibleColumns { get; set; } = new();

    /// <summary>
    /// Column widths (field name -> width string).
    /// </summary>
    [JsonPropertyName("columnWidths")]
    public Dictionary<string, string> ColumnWidths { get; set; } = new();
}

/// <summary>
/// Root structure for the column preferences file.
/// </summary>
internal sealed class ColumnPreferencesFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("preferences")]
    public Dictionary<string, ColumnPreferences> Preferences { get; set; } = new();
}

/// <summary>
/// Service for persisting column preferences per template type.
/// Stores preferences in ~/.menace-modkit/column-prefs.json
/// </summary>
public sealed class ColumnPreferenceService
{
    private readonly string _preferencesPath;
    private ColumnPreferencesFile? _cachedPreferences;

    public ColumnPreferenceService()
    {
        var modkitDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".menace-modkit");

        Directory.CreateDirectory(modkitDir);
        _preferencesPath = Path.Combine(modkitDir, "column-prefs.json");
    }

    /// <summary>
    /// Loads column preferences for a specific template type or category.
    /// </summary>
    /// <param name="templateType">The template type name or category key.</param>
    /// <returns>Column preferences, or null if none saved.</returns>
    public ColumnPreferences? LoadPreferences(string templateType)
    {
        try
        {
            EnsureLoaded();
            return _cachedPreferences?.Preferences.GetValueOrDefault(templateType);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to load column preferences for {templateType}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves column preferences for a specific template type or category.
    /// </summary>
    /// <param name="templateType">The template type name or category key.</param>
    /// <param name="preferences">The column preferences to save.</param>
    public void SavePreferences(string templateType, ColumnPreferences preferences)
    {
        try
        {
            EnsureLoaded();

            if (_cachedPreferences == null)
            {
                _cachedPreferences = new ColumnPreferencesFile();
            }

            _cachedPreferences.Preferences[templateType] = preferences;
            WriteToFile();
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to save column preferences for {templateType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes column preferences for a specific template type.
    /// </summary>
    /// <param name="templateType">The template type name or category key.</param>
    public void DeletePreferences(string templateType)
    {
        try
        {
            EnsureLoaded();

            if (_cachedPreferences?.Preferences.Remove(templateType) == true)
            {
                WriteToFile();
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to delete column preferences for {templateType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all saved template types that have preferences.
    /// </summary>
    public IEnumerable<string> GetSavedTemplateTypes()
    {
        EnsureLoaded();
        if (_cachedPreferences == null)
            return Array.Empty<string>();
        return _cachedPreferences.Preferences.Keys;
    }

    private void EnsureLoaded()
    {
        if (_cachedPreferences != null)
            return;

        if (!File.Exists(_preferencesPath))
        {
            _cachedPreferences = new ColumnPreferencesFile();
            return;
        }

        try
        {
            var json = File.ReadAllText(_preferencesPath);
            _cachedPreferences = JsonSerializer.Deserialize<ColumnPreferencesFile>(json)
                ?? new ColumnPreferencesFile();
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to parse column preferences file: {ex.Message}");
            _cachedPreferences = new ColumnPreferencesFile();
        }
    }

    private void WriteToFile()
    {
        if (_cachedPreferences == null)
            return;

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(_cachedPreferences, options);
            File.WriteAllText(_preferencesPath, json);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Failed to write column preferences file: {ex.Message}");
        }
    }
}
