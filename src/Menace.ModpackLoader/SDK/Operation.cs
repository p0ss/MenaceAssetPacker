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

            // Access OperationsManager via StrategyState.Get().Operations (offset +0x58)
            var strategyStateType = _strategyStateType?.ManagedType;
            if (strategyStateType == null) return GameObj.Null;

            // Use static Get() method instead of s_Singleton property
            var getMethod = strategyStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var strategyState = getMethod?.Invoke(null, null);
            if (strategyState == null) return GameObj.Null;

            // Use direct field access at offset +0x58 for Operations
            var strategyStateObj = new GameObj(((Il2CppObjectBase)strategyState).Pointer);
            var omPtr = strategyStateObj.ReadPtr(0x58);
            if (omPtr == IntPtr.Zero) return GameObj.Null;
            var om = new GameObj(omPtr).ToManaged();
            if (om == null) return GameObj.Null;

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return GameObj.Null;

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

            // Get template via direct field access at offset +0x10
            var templatePtr = operation.ReadPtr(0x10);
            if (templatePtr != IntPtr.Zero)
            {
                var templateObj = new GameObj(templatePtr);
                info.TemplateName = templateObj.GetName();
            }

            // Get enemy faction via GetEnemyStoryFaction() method
            var getEnemyMethod = opType.GetMethod("GetEnemyStoryFaction", BindingFlags.Public | BindingFlags.Instance);
            var enemy = getEnemyMethod?.Invoke(proxy, null);
            if (enemy != null)
            {
                var enemyObj = new GameObj(((Il2CppObjectBase)enemy).Pointer);
                info.EnemyFaction = enemyObj.GetName();
            }

            // Get friendly faction via GetFriendlyFaction() method
            var getFriendlyMethod = opType.GetMethod("GetFriendlyFaction", BindingFlags.Public | BindingFlags.Instance);
            var friendly = getFriendlyMethod?.Invoke(proxy, null);
            if (friendly != null)
            {
                var friendlyObj = new GameObj(((Il2CppObjectBase)friendly).Pointer);
                info.FriendlyFaction = friendlyObj.GetName();
            }

            // Get planet - GetPlanet(bool) requires a bool parameter
            // Planet name is on m_Template, not directly on Planet object
            var getPlanetMethod = opType.GetMethod("GetPlanet", BindingFlags.Public | BindingFlags.Instance);
            if (getPlanetMethod != null)
            {
                var planet = getPlanetMethod.Invoke(proxy, new object[] { false });
                if (planet != null)
                {
                    var planetObj = new GameObj(((Il2CppObjectBase)planet).Pointer);
                    // Planet has m_Template field which contains the name
                    var planetTemplatePtr = planetObj.ReadPtr("m_Template");
                    if (planetTemplatePtr != IntPtr.Zero)
                    {
                        var planetTemplateObj = new GameObj(planetTemplatePtr);
                        info.Planet = planetTemplateObj.GetName();
                    }
                }
            }

            // Get mission info - use direct field read at offset +0x40
            info.CurrentMissionIndex = operation.ReadInt(0x40);

            // Get missions via direct field access at offset +0x50
            var missionsPtr = operation.ReadPtr(0x50);
            if (missionsPtr != IntPtr.Zero)
            {
                var missionsList = new GameList(missionsPtr);
                info.MissionCount = missionsList.Count;
            }

            // Get time info - use direct field reads at +0x5c (m_PassedTime) and +0x58 (m_MaxTimeUntilTimeout)
            info.TimeSpent = operation.ReadInt(0x5c);
            info.TimeLimit = operation.ReadInt(0x58);

            var getRemainingMethod = opType.GetMethod("GetRemainingTime", BindingFlags.Public | BindingFlags.Instance);
            if (getRemainingMethod != null)
                info.TimeRemaining = Convert.ToInt32(getRemainingMethod.Invoke(proxy, null) ?? 0);

            // HasCompletedOnce doesn't exist on Operation - would need OperationsManager.m_CompletedOperationTypes
            // Leave as default (false) for now

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

            // Get missions via direct field access at offset +0x50
            var missionsPtr = op.ReadPtr(0x50);
            if (missionsPtr == IntPtr.Zero) return result;

            var missionsList = new GameList(missionsPtr);
            for (int i = 0; i < missionsList.Count; i++)
            {
                var mission = missionsList[i];
                if (!mission.IsNull)
                    result.Add(mission);
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
