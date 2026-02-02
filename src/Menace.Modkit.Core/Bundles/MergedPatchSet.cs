using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Represents the merged result of all modpack patches applied in load order (last-wins).
/// Structure: templateType → instanceName → field → value
/// </summary>
public class MergedPatchSet
{
    public Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> Patches { get; set; } = new();

    /// <summary>
    /// Merge multiple modpack patch sets in order. Later entries override earlier ones (last-wins).
    /// </summary>
    public static MergedPatchSet MergePatchSets(
        IEnumerable<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>> orderedPatchSets)
    {
        var merged = new MergedPatchSet();

        foreach (var patchSet in orderedPatchSets)
        {
            foreach (var (templateType, instances) in patchSet)
            {
                if (!merged.Patches.TryGetValue(templateType, out var mergedInstances))
                {
                    mergedInstances = new Dictionary<string, Dictionary<string, JsonElement>>();
                    merged.Patches[templateType] = mergedInstances;
                }

                foreach (var (instanceName, fields) in instances)
                {
                    if (!mergedInstances.TryGetValue(instanceName, out var mergedFields))
                    {
                        mergedFields = new Dictionary<string, JsonElement>();
                        mergedInstances[instanceName] = mergedFields;
                    }

                    foreach (var (fieldName, value) in fields)
                    {
                        mergedFields[fieldName] = value; // last-wins
                    }
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Get all unique template types that have patches.
    /// </summary>
    public IEnumerable<string> GetTemplateTypes() => Patches.Keys;

    /// <summary>
    /// Get the merged fields for a specific template instance.
    /// </summary>
    public Dictionary<string, JsonElement>? GetInstancePatch(string templateType, string instanceName)
    {
        if (Patches.TryGetValue(templateType, out var instances))
        {
            if (instances.TryGetValue(instanceName, out var fields))
                return fields;
        }
        return null;
    }
}
