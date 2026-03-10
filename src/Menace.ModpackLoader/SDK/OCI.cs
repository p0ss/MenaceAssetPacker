using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for Orbital Command Interface (OCI) - Ship Upgrades.
/// Provides access to ship upgrade management, slots, and permanent upgrades.
///
/// Game Model:
///   Ship → Slots (Armament, Electronics, Hull) → Upgrades
///   Each slot can hold upgrades, with levels and amounts.
///   OCI Points are spent to install upgrades.
///
/// Based on reverse engineering:
/// - StrategyState → ShipUpgrades @ ProcessSaveState order
/// - ShipUpgrades.m_SlotOverrides, m_PermanentUpgrades, m_SlotLevels, m_UpgradeAmounts
/// - ShipUpgradeTemplate: OciPointsCosts, UpgradeType, UnlockType, UnlockedByFaction
/// </summary>
public static class OCI
{
    // Cached types
    private static GameType _shipUpgradesType;
    private static GameType _shipUpgradeTemplateType;
    private static GameType _shipUpgradeSlotTemplateType;
    private static GameType _strategyStateType;

    // Ship upgrade type enum values (from schema)
    public const int TYPE_ARMAMENT = 0;
    public const int TYPE_ELECTRONICS = 1;
    public const int TYPE_HULL = 2;
    public const int TYPE_HIDDEN = 3;

    // Ship upgrade unlock type enum values
    public const int UNLOCK_ALWAYS = 0;
    public const int UNLOCK_FACTION = 1;
    public const int UNLOCK_EVENT_ONLY = 2;

    /// <summary>
    /// Ship upgrade information structure.
    /// </summary>
    public class UpgradeInfo
    {
        /// <summary>Template name identifier.</summary>
        public string TemplateName { get; set; }
        /// <summary>Localized display name.</summary>
        public string DisplayName { get; set; }
        /// <summary>Localized short name.</summary>
        public string ShortName { get; set; }
        /// <summary>Upgrade type (Armament, Electronics, Hull, Hidden).</summary>
        public int UpgradeType { get; set; }
        /// <summary>Human-readable upgrade type name.</summary>
        public string UpgradeTypeName { get; set; }
        /// <summary>OCI points cost to install.</summary>
        public int OciPointsCost { get; set; }
        /// <summary>Unlock type (Always, Faction, EventOnly).</summary>
        public int UnlockType { get; set; }
        /// <summary>Human-readable unlock type name.</summary>
        public string UnlockTypeName { get; set; }
        /// <summary>Faction type required if UnlockType is Faction.</summary>
        public int UnlockedByFaction { get; set; }
        /// <summary>Human-readable faction name.</summary>
        public string UnlockedByFactionName { get; set; }
        /// <summary>Whether this upgrade is installed.</summary>
        public bool IsInstalled { get; set; }
        /// <summary>Amount installed (for stackable upgrades).</summary>
        public int InstalledAmount { get; set; }
        /// <summary>Whether this upgrade is available to install.</summary>
        public bool IsAvailable { get; set; }
        /// <summary>Pointer to ShipUpgradeTemplate.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Ship upgrade slot information.
    /// </summary>
    public class SlotInfo
    {
        /// <summary>Slot template name.</summary>
        public string TemplateName { get; set; }
        /// <summary>Localized display name.</summary>
        public string DisplayName { get; set; }
        /// <summary>Slot type (Armament, Electronics, Hull).</summary>
        public int SlotType { get; set; }
        /// <summary>Human-readable slot type name.</summary>
        public string SlotTypeName { get; set; }
        /// <summary>Current slot level.</summary>
        public int Level { get; set; }
        /// <summary>Upgrade currently in this slot (if any).</summary>
        public UpgradeInfo CurrentUpgrade { get; set; }
        /// <summary>Pointer to slot template.</summary>
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Core Accessors
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the ShipUpgrades instance from StrategyState.
    /// </summary>
    public static GameObj GetShipUpgrades()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return GameObj.Null;

            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return GameObj.Null;

            // Try ShipUpgrades property
            var upgProp = ssType.GetProperty("ShipUpgrades", BindingFlags.Public | BindingFlags.Instance);
            if (upgProp != null)
            {
                var shipUpg = upgProp.GetValue(ss);
                if (shipUpg != null)
                    return new GameObj(((Il2CppObjectBase)shipUpg).Pointer);
            }

            // Fallback: direct field access
            // ShipUpgrades is early in save order, try common offsets
            var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
            var candidates = new uint[] { 0x78, 0x80, 0x88, 0x48 };
            foreach (var offset in candidates)
            {
                var ptr = ssObj.ReadPtr(offset);
                if (ptr != IntPtr.Zero)
                {
                    var obj = new GameObj(ptr);
                    var gameType = obj.GetGameType();
                    if (gameType?.FullName?.Contains("ShipUpgrade") == true)
                        return obj;
                }
            }

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetShipUpgrades", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get current OCI points available.
    /// </summary>
    public static int GetOciPoints()
    {
        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return 0;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return 0;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return 0;

            var method = suType.GetMethod("GetOciPoints", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (int)method.Invoke(proxy, null);

            // Try property
            var prop = suType.GetProperty("OciPoints", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return (int)(prop.GetValue(proxy) ?? 0);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get max OCI points.
    /// </summary>
    public static int GetMaxOciPoints()
    {
        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return 0;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return 0;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return 0;

            var method = suType.GetMethod("GetMaxOciPoints", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (int)method.Invoke(proxy, null);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrade Queries
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all available ship upgrade templates.
    /// </summary>
    public static List<UpgradeInfo> GetAllUpgradeTemplates()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            var templates = GameQuery.FindAll("ShipUpgradeTemplate");
            foreach (var t in templates)
            {
                var info = GetUpgradeInfo(t);
                if (info != null)
                    result.Add(info);
            }
            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetAllUpgradeTemplates", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get installed upgrades (permanent upgrades).
    /// </summary>
    public static List<UpgradeInfo> GetInstalledUpgrades()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return result;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return result;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return result;

            // Get permanent upgrades
            var getPermanentMethod = suType.GetMethod("GetPermanentUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            if (getPermanentMethod != null)
            {
                var permanent = getPermanentMethod.Invoke(proxy, null);
                if (permanent != null)
                {
                    var listType = permanent.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");

                    int count = (int)(countProp?.GetValue(permanent) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var upgrade = indexer?.Invoke(permanent, new object[] { i });
                        if (upgrade != null)
                        {
                            var upgradeObj = new GameObj(((Il2CppObjectBase)upgrade).Pointer);
                            var info = GetUpgradeInfo(upgradeObj);
                            if (info != null)
                            {
                                info.IsInstalled = true;
                                info.InstalledAmount = GetUpgradeAmount(su, upgradeObj);
                                result.Add(info);
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetInstalledUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get available upgrades (can be installed).
    /// </summary>
    public static List<UpgradeInfo> GetAvailableUpgrades()
    {
        var result = new List<UpgradeInfo>();

        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return result;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return result;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return result;

            // Get available upgrades method
            var getAvailableMethod = suType.GetMethod("GetAvailableUpgrades",
                BindingFlags.Public | BindingFlags.Instance);
            if (getAvailableMethod != null)
            {
                var available = getAvailableMethod.Invoke(proxy, null);
                if (available != null)
                {
                    var listType = available.GetType();
                    var countProp = listType.GetProperty("Count");
                    var indexer = listType.GetMethod("get_Item");

                    int count = (int)(countProp?.GetValue(available) ?? 0);
                    for (int i = 0; i < count; i++)
                    {
                        var upgrade = indexer?.Invoke(available, new object[] { i });
                        if (upgrade != null)
                        {
                            var upgradeObj = new GameObj(((Il2CppObjectBase)upgrade).Pointer);
                            var info = GetUpgradeInfo(upgradeObj);
                            if (info != null)
                            {
                                info.IsAvailable = true;
                                result.Add(info);
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetAvailableUpgrades", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get upgrade info from a template.
    /// </summary>
    public static UpgradeInfo GetUpgradeInfo(GameObj template)
    {
        if (template.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var templateType = _shipUpgradeTemplateType?.ManagedType;
            if (templateType == null) return null;

            var proxy = GetManagedProxy(template, templateType);
            if (proxy == null) return null;

            var info = new UpgradeInfo
            {
                Pointer = template.Pointer,
                TemplateName = template.GetName()
            };

            // Get display name
            var getNameMethod = templateType.GetMethod("GetName",
                BindingFlags.Public | BindingFlags.Instance);
            if (getNameMethod != null)
                info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(proxy, null));

            // Get short name
            var getShortMethod = templateType.GetMethod("GetShortName",
                BindingFlags.Public | BindingFlags.Instance);
            if (getShortMethod != null)
                info.ShortName = Il2CppUtils.ToManagedString(getShortMethod.Invoke(proxy, null));

            // Get upgrade type - read from offset 0x98
            info.UpgradeType = template.ReadInt(0x98);
            info.UpgradeTypeName = GetUpgradeTypeName(info.UpgradeType);

            // Get OCI points cost - offset 0xB0
            info.OciPointsCost = template.ReadInt(0xB0);

            // Get unlock type - offset 0xB4
            info.UnlockType = template.ReadInt(0xB4);
            info.UnlockTypeName = GetUnlockTypeName(info.UnlockType);

            // Get unlocked by faction - offset 0xB8
            info.UnlockedByFaction = template.ReadInt(0xB8);
            info.UnlockedByFactionName = Faction.GetFactionTypeName(info.UnlockedByFaction);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetUpgradeInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Slots
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all upgrade slots.
    /// </summary>
    public static List<SlotInfo> GetSlots()
    {
        var result = new List<SlotInfo>();

        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return result;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return result;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return result;

            // Get slots
            var getSlotsMethod = suType.GetMethod("GetSlots",
                BindingFlags.Public | BindingFlags.Instance);
            if (getSlotsMethod != null)
            {
                var slots = getSlotsMethod.Invoke(proxy, null);
                if (slots != null)
                {
                    var arrayType = slots.GetType();
                    var lengthProp = arrayType.GetProperty("Length");
                    int length = (int)(lengthProp?.GetValue(slots) ?? 0);

                    var getMethod = arrayType.GetMethod("Get") ?? arrayType.GetMethod("get_Item");
                    if (getMethod != null)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            var slot = getMethod.Invoke(slots, new object[] { i });
                            if (slot != null)
                            {
                                var slotObj = new GameObj(((Il2CppObjectBase)slot).Pointer);
                                var info = GetSlotInfo(slotObj, i, su);
                                if (info != null)
                                    result.Add(info);
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetSlots", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get slot info.
    /// </summary>
    public static SlotInfo GetSlotInfo(GameObj slot, int slotIndex, GameObj shipUpgrades)
    {
        if (slot.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var slotType = _shipUpgradeSlotTemplateType?.ManagedType;
            if (slotType == null) return null;

            var proxy = GetManagedProxy(slot, slotType);
            if (proxy == null) return null;

            var info = new SlotInfo
            {
                Pointer = slot.Pointer,
                TemplateName = slot.GetName()
            };

            // Get display name
            var getNameMethod = slotType.GetMethod("GetName",
                BindingFlags.Public | BindingFlags.Instance);
            if (getNameMethod != null)
                info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(proxy, null));

            // Get slot type
            var typeProp = slotType.GetProperty("UpgradeType",
                BindingFlags.Public | BindingFlags.Instance);
            if (typeProp != null)
            {
                info.SlotType = Convert.ToInt32(typeProp.GetValue(proxy));
                info.SlotTypeName = GetUpgradeTypeName(info.SlotType);
            }

            // Get slot level from ShipUpgrades
            if (!shipUpgrades.IsNull)
            {
                info.Level = GetSlotLevel(shipUpgrades, slotIndex);

                // Get current upgrade in slot
                var overrideUpgrade = GetSlotOverride(shipUpgrades, slotIndex);
                if (!overrideUpgrade.IsNull)
                {
                    info.CurrentUpgrade = GetUpgradeInfo(overrideUpgrade);
                    if (info.CurrentUpgrade != null)
                        info.CurrentUpgrade.IsInstalled = true;
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.GetSlotInfo", "Failed", ex);
            return null;
        }
    }

    private static int GetSlotLevel(GameObj shipUpgrades, int slotIndex)
    {
        try
        {
            EnsureTypesLoaded();

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return 0;

            var proxy = GetManagedProxy(shipUpgrades, suType);
            if (proxy == null) return 0;

            var method = suType.GetMethod("GetSlotLevel", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (int)method.Invoke(proxy, new object[] { slotIndex });

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static GameObj GetSlotOverride(GameObj shipUpgrades, int slotIndex)
    {
        try
        {
            EnsureTypesLoaded();

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return GameObj.Null;

            var proxy = GetManagedProxy(shipUpgrades, suType);
            if (proxy == null) return GameObj.Null;

            var method = suType.GetMethod("GetSlotOverride", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                var result = method.Invoke(proxy, new object[] { slotIndex });
                if (result != null)
                    return new GameObj(((Il2CppObjectBase)result).Pointer);
            }

            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    private static int GetUpgradeAmount(GameObj shipUpgrades, GameObj upgrade)
    {
        try
        {
            EnsureTypesLoaded();

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return 0;

            var proxy = GetManagedProxy(shipUpgrades, suType);
            if (proxy == null) return 0;

            var templateType = _shipUpgradeTemplateType?.ManagedType;
            if (templateType == null) return 0;

            var upgradeProxy = GetManagedProxy(upgrade, templateType);
            if (upgradeProxy == null) return 0;

            var method = suType.GetMethod("GetUpgradeAmount", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (int)method.Invoke(proxy, new[] { upgradeProxy });

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Upgrade Installation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Install an upgrade.
    /// </summary>
    public static bool InstallUpgrade(GameObj upgrade)
    {
        if (upgrade.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return false;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return false;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return false;

            var templateType = _shipUpgradeTemplateType?.ManagedType;
            if (templateType == null) return false;

            var upgradeProxy = GetManagedProxy(upgrade, templateType);
            if (upgradeProxy == null) return false;

            var method = suType.GetMethod("InstallUpgrade", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(proxy, new[] { upgradeProxy });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.InstallUpgrade", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Uninstall an upgrade.
    /// </summary>
    public static bool UninstallUpgrade(GameObj upgrade)
    {
        if (upgrade.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var su = GetShipUpgrades();
            if (su.IsNull) return false;

            var suType = _shipUpgradesType?.ManagedType;
            if (suType == null) return false;

            var proxy = GetManagedProxy(su, suType);
            if (proxy == null) return false;

            var templateType = _shipUpgradeTemplateType?.ManagedType;
            if (templateType == null) return false;

            var upgradeProxy = GetManagedProxy(upgrade, templateType);
            if (upgradeProxy == null) return false;

            var method = suType.GetMethod("UninstallUpgrade", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) return false;

            method.Invoke(proxy, new[] { upgradeProxy });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("OCI.UninstallUpgrade", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Find upgrade template by name.
    /// </summary>
    public static GameObj FindUpgrade(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("ShipUpgradeTemplate", name);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Name Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get upgrade type name from enum value.
    /// </summary>
    public static string GetUpgradeTypeName(int upgradeType)
    {
        return upgradeType switch
        {
            TYPE_ARMAMENT => "Armament",
            TYPE_ELECTRONICS => "Electronics",
            TYPE_HULL => "Hull",
            TYPE_HIDDEN => "Hidden",
            _ => $"Type{upgradeType}"
        };
    }

    /// <summary>
    /// Get unlock type name from enum value.
    /// </summary>
    public static string GetUnlockTypeName(int unlockType)
    {
        return unlockType switch
        {
            UNLOCK_ALWAYS => "Always",
            UNLOCK_FACTION => "Faction",
            UNLOCK_EVENT_ONLY => "EventOnly",
            _ => $"Unlock{unlockType}"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for OCI SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // oci - Show OCI status
        DevConsole.RegisterCommand("oci", "", "Show OCI (ship upgrades) status", args =>
        {
            var points = GetOciPoints();
            var maxPoints = GetMaxOciPoints();
            var installed = GetInstalledUpgrades();
            var available = GetAvailableUpgrades();

            var lines = new List<string>
            {
                $"OCI Points: {points}/{maxPoints}",
                $"Installed Upgrades ({installed.Count}):"
            };

            foreach (var u in installed)
            {
                var name = !string.IsNullOrEmpty(u.DisplayName) ? u.DisplayName : u.TemplateName;
                var amount = u.InstalledAmount > 1 ? $" x{u.InstalledAmount}" : "";
                lines.Add($"  [{u.UpgradeTypeName}] {name}{amount}");
            }

            lines.Add($"Available Upgrades ({available.Count}):");
            foreach (var u in available)
            {
                var name = !string.IsNullOrEmpty(u.DisplayName) ? u.DisplayName : u.TemplateName;
                lines.Add($"  [{u.UpgradeTypeName}] {name} ({u.OciPointsCost} pts)");
            }

            return string.Join("\n", lines);
        });

        // ocislots - Show OCI slots
        DevConsole.RegisterCommand("ocislots", "", "Show OCI upgrade slots", args =>
        {
            var slots = GetSlots();
            if (slots.Count == 0)
                return "No slots found (strategy layer not active?)";

            var lines = new List<string> { $"OCI Slots ({slots.Count}):" };
            foreach (var s in slots)
            {
                var name = !string.IsNullOrEmpty(s.DisplayName) ? s.DisplayName : s.TemplateName;
                var upgrade = s.CurrentUpgrade != null
                    ? $" → {s.CurrentUpgrade.DisplayName ?? s.CurrentUpgrade.TemplateName}"
                    : " (empty)";
                lines.Add($"  [{s.SlotTypeName}] {name} Lv{s.Level}{upgrade}");
            }
            return string.Join("\n", lines);
        });

        // ociupgrades - List all upgrade templates
        DevConsole.RegisterCommand("ociupgrades", "[type]", "List all OCI upgrade templates", args =>
        {
            var upgrades = GetAllUpgradeTemplates();
            if (upgrades.Count == 0)
                return "No upgrade templates found";

            // Filter by type if specified
            if (args.Length > 0)
            {
                var typeFilter = args[0].ToLowerInvariant();
                upgrades = upgrades.FindAll(u =>
                    u.UpgradeTypeName.ToLowerInvariant().Contains(typeFilter));
            }

            var lines = new List<string> { $"OCI Upgrades ({upgrades.Count}):" };
            foreach (var u in upgrades)
            {
                var name = !string.IsNullOrEmpty(u.DisplayName) ? u.DisplayName : u.TemplateName;
                var unlock = u.UnlockType == UNLOCK_FACTION
                    ? $" [req: {u.UnlockedByFactionName}]"
                    : u.UnlockType == UNLOCK_EVENT_ONLY
                        ? " [event only]"
                        : "";
                lines.Add($"  [{u.UpgradeTypeName}] {name} ({u.OciPointsCost} pts){unlock}");
            }
            return string.Join("\n", lines);
        });

        // installoci <name> - Install an upgrade
        DevConsole.RegisterCommand("installoci", "<name>", "Install an OCI upgrade", args =>
        {
            if (args.Length == 0)
                return "Usage: installoci <upgrade_name>";

            var name = string.Join(" ", args);
            var upgrade = FindUpgrade(name);
            if (upgrade.IsNull)
                return $"Upgrade '{name}' not found";

            var info = GetUpgradeInfo(upgrade);
            if (InstallUpgrade(upgrade))
                return $"Installed: {info?.DisplayName ?? name}";
            return "Failed to install upgrade";
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Internal Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureTypesLoaded()
    {
        _shipUpgradesType ??= GameType.Find("Menace.Strategy.ShipUpgrades");
        _shipUpgradeTemplateType ??= GameType.Find("Menace.Strategy.ShipUpgradeTemplate");
        _shipUpgradeSlotTemplateType ??= GameType.Find("Menace.Strategy.ShipUpgradeSlotTemplate");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);
}
