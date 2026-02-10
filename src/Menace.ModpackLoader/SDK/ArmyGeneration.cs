using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for army generation operations.
/// Provides safe access to army creation, budget management, and unit selection.
///
/// Based on reverse engineering findings:
/// - Army.Template @ +0x10
/// - Army.Entries @ +0x18
/// - Army.TotalBudget @ +0x20
/// - ArmyEntry.Template @ +0x10
/// - ArmyEntry.Count @ +0x18
/// </summary>
public static class ArmyGeneration
{
    // Cached types
    private static GameType _armyType;
    private static GameType _armyEntryType;
    private static GameType _armyTemplateType;
    private static GameType _entityTemplateType;

    // Default spawn area index
    public const byte DEFAULT_SPAWN_AREA = 3;

    /// <summary>
    /// Army information structure.
    /// </summary>
    public class ArmyInfo
    {
        public string TemplateName { get; set; }
        public int TotalBudget { get; set; }
        public int UsedBudget { get; set; }
        public int UnitCount { get; set; }
        public int EntryCount { get; set; }
        public List<ArmyEntryInfo> Entries { get; set; } = new();
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Army entry information structure.
    /// </summary>
    public class ArmyEntryInfo
    {
        public string TemplateName { get; set; }
        public int Count { get; set; }
        public int Cost { get; set; }
        public int TotalCost { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get army information from an army object.
    /// </summary>
    public static ArmyInfo GetArmyInfo(GameObj army)
    {
        if (army.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var armyType = _armyType?.ManagedType;
            if (armyType == null) return null;

            var proxy = GetManagedProxy(army, armyType);
            if (proxy == null) return null;

            var info = new ArmyInfo { Pointer = army.Pointer };

            // Get template
            var templateProp = armyType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get budget
            var budgetProp = armyType.GetProperty("TotalBudget", BindingFlags.Public | BindingFlags.Instance);
            if (budgetProp != null)
                info.TotalBudget = (int)budgetProp.GetValue(proxy);

            // Get entries
            var entriesProp = armyType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance);
            var entries = entriesProp?.GetValue(proxy);
            if (entries != null)
            {
                var listType = entries.GetType();
                var countProp = listType.GetProperty("Count");
                var indexer = listType.GetMethod("get_Item");

                int count = (int)countProp.GetValue(entries);
                info.EntryCount = count;

                for (int i = 0; i < count; i++)
                {
                    var entry = indexer.Invoke(entries, new object[] { i });
                    if (entry == null) continue;

                    var entryInfo = GetEntryInfo(new GameObj(((Il2CppObjectBase)entry).Pointer));
                    if (entryInfo != null)
                    {
                        info.Entries.Add(entryInfo);
                        info.UnitCount += entryInfo.Count;
                        info.UsedBudget += entryInfo.TotalCost;
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ArmyGeneration.GetArmyInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get information about an army entry.
    /// </summary>
    public static ArmyEntryInfo GetEntryInfo(GameObj entry)
    {
        if (entry.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var entryType = _armyEntryType?.ManagedType;
            if (entryType == null) return null;

            var proxy = GetManagedProxy(entry, entryType);
            if (proxy == null) return null;

            var info = new ArmyEntryInfo { Pointer = entry.Pointer };

            // Get template
            var templateProp = entryType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();

                // Get cost from template
                var costProp = template.GetType().GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
                if (costProp != null)
                    info.Cost = (int)costProp.GetValue(template);
            }

            // Get count
            var countProp = entryType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProp != null)
                info.Count = (int)countProp.GetValue(proxy);

            info.TotalCost = info.Cost * info.Count;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ArmyGeneration.GetEntryInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if an army template is pickable at current progress and budget.
    /// </summary>
    public static bool IsTemplatePickable(GameObj template, int progress, int budget)
    {
        if (template.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var templateType = _armyTemplateType?.ManagedType;
            if (templateType == null) return false;

            var proxy = GetManagedProxy(template, templateType);
            if (proxy == null) return false;

            var isPickableMethod = templateType.GetMethod("IsPickable",
                BindingFlags.Public | BindingFlags.Instance);
            if (isPickableMethod != null)
            {
                return (bool)isPickableMethod.Invoke(proxy, new object[] { progress, budget });
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get entity cost from template.
    /// </summary>
    public static int GetEntityCost(GameObj entityTemplate)
    {
        if (entityTemplate.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var templateType = _entityTemplateType?.ManagedType;
            if (templateType == null) return 0;

            var proxy = GetManagedProxy(entityTemplate, templateType);
            if (proxy == null) return 0;

            var costProp = templateType.GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
            if (costProp != null)
                return (int)costProp.GetValue(proxy);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get all available army templates.
    /// </summary>
    public static List<string> GetArmyTemplates()
    {
        var templates = GameQuery.FindAll("ArmyTemplate");
        var result = new List<string>();
        foreach (var t in templates)
        {
            var name = t.GetName();
            if (!string.IsNullOrEmpty(name))
                result.Add(name);
        }
        result.Sort();
        return result;
    }

    /// <summary>
    /// Register console commands for ArmyGeneration SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // armytemplates - List available army templates
        DevConsole.RegisterCommand("armytemplates", "", "List available army templates", args =>
        {
            var templates = GetArmyTemplates();
            if (templates.Count == 0)
                return "No army templates found";

            var lines = new List<string> { $"Army Templates ({templates.Count}):" };
            foreach (var t in templates.GetRange(0, Math.Min(20, templates.Count)))
            {
                lines.Add($"  {t}");
            }
            if (templates.Count > 20)
                lines.Add($"  ... and {templates.Count - 20} more");
            return string.Join("\n", lines);
        });

        // entitycost <name> - Get entity template cost
        DevConsole.RegisterCommand("entitycost", "<name>", "Get entity template cost", args =>
        {
            if (args.Length == 0)
                return "Usage: entitycost <template_name>";

            var name = string.Join(" ", args);
            var template = GameQuery.FindByName("EntityTemplate", name);
            if (template.IsNull)
                return $"Entity template '{name}' not found";

            var cost = GetEntityCost(template);
            return $"Entity: {name}\nCost: {cost} points";
        });

        // armyentries <name> - List all entries in an army template (useful for verifying clones)
        DevConsole.RegisterCommand("armyentries", "<name>", "List all entries in an army template", args =>
        {
            if (args.Length == 0)
                return "Usage: armyentries <army_template_name>";

            var name = string.Join(" ", args);
            var template = GameQuery.FindByName("ArmyTemplate", name);
            if (template.IsNull)
                return $"Army template '{name}' not found";

            var entries = GetArmyTemplateEntries(template);
            if (entries.Count == 0)
                return $"Army '{name}' has no entries";

            var lines = new List<string> { $"Army '{name}' entries ({entries.Count}):" };
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                lines.Add($"  [{i}] {e.TemplateName} x{e.Count} (cost: {e.Cost})");
            }
            return string.Join("\n", lines);
        });

        // checkclone <name> - Verify if a template clone is registered
        DevConsole.RegisterCommand("checkclone", "<name>", "Check if a template is registered (useful for verifying clones)", args =>
        {
            if (args.Length == 0)
                return "Usage: checkclone <template_name>";

            var name = string.Join(" ", args);
            var lines = new List<string> { $"Checking template '{name}':" };

            // Try EntityTemplate first (most common clone target)
            var entityTemplate = GameQuery.FindByName("EntityTemplate", name);
            if (!entityTemplate.IsNull)
            {
                var cost = GetEntityCost(entityTemplate);
                lines.Add($"  [OK] Found as EntityTemplate (cost: {cost})");
            }
            else
            {
                lines.Add($"  [--] Not found as EntityTemplate");
            }

            // Try ArmyTemplate
            var armyTemplate = GameQuery.FindByName("ArmyTemplate", name);
            if (!armyTemplate.IsNull)
            {
                lines.Add($"  [OK] Found as ArmyTemplate");
            }

            // Check if it appears in any army entries
            var inArmies = FindTemplateInArmies(name);
            if (inArmies.Count > 0)
            {
                lines.Add($"  Referenced in {inArmies.Count} army template(s):");
                foreach (var army in inArmies.Take(5))
                {
                    lines.Add($"    - {army}");
                }
                if (inArmies.Count > 5)
                    lines.Add($"    ... and {inArmies.Count - 5} more");
            }
            else
            {
                lines.Add($"  Not referenced in any army templates");
            }

            return string.Join("\n", lines);
        });

        // clonestatus - Show status of all registered clones
        DevConsole.RegisterCommand("clonestatus", "", "Show status of clones from loaded modpacks", args =>
        {
            var lines = new List<string> { "Clone Status:" };

            // Get all entity templates and check for ones that might be clones
            // (Clones typically have a naming pattern like original_suffix)
            var entities = GameQuery.FindAll("EntityTemplate");
            int cloneCount = 0;

            foreach (var entity in entities)
            {
                var name = entity.GetName();
                if (string.IsNullOrEmpty(name)) continue;

                // Check if this might be a clone (contains underscore in the last segment)
                var lastDot = name.LastIndexOf('.');
                var lastName = lastDot >= 0 ? name[(lastDot + 1)..] : name;
                if (lastName.Contains("_clone") || lastName.Contains("_elite") ||
                    lastName.Contains("_heavy") || lastName.Contains("_light"))
                {
                    var inArmies = FindTemplateInArmies(name);
                    var status = inArmies.Count > 0 ? $"in {inArmies.Count} armies" : "NOT in any army";
                    lines.Add($"  {name}: {status}");
                    cloneCount++;

                    if (cloneCount >= 20)
                    {
                        lines.Add("  ... (limited to 20 entries)");
                        break;
                    }
                }
            }

            if (cloneCount == 0)
                lines.Add("  No obvious clones detected (looking for *_clone, *_elite, *_heavy, *_light patterns)");

            return string.Join("\n", lines);
        });
    }

    /// <summary>
    /// Get all entries from an ArmyTemplate.
    /// </summary>
    public static List<ArmyEntryInfo> GetArmyTemplateEntries(GameObj armyTemplate)
    {
        var result = new List<ArmyEntryInfo>();
        if (armyTemplate.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var templateType = _armyTemplateType?.ManagedType;
            if (templateType == null) return result;

            var proxy = GetManagedProxy(armyTemplate, templateType);
            if (proxy == null) return result;

            // Get Entries property
            var entriesProp = templateType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance);
            var entries = entriesProp?.GetValue(proxy);
            if (entries == null) return result;

            var listType = entries.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            if (countProp == null || indexer == null) return result;

            int count = (int)countProp.GetValue(entries);
            for (int i = 0; i < count; i++)
            {
                var entry = indexer.Invoke(entries, new object[] { i });
                if (entry == null) continue;

                var entryInfo = new ArmyEntryInfo { Pointer = ((Il2CppObjectBase)entry).Pointer };

                // Get Template field from entry
                var entryType = entry.GetType();
                var templateProp = entryType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
                var template = templateProp?.GetValue(entry);
                if (template != null)
                {
                    var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                    entryInfo.TemplateName = templateObj.GetName() ?? "(unknown)";

                    // Try to get cost from template
                    var costProp = template.GetType().GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
                    if (costProp != null)
                        entryInfo.Cost = (int)costProp.GetValue(template);
                }
                else
                {
                    entryInfo.TemplateName = "(null template)";
                }

                // Get Count/Amount field from entry
                var countField = entryType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)
                    ?? entryType.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance);
                if (countField != null)
                    entryInfo.Count = (int)countField.GetValue(entry);
                else
                    entryInfo.Count = 1;

                entryInfo.TotalCost = entryInfo.Cost * entryInfo.Count;
                result.Add(entryInfo);
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ArmyGeneration.GetArmyTemplateEntries", "Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Find all army templates that reference a given entity template.
    /// </summary>
    public static List<string> FindTemplateInArmies(string entityTemplateName)
    {
        var result = new List<string>();

        try
        {
            var armyTemplates = GameQuery.FindAll("ArmyTemplate");
            foreach (var army in armyTemplates)
            {
                var entries = GetArmyTemplateEntries(army);
                foreach (var entry in entries)
                {
                    if (string.Equals(entry.TemplateName, entityTemplateName, StringComparison.OrdinalIgnoreCase))
                    {
                        var armyName = army.GetName();
                        if (!string.IsNullOrEmpty(armyName) && !result.Contains(armyName))
                            result.Add(armyName);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ArmyGeneration.FindTemplateInArmies", "Failed", ex);
        }

        return result;
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _armyType ??= GameType.Find("Army");
        _armyEntryType ??= GameType.Find("ArmyEntry");
        _armyTemplateType ??= GameType.Find("ArmyTemplate");
        _entityTemplateType ??= GameType.Find("EntityTemplate");
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
