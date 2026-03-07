using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Loads field descriptions from knowledge base files and provides lookup by type and field name.
/// Supports both EventHandler types (eventhandler_knowledge.json) and template types (template_knowledge.json).
/// </summary>
public class FieldDescriptionService
{
    // handlerType -> fieldName -> FieldDescription
    private readonly Dictionary<string, Dictionary<string, FieldDescription>> _handlers = new(StringComparer.Ordinal);

    // templateType -> fieldName -> FieldDescription
    private readonly Dictionary<string, Dictionary<string, FieldDescription>> _templates = new(StringComparer.Ordinal);

    // embeddedClass -> fieldName -> FieldDescription (for non-handler embedded classes)
    private readonly Dictionary<string, Dictionary<string, FieldDescription>> _embeddedClasses = new(StringComparer.Ordinal);

    public class FieldDescription
    {
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Offset { get; set; }
        public double Confidence { get; set; }
        public string? Source { get; set; }
        public string? Note { get; set; }
        public Dictionary<string, string>? EnumValues { get; set; }
    }

    /// <summary>
    /// Load the EventHandler knowledge base from the given JSON file path.
    /// </summary>
    public void LoadKnowledgeBase(string jsonPath)
    {
        _handlers.Clear();

        if (!File.Exists(jsonPath))
        {
            ModkitLog.Info($"[FieldDescriptionService] Knowledge base not found at {jsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("handlers", out var handlers))
            {
                ModkitLog.Warn("[FieldDescriptionService] No 'handlers' section in knowledge base");
                return;
            }

            foreach (var handlerProp in handlers.EnumerateObject())
            {
                var handlerName = handlerProp.Name;
                var fieldDict = ParseFieldDescriptions(handlerProp.Value);
                _handlers[handlerName] = fieldDict;
            }

            ModkitLog.Info($"[FieldDescriptionService] Loaded descriptions for {_handlers.Count} handler types");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[FieldDescriptionService] Failed to load knowledge base: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the template knowledge base from the given JSON file path.
    /// Expected format: { "templates": { "SkillTemplate": { "Range": { "description": "...", ... } } } }
    /// </summary>
    public void LoadTemplateKnowledgeBase(string jsonPath)
    {
        _templates.Clear();
        _embeddedClasses.Clear();

        if (!File.Exists(jsonPath))
        {
            ModkitLog.Info($"[FieldDescriptionService] Template knowledge base not found at {jsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            // Load template descriptions
            if (doc.RootElement.TryGetProperty("templates", out var templates))
            {
                foreach (var templateProp in templates.EnumerateObject())
                {
                    var templateName = templateProp.Name;
                    var fieldDict = ParseFieldDescriptions(templateProp.Value);
                    _templates[templateName] = fieldDict;
                }
                ModkitLog.Info($"[FieldDescriptionService] Loaded descriptions for {_templates.Count} template types");
            }

            // Load embedded class descriptions (non-handler embedded types like Army, ArmyEntry)
            if (doc.RootElement.TryGetProperty("embedded_classes", out var embedded))
            {
                foreach (var classProp in embedded.EnumerateObject())
                {
                    var className = classProp.Name;
                    var fieldDict = ParseFieldDescriptions(classProp.Value);
                    _embeddedClasses[className] = fieldDict;
                }
                ModkitLog.Info($"[FieldDescriptionService] Loaded descriptions for {_embeddedClasses.Count} embedded classes");
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[FieldDescriptionService] Failed to load template knowledge base: {ex.Message}");
        }
    }

    private Dictionary<string, FieldDescription> ParseFieldDescriptions(JsonElement typeElement)
    {
        var fieldDict = new Dictionary<string, FieldDescription>(StringComparer.Ordinal);

        foreach (var fieldProp in typeElement.EnumerateObject())
        {
            var fieldName = fieldProp.Name;
            var fieldData = fieldProp.Value;

            var desc = new FieldDescription
            {
                Description = fieldData.TryGetProperty("description", out var d) ? d.GetString() : null,
                Type = fieldData.TryGetProperty("type", out var t) ? t.GetString() : null,
                Offset = fieldData.TryGetProperty("offset", out var o) ? o.GetString() : null,
                Confidence = fieldData.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0,
                Source = fieldData.TryGetProperty("source", out var s) ? s.GetString() : null,
                Note = fieldData.TryGetProperty("note", out var n) ? n.GetString() : null
            };

            // Parse enum values if present
            if (fieldData.TryGetProperty("enum_values", out var enumValues))
            {
                desc.EnumValues = new Dictionary<string, string>();
                foreach (var ev in enumValues.EnumerateObject())
                {
                    desc.EnumValues[ev.Name] = ev.Value.GetString() ?? "";
                }
            }

            fieldDict[fieldName] = desc;
        }

        return fieldDict;
    }

    /// <summary>
    /// Get the full field description object for a handler type and field name.
    /// </summary>
    public FieldDescription? GetDescription(string handlerType, string fieldName)
    {
        if (_handlers.TryGetValue(handlerType, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var desc))
                return desc;
        }
        return null;
    }

    /// <summary>
    /// Get the full field description object for a template type and field name.
    /// </summary>
    public FieldDescription? GetTemplateDescription(string templateType, string fieldName)
    {
        if (_templates.TryGetValue(templateType, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var desc))
                return desc;
        }
        return null;
    }

    /// <summary>
    /// Get the full field description object for an embedded class and field name.
    /// </summary>
    public FieldDescription? GetEmbeddedClassDescription(string className, string fieldName)
    {
        if (_embeddedClasses.TryGetValue(className, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var desc))
                return desc;
        }
        return null;
    }

    /// <summary>
    /// Get formatted description text for display in tooltips.
    /// Includes description, enum values (if present), type info, and verification status.
    /// </summary>
    public string? GetDescriptionText(string handlerType, string fieldName)
    {
        var desc = GetDescription(handlerType, fieldName);
        return FormatDescriptionText(desc);
    }

    /// <summary>
    /// Get formatted description text for a template field.
    /// </summary>
    public string? GetTemplateDescriptionText(string templateType, string fieldName)
    {
        var desc = GetTemplateDescription(templateType, fieldName);
        return FormatDescriptionText(desc);
    }

    /// <summary>
    /// Get formatted description text for an embedded class field.
    /// </summary>
    public string? GetEmbeddedClassDescriptionText(string className, string fieldName)
    {
        var desc = GetEmbeddedClassDescription(className, fieldName);
        return FormatDescriptionText(desc);
    }

    private string? FormatDescriptionText(FieldDescription? desc)
    {
        if (desc == null || string.IsNullOrEmpty(desc.Description))
            return null;

        var text = desc.Description;

        // Add note if present
        if (!string.IsNullOrEmpty(desc.Note))
        {
            text += $"\n\nNote: {desc.Note}";
        }

        // Add enum values if present
        if (desc.EnumValues != null && desc.EnumValues.Count > 0)
        {
            text += "\n\nValues:";
            foreach (var kvp in desc.EnumValues)
            {
                text += $"\n  {kvp.Key} = {kvp.Value}";
            }
        }

        // Add type info
        if (!string.IsNullOrEmpty(desc.Type))
        {
            text += $"\n\nType: {desc.Type}";
        }

        // Add verification status
        if (desc.Confidence >= 1.0 || desc.Source == "ghidra_verified")
        {
            text += "\n[Verified]";
        }
        else if (desc.Confidence >= 0.9)
        {
            text += "\n[High confidence]";
        }

        return text;
    }

    /// <summary>
    /// Get statistics about loaded descriptions.
    /// </summary>
    public (int HandlerTypes, int HandlerFields, int TemplateTypes, int TemplateFields, int EmbeddedTypes, int EmbeddedFields) GetStats()
    {
        int handlerFields = 0;
        foreach (var h in _handlers.Values)
            handlerFields += h.Count;

        int templateFields = 0;
        foreach (var t in _templates.Values)
            templateFields += t.Count;

        int embeddedFields = 0;
        foreach (var e in _embeddedClasses.Values)
            embeddedFields += e.Count;

        return (_handlers.Count, handlerFields, _templates.Count, templateFields, _embeddedClasses.Count, embeddedFields);
    }
}
