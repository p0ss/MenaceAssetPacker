using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// </summary>
    public static GameObj GetCurrentMission()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var tm = instanceProp?.GetValue(null);
            if (tm == null) return GameObj.Null;

            var missionProp = tmType.GetProperty("Mission", BindingFlags.Public | BindingFlags.Instance);
            var mission = missionProp?.GetValue(tm);
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

            // Get template
            var templateProp = missionType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get status
            var statusProp = missionType.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance);
            if (statusProp != null)
            {
                info.Status = Convert.ToInt32(statusProp.GetValue(proxy));
                info.StatusName = GetStatusName(info.Status);
            }

            // Get layer
            var layerProp = missionType.GetProperty("Layer", BindingFlags.Public | BindingFlags.Instance);
            if (layerProp != null)
            {
                info.Layer = Convert.ToInt32(layerProp.GetValue(proxy));
                info.LayerName = GetLayerName(info.Layer);
            }

            // Get map width and seed
            var widthProp = missionType.GetProperty("MapWidth", BindingFlags.Public | BindingFlags.Instance);
            var seedProp = missionType.GetProperty("Seed", BindingFlags.Public | BindingFlags.Instance);
            if (widthProp != null) info.MapWidth = (int)widthProp.GetValue(proxy);
            if (seedProp != null) info.Seed = (int)seedProp.GetValue(proxy);

            // Get biome
            var biomeProp = missionType.GetProperty("Biome", BindingFlags.Public | BindingFlags.Instance);
            var biome = biomeProp?.GetValue(proxy);
            if (biome != null)
            {
                var biomeObj = new GameObj(((Il2CppObjectBase)biome).Pointer);
                info.BiomeName = biomeObj.GetName();
            }

            // Get weather
            var weatherProp = missionType.GetProperty("Weather", BindingFlags.Public | BindingFlags.Instance);
            var weather = weatherProp?.GetValue(proxy);
            if (weather != null)
            {
                var weatherObj = new GameObj(((Il2CppObjectBase)weather).Pointer);
                info.WeatherName = weatherObj.GetName();
            }

            // Get light condition
            var lightProp = missionType.GetProperty("LightCondition", BindingFlags.Public | BindingFlags.Instance);
            if (lightProp != null)
            {
                info.LightCondition = lightProp.GetValue(proxy)?.ToString();
            }

            // Get difficulty
            var diffProp = missionType.GetProperty("Difficulty", BindingFlags.Public | BindingFlags.Instance);
            var diff = diffProp?.GetValue(proxy);
            if (diff != null)
            {
                var diffObj = new GameObj(((Il2CppObjectBase)diff).Pointer);
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

            // Get ObjectiveManager
            var objMgrProp = missionType.GetProperty("Objectives", BindingFlags.Public | BindingFlags.Instance);
            var objMgr = objMgrProp?.GetValue(proxy);
            if (objMgr == null) return result;

            // Get objectives list
            var objectivesProp = objMgr.GetType().GetProperty("Objectives", BindingFlags.Public | BindingFlags.Instance);
            var objectives = objectivesProp?.GetValue(objMgr);
            if (objectives == null) return result;

            // Iterate list
            var listType = objectives.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(objectives);
            for (int i = 0; i < count; i++)
            {
                var objective = indexer.Invoke(objectives, new object[] { i });
                if (objective == null) continue;

                var info = new ObjectiveInfo
                {
                    Pointer = ((Il2CppObjectBase)objective).Pointer
                };

                var objType = objective.GetType();

                // Get name/description
                var titleProp = objType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
                var descProp = objType.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                if (titleProp != null) info.Name = titleProp.GetValue(objective)?.ToString();
                if (descProp != null) info.Description = descProp.GetValue(objective)?.ToString();

                // Get status
                var isCompleteProp = objType.GetProperty("IsComplete", BindingFlags.Public | BindingFlags.Instance);
                var isFailedProp = objType.GetProperty("IsFailed", BindingFlags.Public | BindingFlags.Instance);
                var isOptionalProp = objType.GetProperty("IsOptional", BindingFlags.Public | BindingFlags.Instance);

                if (isCompleteProp != null) info.IsComplete = (bool)isCompleteProp.GetValue(objective);
                if (isFailedProp != null) info.IsFailed = (bool)isFailedProp.GetValue(objective);
                if (isOptionalProp != null) info.IsOptional = (bool)isOptionalProp.GetValue(objective);

                // Get progress
                var progressProp = objType.GetProperty("Progress", BindingFlags.Public | BindingFlags.Instance);
                var targetProp = objType.GetProperty("TargetProgress", BindingFlags.Public | BindingFlags.Instance);

                if (progressProp != null) info.Progress = Convert.ToInt32(progressProp.GetValue(objective));
                if (targetProp != null) info.TargetProgress = Convert.ToInt32(targetProp.GetValue(objective));

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

            var completeMethod = objType.GetMethod("Complete", BindingFlags.Public | BindingFlags.Instance);
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
