using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for campaign operation management.
/// Provides safe access to operations, missions, factions, and strategic assets.
///
/// Based on reverse engineering findings:
/// - Operation.Template @ +0x10
/// - Operation.EnemyFaction @ +0x18
/// - Operation.FriendlyFaction @ +0x20
/// - Operation.CurrentMissionIndex @ +0x40
/// - Operation.Missions @ +0x50
/// - Operation.TimeSpent/TimeLimit @ +0x58, +0x5C
/// </summary>
public static class Operation
{
    // Cached types
    private static GameType _operationType;
    private static GameType _operationsManagerType;
    private static GameType _missionType;
    private static GameType _strategyStateType;

    /// <summary>
    /// Operation information structure.
    /// </summary>
    public class OperationInfo
    {
        public string TemplateName { get; set; }
        public string EnemyFaction { get; set; }
        public string FriendlyFaction { get; set; }
        public string Planet { get; set; }
        public int CurrentMissionIndex { get; set; }
        public int MissionCount { get; set; }
        public int TimeSpent { get; set; }
        public int TimeLimit { get; set; }
        public int TimeRemaining { get; set; }
        public bool HasCompletedOnce { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current active operation.
    /// </summary>
    public static GameObj GetCurrentOperation()
    {
        try
        {
            EnsureTypesLoaded();

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return GameObj.Null;

            var instanceProp = omType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var om = instanceProp?.GetValue(null);
            if (om == null) return GameObj.Null;

            var getCurrentMethod = omType.GetMethod("GetCurrentOperation",
                BindingFlags.Public | BindingFlags.Instance);
            var operation = getCurrentMethod?.Invoke(om, null);
            if (operation == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)operation).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetCurrentOperation", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get information about the current operation.
    /// </summary>
    public static OperationInfo GetOperationInfo()
    {
        var op = GetCurrentOperation();
        return GetOperationInfo(op);
    }

    /// <summary>
    /// Get information about an operation.
    /// </summary>
    public static OperationInfo GetOperationInfo(GameObj operation)
    {
        if (operation.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var opType = _operationType?.ManagedType;
            if (opType == null) return null;

            var proxy = GetManagedProxy(operation, opType);
            if (proxy == null) return null;

            var info = new OperationInfo { Pointer = operation.Pointer };

            // Get template
            var templateProp = opType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get enemy faction
            var enemyProp = opType.GetProperty("EnemyFaction", BindingFlags.Public | BindingFlags.Instance);
            var enemy = enemyProp?.GetValue(proxy);
            if (enemy != null)
            {
                var enemyObj = new GameObj(((Il2CppObjectBase)enemy).Pointer);
                info.EnemyFaction = enemyObj.GetName();
            }

            // Get friendly faction
            var friendlyProp = opType.GetProperty("FriendlyFaction", BindingFlags.Public | BindingFlags.Instance);
            var friendly = friendlyProp?.GetValue(proxy);
            if (friendly != null)
            {
                var friendlyObj = new GameObj(((Il2CppObjectBase)friendly).Pointer);
                info.FriendlyFaction = friendlyObj.GetName();
            }

            // Get planet
            var getPlanetMethod = opType.GetMethod("GetPlanet", BindingFlags.Public | BindingFlags.Instance);
            var planet = getPlanetMethod?.Invoke(proxy, null);
            if (planet != null)
            {
                var planetObj = new GameObj(((Il2CppObjectBase)planet).Pointer);
                info.Planet = planetObj.GetName();
            }

            // Get mission info
            var curMissionProp = opType.GetProperty("CurrentMissionIndex", BindingFlags.Public | BindingFlags.Instance);
            if (curMissionProp != null)
                info.CurrentMissionIndex = (int)curMissionProp.GetValue(proxy);

            var missionsProp = opType.GetProperty("Missions", BindingFlags.Public | BindingFlags.Instance);
            var missions = missionsProp?.GetValue(proxy);
            if (missions != null)
            {
                var countProp = missions.GetType().GetProperty("Count");
                info.MissionCount = (int)(countProp?.GetValue(missions) ?? 0);
            }

            // Get time info
            var timeSpentProp = opType.GetProperty("TimeSpent", BindingFlags.Public | BindingFlags.Instance);
            var timeLimitProp = opType.GetProperty("TimeLimit", BindingFlags.Public | BindingFlags.Instance);
            if (timeSpentProp != null) info.TimeSpent = (int)timeSpentProp.GetValue(proxy);
            if (timeLimitProp != null) info.TimeLimit = (int)timeLimitProp.GetValue(proxy);

            var getRemainingMethod = opType.GetMethod("GetRemainingTime", BindingFlags.Public | BindingFlags.Instance);
            if (getRemainingMethod != null)
                info.TimeRemaining = (int)getRemainingMethod.Invoke(proxy, null);

            // Get completion flag
            var completedProp = opType.GetProperty("HasCompletedOnce", BindingFlags.Public | BindingFlags.Instance);
            if (completedProp != null)
                info.HasCompletedOnce = (bool)completedProp.GetValue(proxy);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetOperationInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the current mission from the operation.
    /// </summary>
    public static GameObj GetCurrentMission()
    {
        try
        {
            var op = GetCurrentOperation();
            if (op.IsNull) return GameObj.Null;

            EnsureTypesLoaded();

            var opType = _operationType?.ManagedType;
            if (opType == null) return GameObj.Null;

            var proxy = GetManagedProxy(op, opType);
            if (proxy == null) return GameObj.Null;

            var getCurrentMethod = opType.GetMethod("GetCurrentMission",
                BindingFlags.Public | BindingFlags.Instance);
            var mission = getCurrentMethod?.Invoke(proxy, null);
            if (mission == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)mission).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all missions in the current operation.
    /// </summary>
    public static List<GameObj> GetMissions()
    {
        var result = new List<GameObj>();

        try
        {
            var op = GetCurrentOperation();
            if (op.IsNull) return result;

            EnsureTypesLoaded();

            var opType = _operationType?.ManagedType;
            if (opType == null) return result;

            var proxy = GetManagedProxy(op, opType);
            if (proxy == null) return result;

            var missionsProp = opType.GetProperty("Missions", BindingFlags.Public | BindingFlags.Instance);
            var missions = missionsProp?.GetValue(proxy);
            if (missions == null) return result;

            var listType = missions.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(missions);
            for (int i = 0; i < count; i++)
            {
                var mission = indexer.Invoke(missions, new object[] { i });
                if (mission != null)
                    result.Add(new GameObj(((Il2CppObjectBase)mission).Pointer));
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetMissions", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Check if there is an active operation.
    /// </summary>
    public static bool HasActiveOperation()
    {
        return !GetCurrentOperation().IsNull;
    }

    /// <summary>
    /// Get remaining time in the operation.
    /// </summary>
    public static int GetRemainingTime()
    {
        var info = GetOperationInfo();
        return info?.TimeRemaining ?? 0;
    }

    /// <summary>
    /// Check if operation can time out.
    /// </summary>
    public static bool CanTimeOut()
    {
        var info = GetOperationInfo();
        return info != null && info.TimeLimit > 0;
    }

    /// <summary>
    /// Register console commands for Operation SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // operation - Show current operation info
        DevConsole.RegisterCommand("operation", "", "Show current operation info", args =>
        {
            var info = GetOperationInfo();
            if (info == null)
                return "No active operation";

            var timeInfo = info.TimeLimit > 0
                ? $"Time: {info.TimeSpent}/{info.TimeLimit} ({info.TimeRemaining} remaining)"
                : "Time: Unlimited";

            return $"Operation: {info.TemplateName}\n" +
                   $"Planet: {info.Planet ?? "Unknown"}\n" +
                   $"Enemy: {info.EnemyFaction ?? "Unknown"}\n" +
                   $"Allied: {info.FriendlyFaction ?? "Unknown"}\n" +
                   $"Missions: {info.CurrentMissionIndex + 1}/{info.MissionCount}\n" +
                   $"{timeInfo}\n" +
                   $"Completed Before: {info.HasCompletedOnce}";
        });

        // missions - List operation missions
        DevConsole.RegisterCommand("opmissions", "", "List missions in current operation", args =>
        {
            var missions = GetMissions();
            if (missions.Count == 0)
                return "No missions in operation";

            var info = GetOperationInfo();
            var currentIdx = info?.CurrentMissionIndex ?? -1;

            var lines = new List<string> { $"Operation Missions ({missions.Count}):" };
            for (int i = 0; i < missions.Count; i++)
            {
                var missionInfo = Mission.GetMissionInfo(missions[i]);
                var current = i == currentIdx ? " <-- CURRENT" : "";
                var status = missionInfo?.StatusName ?? "Unknown";
                lines.Add($"  {i}. {missionInfo?.TemplateName ?? "Unknown"} [{status}]{current}");
            }
            return string.Join("\n", lines);
        });

        // optime - Show operation time
        DevConsole.RegisterCommand("optime", "", "Show operation time remaining", args =>
        {
            var info = GetOperationInfo();
            if (info == null)
                return "No active operation";

            if (info.TimeLimit <= 0)
                return "Operation has no time limit";

            return $"Time: {info.TimeSpent}/{info.TimeLimit}\n" +
                   $"Remaining: {info.TimeRemaining}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _operationType ??= GameType.Find("Menace.Strategy.Operation");
        _operationsManagerType ??= GameType.Find("Menace.Strategy.OperationsManager");
        _missionType ??= GameType.Find("Menace.Strategy.Mission");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { obj.Pointer });
        }
        catch
        {
            return null;
        }
    }
}
