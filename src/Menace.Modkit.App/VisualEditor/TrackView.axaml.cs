#nullable disable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Runtime state for a station in the TrackView.
/// Combines the static definition with current UI state.
/// </summary>
public class StationState
{
    public StationDefinition Definition { get; init; }
    public List<string> AttachedMods { get; } = new();
    public Point Position { get; set; }

    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public int Depth => Definition.Depth;
    public string ParentId => Definition.ParentId;
}

/// <summary>
/// Definition of a station (hook point) in the game cycle.
/// </summary>
public class StationDefinition
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public int Depth { get; init; }
    public string ParentId { get; init; }
    public string[] AvailableData { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Type of game cycle.
/// </summary>
public enum CycleType
{
    Tactical,
    Strategic
}

/// <summary>
/// Provides station definitions for each game cycle.
/// </summary>
public static class GameCycleDefinitions
{
    public static IReadOnlyList<StationDefinition> GetCycle(CycleType cycle)
    {
        return cycle switch
        {
            CycleType.Tactical => TacticalCycle,
            CycleType.Strategic => StrategicCycle,
            _ => TacticalCycle
        };
    }

    public static readonly IReadOnlyList<StationDefinition> TacticalCycle = new[]
    {
        new StationDefinition { Id = "round_start", Name = "Round Start", Description = "Beginning of combat round", Depth = 0, AvailableData = new[] { "round_number" } },
        new StationDefinition { Id = "faction_turn", Name = "Faction Turn", Description = "A faction begins acting", Depth = 1, ParentId = "round_start", AvailableData = new[] { "faction" } },
        new StationDefinition { Id = "turn_start", Name = "Turn Start", Description = "An actor begins their turn", Depth = 2, ParentId = "faction_turn", AvailableData = new[] { "actor" } },
        new StationDefinition { Id = "move_start", Name = "Move Start", Description = "Actor begins moving", Depth = 3, ParentId = "turn_start", AvailableData = new[] { "actor", "from_tile", "to_tile" } },
        new StationDefinition { Id = "move_complete", Name = "Move Complete", Description = "Actor finished moving", Depth = 3, ParentId = "turn_start", AvailableData = new[] { "actor", "tile" } },
        new StationDefinition { Id = "skill_used", Name = "Skill Used", Description = "Actor uses a skill", Depth = 3, ParentId = "turn_start", AvailableData = new[] { "actor", "skill", "target" } },
        new StationDefinition { Id = "damage_received", Name = "Damage Received", Description = "Entity takes damage", Depth = 4, ParentId = "skill_used", AvailableData = new[] { "target", "attacker", "skill", "amount" } },
        new StationDefinition { Id = "attack_missed", Name = "Attack Missed", Description = "Attack fails to hit", Depth = 4, ParentId = "skill_used", AvailableData = new[] { "target", "attacker", "skill" } },
        new StationDefinition { Id = "actor_killed", Name = "Actor Killed", Description = "Entity dies", Depth = 4, ParentId = "skill_used", AvailableData = new[] { "actor", "killer", "skill" } },
        new StationDefinition { Id = "turn_end", Name = "Turn End", Description = "Actor's turn ends", Depth = 2, ParentId = "faction_turn", AvailableData = new[] { "actor" } },
        new StationDefinition { Id = "round_end", Name = "Round End", Description = "Combat round ends", Depth = 0, AvailableData = new[] { "round_number" } },
    };

    public static readonly IReadOnlyList<StationDefinition> StrategicCycle = new[]
    {
        new StationDefinition { Id = "operation_start", Name = "Operation Start", Description = "New operation begins", Depth = 0, AvailableData = new[] { "operation" } },
        new StationDefinition { Id = "leader_hired", Name = "Leader Hired", Description = "New leader joins roster", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "leader" } },
        new StationDefinition { Id = "leader_dismissed", Name = "Leader Dismissed", Description = "Leader leaves roster", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "leader" } },
        new StationDefinition { Id = "blackmarket_restocked", Name = "Black Market", Description = "Market inventory changes", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "items" } },
        new StationDefinition { Id = "faction_trust_changed", Name = "Faction Trust", Description = "Faction relations change", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "faction", "old_trust", "new_trust" } },
        new StationDefinition { Id = "mission_start", Name = "Mission Start", Description = "Mission begins", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "mission" } },
        new StationDefinition { Id = "tactical_cycle", Name = "Tactical Cycle", Description = "Combat occurs", Depth = 2, ParentId = "mission_start", AvailableData = new[] { "mission" } },
        new StationDefinition { Id = "mission_ended", Name = "Mission End", Description = "Mission completes", Depth = 1, ParentId = "operation_start", AvailableData = new[] { "mission", "success" } },
        new StationDefinition { Id = "operation_finished", Name = "Operation End", Description = "Operation completes", Depth = 0, AvailableData = new[] { "operation", "success" } },
    };
}

/// <summary>
/// Track View component that visualizes the game cycle as a timeline with stations.
/// Each station is a hook point where modders can attach logic.
/// </summary>
public partial class TrackView : UserControl
{
    private CycleType _currentCycle = CycleType.Tactical;
    private readonly Dictionary<string, Border> _stationControls = new();
    private List<StationState> _currentStations = new();

    // Colors matching the Modkit theme
    private static readonly SolidColorBrush StationBackground = new(Color.Parse("#252525"));
    private static readonly SolidColorBrush StationBackgroundHover = new(Color.Parse("#333333"));
    private static readonly SolidColorBrush StationBorder = new(Color.Parse("#3E3E3E"));
    private static readonly SolidColorBrush StationBorderActive = new(Color.Parse("#004f43"));
    private static readonly SolidColorBrush ConnectorLine = new(Color.Parse("#3E3E3E"));
    private static readonly SolidColorBrush TextPrimary = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush TextSecondary = new(Color.Parse("#AAAAAA"));
    private static readonly SolidColorBrush TextMuted = new(Color.Parse("#666666"));
    private static readonly SolidColorBrush ModBadgeBackground = new(Color.Parse("#003d34"));
    private static readonly SolidColorBrush ModBadgeText = new(Color.Parse("#8ECDC8"));

    /// <summary>
    /// Event raised when a station is clicked.
    /// </summary>
    public event EventHandler<StationClickedEventArgs> StationClicked;

    /// <summary>
    /// Event raised when the "Add" button on a station is clicked.
    /// </summary>
    public event EventHandler<StationClickedEventArgs> AddModClicked;

    /// <summary>
    /// Gets or sets the currently displayed cycle type.
    /// </summary>
    public CycleType CurrentCycle
    {
        get => _currentCycle;
        set
        {
            if (_currentCycle != value)
            {
                _currentCycle = value;
                UpdateTabStyles();
                RenderCycle(_currentCycle);
            }
        }
    }

    public TrackView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderCycle(_currentCycle);
    }

    private void OnTacticalTabClick(object sender, RoutedEventArgs e)
    {
        CurrentCycle = CycleType.Tactical;
    }

    private void OnStrategicTabClick(object sender, RoutedEventArgs e)
    {
        CurrentCycle = CycleType.Strategic;
    }

    private void UpdateTabStyles()
    {
        if (_currentCycle == CycleType.Tactical)
        {
            TacticalTabButton.Classes.Clear();
            TacticalTabButton.Classes.Add("cycleTabActive");
            StrategicTabButton.Classes.Clear();
            StrategicTabButton.Classes.Add("cycleTab");
        }
        else
        {
            TacticalTabButton.Classes.Clear();
            TacticalTabButton.Classes.Add("cycleTab");
            StrategicTabButton.Classes.Clear();
            StrategicTabButton.Classes.Add("cycleTabActive");
        }
    }

    /// <summary>
    /// Renders the specified game cycle on the canvas.
    /// </summary>
    public void RenderCycle(CycleType cycle)
    {
        TrackCanvas.Children.Clear();
        _stationControls.Clear();

        // Get station definitions and create states
        var definitions = GameCycleDefinitions.GetCycle(cycle);
        _currentStations = definitions.Select(d => new StationState { Definition = d }).ToList();

        // Add sample attached mod to skill_used for demonstration
        var skillUsed = _currentStations.FirstOrDefault(s => s.Id == "skill_used");
        if (skillUsed != null && skillUsed.AttachedMods.Count == 0)
        {
            skillUsed.AttachedMods.Add("Beagle's Concealment");
        }

        // Calculate positions
        LayoutStations(_currentStations);

        // Draw connectors first (so they're behind stations)
        DrawConnectors(_currentStations);

        // Draw stations
        foreach (var station in _currentStations)
        {
            var stationControl = CreateStationControl(station);
            Canvas.SetLeft(stationControl, station.Position.X);
            Canvas.SetTop(stationControl, station.Position.Y);
            TrackCanvas.Children.Add(stationControl);
            _stationControls[station.Id] = stationControl;
        }

        // Update canvas size
        if (_currentStations.Count > 0)
        {
            var maxX = _currentStations.Max(s => s.Position.X) + 200;
            var maxY = _currentStations.Max(s => s.Position.Y) + 100;
            TrackCanvas.Width = Math.Max(800, maxX);
            TrackCanvas.Height = Math.Max(400, maxY);
        }
    }

    private void LayoutStations(List<StationState> stations)
    {
        const double startX = 40;
        const double startY = 40;
        const double horizontalSpacing = 180;
        const double verticalSpacing = 80;
        const double depthIndent = 40;

        double currentX = startX;
        double currentY = startY;
        int lastDepth = 0;

        foreach (var station in stations)
        {
            // Handle depth changes for nested structures
            if (station.Depth > lastDepth)
            {
                currentY += verticalSpacing;
                currentX = startX + (station.Depth * depthIndent);
            }
            else if (station.Depth < lastDepth)
            {
                currentY += verticalSpacing;
                currentX = startX + (station.Depth * depthIndent);
            }
            else
            {
                currentX += horizontalSpacing;
            }

            station.Position = new Point(currentX, currentY);
            lastDepth = station.Depth;
        }
    }

    private void DrawConnectors(List<StationState> stations)
    {
        for (int i = 0; i < stations.Count - 1; i++)
        {
            var from = stations[i];
            var to = stations[i + 1];

            // Calculate connection points (from right edge of 'from' to left edge of 'to')
            var fromX = from.Position.X + 120; // Approximate width
            var fromY = from.Position.Y + 30;  // Approximate center height
            var toX = to.Position.X;
            var toY = to.Position.Y + 30;

            // For stations at different depths, draw an L-shaped connector
            if (Math.Abs(from.Depth - to.Depth) > 0 || Math.Abs(fromY - toY) > 10)
            {
                DrawLConnector(fromX, fromY, toX, toY);
            }
            else
            {
                // Simple horizontal line with arrow
                DrawArrowLine(fromX, fromY, toX, toY);
            }
        }
    }

    private void DrawArrowLine(double x1, double y1, double x2, double y2)
    {
        // Main line
        var line = new Line
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(x2 - 8, y2),
            Stroke = ConnectorLine,
            StrokeThickness = 2
        };
        TrackCanvas.Children.Add(line);

        // Arrowhead
        var arrow = new Polygon
        {
            Points = new Points
            {
                new Point(x2, y2),
                new Point(x2 - 10, y2 - 5),
                new Point(x2 - 10, y2 + 5)
            },
            Fill = StationBorderActive
        };
        TrackCanvas.Children.Add(arrow);
    }

    private void DrawLConnector(double x1, double y1, double x2, double y2)
    {
        var midX = x1 + 20;

        // Horizontal segment from source
        var line1 = new Line
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(midX, y1),
            Stroke = ConnectorLine,
            StrokeThickness = 2
        };
        TrackCanvas.Children.Add(line1);

        // Vertical segment
        var line2 = new Line
        {
            StartPoint = new Point(midX, y1),
            EndPoint = new Point(midX, y2),
            Stroke = ConnectorLine,
            StrokeThickness = 2
        };
        TrackCanvas.Children.Add(line2);

        // Horizontal segment to target
        var line3 = new Line
        {
            StartPoint = new Point(midX, y2),
            EndPoint = new Point(x2 - 8, y2),
            Stroke = ConnectorLine,
            StrokeThickness = 2
        };
        TrackCanvas.Children.Add(line3);

        // Arrowhead
        var arrow = new Polygon
        {
            Points = new Points
            {
                new Point(x2, y2),
                new Point(x2 - 10, y2 - 5),
                new Point(x2 - 10, y2 + 5)
            },
            Fill = StationBorderActive
        };
        TrackCanvas.Children.Add(arrow);
    }

    private Border CreateStationControl(StationState station)
    {
        var hasAttachedMods = station.AttachedMods.Count > 0;

        var border = new Border
        {
            Background = StationBackground,
            BorderBrush = hasAttachedMods ? StationBorderActive : StationBorder,
            BorderThickness = new Thickness(hasAttachedMods ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            MinWidth = 120,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = station
        };

        var content = new StackPanel { Spacing = 4 };

        // Station name
        content.Children.Add(new TextBlock
        {
            Text = station.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextPrimary
        });

        // Station description
        if (!string.IsNullOrEmpty(station.Description))
        {
            content.Children.Add(new TextBlock
            {
                Text = station.Description,
                FontSize = 11,
                Foreground = TextMuted,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 140
            });
        }

        // Attached mods
        if (hasAttachedMods)
        {
            var modsPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var mod in station.AttachedMods.Take(3))
            {
                var badge = new Border
                {
                    Background = ModBadgeBackground,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2),
                    Margin = new Thickness(0, 0, 4, 4),
                    Child = new TextBlock
                    {
                        Text = mod,
                        FontSize = 10,
                        Foreground = ModBadgeText
                    }
                };
                modsPanel.Children.Add(badge);
            }

            if (station.AttachedMods.Count > 3)
            {
                modsPanel.Children.Add(new TextBlock
                {
                    Text = $"+{station.AttachedMods.Count - 3} more",
                    FontSize = 10,
                    Foreground = TextMuted,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
            }

            content.Children.Add(modsPanel);
        }

        // Add button
        var addButton = new Button
        {
            Content = "+ Add",
            FontSize = 11,
            Background = Brushes.Transparent,
            Foreground = TextMuted,
            BorderBrush = StationBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 4, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = station
        };
        addButton.Click += OnAddModButtonClick;
        content.Children.Add(addButton);

        border.Child = content;

        // Hover effects
        border.PointerEntered += (s, e) =>
        {
            border.Background = StationBackgroundHover;
            if (!hasAttachedMods)
                border.BorderBrush = StationBorderActive;
        };

        border.PointerExited += (s, e) =>
        {
            border.Background = StationBackground;
            if (!hasAttachedMods)
                border.BorderBrush = StationBorder;
        };

        // Click handler
        border.PointerPressed += OnStationClick;

        return border;
    }

    private void OnStationClick(object sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: StationState station })
        {
            StationClicked?.Invoke(this, new StationClickedEventArgs(station));
        }
    }

    private void OnAddModButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StationState station })
        {
            e.Handled = true; // Prevent bubble to station click
            AddModClicked?.Invoke(this, new StationClickedEventArgs(station));
        }
    }

    /// <summary>
    /// Updates a specific station to show attached mods.
    /// </summary>
    public void UpdateStationMods(string stationId, IEnumerable<string> mods)
    {
        var station = _currentStations.FirstOrDefault(s => s.Id == stationId);
        if (station != null)
        {
            station.AttachedMods.Clear();
            station.AttachedMods.AddRange(mods);
            RenderCycle(_currentCycle);
        }
    }

    /// <summary>
    /// Gets the current state of all stations including attached mods.
    /// </summary>
    public IReadOnlyList<StationState> GetStationStates() => _currentStations.AsReadOnly();
}

/// <summary>
/// Event args for station click events.
/// </summary>
public class StationClickedEventArgs : EventArgs
{
    public StationState Station { get; }
    public string StationId => Station.Id;
    public string StationName => Station.Name;

    public StationClickedEventArgs(StationState station)
    {
        Station = station;
    }
}
