#nullable disable

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.Styles;
using Nodify.Avalonia;
using Nodify.Avalonia.Connections;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Visual node graph editor view using Nodify-Avalonia.
/// Provides a canvas for creating and connecting nodes to define mod behavior.
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
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Handle any view-specific updates based on ViewModel changes
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

        // Title
        toolbarPanel.Children.Add(new TextBlock
        {
            Text = "Visual Editor",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        });

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
            ("Condition", NodeType.Condition),
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
        clearButton.Classes.Add("destructive");
        clearButton.Click += (_, _) => _viewModel?.Clear();
        toolbarPanel.Children.Add(clearButton);

        // Reset viewport button
        var resetViewButton = new Button
        {
            Content = "Reset View",
            FontSize = 11
        };
        resetViewButton.Classes.Add("secondary");
        resetViewButton.Click += (_, _) => _viewModel?.ResetViewport();
        toolbarPanel.Children.Add(resetViewButton);

        // Spacer
        toolbarPanel.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Stretch });

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
        button.Classes.Add("secondary");

        var menu = new ContextMenu();
        foreach (var (name, type) in options)
        {
            var item = new MenuItem { Header = name };
            var capturedType = type;
            item.Click += (_, _) =>
            {
                if (_viewModel != null)
                {
                    // Add node at center of viewport
                    var location = new Point(
                        _viewModel.ViewportLocation.X + 200,
                        _viewModel.ViewportLocation.Y + 150
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
            MaxViewportZoom = 3.0
        };

        // Bind to ViewModel
        _editor.Bind(NodifyEditor.ItemsSourceProperty, new Binding("Nodes"));
        _editor.Bind(NodifyEditor.ConnectionsProperty, new Binding("Connections"));
        _editor.Bind(NodifyEditor.SelectedItemsProperty, new Binding("SelectedNodes"));
        _editor.Bind(NodifyEditor.ViewportZoomProperty, new Binding("ViewportZoom") { Mode = BindingMode.TwoWay });
        _editor.Bind(NodifyEditor.ViewportLocationProperty, new Binding("ViewportLocation") { Mode = BindingMode.TwoWay });

        // Configure node template using FuncDataTemplate
        _editor.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<NodeViewModel>((node, _) =>
        {
            return CreateNodeControl(node);
        });

        // Configure pending connection
        _editor.Bind(NodifyEditor.PendingConnectionProperty, new Binding("PendingConnection"));

        container.Child = _editor;
        return container;
    }

    private Control CreateNodeControl(NodeViewModel node)
    {
        // Create node content directly - ItemContainer handles positioning
        var nodeContent = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            MinWidth = 120,
            Padding = new Thickness(0)
        };

        var contentStack = new StackPanel();

        // Header
        var header = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            CornerRadius = new CornerRadius(5, 5, 0, 0),
            Padding = new Thickness(12, 6)
        };

        var titleBlock = new TextBlock
        {
            Text = node.Title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.Child = titleBlock;
        contentStack.Children.Add(header);

        // Connectors area
        var connectorsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(8)
        };

        // Input connectors (left side)
        var inputStack = new StackPanel
        {
            Spacing = 4
        };
        foreach (var input in node.Input)
        {
            inputStack.Children.Add(CreateConnectorControl(input, isInput: true));
        }
        connectorsGrid.Children.Add(inputStack);
        Grid.SetColumn(inputStack, 0);

        // Output connectors (right side)
        var outputStack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var output in node.Output)
        {
            outputStack.Children.Add(CreateConnectorControl(output, isInput: false));
        }
        connectorsGrid.Children.Add(outputStack);
        Grid.SetColumn(outputStack, 1);

        contentStack.Children.Add(connectorsGrid);

        nodeContent.Child = contentStack;

        return nodeContent;
    }

    private Control CreateConnectorControl(ConnectorViewModel connector, bool isInput)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        // Create the connector element
        var connectorElement = new Connector();

        // Bind anchor position for connections
        connectorElement.Bind(Connector.AnchorProperty, new Binding("Anchor")
        {
            Mode = BindingMode.OneWayToSource,
            Source = connector
        });

        // Bind IsConnected for visual feedback
        connectorElement.Bind(Connector.IsConnectedProperty, new Binding("IsConnected")
        {
            Source = connector
        });

        var label = new TextBlock
        {
            Text = connector.Title,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isInput)
        {
            panel.Children.Add(connectorElement);
            panel.Children.Add(label);
        }
        else
        {
            panel.HorizontalAlignment = HorizontalAlignment.Right;
            panel.Children.Add(label);
            panel.Children.Add(connectorElement);
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

        // Status message
        var statusText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary
        };
        statusText.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        statusPanel.Children.Add(statusText);

        // Node count
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

        // Connection count
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

        // Instructions
        statusPanel.Children.Add(new TextBlock
        {
            Text = "Pan: Middle-click drag | Zoom: Scroll | Select: Click | Multi-select: Ctrl+Click",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextDim,
            Margin = new Thickness(16, 0, 0, 0)
        });

        statusBar.Child = statusPanel;
        return statusBar;
    }
}
