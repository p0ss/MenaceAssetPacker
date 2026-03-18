#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// ViewModel for the node graph editor.
/// Manages nodes, connections, and editor state.
/// </summary>
public class NodeGraphViewModel : INotifyPropertyChanged
{
    private double _viewportZoom = 1.0;
    private Point _viewportLocation;
    private string _statusMessage = "Ready";

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = new();
    public PendingConnectionViewModel PendingConnection { get; } = new();

    public double ViewportZoom
    {
        get => _viewportZoom;
        set { _viewportZoom = Math.Clamp(value, 0.1, 3.0); OnPropertyChanged(); }
    }

    public Point ViewportLocation
    {
        get => _viewportLocation;
        set { _viewportLocation = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public NodeGraphViewModel()
    {
        // Create some example nodes to demonstrate the graph works
        CreateExampleGraph();
    }

    /// <summary>
    /// Creates an example graph demonstrating the Beagle's Concealment mod
    /// as described in VISUAL_EDITOR_DESIGN.md
    /// </summary>
    private void CreateExampleGraph()
    {
        // Event: Skill Used
        var eventNode = new NodeViewModel
        {
            Title = "Skill Used",
            Location = new Point(50, 100)
        };
        var actorOutput = new ConnectorViewModel { Title = "actor", Node = eventNode };
        var skillOutput = new ConnectorViewModel { Title = "skill", Node = eventNode };
        eventNode.Output.Add(actorOutput);
        eventNode.Output.Add(skillOutput);
        Nodes.Add(eventNode);

        // Condition: Is Attack?
        var isAttackNode = new NodeViewModel
        {
            Title = "Is Attack?",
            Location = new Point(300, 50)
        };
        var isAttackInput = new ConnectorViewModel { Title = "skill", Node = isAttackNode };
        var isAttackPass = new ConnectorViewModel { Title = "pass", Node = isAttackNode };
        isAttackNode.Input.Add(isAttackInput);
        isAttackNode.Output.Add(isAttackPass);
        Nodes.Add(isAttackNode);

        // Condition: Not Silent?
        var notSilentNode = new NodeViewModel
        {
            Title = "Not Silent?",
            Location = new Point(300, 180)
        };
        var notSilentInput = new ConnectorViewModel { Title = "skill", Node = notSilentNode };
        var notSilentPass = new ConnectorViewModel { Title = "pass", Node = notSilentNode };
        notSilentNode.Input.Add(notSilentInput);
        notSilentNode.Output.Add(notSilentPass);
        Nodes.Add(notSilentNode);

        // Action: Add Concealment -3
        var concealNode = new NodeViewModel
        {
            Title = "Concealment -3",
            Location = new Point(550, 100)
        };
        var concealActorInput = new ConnectorViewModel { Title = "actor", Node = concealNode };
        var concealTriggerInput = new ConnectorViewModel { Title = "trigger", Node = concealNode };
        concealNode.Input.Add(concealActorInput);
        concealNode.Input.Add(concealTriggerInput);
        Nodes.Add(concealNode);

        // Create connections
        // skill -> Is Attack?
        var conn1 = new ConnectionViewModel
        {
            SourceConnector = skillOutput,
            TargetConnector = isAttackInput
        };
        skillOutput.IsConnected = true;
        isAttackInput.IsConnected = true;
        Connections.Add(conn1);

        // skill -> Not Silent?
        var conn2 = new ConnectionViewModel
        {
            SourceConnector = skillOutput,
            TargetConnector = notSilentInput
        };
        notSilentInput.IsConnected = true;
        Connections.Add(conn2);

        // actor -> Concealment actor
        var conn3 = new ConnectionViewModel
        {
            SourceConnector = actorOutput,
            TargetConnector = concealActorInput
        };
        actorOutput.IsConnected = true;
        concealActorInput.IsConnected = true;
        Connections.Add(conn3);

        // Is Attack pass -> Concealment trigger (representing AND logic visually)
        var conn4 = new ConnectionViewModel
        {
            SourceConnector = isAttackPass,
            TargetConnector = concealTriggerInput
        };
        isAttackPass.IsConnected = true;
        concealTriggerInput.IsConnected = true;
        Connections.Add(conn4);

        StatusMessage = "Example graph loaded: Beagle's Concealment mod";
        ModkitLog.Info("[NodeGraph] Created example graph with 4 nodes and 4 connections");
    }

    /// <summary>
    /// Adds a new node at the specified location.
    /// </summary>
    public NodeViewModel AddNode(NodeType type, Point location)
    {
        var node = new NodeViewModel
        {
            Title = GetNodeTitle(type),
            Location = location
        };

        // Add appropriate connectors based on node type
        ConfigureNodeConnectors(node, type);

        Nodes.Add(node);
        StatusMessage = $"Added {node.Title} node";
        ModkitLog.Info($"[NodeGraph] Added node: {node.Title} at {location}");
        return node;
    }

    /// <summary>
    /// Removes a node and all its connections.
    /// </summary>
    public void RemoveNode(NodeViewModel node)
    {
        // Remove all connections involving this node
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            var conn = Connections[i];
            if (conn.SourceConnector?.Node == node || conn.TargetConnector?.Node == node)
            {
                RemoveConnection(conn);
            }
        }

        Nodes.Remove(node);
        SelectedNodes.Remove(node);
        StatusMessage = $"Removed {node.Title} node";
        ModkitLog.Info($"[NodeGraph] Removed node: {node.Title}");
    }

    /// <summary>
    /// Creates a connection between two connectors.
    /// </summary>
    public ConnectionViewModel? Connect(ConnectorViewModel source, ConnectorViewModel target)
    {
        // Validate connection
        if (source == null || target == null)
            return null;

        if (source.Node == target.Node)
        {
            StatusMessage = "Cannot connect a node to itself";
            return null;
        }

        // Check if connection already exists
        foreach (var conn in Connections)
        {
            if ((conn.SourceConnector == source && conn.TargetConnector == target) ||
                (conn.SourceConnector == target && conn.TargetConnector == source))
            {
                StatusMessage = "Connection already exists";
                return null;
            }
        }

        var connection = new ConnectionViewModel
        {
            SourceConnector = source,
            TargetConnector = target
        };

        source.IsConnected = true;
        target.IsConnected = true;
        Connections.Add(connection);

        StatusMessage = $"Connected {source.Node?.Title}.{source.Title} -> {target.Node?.Title}.{target.Title}";
        ModkitLog.Info($"[NodeGraph] Created connection: {source.Node?.Title}.{source.Title} -> {target.Node?.Title}.{target.Title}");
        return connection;
    }

    /// <summary>
    /// Removes a connection.
    /// </summary>
    public void RemoveConnection(ConnectionViewModel connection)
    {
        if (connection.SourceConnector != null)
            connection.SourceConnector.IsConnected = HasOtherConnections(connection.SourceConnector, connection);
        if (connection.TargetConnector != null)
            connection.TargetConnector.IsConnected = HasOtherConnections(connection.TargetConnector, connection);

        Connections.Remove(connection);
        ModkitLog.Info($"[NodeGraph] Removed connection");
    }

    /// <summary>
    /// Clears all nodes and connections.
    /// </summary>
    public void Clear()
    {
        Connections.Clear();
        Nodes.Clear();
        SelectedNodes.Clear();
        StatusMessage = "Graph cleared";
        ModkitLog.Info("[NodeGraph] Cleared graph");
    }

    /// <summary>
    /// Resets viewport to default position and zoom.
    /// </summary>
    public void ResetViewport()
    {
        ViewportZoom = 1.0;
        ViewportLocation = new Point(0, 0);
        StatusMessage = "Viewport reset";
    }

    /// <summary>
    /// Starts a pending connection from the specified connector.
    /// Called when user starts dragging from a connector.
    /// </summary>
    public void StartConnection(ConnectorViewModel? connector)
    {
        if (connector == null) return;

        PendingConnection.Source = connector;
        PendingConnection.TargetLocation = connector.Anchor;
        PendingConnection.IsVisible = true;
        StatusMessage = $"Connecting from {connector.Node?.Title}.{connector.Title}...";
    }

    /// <summary>
    /// Completes a pending connection to the specified connector.
    /// Called when user releases on a connector.
    /// </summary>
    public void CompleteConnection(ConnectorViewModel? connector)
    {
        if (PendingConnection.Source == null || connector == null)
        {
            CancelConnection();
            return;
        }

        // Don't connect to self
        if (PendingConnection.Source == connector)
        {
            CancelConnection();
            return;
        }

        // Don't connect node to itself
        if (PendingConnection.Source.Node == connector.Node)
        {
            StatusMessage = "Cannot connect a node to itself";
            CancelConnection();
            return;
        }

        // Create the connection
        var source = PendingConnection.Source;
        var target = connector;

        // Determine direction: output -> input
        // If source is from an input, swap
        bool sourceIsInput = source.Node?.Input.Contains(source) ?? false;
        bool targetIsInput = target.Node?.Input.Contains(target) ?? false;

        if (sourceIsInput && !targetIsInput)
        {
            // Swap: we dragged from input to output
            (source, target) = (target, source);
        }
        else if (sourceIsInput && targetIsInput)
        {
            // Both inputs - invalid
            StatusMessage = "Cannot connect two inputs";
            CancelConnection();
            return;
        }
        else if (!sourceIsInput && !targetIsInput)
        {
            // Both outputs - invalid
            StatusMessage = "Cannot connect two outputs";
            CancelConnection();
            return;
        }

        Connect(source, target);
        CancelConnection();
    }

    /// <summary>
    /// Cancels the pending connection.
    /// </summary>
    public void CancelConnection()
    {
        PendingConnection.Source = null;
        PendingConnection.IsVisible = false;
    }

    /// <summary>
    /// Disconnects all connections from the specified connector.
    /// Called when user wants to remove connections from a port.
    /// </summary>
    public void DisconnectConnector(ConnectorViewModel? connector)
    {
        if (connector == null) return;

        // Find and remove all connections involving this connector
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            var conn = Connections[i];
            if (conn.SourceConnector == connector || conn.TargetConnector == connector)
            {
                RemoveConnection(conn);
            }
        }

        StatusMessage = $"Disconnected {connector.Node?.Title}.{connector.Title}";
    }

    private bool HasOtherConnections(ConnectorViewModel connector, ConnectionViewModel excludeConnection)
    {
        foreach (var conn in Connections)
        {
            if (conn == excludeConnection)
                continue;
            if (conn.SourceConnector == connector || conn.TargetConnector == connector)
                return true;
        }
        return false;
    }

    private static string GetNodeTitle(NodeType type) => type switch
    {
        NodeType.EventSkillUsed => "Skill Used",
        NodeType.EventDamageReceived => "Damage Received",
        NodeType.EventActorKilled => "Actor Killed",
        NodeType.EventRoundStart => "Round Start",
        NodeType.EventRoundEnd => "Round End",
        NodeType.EventTurnEnd => "Turn End",
        NodeType.Condition => "Condition",
        NodeType.And => "AND",
        NodeType.Or => "OR",
        NodeType.Not => "NOT",
        NodeType.ActionAddEffect => "Add Effect",
        NodeType.ActionDamage => "Damage",
        NodeType.ActionHeal => "Heal",
        NodeType.ActionSetFlag => "Set Flag",
        NodeType.ActionLog => "Log",
        _ => "Node"
    };

    private static void ConfigureNodeConnectors(NodeViewModel node, NodeType type)
    {
        switch (type)
        {
            case NodeType.EventSkillUsed:
                node.Output.Add(new ConnectorViewModel { Title = "actor", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "skill", Node = node });
                break;

            case NodeType.EventDamageReceived:
                node.Output.Add(new ConnectorViewModel { Title = "target", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "attacker", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "amount", Node = node });
                break;

            case NodeType.EventActorKilled:
                node.Output.Add(new ConnectorViewModel { Title = "actor", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "killer", Node = node });
                break;

            case NodeType.EventRoundStart:
            case NodeType.EventRoundEnd:
                node.Output.Add(new ConnectorViewModel { Title = "round", Node = node });
                break;

            case NodeType.EventTurnEnd:
                node.Output.Add(new ConnectorViewModel { Title = "actor", Node = node });
                break;

            case NodeType.Condition:
                node.Input.Add(new ConnectorViewModel { Title = "input", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "pass", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "fail", Node = node });
                break;

            case NodeType.And:
            case NodeType.Or:
                node.Input.Add(new ConnectorViewModel { Title = "a", Node = node });
                node.Input.Add(new ConnectorViewModel { Title = "b", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "result", Node = node });
                break;

            case NodeType.Not:
                node.Input.Add(new ConnectorViewModel { Title = "input", Node = node });
                node.Output.Add(new ConnectorViewModel { Title = "result", Node = node });
                break;

            case NodeType.ActionAddEffect:
                node.Input.Add(new ConnectorViewModel { Title = "actor", Node = node });
                node.Input.Add(new ConnectorViewModel { Title = "trigger", Node = node });
                break;

            case NodeType.ActionDamage:
            case NodeType.ActionHeal:
                node.Input.Add(new ConnectorViewModel { Title = "actor", Node = node });
                node.Input.Add(new ConnectorViewModel { Title = "trigger", Node = node });
                break;

            case NodeType.ActionSetFlag:
                node.Input.Add(new ConnectorViewModel { Title = "actor", Node = node });
                node.Input.Add(new ConnectorViewModel { Title = "trigger", Node = node });
                break;

            case NodeType.ActionLog:
                node.Input.Add(new ConnectorViewModel { Title = "trigger", Node = node });
                break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
