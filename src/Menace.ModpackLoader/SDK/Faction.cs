using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for faction management.
/// Provides access to story factions, trust levels, relations, and faction-specific operations.
///
/// Game Model:
///   Campaign → Factions → Operations → Missions
///   Each faction has trust/status, upgrades, and associated operations/planets.
///
/// Based on reverse engineering:
/// - StrategyState.StoryFactions @ offset from Config
/// - StoryFaction.Template, Trust, Status, Upgrades
/// - StoryFactionTemplate.Operations, EnemyAssets, Type
/// </summary>
public static class Faction
{
    // Cached types
    private static GameType _storyFactionType;
    private static GameType _storyFactionTemplateType;
    private static GameType _strategyStateType;
    private static GameType _strategyConfigType;

    // Faction status enum values
    public const int STATUS_UNKNOWN = 0;
    public const int STATUS_KNOWN = 1;

    // Faction type enum values (from schema)
    public const int TYPE_JINGWEI = 0;
    public const int TYPE_UNBENT = 1;
    public const int TYPE_DICE = 2;
    public const int TYPE_TOLIMEN = 3;
    public const int TYPE_LURCHEN = 4;
    public const int TYPE_FIRAN = 5;
    public const int TYPE_CMC = 6;
    public const int TYPE_ZBC = 7;

    /// <summary>
    /// Faction information structure.
    /// </summary>
    public class FactionInfo
    {
        /// <summary>Template name identifier.</summary>
        public string TemplateName { get; set; }
        /// <summary>Localized display name.</summary>
        public string DisplayName { get; set; }
        /// <summary>Faction type enum value.</summary>
        public int FactionType { get; set; }
        /// <summary>Human-readable faction type name.</summary>
        public string FactionTypeName { get; set; }
        /// <summary>Current trust level with player (-100 to 100).</summary>
        public int Trust { get; set; }
        /// <summary>Faction status (Unknown=0, Known=1).</summary>
        public int Status { get; set; }
        /// <summary>Human-readable status name.</summary>
        public string StatusName { get; set; }
        /// <summary>Number of unlocked upgrades.</summary>
        public int UnlockedUpgradeCount { get; set; }
        /// <summary>Total available upgrades.</summary>
        public int TotalUpgradeCount { get; set; }
        /// <summary>Number of operations this faction can offer.</summary>
        public int OperationCount { get; set; }
        /// <summary>Whether this faction currently has an active operation.</summary>
        public bool HasActiveOperation { get; set; }
        /// <summary>Pointer to StoryFaction instance.</summary>
        public IntPtr Pointer { get; set; }
        /// <summary>Pointer to StoryFactionTemplate.</summary>
        public IntPtr TemplatePointer { get; set; }
    }

    /// <summary>
    /// Faction upgrade information.
    /// </summary>
    public class UpgradeInfo
    {
        /// <summary>Upgrade template name.</summary>
        public string TemplateName { get; set; }
        /// <summary>Localized display name.</summary>
        public string DisplayName { get; set; }
        /// <summary>Trust level required to unlock.</summary>
        public int TrustRequired { get; set; }
        /// <summary>Whether this upgrade is unlocked.</summary>
        public bool IsUnlocked { get; set; }
        /// <summary>Pointer to upgrade template.</summary>
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Core Accessors
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get StrategyState instance.
    /// </summary>
    public static GameObj GetStrategyState()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return GameObj.Null;

            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)ss).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetStrategyState", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all story factions in the game.
    /// </summary>
    public static List<GameObj> GetAllFactions()
    {
        var result = new List<GameObj>();

        try
        {
            EnsureTypesLoaded();

            var ss = GetStrategyState();
            if (ss.IsNull) return result;

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return result;

            var ssProxy = GetManagedProxy(ss, ssType);
            if (ssProxy == null) return result;

            // Try GetStoryFactions method first
            var getFactionsMethod = ssType.GetMethod("GetStoryFactions",
                BindingFlags.Public | BindingFlags.Instance);

            if (getFactionsMethod != null)
            {
                var factions = getFactionsMethod.Invoke(ssProxy, null);
                if (factions != null)
                {
                    var listType = factions.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");

                    int count = (int)(countProp?.GetValue(factions) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var faction = indexer?.Invoke(factions, new object[] { i });
                        if (faction != null)
                            result.Add(new GameObj(((Il2CppObjectBase)faction).Pointer));
                    }
                    return result;
                }
            }

            // Fallback: Try StoryFactions property
            var factionsProp = ssType.GetProperty("StoryFactions",
                BindingFlags.Public | BindingFlags.Instance);
            if (factionsProp != null)
            {
                var factions = factionsProp.GetValue(ssProxy);
                if (factions != null)
                {
                    var listType = factions.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");

                    int count = (int)(countProp?.GetValue(factions) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var faction = indexer?.Invoke(factions, new object[] { i });
                        if (faction != null)
                            result.Add(new GameObj(((Il2CppObjectBase)faction).Pointer));
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetAllFactions", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a faction.
    /// </summary>
    public static FactionInfo GetFactionInfo(GameObj faction)
    {
        if (faction.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return null;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return null;

            var info = new FactionInfo { Pointer = faction.Pointer };

            // Get template
            var templateProp = factionType.GetProperty("Template",
                BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplatePointer = templateObj.Pointer;
                info.TemplateName = templateObj.GetName();

                // Get display name from template
                var templateType = _storyFactionTemplateType?.ManagedType;
                if (templateType != null)
                {
                    var templateProxy = GetManagedProxy(templateObj, templateType);
                    if (templateProxy != null)
                    {
                        var getNameMethod = templateType.GetMethod("GetName",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null)
                            info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(templateProxy, null));

                        // Get faction type
                        var typeProp = templateType.GetProperty("Type",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (typeProp != null)
                        {
                            info.FactionType = Convert.ToInt32(typeProp.GetValue(templateProxy));
                            info.FactionTypeName = GetFactionTypeName(info.FactionType);
                        }

                        // Get operation count from template
                        var opsProp = templateType.GetProperty("Operations",
                            BindingFlags.Public | BindingFlags.Instance);
                        var ops = opsProp?.GetValue(templateProxy);
                        if (ops != null)
                        {
                            var lengthProp = ops.GetType().GetProperty("Length") ??
                                           ops.GetType().GetProperty("Count");
                            info.OperationCount = (int)(lengthProp?.GetValue(ops) ?? 0);
                        }
                    }
                }
            }

            // Get trust
            var getTrustMethod = factionType.GetMethod("GetTrust",
                BindingFlags.Public | BindingFlags.Instance);
            if (getTrustMethod != null)
                info.Trust = (int)getTrustMethod.Invoke(proxy, null);

            // Get status
            var getStatusMethod = factionType.GetMethod("GetStatus",
                BindingFlags.Public | BindingFlags.Instance);
            if (getStatusMethod != null)
            {
                info.Status = Convert.ToInt32(getStatusMethod.Invoke(proxy, null));
                info.StatusName = GetStatusName(info.Status);
            }

            // Get upgrade counts
            var getUnlockedMethod = factionType.GetMethod("GetUnlockedUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            if (getUnlockedMethod != null)
            {
                var unlocked = getUnlockedMethod.Invoke(proxy, null);
                if (unlocked != null)
                {
                    var countProp = unlocked.GetType().GetProperty("Count");
                    info.UnlockedUpgradeCount = (int)(countProp?.GetValue(unlocked) ?? 0);
                }
            }

            var getAllUpgradesMethod = factionType.GetMethod("GetAllUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAllUpgradesMethod != null)
            {
                var all = getAllUpgradesMethod.Invoke(proxy, null);
                if (all != null)
                {
                    var countProp = all.GetType().GetProperty("Count");
                    info.TotalUpgradeCount = (int)(countProp?.GetValue(all) ?? 0);
                }
            }

            // Check for active operation
            info.HasActiveOperation = HasActiveOperation(faction);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetFactionInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get faction info for all factions.
    /// </summary>
    public static List<FactionInfo> GetAllFactionInfo()
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactions();
        foreach (var f in factions)
        {
            var info = GetFactionInfo(f);
            if (info != null)
                result.Add(info);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Faction Lookup
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find a faction by template name.
    /// </summary>
    public static GameObj FindByName(string templateName)
    {
        if (string.IsNullOrEmpty(templateName)) return GameObj.Null;

        var factions = GetAllFactions();
        foreach (var f in factions)
        {
            var info = GetFactionInfo(f);
            if (info?.TemplateName?.Contains(templateName, StringComparison.OrdinalIgnoreCase) == true)
                return f;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Find a faction by type.
    /// </summary>
    public static GameObj FindByType(int factionType)
    {
        var factions = GetAllFactions();
        foreach (var f in factions)
        {
            var info = GetFactionInfo(f);
            if (info?.FactionType == factionType)
                return f;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Get factions that have active operations.
    /// </summary>
    public static List<FactionInfo> GetFactionsWithOperations()
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactionInfo();
        foreach (var f in factions)
        {
            if (f.HasActiveOperation)
                result.Add(f);
        }
        return result;
    }

    /// <summary>
    /// Get factions by status (Known/Unknown).
    /// </summary>
    public static List<FactionInfo> GetFactionsByStatus(int status)
    {
        var result = new List<FactionInfo>();
        var factions = GetAllFactionInfo();
        foreach (var f in factions)
        {
            if (f.Status == status)
                result.Add(f);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Trust & Relations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get trust level with a faction.
    /// </summary>
    public static int GetTrust(GameObj faction)
    {
        if (faction.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return 0;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return 0;

            var method = factionType.GetMethod("GetTrust", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (int)method.Invoke(proxy, null);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Change trust with a faction.
    /// </summary>
    public static bool ChangeTrust(GameObj faction, int delta)
    {
        if (faction.IsNull || delta == 0) return false;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return false;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return false;

            var method = factionType.GetMethod("ChangeTrust", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(proxy, new object[] { delta });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.ChangeTrust", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set faction status (Known/Unknown).
    /// </summary>
    public static bool SetStatus(GameObj faction, int status)
    {
        if (faction.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return false;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return false;

            var method = factionType.GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            // Convert to enum
            var statusEnumType = FindTypeByName("StoryFactionStatus");
            object statusEnum = status;
            if (statusEnumType != null)
                statusEnum = Enum.ToObject(statusEnumType, status);

            method.Invoke(proxy, new[] { statusEnum });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.SetStatus", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrades
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get upgrades for a faction.
    /// </summary>
    public static List<UpgradeInfo> GetUpgrades(GameObj faction)
    {
        var result = new List<UpgradeInfo>();
        if (faction.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return result;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return result;

            // Get all upgrades
            var getAllMethod = factionType.GetMethod("GetAllUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAllMethod == null) return result;

            var all = getAllMethod.Invoke(proxy, null);
            if (all == null) return result;

            // Get unlocked set for checking
            var getUnlockedMethod = factionType.GetMethod("GetUnlockedUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            var unlockedSet = new HashSet<IntPtr>();
            if (getUnlockedMethod != null)
            {
                var unlocked = getUnlockedMethod.Invoke(proxy, null);
                if (unlocked != null)
                {
                    var listType = unlocked.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");
                    int count = (int)(countProp?.GetValue(unlocked) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var upgrade = indexer?.Invoke(unlocked, new object[] { i });
                        if (upgrade != null)
                            unlockedSet.Add(((Il2CppObjectBase)upgrade).Pointer);
                    }
                }
            }

            // Iterate all upgrades
            var allListType = all.GetType();
            var allCountProp = allListType.GetProperty("Count");
            var allIndexer = allListType.GetMethod("get_Item");
            int allCount = (int)(allCountProp?.GetValue(all) ?? 0);

            for (int i = 0; i < allCount; i++)
            {
                var upgrade = allIndexer?.Invoke(all, new object[] { i });
                if (upgrade == null) continue;

                var upgradeObj = new GameObj(((Il2CppObjectBase)upgrade).Pointer);
                var info = new UpgradeInfo
                {
                    Pointer = upgradeObj.Pointer,
                    TemplateName = upgradeObj.GetName(),
                    IsUnlocked = unlockedSet.Contains(upgradeObj.Pointer)
                };

                // Get display name and trust required
                var upgradeType = upgrade.GetType();
                var getNameMethod = upgradeType.GetMethod("GetName",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null)
                    info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(upgrade, null));

                var trustProp = upgradeType.GetProperty("TrustRequired",
                    BindingFlags.Public | BindingFlags.Instance);
                if (trustProp != null)
                    info.TrustRequired = (int)(trustProp.GetValue(upgrade) ?? 0);

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Unlock an upgrade for a faction.
    /// </summary>
    public static bool UnlockUpgrade(GameObj faction, GameObj upgrade)
    {
        if (faction.IsNull || upgrade.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var factionType = _storyFactionType?.ManagedType;
            if (factionType == null) return false;

            var proxy = GetManagedProxy(faction, factionType);
            if (proxy == null) return false;

            var method = factionType.GetMethod("UnlockUpgrade",
                BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            var upgradeType = method.GetParameters()[0].ParameterType;
            var upgradeProxy = GetManagedProxy(upgrade, upgradeType);
            if (upgradeProxy == null) return false;

            method.Invoke(proxy, new[] { upgradeProxy });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.UnlockUpgrade", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Operations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if faction has an active operation.
    /// </summary>
    public static bool HasActiveOperation(GameObj faction)
    {
        if (faction.IsNull) return false;

        try
        {
            // Get current operation and check if it's for this faction
            var currentOp = Operation.GetCurrentOperation();
            if (currentOp.IsNull) return false;

            var opInfo = Operation.GetOperationInfo(currentOp);
            if (opInfo == null) return false;

            var factionInfo = GetFactionInfo(faction);
            if (factionInfo == null) return false;

            // Compare faction names
            return opInfo.EnemyFaction == factionInfo.TemplateName ||
                   opInfo.FriendlyFaction == factionInfo.TemplateName;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the operation template list for a faction.
    /// </summary>
    public static List<GameObj> GetOperationTemplates(GameObj faction)
    {
        var result = new List<GameObj>();
        if (faction.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var info = GetFactionInfo(faction);
            if (info?.TemplatePointer == IntPtr.Zero) return result;

            var templateObj = new GameObj(info.TemplatePointer);
            var templateType = _storyFactionTemplateType?.ManagedType;
            if (templateType == null) return result;

            var templateProxy = GetManagedProxy(templateObj, templateType);
            if (templateProxy == null) return result;

            var opsProp = templateType.GetProperty("Operations",
                BindingFlags.Public | BindingFlags.Instance);
            var ops = opsProp?.GetValue(templateProxy);
            if (ops == null) return result;

            // Iterate array
            var arrayType = ops.GetType();
            var lengthProp = arrayType.GetProperty("Length");
            int length = (int)(lengthProp?.GetValue(ops) ?? 0);

            var getMethod = arrayType.GetMethod("Get") ?? arrayType.GetMethod("get_Item");
            if (getMethod != null)
            {
                for (int i = 0; i < length; i++)
                {
                    var op = getMethod.Invoke(ops, new object[] { i });
                    if (op != null)
                        result.Add(new GameObj(((Il2CppObjectBase)op).Pointer));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Faction.GetOperationTemplates", "Failed", ex);
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Name Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get faction type name from enum value.
    /// </summary>
    public static string GetFactionTypeName(int factionType)
    {
        return factionType switch
        {
            TYPE_JINGWEI => "Jingwei",
            TYPE_UNBENT => "Unbent",
            TYPE_DICE => "Dice",
            TYPE_TOLIMEN => "Tolimen",
            TYPE_LURCHEN => "Lurchen",
            TYPE_FIRAN => "Firan",
            TYPE_CMC => "CMC",
            TYPE_ZBC => "ZBC",
            _ => $"Faction{factionType}"
        };
    }

    /// <summary>
    /// Get status name from enum value.
    /// </summary>
    public static string GetStatusName(int status)
    {
        return status switch
        {
            STATUS_UNKNOWN => "Unknown",
            STATUS_KNOWN => "Known",
            _ => $"Status{status}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for Faction SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // factions - List all factions
        DevConsole.RegisterCommand("factions", "", "List all story factions", args =>
        {
            var factions = GetAllFactionInfo();
            if (factions.Count == 0)
                return "No factions found (strategy layer not active?)";

            var lines = new List<string> { $"Story Factions ({factions.Count}):" };
            foreach (var f in factions)
            {
                var status = f.StatusName;
                var ops = f.HasActiveOperation ? " [ACTIVE OP]" : "";
                var upgrades = f.TotalUpgradeCount > 0
                    ? $" [{f.UnlockedUpgradeCount}/{f.TotalUpgradeCount} upgrades]"
                    : "";
                lines.Add($"  {f.FactionTypeName}: Trust {f.Trust}, {status}{upgrades}{ops}");
            }
            return string.Join("\n", lines);
        });

        // faction <name> - Show faction details
        DevConsole.RegisterCommand("faction", "<name>", "Show faction details", args =>
        {
            if (args.Length == 0)
                return "Usage: faction <name>";

            var name = string.Join(" ", args);
            var faction = FindByName(name);
            if (faction.IsNull)
                return $"Faction '{name}' not found";

            var info = GetFactionInfo(faction);
            if (info == null)
                return "Could not get faction info";

            var upgrades = GetUpgrades(faction);
            var unlockedNames = new List<string>();
            var lockedNames = new List<string>();
            foreach (var u in upgrades)
            {
                var uName = !string.IsNullOrEmpty(u.DisplayName) ? u.DisplayName : u.TemplateName;
                if (u.IsUnlocked)
                    unlockedNames.Add(uName);
                else
                    lockedNames.Add($"{uName} (req: {u.TrustRequired})");
            }

            return $"Faction: {info.DisplayName ?? info.TemplateName}\n" +
                   $"Type: {info.FactionTypeName}\n" +
                   $"Status: {info.StatusName}\n" +
                   $"Trust: {info.Trust}\n" +
                   $"Operations: {info.OperationCount} available\n" +
                   $"Active Operation: {info.HasActiveOperation}\n" +
                   $"Unlocked ({unlockedNames.Count}): {string.Join(", ", unlockedNames)}\n" +
                   $"Locked ({lockedNames.Count}): {string.Join(", ", lockedNames)}";
        });

        // settrust <faction> <value> - Set faction trust
        DevConsole.RegisterCommand("settrust", "<faction> <delta>", "Change faction trust", args =>
        {
            if (args.Length < 2)
                return "Usage: settrust <faction> <delta>";

            var name = args[0];
            if (!int.TryParse(args[1], out int delta))
                return "Invalid delta value";

            var faction = FindByName(name);
            if (faction.IsNull)
                return $"Faction '{name}' not found";

            var before = GetTrust(faction);
            if (ChangeTrust(faction, delta))
            {
                var after = GetTrust(faction);
                return $"Trust changed: {before} -> {after} (delta: {delta})";
            }
            return "Failed to change trust";
        });

        // factionops - Show factions with active operations
        DevConsole.RegisterCommand("factionops", "", "Show factions with active operations", args =>
        {
            var factions = GetFactionsWithOperations();
            if (factions.Count == 0)
                return "No factions have active operations";

            var lines = new List<string> { $"Factions with Operations ({factions.Count}):" };
            foreach (var f in factions)
            {
                lines.Add($"  {f.FactionTypeName}: Trust {f.Trust}");
            }
            return string.Join("\n", lines);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Internal Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureTypesLoaded()
    {
        _storyFactionType ??= GameType.Find("Menace.Strategy.StoryFaction");
        _storyFactionTemplateType ??= GameType.Find("Menace.Strategy.StoryFactionTemplate");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
        _strategyConfigType ??= GameType.Find("Menace.Strategy.StrategyConfig");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);

    private static Type FindTypeByName(string typeName)
    {
        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (gameAssembly != null)
            {
                foreach (var t in gameAssembly.GetTypes())
                {
                    if (t.Name == typeName)
                        return t;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
