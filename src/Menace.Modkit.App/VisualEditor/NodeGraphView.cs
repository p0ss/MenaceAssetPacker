#nullable enable

using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Menace.Modkit.App.Styles;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Visual node graph editor view using Nodify-Avalonia.
/// </summary>
public class NodeGraphView : UserControl
{
    private NodifyEditor? _editor;
    private NodeGraphViewModel? _viewModel;

    public NodeGraphView()
    {
        Content = BuildUI();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as NodeGraphViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Set up the editor with commands
            if (_editor != null)
            {
                SetupEditorCommands();
            }
        }
    }

    private void SetupEditorCommands()
    {
        if (_editor == null || _viewModel == null) return;

        // Connection commands - use object type since Nodify may pass different types
        _editor.ConnectionStartedCommand = new RelayCommand<object>(param =>
        {
            var connector = param as ConnectorViewModel;
            if (connector != null)
            {
                _viewModel.StartConnection(connector);
            }
            else
            {
                Menace.Modkit.App.Services.ModkitLog.Warn($"[NodeGraph] ConnectionStarted received unexpected type: {param?.GetType().Name ?? "null"}");
            }
        });

        _editor.ConnectionCompletedCommand = new RelayCommand<object>(param =>
        {
            var connector = param as ConnectorViewModel;
            if (connector != null)
            {
                _viewModel.CompleteConnection(connector);
            }
            else
            {
                // If null or wrong type, just cancel the pending connection
                _viewModel.CancelConnection();
                Menace.Modkit.App.Services.ModkitLog.Warn($"[NodeGraph] ConnectionCompleted received unexpected type: {param?.GetType().Name ?? "null"}");
            }
        });

        _editor.DisconnectConnectorCommand = new RelayCommand<object>(param =>
        {
            var connector = param as ConnectorViewModel;
            if (connector != null)
            {
                _viewModel.DisconnectConnector(connector);
            }
        });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Handle any view-specific updates
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        // Top toolbar
        var toolbar = BuildToolbar();
        mainGrid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 0);

        // Main editor area
        var editorContainer = BuildEditorContainer();
        mainGrid.Children.Add(editorContainer);
        Grid.SetRow(editorContainer, 1);

        // Bottom status bar
        var statusBar = BuildStatusBar();
        mainGrid.Children.Add(statusBar);
        Grid.SetRow(statusBar, 2);

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

        var toolbarPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // Add Event Node dropdown
        var eventDropdown = CreateNodeDropdown("+ Event", new[]
        {
            ("Skill Used", NodeType.EventSkillUsed),
            ("Damage Received", NodeType.EventDamageReceived),
            ("Actor Killed", NodeType.EventActorKilled),
            ("Round Start", NodeType.EventRoundStart),
            ("Round End", NodeType.EventRoundEnd),
            ("Turn End", NodeType.EventTurnEnd)
        });
        toolbarPanel.Children.Add(eventDropdown);

        // Add Condition Node dropdown
        var conditionDropdown = CreateNodeDropdown("+ Condition", new[]
        {
            ("Property Check", NodeType.Condition),
            ("AND", NodeType.And),
            ("OR", NodeType.Or),
            ("NOT", NodeType.Not)
        });
        toolbarPanel.Children.Add(conditionDropdown);

        // Add Action Node dropdown
        var actionDropdown = CreateNodeDropdown("+ Action", new[]
        {
            ("Add Effect", NodeType.ActionAddEffect),
            ("Damage", NodeType.ActionDamage),
            ("Heal", NodeType.ActionHeal),
            ("Set Flag", NodeType.ActionSetFlag),
            ("Log", NodeType.ActionLog)
        });
        toolbarPanel.Children.Add(actionDropdown);

        // Separator
        toolbarPanel.Children.Add(new Border
        {
            Width = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(8, 0)
        });

        // Clear button
        var clearButton = new Button
        {
            Content = "Clear",
            FontSize = 11
        };
        clearButton.Click += (_, _) => _viewModel?.Clear();
        toolbarPanel.Children.Add(clearButton);

        // Reset viewport button
        var resetViewButton = new Button
        {
            Content = "Reset View",
            FontSize = 11
        };
        resetViewButton.Click += (_, _) => _viewModel?.ResetViewport();
        toolbarPanel.Children.Add(resetViewButton);

        // Spacer
        toolbarPanel.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Stretch });

        // Zoom indicator
        var zoomText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };
        zoomText.Bind(TextBlock.TextProperty, new Binding("ViewportZoom")
        {
            StringFormat = "Zoom: {0:P0}"
        });
        toolbarPanel.Children.Add(zoomText);

        toolbar.Child = toolbarPanel;
        return toolbar;
    }

    private Control CreateNodeDropdown(string label, (string name, NodeType type)[] options)
    {
        var button = new Button
        {
            Content = label,
            FontSize = 11
        };

        var menu = new ContextMenu();
        foreach (var (name, type) in options)
        {
            var item = new MenuItem { Header = name };
            var capturedType = type;
            item.Click += (_, _) =>
            {
                if (_viewModel != null)
                {
                    var location = new Point(
                        -_viewModel.ViewportLocation.X + 300,
                        -_viewModel.ViewportLocation.Y + 200
                    );
                    _viewModel.AddNode(capturedType, location);
                }
            };
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

    private Control BuildEditorContainer()
    {
        var container = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1a1a1a")),
            ClipToBounds = true
        };

        _editor = new NodifyEditor
        {
            Background = new SolidColorBrush(Color.Parse("#1a1a1a")),
            MinViewportZoom = 0.1,
            MaxViewportZoom = 3.0,
            DisableAutoPanning = true,
            EnableRealtimeSelection = false
        };

        // Bind nodes and connections
        _editor.Bind(NodifyEditor.ItemsSourceProperty, new Binding("Nodes"));
        _editor.Bind(NodifyEditor.ConnectionsProperty, new Binding("Connections"));
        _editor.Bind(NodifyEditor.PendingConnectionProperty, new Binding("PendingConnection"));
        _editor.Bind(NodifyEditor.ViewportZoomProperty, new Binding("ViewportZoom") { Mode = BindingMode.TwoWay });
        _editor.Bind(NodifyEditor.ViewportLocationProperty, new Binding("ViewportLocation") { Mode = BindingMode.TwoWay });

        // Set up ItemContainerStyle to bind Location for dragging
        // NodifyEditor uses ItemContainer which has a Location property
        var containerStyle = new Style(x => x.OfType<ItemContainer>());
        containerStyle.Setters.Add(new Setter(ItemContainer.LocationProperty, new Binding("Location") { Mode = BindingMode.TwoWay }));
        _editor.Styles.Add(containerStyle);

        // Node item template - this creates the visual for each node
        _editor.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<NodeViewModel>((node, _) =>
        {
            return CreateNodeControl(node);
        });

        // Note: Connection rendering is handled by Nodify's internal templates
        // The ConnectionViewModel.SourceAnchor/TargetAnchor properties provide the anchor points
        // Nodify's default Connection control should bind to Source/Target properties

        // Add a style to configure connection appearance
        var connectionStyle = new Style(x => x.OfType<Connection>());
        connectionStyle.Setters.Add(new Setter(Connection.StrokeProperty, ThemeColors.BrushPrimary));
        connectionStyle.Setters.Add(new Setter(Connection.StrokeThicknessProperty, 2.0));
        _editor.Styles.Add(connectionStyle);

        // Set up commands if viewmodel already exists
        if (_viewModel != null)
        {
            SetupEditorCommands();
        }

        container.Child = _editor;
        return container;
    }

    private Control CreateNodeControl(NodeViewModel nodeVm)
    {
        // Create a custom node visual - Nodify positions this via its ItemContainer
        var nodeContent = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            MinWidth = 140,
            Padding = new Thickness(0)
        };

        var contentStack = new StackPanel();

        // Header with node type color
        var headerColor = GetNodeTypeColor(nodeVm.Title);
        var header = new Border
        {
            Background = new SolidColorBrush(headerColor),
            CornerRadius = new CornerRadius(5, 5, 0, 0),
            Padding = new Thickness(12, 8)
        };

        var titleBlock = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleBlock.Bind(TextBlock.TextProperty, new Binding("Title") { Source = nodeVm });
        header.Child = titleBlock;
        contentStack.Children.Add(header);

        // Connectors area
        var connectorsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(0, 8, 0, 8),
            MinHeight = 30
        };

        // Input connectors (left side)
        var inputStack = new StackPanel { Spacing = 6 };
        foreach (var input in nodeVm.Input)
        {
            inputStack.Children.Add(CreateConnectorControl(input, isInput: true));
        }
        connectorsGrid.Children.Add(inputStack);
        Grid.SetColumn(inputStack, 0);

        // Output connectors (right side)
        var outputStack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var output in nodeVm.Output)
        {
            outputStack.Children.Add(CreateConnectorControl(output, isInput: false));
        }
        connectorsGrid.Children.Add(outputStack);
        Grid.SetColumn(outputStack, 1);

        contentStack.Children.Add(connectorsGrid);
        nodeContent.Child = contentStack;

        return nodeContent;
    }

    private Color GetNodeTypeColor(string title)
    {
        if (title.Contains("Skill") || title.Contains("Damage") || title.Contains("Kill") ||
            title.Contains("Round") || title.Contains("Turn"))
            return Color.Parse("#2d5a27"); // Green for events
        if (title.Contains("?") || title == "AND" || title == "OR" || title == "NOT")
            return Color.Parse("#4a4a2d"); // Yellow-ish for conditions
        return Color.Parse("#4a2d2d"); // Red-ish for actions
    }

    private Control CreateConnectorControl(ConnectorViewModel connector, bool isInput)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(isInput ? 8 : 0, 0, isInput ? 0 : 8, 0)
        };

        // The connector dot using Nodify's Connector control
        var connectorDot = new Connector
        {
            Width = 12,
            Height = 12,
            // Set DataContext so Nodify's commands receive our ConnectorViewModel
            DataContext = connector
        };

        // Bind anchor for connection drawing - this is how Nodify knows where connections start/end
        connectorDot.Bind(Connector.AnchorProperty, new Binding("Anchor")
        {
            Mode = BindingMode.OneWayToSource
        });

        connectorDot.Bind(Connector.IsConnectedProperty, new Binding("IsConnected"));

        var label = new TextBlock
        {
            Text = connector.Title,
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isInput)
        {
            panel.Children.Add(connectorDot);
            panel.Children.Add(label);
        }
        else
        {
            panel.HorizontalAlignment = HorizontalAlignment.Right;
            panel.Children.Add(label);
            panel.Children.Add(connectorDot);
        }

        return panel;
    }

    private Control BuildStatusBar()
    {
        var statusBar = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            Padding = new Thickness(12, 6),
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var statusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        var nodeCountText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextMuted
        };
        nodeCountText.Bind(TextBlock.TextProperty, new Binding("Nodes.Count")
        {
            StringFormat = "Nodes: {0}"
        });
        statusPanel.Children.Add(nodeCountText);

        var connCountText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextMuted
        };
        connCountText.Bind(TextBlock.TextProperty, new Binding("Connections.Count")
        {
            StringFormat = "Connections: {0}"
        });
        statusPanel.Children.Add(connCountText);

        statusPanel.Children.Add(new TextBlock
        {
            Text = "Drag nodes to move | Drag from connectors to connect | Right-click to pan",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextDim,
            Margin = new Thickness(16, 0, 0, 0)
        });

        statusBar.Child = statusPanel;
        return statusBar;
    }
}

/// <summary>
/// Simple relay command for Nodify commands.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
