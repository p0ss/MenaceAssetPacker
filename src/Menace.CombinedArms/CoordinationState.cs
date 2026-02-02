using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.CombinedArms;

public enum FormationBand { Frontline = 0, Midline = 1, Backline = 2 }

public class FormationDepthCache
{
    public bool IsValid;
    public List<(float x, float z)> EnemyCentroids = new();
    public float[] BandEdges = new float[4]; // [0]=0, [1]=front/mid, [2]=mid/back, [3]=max

    public void Invalidate()
    {
        IsValid = false;
        EnemyCentroids.Clear();
    }
}

public class AgentAction
{
    public IntPtr ActorPtr;
    public IntPtr TargetTilePtr;
    public int BehaviorGoal; // -1 = non-attack, 0=Damage, 1=Suppression, 2=Stun
    public int TileX, TileZ;
}

public class FactionTurnState
{
    public List<AgentAction> CompletedActions = new();
    public Dictionary<IntPtr, int> TargetedTileCount = new();
    public Dictionary<IntPtr, (int x, int z)> ActedAllyPositions = new();
    public Dictionary<IntPtr, (int x, int z)> PreviousTurnAllyPositions = new();
    public bool HasSuppressorActed;
    public FormationDepthCache DepthCache = new();

    public void Reset()
    {
        CompletedActions.Clear();
        TargetedTileCount.Clear();
        ActedAllyPositions.Clear();
        HasSuppressorActed = false;
        DepthCache.Invalidate();
    }

    public void ArchivePositions()
    {
        PreviousTurnAllyPositions.Clear();
        foreach (var kvp in ActedAllyPositions)
            PreviousTurnAllyPositions[kvp.Key] = kvp.Value;
    }
}

public class CombinedArmsConfig
{
    [JsonPropertyName("EnableAgentSequencing")]
    public bool EnableAgentSequencing { get; set; } = true;

    [JsonPropertyName("EnableFocusFire")]
    public bool EnableFocusFire { get; set; } = true;

    [JsonPropertyName("EnableCenterOfForces")]
    public bool EnableCenterOfForces { get; set; } = true;

    [JsonPropertyName("SuppressorPriorityBoost")]
    public float SuppressorPriorityBoost { get; set; } = 1.5f;

    [JsonPropertyName("DamageDealerPenalty")]
    public float DamageDealerPenalty { get; set; } = 0.7f;

    [JsonPropertyName("FocusFirePickingBoost")]
    public float FocusFirePickingBoost { get; set; } = 1.4f;

    [JsonPropertyName("CenterOfForcesWeight")]
    public float CenterOfForcesWeight { get; set; } = 3.0f;

    [JsonPropertyName("CenterOfForcesMaxRange")]
    public int CenterOfForcesMaxRange { get; set; } = 12;

    [JsonPropertyName("CenterOfForcesMinAllies")]
    public int CenterOfForcesMinAllies { get; set; } = 2;

    [JsonPropertyName("EnableFormationDepth")]
    public bool EnableFormationDepth { get; set; } = true;

    [JsonPropertyName("FormationDepthMaxRange")]
    public int FormationDepthMaxRange { get; set; } = 18;

    [JsonPropertyName("FrontlineFraction")]
    public float FrontlineFraction { get; set; } = 0.33f;

    [JsonPropertyName("MidlineFraction")]
    public float MidlineFraction { get; set; } = 0.34f;

    [JsonPropertyName("FormationDepthWeight")]
    public float FormationDepthWeight { get; set; } = 2.5f;

    [JsonPropertyName("FormationDepthMinOpponents")]
    public int FormationDepthMinOpponents { get; set; } = 1;

    [JsonPropertyName("VerboseLogging")]
    public bool VerboseLogging { get; set; } = false;
}

public static class CoordinationState
{
    public static Dictionary<int, FactionTurnState> FactionStates = new();
    public static bool Enabled = true;
    public static CombinedArmsConfig Config = new();

    private static string ConfigPath =>
        Path.Combine(
            Path.GetDirectoryName(typeof(CoordinationState).Assembly.Location) ?? "",
            "..", "..", "UserData", "CombinedArmsConfig.json");

    public static FactionTurnState GetOrCreate(int factionIndex)
    {
        if (!FactionStates.TryGetValue(factionIndex, out var state))
        {
            state = new FactionTurnState();
            FactionStates[factionIndex] = state;
        }
        return state;
    }

    public static void LoadConfig()
    {
        try
        {
            var path = Path.GetFullPath(ConfigPath);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CombinedArmsConfig>(json);
                if (loaded != null)
                    Config = loaded;
            }
            else
            {
                SaveConfig();
            }
        }
        catch
        {
            Config = new CombinedArmsConfig();
        }
    }

    public static void SaveConfig()
    {
        try
        {
            var path = Path.GetFullPath(ConfigPath);
            var dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Silently fail
        }
    }
}
