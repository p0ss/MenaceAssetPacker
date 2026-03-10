#nullable disable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Represents a connector (input or output port) on a node.
/// </summary>
public class ConnectorViewModel : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private bool _isConnected;
    private Point _anchor;

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public Point Anchor
    {
        get => _anchor;
        set { _anchor = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Reference back to the node that owns this connector.
    /// </summary>
    public NodeViewModel? Node { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Represents a node in the visual editor graph.
/// </summary>
public class NodeViewModel : INotifyPropertyChanged
{
    private Point _location;
    private string _title = "Node";
    private bool _isSelected;

    public Point Location
    {
        get => _location;
        set { _location = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ConnectorViewModel> Input { get; } = new();
    public ObservableCollection<ConnectorViewModel> Output { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Represents a connection between two node connectors.
/// </summary>
public class ConnectionViewModel : INotifyPropertyChanged
{
    private ConnectorViewModel? _source;
    private ConnectorViewModel? _target;

    public ConnectorViewModel? Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    public ConnectorViewModel? Target
    {
        get => _target;
        set { _target = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Represents a pending connection being dragged from a connector.
/// </summary>
public class PendingConnectionViewModel : INotifyPropertyChanged
{
    private ConnectorViewModel? _source;
    private Point _targetLocation;
    private bool _isVisible;

    public ConnectorViewModel? Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    public Point TargetLocation
    {
        get => _targetLocation;
        set { _targetLocation = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Node types supported by the visual editor.
/// </summary>
public enum NodeType
{
    // Event nodes - entry points into the graph
    EventSkillUsed,
    EventDamageReceived,
    EventActorKilled,
    EventRoundStart,
    EventRoundEnd,
    EventTurnEnd,

    // Condition nodes - filter/branch the flow
    Condition,

    // Logic nodes
    And,
    Or,
    Not,

    // Action nodes - produce effects
    ActionAddEffect,
    ActionDamage,
    ActionHeal,
    ActionSetFlag,
    ActionLog
}
