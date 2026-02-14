using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Faction types matching the game's FactionType enum.
/// </summary>
public enum FactionType
{
    Neutral = 0,
    Player = 1,
    PlayerAI = 2,
    Civilian = 3,
    AlliedLocalForces = 4,
    EnemyLocalForces = 5,
    Pirates = 6,
    Wildlife = 7,
    Constructs = 8,
    RogueArmy = 9
}

/// <summary>
/// Reason for finishing a tactical mission.
/// </summary>
public enum TacticalFinishReason
{
    None = 0,
    AllPlayerUnitsDead = 1,
    Leave = 2,
    LoadingSavegame = 3
}

/// <summary>
/// SDK extension for controlling tactical game state including rounds, turns,
/// time scale, and mission flow.
///
/// Based on reverse engineering findings:
/// - TacticalManager singleton manages game state
/// - TacticalManager.GetRound() for round number
/// - TacticalManager.GetActiveFactionID() for active faction
/// - TacticalManager.GetActiveActor() for active actor
/// - TacticalManager.IsPaused() @ 0x180672c90
/// - TacticalManager.SetPaused(bool) @ 0x1806753c0
/// - TacticalManager.NextRound() @ 0x1806736b0
/// - TacticalManager.NextFaction() @ 0x1806730f0
/// - TacticalState.TimeScale @ +0x28
/// </summary>
public static class TacticalController
{
    // Cached types
    private static GameType _tacticalManagerType;
    private static GameType _tacticalStateType;
    private static GameType _baseFactionType;
    private static GameType _tacticalFinishReasonType;

    // TacticalState offsets (still needed for some operations)
    private const uint OFFSET_TS_TIME_SCALE = 0x28;
    private const uint OFFSET_TS_CURRENT_ACTION = 0x38;

    /// <summary>
    /// Get the current round number (1-indexed).
    /// </summary>
    public static int GetCurrentRound()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return 0;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return 0;

            var getRoundMethod = tmType.GetMethod("GetRound", BindingFlags.Public | BindingFlags.Instance);
            if (getRoundMethod == null) return 0;

            return (int)getRoundMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetCurrentRound", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get the currently active faction ID.
    /// </summary>
    public static int GetCurrentFaction()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return -1;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return -1;

            var getActiveFactionMethod = tmType.GetMethod("GetActiveFactionID", BindingFlags.Public | BindingFlags.Instance);
            if (getActiveFactionMethod == null) return -1;

            return (int)getActiveFactionMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetCurrentFaction", "Failed", ex);
            return -1;
        }
    }

    /// <summary>
    /// Get the current faction type.
    /// </summary>
    public static FactionType GetCurrentFactionType()
    {
        var factionId = GetCurrentFaction();
        if (factionId < 0 || factionId > 9)
            return FactionType.Neutral;
        return (FactionType)factionId;
    }

    /// <summary>
    /// Check if it's the player's turn.
    /// </summary>
    public static bool IsPlayerTurn()
    {
        return GetCurrentFactionType() == FactionType.Player;
    }

    /// <summary>
    /// Check if the game is paused.
    /// </summary>
    public static bool IsPaused()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var isPausedMethod = tmType.GetMethod("IsPaused", BindingFlags.Public | BindingFlags.Instance);
            if (isPausedMethod == null) return false;

            return (bool)isPausedMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.IsPaused", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Pause or unpause the game.
    /// </summary>
    public static bool SetPaused(bool paused)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var setPausedMethod = tmType.GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Instance);
            if (setPausedMethod == null) return false;

            setPausedMethod.Invoke(tm, new object[] { paused });
            ModError.Info("Menace.SDK", $"Game {(paused ? "paused" : "unpaused")}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.SetPaused", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Toggle pause state.
    /// </summary>
    public static bool TogglePause()
    {
        return SetPaused(!IsPaused());
    }

    /// <summary>
    /// Get the current time scale (game speed).
    /// </summary>
    public static float GetTimeScale()
    {
        return Time.timeScale;
    }

    /// <summary>
    /// Set the time scale (game speed).
    /// </summary>
    /// <param name="scale">Time scale (1.0 = normal, 2.0 = 2x speed, 0.5 = half speed)</param>
    public static bool SetTimeScale(float scale)
    {
        try
        {
            var clamped = Math.Clamp(scale, 0f, 10f);
            Time.timeScale = clamped;
            ModError.Info("Menace.SDK", $"Time scale set to {clamped}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.SetTimeScale", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Advance to the next round.
    /// </summary>
    public static bool NextRound()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var nextRoundMethod = tmType.GetMethod("NextRound", BindingFlags.Public | BindingFlags.Instance);
            if (nextRoundMethod == null) return false;

            nextRoundMethod.Invoke(tm, null);
            ModError.Info("Menace.SDK", $"Advanced to round {GetCurrentRound()}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.NextRound", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Advance to the next faction's turn.
    /// </summary>
    public static bool NextFaction()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var nextFactionMethod = tmType.GetMethod("NextFaction", BindingFlags.Public | BindingFlags.Instance);
            if (nextFactionMethod == null) return false;

            nextFactionMethod.Invoke(tm, null);
            ModError.Info("Menace.SDK", $"Advanced to faction {GetCurrentFaction()}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.NextFaction", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// End the current turn (for player faction).
    /// </summary>
    public static bool EndTurn()
    {
        try
        {
            EnsureTypesLoaded();

            var tsType = _tacticalStateType?.ManagedType;
            if (tsType == null) return false;

            var ts = GetTacticalStateProxy();
            if (ts == null) return false;

            var endTurnMethod = tsType.GetMethod("EndTurn", BindingFlags.Public | BindingFlags.Instance);
            if (endTurnMethod == null) return false;

            endTurnMethod.Invoke(ts, null);
            ModError.Info("Menace.SDK", "Ended turn");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.EndTurn", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the currently active actor (selected unit).
    /// </summary>
    public static GameObj GetActiveActor()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return GameObj.Null;

            var getActiveActorMethod = tmType.GetMethod("GetActiveActor", BindingFlags.Public | BindingFlags.Instance);
            if (getActiveActorMethod == null) return GameObj.Null;

            var result = getActiveActorMethod.Invoke(tm, null);
            if (result == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetActiveActor", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Set the active actor.
    /// </summary>
    public static bool SetActiveActor(GameObj actor)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var setActiveMethod = tmType.GetMethod("SetActiveActor", BindingFlags.Public | BindingFlags.Instance);
            if (setActiveMethod == null) return false;

            object actorProxy = null;
            if (!actor.IsNull)
            {
                var actorType = GameType.Find("Menace.Tactical.Actor")?.ManagedType;
                if (actorType != null)
                {
                    var ptrCtor = actorType.GetConstructor(new[] { typeof(IntPtr) });
                    actorProxy = ptrCtor?.Invoke(new object[] { actor.Pointer });
                }
            }

            setActiveMethod.Invoke(tm, new object[] { actorProxy, true });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.SetActiveActor", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get total count of enemy actors.
    /// Uses TacticalManager.GetTotalEnemyCount().
    /// </summary>
    public static int GetTotalEnemyCount()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return 0;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return 0;

            var getCountMethod = tmType.GetMethod("GetTotalEnemyCount", BindingFlags.Public | BindingFlags.Instance);
            if (getCountMethod == null) return 0;

            return (int)getCountMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetTotalEnemyCount", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get count of dead enemy actors.
    /// Uses TacticalManager.GetDeadEnemyCount().
    /// </summary>
    public static int GetDeadEnemyCount()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return 0;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return 0;

            var getDeadMethod = tmType.GetMethod("GetDeadEnemyCount", BindingFlags.Public | BindingFlags.Instance);
            if (getDeadMethod == null) return 0;

            return (int)getDeadMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetDeadEnemyCount", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Check if the mission is still running.
    /// </summary>
    public static bool IsMissionRunning()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var isRunningMethod = tmType.GetMethod("IsMissionRunning", BindingFlags.Public | BindingFlags.Instance);
            if (isRunningMethod == null) return false;

            return (bool)isRunningMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.IsMissionRunning", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if any player unit is still alive.
    /// Uses TacticalManager.IsAnyPlayerUnitAlive().
    /// </summary>
    public static bool IsAnyPlayerUnitAlive()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var isAliveMethod = tmType.GetMethod("IsAnyPlayerUnitAlive", BindingFlags.Public | BindingFlags.Instance);
            if (isAliveMethod == null) return false;

            return (bool)isAliveMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.IsAnyPlayerUnitAlive", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if any AI/enemy unit is still alive.
    /// Uses TacticalManager.IsAnyAIUnitAlive().
    /// </summary>
    public static bool IsAnyEnemyAlive()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var isAliveMethod = tmType.GetMethod("IsAnyAIUnitAlive", BindingFlags.Public | BindingFlags.Instance);
            if (isAliveMethod == null) return false;

            return (bool)isAliveMethod.Invoke(tm, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.IsAnyEnemyAlive", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the name of a faction type.
    /// </summary>
    public static string GetFactionName(FactionType faction)
    {
        return faction switch
        {
            FactionType.Neutral => "Neutral",
            FactionType.Player => "Player",
            FactionType.PlayerAI => "Player AI",
            FactionType.Civilian => "Civilian",
            FactionType.AlliedLocalForces => "Allied Local Forces",
            FactionType.EnemyLocalForces => "Enemy Local Forces",
            FactionType.Pirates => "Pirates",
            FactionType.Wildlife => "Wildlife",
            FactionType.Constructs => "Constructs",
            FactionType.RogueArmy => "Rogue Army",
            _ => $"Unknown ({(int)faction})"
        };
    }

    /// <summary>
    /// Get comprehensive tactical state info.
    /// </summary>
    public static TacticalStateInfo GetTacticalState()
    {
        var activeActor = GetActiveActor();
        string activeActorName = null;
        if (!activeActor.IsNull)
        {
            activeActorName = activeActor.GetName();
        }

        var currentFaction = GetCurrentFactionType();
        var totalEnemies = GetTotalEnemyCount();
        var deadEnemies = GetDeadEnemyCount();

        return new TacticalStateInfo
        {
            RoundNumber = GetCurrentRound(),
            CurrentFaction = (int)currentFaction,
            CurrentFactionType = currentFaction,
            CurrentFactionName = GetFactionName(currentFaction),
            IsPlayerTurn = IsPlayerTurn(),
            IsPaused = IsPaused(),
            TimeScale = GetTimeScale(),
            IsMissionRunning = IsMissionRunning(),
            ActiveActorName = activeActorName,
            IsAnyPlayerAlive = IsAnyPlayerUnitAlive(),
            IsAnyEnemyAlive = IsAnyEnemyAlive(),
            TotalEnemyCount = totalEnemies,
            DeadEnemyCount = deadEnemies,
            AliveEnemyCount = totalEnemies - deadEnemies
        };
    }

    public class TacticalStateInfo
    {
        public int RoundNumber { get; set; }
        public int CurrentFaction { get; set; }
        public FactionType CurrentFactionType { get; set; }
        public string CurrentFactionName { get; set; }
        public bool IsPlayerTurn { get; set; }
        public bool IsPaused { get; set; }
        public float TimeScale { get; set; }
        public bool IsMissionRunning { get; set; }
        public string ActiveActorName { get; set; }
        public bool IsAnyPlayerAlive { get; set; }
        public bool IsAnyEnemyAlive { get; set; }
        public int TotalEnemyCount { get; set; }
        public int DeadEnemyCount { get; set; }
        public int AliveEnemyCount { get; set; }
    }

    /// <summary>
    /// Clear all enemies from the battlefield.
    /// </summary>
    public static int ClearAllEnemies()
    {
        return EntitySpawner.ClearEnemies(immediate: true);
    }

    /// <summary>
    /// Spawn a wave of enemies at specified positions.
    /// </summary>
    /// <param name="templateName">EntityTemplate name for enemies</param>
    /// <param name="positions">Tile positions to spawn at</param>
    /// <param name="faction">Faction type for spawned units (default: EnemyLocalForces)</param>
    /// <returns>Number successfully spawned</returns>
    public static int SpawnWave(string templateName, List<(int x, int y)> positions, FactionType faction = FactionType.EnemyLocalForces)
    {
        var results = EntitySpawner.SpawnGroup(templateName, positions, (int)faction);
        return results.FindAll(r => r.Success).Count;
    }

    /// <summary>
    /// Skip the AI turn (immediately end enemy turn).
    /// </summary>
    public static bool SkipAITurn()
    {
        var faction = GetCurrentFactionType();
        // Skip if current faction is not player-controlled
        if (faction == FactionType.Player)
            return false;

        return NextFaction();
    }

    /// <summary>
    /// Finish the mission with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for finishing the mission</param>
    public static bool FinishMission(TacticalFinishReason reason = TacticalFinishReason.Leave)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            // Find the game's TacticalFinishReason enum type
            _tacticalFinishReasonType ??= GameType.Find("Menace.Tactical.TacticalFinishReason");
            var gameReasonType = _tacticalFinishReasonType?.ManagedType;

            var finishMethod = tmType.GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance);
            if (finishMethod == null) return false;

            // Convert our enum to the game's enum
            object gameReason;
            if (gameReasonType != null)
            {
                gameReason = Enum.ToObject(gameReasonType, (int)reason);
            }
            else
            {
                // Fallback: try passing the int value directly
                gameReason = (int)reason;
            }

            finishMethod.Invoke(tm, new object[] { gameReason });
            ModError.Info("Menace.SDK", $"Mission finished with reason: {reason}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.FinishMission", "Failed", ex);
            return false;
        }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
        _tacticalStateType ??= GameType.Find("Menace.States.TacticalState");
        _baseFactionType ??= GameType.Find("Menace.Tactical.AI.BaseFaction");
    }

    private static object GetTacticalManagerProxy()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return null;

            var instanceProp = tmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            return instanceProp?.GetValue(null);
        }
        catch
        {
            return null;
        }
    }

    private static object GetTacticalStateProxy()
    {
        try
        {
            EnsureTypesLoaded();

            var tsType = _tacticalStateType?.ManagedType;
            if (tsType == null) return null;

            // Try Instance property first
            var instanceProp = tsType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
                return instanceProp.GetValue(null);

            // Try Get() static method
            var getMethod = tsType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            return getMethod?.Invoke(null, null);
        }
        catch
        {
            return null;
        }
    }
}
