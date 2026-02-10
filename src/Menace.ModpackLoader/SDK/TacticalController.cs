using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK extension for controlling tactical game state including rounds, turns,
/// time scale, and mission flow.
///
/// Based on reverse engineering findings:
/// - TacticalManager singleton manages game state
/// - TacticalManager.RoundNumber @ +0x60
/// - TacticalManager.CurrentFactionIndex @ +0xB0
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

    // TacticalManager offsets from turn-action-system.md
    private const uint OFFSET_TM_ACTIVE_ACTOR = 0x50;
    private const uint OFFSET_TM_ALL_ACTORS = 0x58;
    private const uint OFFSET_TM_ROUND_NUMBER = 0x60;
    private const uint OFFSET_TM_ALL_STRUCTURES = 0x78;
    private const uint OFFSET_TM_FACTIONS = 0xA8;
    private const uint OFFSET_TM_CURRENT_FACTION = 0xB0;
    private const uint OFFSET_TM_PREVIOUS_FACTION = 0xB4;
    private const uint OFFSET_TM_ROUND_CHECK_PENDING = 0xD8;

    // TacticalState offsets
    private const uint OFFSET_TS_TIME_SCALE = 0x28;
    private const uint OFFSET_TS_CURRENT_ACTION = 0x38;

    // Faction constants
    public const int FACTION_PLAYER = 0;
    public const int FACTION_ENEMY = 1;
    public const int FACTION_NEUTRAL = 2;

    /// <summary>
    /// Get the current round number (1-indexed).
    /// </summary>
    public static int GetCurrentRound()
    {
        var tm = GetTacticalManager();
        if (tm.IsNull)
            return 0;

        return tm.ReadInt(OFFSET_TM_ROUND_NUMBER);
    }

    /// <summary>
    /// Get the currently active faction index.
    /// </summary>
    public static int GetCurrentFaction()
    {
        var tm = GetTacticalManager();
        if (tm.IsNull)
            return -1;

        return tm.ReadInt(OFFSET_TM_CURRENT_FACTION);
    }

    /// <summary>
    /// Check if it's the player's turn.
    /// </summary>
    public static bool IsPlayerTurn()
    {
        return GetCurrentFaction() == FACTION_PLAYER;
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
        var tm = GetTacticalManager();
        if (tm.IsNull)
            return GameObj.Null;

        var ptr = tm.ReadPtr(OFFSET_TM_ACTIVE_ACTOR);
        return new GameObj(ptr);
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
    /// Get count of actors for a faction.
    /// </summary>
    public static int GetActorCount(int factionIndex)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return 0;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return 0;

            var getCountMethod = tmType.GetMethod("GetActorCount", BindingFlags.Public | BindingFlags.Instance);
            if (getCountMethod == null) return 0;

            return (int)getCountMethod.Invoke(tm, new object[] { factionIndex });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetActorCount", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get count of dead actors for a faction.
    /// </summary>
    public static int GetDeadCount(int factionIndex)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return 0;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return 0;

            var getDeadMethod = tmType.GetMethod("GetDeadCount", BindingFlags.Public | BindingFlags.Instance);
            if (getDeadMethod == null) return 0;

            return (int)getDeadMethod.Invoke(tm, new object[] { factionIndex });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TacticalController.GetDeadCount", "Failed", ex);
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
    /// </summary>
    public static bool IsAnyPlayerUnitAlive()
    {
        return GetActorCount(FACTION_PLAYER) > GetDeadCount(FACTION_PLAYER);
    }

    /// <summary>
    /// Check if any enemy unit is still alive.
    /// </summary>
    public static bool IsAnyEnemyAlive()
    {
        return GetActorCount(FACTION_ENEMY) > GetDeadCount(FACTION_ENEMY);
    }

    /// <summary>
    /// Get comprehensive tactical state info.
    /// </summary>
    public static TacticalStateInfo GetTacticalState()
    {
        var tm = GetTacticalManager();

        var activeActor = GetActiveActor();
        string activeActorName = null;
        if (!activeActor.IsNull)
        {
            activeActorName = activeActor.GetName();
        }

        return new TacticalStateInfo
        {
            RoundNumber = GetCurrentRound(),
            CurrentFaction = GetCurrentFaction(),
            CurrentFactionName = GetCurrentFaction() switch
            {
                0 => "Player",
                1 => "Enemy",
                2 => "Neutral",
                _ => $"Faction {GetCurrentFaction()}"
            },
            IsPlayerTurn = IsPlayerTurn(),
            IsPaused = IsPaused(),
            TimeScale = GetTimeScale(),
            IsMissionRunning = IsMissionRunning(),
            ActiveActorName = activeActorName,
            PlayerAliveCount = GetActorCount(FACTION_PLAYER) - GetDeadCount(FACTION_PLAYER),
            PlayerDeadCount = GetDeadCount(FACTION_PLAYER),
            EnemyAliveCount = GetActorCount(FACTION_ENEMY) - GetDeadCount(FACTION_ENEMY),
            EnemyDeadCount = GetDeadCount(FACTION_ENEMY)
        };
    }

    public class TacticalStateInfo
    {
        public int RoundNumber { get; set; }
        public int CurrentFaction { get; set; }
        public string CurrentFactionName { get; set; }
        public bool IsPlayerTurn { get; set; }
        public bool IsPaused { get; set; }
        public float TimeScale { get; set; }
        public bool IsMissionRunning { get; set; }
        public string ActiveActorName { get; set; }
        public int PlayerAliveCount { get; set; }
        public int PlayerDeadCount { get; set; }
        public int EnemyAliveCount { get; set; }
        public int EnemyDeadCount { get; set; }
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
    /// <returns>Number successfully spawned</returns>
    public static int SpawnWave(string templateName, List<(int x, int y)> positions)
    {
        var results = EntitySpawner.SpawnGroup(templateName, positions, FACTION_ENEMY);
        return results.FindAll(r => r.Success).Count;
    }

    /// <summary>
    /// Skip the AI turn (immediately end enemy turn).
    /// </summary>
    public static bool SkipAITurn()
    {
        if (GetCurrentFaction() != FACTION_ENEMY)
            return false;

        return NextFaction();
    }

    /// <summary>
    /// Finish the mission (trigger victory/defeat screen).
    /// </summary>
    public static bool FinishMission()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return false;

            var tm = GetTacticalManagerProxy();
            if (tm == null) return false;

            var finishMethod = tmType.GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance);
            if (finishMethod == null) return false;

            finishMethod.Invoke(tm, null);
            ModError.Info("Menace.SDK", "Mission finished");
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

    private static GameObj GetTacticalManager()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return GameObj.Null;

            var instance = instanceProp.GetValue(null);
            if (instance == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)instance).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    private static object GetTacticalManagerProxy()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return null;

            var instanceProp = tmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
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
            var instanceProp = tsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
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
