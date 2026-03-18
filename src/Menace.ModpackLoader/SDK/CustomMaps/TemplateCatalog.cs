using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Provides a catalog of available EntityTemplates for the map editor.
/// Categorizes templates by entity type (Structure vs Actor) and usage.
/// </summary>
public static class TemplateCatalog
{
    // Entity type values from RE
    private const int ENTITY_TYPE_STRUCTURE = 1;
    private const int ENTITY_TYPE_TRANSIENT_ACTOR = 2;

    // Template offsets from RE
    private const int OFFSET_ENTITY_TYPE = 0x88;
    private const int OFFSET_STRUCTURE_TYPE = 0x90;
    private const int OFFSET_PREFABS = 0x138;

    /// <summary>
    /// Catalog entry for an EntityTemplate.
    /// </summary>
    public class TemplateEntry
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public int EntityType { get; set; }
        public bool IsStructure => EntityType == ENTITY_TYPE_STRUCTURE;
        public bool IsActor => EntityType == ENTITY_TYPE_TRANSIENT_ACTOR;
        public List<string> PrefabNames { get; set; } = new();
        public string DisplayName { get; set; }
        public IntPtr Pointer { get; set; }
    }

    // Cached catalog
    private static readonly Dictionary<string, TemplateEntry> _catalog = new();
    private static readonly Dictionary<string, List<TemplateEntry>> _byCategory = new();
    private static bool _catalogBuilt;

    /// <summary>
    /// Get all templates in the catalog.
    /// </summary>
    public static IReadOnlyDictionary<string, TemplateEntry> GetCatalog()
    {
        EnsureCatalogBuilt();
        return _catalog;
    }

    /// <summary>
    /// Get templates by category.
    /// </summary>
    public static IReadOnlyList<TemplateEntry> GetByCategory(string category)
    {
        EnsureCatalogBuilt();
        return _byCategory.TryGetValue(category, out var list)
            ? list
            : Array.Empty<TemplateEntry>();
    }

    /// <summary>
    /// Get all structures (buildings, objects that block tiles).
    /// </summary>
    public static IReadOnlyList<TemplateEntry> GetStructures()
    {
        EnsureCatalogBuilt();
        return _byCategory.TryGetValue("Structures", out var list)
            ? list
            : Array.Empty<TemplateEntry>();
    }

    /// <summary>
    /// Get all actors (units, vehicles).
    /// </summary>
    public static IReadOnlyList<TemplateEntry> GetActors()
    {
        EnsureCatalogBuilt();
        return _byCategory.TryGetValue("Actors", out var list)
            ? list
            : Array.Empty<TemplateEntry>();
    }

    /// <summary>
    /// Get available categories.
    /// </summary>
    public static string[] GetCategories()
    {
        EnsureCatalogBuilt();
        return _byCategory.Keys.ToArray();
    }

    /// <summary>
    /// Find a template by name.
    /// </summary>
    public static TemplateEntry FindTemplate(string name)
    {
        EnsureCatalogBuilt();
        return _catalog.TryGetValue(name, out var entry) ? entry : null;
    }

    /// <summary>
    /// Search templates by name pattern.
    /// </summary>
    public static List<TemplateEntry> SearchTemplates(string pattern)
    {
        EnsureCatalogBuilt();

        var lowerPattern = pattern?.ToLowerInvariant() ?? "";
        return _catalog.Values
            .Where(e => e.Name.ToLowerInvariant().Contains(lowerPattern) ||
                       (e.DisplayName?.ToLowerInvariant().Contains(lowerPattern) ?? false))
            .ToList();
    }

    /// <summary>
    /// Build or rebuild the template catalog.
    /// </summary>
    public static void BuildCatalog()
    {
        _catalog.Clear();
        _byCategory.Clear();

        try
        {
            // Find all EntityTemplate instances
            var templates = Templates.FindAll("Menace.Tactical.EntityTemplate");

            foreach (var template in templates)
            {
                if (template.IsNull) continue;

                var entry = CreateEntry(template);
                if (entry == null) continue;

                _catalog[entry.Name] = entry;

                // Add to category
                if (!_byCategory.TryGetValue(entry.Category, out var list))
                {
                    list = new List<TemplateEntry>();
                    _byCategory[entry.Category] = list;
                }
                list.Add(entry);
            }

            // Sort categories
            foreach (var list in _byCategory.Values)
            {
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }

            _catalogBuilt = true;

            SdkLogger.Msg($"[TemplateCatalog] Built catalog: {_catalog.Count} templates in {_byCategory.Count} categories");

            // Log category counts
            foreach (var (category, list) in _byCategory)
            {
                SdkLogger.Msg($"  {category}: {list.Count}");
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TemplateCatalog.BuildCatalog", "Failed to build catalog", ex);
        }
    }

    /// <summary>
    /// Clear the catalog cache.
    /// </summary>
    public static void ClearCache()
    {
        _catalog.Clear();
        _byCategory.Clear();
        _catalogBuilt = false;
    }

    /// <summary>
    /// Ensure catalog is built.
    /// </summary>
    private static void EnsureCatalogBuilt()
    {
        if (!_catalogBuilt)
            BuildCatalog();
    }

    /// <summary>
    /// Create a catalog entry from an EntityTemplate.
    /// </summary>
    private static TemplateEntry CreateEntry(GameObj template)
    {
        try
        {
            var name = template.GetName();
            if (string.IsNullOrEmpty(name)) return null;

            var entry = new TemplateEntry
            {
                Name = name,
                Pointer = template.Pointer,
                DisplayName = GetDisplayName(template, name)
            };

            // Read entity type from offset 0x88
            entry.EntityType = template.ReadInt((uint)OFFSET_ENTITY_TYPE);

            // Categorize based on entity type and name patterns
            entry.Category = CategorizeTemplate(name, entry.EntityType);

            // Try to get prefab names
            entry.PrefabNames = GetPrefabNames(template);

            return entry;
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TemplateCatalog.CreateEntry", $"Failed for template: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get display name from localization if available.
    /// </summary>
    private static string GetDisplayName(GameObj template, string fallback)
    {
        try
        {
            // Try to read DisplayName property
            var displayName = Templates.ReadField(template, "DisplayName");
            if (displayName != null)
            {
                var str = displayName.ToString();
                if (!string.IsNullOrEmpty(str) && str != fallback)
                    return str;
            }

            // Try localized name
            var localized = Templates.GetLocalizedText("Menace.Tactical.EntityTemplate", fallback, "DisplayName");
            if (!string.IsNullOrEmpty(localized))
                return localized;
        }
        catch { }

        return fallback;
    }

    /// <summary>
    /// Get prefab names from the template's Prefabs list.
    /// </summary>
    private static List<string> GetPrefabNames(GameObj template)
    {
        var names = new List<string>();

        try
        {
            // Read Prefabs property
            var prefabs = Templates.ReadField(template, "Prefabs");
            if (prefabs == null) return names;

            // Try to iterate the list
            var prefabsType = prefabs.GetType();
            var countProp = prefabsType.GetProperty("Count");
            var itemProp = prefabsType.GetProperty("Item");

            if (countProp == null || itemProp == null) return names;

            var count = (int)countProp.GetValue(prefabs);
            for (int i = 0; i < count && i < 10; i++) // Limit to 10 prefabs
            {
                var prefab = itemProp.GetValue(prefabs, new object[] { i });
                if (prefab is UnityEngine.Object unityObj && unityObj != null)
                {
                    names.Add(unityObj.name);
                }
            }
        }
        catch { }

        return names;
    }

    /// <summary>
    /// Categorize a template based on name patterns and entity type.
    /// </summary>
    private static string CategorizeTemplate(string name, int entityType)
    {
        var lower = name.ToLowerInvariant();

        // Structures
        if (entityType == ENTITY_TYPE_STRUCTURE)
        {
            if (lower.Contains("building") || lower.Contains("house") || lower.Contains("bunker"))
                return "Buildings";
            if (lower.Contains("cover") || lower.Contains("barrier") || lower.Contains("sandbag"))
                return "Cover";
            if (lower.Contains("wall") || lower.Contains("fence"))
                return "Walls";
            if (lower.Contains("vehicle") || lower.Contains("car") || lower.Contains("truck"))
                return "Vehicles";
            if (lower.Contains("tree") || lower.Contains("rock") || lower.Contains("boulder"))
                return "Terrain";
            return "Structures";
        }

        // Actors
        if (entityType == ENTITY_TYPE_TRANSIENT_ACTOR)
        {
            if (lower.Contains("turret") || lower.Contains("sentry"))
                return "Turrets";
            if (lower.Contains("drone"))
                return "Drones";
            if (lower.Contains("robot") || lower.Contains("mech"))
                return "Mechs";
            return "Actors";
        }

        // Unknown
        return "Other";
    }

    /// <summary>
    /// Register console commands for template browsing.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // listtemplates [category] - List available templates
        DevConsole.RegisterCommand("listtemplates", "[category]",
            "List available entity templates for map spawning", args =>
        {
            EnsureCatalogBuilt();

            if (args.Length == 0)
            {
                // Show summary
                var lines = new List<string> { "Template Categories:" };
                foreach (var cat in GetCategories())
                {
                    var count = GetByCategory(cat).Count;
                    lines.Add($"  {cat}: {count} templates");
                }
                lines.Add("\nUse: listtemplates <category> to see details");
                return string.Join("\n", lines);
            }

            var category = args[0];
            var templates = GetByCategory(category);
            if (templates.Count == 0)
                return $"No templates in category: {category}";

            var result = new List<string> { $"{category} ({templates.Count}):" };
            foreach (var t in templates.Take(30))
            {
                var typeStr = t.IsStructure ? "[S]" : "[A]";
                var prefabStr = t.PrefabNames.Count > 0 ? $" ({t.PrefabNames.Count} prefabs)" : "";
                result.Add($"  {typeStr} {t.Name}{prefabStr}");
            }

            if (templates.Count > 30)
                result.Add($"  ... and {templates.Count - 30} more");

            return string.Join("\n", result);
        });

        // searchtemplates <pattern> - Search templates
        DevConsole.RegisterCommand("searchtemplates", "<pattern>",
            "Search templates by name pattern", args =>
        {
            if (args.Length == 0)
                return "Usage: searchtemplates <pattern>";

            var results = SearchTemplates(args[0]);
            if (results.Count == 0)
                return $"No templates matching: {args[0]}";

            var lines = new List<string> { $"Found {results.Count} matches:" };
            foreach (var t in results.Take(20))
            {
                var typeStr = t.IsStructure ? "[S]" : "[A]";
                lines.Add($"  {typeStr} {t.Name} ({t.Category})");
            }

            if (results.Count > 20)
                lines.Add($"  ... and {results.Count - 20} more");

            return string.Join("\n", lines);
        });

        // templateinfo <name> - Show template details
        DevConsole.RegisterCommand("templateinfo", "<name>",
            "Show detailed info about a template", args =>
        {
            if (args.Length == 0)
                return "Usage: templateinfo <template_name>";

            var entry = FindTemplate(args[0]);
            if (entry == null)
                return $"Template not found: {args[0]}";

            var lines = new List<string>
            {
                $"Template: {entry.Name}",
                $"  Display Name: {entry.DisplayName}",
                $"  Category: {entry.Category}",
                $"  Type: {(entry.IsStructure ? "Structure" : "Actor")} (EntityType={entry.EntityType})",
                $"  Prefabs ({entry.PrefabNames.Count}):"
            };

            foreach (var prefab in entry.PrefabNames)
                lines.Add($"    - {prefab}");

            return string.Join("\n", lines);
        });

        // rebuildtemplates - Rebuild catalog
        DevConsole.RegisterCommand("rebuildtemplates", "",
            "Rebuild the template catalog", args =>
        {
            ClearCache();
            BuildCatalog();
            return "Template catalog rebuilt";
        });
    }

    /// <summary>
    /// Get templates suitable for painting on tiles (structures only).
    /// Returns templates organized by category for the editor UI.
    /// </summary>
    public static Dictionary<string, List<TemplateEntry>> GetPaintableTemplates()
    {
        EnsureCatalogBuilt();

        var result = new Dictionary<string, List<TemplateEntry>>();

        // Include structure categories that make sense for tile painting
        var paintableCategories = new[]
        {
            "Buildings", "Cover", "Walls", "Vehicles", "Terrain", "Structures"
        };

        foreach (var category in paintableCategories)
        {
            var templates = GetByCategory(category);
            if (templates.Count > 0)
            {
                result[category] = templates.ToList();
            }
        }

        return result;
    }

    /// <summary>
    /// Get templates suitable for spawning as units/enemies.
    /// </summary>
    public static Dictionary<string, List<TemplateEntry>> GetSpawnableActors()
    {
        EnsureCatalogBuilt();

        var result = new Dictionary<string, List<TemplateEntry>>();

        // Include actor categories
        var actorCategories = new[]
        {
            "Actors", "Turrets", "Drones", "Mechs"
        };

        foreach (var category in actorCategories)
        {
            var templates = GetByCategory(category);
            if (templates.Count > 0)
            {
                result[category] = templates.ToList();
            }
        }

        return result;
    }
}
