#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.Styles;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Free-form node canvas for visual editing.
/// Nodes can be placed anywhere and connected.
/// </summary>
public class TimelineCanvasView : UserControl
{
    private const double NodeWidth = 180;
    private const double NodeMinHeight = 60;
    private const double GridSize = 20;

    private TimelineGraph _graph = new();
    private Canvas _canvas = new();
    private readonly Dictionary<string, Border> _nodeControls = new();
    private readonly Dictionary<string, Avalonia.Controls.Shapes.Path> _connectionPaths = new();

    private TimelineNode? _selectedNode;
    private TimelineNode? _dragNode;
    private Point _dragOffset;

    // Connection dragging
    private NodePort? _connectionStart;
    private Line? _pendingConnectionLine;

    // Schema service for property dropdowns
    private SchemaService? _schemaService;

    // Colors
    private static readonly SolidColorBrush CanvasBg = new(Color.Parse("#1a1a1a"));
    private static readonly SolidColorBrush GridLine = new(Color.Parse("#252525"));
    private static readonly SolidColorBrush NodeBg = new(Color.Parse("#2d2d2d"));
    private static readonly SolidColorBrush NodeBorder = new(Color.Parse("#3e3e3e"));
    private static readonly SolidColorBrush NodeSelected = new(Color.Parse("#00a88f"));
    private static readonly SolidColorBrush TriggerColor = new(Color.Parse("#2d5a27"));
    private static readonly SolidColorBrush EffectColor = new(Color.Parse("#4a2d5a"));
    private static readonly SolidColorBrush ActionColor = new(Color.Parse("#5a2d2d"));
    private static readonly SolidColorBrush ConditionColor = new(Color.Parse("#4a4a2d"));
    private static readonly SolidColorBrush DataColor = new(Color.Parse("#2d4a5a"));
    private static readonly SolidColorBrush TemplateRefColor = new(Color.Parse("#5a4a2d"));  // Orange-brown for template refs
    private static readonly SolidColorBrush LoopColor = new(Color.Parse("#2d4a4a"));
    private static readonly SolidColorBrush ConnectionLine = new(Color.Parse("#00a88f"));

    public TimelineGraph Graph
    {
        get => _graph;
        set
        {
            _graph = value;
            RenderGraph();
        }
    }

    public SchemaService? SchemaService
    {
        get => _schemaService;
        set => _schemaService = value;
    }

    public event EventHandler<TimelineNode?>? SelectionChanged;
    public event EventHandler? GraphChanged;

    public TimelineCanvasView()
    {
        CreateExampleGraph();
        Content = BuildUI();
    }

    private void CreateExampleGraph()
    {
        _graph.Name = "Beagle's Concealment";

        // Trigger: On Skill Used
        var trigger = NodeFactory.Create(TimelineNodeType.TriggerSkillUsed);
        trigger.X = 50;
        trigger.Y = 50;
        _graph.Nodes.Add(trigger);

        // Condition: IsAttack AND NOT IsSilent
        var condition = NodeFactory.Create(TimelineNodeType.Condition);
        condition.Title = "Is Attack & Not Silent?";
        condition.X = 300;
        condition.Y = 50;
        _graph.Nodes.Add(condition);

        // Effect: -3 concealment for 1 round
        var effect = NodeFactory.Create(TimelineNodeType.Effect);
        effect.Properties["property"] = "concealment";
        effect.Properties["modifier"] = -3;
        effect.Properties["duration"] = 1;
        effect.X = 550;
        effect.Y = 50;
        _graph.Nodes.Add(effect);

        // Connections
        Connect(trigger, "actor", effect, "actor");
        Connect(trigger, "skill", condition, "value");
        Connect(condition, "pass", effect, "trigger");
    }

    private void Connect(TimelineNode source, string sourcePort, TimelineNode target, string targetPort)
    {
        var srcPort = source.Outputs.FirstOrDefault(p => p.Name == sourcePort);
        var tgtPort = target.Inputs.FirstOrDefault(p => p.Name == targetPort);

        if (srcPort != null && tgtPort != null)
        {
            srcPort.IsConnected = true;
            tgtPort.IsConnected = true;
            _graph.Connections.Add(new NodeConnection
            {
                SourcePort = srcPort,
                TargetPort = tgtPort
            });
        }
    }

    private Control BuildUI()
    {
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        _canvas = new Canvas
        {
            Background = CanvasBg,
            MinWidth = 2000,
            MinHeight = 1500
        };

        // Draw grid
        DrawGrid();

        _canvas.PointerPressed += OnCanvasPointerPressed;
        _canvas.PointerMoved += OnCanvasPointerMoved;
        _canvas.PointerReleased += OnCanvasPointerReleased;

        scroll.Content = _canvas;

        // Propagate types for initial graph
        PropagateTypes();
        RenderGraph();

        return scroll;
    }

    private void DrawGrid()
    {
        // Draw subtle grid lines
        for (double x = 0; x < 2000; x += GridSize)
        {
            var line = new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, 1500),
                Stroke = GridLine,
                StrokeThickness = x % 100 == 0 ? 1 : 0.5
            };
            _canvas.Children.Add(line);
        }

        for (double y = 0; y < 1500; y += GridSize)
        {
            var line = new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(2000, y),
                Stroke = GridLine,
                StrokeThickness = y % 100 == 0 ? 1 : 0.5
            };
            _canvas.Children.Add(line);
        }
    }

    private void RenderGraph()
    {
        // Clear existing nodes and connections (keep grid)
        var toRemove = _canvas.Children.Where(c => c is Border || c is Avalonia.Controls.Shapes.Path).ToList();
        foreach (var c in toRemove) _canvas.Children.Remove(c);
        _nodeControls.Clear();
        _connectionPaths.Clear();

        // Render connections first (behind nodes)
        foreach (var conn in _graph.Connections)
        {
            RenderConnection(conn);
        }

        // Render nodes
        foreach (var node in _graph.Nodes)
        {
            RenderNode(node);
        }
    }

    /// <summary>
    /// Called after a graph is loaded to ensure types are propagated.
    /// </summary>
    public void RefreshTypes()
    {
        PropagateTypes();
    }

    private void RenderNode(TimelineNode node)
    {
        var border = new Border
        {
            Width = NodeWidth,
            MinHeight = NodeMinHeight,
            Background = NodeBg,
            BorderBrush = node.IsSelected ? NodeSelected : NodeBorder,
            BorderThickness = new Thickness(node.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Tag = node
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        // Header with type color
        var headerColor = GetNodeColor(node.NodeType);
        var header = new Border
        {
            Background = headerColor,
            CornerRadius = new CornerRadius(5, 5, 0, 0),
            Padding = new Thickness(8, 4)
        };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        headerPanel.Children.Add(new TextBlock
        {
            Text = GetNodeIcon(node.NodeType),
            FontSize = 12,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = node.Title,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Child = headerPanel;
        content.Children.Add(header);
        Grid.SetRow(header, 0);

        // Ports area
        var portsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(4, 6, 4, 4)
        };

        // Input ports (left)
        var inputStack = new StackPanel { Spacing = 4 };
        foreach (var port in node.Inputs)
        {
            inputStack.Children.Add(CreatePortControl(port, isLeft: true));
        }
        portsGrid.Children.Add(inputStack);
        Grid.SetColumn(inputStack, 0);

        // Output ports (right)
        var outputStack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var port in node.Outputs)
        {
            outputStack.Children.Add(CreatePortControl(port, isLeft: false));
        }
        portsGrid.Children.Add(outputStack);
        Grid.SetColumn(outputStack, 1);

        content.Children.Add(portsGrid);
        Grid.SetRow(portsGrid, 1);

        border.Child = content;

        // Event handlers
        border.PointerPressed += OnNodePointerPressed;

        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);
        _canvas.Children.Add(border);
        _nodeControls[node.Id] = border;
    }

    private Control CreatePortControl(NodePort port, bool isLeft)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = GetPortColor(port.DataType),
            Stroke = port.IsConnected ? ConnectionLine : NodeBorder,
            StrokeThickness = port.IsConnected ? 2 : 1,
            Tag = port
        };

        dot.PointerPressed += OnPortPointerPressed;
        dot.PointerReleased += OnPortPointerReleased;

        var label = new TextBlock
        {
            Text = port.Name,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextMuted,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isLeft)
        {
            panel.Children.Add(dot);
            panel.Children.Add(label);
        }
        else
        {
            panel.HorizontalAlignment = HorizontalAlignment.Right;
            panel.Children.Add(label);
            panel.Children.Add(dot);
        }

        return panel;
    }

    private void RenderConnection(NodeConnection conn)
    {
        if (conn.SourcePort?.Node == null || conn.TargetPort?.Node == null) return;

        var srcNode = conn.SourcePort.Node;
        var tgtNode = conn.TargetPort.Node;

        // Calculate port positions
        var srcPortIndex = srcNode.Outputs.IndexOf(conn.SourcePort);
        var tgtPortIndex = tgtNode.Inputs.IndexOf(conn.TargetPort);

        var srcX = srcNode.X + NodeWidth - 5;
        var srcY = srcNode.Y + 35 + (srcPortIndex * 18);

        var tgtX = tgtNode.X + 5;
        var tgtY = tgtNode.Y + 35 + (tgtPortIndex * 18);

        var path = new Avalonia.Controls.Shapes.Path
        {
            Stroke = GetPortColor(conn.SourcePort.DataType),
            StrokeThickness = 2,
            Data = CreateBezierPath(srcX, srcY, tgtX, tgtY)
        };

        _canvas.Children.Add(path);
        _connectionPaths[conn.Id] = path;
    }

    private Geometry CreateBezierPath(double x1, double y1, double x2, double y2)
    {
        var dx = Math.Abs(x2 - x1);
        var ctrlOffset = Math.Max(50, dx * 0.4);

        var pathFigure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false,
            Segments = new PathSegments
            {
                new BezierSegment
                {
                    Point1 = new Point(x1 + ctrlOffset, y1),
                    Point2 = new Point(x2 - ctrlOffset, y2),
                    Point3 = new Point(x2, y2)
                }
            }
        };

        return new PathGeometry { Figures = new PathFigures { pathFigure } };
    }

    private static string GetNodeIcon(TimelineNodeType type)
    {
        return type switch
        {
            >= TimelineNodeType.TriggerSkillUsed and <= TimelineNodeType.TriggerSuppressed => ">>",
            TimelineNodeType.Actor or TimelineNodeType.Skill or TimelineNodeType.Tile => "[]",
            TimelineNodeType.TemplateRefActor or TimelineNodeType.TemplateRefSkill or TimelineNodeType.TemplateRefItem => "@",
            >= TimelineNodeType.Condition and <= TimelineNodeType.Or => "?",
            TimelineNodeType.Effect => "fx",
            >= TimelineNodeType.ForEachActor and <= TimelineNodeType.ForEachSkill => "{}",
            >= TimelineNodeType.ActionMoveTo => "!",
            _ => "*"
        };
    }

    private static SolidColorBrush GetNodeColor(TimelineNodeType type)
    {
        return type switch
        {
            >= TimelineNodeType.TriggerSkillUsed and <= TimelineNodeType.TriggerSuppressed => TriggerColor,
            TimelineNodeType.Actor or TimelineNodeType.Skill or TimelineNodeType.Tile => DataColor,
            TimelineNodeType.TemplateRefActor or TimelineNodeType.TemplateRefSkill or TimelineNodeType.TemplateRefItem => TemplateRefColor,
            >= TimelineNodeType.Condition and <= TimelineNodeType.Or => ConditionColor,
            TimelineNodeType.Effect => EffectColor,
            >= TimelineNodeType.ForEachActor and <= TimelineNodeType.ForEachSkill => LoopColor,
            >= TimelineNodeType.ActionMoveTo => ActionColor,
            _ => NodeBg
        };
    }

    private static SolidColorBrush GetPortColor(PortDataType type)
    {
        return type switch
        {
            PortDataType.Actor => new SolidColorBrush(Color.Parse("#4a9eff")),    // Blue
            PortDataType.Skill => new SolidColorBrush(Color.Parse("#4aff9e")),    // Green
            PortDataType.Tile => new SolidColorBrush(Color.Parse("#9e7044")),     // Brown
            PortDataType.Number => new SolidColorBrush(Color.Parse("#ffdd4a")),   // Yellow
            PortDataType.Boolean => new SolidColorBrush(Color.Parse("#ff4a4a")),  // Red
            PortDataType.String => new SolidColorBrush(Color.Parse("#ff9e4a")),   // Orange
            _ => new SolidColorBrush(Color.Parse("#888888"))                       // Gray
        };
    }

    // Event handlers
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Deselect when clicking canvas background
        if (_selectedNode != null)
        {
            _selectedNode.IsSelected = false;
            _selectedNode = null;
            SelectionChanged?.Invoke(this, null);
            RenderGraph();
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragNode != null)
        {
            var pos = e.GetPosition(_canvas);
            // Snap to grid
            var newX = Math.Round((pos.X - _dragOffset.X) / GridSize) * GridSize;
            var newY = Math.Round((pos.Y - _dragOffset.Y) / GridSize) * GridSize;

            newX = Math.Max(0, newX);
            newY = Math.Max(0, newY);

            if (Math.Abs(newX - _dragNode.X) > 0.1 || Math.Abs(newY - _dragNode.Y) > 0.1)
            {
                _dragNode.X = newX;
                _dragNode.Y = newY;
                RenderGraph();
            }
        }

        if (_pendingConnectionLine != null)
        {
            var pos = e.GetPosition(_canvas);
            _pendingConnectionLine.EndPoint = pos;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragNode != null)
        {
            _dragNode = null;
            GraphChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_pendingConnectionLine != null)
        {
            _canvas.Children.Remove(_pendingConnectionLine);
            _pendingConnectionLine = null;
            _connectionStart = null;
        }
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: TimelineNode node })
        {
            e.Handled = true;

            // Select
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            node.IsSelected = true;
            _selectedNode = node;
            SelectionChanged?.Invoke(this, node);

            // Start drag
            var pos = e.GetPosition(_canvas);
            _dragNode = node;
            _dragOffset = new Point(pos.X - node.X, pos.Y - node.Y);

            RenderGraph();
        }
    }

    private void OnPortPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Ellipse { Tag: NodePort port })
        {
            e.Handled = true;
            _connectionStart = port;

            // Start drawing connection line
            var pos = e.GetPosition(_canvas);
            _pendingConnectionLine = new Line
            {
                StartPoint = pos,
                EndPoint = pos,
                Stroke = GetPortColor(port.DataType),
                StrokeThickness = 2,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 }
            };
            _canvas.Children.Add(_pendingConnectionLine);
        }
    }

    private void OnPortPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Ellipse { Tag: NodePort targetPort } && _connectionStart != null)
        {
            if (_connectionStart.CanConnectTo(targetPort))
            {
                // Create connection
                var source = _connectionStart.IsOutput ? _connectionStart : targetPort;
                var target = _connectionStart.IsOutput ? targetPort : _connectionStart;

                // Remove existing connection to this input (inputs can only have one connection)
                if (!target.IsOutput)
                {
                    var existing = _graph.Connections.FirstOrDefault(c => c.TargetPort == target);
                    if (existing != null)
                    {
                        if (existing.SourcePort != null) existing.SourcePort.IsConnected = false;
                        target.IsConnected = false;
                        _graph.Connections.Remove(existing);
                    }
                }

                source.IsConnected = true;
                target.IsConnected = true;

                _graph.Connections.Add(new NodeConnection
                {
                    SourcePort = source,
                    TargetPort = target
                });

                // Propagate types through the graph after connection changes
                PropagateTypes();

                GraphChanged?.Invoke(this, EventArgs.Empty);
                RenderGraph();
            }
        }

        if (_pendingConnectionLine != null)
        {
            _canvas.Children.Remove(_pendingConnectionLine);
            _pendingConnectionLine = null;
        }
        _connectionStart = null;
    }

    /// <summary>
    /// Add a node at the center of the visible area.
    /// </summary>
    public void AddNode(TimelineNodeType type, int _unused = 0)
    {
        var node = NodeFactory.Create(type);

        // Position at a default location, offset from existing nodes
        var existingCount = _graph.Nodes.Count;
        node.X = 100 + (existingCount % 4) * 200;
        node.Y = 100 + (existingCount / 4) * 120;

        _graph.Nodes.Add(node);
        GraphChanged?.Invoke(this, EventArgs.Empty);
        RenderGraph();

        // Select the new node
        if (_selectedNode != null) _selectedNode.IsSelected = false;
        node.IsSelected = true;
        _selectedNode = node;
        SelectionChanged?.Invoke(this, node);
        RenderGraph();
    }

    /// <summary>
    /// Delete the selected node.
    /// </summary>
    public void DeleteSelected()
    {
        if (_selectedNode == null) return;

        // Remove connections
        var toRemove = _graph.Connections
            .Where(c => c.SourceNode == _selectedNode || c.TargetNode == _selectedNode)
            .ToList();
        foreach (var c in toRemove)
        {
            if (c.SourcePort != null) c.SourcePort.IsConnected = false;
            if (c.TargetPort != null) c.TargetPort.IsConnected = false;
            _graph.Connections.Remove(c);
        }

        _graph.Nodes.Remove(_selectedNode);
        _selectedNode = null;

        // Propagate types after connections are removed
        PropagateTypes();

        SelectionChanged?.Invoke(this, null);
        GraphChanged?.Invoke(this, EventArgs.Empty);
        RenderGraph();
    }

    /// <summary>
    /// Get fields that can be modified, based on connected template type.
    /// Uses resolved type propagation to determine the specific template connected to the node.
    /// </summary>
    public List<string> GetModifiableFields(TimelineNode node)
    {
        if (_schemaService == null) return new List<string>();

        // Get the resolved type for the "actor" input using type propagation
        var resolvedType = GetResolvedTypeForInput(node, "actor");

        // If no actor input, try skill input
        if (resolvedType == null)
        {
            resolvedType = GetResolvedTypeForInput(node, "skill");
        }

        if (string.IsNullOrEmpty(resolvedType))
        {
            return new List<string>();
        }

        // Get the base type (e.g., "ActorTemplate" from "ActorTemplate:soldier_rifleman")
        var baseType = GetBaseType(resolvedType);

        // Map runtime types to their template counterparts
        var templateType = baseType switch
        {
            "Actor" => "ActorTemplate",
            "Skill" => "SkillTemplate",
            "Item" => "ItemTemplate",
            "Tile" => "TileTemplate",
            _ => baseType  // Already a template type like "ActorTemplate"
        };

        // Check if we have a specific template ID (e.g., "soldier_rifleman")
        var templateId = GetTemplateIdFromResolvedType(resolvedType);

        // If we have a specific template ID and a schema service with instance data,
        // we could potentially load that specific template's fields.
        // For now, we load all fields from the template type's schema definition.
        // The schema defines what fields exist on the type, which applies to all instances.

        // Get fields from schema for the template type
        var fields = _schemaService.GetAllTemplateFields(templateType);
        return fields
            .Where(f => IsModifiableType(f.Type))
            .Select(f => f.Name)
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Get the resolved type info for a node's input, providing both base type and template ID if available.
    /// </summary>
    /// <param name="node">The node to check</param>
    /// <param name="inputPortName">The name of the input port</param>
    /// <returns>Tuple of (baseType, templateId) where templateId may be null for generic types</returns>
    public (string baseType, string? templateId) GetResolvedTypeInfo(TimelineNode node, string inputPortName)
    {
        var resolvedType = GetResolvedTypeForInput(node, inputPortName);
        if (string.IsNullOrEmpty(resolvedType))
        {
            return ("Any", null);
        }

        return (GetBaseType(resolvedType), GetTemplateIdFromResolvedType(resolvedType));
    }

    private static bool IsModifiableType(string type)
    {
        return type switch
        {
            "int" or "Int32" or "float" or "Single" or "double" or "Double" => true,
            "bool" or "Boolean" => true,
            _ => false
        };
    }

    #region Type Propagation

    /// <summary>
    /// Propagate resolved types through all connections in the graph.
    /// This walks the graph and determines what type flows through each port.
    /// </summary>
    public void PropagateTypes()
    {
        // Clear all resolved types first
        foreach (var node in _graph.Nodes)
        {
            foreach (var port in node.Inputs)
                port.ResolvedType = null;
            foreach (var port in node.Outputs)
                port.ResolvedType = null;
        }

        // Build dependency order - process nodes whose inputs are resolved first
        var processed = new HashSet<string>();
        var maxIterations = _graph.Nodes.Count * 2; // Prevent infinite loops
        var iterations = 0;

        while (processed.Count < _graph.Nodes.Count && iterations < maxIterations)
        {
            iterations++;
            foreach (var node in _graph.Nodes)
            {
                if (processed.Contains(node.Id)) continue;

                // Check if all connected inputs have resolved types
                var canProcess = true;
                foreach (var input in node.Inputs)
                {
                    var conn = _graph.Connections.FirstOrDefault(c => c.TargetPort == input);
                    if (conn?.SourcePort != null && conn.SourcePort.ResolvedType == null)
                    {
                        // Source not yet resolved, skip this node for now
                        // But only if the source node hasn't been processed
                        if (!processed.Contains(conn.SourceNode?.Id ?? ""))
                        {
                            canProcess = false;
                            break;
                        }
                    }
                }

                if (canProcess)
                {
                    ResolveNodeTypes(node);
                    processed.Add(node.Id);
                }
            }
        }
    }

    /// <summary>
    /// Resolve the output types for a single node based on its type and connected inputs.
    /// </summary>
    private void ResolveNodeTypes(TimelineNode node)
    {
        switch (node.NodeType)
        {
            // Triggers output generic types
            case TimelineNodeType.TriggerSkillUsed:
            case TimelineNodeType.TriggerDamageReceived:
            case TimelineNodeType.TriggerActorKilled:
            case TimelineNodeType.TriggerMovementStarted:
            case TimelineNodeType.TriggerMovementFinished:
            case TimelineNodeType.TriggerTurnStart:
            case TimelineNodeType.TriggerTurnEnd:
            case TimelineNodeType.TriggerHitpointsChanged:
            case TimelineNodeType.TriggerSuppressed:
                foreach (var output in node.Outputs)
                {
                    output.ResolvedType = PortDataTypeToResolvedType(output.DataType);
                }
                break;

            case TimelineNodeType.TriggerRoundStart:
            case TimelineNodeType.TriggerRoundEnd:
                foreach (var output in node.Outputs)
                {
                    output.ResolvedType = "Number";
                }
                break;

            // Data nodes pass through their input type
            case TimelineNodeType.Actor:
                ResolvePassThroughNode(node, "in", "out", "Actor");
                // Property outputs have fixed types
                SetOutputType(node, "name", "String");
                SetOutputType(node, "faction", "Number");
                SetOutputType(node, "hp", "Number");
                SetOutputType(node, "ap", "Number");
                break;

            case TimelineNodeType.Skill:
                ResolvePassThroughNode(node, "in", "out", "Skill");
                SetOutputType(node, "isAttack", "Boolean");
                SetOutputType(node, "isSilent", "Boolean");
                SetOutputType(node, "name", "String");
                break;

            case TimelineNodeType.Tile:
                ResolvePassThroughNode(node, "in", "out", "Tile");
                SetOutputType(node, "x", "Number");
                SetOutputType(node, "y", "Number");
                SetOutputType(node, "occupant", "Actor");
                break;

            // Template reference nodes output specific template types
            case TimelineNodeType.TemplateRefActor:
                {
                    var templateId = node.Properties.TryGetValue("templateId", out var tid) ? tid?.ToString() : null;
                    var resolvedType = string.IsNullOrEmpty(templateId) ? "ActorTemplate" : $"ActorTemplate:{templateId}";
                    SetOutputType(node, "actor", resolvedType);
                }
                break;

            case TimelineNodeType.TemplateRefSkill:
                {
                    var templateId = node.Properties.TryGetValue("templateId", out var tid) ? tid?.ToString() : null;
                    var resolvedType = string.IsNullOrEmpty(templateId) ? "SkillTemplate" : $"SkillTemplate:{templateId}";
                    SetOutputType(node, "skill", resolvedType);
                }
                break;

            case TimelineNodeType.TemplateRefItem:
                {
                    var templateId = node.Properties.TryGetValue("templateId", out var tid) ? tid?.ToString() : null;
                    var resolvedType = string.IsNullOrEmpty(templateId) ? "ItemTemplate" : $"ItemTemplate:{templateId}";
                    SetOutputType(node, "item", resolvedType);
                }
                break;

            // Logic nodes have fixed output types
            case TimelineNodeType.Compare:
            case TimelineNodeType.Not:
            case TimelineNodeType.And:
            case TimelineNodeType.Or:
                foreach (var output in node.Outputs)
                {
                    output.ResolvedType = "Boolean";
                }
                break;

            case TimelineNodeType.MathOp:
                SetOutputType(node, "result", "Number");
                break;

            case TimelineNodeType.Condition:
                // Pass/fail outputs inherit from value input or stay generic
                SetOutputType(node, "pass", "Flow");
                SetOutputType(node, "fail", "Flow");
                break;

            // Variable nodes
            case TimelineNodeType.SetVariable:
                // No outputs
                break;

            case TimelineNodeType.GetVariable:
                // Type depends on what was set - for now use Any
                SetOutputType(node, "value", "Any");
                break;

            // Loop nodes
            case TimelineNodeType.ForEachActor:
                SetOutputType(node, "actor", "Actor");
                SetOutputType(node, "done", "Flow");
                break;

            case TimelineNodeType.ForEachTileInRadius:
                SetOutputType(node, "tile", "Tile");
                SetOutputType(node, "done", "Flow");
                break;

            case TimelineNodeType.ForEachSkill:
                SetOutputType(node, "skill", "Skill");
                SetOutputType(node, "done", "Flow");
                break;

            // Effect node - no outputs
            case TimelineNodeType.Effect:
                break;

            // Action nodes
            case TimelineNodeType.ActionMoveTo:
            case TimelineNodeType.ActionApplyDamage:
            case TimelineNodeType.ActionHeal:
            case TimelineNodeType.ActionApplySuppression:
            case TimelineNodeType.ActionAddSkill:
            case TimelineNodeType.ActionRemoveSkill:
            case TimelineNodeType.ActionKill:
                SetOutputType(node, "done", "Flow");
                break;

            case TimelineNodeType.ActionSpawn:
                SetOutputType(node, "actor", "Actor");
                break;

            case TimelineNodeType.ActionLog:
                // No outputs
                break;

            default:
                // For unknown nodes, use port data types
                foreach (var output in node.Outputs)
                {
                    output.ResolvedType = PortDataTypeToResolvedType(output.DataType);
                }
                break;
        }

        // Also set resolved types on inputs based on what's connected
        foreach (var input in node.Inputs)
        {
            var conn = _graph.Connections.FirstOrDefault(c => c.TargetPort == input);
            if (conn?.SourcePort != null)
            {
                input.ResolvedType = conn.SourcePort.ResolvedType;
            }
        }
    }

    /// <summary>
    /// Helper to resolve a pass-through node where output inherits input type.
    /// </summary>
    private void ResolvePassThroughNode(TimelineNode node, string inputName, string outputName, string fallbackType)
    {
        var input = node.Inputs.FirstOrDefault(p => p.Name == inputName);
        var output = node.Outputs.FirstOrDefault(p => p.Name == outputName);

        if (output == null) return;

        // Find what's connected to the input
        var conn = _graph.Connections.FirstOrDefault(c => c.TargetPort == input);
        if (conn?.SourcePort?.ResolvedType != null)
        {
            // Inherit the type from the connected source
            output.ResolvedType = conn.SourcePort.ResolvedType;
        }
        else
        {
            // Use fallback generic type
            output.ResolvedType = fallbackType;
        }
    }

    /// <summary>
    /// Helper to set the resolved type for a specific output port by name.
    /// </summary>
    private void SetOutputType(TimelineNode node, string portName, string resolvedType)
    {
        var port = node.Outputs.FirstOrDefault(p => p.Name == portName);
        if (port != null)
        {
            port.ResolvedType = resolvedType;
        }
    }

    /// <summary>
    /// Convert a PortDataType enum to its resolved type string equivalent.
    /// </summary>
    private static string PortDataTypeToResolvedType(PortDataType dataType)
    {
        return dataType switch
        {
            PortDataType.Actor => "Actor",
            PortDataType.Skill => "Skill",
            PortDataType.Tile => "Tile",
            PortDataType.Number => "Number",
            PortDataType.Boolean => "Boolean",
            PortDataType.String => "String",
            PortDataType.Any => "Any",
            _ => "Any"
        };
    }

    /// <summary>
    /// Get the resolved type flowing into a specific input port of a node.
    /// This traces back through connections to find what type is connected.
    /// </summary>
    /// <param name="node">The node to check</param>
    /// <param name="inputPortName">The name of the input port</param>
    /// <returns>The resolved type string, or null if not connected or not resolved</returns>
    public string? GetResolvedTypeForInput(TimelineNode node, string inputPortName)
    {
        var input = node.Inputs.FirstOrDefault(p => p.Name == inputPortName);
        if (input == null) return null;

        // If we already have a resolved type cached, use it
        if (input.ResolvedType != null) return input.ResolvedType;

        // Otherwise trace back through the connection
        var conn = _graph.Connections.FirstOrDefault(c => c.TargetPort == input);
        if (conn?.SourcePort == null) return null;

        return conn.SourcePort.ResolvedType;
    }

    /// <summary>
    /// Get the template ID from a resolved type string.
    /// For example, "ActorTemplate:soldier_rifleman" returns "soldier_rifleman".
    /// Returns null if the type doesn't include a specific template.
    /// </summary>
    public static string? GetTemplateIdFromResolvedType(string? resolvedType)
    {
        if (string.IsNullOrEmpty(resolvedType)) return null;

        var colonIndex = resolvedType.IndexOf(':');
        if (colonIndex < 0) return null;

        return resolvedType.Substring(colonIndex + 1);
    }

    /// <summary>
    /// Get the base type from a resolved type string.
    /// For example, "ActorTemplate:soldier_rifleman" returns "ActorTemplate".
    /// "Actor" returns "Actor".
    /// </summary>
    public static string GetBaseType(string? resolvedType)
    {
        if (string.IsNullOrEmpty(resolvedType)) return "Any";

        var colonIndex = resolvedType.IndexOf(':');
        if (colonIndex < 0) return resolvedType;

        return resolvedType.Substring(0, colonIndex);
    }

    #endregion
}
