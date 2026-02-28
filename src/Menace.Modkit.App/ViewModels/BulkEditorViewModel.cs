using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the bulk editor panel that displays a DataGrid of items.
/// Used when a folder/category is selected in either StatsEditor or AssetBrowser.
/// </summary>
public sealed class BulkEditorViewModel : ViewModelBase
{
    private readonly SchemaService? _schemaService;
    private readonly Func<string, string, object?, bool>? _commitChangeCallback;
    private readonly ColumnPreferenceService _columnPreferences;

    /// <summary>
    /// All rows in the table.
    /// </summary>
    public ObservableCollection<BulkEditRowModel> Rows { get; } = new();

    /// <summary>
    /// All available columns for this category.
    /// </summary>
    public ObservableCollection<BulkEditColumnDefinition> AllColumns { get; } = new();

    /// <summary>
    /// Currently visible columns (user-selected subset).
    /// </summary>
    public ObservableCollection<BulkEditColumnDefinition> VisibleColumns { get; } = new();

    private string _categoryName = string.Empty;
    /// <summary>
    /// The name of the category/folder being edited.
    /// </summary>
    public string CategoryName
    {
        get => _categoryName;
        set => this.RaiseAndSetIfChanged(ref _categoryName, value);
    }

    private string _templateType = string.Empty;
    /// <summary>
    /// The template type name (for stats) or asset folder type.
    /// </summary>
    public string TemplateType
    {
        get => _templateType;
        set => this.RaiseAndSetIfChanged(ref _templateType, value);
    }

    private int _totalRowCount;
    /// <summary>
    /// Total number of items in this category.
    /// </summary>
    public int TotalRowCount
    {
        get => _totalRowCount;
        private set => this.RaiseAndSetIfChanged(ref _totalRowCount, value);
    }

    private int _modifiedRowCount;
    /// <summary>
    /// Number of items with modifications.
    /// </summary>
    public int ModifiedRowCount
    {
        get => _modifiedRowCount;
        private set => this.RaiseAndSetIfChanged(ref _modifiedRowCount, value);
    }

    private int _selectedRowCount;
    /// <summary>
    /// Number of selected items.
    /// </summary>
    public int SelectedRowCount
    {
        get => _selectedRowCount;
        private set => this.RaiseAndSetIfChanged(ref _selectedRowCount, value);
    }

    private bool _isLoading;
    /// <summary>
    /// True while data is being loaded.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private string? _statusMessage;
    /// <summary>
    /// Status message for user feedback.
    /// </summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>
    /// Read-only field names that cannot be edited.
    /// </summary>
    private static readonly HashSet<string> ReadOnlyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "DisplayTitle", "DisplayShortName", "DisplayDescription",
        "name", "m_ID",
        "HasIcon", "IconAssetName"
    };

    /// <summary>
    /// Creates a new BulkEditorViewModel.
    /// </summary>
    /// <param name="schemaService">Optional schema service for field metadata.</param>
    /// <param name="commitChangeCallback">Callback to commit changes to parent ViewModel.
    /// Parameters: compositeKey, fieldName, value. Returns true if successful.</param>
    public BulkEditorViewModel(
        SchemaService? schemaService = null,
        Func<string, string, object?, bool>? commitChangeCallback = null)
    {
        _schemaService = schemaService;
        _commitChangeCallback = commitChangeCallback;
        _columnPreferences = new ColumnPreferenceService();
    }

    /// <summary>
    /// Loads data from a list of tree nodes (for stats editor).
    /// </summary>
    public void LoadFromTreeNodes(
        string categoryName,
        string templateType,
        IEnumerable<TreeNodeViewModel> nodes,
        Func<DataTemplate, Dictionary<string, object?>> convertToProperties,
        Func<string, Dictionary<string, object?>?> getStagingOverrides,
        Func<string, Dictionary<string, object?>?> getPendingChanges)
    {
        Services.ModkitLog.Info($"BulkEditorVM.LoadFromTreeNodes: category={categoryName}, type={templateType}");
        IsLoading = true;
        Rows.Clear();
        AllColumns.Clear();
        VisibleColumns.Clear();

        CategoryName = categoryName;
        TemplateType = templateType;

        var allFieldNames = new HashSet<string>();
        var fieldTypeMap = new Dictionary<string, FieldTypeCategory>();

        try
        {
            foreach (var node in nodes)
            {
                if (node.IsCategory || node.Template == null)
                    continue;

                var template = node.Template;
                var compositeKey = GetCompositeKey(templateType, template.Name);
                var vanillaProps = convertToProperties(template);
                var modifiedProps = new Dictionary<string, object?>(vanillaProps);

                // Apply staging overrides
                var staging = getStagingOverrides(compositeKey);
                if (staging != null)
                {
                    foreach (var kvp in staging)
                    {
                        if (modifiedProps.ContainsKey(kvp.Key))
                            modifiedProps[kvp.Key] = kvp.Value;
                    }
                }

                // Apply pending changes
                var pending = getPendingChanges(compositeKey);
                if (pending != null)
                {
                    foreach (var kvp in pending)
                    {
                        if (modifiedProps.ContainsKey(kvp.Key))
                            modifiedProps[kvp.Key] = kvp.Value;
                    }
                }

                var row = new BulkEditRowModel(
                    compositeKey,
                    template.Name,
                    templateType,
                    template,
                    vanillaProps,
                    modifiedProps);

                Rows.Add(row);

                // Collect field names for column generation
                foreach (var fieldName in vanillaProps.Keys)
                {
                    if (allFieldNames.Add(fieldName))
                    {
                        // Determine field type category
                        fieldTypeMap[fieldName] = DetermineFieldTypeCategory(
                            templateType, fieldName, vanillaProps[fieldName]);
                    }
                }
            }

            Services.ModkitLog.Info($"BulkEditorVM: Processed {Rows.Count} rows, {allFieldNames.Count} unique fields");

            // Generate columns
            GenerateColumns(allFieldNames, fieldTypeMap);
            Services.ModkitLog.Info($"BulkEditorVM: Generated {AllColumns.Count} columns");

            // Load saved column preferences
            LoadColumnPreferences();
            Services.ModkitLog.Info($"BulkEditorVM: {VisibleColumns.Count} visible columns after preferences");

            TotalRowCount = Rows.Count;
            RecalculateModifiedCount();
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Error($"BulkEditorVM.LoadFromTreeNodes failed: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads data from asset tree nodes (for asset browser).
    /// </summary>
    public void LoadFromAssetNodes(
        string folderPath,
        IEnumerable<AssetTreeNode> files,
        Func<AssetTreeNode, bool> hasModifiedReplacement)
    {
        IsLoading = true;
        Rows.Clear();
        AllColumns.Clear();
        VisibleColumns.Clear();

        CategoryName = folderPath;
        TemplateType = "Assets";

        try
        {
            foreach (var file in files)
            {
                if (!file.IsFile)
                    continue;

                var vanillaProps = new Dictionary<string, object?>
                {
                    ["name"] = file.Name,
                    ["FileType"] = GetFileTypeFromName(file.Name),
                    ["Size"] = file.Size,
                    ["FullPath"] = file.FullPath
                };

                var modifiedProps = new Dictionary<string, object?>(vanillaProps);
                modifiedProps["HasReplacement"] = hasModifiedReplacement(file);

                var row = new BulkEditRowModel(
                    file.FullPath,
                    file.Name,
                    "Asset",
                    file,
                    vanillaProps,
                    modifiedProps);

                Rows.Add(row);
            }

            // Generate asset-specific columns
            GenerateAssetColumns();

            TotalRowCount = Rows.Count;
            RecalculateModifiedCount();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Updates a cell value and optionally commits to parent ViewModel.
    /// </summary>
    public bool UpdateCellValue(BulkEditRowModel row, string fieldName, object? newValue)
    {
        if (!row.UpdateField(fieldName, newValue))
            return false;

        // Commit to parent ViewModel if callback is provided
        _commitChangeCallback?.Invoke(row.CompositeKey, fieldName, newValue);

        RecalculateModifiedCount();
        StatusMessage = $"Updated {fieldName} for {row.Name}";
        return true;
    }

    /// <summary>
    /// Resets a cell to its vanilla value.
    /// </summary>
    public void ResetCellToVanilla(BulkEditRowModel row, string fieldName)
    {
        var vanillaValue = row.GetVanillaValue(fieldName);
        row.ResetField(fieldName);

        // Commit vanilla value to parent
        _commitChangeCallback?.Invoke(row.CompositeKey, fieldName, vanillaValue);

        RecalculateModifiedCount();
        StatusMessage = $"Reset {fieldName} to vanilla for {row.Name}";
    }

    /// <summary>
    /// Selects all rows.
    /// </summary>
    public void SelectAll()
    {
        foreach (var row in Rows)
            row.IsSelected = true;
        RecalculateSelectedCount();
    }

    /// <summary>
    /// Deselects all rows.
    /// </summary>
    public void DeselectAll()
    {
        foreach (var row in Rows)
            row.IsSelected = false;
        RecalculateSelectedCount();
    }

    /// <summary>
    /// Toggles column visibility.
    /// </summary>
    public void SetColumnVisibility(string fieldName, bool isVisible)
    {
        var column = AllColumns.FirstOrDefault(c => c.FieldName == fieldName);
        if (column != null)
        {
            column.IsVisible = isVisible;
            RefreshVisibleColumns();
            SaveColumnPreferences();
        }
    }

    /// <summary>
    /// Gets all pending changes from all rows for committing to staging.
    /// </summary>
    public Dictionary<string, Dictionary<string, object?>> GetAllPendingChanges()
    {
        var result = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var row in Rows)
        {
            var changes = row.GetPendingChanges();
            if (changes.Count > 0)
            {
                result[row.CompositeKey] = changes;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets field names that were reverted to vanilla for all rows.
    /// </summary>
    public Dictionary<string, HashSet<string>> GetAllRevertedFields()
    {
        var result = new Dictionary<string, HashSet<string>>();

        foreach (var row in Rows)
        {
            var reverted = row.GetRevertedFields();
            if (reverted.Count > 0)
            {
                result[row.CompositeKey] = reverted;
            }
        }

        return result;
    }

    /// <summary>
    /// Exports visible columns and rows to TSV format.
    /// </summary>
    public string ExportToTsv()
    {
        return TsvExportImportService.ExportToTsv(Rows, VisibleColumns.ToList());
    }

    /// <summary>
    /// Imports data from TSV string.
    /// </summary>
    public TsvImportResult ImportFromTsv(string tsvContent)
    {
        var result = TsvExportImportService.ImportFromTsv(tsvContent, Rows, VisibleColumns.ToList());

        if (result.Success)
        {
            // Commit imported changes
            foreach (var row in Rows)
            {
                foreach (var fieldName in row.GetUserEditedFields())
                {
                    _commitChangeCallback?.Invoke(
                        row.CompositeKey,
                        fieldName,
                        row.GetDisplayValue(fieldName));
                }
            }

            RecalculateModifiedCount();
            StatusMessage = $"Imported {result.UpdatedCount} values from TSV";
        }

        return result;
    }

    private void GenerateColumns(
        HashSet<string> fieldNames,
        Dictionary<string, FieldTypeCategory> fieldTypeMap)
    {
        // Always include Name column first
        AllColumns.Add(new BulkEditColumnDefinition
        {
            FieldName = "name",
            Header = "Name",
            Width = "200",
            IsVisible = true,
            IsReadOnly = true,
            TypeCategory = FieldTypeCategory.String
        });

        // Sort remaining fields alphabetically, grouping by prefix
        var sortedFields = fieldNames
            .Where(f => !f.Equals("name", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        foreach (var fieldName in sortedFields)
        {
            var typeCategory = fieldTypeMap.GetValueOrDefault(fieldName, FieldTypeCategory.String);
            var isReadOnly = ReadOnlyFields.Contains(fieldName);

            // Get metadata for template references if applicable
            string? templateRefType = null;

            if (_schemaService?.IsLoaded == true && !string.IsNullOrEmpty(TemplateType))
            {
                var fieldMeta = _schemaService.GetFieldMetadata(TemplateType, fieldName);
                if (fieldMeta?.Category == "template_ref")
                {
                    templateRefType = fieldMeta.Type;
                }
            }

            // Note: enum values are currently rendered as text; dropdown editing would require
            // schema extension to provide enum value lists

            AllColumns.Add(new BulkEditColumnDefinition
            {
                FieldName = fieldName,
                Header = FormatHeaderName(fieldName),
                Width = DetermineColumnWidth(typeCategory),
                IsVisible = ShouldColumnBeVisibleByDefault(fieldName, typeCategory),
                IsReadOnly = isReadOnly,
                TypeCategory = typeCategory,
                TemplateRefType = templateRefType
            });
        }

        RefreshVisibleColumns();
    }

    private void GenerateAssetColumns()
    {
        AllColumns.Add(new BulkEditColumnDefinition
        {
            FieldName = "name",
            Header = "Name",
            Width = "250",
            IsVisible = true,
            IsReadOnly = true,
            TypeCategory = FieldTypeCategory.String
        });

        AllColumns.Add(new BulkEditColumnDefinition
        {
            FieldName = "FileType",
            Header = "Type",
            Width = "80",
            IsVisible = true,
            IsReadOnly = true,
            TypeCategory = FieldTypeCategory.String
        });

        AllColumns.Add(new BulkEditColumnDefinition
        {
            FieldName = "Size",
            Header = "Size",
            Width = "80",
            IsVisible = true,
            IsReadOnly = true,
            TypeCategory = FieldTypeCategory.Number
        });

        AllColumns.Add(new BulkEditColumnDefinition
        {
            FieldName = "HasReplacement",
            Header = "Modified",
            Width = "80",
            IsVisible = true,
            IsReadOnly = true,
            TypeCategory = FieldTypeCategory.Boolean
        });

        RefreshVisibleColumns();
    }

    private void RefreshVisibleColumns()
    {
        VisibleColumns.Clear();
        foreach (var col in AllColumns.Where(c => c.IsVisible))
        {
            VisibleColumns.Add(col);
        }
    }

    private void RecalculateModifiedCount()
    {
        ModifiedRowCount = Rows.Count(r => r.HasAnyModifications);
    }

    private void RecalculateSelectedCount()
    {
        SelectedRowCount = Rows.Count(r => r.IsSelected);
    }

    private void LoadColumnPreferences()
    {
        var prefs = _columnPreferences.LoadPreferences(TemplateType);
        if (prefs != null && prefs.VisibleColumns.Count > 0)
        {
            // Check if any preferences match current columns
            var currentFieldNames = AllColumns.Select(c => c.FieldName).ToHashSet();
            var matchingPrefs = prefs.VisibleColumns.Where(f => currentFieldNames.Contains(f)).ToList();

            // Only apply preferences if at least some match
            if (matchingPrefs.Count > 0)
            {
                foreach (var col in AllColumns)
                {
                    col.IsVisible = matchingPrefs.Contains(col.FieldName);
                }
            }
            // Always ensure at least 'name' column is visible
            var nameCol = AllColumns.FirstOrDefault(c => c.FieldName == "name");
            if (nameCol != null)
                nameCol.IsVisible = true;

            RefreshVisibleColumns();
        }

        // Final check: ensure we have at least one visible column
        if (VisibleColumns.Count == 0 && AllColumns.Count > 0)
        {
            // Make first column visible as fallback
            AllColumns[0].IsVisible = true;
            RefreshVisibleColumns();
        }
    }

    private void SaveColumnPreferences()
    {
        var visibleFields = AllColumns.Where(c => c.IsVisible).Select(c => c.FieldName).ToList();
        _columnPreferences.SavePreferences(TemplateType, new ColumnPreferences
        {
            VisibleColumns = visibleFields
        });
    }

    private FieldTypeCategory DetermineFieldTypeCategory(
        string templateType,
        string fieldName,
        object? sampleValue)
    {
        // Check schema first
        if (_schemaService?.IsLoaded == true && !string.IsNullOrEmpty(templateType))
        {
            var fieldMeta = _schemaService.GetFieldMetadata(templateType, fieldName);
            if (fieldMeta != null)
            {
                return fieldMeta.Category switch
                {
                    "enum" => FieldTypeCategory.Enum,
                    "template_ref" => FieldTypeCategory.TemplateReference,
                    "unity_asset" => FieldTypeCategory.AssetReference,
                    _ => DetermineFromValue(sampleValue)
                };
            }
        }

        return DetermineFromValue(sampleValue);
    }

    private static FieldTypeCategory DetermineFromValue(object? value)
    {
        return value switch
        {
            bool => FieldTypeCategory.Boolean,
            int or long or float or double or decimal => FieldTypeCategory.Number,
            JsonElement je when je.ValueKind == JsonValueKind.Array =>
                ArrayContainsObjects(je) ? FieldTypeCategory.ComplexArray : FieldTypeCategory.SimpleArray,
            JsonElement je when je.ValueKind == JsonValueKind.Object => FieldTypeCategory.NestedObject,
            AssetPropertyValue => FieldTypeCategory.AssetReference,
            _ => FieldTypeCategory.String
        };
    }

    private static bool ArrayContainsObjects(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
                return true;
        }
        return false;
    }

    private static string FormatHeaderName(string fieldName)
    {
        // Handle dotted names (e.g., "Properties.HitpointsPerElement")
        var lastPart = fieldName.Contains('.')
            ? fieldName[(fieldName.LastIndexOf('.') + 1)..]
            : fieldName;

        // Add spaces before capital letters
        var result = new System.Text.StringBuilder();
        foreach (var c in lastPart)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append(' ');
            result.Append(c);
        }

        return result.ToString();
    }

    private static string DetermineColumnWidth(FieldTypeCategory typeCategory)
    {
        return typeCategory switch
        {
            FieldTypeCategory.Boolean => "70",
            FieldTypeCategory.Number => "100",
            FieldTypeCategory.Enum => "150",
            FieldTypeCategory.SimpleArray => "180",
            FieldTypeCategory.ComplexArray => "120",
            FieldTypeCategory.String => "150",
            FieldTypeCategory.AssetReference => "180",
            FieldTypeCategory.TemplateReference => "180",
            _ => "150"  // Fixed width instead of star to prevent squishing
        };
    }

    private static bool ShouldColumnBeVisibleByDefault(string fieldName, FieldTypeCategory typeCategory)
    {
        // Hide complex types by default
        if (typeCategory is FieldTypeCategory.ComplexArray or FieldTypeCategory.NestedObject)
            return false;

        // Hide common metadata fields
        var lowerName = fieldName.ToLowerInvariant();
        if (lowerName.Contains("display") || lowerName == "hasicon" || lowerName == "iconassetname")
            return false;

        return true;
    }

    private static string GetFileTypeFromName(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" => "Image",
            ".wav" or ".ogg" or ".mp3" => "Audio",
            ".glb" or ".gltf" or ".fbx" or ".obj" => "Model",
            ".json" or ".xml" or ".txt" => "Text",
            _ => "Binary"
        };
    }

    private static string GetCompositeKey(string templateType, string instanceName)
    {
        return $"{templateType}/{instanceName}";
    }
}
