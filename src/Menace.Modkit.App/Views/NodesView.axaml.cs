#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.Styles;
using Menace.Modkit.App.ViewModels;
using Menace.Modkit.App.VisualEditor;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Nodes view: free-form visual editor for game logic.
/// </summary>
public class NodesView : UserControl
{
    private TimelineCanvasView? _canvas;
    private StackPanel? _propertiesPanel;
    private TextBlock? _codePreview;
    private SchemaService? _schemaService;
    private TimelineNode? _currentNode;
    private ComboBox? _modpackCombo;

    public NodesView()
    {
        Content = BuildUI();

        // Wire up schema service and modpacks when DataContext changes
        DataContextChanged += (_, _) =>
        {
            if (DataContext is NodesViewModel vm)
            {
                _schemaService = vm.SchemaService;
                if (_canvas != null)
                {
                    _canvas.SchemaService = _schemaService;
                }

                // Populate modpack dropdown
                RefreshModpackDropdown(vm);
            }
        };

        // Generate initial code after layout
        Loaded += (_, _) => RegenerateCode();
    }

    private void RefreshModpackDropdown(NodesViewModel vm)
    {
        if (_modpackCombo == null) return;

        _modpackCombo.ItemsSource = vm.AvailableModpacks;
        if (vm.SelectedModpack != null)
        {
            _modpackCombo.SelectedItem = vm.SelectedModpack;
        }
        else if (vm.AvailableModpacks.Count > 1)
        {
            _modpackCombo.SelectedIndex = 1; // Select first real mod
        }
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            // Toolbar, Content, Splitter, Code preview (resizable)
            RowDefinitions = new RowDefinitions("Auto,*,Auto,180"),
            Background = ThemeColors.BrushBgWindow
        };

        // Toolbar
        var toolbar = BuildToolbar();
        mainGrid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 0);

        // Main content: canvas + properties
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,280")
        };

        _canvas = new TimelineCanvasView();
        _canvas.SelectionChanged += OnSelectionChanged;
        _canvas.GraphChanged += OnGraphChanged;
        contentGrid.Children.Add(_canvas);
        Grid.SetColumn(_canvas, 0);

        var propertiesPanel = BuildPropertiesPanel();
        contentGrid.Children.Add(propertiesPanel);
        Grid.SetColumn(propertiesPanel, 1);

        mainGrid.Children.Add(contentGrid);
        Grid.SetRow(contentGrid, 1);

        // Draggable splitter between content and code preview
        var splitter = new GridSplitter
        {
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ThemeColors.BrushBorder,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.SizeNorthSouth)
        };
        mainGrid.Children.Add(splitter);
        Grid.SetRow(splitter, 2);

        // Code preview (resizable via splitter)
        var codePanel = BuildCodePreview();
        mainGrid.Children.Add(codePanel);
        Grid.SetRow(codePanel, 3);

        return mainGrid;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            Padding = new Thickness(12, 8),
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // Add node dropdowns
        panel.Children.Add(CreateNodeDropdown("+ Trigger", new[]
        {
            ("On Skill Used", TimelineNodeType.TriggerSkillUsed),
            ("On Damage", TimelineNodeType.TriggerDamageReceived),
            ("On Kill", TimelineNodeType.TriggerActorKilled),
            ("On Move Start", TimelineNodeType.TriggerMovementStarted),
            ("On Turn Start", TimelineNodeType.TriggerTurnStart),
            ("On Turn End", TimelineNodeType.TriggerTurnEnd),
            ("On Round Start", TimelineNodeType.TriggerRoundStart),
            ("On Round End", TimelineNodeType.TriggerRoundEnd),
        }));

        panel.Children.Add(CreateNodeDropdown("+ Condition", new[]
        {
            ("If", TimelineNodeType.Condition),
            ("Compare", TimelineNodeType.Compare),
            ("NOT", TimelineNodeType.Not),
            ("AND", TimelineNodeType.And),
            ("OR", TimelineNodeType.Or),
        }));

        panel.Children.Add(CreateNodeDropdown("+ Data", new[]
        {
            ("Actor", TimelineNodeType.Actor),
            ("Skill", TimelineNodeType.Skill),
            ("Tile", TimelineNodeType.Tile),
            ("---", TimelineNodeType.Actor),  // Separator marker (will be handled specially)
            ("Actor Template", TimelineNodeType.TemplateRefActor),
            ("Skill Template", TimelineNodeType.TemplateRefSkill),
            ("Item Template", TimelineNodeType.TemplateRefItem),
            ("---", TimelineNodeType.Skill),  // Separator marker
            ("Get Variable", TimelineNodeType.GetVariable),
            ("Set Variable", TimelineNodeType.SetVariable),
        }));

        panel.Children.Add(CreateNodeDropdown("+ Effect/Action", new[]
        {
            ("Effect", TimelineNodeType.Effect),
            ("Damage", TimelineNodeType.ActionApplyDamage),
            ("Heal", TimelineNodeType.ActionHeal),
            ("Move To", TimelineNodeType.ActionMoveTo),
            ("Spawn", TimelineNodeType.ActionSpawn),
            ("Kill", TimelineNodeType.ActionKill),
            ("Log", TimelineNodeType.ActionLog),
        }));

        panel.Children.Add(CreateNodeDropdown("+ Loop", new[]
        {
            ("Each Actor", TimelineNodeType.ForEachActor),
            ("Tiles In Radius", TimelineNodeType.ForEachTileInRadius),
            ("Each Skill", TimelineNodeType.ForEachSkill),
        }));

        panel.Children.Add(CreateNodeDropdown("+ Timing", new[]
        {
            ("Delay", TimelineNodeType.Delay),
            ("Repeat", TimelineNodeType.Repeat),
            ("Once", TimelineNodeType.Once),
        }));

        panel.Children.Add(CreateNodeDropdown("+ State", new[]
        {
            ("State", TimelineNodeType.State),
            ("Transition", TimelineNodeType.Transition),
            ("Get State", TimelineNodeType.GetState),
            ("Set State", TimelineNodeType.SetState),
        }));

        // Separator
        panel.Children.Add(new Border
        {
            Width = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(8, 0)
        });

        // Delete button
        var deleteBtn = new Button { Content = "Delete", FontSize = 11 };
        deleteBtn.Click += (_, _) => _canvas?.DeleteSelected();
        panel.Children.Add(deleteBtn);

        // Refresh code button (code auto-generates, this is just for manual refresh)
        var generateBtn = new Button
        {
            Content = "Refresh Code",
            FontSize = 11
        };
        generateBtn.Click += OnGenerateClick;
        panel.Children.Add(generateBtn);

        // Separator
        panel.Children.Add(new Border
        {
            Width = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(8, 0)
        });

        // Mod selection dropdown
        panel.Children.Add(new TextBlock
        {
            Text = "Mod:",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 4, 0)
        });

        _modpackCombo = new ComboBox { FontSize = 11, MinWidth = 150 };
        _modpackCombo.SelectionChanged += OnModpackSelectionChanged;
        panel.Children.Add(_modpackCombo);

        // Save to mod button
        var saveBtn = new Button
        {
            Content = "Save to Mod",
            FontSize = 11
        };
        saveBtn.Click += OnSaveToModClick;
        panel.Children.Add(saveBtn);

        // Load from mod button
        var loadFromModBtn = new Button
        {
            Content = "Load from Mod",
            FontSize = 11
        };
        loadFromModBtn.Click += OnLoadFromModClick;
        panel.Children.Add(loadFromModBtn);

        // Separator
        panel.Children.Add(new Border
        {
            Width = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(8, 0)
        });

        // Export to file button (for standalone save)
        var exportBtn = new Button
        {
            Content = "Export",
            FontSize = 11
        };
        exportBtn.Click += OnSaveClick;
        panel.Children.Add(exportBtn);

        // Import from file button
        var importBtn = new Button
        {
            Content = "Import",
            FontSize = 11
        };
        importBtn.Click += OnLoadClick;
        panel.Children.Add(importBtn);

        toolbar.Child = panel;
        return toolbar;
    }

    private Control CreateNodeDropdown(string label, (string name, TimelineNodeType type)[] options)
    {
        var button = new Button { Content = label, FontSize = 11 };

        var menu = new ContextMenu();
        foreach (var (name, type) in options)
        {
            // Handle separator markers
            if (name == "---")
            {
                menu.Items.Add(new Separator());
                continue;
            }

            var item = new MenuItem { Header = name };
            var capturedType = type;
            item.Click += (_, _) => _canvas?.AddNode(capturedType);
            menu.Items.Add(item);
        }

        button.Click += (sender, _) =>
        {
            if (sender is Button btn)
            {
                menu.PlacementTarget = btn;
                menu.Open(btn);
            }
        };

        return button;
    }

    private Control BuildPropertiesPanel()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(12)
        };

        var scroll = new ScrollViewer();

        _propertiesPanel = new StackPanel { Spacing = 12 };

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "PROPERTIES",
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = ThemeColors.BrushTextSecondary
        });

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Select a node to edit properties",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextMuted,
            FontStyle = FontStyle.Italic
        });

        scroll.Content = _propertiesPanel;
        border.Child = scroll;
        return border;
    }

    private Control BuildCodePreview()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 0, 0),  // Splitter provides visual separation
            Padding = new Thickness(12, 8),
            MinHeight = 60  // Minimum height when resizing
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        grid.Children.Add(new TextBlock
        {
            Text = "CODE PREVIEW",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextMuted,
            VerticalAlignment = VerticalAlignment.Top
        });
        Grid.SetColumn((Control)grid.Children[0], 0);

        _codePreview = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Monaco, monospace"),
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 0)
        };
        var scroll = new ScrollViewer { Content = _codePreview };
        grid.Children.Add(scroll);
        Grid.SetColumn(scroll, 1);

        var copyBtn = new Button
        {
            Content = "Copy",
            FontSize = 10,
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Top
        };
        copyBtn.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null && _codePreview != null)
            {
                await clipboard.SetTextAsync(_codePreview.Text ?? "");
            }
        };
        grid.Children.Add(copyBtn);
        Grid.SetColumn(copyBtn, 2);

        border.Child = grid;
        return border;
    }

    private void OnSelectionChanged(object? sender, TimelineNode? node)
    {
        _currentNode = node;
        RefreshPropertiesPanel();
    }

    private void RefreshPropertiesPanel()
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Clear();

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "PROPERTIES",
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = ThemeColors.BrushTextSecondary
        });

        if (_currentNode == null)
        {
            _propertiesPanel.Children.Add(new TextBlock
            {
                Text = "Select a node to edit properties",
                FontSize = 11,
                Foreground = ThemeColors.BrushTextMuted,
                FontStyle = FontStyle.Italic
            });
            return;
        }

        var node = _currentNode;

        // Node type
        _propertiesPanel.Children.Add(CreatePropertyRow("Type", node.NodeType.ToString()));

        // Title (editable)
        var titleBox = new TextBox { Text = node.Title, FontSize = 11 };
        titleBox.LostFocus += (_, _) =>
        {
            node.Title = titleBox.Text ?? "";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Title", titleBox));

        // Position
        _propertiesPanel.Children.Add(CreatePropertyRow("Position", $"{node.X:F0}, {node.Y:F0}"));

        // Separator
        _propertiesPanel.Children.Add(new Border
        {
            Height = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(0, 8)
        });

        // Node-specific properties
        BuildNodeProperties(node);
    }

    private void BuildNodeProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        switch (node.NodeType)
        {
            case TimelineNodeType.Effect:
                BuildEffectProperties(node);
                break;

            case TimelineNodeType.ActionApplyDamage:
            case TimelineNodeType.ActionHeal:
            case TimelineNodeType.ActionApplySuppression:
                BuildAmountProperty(node);
                break;

            case TimelineNodeType.Compare:
                BuildCompareProperties(node);
                break;

            case TimelineNodeType.And:
            case TimelineNodeType.Or:
                BuildLogicGateProperties(node);
                break;

            case TimelineNodeType.Not:
                BuildNotProperties(node);
                break;

            case TimelineNodeType.Condition:
                BuildConditionProperties(node);
                break;

            case TimelineNodeType.MathOp:
                BuildMathProperties(node);
                break;

            case TimelineNodeType.SetVariable:
            case TimelineNodeType.GetVariable:
                BuildVariableProperties(node);
                break;

            case TimelineNodeType.ForEachActor:
                BuildForEachActorProperties(node);
                break;

            case TimelineNodeType.ActionLog:
                BuildLogProperties(node);
                break;

            case TimelineNodeType.ActionSpawn:
                BuildSpawnProperties(node);
                break;

            case TimelineNodeType.TemplateRefActor:
            case TimelineNodeType.TemplateRefSkill:
            case TimelineNodeType.TemplateRefItem:
                BuildTemplateRefProperties(node);
                break;

            case TimelineNodeType.Delay:
                BuildDelayProperties(node);
                break;

            case TimelineNodeType.Repeat:
                BuildRepeatProperties(node);
                break;

            case TimelineNodeType.Once:
                BuildOnceProperties(node);
                break;

            case TimelineNodeType.State:
                BuildStateProperties(node);
                break;

            case TimelineNodeType.Transition:
                BuildTransitionProperties(node);
                break;

            case TimelineNodeType.GetState:
                BuildGetStateProperties(node);
                break;

            case TimelineNodeType.SetState:
                BuildSetStateProperties(node);
                break;

            default:
                // Generic property display
                foreach (var kvp in node.Properties)
                {
                    var valueBox = new TextBox { Text = kvp.Value?.ToString() ?? "", FontSize = 11 };
                    var key = kvp.Key;
                    valueBox.TextChanged += (_, _) =>
                    {
                        if (int.TryParse(valueBox.Text, out var intVal))
                            node.Properties[key] = intVal;
                        else
                            node.Properties[key] = valueBox.Text;
                    };
                    _propertiesPanel.Children.Add(CreatePropertyRow(key, valueBox));
                }
                break;
        }
    }

    private void BuildEffectProperties(TimelineNode node)
    {
        if (_propertiesPanel == null || _canvas == null) return;

        // Get resolved type info for the actor input to display connection status
        var (baseType, templateId) = _canvas.GetResolvedTypeInfo(node, "actor");
        var hasConnection = baseType != "Any";
        var hasSpecificTemplate = !string.IsNullOrEmpty(templateId);

        // Show connection status
        if (hasConnection)
        {
            var connectionInfo = hasSpecificTemplate
                ? $"{baseType}: {templateId}"
                : $"{baseType} (generic)";
            _propertiesPanel.Children.Add(CreatePropertyRow("Connected", connectionInfo));
        }

        // Property dropdown - schema-driven based on resolved type
        var propertyCombo = new ComboBox { FontSize = 11, MinWidth = 120 };

        // Get modifiable fields from schema using resolved type
        var fields = GetModifiableFieldsForNode(node);
        if (fields.Count > 0)
        {
            foreach (var field in fields)
            {
                propertyCombo.Items.Add(field);
            }
        }
        else
        {
            // Fallback common properties when no schema fields available
            propertyCombo.Items.Add("concealment");
            propertyCombo.Items.Add("armor");
            propertyCombo.Items.Add("damage");
            propertyCombo.Items.Add("range");
            propertyCombo.Items.Add("accuracy");
            propertyCombo.Items.Add("critChance");
            propertyCombo.Items.Add("actionPoints");
            propertyCombo.Items.Add("moveSpeed");
        }

        var currentProp = node.Properties.TryGetValue("property", out var propVal) ? propVal?.ToString() : "";
        propertyCombo.SelectedItem = currentProp;
        if (propertyCombo.SelectedItem == null && propertyCombo.ItemCount > 0)
            propertyCombo.SelectedIndex = 0;

        propertyCombo.SelectionChanged += (_, _) =>
        {
            node.Properties["property"] = propertyCombo.SelectedItem?.ToString() ?? "";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Property", propertyCombo));

        // Modifier value
        var modifierBox = new TextBox
        {
            Text = node.Properties.TryGetValue("modifier", out var modVal) ? modVal?.ToString() : "-3",
            FontSize = 11,
            Width = 60
        };
        modifierBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(modifierBox.Text, out var val))
            {
                node.Properties["modifier"] = val;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Modifier", modifierBox));

        // Duration
        var durationBox = new TextBox
        {
            Text = node.Properties.TryGetValue("duration", out var durVal) ? durVal?.ToString() : "1",
            FontSize = 11,
            Width = 60
        };
        durationBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(durationBox.Text, out var val))
            {
                node.Properties["duration"] = val;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Duration (rounds)", durationBox));

        // Help text based on connection state
        string helpText;
        if (!hasConnection)
        {
            helpText = "Connect an Actor or Template to see available fields";
        }
        else if (fields.Count > 0)
        {
            helpText = $"{fields.Count} modifiable fields available";
        }
        else
        {
            helpText = "Using common fields (schema not loaded)";
        }

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = helpText,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void BuildAmountProperty(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var amountBox = new TextBox
        {
            Text = node.Properties.TryGetValue("amount", out var val) ? val?.ToString() : "10",
            FontSize = 11,
            Width = 60
        };
        amountBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(amountBox.Text, out var v))
            {
                node.Properties["amount"] = v;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Amount", amountBox));
    }

    private void BuildCompareProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var opCombo = new ComboBox { FontSize = 11 };
        opCombo.Items.Add("==");
        opCombo.Items.Add("!=");
        opCombo.Items.Add(">");
        opCombo.Items.Add("<");
        opCombo.Items.Add(">=");
        opCombo.Items.Add("<=");

        var currentOp = node.Properties.TryGetValue("operator", out var opVal) ? opVal?.ToString() : "==";
        opCombo.SelectedItem = currentOp;

        opCombo.SelectionChanged += (_, _) =>
        {
            node.Properties["operator"] = opCombo.SelectedItem?.ToString() ?? "==";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Operator", opCombo));
    }

    private void BuildLogicGateProperties(TimelineNode node)
    {
        if (_propertiesPanel == null || _canvas == null) return;

        var isAnd = node.NodeType == TimelineNodeType.And;
        var gateType = isAnd ? "AND" : "OR";

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = isAnd
                ? "Outputs TRUE when both inputs are TRUE"
                : "Outputs TRUE when either input is TRUE",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        // Show connection status for inputs
        var inputA = GetConnectedNodeDescription(node, "a");
        var inputB = GetConnectedNodeDescription(node, "b");

        _propertiesPanel.Children.Add(CreatePropertyRow("Input A", inputA ?? "(not connected)"));
        _propertiesPanel.Children.Add(CreatePropertyRow("Input B", inputB ?? "(not connected)"));

        // Help text
        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = $"Connect two boolean outputs to the '{gateType}' inputs",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void BuildNotProperties(TimelineNode node)
    {
        if (_propertiesPanel == null || _canvas == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Inverts a boolean: TRUE becomes FALSE, FALSE becomes TRUE",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        // Show connection status
        var input = GetConnectedNodeDescription(node, "in");
        _propertiesPanel.Children.Add(CreatePropertyRow("Input", input ?? "(not connected)"));

        // Help text
        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Connect a boolean output to the 'NOT' input",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void BuildConditionProperties(TimelineNode node)
    {
        if (_propertiesPanel == null || _canvas == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Routes execution based on a condition. If TRUE, the 'pass' output triggers. If FALSE, the 'fail' output triggers.",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        // Show connection status
        var condition = GetConnectedNodeDescription(node, "value");
        _propertiesPanel.Children.Add(CreatePropertyRow("Condition", condition ?? "(not connected)"));

        // Help text
        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Connect a boolean (from Compare, NOT, AND, OR nodes, or skill/actor properties like 'isAttack')",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private string? GetConnectedNodeDescription(TimelineNode node, string inputName)
    {
        if (_canvas == null) return null;

        // Find connection to the specified input port on this node
        var connection = _canvas.Graph.Connections.FirstOrDefault(c =>
            c.TargetNode?.Id == node.Id && c.TargetPort?.Name == inputName);
        if (connection == null) return null;

        // Get the source node and port
        var sourceNode = connection.SourceNode;
        var sourcePort = connection.SourcePort;
        if (sourceNode == null || sourcePort == null) return null;

        return $"{sourceNode.Title}.{sourcePort.Name}";
    }

    private void BuildMathProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var opCombo = new ComboBox { FontSize = 11 };
        opCombo.Items.Add("+");
        opCombo.Items.Add("-");
        opCombo.Items.Add("*");
        opCombo.Items.Add("/");

        var currentOp = node.Properties.TryGetValue("operator", out var opVal) ? opVal?.ToString() : "+";
        opCombo.SelectedItem = currentOp;

        opCombo.SelectionChanged += (_, _) =>
        {
            node.Properties["operator"] = opCombo.SelectedItem?.ToString() ?? "+";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Operator", opCombo));
    }

    private void BuildVariableProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var nameBox = new TextBox
        {
            Text = node.Properties.TryGetValue("name", out var val) ? val?.ToString() : "myVar",
            FontSize = 11
        };
        nameBox.LostFocus += (_, _) =>
        {
            node.Properties["name"] = nameBox.Text ?? "myVar";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Variable Name", nameBox));
    }

    private void BuildForEachActorProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var filterCombo = new ComboBox { FontSize = 11 };
        filterCombo.Items.Add("all");
        filterCombo.Items.Add("player");
        filterCombo.Items.Add("enemy");

        var currentFilter = node.Properties.TryGetValue("filter", out var filterVal) ? filterVal?.ToString() : "all";
        filterCombo.SelectedItem = currentFilter;

        filterCombo.SelectionChanged += (_, _) =>
        {
            node.Properties["filter"] = filterCombo.SelectedItem?.ToString() ?? "all";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Filter", filterCombo));
    }

    private void BuildLogProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var msgBox = new TextBox
        {
            Text = node.Properties.TryGetValue("message", out var val) ? val?.ToString() : "Debug message",
            FontSize = 11
        };
        msgBox.LostFocus += (_, _) =>
        {
            node.Properties["message"] = msgBox.Text ?? "";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Message", msgBox));
    }

    private void BuildSpawnProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        var templateBox = new TextBox
        {
            Text = node.Properties.TryGetValue("template", out var val) ? val?.ToString() : "",
            FontSize = 11
        };
        templateBox.LostFocus += (_, _) =>
        {
            node.Properties["template"] = templateBox.Text ?? "";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Template ID", templateBox));
    }

    private void BuildTemplateRefProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        // Get template type from node properties
        var templateType = node.Properties.TryGetValue("templateType", out var typeVal)
            ? typeVal?.ToString() ?? "ActorTemplate"
            : "ActorTemplate";

        // Display the template type
        _propertiesPanel.Children.Add(CreatePropertyRow("Template Type", templateType));

        // Create ComboBox for template selection
        var templateCombo = new ComboBox { FontSize = 11, MinWidth = 140 };

        // Get template instance IDs from ViewModel
        var templateIds = new List<string>();
        if (DataContext is NodesViewModel vm && vm.HasVanillaData)
        {
            templateIds = vm.GetTemplateInstanceIds(templateType);
        }

        // Add placeholder if no templates available
        if (templateIds.Count == 0)
        {
            templateCombo.Items.Add("(no templates found)");
            templateCombo.IsEnabled = false;

            // Show help message
            _propertiesPanel.Children.Add(new TextBlock
            {
                Text = "Extract game data in Settings to see available templates",
                FontSize = 10,
                Foreground = ThemeColors.BrushTextMuted,
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            // Populate dropdown with available templates
            foreach (var templateId in templateIds)
            {
                templateCombo.Items.Add(templateId);
            }

            // Select current value
            var currentId = node.Properties.TryGetValue("templateId", out var idVal)
                ? idVal?.ToString()
                : "";
            if (!string.IsNullOrEmpty(currentId) && templateIds.Contains(currentId))
            {
                templateCombo.SelectedItem = currentId;
            }
            else if (templateCombo.ItemCount > 0)
            {
                templateCombo.SelectedIndex = 0;
                // Update node property with first item
                node.Properties["templateId"] = templateCombo.SelectedItem?.ToString() ?? "";
            }

            templateCombo.SelectionChanged += (_, _) =>
            {
                var selected = templateCombo.SelectedItem?.ToString() ?? "";
                node.Properties["templateId"] = selected;

                // Update node title to show selected template
                node.Title = $"{GetTemplateTypeDisplayName(templateType)}: {selected}";

                RegenerateCode();
            };

            // Show count
            _propertiesPanel.Children.Add(new TextBlock
            {
                Text = $"{templateIds.Count} templates available",
                FontSize = 10,
                Foreground = ThemeColors.BrushTextMuted,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        _propertiesPanel.Children.Add(CreatePropertyRow("Template", templateCombo));
    }

    private void BuildDelayProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Delays execution by a number of rounds",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var roundsBox = new TextBox
        {
            Text = node.Properties.TryGetValue("rounds", out var val) ? val?.ToString() : "1",
            FontSize = 11,
            Width = 60
        };
        roundsBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(roundsBox.Text, out var v))
            {
                node.Properties["rounds"] = v;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Delay (rounds)", roundsBox));
    }

    private void BuildRepeatProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Repeats execution at an interval",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var intervalBox = new TextBox
        {
            Text = node.Properties.TryGetValue("interval", out var intVal) ? intVal?.ToString() : "1",
            FontSize = 11,
            Width = 60
        };
        intervalBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(intervalBox.Text, out var v))
            {
                node.Properties["interval"] = v;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Interval (rounds)", intervalBox));

        var countBox = new TextBox
        {
            Text = node.Properties.TryGetValue("count", out var cntVal) ? cntVal?.ToString() : "3",
            FontSize = 11,
            Width = 60
        };
        countBox.LostFocus += (_, _) =>
        {
            if (int.TryParse(countBox.Text, out var v))
            {
                node.Properties["count"] = v;
                RegenerateCode();
            }
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Count (0=infinite)", countBox));
    }

    private void BuildOnceProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Executes only once per entity or per combat",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var scopeCombo = new ComboBox { FontSize = 11 };
        scopeCombo.Items.Add("entity");
        scopeCombo.Items.Add("combat");

        var currentScope = node.Properties.TryGetValue("scope", out var scopeVal) ? scopeVal?.ToString() : "entity";
        scopeCombo.SelectedItem = currentScope;

        scopeCombo.SelectionChanged += (_, _) =>
        {
            node.Properties["scope"] = scopeCombo.SelectedItem?.ToString() ?? "entity";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Scope", scopeCombo));

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "entity: Once per actor\ncombat: Once per mission",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private void BuildStateProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Defines a state in a state machine",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var machineBox = new TextBox
        {
            Text = node.Properties.TryGetValue("machine", out var machVal) ? machVal?.ToString() : "default",
            FontSize = 11
        };
        machineBox.LostFocus += (_, _) =>
        {
            node.Properties["machine"] = machineBox.Text ?? "default";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Machine", machineBox));

        var nameBox = new TextBox
        {
            Text = node.Properties.TryGetValue("name", out var nameVal) ? nameVal?.ToString() : "idle",
            FontSize = 11
        };
        nameBox.LostFocus += (_, _) =>
        {
            node.Properties["name"] = nameBox.Text ?? "idle";
            node.Title = $"State: {nameBox.Text}";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("State Name", nameBox));
    }

    private void BuildTransitionProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Transitions between states when condition is true",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var machineBox = new TextBox
        {
            Text = node.Properties.TryGetValue("machine", out var machVal) ? machVal?.ToString() : "default",
            FontSize = 11
        };
        machineBox.LostFocus += (_, _) =>
        {
            node.Properties["machine"] = machineBox.Text ?? "default";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Machine", machineBox));

        var fromBox = new TextBox
        {
            Text = node.Properties.TryGetValue("from", out var fromVal) ? fromVal?.ToString() : "idle",
            FontSize = 11
        };
        fromBox.LostFocus += (_, _) =>
        {
            node.Properties["from"] = fromBox.Text ?? "idle";
            node.Title = $"{fromBox.Text} → {node.Properties.GetValueOrDefault("to", "?")}";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("From State", fromBox));

        var toBox = new TextBox
        {
            Text = node.Properties.TryGetValue("to", out var toVal) ? toVal?.ToString() : "active",
            FontSize = 11
        };
        toBox.LostFocus += (_, _) =>
        {
            node.Properties["to"] = toBox.Text ?? "active";
            node.Title = $"{node.Properties.GetValueOrDefault("from", "?")} → {toBox.Text}";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("To State", toBox));
    }

    private void BuildGetStateProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Gets the current state of an entity",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var machineBox = new TextBox
        {
            Text = node.Properties.TryGetValue("machine", out var machVal) ? machVal?.ToString() : "default",
            FontSize = 11
        };
        machineBox.LostFocus += (_, _) =>
        {
            node.Properties["machine"] = machineBox.Text ?? "default";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Machine", machineBox));

        var checkBox = new TextBox
        {
            Text = node.Properties.TryGetValue("checkState", out var checkVal) ? checkVal?.ToString() : "",
            FontSize = 11
        };
        checkBox.LostFocus += (_, _) =>
        {
            node.Properties["checkState"] = checkBox.Text ?? "";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Check State", checkBox));

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "If 'Check State' is set, 'isState' output is true when current state matches",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void BuildSetStateProperties(TimelineNode node)
    {
        if (_propertiesPanel == null) return;

        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Sets the state of an entity directly",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var machineBox = new TextBox
        {
            Text = node.Properties.TryGetValue("machine", out var machVal) ? machVal?.ToString() : "default",
            FontSize = 11
        };
        machineBox.LostFocus += (_, _) =>
        {
            node.Properties["machine"] = machineBox.Text ?? "default";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("Machine", machineBox));

        var stateBox = new TextBox
        {
            Text = node.Properties.TryGetValue("state", out var stateVal) ? stateVal?.ToString() : "idle",
            FontSize = 11
        };
        stateBox.LostFocus += (_, _) =>
        {
            node.Properties["state"] = stateBox.Text ?? "idle";
            node.Title = $"Set State: {stateBox.Text}";
            RegenerateCode();
        };
        _propertiesPanel.Children.Add(CreatePropertyRow("State", stateBox));
    }

    private static string GetTemplateTypeDisplayName(string templateType)
    {
        return templateType switch
        {
            "ActorTemplate" => "Actor",
            "SkillTemplate" => "Skill",
            "ItemTemplate" => "Item",
            _ => templateType.Replace("Template", "")
        };
    }

    private List<string> GetModifiableFieldsForNode(TimelineNode node)
    {
        if (_schemaService == null || _canvas == null)
            return new List<string>();

        return _canvas.GetModifiableFields(node);
    }

    private Control CreatePropertyRow(string label, string value)
    {
        return CreatePropertyRow(label, new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = ThemeColors.BrushTextPrimary
        });
    }

    private Control CreatePropertyRow(string label, Control valueControl)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("90,*"),
            Margin = new Thickness(0, 2)
        };

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = ThemeColors.BrushTextMuted,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn((Control)row.Children[0], 0);

        row.Children.Add(valueControl);
        Grid.SetColumn(valueControl, 1);

        return row;
    }

    private void OnGraphChanged(object? sender, EventArgs e)
    {
        // Refresh properties if connections changed
        RefreshPropertiesPanel();

        // Auto-regenerate code
        RegenerateCode();
    }

    private void RegenerateCode()
    {
        if (_canvas == null || _codePreview == null) return;

        var generator = new TimelineCodeGenerator(_canvas.Graph);
        _codePreview.Text = generator.Generate();
    }

    private void OnGenerateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RegenerateCode();
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_canvas == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var suggestedName = $"{_canvas.Graph.Name.Replace(" ", "_")}.json";
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Node Graph",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Node Graph JSON") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file == null) return;

        try
        {
            var json = GraphSerializer.Serialize(_canvas.Graph);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save graph: {ex.Message}");
        }
    }

    private async void OnLoadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_canvas == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Node Graph",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Node Graph JSON") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var graph = GraphSerializer.Deserialize(json);
            if (graph != null)
            {
                _canvas.Graph = graph;
                _currentNode = null;
                RefreshPropertiesPanel();
                RegenerateCode();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load graph: {ex.Message}");
        }
    }

    private void OnModpackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is NodesViewModel vm && _modpackCombo?.SelectedItem is string selected)
        {
            if (selected == NodesViewModel.CreateNewModOption)
            {
                // TODO: Show create mod dialog
                // For now, reset to previous selection
                if (vm.SelectedModpack != null)
                    _modpackCombo.SelectedItem = vm.SelectedModpack;
                return;
            }

            vm.SelectedModpack = selected;
        }
    }

    private void OnSaveToModClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_canvas == null || DataContext is not NodesViewModel vm) return;

        var graphPath = vm.GetGraphSavePath(_canvas.Graph.Name);
        if (graphPath == null)
        {
            System.Diagnostics.Debug.WriteLine("No modpack selected");
            return;
        }

        try
        {
            // Save graph JSON
            var json = GraphSerializer.Serialize(_canvas.Graph);
            File.WriteAllText(graphPath, json);

            // Also save generated code
            var codePath = vm.GetCodeSavePath(_canvas.Graph.Name);
            if (codePath != null)
            {
                var generator = new TimelineCodeGenerator(_canvas.Graph);
                var code = generator.Generate();
                File.WriteAllText(codePath, code);
            }

            System.Diagnostics.Debug.WriteLine($"Saved graph to: {graphPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save to mod: {ex.Message}");
        }
    }

    private void OnLoadFromModClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_canvas == null || DataContext is not NodesViewModel vm) return;

        var savedGraphs = vm.GetSavedGraphs();
        if (savedGraphs.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("No saved graphs in selected modpack");
            return;
        }

        // Create a popup menu with saved graphs
        var menu = new ContextMenu();
        foreach (var graphName in savedGraphs)
        {
            var item = new MenuItem { Header = graphName };
            var capturedName = graphName;
            item.Click += (_, _) =>
            {
                var json = vm.LoadGraphJson(capturedName);
                if (json != null)
                {
                    var graph = GraphSerializer.Deserialize(json);
                    if (graph != null)
                    {
                        _canvas.Graph = graph;
                        _currentNode = null;
                        RefreshPropertiesPanel();
                        RegenerateCode();
                    }
                }
            };
            menu.Items.Add(item);
        }

        if (sender is Button btn)
        {
            menu.PlacementTarget = btn;
            menu.Open(btn);
        }
    }
}
