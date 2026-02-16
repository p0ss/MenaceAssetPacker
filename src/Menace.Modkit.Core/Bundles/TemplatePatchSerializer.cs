using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Converts JSON patch values into binary Unity serialized format using typetree node layout.
/// This bridges the modkit's JSON-based patch format with Unity's binary asset format.
/// </summary>
public class TemplatePatchSerializer
{
    // Regex patterns for indexed path segments
    private static readonly Regex IndexedPathPattern = new(@"^(\w+)\[(\d+)\]$", RegexOptions.Compiled);
    private static readonly Regex TypeMatchPattern = new(@"^(\w+)\[type=(\w+)\]$", RegexOptions.Compiled);

    /// <summary>
    /// Represents a parsed path segment.
    /// </summary>
    private class PathSegment
    {
        public string Name { get; set; } = "";
        public bool HasIndex { get; set; }
        public int Index { get; set; }
        public bool HasTypeMatch { get; set; }
        public string TypeMatch { get; set; } = "";
    }

    /// <summary>
    /// Apply a set of field patches to an existing AssetTypeValueField (in-memory asset data).
    /// </summary>
    public static void ApplyPatches(
        AssetTypeValueField baseField,
        Dictionary<string, JsonElement> patches)
    {
        foreach (var (fieldName, value) in patches)
        {
            var targetField = FindField(baseField, fieldName);
            if (targetField == null)
                continue;

            ApplyValue(targetField, value);
        }
    }

    /// <summary>
    /// Parse a field path into segments, supporting:
    /// - Simple names: "FieldName"
    /// - Indexed access: "EventHandlers[0]"
    /// - Type matching: "EventHandlers[type=Damage]"
    /// </summary>
    private static List<PathSegment> ParseFieldPath(string fieldPath)
    {
        var segments = new List<PathSegment>();
        var parts = fieldPath.Split('.');

        foreach (var part in parts)
        {
            // Try indexed pattern: FieldName[0]
            var indexMatch = IndexedPathPattern.Match(part);
            if (indexMatch.Success)
            {
                segments.Add(new PathSegment
                {
                    Name = indexMatch.Groups[1].Value,
                    HasIndex = true,
                    Index = int.Parse(indexMatch.Groups[2].Value)
                });
                continue;
            }

            // Try type match pattern: FieldName[type=TypeName]
            var typeMatch = TypeMatchPattern.Match(part);
            if (typeMatch.Success)
            {
                segments.Add(new PathSegment
                {
                    Name = typeMatch.Groups[1].Value,
                    HasTypeMatch = true,
                    TypeMatch = typeMatch.Groups[2].Value
                });
                continue;
            }

            // Simple field name
            segments.Add(new PathSegment { Name = part });
        }

        return segments;
    }

    /// <summary>
    /// Find a field by name, supporting:
    /// - Dotted paths: "Properties.Armor"
    /// - Indexed collection access: "EventHandlers[0].BaseDamage"
    /// - Type matching: "EventHandlers[type=Damage].BaseDamage"
    /// </summary>
    private static AssetTypeValueField? FindField(AssetTypeValueField root, string fieldPath)
    {
        var segments = ParseFieldPath(fieldPath);
        var current = root;

        foreach (var segment in segments)
        {
            // Find child by name
            AssetTypeValueField? found = null;
            foreach (var child in current.Children)
            {
                if (child.FieldName == segment.Name)
                {
                    found = child;
                    break;
                }
            }

            if (found == null)
                return null;

            current = found;

            // Handle indexed access
            if (segment.HasIndex)
            {
                current = NavigateToArrayElement(current, segment.Index);
                if (current == null)
                    return null;
            }
            // Handle type matching
            else if (segment.HasTypeMatch)
            {
                current = FindArrayElementByType(current, segment.TypeMatch);
                if (current == null)
                    return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Navigate into an array/list field to get a specific element by index.
    /// Unity serializes arrays with an "Array" child containing "size" and "data" children.
    /// </summary>
    private static AssetTypeValueField? NavigateToArrayElement(AssetTypeValueField arrayField, int index)
    {
        // Find the Array child
        AssetTypeValueField? arrayContainer = null;
        foreach (var child in arrayField.Children)
        {
            if (child.FieldName == "Array")
            {
                arrayContainer = child;
                break;
            }
        }

        if (arrayContainer == null)
        {
            // Maybe the field itself is the array container
            arrayContainer = arrayField;
        }

        // Get children (skip "size" field if present)
        int dataIndex = 0;
        foreach (var child in arrayContainer.Children)
        {
            if (child.FieldName == "size")
                continue;

            if (child.FieldName == "data" || child.FieldName.StartsWith("data"))
            {
                if (dataIndex == index)
                    return child;
                dataIndex++;
            }
        }

        // Direct index into children (for simple arrays)
        if (index < arrayContainer.Children.Count)
        {
            var child = arrayContainer.Children[index];
            if (child.FieldName != "size")
                return child;
        }

        return null;
    }

    /// <summary>
    /// Find an array element by matching a type field (for polymorphic SerializeReference arrays).
    /// Looks for elements with a "_type" or "m_Type" field matching the specified type name.
    /// </summary>
    private static AssetTypeValueField? FindArrayElementByType(AssetTypeValueField arrayField, string typeName)
    {
        // Find the Array child
        AssetTypeValueField? arrayContainer = null;
        foreach (var child in arrayField.Children)
        {
            if (child.FieldName == "Array")
            {
                arrayContainer = child;
                break;
            }
        }

        if (arrayContainer == null)
            arrayContainer = arrayField;

        // Search for element with matching type
        foreach (var child in arrayContainer.Children)
        {
            if (child.FieldName == "size")
                continue;

            // Look for type indicator in the element
            string? elementType = GetElementTypeName(child);
            if (elementType != null && string.Equals(elementType, typeName, StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Try to determine the type name of a serialized element.
    /// Checks for common type indicator fields.
    /// </summary>
    private static string? GetElementTypeName(AssetTypeValueField element)
    {
        // Check for type indicator fields
        string[] typeFields = { "_type", "m_Type", "Type", "type", "$type" };

        foreach (var fieldName in typeFields)
        {
            foreach (var child in element.Children)
            {
                if (child.FieldName == fieldName && child.Value?.AsString != null)
                    return child.Value.AsString;
            }
        }

        // For SerializeReference, the type might be in metadata
        // Check the field's type name if available
        if (!string.IsNullOrEmpty(element.TypeName))
        {
            // Extract short name from full type
            var lastDot = element.TypeName.LastIndexOf('.');
            return lastDot >= 0 ? element.TypeName.Substring(lastDot + 1) : element.TypeName;
        }

        return null;
    }

    /// <summary>
    /// Set a field's value from a JsonElement, converting types appropriately.
    /// </summary>
    private static void ApplyValue(AssetTypeValueField field, JsonElement value)
    {
        if (field.Value == null) return;
        var valueType = field.Value.ValueType;

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                if (value.TryGetInt32(out var intVal))
                {
                    switch (valueType)
                    {
                        case AssetValueType.Int32:
                            field.Value.AsInt = intVal;
                            break;
                        case AssetValueType.Float:
                            field.Value.AsFloat = intVal;
                            break;
                        case AssetValueType.Int64:
                            field.Value.AsLong = intVal;
                            break;
                        case AssetValueType.Double:
                            field.Value.AsDouble = intVal;
                            break;
                        case AssetValueType.UInt32:
                            field.Value.AsUInt = (uint)intVal;
                            break;
                        case AssetValueType.Int16:
                            field.Value.AsShort = (short)intVal;
                            break;
                        case AssetValueType.UInt16:
                            field.Value.AsUShort = (ushort)intVal;
                            break;
                        case AssetValueType.Int8:
                            field.Value.AsSByte = (sbyte)intVal;
                            break;
                        case AssetValueType.UInt8:
                            field.Value.AsByte = (byte)intVal;
                            break;
                        default:
                            field.Value.AsInt = intVal;
                            break;
                    }
                }
                else if (value.TryGetDouble(out var doubleVal))
                {
                    switch (valueType)
                    {
                        case AssetValueType.Float:
                            field.Value.AsFloat = (float)doubleVal;
                            break;
                        case AssetValueType.Double:
                            field.Value.AsDouble = doubleVal;
                            break;
                        default:
                            field.Value.AsFloat = (float)doubleVal;
                            break;
                    }
                }
                break;

            case JsonValueKind.True:
                field.Value.AsBool = true;
                break;

            case JsonValueKind.False:
                field.Value.AsBool = false;
                break;

            case JsonValueKind.String:
                var str = value.GetString() ?? string.Empty;
                if (valueType == AssetValueType.String)
                    field.Value.AsString = str;
                break;
        }
    }
}
