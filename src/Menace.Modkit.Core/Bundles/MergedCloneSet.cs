using System.Collections.Generic;
using System.Linq;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Merged clone definitions from all active modpacks.
/// Structure: templateType → { newName → sourceName }
/// </summary>
public class MergedCloneSet
{
    /// <summary>
    /// Clone mappings keyed by template type name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Clones { get; } = new();

    /// <summary>
    /// Add clones from a modpack, applying last-wins ordering.
    /// </summary>
    public void AddFromModpack(Dictionary<string, Dictionary<string, string>>? modpackClones)
    {
        if (modpackClones == null) return;

        foreach (var (templateType, cloneMap) in modpackClones)
        {
            if (cloneMap == null) continue;

            if (!Clones.TryGetValue(templateType, out var existing))
            {
                existing = new Dictionary<string, string>();
                Clones[templateType] = existing;
            }

            foreach (var (newName, sourceName) in cloneMap)
            {
                existing[newName] = sourceName;  // Last-wins
            }
        }
    }

    /// <summary>
    /// Get all template types that have clone definitions.
    /// </summary>
    public IEnumerable<string> GetTemplateTypes() => Clones.Keys;

    /// <summary>
    /// Check if any clones are defined.
    /// </summary>
    public bool HasClones => Clones.Count > 0 && Clones.Values.Any(m => m.Count > 0);

    /// <summary>
    /// Get total number of clones across all types.
    /// </summary>
    public int TotalCloneCount => Clones.Values.Sum(m => m.Count);
}
