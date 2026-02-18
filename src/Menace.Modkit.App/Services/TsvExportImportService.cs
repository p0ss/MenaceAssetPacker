using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Result of a TSV import operation.
/// </summary>
public sealed class TsvImportResult
{
    /// <summary>
    /// Whether the import was successful overall.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of rows processed.
    /// </summary>
    public int ProcessedCount { get; init; }

    /// <summary>
    /// Number of values actually updated.
    /// </summary>
    public int UpdatedCount { get; init; }

    /// <summary>
    /// Number of rows skipped (not found in current data).
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Error messages for specific rows/fields.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Warning messages for specific rows/fields.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Service for exporting and importing bulk edit data in TSV format.
/// </summary>
public static class TsvExportImportService
{
    private const string NullMarker = "<null>";
    private const char TabChar = '\t';

    /// <summary>
    /// Exports rows to TSV format using visible columns.
    /// </summary>
    public static string ExportToTsv(
        IEnumerable<BulkEditRowModel> rows,
        IReadOnlyList<BulkEditColumnDefinition> visibleColumns)
    {
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(TabChar.ToString(), visibleColumns.Select(c => c.FieldName)));

        // Data rows
        foreach (var row in rows)
        {
            var values = new List<string>();
            foreach (var col in visibleColumns)
            {
                var value = row.GetDisplayValue(col.FieldName);
                values.Add(FormatValueForTsv(value, col.TypeCategory));
            }
            sb.AppendLine(string.Join(TabChar.ToString(), values));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Imports data from TSV string into existing rows.
    /// </summary>
    public static TsvImportResult ImportFromTsv(
        string tsvContent,
        IList<BulkEditRowModel> rows,
        IReadOnlyList<BulkEditColumnDefinition> visibleColumns)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        int processedCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;

        try
        {
            var lines = tsvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
            {
                return new TsvImportResult
                {
                    Success = false,
                    Errors = new List<string> { "TSV content is empty" }
                };
            }

            // Parse header
            var headerFields = lines[0].Split(TabChar);
            var fieldIndexMap = new Dictionary<string, int>();
            for (int i = 0; i < headerFields.Length; i++)
            {
                fieldIndexMap[headerFields[i].Trim()] = i;
            }

            // Validate that 'name' column exists
            if (!fieldIndexMap.ContainsKey("name"))
            {
                return new TsvImportResult
                {
                    Success = false,
                    Errors = new List<string> { "TSV must have a 'name' column to identify rows" }
                };
            }

            var nameIndex = fieldIndexMap["name"];

            // Build lookup for existing rows by name
            var rowLookup = rows.ToDictionary(r => r.Name, r => r);

            // Build column lookup for type info
            var columnLookup = visibleColumns.ToDictionary(c => c.FieldName, c => c);

            // Process data rows
            for (int lineNum = 1; lineNum < lines.Count; lineNum++)
            {
                var values = lines[lineNum].Split(TabChar);
                processedCount++;

                if (values.Length <= nameIndex)
                {
                    errors.Add($"Line {lineNum + 1}: Not enough columns");
                    continue;
                }

                var rowName = values[nameIndex].Trim();
                if (!rowLookup.TryGetValue(rowName, out var row))
                {
                    warnings.Add($"Line {lineNum + 1}: Row '{rowName}' not found, skipping");
                    skippedCount++;
                    continue;
                }

                // Update each field (except name and read-only fields)
                for (int i = 0; i < headerFields.Length && i < values.Length; i++)
                {
                    var fieldName = headerFields[i].Trim();
                    if (fieldName == "name")
                        continue;

                    if (!columnLookup.TryGetValue(fieldName, out var colDef))
                    {
                        // Column not in visible columns, skip
                        continue;
                    }

                    if (colDef.IsReadOnly)
                    {
                        continue;
                    }

                    try
                    {
                        var parsedValue = ParseValueFromTsv(values[i], colDef.TypeCategory, row.GetVanillaValue(fieldName));
                        if (row.UpdateField(fieldName, parsedValue))
                        {
                            updatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {lineNum + 1}, field '{fieldName}': {ex.Message}");
                    }
                }
            }

            return new TsvImportResult
            {
                Success = errors.Count == 0,
                ProcessedCount = processedCount,
                UpdatedCount = updatedCount,
                SkippedCount = skippedCount,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            return new TsvImportResult
            {
                Success = false,
                Errors = new List<string> { $"Failed to parse TSV: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Formats a value for TSV output.
    /// </summary>
    private static string FormatValueForTsv(object? value, FieldTypeCategory typeCategory)
    {
        if (value == null)
            return NullMarker;

        switch (value)
        {
            case bool b:
                return b.ToString().ToLowerInvariant();

            case JsonElement je:
                var jsonStr = je.ValueKind switch
                {
                    JsonValueKind.Null => NullMarker,
                    JsonValueKind.Array => je.GetRawText(),
                    JsonValueKind.Object => je.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => je.GetRawText(),
                    _ => je.GetString() ?? string.Empty
                };
                // Always escape tabs and newlines in JSON output
                return EscapeForTsv(jsonStr);

            case AssetPropertyValue assetValue:
                return EscapeForTsv(assetValue.AssetName ?? assetValue.RawValue?.ToString() ?? string.Empty);

            case string s when string.IsNullOrEmpty(s):
                return "\"\"";

            default:
                return EscapeForTsv(value.ToString() ?? string.Empty);
        }
    }

    /// <summary>
    /// Escapes a string for TSV output (tabs and newlines).
    /// </summary>
    private static string EscapeForTsv(string str)
    {
        return str.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    /// <summary>
    /// Parses a TSV value back to the appropriate type.
    /// </summary>
    private static object? ParseValueFromTsv(string value, FieldTypeCategory typeCategory, object? vanillaValue)
    {
        value = value.Trim();

        // Handle special markers
        if (value == NullMarker)
            return null;

        if (value == "\"\"")
            return string.Empty;

        // Unescape
        value = value.Replace("\\t", "\t").Replace("\\n", "\n").Replace("\\r", "\r");

        switch (typeCategory)
        {
            case FieldTypeCategory.Boolean:
                if (bool.TryParse(value, out var boolResult))
                    return boolResult;
                // Handle "1"/"0" as bool
                if (value == "1") return true;
                if (value == "0") return false;
                throw new FormatException($"Cannot parse '{value}' as boolean");

            case FieldTypeCategory.Number:
                // Try to preserve the original numeric type
                if (vanillaValue is int)
                {
                    if (int.TryParse(value, out var intResult))
                        return intResult;
                }
                else if (vanillaValue is long)
                {
                    if (long.TryParse(value, out var longResult))
                        return longResult;
                }
                else if (vanillaValue is float)
                {
                    if (float.TryParse(value, out var floatResult))
                        return floatResult;
                }
                else if (vanillaValue is double)
                {
                    if (double.TryParse(value, out var doubleResult))
                        return doubleResult;
                }

                // Fallback: try double, then long
                if (value.Contains('.'))
                {
                    if (double.TryParse(value, out var doubleResult))
                        return doubleResult;
                }
                else
                {
                    if (long.TryParse(value, out var longResult))
                        return longResult;
                }
                throw new FormatException($"Cannot parse '{value}' as number");

            case FieldTypeCategory.SimpleArray:
                // Parse as JSON array
                try
                {
                    var doc = JsonDocument.Parse(value);
                    return doc.RootElement.Clone();
                }
                catch
                {
                    throw new FormatException($"Cannot parse '{value}' as JSON array");
                }

            case FieldTypeCategory.Enum:
                // Return as string (enum name)
                return value;

            case FieldTypeCategory.AssetReference:
            case FieldTypeCategory.TemplateReference:
            case FieldTypeCategory.String:
            default:
                return value;
        }
    }
}
