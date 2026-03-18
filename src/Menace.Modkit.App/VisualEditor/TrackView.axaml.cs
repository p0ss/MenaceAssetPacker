#nullable enable

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
    public required StationDefinition Definition { get; init; }
    public List<string> AttachedMods { get; } = new();
    public Point Position { get; set; }

    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public int Depth => Definition.Depth;
    public string? ParentId => Definition.ParentId;
}

/// <summary>
/// Definition of a station (hook point) in the game cycle.
/// </summary>
public class StationDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public int Depth { get; init; }
    public string? ParentId { get; init; }
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

    // Simplified tactical cycle - main hook points only
    public static readonly IReadOnlyList<StationDefinition> TacticalCycle = new[]
    {
        new StationDefinition { Id = "round_start", Name = "Round Start", Description = "Combat round begins", AvailableData = new[] { "round" } },
        new StationDefinition { Id = "turn_start", Name = "Turn Start", Description = "Actor's turn begins", AvailableData = new[] { "actor" } },
        new StationDefinition { Id = "skill_used", Name = "Skill Used", Description = "Actor uses ability", AvailableData = new[] { "actor", "skill", "target" } },
        new StationDefinition { Id = "damage_dealt", Name = "Damage Dealt", Description = "Damage is applied", AvailableData = new[] { "target", "attacker", "amount" } },
        new StationDefinition { Id = "actor_killed", Name = "Actor Killed", Description = "Actor dies", AvailableData = new[] { "actor", "killer" } },
        new StationDefinition { Id = "turn_end", Name = "Turn End", Description = "Actor's turn ends", AvailableData = new[] { "actor" } },
        new StationDefinition { Id = "round_end", Name = "Round End", Description = "Combat round ends", AvailableData = new[] { "round" } },
    };

    // Simplified strategic cycle
    public static readonly IReadOnlyList<StationDefinition> StrategicCycle = new[]
    {
        new StationDefinition { Id = "mission_start", Name = "Mission Start", Description = "Mission begins", AvailableData = new[] { "mission" } },
        new StationDefinition { Id = "squad_deployed", Name = "Squad Deployed", Description = "Squad enters mission", AvailableData = new[] { "squad" } },
        new StationDefinition { Id = "objective_complete", Name = "Objective Done", Description = "Objective completed", AvailableData = new[] { "objective" } },
        new StationDefinition { Id = "mission_end", Name = "Mission End", Description = "Mission completes", AvailableData = new[] { "mission", "success" } },
        new StationDefinition { Id = "loot_acquired", Name = "Loot Acquired", Description = "Items obtained", AvailableData = new[] { "items" } },
    };
}

/// <summary>
/// Track View component that visualizes the game cycle as a timeline with stations.
/// Each station is a hook point where modders can attach logic.
/// </summary>
public partial class TrackView : UserControl
{
    private CycleType _currentCycle = CycleType.Tactical;
    private string? _selectedStationId;
    private readonly Dictionary<string, Border> _stationControls = new();
    private List<StationState> _currentStations = new();

    // Colors matching the Modkit theme
    private static readonly SolidColorBrush StationBackground = new(Color.Parse("#252525"));
    private static readonly SolidColorBrush StationBackgroundHover = new(Color.Parse("#333333"));
    private static readonly SolidColorBrush StationBackgroundSelected = new(Color.Parse("#1a3d38"));
    private static readonly SolidColorBrush StationBorder = new(Color.Parse("#3E3E3E"));
    private static readonly SolidColorBrush StationBorderActive = new(Color.Parse("#004f43"));
    private static readonly SolidColorBrush StationBorderSelected = new(Color.Parse("#00a88f"));
    private static readonly SolidColorBrush ConnectorLine = new(Color.Parse("#3E3E3E"));
    private static readonly SolidColorBrush TextPrimary = new(Color.Parse("#FFFFFF"));
    private static readonly SolidColorBrush TextSecondary = new(Color.Parse("#AAAAAA"));
    private static readonly SolidColorBrush TextMuted = new(Color.Parse("#666666"));
    private static readonly SolidColorBrush ModBadgeBackground = new(Color.Parse("#003d34"));
    private static readonly SolidColorBrush ModBadgeText = new(Color.Parse("#8ECDC8"));

    /// <summary>
    /// Event raised when a station is clicked.
    /// </summary>
    public event EventHandler<StationClickedEventArgs>? StationClicked;

    /// <summary>
    /// Event raised when the "Add" button on a station is clicked.
    /// </summary>
    public event EventHandler<StationClickedEventArgs>? AddModClicked;

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

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        RenderCycle(_currentCycle);
    }

    private void OnTacticalTabClick(object? sender, RoutedEventArgs e)
    {
        CurrentCycle = CycleType.Tactical;
    }

    private void OnStrategicTabClick(object? sender, RoutedEventArgs e)
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
        const double startX = 20;
        const double startY = 20;
        const double horizontalSpacing = 160;

        // Simple horizontal layout - all stations in a row
        for (int i = 0; i < stations.Count; i++)
        {
            var station = stations[i];
            station.Position = new Point(startX + (i * horizontalSpacing), startY);
        }
    }

    private void DrawConnectors(List<StationState> stations)
    {
        const double stationWidth = 140;
        const double stationHeight = 60;

        for (int i = 0; i < stations.Count - 1; i++)
        {
            var from = stations[i];
            var to = stations[i + 1];

            // Draw horizontal line from right edge of 'from' to left edge of 'to'
            var fromX = from.Position.X + stationWidth;
            var fromY = from.Position.Y + (stationHeight / 2);
            var toX = to.Position.X;
            var toY = to.Position.Y + (stationHeight / 2);

            // Main line
            var line = new Line
            {
                StartPoint = new Point(fromX, fromY),
                EndPoint = new Point(toX - 8, toY),
                Stroke = ConnectorLine,
                StrokeThickness = 2
            };
            TrackCanvas.Children.Add(line);

            // Arrowhead
            var arrow = new Polygon
            {
                Points = new Points
                {
                    new Point(toX, toY),
                    new Point(toX - 10, toY - 5),
                    new Point(toX - 10, toY + 5)
                },
                Fill = StationBorderActive
            };
            TrackCanvas.Children.Add(arrow);
        }
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

    private void OnStationClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is StationState station)
        {
            SelectStation(station.Id);
            StationClicked?.Invoke(this, new StationClickedEventArgs(station));
        }
    }

    /// <summary>
    /// Selects a station by ID, highlighting it visually.
    /// </summary>
    public void SelectStation(string? stationId)
    {
        // Deselect previous
        if (_selectedStationId != null && _stationControls.TryGetValue(_selectedStationId, out var prevBorder))
        {
            var prevStation = _currentStations.FirstOrDefault(s => s.Id == _selectedStationId);
            bool hasMods = prevStation?.AttachedMods.Count > 0;
            prevBorder.Background = StationBackground;
            prevBorder.BorderBrush = hasMods ? StationBorderActive : StationBorder;
            prevBorder.BorderThickness = new Thickness(hasMods ? 2 : 1);
        }

        _selectedStationId = stationId;

        // Select new
        if (stationId != null && _stationControls.TryGetValue(stationId, out var newBorder))
        {
            newBorder.Background = StationBackgroundSelected;
            newBorder.BorderBrush = StationBorderSelected;
            newBorder.BorderThickness = new Thickness(2);
        }
    }

    /// <summary>
    /// Gets the currently selected station ID.
    /// </summary>
    public string? SelectedStationId => _selectedStationId;

    private void OnAddModButtonClick(object? sender, RoutedEventArgs e)
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
