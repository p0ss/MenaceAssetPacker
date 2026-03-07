using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Full-width modal editor for EventHandlers arrays in Skills and Perks.
/// Provides dynamic UI generation based on schema, enum resolution, and JSON toggle view.
/// </summary>
public class EventHandlerEditorDialog : Window
{
    private readonly EventHandlerEditorViewModel _viewModel;
    private readonly ListBox _handlerListBox;
    private readonly StackPanel _editorPanel;
    private readonly ScrollViewer _editorScroll;
    private readonly Button _jsonToggleBtn;
    private bool _jsonViewMode = false;
    private TextBox? _jsonTextBox;

    /// <summary>
    /// The result to return to the caller. Set when Apply is clicked.
    /// </summary>
    public JsonElement? Result { get; private set; }

    private static IBrush GetPrimaryBrush()
    {
        return Application.Current?.FindResource("BrushPrimary") as IBrush
            ?? new SolidColorBrush(Color.Parse("#004f43"));
    }

    public EventHandlerEditorDialog(
        string fieldName,
        JsonElement arrayElement,
        StatsEditorViewModel parentVm)
    {
        Title = $"Edit {fieldName}";
        Width = 900;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = true;
        SystemDecorations = SystemDecorations.Full;

        _viewModel = new EventHandlerEditorViewModel(fieldName, arrayElement, parentVm);

        _handlerListBox = new ListBox();
        _editorPanel = new StackPanel { Spacing = 8 };
        _editorScroll = new ScrollViewer();
        _jsonToggleBtn = new Button { Content = "View as JSON" };

        Content = BuildUI();
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,4,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(8)
        };

        // Left: List of handlers
        var leftPanel = BuildHandlerList();
        mainGrid.Children.Add(leftPanel);
        Grid.SetColumn(leftPanel, 0);
        Grid.SetRow(leftPanel, 0);

        // Splitter
        var splitter = new GridSplitter
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            ResizeDirection = GridResizeDirection.Columns
        };
        mainGrid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);
        Grid.SetRow(splitter, 0);

        // Right: Editor panel
        var rightPanel = BuildEditorPanel();
        mainGrid.Children.Add(rightPanel);
        Grid.SetColumn(rightPanel, 2);
        Grid.SetRow(rightPanel, 0);

        // Bottom: Buttons
        var buttonRow = BuildButtonRow();
        mainGrid.Children.Add(buttonRow);
        Grid.SetColumn(buttonRow, 0);
        Grid.SetColumnSpan(buttonRow, 3);
        Grid.SetRow(buttonRow, 1);

        return mainGrid;
    }

    private Control BuildHandlerList()
    {
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8)
        };

        var stack = new StackPanel { Spacing = 8 };

        stack.Children.Add(new TextBlock
        {
            Text = $"EventHandlers ({_viewModel.Handlers.Count})",
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold
        });

        _handlerListBox.ItemsSource = _viewModel.Handlers;
        _handlerListBox.Background = Brushes.Transparent;
        _handlerListBox.BorderThickness = new Thickness(0);

        _handlerListBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<HandlerItem>(
            (item, _) => BuildHandlerListItem(item));

        _handlerListBox.SelectionChanged += OnHandlerSelected;

        var scrollViewer = new ScrollViewer
        {
            Content = _handlerListBox,
            MaxHeight = 450
        };
        stack.Children.Add(scrollViewer);

        // Add button
        var addButton = new Button
        {
            Content = "+ Add EventHandler",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1)
        };
        addButton.Click += OnAddHandler;
        stack.Children.Add(addButton);

        panel.Child = stack;
        return panel;
    }

    private Control BuildHandlerListItem(HandlerItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 2)
        };

        var indexBlock = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        indexBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Index")
        {
            StringFormat = "[{0}]",
            Source = item
        });
        grid.Children.Add(indexBlock);
        Grid.SetColumn(indexBlock, 0);

        var typeBlock = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        typeBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TypeName")
        {
            Source = item,
            FallbackValue = "(empty)",
            TargetNullValue = "(empty)"
        });
        grid.Children.Add(typeBlock);
        Grid.SetColumn(typeBlock, 1);

        var deleteBtn = new Button
        {
            Content = "×",
            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 16,
            Padding = new Thickness(4, 0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        deleteBtn.Click += (_, _) => OnDeleteHandler(item);
        grid.Children.Add(deleteBtn);
        Grid.SetColumn(deleteBtn, 2);

        return grid;
    }

    private Control BuildEditorPanel()
    {
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12)
        };

        _editorScroll.Content = _editorPanel;
        panel.Child = _editorScroll;
        return panel;
    }

    private void OnHandlerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_handlerListBox.SelectedItem is not HandlerItem item)
        {
            _editorPanel.Children.Clear();
            return;
        }

        _viewModel.SelectedHandler = item;
        RenderHandlerEditor(item);
    }

    private void RefreshListBox()
    {
        // Force ListBox to refresh by temporarily swapping ItemsSource
        var selectedItem = _handlerListBox.SelectedItem;
        var items = _handlerListBox.ItemsSource;
        _handlerListBox.ItemsSource = null;
        _handlerListBox.ItemsSource = items;
        _handlerListBox.SelectedItem = selectedItem;
    }

    private void RenderHandlerEditor(HandlerItem item)
    {
        _editorPanel.Children.Clear();

        if (_jsonViewMode)
        {
            RenderJsonEditor(item);
            return;
        }

        // Title
        _editorPanel.Children.Add(new TextBlock
        {
            Text = $"EventHandler [{item.Index}]",
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // _type dropdown
        _editorPanel.Children.Add(CreateTypeSelector(item));

        // Dynamic fields based on selected type
        if (!string.IsNullOrEmpty(item.TypeName))
        {
            RenderFieldsForType(item);
        }
    }

    private Control CreateTypeSelector(HandlerItem item)
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(new TextBlock
        {
            Text = "_type",
            Foreground = Brushes.White,
            Opacity = 0.8,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        });

        var allTypes = _viewModel.GetAllEventHandlerTypes();

        var comboBox = new ComboBox
        {
            ItemsSource = allTypes,
            SelectedItem = item.TypeName,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            FontSize = 12
        };

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string newType)
            {
                item.TypeName = newType;
                item.Data["_type"] = newType;
                _viewModel.InitializeFieldsForType(item, newType);
                RefreshListBox();
                RenderHandlerEditor(item);
            }
        };

        stack.Children.Add(comboBox);
        return stack;
    }

    private void RenderFieldsForType(HandlerItem item)
    {
        var fields = _viewModel.ParentVm.SchemaService?
            .GetAllEmbeddedClassFields(item.TypeName!);

        if (fields == null || fields.Count == 0)
        {
            _editorPanel.Children.Add(new TextBlock
            {
                Text = "No schema found for this type",
                Foreground = new SolidColorBrush(Color.Parse("#FFB347")),
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(0, 8)
            });
            return;
        }

        // Render each field
        foreach (var field in fields.Where(f => f.Name != "_type"))
        {
            var fieldControl = CreateFieldControl(item, field);
            if (fieldControl != null)
                _editorPanel.Children.Add(fieldControl);
        }
    }

    private Control? CreateFieldControl(HandlerItem item, SchemaService.FieldMeta field)
    {
        var fieldStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4) };

        // Label row with optional info icon
        var labelRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        var label = new TextBlock
        {
            Text = field.Name,
            Foreground = Brushes.White,
            Opacity = 0.8,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelRow.Children.Add(label);

        // Add info icon if description is available
        var description = _viewModel.ParentVm.SchemaService?
            .GetFieldDescription(item.TypeName ?? "", field.Name);

        if (!string.IsNullOrEmpty(description))
        {
            var infoButton = new InfoButton { TooltipText = description };
            labelRow.Children.Add(infoButton);
        }

        fieldStack.Children.Add(labelRow);

        // Get current value
        item.Data.TryGetValue(field.Name, out var currentValue);

        // Create control based on field category
        Control control = field.Category switch
        {
            "enum" => CreateEnumDropdownForField(item, field, currentValue),
            "reference" => CreateReferencePickerForField(item, field, currentValue),
            "collection" => CreateCollectionFieldControl(item, field, currentValue),
            "primitive" => CreatePrimitiveControlForField(item, field, currentValue),
            _ => CreateTextBoxForField(item, field, currentValue)
        };

        fieldStack.Children.Add(control);
        return fieldStack;
    }

    private Control CreateEnumDropdownForField(HandlerItem item, SchemaService.FieldMeta field, object? currentValue)
    {
        var enumTypeName = field.Type;
        var currentIntValue = currentValue is long l ? (int)l : 0;

        var enumValues = _viewModel.ParentVm.SchemaService?.GetEnumValues(enumTypeName);
        if (enumValues == null || enumValues.Count == 0)
        {
            return CreateTextBoxForField(item, field, currentValue);
        }

        var displayItems = enumValues
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new { DisplayText = $"{kvp.Value} ({kvp.Key})", Value = kvp.Key })
            .ToList();

        var selectedItem = displayItems.FirstOrDefault(x => x.Value == currentIntValue);

        var comboBox = new ComboBox
        {
            ItemsSource = displayItems,
            SelectedItem = selectedItem,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            FontSize = 12
        };

        comboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<dynamic>((x, _) =>
            new TextBlock
            {
                Text = x?.DisplayText ?? "",
                Foreground = Brushes.White
            }
        );

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem != null)
            {
                var value = ((dynamic)comboBox.SelectedItem).Value;
                item.Data[field.Name] = (long)value;
            }
        };

        return comboBox;
    }

    private Control CreateReferencePickerForField(HandlerItem item, SchemaService.FieldMeta field, object? currentValue)
    {
        var refType = field.Type;
        var instanceNames = _viewModel.ParentVm.GetTemplateInstanceNames(refType);

        var currentText = currentValue?.ToString() ?? "";

        var autoComplete = new AutoCompleteBox
        {
            Text = currentText,
            ItemsSource = instanceNames,
            FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
            MinimumPrefixLength = 0,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            FontSize = 12
        };

        autoComplete.GetObservable(AutoCompleteBox.TextProperty)
            .Subscribe(text =>
            {
                item.Data[field.Name] = text ?? "";
            });

        return autoComplete;
    }

    private Control CreateCollectionFieldControl(HandlerItem item, SchemaService.FieldMeta field, object? currentValue)
    {
        var stack = new StackPanel { Spacing = 4 };

        // Parse current value as list
        var items = new ObservableCollection<string>();
        if (currentValue is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in je.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.String)
                    items.Add(elem.GetString() ?? "");
            }
        }
        else if (currentValue is List<object> list)
        {
            foreach (var elem in list)
                items.Add(elem?.ToString() ?? "");
        }

        // List display with items
        var listBox = new ListBox
        {
            ItemsSource = items,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            MaxHeight = 150,
            Margin = new Thickness(0, 4)
        };

        stack.Children.Add(listBox);

        // Add/Remove buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        var addButton = new Button
        {
            Content = "+",
            Width = 30,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E"))
        };

        var removeButton = new Button
        {
            Content = "−",
            Width = 30,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E"))
        };

        // Add handler - show list of available items to select from
        addButton.Click += async (_, _) =>
        {
            var elementType = field.ElementType;
            Control selectionControl;
            string? selectedValue = null;

            // Check if element type is a template - if so, show list of instances
            if (!string.IsNullOrEmpty(elementType))
            {
                var availableInstances = _viewModel.ParentVm.GetTemplateInstanceNames(elementType);

                if (availableInstances != null && availableInstances.Count > 0)
                {
                    // Show list of available instances
                    var listBox = new ListBox
                    {
                        ItemsSource = availableInstances,
                        Height = 300,
                        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 8)
                    };

                    listBox.SelectionChanged += (_, _) =>
                    {
                        selectedValue = listBox.SelectedItem?.ToString();
                    };

                    selectionControl = listBox;
                }
                else
                {
                    // Fallback to text input if no instances found
                    var inputBox = new TextBox
                    {
                        Watermark = $"Enter {elementType}...",
                        Width = 300,
                        Margin = new Thickness(0, 8)
                    };

                    inputBox.TextChanged += (_, _) =>
                    {
                        selectedValue = inputBox.Text;
                    };

                    selectionControl = inputBox;
                }
            }
            else
            {
                // No element type specified, use text input
                var inputBox = new TextBox
                {
                    Watermark = "Enter value...",
                    Width = 300,
                    Margin = new Thickness(0, 8)
                };

                inputBox.TextChanged += (_, _) =>
                {
                    selectedValue = inputBox.Text;
                };

                selectionControl = inputBox;
            }

            var okButton = new Button
            {
                Content = "Add",
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = GetPrimaryBrush(),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            var dialog = new Window
            {
                Title = $"Add {elementType ?? "Item"}",
                Width = 400,
                Height = 450,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Select {elementType ?? "item"} to add:",
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.SemiBold
                        },
                        selectionControl,
                        okButton
                    }
                },
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                SystemDecorations = SystemDecorations.Full
            };

            okButton.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(selectedValue) && !items.Contains(selectedValue))
                {
                    items.Add(selectedValue);
                    UpdateCollectionData(item, field.Name, items);
                }
                dialog.Close();
            };

            await dialog.ShowDialog(this);
        };

        // Remove handler
        removeButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is string selected)
            {
                items.Remove(selected);
                UpdateCollectionData(item, field.Name, items);
            }
        };

        buttonPanel.Children.Add(addButton);
        buttonPanel.Children.Add(removeButton);
        stack.Children.Add(buttonPanel);

        return stack;
    }

    private void UpdateCollectionData(HandlerItem item, string fieldName, ObservableCollection<string> items)
    {
        var list = new List<object>(items);
        item.Data[fieldName] = list;
    }

    private Control CreatePrimitiveControlForField(HandlerItem item, SchemaService.FieldMeta field, object? currentValue)
    {
        // Check if it's a boolean
        if (currentValue is bool boolVal)
        {
            var checkBox = new CheckBox
            {
                IsChecked = boolVal,
                Content = boolVal ? "True" : "False",
                Foreground = Brushes.White,
                FontSize = 12
            };

            checkBox.IsCheckedChanged += (s, _) =>
            {
                if (s is CheckBox cb)
                {
                    var isChecked = cb.IsChecked ?? false;
                    cb.Content = isChecked ? "True" : "False";
                    item.Data[field.Name] = isChecked;
                }
            };

            return checkBox;
        }

        return CreateTextBoxForField(item, field, currentValue);
    }

    private Control CreateTextBoxForField(HandlerItem item, SchemaService.FieldMeta field, object? currentValue)
    {
        var textBox = new TextBox
        {
            Text = currentValue?.ToString() ?? "",
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6),
            FontSize = 12
        };

        textBox.TextChanged += (_, _) =>
        {
            var text = textBox.Text ?? "";

            // Try to parse as appropriate type
            if (field.Type.ToLowerInvariant() is "int" or "int32")
            {
                item.Data[field.Name] = long.TryParse(text, out var l) ? l : (object)text;
            }
            else if (field.Type.ToLowerInvariant() is "float" or "single" or "double")
            {
                item.Data[field.Name] = double.TryParse(text, out var d) ? d : (object)text;
            }
            else
            {
                item.Data[field.Name] = text;
            }
        };

        return textBox;
    }

    private void RenderJsonEditor(HandlerItem item)
    {
        _editorPanel.Children.Add(new TextBlock
        {
            Text = "JSON Editor (Advanced)",
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var json = JsonSerializer.Serialize(item.Data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var validationText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
            FontSize = 11,
            Margin = new Thickness(0, 4),
            IsVisible = false
        };

        _jsonTextBox = new TextBox
        {
            Text = json,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            MinHeight = 400
        };

        // Real-time JSON validation
        _jsonTextBox.TextChanged += (_, _) =>
        {
            try
            {
                var text = _jsonTextBox.Text ?? "";
                if (string.IsNullOrWhiteSpace(text))
                {
                    validationText.IsVisible = false;
                    _jsonTextBox.BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E"));
                    return;
                }

                JsonDocument.Parse(text);
                validationText.IsVisible = false;
                _jsonTextBox.BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E"));
            }
            catch (JsonException ex)
            {
                validationText.Text = $"Invalid JSON: {ex.Message}";
                validationText.IsVisible = true;
                _jsonTextBox.BorderBrush = new SolidColorBrush(Color.Parse("#CC4444"));
            }
        };

        _editorPanel.Children.Add(_jsonTextBox);
        _editorPanel.Children.Add(validationText);
    }

    private Control BuildButtonRow()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _jsonToggleBtn.Background = new SolidColorBrush(Color.Parse("#2D2D2D"));
        _jsonToggleBtn.Foreground = Brushes.White;
        _jsonToggleBtn.BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E"));
        _jsonToggleBtn.BorderThickness = new Thickness(1);
        _jsonToggleBtn.Click += (_, _) =>
        {
            _jsonViewMode = !_jsonViewMode;
            _jsonToggleBtn.Content = _jsonViewMode ? "View as Form" : "View as JSON";
            if (_viewModel.SelectedHandler != null)
                RenderHandlerEditor(_viewModel.SelectedHandler);
        };
        panel.Children.Add(_jsonToggleBtn);

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1)
        };
        cancelBtn.Click += (_, _) =>
        {
            Result = null;
            Close();
        };
        panel.Children.Add(cancelBtn);

        var applyBtn = new Button
        {
            Content = "Apply",
            Background = GetPrimaryBrush(),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        applyBtn.Click += async (_, _) =>
        {
            try
            {
                // Sync JSON edits back to data if in JSON mode
                if (_jsonViewMode && _jsonTextBox != null && _viewModel.SelectedHandler != null)
                {
                    try
                    {
                        var text = _jsonTextBox.Text ?? "{}";
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(text);
                        if (parsed != null)
                            _viewModel.SelectedHandler.Data = parsed;
                    }
                    catch (JsonException ex)
                    {
                        // Show error dialog
                        var errorDialog = new Window
                        {
                            Title = "Invalid JSON",
                            Width = 400,
                            Height = 200,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                            SystemDecorations = SystemDecorations.Full
                        };

                        var panel = new StackPanel
                        {
                            Margin = new Thickness(16),
                            Spacing = 12
                        };

                        panel.Children.Add(new TextBlock
                        {
                            Text = "Cannot apply changes - JSON is invalid:",
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.SemiBold
                        });

                        panel.Children.Add(new TextBlock
                        {
                            Text = ex.Message,
                            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
                            TextWrapping = TextWrapping.Wrap
                        });

                        var okBtn = new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Background = GetPrimaryBrush(),
                            Foreground = Brushes.White,
                            BorderThickness = new Thickness(0)
                        };
                        okBtn.Click += (_, _) => errorDialog.Close();
                        panel.Children.Add(okBtn);

                        errorDialog.Content = panel;
                        await errorDialog.ShowDialog(this);
                        return;
                    }
                }

                var result = _viewModel.BuildResult();

                // Set the Result property so the parent can access it
                Result = result;
                Close();
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[EventHandlerEditor] Error in Apply: {ex.Message}");
                ModkitLog.Error($"[EventHandlerEditor] Stack trace: {ex.StackTrace}");
            }
        };
        panel.Children.Add(applyBtn);

        return panel;
    }

    private void OnAddHandler(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var newHandler = new HandlerItem
        {
            Index = _viewModel.Handlers.Count,
            TypeName = null,
            Data = new Dictionary<string, object?>()
        };

        _viewModel.Handlers.Add(newHandler);

        // Reindex handlers
        for (int i = 0; i < _viewModel.Handlers.Count; i++)
        {
            _viewModel.Handlers[i].Index = i;
        }

        RefreshListBox();
        _handlerListBox.SelectedItem = newHandler;
    }

    private void OnDeleteHandler(HandlerItem item)
    {
        // Store current index before removal
        var index = _viewModel.Handlers.IndexOf(item);
        if (index < 0)
            return; // Item not found

        // Temporarily disable selection events to prevent re-render during deletion
        _handlerListBox.SelectionChanged -= OnHandlerSelected;

        // Clear editor first to avoid rendering issues
        _editorPanel.Children.Clear();

        // Remove the item
        _viewModel.Handlers.Remove(item);

        // Reindex handlers
        for (int i = 0; i < _viewModel.Handlers.Count; i++)
        {
            _viewModel.Handlers[i].Index = i;
        }

        // Re-enable selection events
        _handlerListBox.SelectionChanged += OnHandlerSelected;

        // Select next item safely
        if (_viewModel.Handlers.Count > 0)
        {
            var newIndex = Math.Min(index, _viewModel.Handlers.Count - 1);
            var nextItem = _viewModel.Handlers[newIndex];
            _handlerListBox.SelectedItem = nextItem;

            // Manually trigger render since we disabled events
            _viewModel.SelectedHandler = nextItem;
            RenderHandlerEditor(nextItem);
        }
        else
        {
            // No handlers left, just show empty state
            _editorPanel.Children.Clear();
        }
    }
}
