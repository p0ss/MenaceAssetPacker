#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Type of game cycle.
/// </summary>
public enum GameCycle
{
    Tactical,
    Strategic
}

/// <summary>
/// Game steps in the tactical cycle (columns).
/// </summary>
public enum TacticalStep
{
    RoundStart,
    PlayerTurnStart,
    PlayerMove,
    PlayerSkill,
    PlayerTurnEnd,
    EnemyTurnStart,
    EnemyMove,
    EnemySkill,
    EnemyTurnEnd,
    RoundEnd
}

/// <summary>
/// Game steps in the strategic cycle (columns).
/// </summary>
public enum StrategicStep
{
    OperationStart,
    AlertFired,
    RosterManagement,
    BlackMarket,
    MissionSelect,
    MissionBrief,
    TacticalCycle,
    MissionDebrief,
    OperationEnd
}

/// <summary>
/// Data types that can flow through connections.
/// </summary>
public enum PortDataType
{
    Any,        // Accepts anything
    Actor,      // Actor reference
    Skill,      // Skill reference
    Tile,       // Tile reference
    Number,     // int or float
    Boolean,    // true/false
    String      // text
}

/// <summary>
/// Types of nodes in the graph.
/// </summary>
public enum TimelineNodeType
{
    // Triggers (entry points)
    TriggerSkillUsed,
    TriggerDamageReceived,
    TriggerActorKilled,
    TriggerMovementStarted,
    TriggerMovementFinished,
    TriggerTurnStart,
    TriggerTurnEnd,
    TriggerRoundStart,
    TriggerRoundEnd,
    TriggerHitpointsChanged,
    TriggerSuppressed,

    // Data nodes
    Actor,
    Skill,
    Tile,

    // Template selector nodes (let user pick a specific template ID)
    TemplateRefActor,
    TemplateRefSkill,
    TemplateRefItem,

    // Logic
    Condition,
    Compare,
    MathOp,
    Not,
    And,
    Or,

    // Variables
    SetVariable,
    GetVariable,

    // Loops
    ForEachActor,
    ForEachTileInRadius,
    ForEachSkill,

    // Timing/Coroutines
    Delay,              // Wait N rounds before continuing
    Repeat,             // Execute every N rounds, M times total
    Once,               // Execute only once (per entity/combat)

    // State Machine
    State,              // Define a named state
    Transition,         // Transition between states on condition
    GetState,           // Check current state
    SetState,           // Force set to a state

    // Effects
    Effect,

    // Actions
    ActionMoveTo,
    ActionApplyDamage,
    ActionHeal,
    ActionApplySuppression,
    ActionAddSkill,
    ActionRemoveSkill,
    ActionSpawn,
    ActionKill,
    ActionLog
}

/// <summary>
/// A port (input or output) on a node.
/// </summary>
public class NodePort : INotifyPropertyChanged
{
    private bool _isConnected;
    private string? _resolvedType;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public PortDataType DataType { get; set; } = PortDataType.Any;
    public bool IsOutput { get; set; }
    public TimelineNode? Node { get; set; }

    /// <summary>
    /// The resolved type flowing through this port.
    /// Examples: "Actor", "ActorTemplate:soldier_rifleman", "Skill", "Boolean", "Number"
    /// This is computed by type propagation and reflects the actual data flowing through connections.
    /// </summary>
    public string? ResolvedType
    {
        get => _resolvedType;
        set { _resolvedType = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Check if this port can connect to another port.
    /// </summary>
    public bool CanConnectTo(NodePort other)
    {
        if (other == null || other == this) return false;
        if (other.Node == Node) return false;  // No self-connections
        if (IsOutput == other.IsOutput) return false;  // Must be input->output

        // Type compatibility
        if (DataType == PortDataType.Any || other.DataType == PortDataType.Any)
            return true;

        return DataType == other.DataType;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A node in the timeline graph.
/// </summary>
public class TimelineNode : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private bool _isSelected;
    private string _title = "Node";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public TimelineNodeType NodeType { get; set; }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// X position on canvas.
    /// </summary>
    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Y position on canvas.
    /// </summary>
    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    // Legacy column properties for compatibility
    [Obsolete("Use X/Y instead")]
    public int Column { get => (int)(X / 120); set => X = value * 120; }
    [Obsolete("Use X/Y instead")]
    public int ColumnSpan { get; set; } = 1;
    [Obsolete("Use X/Y instead")]
    public int Row { get => (int)(Y / 90); set => Y = value * 90; }

    /// <summary>
    /// Node-specific properties (varies by node type).
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = new();

    /// <summary>
    /// Input ports.
    /// </summary>
    public ObservableCollection<NodePort> Inputs { get; } = new();

    /// <summary>
    /// Output ports.
    /// </summary>
    public ObservableCollection<NodePort> Outputs { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A connection between two ports.
/// </summary>
public class NodeConnection : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public NodePort? SourcePort { get; set; }
    public NodePort? TargetPort { get; set; }

    public TimelineNode? SourceNode => SourcePort?.Node;
    public TimelineNode? TargetNode => TargetPort?.Node;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The complete timeline graph.
/// </summary>
public class TimelineGraph : INotifyPropertyChanged
{
    private string _name = "New Effect";
    private GameCycle _cycle = GameCycle.Tactical;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public GameCycle Cycle
    {
        get => _cycle;
        set { _cycle = value; OnPropertyChanged(); OnPropertyChanged(nameof(ColumnCount)); }
    }

    public ObservableCollection<TimelineNode> Nodes { get; } = new();
    public ObservableCollection<NodeConnection> Connections { get; } = new();

    /// <summary>
    /// Number of columns based on cycle type.
    /// </summary>
    public int ColumnCount => Cycle == GameCycle.Tactical ? 10 : 9;

    /// <summary>
    /// Get column names for the current cycle.
    /// </summary>
    public string[] GetColumnNames()
    {
        if (Cycle == GameCycle.Tactical)
        {
            return new[]
            {
                "Round\nStart", "Player\nTurn", "Player\nMove", "Player\nSkill", "Player\nEnd",
                "Enemy\nTurn", "Enemy\nMove", "Enemy\nSkill", "Enemy\nEnd", "Round\nEnd"
            };
        }
        else
        {
            return new[]
            {
                "Op\nStart", "Alert", "Roster", "Market", "Mission\nSelect",
                "Brief", "Tactical", "Debrief", "Op\nEnd"
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
