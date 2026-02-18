using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents a single row in the bulk editor table.
/// Each row corresponds to one template instance or asset file.
/// </summary>
public sealed class BulkEditRowModel : ViewModelBase
{
    /// <summary>
    /// Composite key for change tracking (e.g., "WeaponTemplate/weapon.rifle").
    /// </summary>
    public string CompositeKey { get; }

    /// <summary>
    /// Display name shown in the Name column.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Template type name (e.g., "WeaponTemplate").
    /// </summary>
    public string TemplateType { get; }

    /// <summary>
    /// Reference to the underlying data template (for stats) or asset node.
    /// </summary>
    public object? SourceObject { get; }

    /// <summary>
    /// Vanilla property values (read-only baseline).
    /// </summary>
    public IReadOnlyDictionary<string, object?> VanillaProperties { get; }

    /// <summary>
    /// Modified property values (editable). Includes staging + pending changes.
    /// </summary>
    private readonly Dictionary<string, object?> _modifiedProperties;
    public IReadOnlyDictionary<string, object?> ModifiedProperties => _modifiedProperties;

    /// <summary>
    /// Set of field names that have been modified (differ from vanilla).
    /// </summary>
    private readonly HashSet<string> _modifiedFields = new();

    /// <summary>
    /// Set of field names edited by the user in this session.
    /// </summary>
    private readonly HashSet<string> _userEditedFields = new();

    private bool _isSelected;
    /// <summary>
    /// Whether this row is selected for batch operations.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private bool _hasAnyModifications;
    /// <summary>
    /// True if any field in this row differs from vanilla.
    /// </summary>
    public bool HasAnyModifications
    {
        get => _hasAnyModifications;
        private set => this.RaiseAndSetIfChanged(ref _hasAnyModifications, value);
    }

    public BulkEditRowModel(
        string compositeKey,
        string name,
        string templateType,
        object? sourceObject,
        Dictionary<string, object?> vanillaProperties,
        Dictionary<string, object?> modifiedProperties)
    {
        CompositeKey = compositeKey;
        Name = name;
        TemplateType = templateType;
        SourceObject = sourceObject;
        VanillaProperties = vanillaProperties;
        _modifiedProperties = new Dictionary<string, object?>(modifiedProperties);

        // Initialize modified field tracking
        RecalculateModifiedFields();
    }

    /// <summary>
    /// Gets the display value for a given field name.
    /// Returns the modified value if present, otherwise vanilla.
    /// </summary>
    public object? GetDisplayValue(string fieldName)
    {
        return _modifiedProperties.TryGetValue(fieldName, out var value)
            ? value
            : VanillaProperties.GetValueOrDefault(fieldName);
    }

    /// <summary>
    /// Gets the vanilla value for a given field name (for tooltips).
    /// </summary>
    public object? GetVanillaValue(string fieldName)
    {
        return VanillaProperties.GetValueOrDefault(fieldName);
    }

    /// <summary>
    /// Checks if a specific field has been modified from vanilla.
    /// </summary>
    public bool IsFieldModified(string fieldName)
    {
        return _modifiedFields.Contains(fieldName);
    }

    /// <summary>
    /// Updates a field value and tracks the change.
    /// </summary>
    /// <param name="fieldName">The field name to update.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns>True if the value actually changed.</returns>
    public bool UpdateField(string fieldName, object? newValue)
    {
        var oldValue = GetDisplayValue(fieldName);

        // Check if value actually changed
        if (ValuesEqual(oldValue, newValue))
            return false;

        _modifiedProperties[fieldName] = newValue;
        _userEditedFields.Add(fieldName);

        // Recalculate if this field is now modified vs vanilla
        RecalculateModifiedFields();

        return true;
    }

    /// <summary>
    /// Resets a field to its vanilla value.
    /// </summary>
    public void ResetField(string fieldName)
    {
        if (VanillaProperties.TryGetValue(fieldName, out var vanillaValue))
        {
            _modifiedProperties[fieldName] = vanillaValue;
        }
        else
        {
            _modifiedProperties.Remove(fieldName);
        }

        _userEditedFields.Remove(fieldName);
        RecalculateModifiedFields();
    }

    /// <summary>
    /// Gets all fields that were explicitly edited by the user in this session.
    /// </summary>
    public IReadOnlySet<string> GetUserEditedFields() => _userEditedFields;

    /// <summary>
    /// Gets all fields that differ from vanilla.
    /// </summary>
    public IReadOnlySet<string> GetModifiedFields() => _modifiedFields;

    /// <summary>
    /// Gets the pending changes (only fields that differ from vanilla).
    /// </summary>
    public Dictionary<string, object?> GetPendingChanges()
    {
        var changes = new Dictionary<string, object?>();
        foreach (var fieldName in _modifiedFields)
        {
            if (_modifiedProperties.TryGetValue(fieldName, out var value))
            {
                changes[fieldName] = value;
            }
        }
        return changes;
    }

    /// <summary>
    /// Gets field names that were reverted back to vanilla values.
    /// </summary>
    public HashSet<string> GetRevertedFields()
    {
        var reverted = new HashSet<string>();
        foreach (var fieldName in _userEditedFields)
        {
            if (!_modifiedFields.Contains(fieldName))
            {
                // User edited this field but it now matches vanilla
                reverted.Add(fieldName);
            }
        }
        return reverted;
    }

    /// <summary>
    /// Indexer for DataGrid cell binding.
    /// Allows binding to row[fieldName] syntax.
    /// </summary>
    public object? this[string fieldName]
    {
        get => GetDisplayValue(fieldName);
        set => UpdateField(fieldName, value);
    }

    private void RecalculateModifiedFields()
    {
        _modifiedFields.Clear();

        foreach (var kvp in _modifiedProperties)
        {
            if (VanillaProperties.TryGetValue(kvp.Key, out var vanillaValue))
            {
                if (!ValuesEqual(vanillaValue, kvp.Value))
                {
                    _modifiedFields.Add(kvp.Key);
                }
            }
            else
            {
                // Field exists in modified but not in vanilla - treat as modified
                _modifiedFields.Add(kvp.Key);
            }
        }

        HasAnyModifications = _modifiedFields.Count > 0;
    }

    /// <summary>
    /// Compares two values for equality, handling type conversions.
    /// </summary>
    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        // Handle numeric type coercions
        if (a is IConvertible && b is IConvertible)
        {
            try
            {
                // Compare as strings for numeric types to handle int/long/double comparisons
                if (IsNumericType(a) && IsNumericType(b))
                {
                    var aDouble = System.Convert.ToDouble(a);
                    var bDouble = System.Convert.ToDouble(b);
                    return System.Math.Abs(aDouble - bDouble) < 0.0000001;
                }

                // Handle bool comparisons
                if (a is bool aBool && b is bool bBool)
                    return aBool == bBool;

                // Handle string from bool
                if (a is bool aBool2 && b is string bStr)
                    return aBool2.ToString().Equals(bStr, System.StringComparison.OrdinalIgnoreCase);
                if (a is string aStr && b is bool bBool2)
                    return bBool2.ToString().Equals(aStr, System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Fall through to string comparison
            }
        }

        // Handle JsonElement comparisons
        if (a is System.Text.Json.JsonElement jeA && b is System.Text.Json.JsonElement jeB)
        {
            return jeA.ToString() == jeB.ToString();
        }

        // Fall back to string comparison
        return a.ToString() == b.ToString();
    }

    private static bool IsNumericType(object? value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }
}

/// <summary>
/// Represents a column definition for the bulk editor table.
/// </summary>
public sealed class BulkEditColumnDefinition
{
    /// <summary>
    /// The field name in the property dictionary.
    /// </summary>
    public string FieldName { get; init; } = string.Empty;

    /// <summary>
    /// Display header for the column.
    /// </summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>
    /// Width of the column (Auto, *, or fixed pixel value).
    /// </summary>
    public string Width { get; init; } = "*";

    /// <summary>
    /// Whether this column is currently visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// The data type category for this field (for editor selection).
    /// </summary>
    public FieldTypeCategory TypeCategory { get; init; } = FieldTypeCategory.String;

    /// <summary>
    /// Whether this field is read-only and cannot be edited.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// For enum fields, the available values.
    /// </summary>
    public IReadOnlyList<string>? EnumValues { get; init; }

    /// <summary>
    /// For template reference fields, the target template type.
    /// </summary>
    public string? TemplateRefType { get; init; }
}

/// <summary>
/// Categories of field types for bulk editor cell rendering.
/// </summary>
public enum FieldTypeCategory
{
    /// <summary>Plain string or unknown type.</summary>
    String,
    /// <summary>Numeric value (int, float, etc.).</summary>
    Number,
    /// <summary>Boolean checkbox.</summary>
    Boolean,
    /// <summary>Enum dropdown.</summary>
    Enum,
    /// <summary>Asset reference with browse button.</summary>
    AssetReference,
    /// <summary>Template reference with autocomplete.</summary>
    TemplateReference,
    /// <summary>Simple array (editable as JSON).</summary>
    SimpleArray,
    /// <summary>Complex array of objects (read-only preview).</summary>
    ComplexArray,
    /// <summary>Nested object (flattened to dotted keys).</summary>
    NestedObject
}
