using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Loads extracted game data templates from JSON files
/// </summary>
public class DataTemplateLoader
{
    private readonly string _extractedDataPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataTemplateLoader(string extractedDataPath)
    {
        _extractedDataPath = extractedDataPath;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Get all available template types (JSON files in the extracted data directory)
    /// </summary>
    public IEnumerable<string> GetTemplateTypes()
    {
        if (!Directory.Exists(_extractedDataPath))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(_extractedDataPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name)!;
    }

    /// <summary>
    /// Load weapon templates from WeaponTemplate.json
    /// </summary>
    public List<WeaponTemplate> LoadWeapons()
    {
        return LoadTemplates<WeaponTemplate>("WeaponTemplate");
    }

    /// <summary>
    /// Load any template type generically
    /// </summary>
    public List<T> LoadTemplates<T>(string templateType) where T : DataTemplate
    {
        var filePath = Path.Combine(_extractedDataPath, $"{templateType}.json");
        if (!File.Exists(filePath))
            return new List<T>();

        try
        {
            var json = File.ReadAllText(filePath);
            var templates = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
            return templates ?? new List<T>();
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Error loading {templateType}: {ex.Message}");
            return new List<T>();
        }
    }

    /// <summary>
    /// Load templates as a dynamic collection (for types we don't have specific classes for)
    /// </summary>
    public List<DataTemplate> LoadTemplatesGeneric(string templateType)
    {
        var filePath = Path.Combine(_extractedDataPath, $"{templateType}.json");
        if (!File.Exists(filePath))
            return new List<DataTemplate>();

        try
        {
            var json = File.ReadAllText(filePath);

            // Parse as JsonDocument to preserve all properties
            using var document = JsonDocument.Parse(json);
            var templates = new List<DataTemplate>();

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    // Extract name property for the tree structure
                    var name = element.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? string.Empty
                        : string.Empty;

                    // Create a dynamic template that stores the full JSON
                    var template = new DynamicDataTemplate(name, element.Clone());
                    templates.Add(template);
                }
            }

            return templates;
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Error loading {templateType}: {ex.Message}");
            return new List<DataTemplate>();
        }
    }

    /// <summary>
    /// Save modified templates back to JSON
    /// </summary>
    public void SaveTemplates<T>(string templateType, List<T> templates) where T : DataTemplate
    {
        var filePath = Path.Combine(_extractedDataPath, $"{templateType}.json");

        try
        {
            var json = JsonSerializer.Serialize(templates, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            });

            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save {templateType}: {ex.Message}", ex);
        }
    }
}
