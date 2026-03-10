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

    // ═══════════════════════════════════════════════════════════════════
    //  Multi-Operation Support
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the OperationsManager instance.
    /// </summary>
    public static GameObj GetOperationsManager()
    {
        try
        {
            EnsureTypesLoaded();

            var strategyStateType = _strategyStateType?.ManagedType;
            if (strategyStateType == null) return GameObj.Null;

            var getMethod = strategyStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var strategyState = getMethod?.Invoke(null, null);
            if (strategyState == null) return GameObj.Null;

            // OperationsManager at offset +0x58
            var strategyStateObj = new GameObj(((Il2CppObjectBase)strategyState).Pointer);
            var omPtr = strategyStateObj.ReadPtr(0x58);
            if (omPtr == IntPtr.Zero) return GameObj.Null;

            return new GameObj(omPtr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetOperationsManager", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all active operations (not just the current one).
    /// </summary>
    public static List<GameObj> GetAllOperations()
    {
        var result = new List<GameObj>();

        try
        {
            EnsureTypesLoaded();

            var om = GetOperationsManager();
            if (om.IsNull) return result;

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return result;

            var omProxy = GetManagedProxy(om, omType);
            if (omProxy == null) return result;

            // Try GetAllOperations method first
            var getAllMethod = omType.GetMethod("GetAllOperations",
                BindingFlags.Public | BindingFlags.Instance);

            if (getAllMethod != null)
            {
                var ops = getAllMethod.Invoke(omProxy, null);
                if (ops != null)
                {
                    var listType = ops.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");

                    int count = (int)(countProp?.GetValue(ops) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var op = indexer?.Invoke(ops, new object[] { i });
                        if (op != null)
                            result.Add(new GameObj(((Il2CppObjectBase)op).Pointer));
                    }
                    return result;
                }
            }

            // Fallback: Try m_Operations field at offset +0x18
            var opsPtr = om.ReadPtr(0x18);
            if (opsPtr != IntPtr.Zero)
            {
                var opsList = new GameList(opsPtr);
                for (int i = 0; i < opsList.Count; i++)
                {
                    var op = opsList[i];
                    if (!op.IsNull)
                        result.Add(op);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetAllOperations", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get all operation info (for all active operations).
    /// </summary>
    public static List<OperationInfo> GetAllOperationInfo()
    {
        var result = new List<OperationInfo>();
        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            var info = GetOperationInfo(op);
            if (info != null)
                result.Add(info);
        }
        return result;
    }

    /// <summary>
    /// Find an operation by faction name.
    /// </summary>
    /// <param name="factionName">Name of enemy or friendly faction.</param>
    public static GameObj FindByFaction(string factionName)
    {
        if (string.IsNullOrEmpty(factionName)) return GameObj.Null;

        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            var info = GetOperationInfo(op);
            if (info == null) continue;

            if (info.EnemyFaction?.Contains(factionName, StringComparison.OrdinalIgnoreCase) == true ||
                info.FriendlyFaction?.Contains(factionName, StringComparison.OrdinalIgnoreCase) == true)
            {
                return op;
            }
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Find an operation by planet name.
    /// </summary>
    public static GameObj FindByPlanet(string planetName)
    {
        if (string.IsNullOrEmpty(planetName)) return GameObj.Null;

        var operations = GetAllOperations();
        foreach (var op in operations)
        {
            var info = GetOperationInfo(op);
            if (info?.Planet?.Contains(planetName, StringComparison.OrdinalIgnoreCase) == true)
                return op;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Get completed operation types (operations that have been completed at least once).
    /// </summary>
    public static List<string> GetCompletedOperationTypes()
    {
        var result = new List<string>();

        try
        {
            EnsureTypesLoaded();

            var om = GetOperationsManager();
            if (om.IsNull) return result;

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return result;

            var omProxy = GetManagedProxy(om, omType);
            if (omProxy == null) return result;

            // Try m_CompletedOperationTypes HashSet
            var completedProp = omType.GetProperty("CompletedOperationTypes",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (completedProp == null)
            {
                // Try field directly
                var completedField = omType.GetField("m_CompletedOperationTypes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (completedField != null)
                {
                    var completed = completedField.GetValue(omProxy);
                    if (completed != null)
                    {
                        var setType = completed.GetType();
                        var enumerator = setType.GetMethod("GetEnumerator")?.Invoke(completed, null);
                        if (enumerator != null)
                        {
                            var enumType = enumerator.GetType();
                            var moveNext = enumType.GetMethod("MoveNext");
                            var currentProp = enumType.GetProperty("Current");

                            while ((bool)moveNext.Invoke(enumerator, null))
                            {
                                var current = currentProp.GetValue(enumerator);
                                if (current != null)
                                {
                                    var templateObj = new GameObj(((Il2CppObjectBase)current).Pointer);
                                    result.Add(templateObj.GetName() ?? "Unknown");
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Operation.GetCompletedOperationTypes", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Check if an operation type has been completed before.
    /// </summary>
    public static bool HasCompletedOperationType(string operationTemplateName)
    {
        var completed = GetCompletedOperationTypes();
        return completed.Exists(c => c.Equals(operationTemplateName, StringComparison.OrdinalIgnoreCase));
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

        // alloperations - List all active operations
        DevConsole.RegisterCommand("alloperations", "", "List all active operations", args =>
        {
            var operations = GetAllOperationInfo();
            if (operations.Count == 0)
                return "No active operations";

            var current = GetCurrentOperation();
            var currentPtr = current.IsNull ? IntPtr.Zero : current.Pointer;

            var lines = new List<string> { $"Active Operations ({operations.Count}):" };
            foreach (var op in operations)
            {
                var isCurrent = op.Pointer == currentPtr ? " <-- CURRENT" : "";
                var time = op.TimeLimit > 0 ? $" (Time: {op.TimeRemaining} left)" : "";
                lines.Add($"  {op.TemplateName}: {op.EnemyFaction} vs {op.FriendlyFaction}{time}{isCurrent}");
                lines.Add($"    Planet: {op.Planet}, Mission {op.CurrentMissionIndex + 1}/{op.MissionCount}");
            }
            return string.Join("\n", lines);
        });

        // completedops - List completed operation types
        DevConsole.RegisterCommand("completedops", "", "List completed operation types", args =>
        {
            var completed = GetCompletedOperationTypes();
            if (completed.Count == 0)
                return "No operations completed yet";

            var lines = new List<string> { $"Completed Operation Types ({completed.Count}):" };
            foreach (var c in completed)
            {
                lines.Add($"  {c}");
            }
            return string.Join("\n", lines);
        });

        // findop <faction|planet> - Find operation by faction or planet
        DevConsole.RegisterCommand("findop", "<name>", "Find operation by faction or planet name", args =>
        {
            if (args.Length == 0)
                return "Usage: findop <faction_or_planet_name>";

            var name = string.Join(" ", args);

            // Try faction first
            var op = FindByFaction(name);
            if (!op.IsNull)
            {
                var info = GetOperationInfo(op);
                return $"Found by faction:\n" +
                       $"  {info.TemplateName}\n" +
                       $"  Enemy: {info.EnemyFaction}\n" +
                       $"  Friendly: {info.FriendlyFaction}\n" +
                       $"  Planet: {info.Planet}";
            }

            // Try planet
            op = FindByPlanet(name);
            if (!op.IsNull)
            {
                var info = GetOperationInfo(op);
                return $"Found by planet:\n" +
                       $"  {info.TemplateName}\n" +
                       $"  Enemy: {info.EnemyFaction}\n" +
                       $"  Friendly: {info.FriendlyFaction}\n" +
                       $"  Planet: {info.Planet}";
            }

            return $"No operation found for '{name}'";
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
        => Il2CppUtils.GetManagedProxy(obj, managedType);
}
