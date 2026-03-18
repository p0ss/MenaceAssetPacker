#nullable enable

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
    private ConnectorViewModel? _sourceConnector;
    private ConnectorViewModel? _targetConnector;

    public ConnectionViewModel()
    {
    }

    /// <summary>
    /// The source connector (for internal reference).
    /// </summary>
    public ConnectorViewModel? SourceConnector
    {
        get => _sourceConnector;
        set
        {
            if (_sourceConnector != null)
                _sourceConnector.PropertyChanged -= OnConnectorPropertyChanged;
            _sourceConnector = value;
            if (_sourceConnector != null)
                _sourceConnector.PropertyChanged += OnConnectorPropertyChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Source));
        }
    }

    /// <summary>
    /// The target connector (for internal reference).
    /// </summary>
    public ConnectorViewModel? TargetConnector
    {
        get => _targetConnector;
        set
        {
            if (_targetConnector != null)
                _targetConnector.PropertyChanged -= OnConnectorPropertyChanged;
            _targetConnector = value;
            if (_targetConnector != null)
                _targetConnector.PropertyChanged += OnConnectorPropertyChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Target));
        }
    }

    /// <summary>
    /// Source anchor point for connection drawing (used by Nodify).
    /// </summary>
    public Point Source => _sourceConnector?.Anchor ?? default;

    /// <summary>
    /// Target anchor point for connection drawing (used by Nodify).
    /// </summary>
    public Point Target => _targetConnector?.Anchor ?? default;

    private void OnConnectorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectorViewModel.Anchor))
        {
            if (sender == _sourceConnector)
                OnPropertyChanged(nameof(Source));
            else if (sender == _targetConnector)
                OnPropertyChanged(nameof(Target));
        }
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
        set
        {
            if (_source != null)
                _source.PropertyChanged -= OnSourcePropertyChanged;
            _source = value;
            if (_source != null)
                _source.PropertyChanged += OnSourcePropertyChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceAnchor));
        }
    }

    /// <summary>
    /// Source anchor point for pending connection drawing.
    /// </summary>
    public Point SourceAnchor => _source?.Anchor ?? default;

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

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectorViewModel.Anchor))
        {
            OnPropertyChanged(nameof(SourceAnchor));
        }
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
