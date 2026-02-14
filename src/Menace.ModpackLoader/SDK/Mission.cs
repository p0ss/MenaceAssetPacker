using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for mission system operations.
/// Provides safe access to mission state, objectives, and mission flow control.
///
/// Based on reverse engineering findings:
/// - Mission class @ 0x180588900
/// - Mission.Template @ +0x10
/// - Mission.Status @ +0xB8
/// - Mission.Objectives @ +0x40
/// </summary>
public static class Mission
{
    // Cached types
    private static GameType _missionType;
    private static GameType _missionTemplateType;
    private static GameType _objectiveManagerType;
    private static GameType _tacticalManagerType;

    // Mission status constants
    public const int STATUS_PENDING = 0;
    public const int STATUS_ACTIVE = 1;
    public const int STATUS_COMPLETE = 2;
    public const int STATUS_FAILED = 3;

    // Mission layer constants
    public const int LAYER_SURFACE = 0;
    public const int LAYER_UNDERGROUND = 1;
    public const int LAYER_INTERIOR = 2;
    public const int LAYER_SPACE = 3;
    public const int LAYER_RANDOM = 4;

    /// <summary>
    /// Mission information structure.
    /// </summary>
    public class MissionInfo
    {
        public string TemplateName { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; }
        public int Layer { get; set; }
        public string LayerName { get; set; }
        public int MapWidth { get; set; }
        public int Seed { get; set; }
        public string BiomeName { get; set; }
        public string WeatherName { get; set; }
        public string LightCondition { get; set; }
        public string DifficultyName { get; set; }
        public int EnemyArmyPoints { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Objective information structure.
    /// </summary>
    public class ObjectiveInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public bool IsOptional { get; set; }
        public int Progress { get; set; }
        public int TargetProgress { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current active mission.
    /// Mission is accessed via StrategyState -> Operation chain, not TacticalManager.
    /// </summary>
    public static GameObj GetCurrentMission()
    {
        try
        {
            EnsureTypesLoaded();

            // Get TacticalManager via Get() static method
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var getMethod = tmType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var tm = getMethod?.Invoke(null, null);
            if (tm == null) return GameObj.Null;

            // Get StrategyState from TacticalManager
            var strategyStateType = GameType.Find("Menace.Strategy.StrategyState")?.ManagedType;
            if (strategyStateType == null) return GameObj.Null;

            var getStateMethod = strategyStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var state = getStateMethod?.Invoke(null, null);
            if (state == null) return GameObj.Null;

            // Get current Operation from StrategyState
            var operationProp = strategyStateType.GetProperty("CurrentOperation", BindingFlags.Public | BindingFlags.Instance);
            var operation = operationProp?.GetValue(state);
            if (operation == null) return GameObj.Null;

            // Get Mission from Operation
            var operationType = operation.GetType();
            var missionProp = operationType.GetProperty("Mission", BindingFlags.Public | BindingFlags.Instance);
            var mission = missionProp?.GetValue(operation);
            if (mission == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)mission).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.GetCurrentMission", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get information about the current mission.
    /// </summary>
    public static MissionInfo GetMissionInfo()
    {
        var mission = GetCurrentMission();
        return GetMissionInfo(mission);
    }

    /// <summary>
    /// Get information about a mission.
    /// </summary>
    public static MissionInfo GetMissionInfo(GameObj mission)
    {
        if (mission.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var missionType = _missionType?.ManagedType;
            if (missionType == null) return null;

            var proxy = GetManagedProxy(mission, missionType);
            if (proxy == null) return null;

            var info = new MissionInfo { Pointer = mission.Pointer };

            // Get template via direct field at +0x10
            var templatePtr = Marshal.ReadIntPtr(mission.Pointer + 0x10);
            if (templatePtr != IntPtr.Zero)
            {
                var templateObj = new GameObj(templatePtr);
                info.TemplateName = templateObj.GetName();
            }

            // Get status via direct field at +0xB8
            info.Status = Marshal.ReadInt32(mission.Pointer + 0xB8);
            info.StatusName = GetStatusName(info.Status);

            // Get layer via direct field at +0x20
            info.Layer = Marshal.ReadInt32(mission.Pointer + 0x20);
            info.LayerName = GetLayerName(info.Layer);

            // Get seed via direct field at +0x24
            info.Seed = Marshal.ReadInt32(mission.Pointer + 0x24);
            // MapWidth doesn't exist - leave as default 0

            // Get biome via direct field at +0x70
            var biomePtr = Marshal.ReadIntPtr(mission.Pointer + 0x70);
            if (biomePtr != IntPtr.Zero)
            {
                var biomeObj = new GameObj(biomePtr);
                info.BiomeName = biomeObj.GetName();
            }

            // Get weather template via direct field at +0x60
            var weatherPtr = Marshal.ReadIntPtr(mission.Pointer + 0x60);
            if (weatherPtr != IntPtr.Zero)
            {
                var weatherObj = new GameObj(weatherPtr);
                info.WeatherName = weatherObj.GetName();
            }

            // Get light condition via GetLightConditionTemplate() method
            var getLightMethod = missionType.GetMethod("GetLightConditionTemplate", BindingFlags.Public | BindingFlags.Instance);
            if (getLightMethod != null)
            {
                var lightCondition = getLightMethod.Invoke(proxy, null);
                if (lightCondition != null)
                {
                    var lightObj = new GameObj(((Il2CppObjectBase)lightCondition).Pointer);
                    info.LightCondition = lightObj.GetName();
                }
            }

            // Get difficulty via direct field at +0x38
            var diffPtr = Marshal.ReadIntPtr(mission.Pointer + 0x38);
            if (diffPtr != IntPtr.Zero)
            {
                var diffObj = new GameObj(diffPtr);
                info.DifficultyName = diffObj.GetName();
            }

            // Get enemy army points
            var getArmyPointsMethod = missionType.GetMethod("GetEnemyArmyPoints",
                BindingFlags.Public | BindingFlags.Instance);
            if (getArmyPointsMethod != null)
            {
                info.EnemyArmyPoints = (int)getArmyPointsMethod.Invoke(proxy, null);
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.GetMissionInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get all objectives for the current mission.
    /// </summary>
    public static List<ObjectiveInfo> GetObjectives()
    {
        var mission = GetCurrentMission();
        return GetObjectives(mission);
    }

    /// <summary>
    /// Get all objectives for a mission.
    /// </summary>
    public static List<ObjectiveInfo> GetObjectives(GameObj mission)
    {
        var result = new List<ObjectiveInfo>();
        if (mission.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var missionType = _missionType?.ManagedType;
            if (missionType == null) return result;

            var proxy = GetManagedProxy(mission, missionType);
            if (proxy == null) return result;

            // Get ObjectiveManager via direct field at +0x40
            var objMgrPtr = Marshal.ReadIntPtr(mission.Pointer + 0x40);
            if (objMgrPtr == IntPtr.Zero) return result;

            // Get objectives array via direct field at +0x18 in ObjectiveManager
            var objectivesArrayPtr = Marshal.ReadIntPtr(objMgrPtr + 0x18);
            if (objectivesArrayPtr == IntPtr.Zero) return result;

            // Create managed proxy for the objectives array
            var objMgrType = _objectiveManagerType?.ManagedType;
            if (objMgrType == null) return result;

            var objMgrProxy = GetManagedProxy(new GameObj(objMgrPtr), objMgrType);
            if (objMgrProxy == null) return result;

            // Read the array - Il2Cpp arrays have length at +0x18 and elements starting at +0x20
            var arrayLength = Marshal.ReadInt32(objectivesArrayPtr + 0x18);
            if (arrayLength <= 0 || arrayLength > 1000) return result; // Sanity check

            var objectives = new List<IntPtr>();
            for (int i = 0; i < arrayLength; i++)
            {
                var elementPtr = Marshal.ReadIntPtr(objectivesArrayPtr + 0x20 + (i * IntPtr.Size));
                if (elementPtr != IntPtr.Zero)
                    objectives.Add(elementPtr);
            }

            if (objectives.Count == 0) return result;

            // Iterate objectives array
            var objectiveType = GameType.Find("Menace.Tactical.Objectives.Objective")?.ManagedType;

            foreach (var objPtr in objectives)
            {
                var info = new ObjectiveInfo
                {
                    Pointer = objPtr
                };

                // Create managed proxy for this objective
                if (objectiveType != null)
                {
                    var objProxy = GetManagedProxy(new GameObj(objPtr), objectiveType);
                    if (objProxy != null)
                    {
                        var objType = objProxy.GetType();

                        // Get name/description via GetTitle() method (no Description method exists)
                        var getTitleMethod = objType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                        var getDescMethod = objType.GetMethod("GetTranslatedObjectiveText", BindingFlags.Public | BindingFlags.Instance);
                        if (getTitleMethod != null) info.Name = getTitleMethod.Invoke(objProxy, null)?.ToString();
                        if (getDescMethod != null) info.Description = getDescMethod.Invoke(objProxy, null)?.ToString();

                        // Get completed status via IsCompleted() method
                        var isCompletedMethod = objType.GetMethod("IsCompleted", BindingFlags.Public | BindingFlags.Instance);
                        if (isCompletedMethod != null) info.IsComplete = (bool)isCompletedMethod.Invoke(objProxy, null);

                        // Check failed status via direct field - state == 3 at offset +0x1C
                        var stateValue = Marshal.ReadInt32(objPtr + 0x1C);
                        info.IsFailed = (stateValue == 3);
                        // IsOptional doesn't exist - leave as default (false)

                        // Get progress via GetProgress() and GetRequiredProgress() methods
                        var getProgressMethod = objType.GetMethod("GetProgress", BindingFlags.Public | BindingFlags.Instance);
                        var getRequiredMethod = objType.GetMethod("GetRequiredProgress", BindingFlags.Public | BindingFlags.Instance);

                        if (getProgressMethod != null) info.Progress = Convert.ToInt32(getProgressMethod.Invoke(objProxy, null) ?? 0);
                        if (getRequiredMethod != null) info.TargetProgress = Convert.ToInt32(getRequiredMethod.Invoke(objProxy, null) ?? 0);
                    }
                }

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.GetObjectives", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get current mission status.
    /// </summary>
    public static int GetStatus()
    {
        var info = GetMissionInfo();
        return info?.Status ?? STATUS_PENDING;
    }

    /// <summary>
    /// Check if mission is active.
    /// </summary>
    public static bool IsActive()
    {
        return GetStatus() == STATUS_ACTIVE;
    }

    /// <summary>
    /// Check if mission is complete.
    /// </summary>
    public static bool IsComplete()
    {
        return GetStatus() == STATUS_COMPLETE;
    }

    /// <summary>
    /// Check if mission has failed.
    /// </summary>
    public static bool IsFailed()
    {
        return GetStatus() == STATUS_FAILED;
    }

    /// <summary>
    /// Complete an objective by index.
    /// </summary>
    public static bool CompleteObjective(int index)
    {
        var objectives = GetObjectives();
        if (index < 0 || index >= objectives.Count) return false;

        try
        {
            EnsureTypesLoaded();

            var objPtr = objectives[index].Pointer;
            var objType = GameType.Find("Menace.Tactical.Objectives.Objective")?.ManagedType;
            if (objType == null) return false;

            var proxy = GetManagedProxy(new GameObj(objPtr), objType);
            if (proxy == null) return false;

            var completeMethod = objType.GetMethod("ForceComplete", BindingFlags.Public | BindingFlags.Instance);
            completeMethod?.Invoke(proxy, null);

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Mission.CompleteObjective", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get status name from status code.
    /// </summary>
    public static string GetStatusName(int status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Active",
            2 => "Complete",
            3 => "Failed",
            _ => $"Status {status}"
        };
    }

    /// <summary>
    /// Get layer name from layer code.
    /// </summary>
    public static string GetLayerName(int layer)
    {
        return layer switch
        {
            0 => "Surface",
            1 => "Underground",
            2 => "Interior",
            3 => "Space",
            4 => "Random",
            _ => $"Layer {layer}"
        };
    }

    /// <summary>
    /// Register console commands for Mission SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // mission - Show current mission info
        DevConsole.RegisterCommand("mission", "", "Show current mission info", args =>
        {
            var info = GetMissionInfo();
            if (info == null)
                return "No active mission";

            return $"Mission: {info.TemplateName}\n" +
                   $"Status: {info.StatusName}, Layer: {info.LayerName}\n" +
                   $"Map: {info.MapWidth}x{info.MapWidth}, Seed: {info.Seed}\n" +
                   $"Biome: {info.BiomeName ?? "N/A"}, Weather: {info.WeatherName ?? "N/A"}\n" +
                   $"Light: {info.LightCondition ?? "N/A"}, Difficulty: {info.DifficultyName ?? "N/A"}\n" +
                   $"Enemy Army Points: {info.EnemyArmyPoints}";
        });

        // objectives - List mission objectives
        DevConsole.RegisterCommand("objectives", "", "List mission objectives", args =>
        {
            var objectives = GetObjectives();
            if (objectives.Count == 0)
                return "No objectives";

            var lines = new List<string> { $"Objectives ({objectives.Count}):" };
            for (int i = 0; i < objectives.Count; i++)
            {
                var obj = objectives[i];
                var status = obj.IsComplete ? "[DONE]" : obj.IsFailed ? "[FAIL]" : "[    ]";
                var optional = obj.IsOptional ? " (optional)" : "";
                var progress = obj.TargetProgress > 0 ? $" [{obj.Progress}/{obj.TargetProgress}]" : "";
                lines.Add($"  {i}. {status} {obj.Name}{optional}{progress}");
            }
            return string.Join("\n", lines);
        });

        // completeobjective <index> - Complete an objective
        DevConsole.RegisterCommand("completeobjective", "<index>", "Complete an objective", args =>
        {
            if (args.Length == 0)
                return "Usage: completeobjective <index>";
            if (!int.TryParse(args[0], out int index))
                return "Invalid index";

            return CompleteObjective(index)
                ? $"Completed objective {index}"
                : "Failed to complete objective";
        });

        // missionstatus - Show mission status
        DevConsole.RegisterCommand("missionstatus", "", "Show mission status", args =>
        {
            var status = GetStatus();
            var objectives = GetObjectives();
            int complete = objectives.FindAll(o => o.IsComplete).Count;
            int failed = objectives.FindAll(o => o.IsFailed).Count;

            return $"Mission Status: {GetStatusName(status)}\n" +
                   $"Objectives: {complete} complete, {failed} failed, {objectives.Count - complete - failed} remaining";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _missionType ??= GameType.Find("Menace.Strategy.Mission");
        _missionTemplateType ??= GameType.Find("Menace.Strategy.Missions.MissionTemplate");
        _objectiveManagerType ??= GameType.Find("Menace.Tactical.Objectives.ObjectiveManager");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
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
