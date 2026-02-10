using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Current step in the cloning wizard.
/// </summary>
public enum CloneWizardStep
{
    /// <summary>Step 1: Enter clone name</summary>
    Name,
    /// <summary>Step 2: Select which references should include the clone</summary>
    References,
    /// <summary>Step 3: Handle asset dependencies</summary>
    Assets,
    /// <summary>Step 4: Review and generate</summary>
    Review
}

/// <summary>
/// Strategy for how to add the clone to a collection reference.
/// </summary>
public enum CloneInjectionStrategy
{
    /// <summary>Add clone alongside the source (both remain in the list)</summary>
    AddAlongside,
    /// <summary>Replace the source with the clone</summary>
    ReplaceSource
}

/// <summary>
/// Strategy for how to handle an asset dependency.
/// </summary>
public enum AssetCloneStrategy
{
    /// <summary>Clone uses the same asset as source</summary>
    KeepOriginal,
    /// <summary>Clone gets a copy of the asset with a new name</summary>
    CloneAsset,
    /// <summary>User will provide a custom replacement asset</summary>
    ReplaceWithCustom
}

/// <summary>
/// Represents a single reference that could include the clone.
/// </summary>
public class ReferenceSelection
{
    /// <summary>The enhanced reference entry from the graph</summary>
    public required EnhancedReferenceEntry Reference { get; init; }

    /// <summary>Whether this reference is selected for clone injection</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Injection strategy for this reference</summary>
    public CloneInjectionStrategy Strategy { get; set; } = CloneInjectionStrategy.AddAlongside;

    /// <summary>Display text for the reference (e.g., "ArmyTemplate/pirates_standard -> Entries[3]")</summary>
    public string DisplayText =>
        $"{Reference.SourceTemplateType}/{Reference.SourceInstanceName} -> {Reference.FullFieldPath}";

    /// <summary>Short display text (just source instance and path)</summary>
    public string ShortDisplayText =>
        $"{Reference.SourceInstanceName} -> {Reference.FullFieldPath}";
}

/// <summary>
/// Represents an asset dependency of the source template.
/// </summary>
public class AssetDependency
{
    /// <summary>The field name that references this asset</summary>
    public required string FieldName { get; init; }

    /// <summary>Category of asset (e.g., "sprite", "prefab", "audio")</summary>
    public required string Category { get; init; }

    /// <summary>Original asset name/path</summary>
    public required string OriginalAsset { get; init; }

    /// <summary>Strategy for handling this asset</summary>
    public AssetCloneStrategy Strategy { get; set; } = AssetCloneStrategy.KeepOriginal;

    /// <summary>New asset name (when Strategy is CloneAsset)</summary>
    public string? NewAssetName { get; set; }

    /// <summary>Custom asset path (when Strategy is ReplaceWithCustom)</summary>
    public string? CustomAssetPath { get; set; }

    /// <summary>Whether this asset type can be cloned (some require Unity Editor)</summary>
    public bool CanBeCloned => Category switch
    {
        "sprite" or "texture" or "audio" => true,
        "prefab" or "material" => true, // Can export via modkit
        _ => false
    };

    /// <summary>Warning message if asset has limitations</summary>
    public string? Warning => Category switch
    {
        "prefab" => "Prefabs require export and asset bundle building",
        "material" => "Materials require export and asset bundle building",
        _ => null
    };

    /// <summary>Display text for the asset</summary>
    public string DisplayText => $"{FieldName}: {OriginalAsset} ({Category})";
}

/// <summary>
/// Complete state of the cloning wizard.
/// </summary>
public class CloneWizardState
{
    /// <summary>Current wizard step</summary>
    public CloneWizardStep CurrentStep { get; set; } = CloneWizardStep.Name;

    /// <summary>Source template type (e.g., "EntityTemplate")</summary>
    public required string SourceTemplateType { get; init; }

    /// <summary>Source instance name (e.g., "enemy.pirate_captain")</summary>
    public required string SourceInstanceName { get; init; }

    /// <summary>Target modpack name</summary>
    public required string ModpackName { get; init; }

    /// <summary>Name for the new clone</summary>
    public string CloneName { get; set; } = string.Empty;

    /// <summary>Whether to copy all property values from source</summary>
    public bool CopyAllProperties { get; set; } = true;

    /// <summary>Collection references that could include the clone</summary>
    public List<ReferenceSelection> References { get; set; } = new();

    /// <summary>Global injection strategy (applied to all selected references)</summary>
    public CloneInjectionStrategy GlobalStrategy { get; set; } = CloneInjectionStrategy.AddAlongside;

    /// <summary>Asset dependencies of the source template</summary>
    public List<AssetDependency> AssetDependencies { get; set; } = new();

    /// <summary>Generated patches (populated in Review step)</summary>
    public Dictionary<string, Dictionary<string, JsonObject>> GeneratedPatches { get; set; } = new();

    /// <summary>Validation error message (null if valid)</summary>
    public string? ValidationError { get; set; }

    /// <summary>Whether the current step is valid and can proceed</summary>
    public bool CanProceed => string.IsNullOrEmpty(ValidationError);

    /// <summary>Full key for the source template</summary>
    public string SourceKey => $"{SourceTemplateType}/{SourceInstanceName}";

    /// <summary>Suggested default clone name based on source</summary>
    public string SuggestedCloneName
    {
        get
        {
            // If source has dots (e.g., "enemy.pirate_captain"), append to last segment
            var lastDot = SourceInstanceName.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var prefix = SourceInstanceName[..lastDot];
                var suffix = SourceInstanceName[(lastDot + 1)..];
                return $"{prefix}.{suffix}_clone";
            }
            return $"{SourceInstanceName}_clone";
        }
    }

    /// <summary>Validate the current step and set ValidationError.</summary>
    public void Validate()
    {
        ValidationError = CurrentStep switch
        {
            CloneWizardStep.Name => ValidateName(),
            CloneWizardStep.References => ValidateReferences(),
            CloneWizardStep.Assets => ValidateAssets(),
            CloneWizardStep.Review => null, // Always valid to submit from review
            _ => null
        };
    }

    private string? ValidateName()
    {
        if (string.IsNullOrWhiteSpace(CloneName))
            return "Clone name is required";

        if (CloneName == SourceInstanceName)
            return "Clone name must be different from source";

        // Check for invalid characters
        if (CloneName.Any(c => char.IsWhiteSpace(c)))
            return "Clone name cannot contain whitespace";

        if (!CloneName.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-'))
            return "Clone name can only contain letters, digits, dots, underscores, and hyphens";

        return null;
    }

    private string? ValidateReferences()
    {
        // References step is always valid (0 selections is ok)
        return null;
    }

    private string? ValidateAssets()
    {
        // Check if any custom asset replacements are missing paths
        foreach (var dep in AssetDependencies)
        {
            if (dep.Strategy == AssetCloneStrategy.ReplaceWithCustom &&
                string.IsNullOrEmpty(dep.CustomAssetPath))
            {
                return $"Custom asset path required for {dep.FieldName}";
            }
        }
        return null;
    }
}

/// <summary>
/// Result from completing the cloning wizard.
/// Contains all the data needed to create the clone and its patches.
/// </summary>
public class CloneWizardResult
{
    /// <summary>Name of the new clone</summary>
    public required string CloneName { get; init; }

    /// <summary>Source template type</summary>
    public required string SourceTemplateType { get; init; }

    /// <summary>Source instance name</summary>
    public required string SourceInstanceName { get; init; }

    /// <summary>Target modpack name</summary>
    public required string ModpackName { get; init; }

    /// <summary>Whether to copy all properties from source</summary>
    public bool CopyAllProperties { get; init; } = true;

    /// <summary>
    /// Patches to apply, organized by template type -> instance name -> patch object.
    /// </summary>
    public Dictionary<string, Dictionary<string, JsonObject>> Patches { get; init; } = new();

    /// <summary>
    /// Asset files to copy, source path -> destination path.
    /// </summary>
    public Dictionary<string, string> AssetsToCopy { get; init; } = new();

    /// <summary>
    /// Asset patches (template patches that update asset references).
    /// Organized same as Patches.
    /// </summary>
    public Dictionary<string, Dictionary<string, JsonObject>> AssetPatches { get; init; } = new();
}
