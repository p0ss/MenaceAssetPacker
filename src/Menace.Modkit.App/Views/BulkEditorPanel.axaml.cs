using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// A panel that displays a bulk editing DataGrid for categories/folders.
/// Used when a folder is selected in StatsEditor or AssetBrowser.
/// </summary>
public partial class BulkEditorPanel : UserControl
{
    private DataGrid? _dataGrid;
    private BulkEditorViewModel? _viewModel;
    private bool _isLoadingData;

    // Teal background color for modified cells
    private static readonly IBrush ModifiedCellBackground = new SolidColorBrush(Color.Parse("#1A3A3A"));
    private static readonly IBrush DefaultCellBackground = Brushes.Transparent;
    private static readonly IBrush SelectedRowBackground = new SolidColorBrush(Color.Parse("#264f78"));

    public BulkEditorPanel()
    {
        Content = BuildUI();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as BulkEditorViewModel;
        // Only regenerate columns if not already loading data
        // (LoadCategory sets DataContext then regenerates columns itself)
        if (_viewModel != null && !_isLoadingData)
        {
            RegenerateColumns();
        }
    }

    private Control BuildUI()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        // Row 0: Toolbar
        grid.Children.Add(BuildToolbar());
        Grid.SetRow((Control)grid.Children[0], 0);

        // Row 1: DataGrid
        var dataGridContainer = BuildDataGrid();
        grid.Children.Add(dataGridContainer);
        Grid.SetRow(dataGridContainer, 1);

        // Row 2: Status bar
        grid.Children.Add(BuildStatusBar());
        Grid.SetRow((Control)grid.Children[2], 2);

        return grid;
    }

    private Control BuildToolbar()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Category name / title
        var titleText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleText.Bind(TextBlock.TextProperty, new Binding("CategoryName"));
        toolbar.Children.Add(titleText);

        // Item count
        var countText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        countText.Bind(TextBlock.TextProperty, new Binding("TotalRowCount")
        {
            StringFormat = "({0} items)"
        });
        toolbar.Children.Add(countText);

        // Spacer
        toolbar.Children.Add(new Border { Width = 24 });

        // Columns dropdown
        var columnsLabel = new TextBlock
        {
            Text = "Columns:",
            FontSize = 12,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        toolbar.Children.Add(columnsLabel);

        var columnsButton = new Button
        {
            Content = "Select Columns...",
            FontSize = 11
        };
        columnsButton.Classes.Add("secondary");
        columnsButton.Click += OnColumnsButtonClick;
        toolbar.Children.Add(columnsButton);

        // Spacer
        toolbar.Children.Add(new Border { Width = 24 });

        // TSV Export button
        var exportButton = new Button
        {
            Content = "Export TSV",
            FontSize = 11
        };
        exportButton.Classes.Add("secondary");
        exportButton.Click += OnExportTsvClick;
        toolbar.Children.Add(exportButton);

        // TSV Import button
        var importButton = new Button
        {
            Content = "Import TSV",
            FontSize = 11
        };
        importButton.Classes.Add("secondary");
        importButton.Click += OnImportTsvClick;
        toolbar.Children.Add(importButton);

        // Spacer
        toolbar.Children.Add(new Border { Width = 24 });

        // Select All button
        var selectAllButton = new Button
        {
            Content = "Select All",
            FontSize = 11
        };
        selectAllButton.Classes.Add("secondary");
        selectAllButton.Click += (_, _) => _viewModel?.SelectAll();
        toolbar.Children.Add(selectAllButton);

        // Deselect All button
        var deselectAllButton = new Button
        {
            Content = "Deselect All",
            FontSize = 11
        };
        deselectAllButton.Classes.Add("secondary");
        deselectAllButton.Click += (_, _) => _viewModel?.DeselectAll();
        toolbar.Children.Add(deselectAllButton);

        return toolbar;
    }

    private Control BuildDataGrid()
    {
        _dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            CanUserSortColumns = false,  // Disabled to remove header padding for sort indicators
            IsReadOnly = false,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Extended,
            // Dark theme colors
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(1),
            RowBackground = new SolidColorBrush(Color.Parse("#252525")),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            VerticalGridLinesBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            // Ensure the DataGrid fills available space
            MinHeight = 200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            // Enable horizontal scrolling
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        _dataGrid.CellEditEnding += OnCellEditEnding;
        _dataGrid.LoadingRow += OnLoadingRow;

        return _dataGrid;
    }

    private Control BuildStatusBar()
    {
        var statusBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Modified count
        var modifiedText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            VerticalAlignment = VerticalAlignment.Center
        };
        modifiedText.Bind(TextBlock.TextProperty, new Binding("ModifiedRowCount")
        {
            StringFormat = "{0} modified"
        });
        statusBar.Children.Add(modifiedText);

        // Selected count
        var selectedText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center
        };
        selectedText.Bind(TextBlock.TextProperty, new Binding("SelectedRowCount")
        {
            StringFormat = "{0} selected"
        });
        statusBar.Children.Add(selectedText);

        // Status message
        var statusMessage = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0)
        };
        statusMessage.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        statusBar.Children.Add(statusMessage);

        // Modified cells legend
        var legendPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(24, 0, 0, 0)
        };

        var legendBox = new Border
        {
            Background = ModifiedCellBackground,
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            BorderBrush = new SolidColorBrush(Color.Parse("#8ECDC8")),
            BorderThickness = new Thickness(1)
        };
        legendPanel.Children.Add(legendBox);

        legendPanel.Children.Add(new TextBlock
        {
            Text = "= Modified from vanilla",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center
        });

        statusBar.Children.Add(legendPanel);

        return statusBar;
    }

    private void RegenerateColumns()
    {
        if (_dataGrid == null || _viewModel == null)
        {
            ModkitLog.Warn($"RegenerateColumns: _dataGrid={_dataGrid != null}, _viewModel={_viewModel != null}");
            return;
        }

        try
        {
            ModkitLog.Info($"RegenerateColumns: {_viewModel.VisibleColumns.Count} visible columns, {_viewModel.Rows.Count} rows");
            _dataGrid.Columns.Clear();

            // Add checkbox column for selection
            var checkboxColumn = new DataGridCheckBoxColumn
            {
                Header = "✓",
                Width = new DataGridLength(40),
                Binding = new Binding("IsSelected")
            };
            _dataGrid.Columns.Add(checkboxColumn);

            // Add Name column with direct property binding first (not indexer)
            var nameColumn = new DataGridTextColumn
            {
                Header = "Name",
                Width = new DataGridLength(200),
                Binding = new Binding("Name"),  // Direct property, not indexer
                IsReadOnly = true,
                Foreground = Brushes.White,
                Tag = "name"  // Store field name for edit handling
            };
            _dataGrid.Columns.Add(nameColumn);

            // Add columns for other visible fields (using indexer)
            foreach (var colDef in _viewModel.VisibleColumns)
            {
                // Skip "name" since we added it above
                if (colDef.FieldName.Equals("name", StringComparison.OrdinalIgnoreCase))
                    continue;

                var column = CreateColumnForDefinition(colDef);
                column.Tag = colDef.FieldName;  // Store field name for edit handling
                _dataGrid.Columns.Add(column);
                ModkitLog.Info($"  Added column: {colDef.FieldName} ({colDef.TypeCategory})");
            }

            // Set ItemsSource directly
            _dataGrid.ItemsSource = _viewModel.Rows;
            ModkitLog.Info($"RegenerateColumns complete: {_dataGrid.Columns.Count} columns, {_viewModel.Rows.Count} rows in source");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Failed to generate columns: {ex}");
        }
    }

    private DataGridColumn CreateColumnForDefinition(BulkEditColumnDefinition colDef)
    {
        DataGridColumn column;

        switch (colDef.TypeCategory)
        {
            case FieldTypeCategory.Boolean:
                column = new DataGridCheckBoxColumn
                {
                    Header = colDef.Header,
                    Binding = CreateCellBinding(colDef.FieldName),
                    IsReadOnly = colDef.IsReadOnly
                };
                break;

            case FieldTypeCategory.ComplexArray:
            case FieldTypeCategory.NestedObject:
                // Read-only text display for complex types
                column = new DataGridTemplateColumn
                {
                    Header = colDef.Header,
                    IsReadOnly = true,
                    CellTemplate = CreateComplexTypeTemplate(colDef.FieldName, colDef.TypeCategory)
                };
                break;

            default:
                // Use template column for text fields so we can add tooltips
                column = new DataGridTemplateColumn
                {
                    Header = colDef.Header,
                    IsReadOnly = colDef.IsReadOnly,
                    CellTemplate = CreateTextCellTemplate(colDef.FieldName, colDef.IsReadOnly),
                    CellEditingTemplate = colDef.IsReadOnly ? null : CreateTextEditingTemplate(colDef.FieldName)
                };
                break;
        }

        // Set width
        column.Width = ParseColumnWidth(colDef.Width);

        return column;
    }

    private static Avalonia.Controls.Templates.IDataTemplate CreateTextCellTemplate(string fieldName, bool isReadOnly)
    {
        return new Avalonia.Controls.Templates.FuncDataTemplate<BulkEditRowModel>((row, _) =>
        {
            if (row == null) return new TextBlock();

            var value = row.GetDisplayValue(fieldName);
            var isModified = row.IsFieldModified(fieldName);
            var vanillaValue = row.GetVanillaValue(fieldName);

            // Format the display text - handle JSON and complex types
            var displayText = FormatCellValue(value);
            var tooltipText = FormatCellValueForTooltip(value);

            var textBlock = new TextBlock
            {
                Text = displayText,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // Add teal background for modified cells
            var border = new Border
            {
                Child = textBlock,
                Background = isModified ? ModifiedCellBackground : Brushes.Transparent,
                Padding = new Thickness(2)
            };

            // Add tooltip - show full value and vanilla if modified
            var fullTooltip = tooltipText;
            if (isModified && vanillaValue != null)
            {
                var vanillaDisplay = FormatCellValueForTooltip(vanillaValue);
                fullTooltip = $"{tooltipText}\n\nVanilla: {vanillaDisplay}";
            }
            if (!string.IsNullOrEmpty(fullTooltip) && fullTooltip != displayText)
            {
                ToolTip.SetTip(border, fullTooltip);
            }

            return border;
        });
    }

    private static string FormatCellValue(object? value)
    {
        if (value == null)
            return "";

        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Array => $"[{je.GetArrayLength()} items]",
                System.Text.Json.JsonValueKind.Object => "{...}",
                System.Text.Json.JsonValueKind.String => je.GetString() ?? "",
                System.Text.Json.JsonValueKind.Number => je.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => "",
                _ => je.ToString()
            };
        }

        var str = value.ToString() ?? "";
        // Clean up any escaped newlines for display
        if (str.Contains("\\r") || str.Contains("\\n"))
        {
            str = str.Replace("\\r\\n", " ").Replace("\\r", " ").Replace("\\n", " ");
        }
        return str;
    }

    private static string FormatCellValueForTooltip(object? value)
    {
        if (value == null)
            return "";

        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                // Show array items on separate lines
                var items = new List<string>();
                foreach (var item in je.EnumerateArray())
                {
                    items.Add(item.ValueKind == System.Text.Json.JsonValueKind.String
                        ? item.GetString() ?? ""
                        : item.ToString());
                }
                return string.Join("\n", items);
            }
            if (je.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // Pretty print the object
                return System.Text.Json.JsonSerializer.Serialize(je, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            return je.ToString();
        }

        return value.ToString() ?? "";
    }

    private static Avalonia.Controls.Templates.IDataTemplate CreateTextEditingTemplate(string fieldName)
    {
        return new Avalonia.Controls.Templates.FuncDataTemplate<BulkEditRowModel>((row, _) =>
        {
            var value = row?.GetDisplayValue(fieldName);
            return new TextBox
            {
                Text = value?.ToString() ?? "",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse("#333333")),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2)
            };
        });
    }

    private static Binding CreateCellBinding(string fieldName)
    {
        // Use indexer binding to get values from the BulkEditRowModel
        // The indexer is: public object? this[string fieldName]
        var binding = new Binding
        {
            Path = $"[{fieldName}]",
            Mode = BindingMode.TwoWay
        };
        return binding;
    }

    private static Avalonia.Controls.Templates.IDataTemplate CreateComplexTypeTemplate(
        string fieldName,
        FieldTypeCategory typeCategory)
    {
        return new Avalonia.Controls.Templates.FuncDataTemplate<BulkEditRowModel>((row, _) =>
        {
            var value = row?.GetDisplayValue(fieldName);

            string displayText;
            if (value == null)
            {
                displayText = "(null)";
            }
            else if (typeCategory == FieldTypeCategory.ComplexArray)
            {
                if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    displayText = $"({je.GetArrayLength()} items)";
                }
                else
                {
                    displayText = "(array)";
                }
            }
            else
            {
                displayText = "(object)";
            }

            var textBlock = new TextBlock
            {
                Text = displayText,
                FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0)
            };

            // Add click handler to open modal editor
            var border = new Border
            {
                Background = Brushes.Transparent,
                Child = textBlock,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            border.PointerPressed += (s, e) =>
            {
                // TODO: Open modal editor for complex types
                ModkitLog.Info($"Complex type clicked: {fieldName}");
            };

            return border;
        });
    }

    private static DataGridLength ParseColumnWidth(string width)
    {
        if (width == "*")
            return new DataGridLength(1, DataGridLengthUnitType.Star);

        if (width.EndsWith("*"))
        {
            if (double.TryParse(width[..^1], out var stars))
                return new DataGridLength(stars, DataGridLengthUnitType.Star);
        }

        if (width.ToLowerInvariant() == "auto")
            return DataGridLength.Auto;

        if (double.TryParse(width, out var pixels))
            return new DataGridLength(pixels, DataGridLengthUnitType.Pixel);

        return new DataGridLength(100, DataGridLengthUnitType.Pixel);
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        // Apply modified cell highlighting
        if (e.Row.DataContext is BulkEditRowModel row)
        {
            // Check if row has any modifications and apply subtle background
            if (row.HasAnyModifications)
            {
                e.Row.Background = new SolidColorBrush(Color.Parse("#1A2828"));
            }
        }
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (e.Row.DataContext is not BulkEditRowModel row)
            return;

        if (_viewModel == null)
            return;

        // Get the column's field name from Tag
        var fieldName = e.Column.Tag as string;
        if (string.IsNullOrEmpty(fieldName))
            return;

        // Skip read-only fields
        if (fieldName == "name")
            return;

        // Get the new value from the editing element
        object? newValue = null;
        if (e.EditingElement is TextBox textBox)
        {
            newValue = textBox.Text;
        }
        else if (e.EditingElement is CheckBox checkBox)
        {
            newValue = checkBox.IsChecked ?? false;
        }
        else if (e.EditingElement is ComboBox comboBox)
        {
            newValue = comboBox.SelectedItem?.ToString();
        }

        if (newValue != null)
        {
            _viewModel.UpdateCellValue(row, fieldName, newValue);
        }
    }

    private void OnColumnsButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel == null || sender is not Button button)
            return;

        // Build checkbox list for column selection
        var panel = new StackPanel
        {
            Spacing = 4,
            MinWidth = 200
        };

        foreach (var col in _viewModel.AllColumns)
        {
            var checkBox = new CheckBox
            {
                Content = col.Header,
                IsChecked = col.IsVisible,
                Tag = col.FieldName
            };

            checkBox.IsCheckedChanged += (s, _) =>
            {
                if (s is CheckBox cb && cb.Tag is string fieldName)
                {
                    _viewModel.SetColumnVisibility(fieldName, cb.IsChecked ?? false);
                }
            };

            panel.Children.Add(checkBox);
        }

        // Add an "Apply" button at the bottom to regenerate columns once
        var applyButton = new Button
        {
            Content = "Apply",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Create the flyout
        var flyout = new Flyout
        {
            Content = new ScrollViewer
            {
                MaxHeight = 400,
                Content = new StackPanel
                {
                    Children = { panel, applyButton }
                }
            }
        };

        applyButton.Click += (_, _) =>
        {
            RegenerateColumns();
            flyout.Hide();
        };

        flyout.ShowAt(button);
    }

    private async void OnExportTsvClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var suggestedName = $"{_viewModel.CategoryName.Replace("/", "_")}.tsv";
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export TSV",
            SuggestedFileName = suggestedName,
            DefaultExtension = ".tsv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("TSV Files") { Patterns = new[] { "*.tsv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            try
            {
                var tsv = _viewModel.ExportToTsv();
                await System.IO.File.WriteAllTextAsync(file.Path.LocalPath, tsv);
                _viewModel.StatusMessage = $"Exported {_viewModel.TotalRowCount} rows to {file.Name}";
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    private async void OnImportTsvClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import TSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("TSV Files") { Patterns = new[] { "*.tsv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(files[0].Path.LocalPath);
                var result = _viewModel.ImportFromTsv(content);

                if (result.Success)
                {
                    _viewModel.StatusMessage = $"Imported {result.UpdatedCount} values";
                }
                else
                {
                    // Show errors
                    var errorMsg = string.Join("\n", result.Errors.Take(5));
                    if (result.Errors.Count > 5)
                        errorMsg += $"\n... and {result.Errors.Count - 5} more errors";

                    _viewModel.StatusMessage = $"Import completed with errors. {result.UpdatedCount} values updated.";
                    ModkitLog.Warn($"TSV Import errors:\n{errorMsg}");
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Import failed: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Updates the BulkEditorViewModel with a category node's children.
    /// Call this from the parent view when a category is selected.
    /// </summary>
    public void LoadCategory(
        BulkEditorViewModel viewModel,
        string categoryName,
        string templateType,
        IEnumerable<TreeNodeViewModel> childNodes,
        Func<DataTemplate, Dictionary<string, object?>> convertToProperties,
        Func<string, Dictionary<string, object?>?> getStagingOverrides,
        Func<string, Dictionary<string, object?>?> getPendingChanges)
    {
        _isLoadingData = true;
        _viewModel = viewModel;
        try
        {
            ModkitLog.Info($"BulkEditorPanel.LoadCategory: Loading {categoryName} ({templateType})");
            DataContext = viewModel;

            viewModel.LoadFromTreeNodes(
                categoryName,
                templateType,
                childNodes,
                convertToProperties,
                getStagingOverrides,
                getPendingChanges);

            ModkitLog.Info($"BulkEditorPanel.LoadCategory: Loaded {viewModel.Rows.Count} rows, {viewModel.AllColumns.Count} columns");
            RegenerateColumns();
            ModkitLog.Info($"BulkEditorPanel.LoadCategory: DataGrid has {_dataGrid?.Columns.Count ?? 0} columns");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"BulkEditorPanel.LoadCategory failed: {ex}");
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    /// <summary>
    /// Updates the BulkEditorViewModel with an asset folder's files.
    /// </summary>
    public void LoadAssetFolder(
        BulkEditorViewModel viewModel,
        string folderPath,
        IEnumerable<AssetTreeNode> files,
        Func<AssetTreeNode, bool> hasModifiedReplacement)
    {
        _isLoadingData = true;
        try
        {
            DataContext = viewModel;

            viewModel.LoadFromAssetNodes(folderPath, files, hasModifiedReplacement);

            RegenerateColumns();
        }
        finally
        {
            _isLoadingData = false;
        }
    }
}
