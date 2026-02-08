using System;
using System.Collections.Generic;

namespace Menace.Modkit.App.Models;

/// <summary>
/// StrategyVars indices from the game.
/// </summary>
public static class StrategyVarIndex
{
    public const int OciComponents = 1;        // Credits / currency
    public const int PromotionPoints = 2;       // Points for unit promotions
    public const int PromotionPointsEarned = 3; // Total earned (lifetime)
    public const int OperationsPlayed = 4;
    public const int OperationsWon = 5;
    public const int DemoteRefundPercentage = 11;
    public const int OperationsTimeoutBonus = 12;
    public const int Intelligence = 13;         // Intel resource
    public const int OciRefundPercentage = 14;
    public const int Authority = 15;            // Authority resource
    public const int OciComponentsEarned = 16;  // Total credits earned (lifetime)
    public const int StoryCheckpoints = 17;
}

/// <summary>
/// Parsed save body data from StrategyState.
/// </summary>
public class SaveBodyData
{
    // StrategyState fields
    public double TotalPlayTimeInSec { get; set; }
    public bool Ironman { get; set; }
    public string IronmanSaveGameName { get; set; } = "";
    public int Seed { get; set; }
    public bool HasPickedInitialItemPack { get; set; }
    public bool HasPickedInitialLeaders { get; set; }
    public string GlobalDifficulty { get; set; } = "";
    public int[] StrategyVars { get; set; } = Array.Empty<int>();

    // --- Resource accessors (backed by StrategyVars) ---

    /// <summary>
    /// Credits / OCI Components (index 1).
    /// </summary>
    public int Credits
    {
        get => GetVar(StrategyVarIndex.OciComponents);
        set => SetVar(StrategyVarIndex.OciComponents, value);
    }

    /// <summary>
    /// Intelligence resource (index 13).
    /// </summary>
    public int Intelligence
    {
        get => GetVar(StrategyVarIndex.Intelligence);
        set => SetVar(StrategyVarIndex.Intelligence, value);
    }

    /// <summary>
    /// Authority resource (index 15).
    /// </summary>
    public int Authority
    {
        get => GetVar(StrategyVarIndex.Authority);
        set => SetVar(StrategyVarIndex.Authority, value);
    }

    /// <summary>
    /// Promotion points (index 2).
    /// </summary>
    public int PromotionPoints
    {
        get => GetVar(StrategyVarIndex.PromotionPoints);
        set => SetVar(StrategyVarIndex.PromotionPoints, value);
    }

    /// <summary>
    /// Operations won (index 5).
    /// </summary>
    public int OperationsWon
    {
        get => GetVar(StrategyVarIndex.OperationsWon);
        set => SetVar(StrategyVarIndex.OperationsWon, value);
    }

    /// <summary>
    /// Operations played (index 4).
    /// </summary>
    public int OperationsPlayed
    {
        get => GetVar(StrategyVarIndex.OperationsPlayed);
        set => SetVar(StrategyVarIndex.OperationsPlayed, value);
    }

    private int GetVar(int index)
    {
        if (StrategyVars != null && index >= 0 && index < StrategyVars.Length)
            return StrategyVars[index];
        return 0;
    }

    private void SetVar(int index, int value)
    {
        if (StrategyVars != null && index >= 0 && index < StrategyVars.Length)
            StrategyVars[index] = value;
    }

    // Nested data
    public List<PlanetData> Planets { get; set; } = new();
    public List<LeaderData> HiredLeaders { get; set; } = new();
    public List<LeaderData> DismissedLeaders { get; set; } = new();
    public List<LeaderData> DeadLeaders { get; set; } = new();
    public List<SquaddieData> Squaddies { get; set; } = new();
    public OperationData? CurrentOperation { get; set; }

    // Parsing state
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public long BodyEndOffset { get; set; }

    // File offsets for surgical edits
    public long IronmanOffset { get; set; }
    public long StrategyVarsOffset { get; set; }
    public long PlanetsOffset { get; set; }
}

/// <summary>
/// Planet control data.
/// </summary>
public class PlanetData
{
    public string TemplateName { get; set; } = "";
    public int Control { get; set; }
    public int ControlChange { get; set; }
}

/// <summary>
/// Leader data from Roster.
/// </summary>
public class LeaderData
{
    public int ActorType { get; set; } // 0=SquadLeader, 1=Pilot
    public string TemplateName { get; set; } = "";
    public string DisplayName => string.IsNullOrEmpty(TemplateName) ? "Unknown" : TemplateName;
}

/// <summary>
/// Squaddie data.
/// </summary>
public class SquaddieData
{
    public int ID { get; set; }
    public int Gender { get; set; }
    public int SkinColor { get; set; }
    public int HomePlanetType { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int PortraitIndex { get; set; }
    public string TemplateName { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Current operation data.
/// </summary>
public class OperationData
{
    public string TemplateName { get; set; } = "";
    public string StoryFaction { get; set; } = "";
    public string Faction { get; set; } = "";
    public string Planet { get; set; } = "";
    public int CurrentMission { get; set; }
    public int AvailableDrops { get; set; }
    public int Deployments { get; set; }
}
