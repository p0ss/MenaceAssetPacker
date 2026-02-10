using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for perk and skill management.
/// Provides safe access to perk trees, perk manipulation, and skill inspection.
///
/// Based on reverse engineering findings:
/// - BaseUnitLeader.m_Perks @ +0x28 (List&lt;PerkTemplate&gt;)
/// - UnitLeaderTemplate.PerkTrees @ array of PerkTreeTemplate
/// - PerkTreeTemplate.Perks @ array of Perk (Tier 1-4)
/// - PerkTemplate extends SkillTemplate
/// </summary>
public static class Perks
{
    // Cached types
    private static GameType _perkTemplateType;
    private static GameType _perkTreeTemplateType;
    private static GameType _perkType;
    private static GameType _skillTemplateType;
    private static GameType _unitLeaderType;

    /// <summary>
    /// Perk information structure.
    /// </summary>
    public class PerkInfo
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Tier { get; set; }
        public int ActionPointCost { get; set; }
        public bool IsActive { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Perk tree information structure.
    /// </summary>
    public class PerkTreeInfo
    {
        public string Name { get; set; }
        public int PerkCount { get; set; }
        public List<PerkInfo> Perks { get; set; } = new();
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Queries
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all perks for a unit leader with detailed info.
    /// </summary>
    public static List<PerkInfo> GetLeaderPerks(GameObj leader)
    {
        var result = new List<PerkInfo>();
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
                var info = GetPerkInfo(perkObj);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetLeaderPerks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get detailed information about a perk template.
    /// </summary>
    public static PerkInfo GetPerkInfo(GameObj perkTemplate)
    {
        if (perkTemplate.IsNull) return null;

        try
        {
            var info = new PerkInfo
            {
                Pointer = perkTemplate.Pointer,
                Name = perkTemplate.GetName()
            };

            // PerkTemplate inherits from SkillTemplate
            // Get Title (LocalizedLine)
            var title = perkTemplate.ReadObj("Title");
            if (!title.IsNull)
            {
                var type = title.GetGameType().ManagedType;
                if (type != null)
                {
                    var proxy = GetManagedProxy(title, type);
                    info.Title = proxy?.ToString() ?? info.Name;
                }
            }

            // Get Description
            var desc = perkTemplate.ReadObj("Description");
            if (!desc.IsNull)
            {
                var type = desc.GetGameType().ManagedType;
                if (type != null)
                {
                    var proxy = GetManagedProxy(desc, type);
                    info.Description = proxy?.ToString();
                }
            }

            // Get ActionPointCost
            info.ActionPointCost = perkTemplate.ReadInt("ActionPointCost");

            // Get IsActive
            info.IsActive = perkTemplate.ReadBool("IsActive");

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get perk trees available to a unit leader from their template.
    /// </summary>
    public static List<PerkTreeInfo> GetPerkTrees(GameObj leader)
    {
        var result = new List<PerkTreeInfo>();
        if (leader.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            // Get leader's template
            var template = Roster.GetLeaderTemplate(leader);
            if (template.IsNull) return result;

            // Get PerkTrees array from template
            var perkTrees = template.ReadObj("PerkTrees");
            if (perkTrees.IsNull) return result;

            // It's an array, iterate
            var arrayType = perkTrees.GetGameType().ManagedType;
            if (arrayType == null) return result;

            var proxy = GetManagedProxy(perkTrees, arrayType);
            if (proxy is not Array array) return result;

            foreach (var item in array)
            {
                if (item == null) continue;
                var treeObj = new GameObj(((Il2CppObjectBase)item).Pointer);
                var treeInfo = GetPerkTreeInfo(treeObj);
                if (treeInfo != null)
                    result.Add(treeInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkTrees", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a perk tree.
    /// </summary>
    public static PerkTreeInfo GetPerkTreeInfo(GameObj perkTree)
    {
        if (perkTree.IsNull) return null;

        try
        {
            var info = new PerkTreeInfo
            {
                Pointer = perkTree.Pointer,
                Name = perkTree.GetName()
            };

            // PerkTreeTemplate has Perks array (of Perk, not PerkTemplate)
            var perksArray = perkTree.ReadObj("Perks");
            if (perksArray.IsNull) return info;

            var arrayType = perksArray.GetGameType().ManagedType;
            if (arrayType == null) return info;

            var proxy = GetManagedProxy(perksArray, arrayType);
            if (proxy is Array array)
            {
                info.PerkCount = array.Length;

                foreach (var item in array)
                {
                    if (item == null) continue;

                    // Perk has: Skill (PerkTemplate), Tier (int)
                    var perkObj = new GameObj(((Il2CppObjectBase)item).Pointer);

                    var skillObj = perkObj.ReadObj("Skill");
                    if (!skillObj.IsNull)
                    {
                        var perkInfo = GetPerkInfo(skillObj);
                        if (perkInfo != null)
                        {
                            perkInfo.Tier = perkObj.ReadInt("Tier");
                            info.Perks.Add(perkInfo);
                        }
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetPerkTreeInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Manipulation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a leader can be promoted (has room for more perks).
    /// </summary>
    public static bool CanBePromoted(GameObj leader)
    {
        if (leader.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return false;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return false;

            var method = leaderType.GetMethod("CanBePromoted", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(proxy, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.CanBePromoted", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader can be demoted (has perks to remove).
    /// </summary>
    public static bool CanBeDemoted(GameObj leader)
    {
        if (leader.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return false;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return false;

            var method = leaderType.GetMethod("CanBeDemoted", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(proxy, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.CanBeDemoted", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Add a perk to a unit leader.
    /// </summary>
    /// <param name="leader">The leader to add the perk to</param>
    /// <param name="perkTemplate">The perk template to add</param>
    /// <param name="spendPromotionPoints">Whether to spend promotion points (default true)</param>
    public static bool AddPerk(GameObj leader, GameObj perkTemplate, bool spendPromotionPoints = true)
    {
        if (leader.IsNull || perkTemplate.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            var perkType = _perkTemplateType?.ManagedType;
            if (leaderType == null || perkType == null) return false;

            var leaderProxy = GetManagedProxy(leader, leaderType);
            var perkProxy = GetManagedProxy(perkTemplate, perkType);
            if (leaderProxy == null || perkProxy == null) return false;

            var method = leaderType.GetMethod("AddPerk", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(leaderProxy, new object[] { perkProxy, spendPromotionPoints });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.AddPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove the last perk from a unit leader.
    /// </summary>
    public static bool RemoveLastPerk(GameObj leader)
    {
        if (leader.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return false;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return false;

            var method = leaderType.GetMethod("TryRemoveLastPerk", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(proxy, null);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.RemoveLastPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a leader has a specific perk.
    /// </summary>
    public static bool HasPerk(GameObj leader, GameObj perkTemplate)
    {
        if (leader.IsNull || perkTemplate.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            var perkType = _perkTemplateType?.ManagedType;
            if (leaderType == null || perkType == null) return false;

            var leaderProxy = GetManagedProxy(leader, leaderType);
            var perkProxy = GetManagedProxy(perkTemplate, perkType);
            if (leaderProxy == null || perkProxy == null) return false;

            var method = leaderType.GetMethod("HasPerk", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            return (bool)method.Invoke(leaderProxy, new object[] { perkProxy });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.HasPerk", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the last perk added to a leader.
    /// </summary>
    public static GameObj GetLastPerk(GameObj leader)
    {
        if (leader.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var leaderType = _unitLeaderType?.ManagedType;
            if (leaderType == null) return GameObj.Null;

            var proxy = GetManagedProxy(leader, leaderType);
            if (proxy == null) return GameObj.Null;

            var method = leaderType.GetMethod("GetLastPerk", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return GameObj.Null;

            var result = method.Invoke(proxy, null);
            if (result == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)result).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetLastPerk", "Failed", ex);
            return GameObj.Null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Perk Finding
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find a perk template by name from all perk trees of a leader.
    /// </summary>
    public static GameObj FindPerkByName(GameObj leader, string perkName)
    {
        if (leader.IsNull || string.IsNullOrEmpty(perkName)) return GameObj.Null;

        try
        {
            var trees = GetPerkTrees(leader);
            foreach (var tree in trees)
            {
                foreach (var perk in tree.Perks)
                {
                    if (perk.Name?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true ||
                        perk.Title?.Contains(perkName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return new GameObj(perk.Pointer);
                    }
                }
            }
            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get available perks (not yet learned) for a leader.
    /// </summary>
    public static List<PerkInfo> GetAvailablePerks(GameObj leader)
    {
        var result = new List<PerkInfo>();
        if (leader.IsNull) return result;

        try
        {
            // Get all learned perks
            var learnedPerks = new HashSet<IntPtr>();
            var learned = GetLeaderPerks(leader);
            foreach (var p in learned)
                learnedPerks.Add(p.Pointer);

            // Get all perks from trees
            var trees = GetPerkTrees(leader);
            foreach (var tree in trees)
            {
                foreach (var perk in tree.Perks)
                {
                    if (!learnedPerks.Contains(perk.Pointer))
                        result.Add(perk);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Perks.GetAvailablePerks", "Failed", ex);
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for Perks SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // perks <nickname> - Show unit's perks
        DevConsole.RegisterCommand("perks", "<nickname>", "Show a unit's learned perks", args =>
        {
            if (args.Length == 0)
                return "Usage: perks <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            var perks = GetLeaderPerks(leader);
            if (perks.Count == 0)
                return $"{nickname} has no perks";

            var lines = new List<string> { $"{nickname}'s Perks ({perks.Count}):" };
            foreach (var p in perks)
            {
                var title = !string.IsNullOrEmpty(p.Title) ? p.Title : p.Name;
                var active = p.IsActive ? " [Active]" : "";
                lines.Add($"  {title}{active}");
            }
            return string.Join("\n", lines);
        });

        // perktrees <nickname> - Show available perk trees
        DevConsole.RegisterCommand("perktrees", "<nickname>", "Show a unit's perk trees", args =>
        {
            if (args.Length == 0)
                return "Usage: perktrees <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            var trees = GetPerkTrees(leader);
            if (trees.Count == 0)
                return $"{nickname} has no perk trees";

            var lines = new List<string> { $"{nickname}'s Perk Trees ({trees.Count}):" };
            foreach (var tree in trees)
            {
                lines.Add($"  {tree.Name} ({tree.PerkCount} perks):");
                foreach (var perk in tree.Perks)
                {
                    var title = !string.IsNullOrEmpty(perk.Title) ? perk.Title : perk.Name;
                    lines.Add($"    T{perk.Tier}: {title}");
                }
            }
            return string.Join("\n", lines);
        });

        // availableperks <nickname> - Show perks available to learn
        DevConsole.RegisterCommand("availableperks", "<nickname>", "Show perks a unit can still learn", args =>
        {
            if (args.Length == 0)
                return "Usage: availableperks <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            var available = GetAvailablePerks(leader);
            if (available.Count == 0)
                return $"{nickname} has learned all available perks";

            var canPromote = CanBePromoted(leader);
            var lines = new List<string> { $"Available Perks ({available.Count}) - Can Promote: {canPromote}" };

            // Group by tier
            var byTier = new Dictionary<int, List<PerkInfo>>();
            foreach (var p in available)
            {
                if (!byTier.ContainsKey(p.Tier))
                    byTier[p.Tier] = new List<PerkInfo>();
                byTier[p.Tier].Add(p);
            }

            foreach (var tier in byTier.Keys)
            {
                lines.Add($"  Tier {tier}:");
                foreach (var p in byTier[tier])
                {
                    var title = !string.IsNullOrEmpty(p.Title) ? p.Title : p.Name;
                    lines.Add($"    {title}");
                }
            }
            return string.Join("\n", lines);
        });

        // addperk <nickname> <perk> - Add a perk to a unit
        DevConsole.RegisterCommand("addperk", "<nickname> <perk>", "Add a perk to a unit (no cost)", args =>
        {
            if (args.Length < 2)
                return "Usage: addperk <nickname> <perk>";

            var nickname = args[0];
            var perkName = string.Join(" ", args, 1, args.Length - 1);

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            var perk = FindPerkByName(leader, perkName);
            if (perk.IsNull)
                return $"Perk '{perkName}' not found in {nickname}'s perk trees";

            if (AddPerk(leader, perk, false))
            {
                var info = GetPerkInfo(perk);
                return $"Added perk '{info?.Title ?? perkName}' to {nickname}";
            }
            return "Failed to add perk";
        });

        // removeperk <nickname> - Remove last perk from a unit
        DevConsole.RegisterCommand("removeperk", "<nickname>", "Remove last perk from a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: removeperk <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Leader '{nickname}' not found";

            if (!CanBeDemoted(leader))
                return $"{nickname} cannot be demoted (no perks to remove)";

            var lastPerk = GetLastPerk(leader);
            var perkName = lastPerk.IsNull ? "unknown" : (GetPerkInfo(lastPerk)?.Title ?? lastPerk.GetName());

            if (RemoveLastPerk(leader))
                return $"Removed perk '{perkName}' from {nickname}";
            return "Failed to remove perk";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _perkTemplateType ??= GameType.Find("Menace.Strategy.PerkTemplate");
        _perkTreeTemplateType ??= GameType.Find("Menace.Strategy.PerkTreeTemplate");
        _perkType ??= GameType.Find("Menace.Strategy.Perk");
        _skillTemplateType ??= GameType.Find("Menace.Tactical.Skills.SkillTemplate");
        _unitLeaderType ??= GameType.Find("Menace.Strategy.BaseUnitLeader");
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
