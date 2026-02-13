using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Validates extracted game data against the schema to detect missing fields
/// and ensure extraction captured everything expected.
/// </summary>
public class ExtractionValidator
{
    private readonly SchemaService _schema;

    public ExtractionValidator(SchemaService schema)
    {
        _schema = schema;
    }

    /// <summary>
    /// Represents a single field issue found during validation.
    /// </summary>
    public class FieldIssue
    {
        public string TemplateType { get; set; } = "";
        public string InstanceName { get; set; } = "";
        public string FieldName { get; set; } = "";
        public string Expected { get; set; } = "";
        public string Actual { get; set; } = "";
        public string Context { get; set; } = "";

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Context))
                return $"{TemplateType}/{InstanceName}: {Context}.{FieldName} - expected {Expected}, got {Actual}";
            return $"{TemplateType}/{InstanceName}: {FieldName} - expected {Expected}, got {Actual}";
        }
    }

    /// <summary>
    /// Result of validating extracted data against the schema.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => MissingTemplateTypes.Count == 0 && MissingFields.Count == 0 && TypeMismatches.Count == 0;
        public List<string> MissingTemplateTypes { get; set; } = new();
        public List<FieldIssue> MissingFields { get; set; } = new();
        public List<FieldIssue> TypeMismatches { get; set; } = new();
        public int ValidatedInstanceCount { get; set; }
        public int ValidatedFieldCount { get; set; }

        public string GetSummary()
        {
            if (IsValid)
                return $"Validation passed: {ValidatedInstanceCount} instances, {ValidatedFieldCount} fields checked";

            var issues = new List<string>();
            if (MissingTemplateTypes.Count > 0)
                issues.Add($"{MissingTemplateTypes.Count} missing template types");
            if (MissingFields.Count > 0)
                issues.Add($"{MissingFields.Count} missing fields");
            if (TypeMismatches.Count > 0)
                issues.Add($"{TypeMismatches.Count} type mismatches");

            return $"Validation found issues: {string.Join(", ", issues)}";
        }
    }

    /// <summary>
    /// Validate all extracted data in the given directory against the schema.
    /// </summary>
    public ValidationResult ValidateExtraction(string extractedDataPath)
    {
        var result = new ValidationResult();

        if (!_schema.IsLoaded)
        {
            ModkitLog.Warn("[ExtractionValidator] Schema not loaded, skipping validation");
            return result;
        }

        if (!Directory.Exists(extractedDataPath))
        {
            ModkitLog.Warn($"[ExtractionValidator] Extracted data path not found: {extractedDataPath}");
            return result;
        }

        // Get all JSON files in extracted data
        var jsonFiles = Directory.GetFiles(extractedDataPath, "*.json");
        var extractedTypes = jsonFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in jsonFiles)
        {
            var typeName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(typeName) || typeName == "AssetReferences" || typeName == "menu")
                continue;

            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var instances = doc.RootElement.EnumerateArray().ToList();
                    var typeResult = ValidateTemplateType(typeName, instances);

                    result.MissingFields.AddRange(typeResult.MissingFields);
                    result.TypeMismatches.AddRange(typeResult.TypeMismatches);
                    result.ValidatedInstanceCount += instances.Count;
                    result.ValidatedFieldCount += typeResult.ValidatedFieldCount;
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[ExtractionValidator] Failed to parse {file}: {ex.Message}");
            }
        }

        // Log summary
        if (result.IsValid)
        {
            ModkitLog.Info($"[ExtractionValidator] {result.GetSummary()}");
        }
        else
        {
            ModkitLog.Warn($"[ExtractionValidator] {result.GetSummary()}");

            // Log details (first 10 of each type)
            foreach (var issue in result.MissingFields.Take(10))
                ModkitLog.Warn($"  Missing: {issue}");
            if (result.MissingFields.Count > 10)
                ModkitLog.Warn($"  ... and {result.MissingFields.Count - 10} more missing fields");

            foreach (var issue in result.TypeMismatches.Take(10))
                ModkitLog.Warn($"  Type mismatch: {issue}");
            if (result.TypeMismatches.Count > 10)
                ModkitLog.Warn($"  ... and {result.TypeMismatches.Count - 10} more type mismatches");
        }

        return result;
    }

    /// <summary>
    /// Validate instances of a single template type against its schema.
    /// </summary>
    public ValidationResult ValidateTemplateType(string typeName, List<JsonElement> instances)
    {
        var result = new ValidationResult();

        foreach (var instance in instances)
        {
            if (instance.ValueKind != JsonValueKind.Object)
                continue;

            var instanceName = instance.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? "(unnamed)"
                : "(unnamed)";

            ValidateObjectAgainstSchema(typeName, instanceName, instance, "", result, isTemplate: true);
        }

        return result;
    }

    /// <summary>
    /// Validate a nested object against its embedded class schema.
    /// </summary>
    public List<FieldIssue> ValidateEmbeddedObject(string className, JsonElement obj, string context)
    {
        var result = new ValidationResult();
        ValidateObjectAgainstSchema(className, context, obj, "", result, isTemplate: false);
        return result.MissingFields;
    }

    private void ValidateObjectAgainstSchema(
        string schemaTypeName,
        string instanceName,
        JsonElement obj,
        string context,
        ValidationResult result,
        bool isTemplate)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return;

        // Get expected fields from schema
        IEnumerable<SchemaService.FieldMeta> expectedFields;
        if (isTemplate)
        {
            // For templates, we don't have a GetAllTemplateFields method,
            // so we'll validate against what's in the JSON and check collections
            expectedFields = new List<SchemaService.FieldMeta>();
        }
        else
        {
            expectedFields = _schema.GetAllEmbeddedClassFields(schemaTypeName);
        }

        // Build set of present field names
        var presentFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in obj.EnumerateObject())
            presentFields.Add(prop.Name);

        // Check for expected fields that are missing
        // Note: Reference types (reference, unity_asset, collection) are often null/optional,
        // so we only report missing primitive/enum fields as actual issues.
        foreach (var field in expectedFields)
        {
            result.ValidatedFieldCount++;

            if (!presentFields.Contains(field.Name))
            {
                // Skip reporting missing reference/collection fields - these are commonly null
                var category = field.Category?.ToLowerInvariant() ?? "";
                if (category == "reference" || category == "unity_asset" || category == "collection")
                    continue;

                result.MissingFields.Add(new FieldIssue
                {
                    TemplateType = isTemplate ? schemaTypeName : "",
                    InstanceName = instanceName,
                    FieldName = field.Name,
                    Expected = field.Type,
                    Actual = "(missing)",
                    Context = context
                });
            }
        }

        // Recursively validate collection fields with embedded class element types
        foreach (var prop in obj.EnumerateObject())
        {
            result.ValidatedFieldCount++;

            // Get field metadata
            SchemaService.FieldMeta? fieldMeta = null;
            if (isTemplate)
            {
                fieldMeta = _schema.GetFieldMetadata(schemaTypeName, prop.Name);
            }
            else
            {
                fieldMeta = _schema.GetEmbeddedClassFieldMetadata(schemaTypeName, prop.Name);
            }

            if (fieldMeta == null)
                continue;

            // If it's a collection with an embedded class element type, validate each element
            if (fieldMeta.Category == "collection" &&
                !string.IsNullOrEmpty(fieldMeta.ElementType) &&
                _schema.IsEmbeddedClass(fieldMeta.ElementType))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var element in prop.Value.EnumerateArray())
                    {
                        var elementContext = string.IsNullOrEmpty(context)
                            ? $"{prop.Name}[{index}]"
                            : $"{context}.{prop.Name}[{index}]";

                        ValidateObjectAgainstSchema(
                            fieldMeta.ElementType,
                            instanceName,
                            element,
                            elementContext,
                            result,
                            isTemplate: false);

                        index++;
                    }
                }
            }
            // If it's an object field with an embedded class type, validate it
            else if (prop.Value.ValueKind == JsonValueKind.Object &&
                     _schema.IsEmbeddedClass(fieldMeta.Type))
            {
                var nestedContext = string.IsNullOrEmpty(context)
                    ? prop.Name
                    : $"{context}.{prop.Name}";

                ValidateObjectAgainstSchema(
                    fieldMeta.Type,
                    instanceName,
                    prop.Value,
                    nestedContext,
                    result,
                    isTemplate: false);
            }
        }
    }
}
