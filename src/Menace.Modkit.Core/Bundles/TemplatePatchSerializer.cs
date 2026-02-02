using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Converts JSON patch values into binary Unity serialized format using typetree node layout.
/// This bridges the modkit's JSON-based patch format with Unity's binary asset format.
/// </summary>
public class TemplatePatchSerializer
{
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
    /// Find a field by name, supporting dotted paths (e.g. "Properties.Armor").
    /// </summary>
    private static AssetTypeValueField? FindField(AssetTypeValueField root, string fieldPath)
    {
        var parts = fieldPath.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            AssetTypeValueField? found = null;
            foreach (var child in current.Children)
            {
                if (child.FieldName == part)
                {
                    found = child;
                    break;
                }
            }

            if (found == null)
                return null;

            current = found;
        }

        return current;
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
