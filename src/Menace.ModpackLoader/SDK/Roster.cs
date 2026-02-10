using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for roster and unit management.
/// Provides safe access to hired units, squaddies, perks, and unit status.
///
/// Based on reverse engineering findings:
/// - Roster via StrategyState @ +0x70
/// - BaseUnitLeader.Perks @ +0x48
/// - BaseUnitLeader.Skills @ +0x38
/// - Squaddie structure with NameSeed, Gender, HomePlanet
/// </summary>
public static class Roster
{
    // Cached types
    private static GameType _rosterType;
    private static GameType _unitLeaderType;
    private static GameType _squaddieType;
    private static GameType _strategyStateType;

    // Leader status constants
    public const int STATUS_HIRED = 0;
    public const int STATUS_AVAILABLE = 1;
    public const int STATUS_DEAD = 2;
    public const int STATUS_DISMISSED = 3;
    public const int STATUS_AWAITING_BURIAL = 4;

    /// <summary>
    /// Unit leader information structure.
    /// </summary>
    public class UnitLeaderInfo
    {
        public string TemplateName { get; set; }
        public string Nickname { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; }
        public int Rank { get; set; }
        public string RankName { get; set; }
        public int PerkCount { get; set; }
        public float HealthPercent { get; set; }
        public bool IsDeployable { get; set; }
        public bool IsUnavailable { get; set; }
        public int SquaddieCount { get; set; }
        public int DeployCost { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Squaddie information structure.
    /// </summary>
    public class SquaddieInfo
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Gender { get; set; }
        public string HomePlanet { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Unit leader template information structure.
    /// </summary>
    public class UnitLeaderTemplateInfo
    {
        public string TemplateName { get; set; }
        public string DisplayName { get; set; }
        public int HiringCost { get; set; }
        public int Rarity { get; set; }
        public int MinCampaignProgress { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current roster instance.
    /// </summary>
    public static GameObj GetRoster()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return GameObj.Null;

            var instanceProp = ssType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var ss = instanceProp?.GetValue(null);
            if (ss == null) return GameObj.Null;

            var rosterProp = ssType.GetProperty("Roster", BindingFlags.Public | BindingFlags.Instance);
            var roster = rosterProp?.GetValue(ss);
            if (roster == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)roster).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetRoster", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all hired unit leaders.
    /// </summary>
    public static List<UnitLeaderInfo> GetHiredLeaders()
    {
        var result = new List<UnitLeaderInfo>();

        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return result;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return result;

            var proxy = GetManagedProxy(roster, rosterType);
            if (proxy == null) return result;

            var hiredProp = rosterType.GetProperty("HiredLeaders", BindingFlags.Public | BindingFlags.Instance);
            var hiredList = hiredProp?.GetValue(proxy);
            if (hiredList == null) return result;

            var listType = hiredList.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(hiredList);
            for (int i = 0; i < count; i++)
            {
                var leader = indexer.Invoke(hiredList, new object[] { i });
                if (leader == null) continue;

                var info = GetLeaderInfo(new GameObj(((Il2CppObjectBase)leader).Pointer));
                if (info != null)
                {
                    info.Status = STATUS_HIRED;
                    info.StatusName = "Hired";
                    result.Add(info);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetHiredLeaders", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader.
    /// </summary>
    public static UnitLeaderInfo GetLeaderInfo(GameObj leader)
    {
        if (leader.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return null;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return null;

            var info = new UnitLeaderInfo { Pointer = leader.Pointer };

            // Get template name
            var templateProp = leaderType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get nickname
            var getNicknameMethod = leaderType.GetMethod("GetNickname", BindingFlags.Public | BindingFlags.Instance);
            if (getNicknameMethod != null)
                info.Nickname = getNicknameMethod.Invoke(proxy, null)?.ToString();

            // Get rank
            var getRankMethod = leaderType.GetMethod("GetRank", BindingFlags.Public | BindingFlags.Instance);
            if (getRankMethod != null)
                info.Rank = (int)getRankMethod.Invoke(proxy, null);

            var getRankTemplateMethod = leaderType.GetMethod("GetRankTemplate", BindingFlags.Public | BindingFlags.Instance);
            var rankTemplate = getRankTemplateMethod?.Invoke(proxy, null);
            if (rankTemplate != null)
            {
                var rankObj = new GameObj(((Il2CppObjectBase)rankTemplate).Pointer);
                info.RankName = rankObj.GetName();
            }

            // Get perk count
            var getPerkCountMethod = leaderType.GetMethod("GetPerkCount", BindingFlags.Public | BindingFlags.Instance);
            if (getPerkCountMethod != null)
                info.PerkCount = (int)getPerkCountMethod.Invoke(proxy, null);

            // Get health
            var getHealthMethod = leaderType.GetMethod("GetHitpointsPct", BindingFlags.Public | BindingFlags.Instance);
            if (getHealthMethod != null)
                info.HealthPercent = (float)getHealthMethod.Invoke(proxy, null);

            // Get status flags
            var isDeployableMethod = leaderType.GetMethod("IsDeployable", BindingFlags.Public | BindingFlags.Instance);
            if (isDeployableMethod != null)
                info.IsDeployable = (bool)isDeployableMethod.Invoke(proxy, null);

            var isUnavailableMethod = leaderType.GetMethod("IsUnavailable", BindingFlags.Public | BindingFlags.Instance);
            if (isUnavailableMethod != null)
                info.IsUnavailable = (bool)isUnavailableMethod.Invoke(proxy, null);

            // Get deploy cost
            var getDeployCostsMethod = leaderType.GetMethod("GetDeployCosts", BindingFlags.Public | BindingFlags.Instance);
            if (getDeployCostsMethod != null)
                info.DeployCost = (int)getDeployCostsMethod.Invoke(proxy, null);

            // Get squaddie count (if SquadLeader)
            try
            {
                var squaddiesProp = proxy.GetType().GetProperty("Squaddies", BindingFlags.Public | BindingFlags.Instance);
                var squaddies = squaddiesProp?.GetValue(proxy);
                if (squaddies != null)
                {
                    var countProp = squaddies.GetType().GetProperty("Count");
                    info.SquaddieCount = (int)(countProp?.GetValue(squaddies) ?? 0);
                }
            }
            catch { }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetLeaderInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get total hired unit count.
    /// </summary>
    public static int GetHiredCount()
    {
        return GetHiredLeaders().Count;
    }

    /// <summary>
    /// Get available (deployable) unit count.
    /// </summary>
    public static int GetAvailableCount()
    {
        var leaders = GetHiredLeaders();
        int count = 0;
        foreach (var leader in leaders)
        {
            if (leader.IsDeployable)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Find a unit leader by nickname.
    /// </summary>
    public static GameObj FindByNickname(string nickname)
    {
        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return GameObj.Null;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return GameObj.Null;

            var proxy = GetManagedProxy(roster, rosterType);
            if (proxy == null) return GameObj.Null;

            var hiredProp = rosterType.GetProperty("HiredLeaders", BindingFlags.Public | BindingFlags.Instance);
            var hiredList = hiredProp?.GetValue(proxy);
            if (hiredList == null) return GameObj.Null;

            var listType = hiredList.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(hiredList);
            for (int i = 0; i < count; i++)
            {
                var leader = indexer.Invoke(hiredList, new object[] { i });
                if (leader == null) continue;

                var leaderObj = new GameObj(((Il2CppObjectBase)leader).Pointer);
                var info = GetLeaderInfo(leaderObj);
                if (info?.Nickname?.Contains(nickname, StringComparison.OrdinalIgnoreCase) == true)
                    return leaderObj;
            }

            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get perks for a unit leader.
    /// </summary>
    public static List<string> GetPerks(GameObj leader)
    {
        var result = new List<string>();
        if (leader.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return result;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return result;

            var perksProp = leaderType.GetProperty("Perks", BindingFlags.Public | BindingFlags.Instance);
            var perks = perksProp?.GetValue(proxy);
            if (perks == null) return result;

            var listType = perks.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(perks);
            for (int i = 0; i < count; i++)
            {
                var perk = indexer.Invoke(perks, new object[] { i });
                if (perk == null) continue;

                var perkObj = new GameObj(((Il2CppObjectBase)perk).Pointer);
                result.Add(perkObj.GetName() ?? $"Perk {i}");
            }

            return result;
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// Get status name from status code.
    /// </summary>
    public static string GetStatusName(int status)
    {
        return status switch
        {
            0 => "Hired",
            1 => "Available",
            2 => "Dead",
            3 => "Dismissed",
            4 => "Awaiting Burial",
            _ => $"Status {status}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Roster Manipulation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all hirable unit leader templates.
    /// </summary>
    public static List<UnitLeaderTemplateInfo> GetHirableLeaders()
    {
        var result = new List<UnitLeaderTemplateInfo>();

        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return result;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return result;

            var proxy = GetManagedProxy(roster, rosterType);
            if (proxy == null) return result;

            var method = rosterType.GetMethod("GetHirableLeaders", BindingFlags.Public | BindingFlags.Instance);
            var hirableList = method?.Invoke(proxy, null);
            if (hirableList == null) return result;

            var listType = hirableList.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(hirableList);
            for (int i = 0; i < count; i++)
            {
                var template = indexer.Invoke(hirableList, new object[] { i });
                if (template == null) continue;

                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                var info = GetTemplateInfo(templateObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetHirableLeaders", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a unit leader template.
    /// </summary>
    public static UnitLeaderTemplateInfo GetTemplateInfo(GameObj template)
    {
        if (template.IsNull) return null;

        try
        {
            var info = new UnitLeaderTemplateInfo
            {
                Pointer = template.Pointer,
                TemplateName = template.GetName()
            };

            // Get title (localized)
            var title = template.ReadObj("UnitTitle");
            if (!title.IsNull)
            {
                var titleType = title.GetGameType().ManagedType;
                var getText = titleType?.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance);
                if (getText != null)
                {
                    var proxy = GetManagedProxy(title, titleType);
                    info.DisplayName = getText.Invoke(proxy, null)?.ToString() ?? info.TemplateName;
                }
            }

            // Get hiring costs
            var costs = template.ReadObj("HiringCosts");
            if (!costs.IsNull)
            {
                // OperationResources has fields like Supplies, Fuel, etc.
                // For simplicity, try to get a total
                info.HiringCost = template.ReadInt("HiringCosts");
            }

            // Get rarity
            info.Rarity = template.ReadInt("Rarity");

            // Get min campaign progress
            info.MinCampaignProgress = template.ReadInt("MinCampaignProgress");

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.GetTemplateInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Add a unit leader template to the hirable pool.
    /// </summary>
    public static bool AddHirableLeader(GameObj template)
    {
        if (template.IsNull) return false;

        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return false;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return false;

            var proxy = GetManagedProxy(roster, rosterType);
            if (proxy == null) return false;

            // Find UnitLeaderTemplate type
            var templateType = GameType.Find("Menace.Strategy.UnitLeaderTemplate")?.ManagedType;
            if (templateType == null) return false;

            var method = rosterType.GetMethod("AddHirableLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null) return false;

            method.Invoke(proxy, new[] { templateProxy });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.AddHirableLeader", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Hire a unit leader from a template.
    /// </summary>
    public static GameObj HireLeader(GameObj template)
    {
        if (template.IsNull) return GameObj.Null;

        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return GameObj.Null;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            if (rosterType == null) return GameObj.Null;

            var proxy = GetManagedProxy(roster, rosterType);
            if (proxy == null) return GameObj.Null;

            var templateType = GameType.Find("Menace.Strategy.UnitLeaderTemplate")?.ManagedType;
            if (templateType == null) return GameObj.Null;

            var method = rosterType.GetMethod("HireLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return GameObj.Null;

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null) return GameObj.Null;

            var result = method.Invoke(proxy, new[] { templateProxy });
            if (result == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.HireLeader", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Dismiss a hired unit leader.
    /// </summary>
    public static bool DismissLeader(GameObj leader)
    {
        if (leader.IsNull) return false;

        try
        {
            var roster = GetRoster();
            if (roster.IsNull) return false;

            EnsureTypesLoaded();

            var rosterType = _rosterType?.ManagedType;
            var leaderType = _unitLeaderType?.ManagedType;
            if (rosterType == null || leaderType == null) return false;

            var rosterProxy = GetManagedProxy(roster, rosterType);
            var leaderProxy = GetManagedProxy(leader, leaderType);
            if (rosterProxy == null || leaderProxy == null) return false;

            var method = rosterType.GetMethod("TryDismissLeader", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(rosterProxy, new[] { leaderProxy });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Roster.DismissLeader", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Find a hirable leader template by name.
    /// </summary>
    public static GameObj FindHirableByName(string templateName)
    {
        try
        {
            var hirables = GetHirableLeaders();
            foreach (var h in hirables)
            {
                if (h.TemplateName?.Contains(templateName, StringComparison.OrdinalIgnoreCase) == true)
                    return new GameObj(h.Pointer);
            }
            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Find a hired leader by template name.
    /// </summary>
    public static GameObj FindByTemplateName(string templateName)
    {
        try
        {
            var leaders = GetHiredLeaders();
            foreach (var l in leaders)
            {
                if (l.TemplateName?.Contains(templateName, StringComparison.OrdinalIgnoreCase) == true)
                    return new GameObj(l.Pointer);
            }
            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the leader's template object.
    /// </summary>
    public static GameObj GetLeaderTemplate(GameObj leader)
    {
        if (leader.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return GameObj.Null;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return GameObj.Null;

            var templateProp = leaderType.GetProperty("LeaderTemplate", BindingFlags.Public | BindingFlags.Instance)
                            ?? leaderType.GetField("LeaderTemplate", BindingFlags.Public | BindingFlags.Instance)?.GetValue(proxy) as System.Reflection.PropertyInfo;

            // LeaderTemplate is a readonly field, not a property
            var templateField = leaderType.GetField("LeaderTemplate", BindingFlags.Public | BindingFlags.Instance);
            var template = templateField?.GetValue(proxy);
            if (template == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)template).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Register console commands for Roster SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // roster - List all hired units
        DevConsole.RegisterCommand("roster", "", "List all hired units", args =>
        {
            var leaders = GetHiredLeaders();
            if (leaders.Count == 0)
                return "No hired units";

            var lines = new List<string> { $"Hired Units ({leaders.Count}):" };
            foreach (var l in leaders)
            {
                var status = l.IsDeployable ? "Ready" : (l.IsUnavailable ? "Unavailable" : "Busy");
                var squaddies = l.SquaddieCount > 0 ? $" (+{l.SquaddieCount} squaddies)" : "";
                lines.Add($"  {l.Nickname} - {l.RankName} ({l.PerkCount} perks) [{status}]{squaddies}");
            }
            return string.Join("\n", lines);
        });

        // unit <nickname> - Show unit info
        DevConsole.RegisterCommand("unit", "<nickname>", "Show unit information", args =>
        {
            if (args.Length == 0)
                return "Usage: unit <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            var info = GetLeaderInfo(leader);
            if (info == null)
                return "Could not get unit info";

            var perks = GetPerks(leader);

            return $"Unit: {info.Nickname}\n" +
                   $"Template: {info.TemplateName}\n" +
                   $"Rank: {info.RankName} (Rank {info.Rank})\n" +
                   $"Health: {info.HealthPercent:P0}\n" +
                   $"Deploy Cost: {info.DeployCost}\n" +
                   $"Deployable: {info.IsDeployable}, Unavailable: {info.IsUnavailable}\n" +
                   $"Squaddies: {info.SquaddieCount}\n" +
                   $"Perks ({info.PerkCount}): {string.Join(", ", perks)}";
        });

        // available - Show available units count
        DevConsole.RegisterCommand("available", "", "Show available units count", args =>
        {
            var total = GetHiredCount();
            var available = GetAvailableCount();
            return $"Available: {available}/{total} units ready for deployment";
        });

        // hirable - List hirable leaders
        DevConsole.RegisterCommand("hirable", "", "List available leaders for hire", args =>
        {
            var hirables = GetHirableLeaders();
            if (hirables.Count == 0)
                return "No leaders available for hire";

            var lines = new List<string> { $"Available for Hire ({hirables.Count}):" };
            foreach (var h in hirables)
            {
                var name = !string.IsNullOrEmpty(h.DisplayName) ? h.DisplayName : h.TemplateName;
                var rarity = h.Rarity > 0 ? $" (Rarity: {h.Rarity}%)" : "";
                lines.Add($"  {name}{rarity}");
            }
            return string.Join("\n", lines);
        });

        // hire <template> - Hire a leader
        DevConsole.RegisterCommand("hire", "<template>", "Hire a leader by template name", args =>
        {
            if (args.Length == 0)
                return "Usage: hire <template>";

            var templateName = string.Join(" ", args);
            var template = FindHirableByName(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found in hire pool";

            var hired = HireLeader(template);
            if (hired.IsNull)
                return "Failed to hire leader";

            var info = GetLeaderInfo(hired);
            return $"Hired: {info?.Nickname ?? "Unknown"}";
        });

        // dismiss <nickname> - Dismiss a leader
        DevConsole.RegisterCommand("dismiss", "<nickname>", "Dismiss a hired leader", args =>
        {
            if (args.Length == 0)
                return "Usage: dismiss <nickname>";

            var nickname = string.Join(" ", args);
            var leader = FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            var info = GetLeaderInfo(leader);
            if (DismissLeader(leader))
                return $"Dismissed: {info?.Nickname ?? nickname}";
            else
                return "Failed to dismiss leader";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _rosterType ??= GameType.Find("Menace.Strategy.Roster");
        _unitLeaderType ??= GameType.Find("Menace.Strategy.BaseUnitLeader");
        _squaddieType ??= GameType.Find("Menace.Strategy.Squaddie");
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
