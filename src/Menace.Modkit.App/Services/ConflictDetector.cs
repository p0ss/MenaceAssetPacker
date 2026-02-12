using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Detects conflicts between active modpacks: field-level, DLL name, and dependency issues.
/// </summary>
public class ConflictDetector
{
    /// <summary>
    /// Detect field-level conflicts across all active modpacks.
    /// A conflict exists when two or more modpacks modify the same field on the same template instance.
    /// </summary>
    public List<FieldConflict> DetectFieldConflicts(List<ModpackManifest> orderedModpacks)
    {
        // field key → list of (modpack, value)
        var fieldMap = new Dictionary<string, List<ConflictingMod>>();

        foreach (var modpack in orderedModpacks)
        {
            // Collect patches from the manifest
            if (modpack.Patches != null)
            {
                foreach (var (templateType, instances) in modpack.Patches)
                {
                    foreach (var (instanceName, fields) in instances)
                    {
                        foreach (var (fieldName, value) in fields)
                        {
                            var key = $"{templateType}/{instanceName}/{fieldName}";
                            if (!fieldMap.TryGetValue(key, out var list))
                            {
                                list = new List<ConflictingMod>();
                                fieldMap[key] = list;
                            }
                            list.Add(new ConflictingMod
                            {
                                ModpackName = modpack.Name,
                                LoadOrder = modpack.LoadOrder,
                                Value = value.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            // Also scan stats/*.json files in the staging directory
            var statsDir = Path.Combine(modpack.Path, "stats");
            if (Directory.Exists(statsDir))
            {
                foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
                {
                    var templateType = Path.GetFileNameWithoutExtension(statsFile);
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(statsFile));
                        foreach (var instanceProp in doc.RootElement.EnumerateObject())
                        {
                            if (instanceProp.Value.ValueKind != JsonValueKind.Object) continue;
                            foreach (var fieldProp in instanceProp.Value.EnumerateObject())
                            {
                                var key = $"{templateType}/{instanceProp.Name}/{fieldProp.Name}";
                                if (!fieldMap.TryGetValue(key, out var list))
                                {
                                    list = new List<ConflictingMod>();
                                    fieldMap[key] = list;
                                }
                                // Avoid duplicates if already from patches
                                if (!list.Any(c => c.ModpackName == modpack.Name))
                                {
                                    list.Add(new ConflictingMod
                                    {
                                        ModpackName = modpack.Name,
                                        LoadOrder = modpack.LoadOrder,
                                        Value = fieldProp.Value.ToString()
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        // Only return keys with 2+ modpacks
        var conflicts = new List<FieldConflict>();
        foreach (var (key, mods) in fieldMap)
        {
            if (mods.Count < 2) continue;

            var parts = key.Split('/', 3);
            if (parts.Length < 3) continue;

            var sorted = mods.OrderBy(m => m.LoadOrder).ToList();
            conflicts.Add(new FieldConflict
            {
                TemplateType = parts[0],
                InstanceName = parts[1],
                FieldName = parts[2],
                ConflictingMods = sorted,
                Winner = sorted.Last().ModpackName
            });
        }

        return conflicts;
    }

    /// <summary>
    /// Detect DLL assembly name collisions across modpacks.
    /// </summary>
    public List<DllConflict> DetectDllConflicts(List<ModpackManifest> modpacks)
    {
        var assemblyMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var modpack in modpacks)
        {
            if (modpack.Code?.PrebuiltDlls == null) continue;

            foreach (var dll in modpack.Code.PrebuiltDlls)
            {
                var asmName = Path.GetFileNameWithoutExtension(dll);
                if (!assemblyMap.TryGetValue(asmName, out var list))
                {
                    list = new List<string>();
                    assemblyMap[asmName] = list;
                }
                list.Add(modpack.Name);
            }
        }

        return assemblyMap
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new DllConflict
            {
                AssemblyName = kvp.Key,
                ModpackNames = kvp.Value
            })
            .ToList();
    }

    /// <summary>
    /// Detect missing or incompatible dependencies.
    /// </summary>
    public List<DependencyIssue> DetectDependencyIssues(List<ModpackManifest> modpacks)
    {
        var issues = new List<DependencyIssue>();

        // Use GroupBy to handle duplicate modpack names (take first occurrence)
        var available = modpacks
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Version, StringComparer.OrdinalIgnoreCase);

        foreach (var modpack in modpacks)
        {
            foreach (var dep in modpack.ParsedDependencies)
            {
                if (!available.TryGetValue(dep.Name, out var availableVersion))
                {
                    issues.Add(new DependencyIssue
                    {
                        ModpackName = modpack.Name,
                        RequiredDependency = dep.ToString(),
                        Severity = DependencyIssueSeverity.Missing,
                        Message = $"'{modpack.Name}' requires '{dep}' but it is not installed"
                    });
                }
                else if (!dep.IsSatisfiedBy(availableVersion))
                {
                    issues.Add(new DependencyIssue
                    {
                        ModpackName = modpack.Name,
                        RequiredDependency = dep.ToString(),
                        Severity = DependencyIssueSeverity.IncompatibleVersion,
                        Message = $"'{modpack.Name}' requires '{dep}' but installed version is {availableVersion}"
                    });
                }
            }
        }

        return issues;
    }
}
