using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the Cloning Wizard dialog.
/// Manages wizard state, step navigation, and patch generation.
/// </summary>
public class CloningWizardViewModel : INotifyPropertyChanged
{
    private readonly CloneWizardState _state;
    private readonly ReferenceGraphService _referenceGraph;
    private readonly SchemaService _schemaService;
    private readonly PatchGenerationService _patchService;
    private readonly string _vanillaDataPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CloningWizardViewModel(
        string sourceTemplateType,
        string sourceInstanceName,
        string modpackName,
        ReferenceGraphService referenceGraph,
        SchemaService schemaService,
        string vanillaDataPath)
    {
        _referenceGraph = referenceGraph;
        _schemaService = schemaService;
        _vanillaDataPath = vanillaDataPath;
        _patchService = new PatchGenerationService(schemaService, vanillaDataPath);

        _state = new CloneWizardState
        {
            SourceTemplateType = sourceTemplateType,
            SourceInstanceName = sourceInstanceName,
            ModpackName = modpackName,
            CloneName = GenerateSuggestedName(sourceInstanceName)
        };

        References = new ObservableCollection<ReferenceSelection>();
        AssetDependencies = new ObservableCollection<AssetDependency>();

        // Load references upfront
        LoadReferences();
        LoadAssetDependencies();
    }

    // --- Properties for Data Binding ---

    public CloneWizardStep CurrentStep
    {
        get => _state.CurrentStep;
        set
        {
            if (_state.CurrentStep != value)
            {
                _state.CurrentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepDescription));
                OnPropertyChanged(nameof(IsNameStep));
                OnPropertyChanged(nameof(IsReferencesStep));
                OnPropertyChanged(nameof(IsAssetsStep));
                OnPropertyChanged(nameof(IsReviewStep));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(NextButtonText));
            }
        }
    }

    public string CloneName
    {
        get => _state.CloneName;
        set
        {
            if (_state.CloneName != value)
            {
                _state.CloneName = value;
                OnPropertyChanged();
                _state.Validate();
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool CopyAllProperties
    {
        get => _state.CopyAllProperties;
        set
        {
            if (_state.CopyAllProperties != value)
            {
                _state.CopyAllProperties = value;
                OnPropertyChanged();
            }
        }
    }

    public CloneInjectionStrategy GlobalStrategy
    {
        get => _state.GlobalStrategy;
        set
        {
            if (_state.GlobalStrategy != value)
            {
                _state.GlobalStrategy = value;
                // Apply to all selected references
                foreach (var r in References)
                {
                    if (r.IsSelected)
                        r.Strategy = value;
                }
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<ReferenceSelection> References { get; }
    public ObservableCollection<AssetDependency> AssetDependencies { get; }

    public string SourceKey => _state.SourceKey;
    public string SourceInstanceName => _state.SourceInstanceName;
    public string SourceTemplateType => _state.SourceTemplateType;
    public string ModpackName => _state.ModpackName;

    public string? ValidationError => _state.ValidationError;

    // --- Step Display Properties ---

    public string StepTitle => CurrentStep switch
    {
        CloneWizardStep.Name => "Step 1: Clone Name",
        CloneWizardStep.References => "Step 2: Reference Injection",
        CloneWizardStep.Assets => "Step 3: Asset Dependencies",
        CloneWizardStep.Review => "Step 4: Review & Generate",
        _ => "Clone Wizard"
    };

    public string StepDescription => CurrentStep switch
    {
        CloneWizardStep.Name => "Choose a name for your cloned template.",
        CloneWizardStep.References => "Select which collections should include your clone.",
        CloneWizardStep.Assets => "Choose how to handle asset dependencies.",
        CloneWizardStep.Review => "Review the changes that will be made.",
        _ => ""
    };

    public bool IsNameStep => CurrentStep == CloneWizardStep.Name;
    public bool IsReferencesStep => CurrentStep == CloneWizardStep.References;
    public bool IsAssetsStep => CurrentStep == CloneWizardStep.Assets;
    public bool IsReviewStep => CurrentStep == CloneWizardStep.Review;

    public bool CanGoBack => CurrentStep != CloneWizardStep.Name;

    public bool CanGoNext
    {
        get
        {
            _state.Validate();
            return _state.CanProceed;
        }
    }

    public string NextButtonText => CurrentStep switch
    {
        CloneWizardStep.Review => "Generate Clone & Patches",
        _ => "Next"
    };

    public bool HasReferences => References.Count > 0;
    public bool HasAssetDependencies => AssetDependencies.Count > 0;

    // --- Generated Patches Preview ---

    public string GeneratedPatchesPreview
    {
        get
        {
            if (!IsReviewStep)
                return "";

            GeneratePatches();
            return FormatPatchesPreview();
        }
    }

    public int SelectedReferenceCount => References.Count(r => r.IsSelected);

    // --- Navigation Methods ---

    public void GoBack()
    {
        if (!CanGoBack) return;

        CurrentStep = CurrentStep switch
        {
            CloneWizardStep.References => CloneWizardStep.Name,
            CloneWizardStep.Assets => CloneWizardStep.References,
            CloneWizardStep.Review => CloneWizardStep.Assets,
            _ => CurrentStep
        };
    }

    public void GoNext()
    {
        if (!CanGoNext) return;

        CurrentStep = CurrentStep switch
        {
            CloneWizardStep.Name => CloneWizardStep.References,
            CloneWizardStep.References => CloneWizardStep.Assets,
            CloneWizardStep.Assets => CloneWizardStep.Review,
            _ => CurrentStep
        };

        // Refresh generated patches when entering review
        if (CurrentStep == CloneWizardStep.Review)
        {
            OnPropertyChanged(nameof(GeneratedPatchesPreview));
        }
    }

    // --- Data Loading ---

    private void LoadReferences()
    {
        // Try to get actual backlinks from extracted data
        // Use GetEnhancedBacklinks to include all reference types (Direct, CollectionDirect, CollectionEmbedded)
        var backlinks = _referenceGraph.GetEnhancedBacklinks(
            _state.SourceTemplateType,
            _state.SourceInstanceName);

        foreach (var backlink in backlinks)
        {
            References.Add(new ReferenceSelection
            {
                Reference = backlink,
                IsSelected = true,
                Strategy = CloneInjectionStrategy.AddAlongside
            });
        }

        // If no backlinks found, check schema-based hints
        if (References.Count == 0)
        {
            var hints = _referenceGraph.GetSchemaRelationshipHints(_state.SourceTemplateType);
            foreach (var hint in hints)
            {
                // Create a synthetic reference for the wizard to display
                // This represents a potential reference based on schema, not actual data
                var syntheticRef = new EnhancedReferenceEntry
                {
                    SourceTemplateType = hint.SourceTemplateType,
                    SourceInstanceName = "(schema hint - select instance)",
                    FieldName = hint.Path,
                    Type = ReferenceType.CollectionEmbedded,
                    EmbeddedClassName = null,
                    EmbeddedFieldName = hint.Path.Contains('.') ? hint.Path.Split('.').Last() : hint.Path,
                    CollectionIndex = -1,
                    ReferencedValue = _state.SourceInstanceName
                };

                References.Add(new ReferenceSelection
                {
                    Reference = syntheticRef,
                    IsSelected = false, // Default to not selected since we don't have instance info
                    Strategy = CloneInjectionStrategy.AddAlongside
                });
            }

            // If we added schema hints, show a warning
            if (References.Count > 0)
            {
                _dataIncomplete = true;
                OnPropertyChanged(nameof(DataIncompleteWarning));
            }
        }

        OnPropertyChanged(nameof(HasReferences));
        OnPropertyChanged(nameof(SelectedReferenceCount));
    }

    private bool _dataIncomplete;

    public string? DataIncompleteWarning => _dataIncomplete
        ? "Extracted data is incomplete. Showing schema-based relationship hints. " +
          "Re-run data extraction in-game (F11) to get actual instance references."
        : null;

    private void LoadAssetDependencies()
    {
        // Load the source template to find asset references
        var templatePath = Path.Combine(_vanillaDataPath, $"{_state.SourceTemplateType}.json");
        if (!File.Exists(templatePath))
            return;

        try
        {
            var json = File.ReadAllText(templatePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return;

            // Find the source template
            foreach (var template in doc.RootElement.EnumerateArray())
            {
                if (template.ValueKind != JsonValueKind.Object)
                    continue;

                if (template.TryGetProperty("name", out var nameProp) &&
                    nameProp.GetString() == _state.SourceInstanceName)
                {
                    // Scan fields for asset references using SchemaService's GetAssetFields
                    var assetFields = _schemaService.GetAssetFields(_state.SourceTemplateType);

                    foreach (var field in assetFields)
                    {
                        if (template.TryGetProperty(field.Name, out var assetValue) &&
                            assetValue.ValueKind == JsonValueKind.String)
                        {
                            var assetName = assetValue.GetString();
                            if (!string.IsNullOrEmpty(assetName) &&
                                !assetName.StartsWith("(") &&
                                !assetName.EndsWith(")"))
                            {
                                var category = InferAssetCategory(field.Type);
                                AssetDependencies.Add(new AssetDependency
                                {
                                    FieldName = field.Name,
                                    Category = category,
                                    OriginalAsset = assetName,
                                    Strategy = category switch
                                    {
                                        "sprite" or "texture" or "audio" => AssetCloneStrategy.KeepOriginal,
                                        _ => AssetCloneStrategy.KeepOriginal
                                    },
                                    NewAssetName = GenerateClonedAssetName(assetName, _state.SuggestedCloneName)
                                });
                            }
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[CloningWizardViewModel] Failed to load asset dependencies: {ex.Message}");
        }

        OnPropertyChanged(nameof(HasAssetDependencies));
    }

    private static string InferAssetCategory(string fieldType)
    {
        var lower = fieldType.ToLowerInvariant();
        if (lower.Contains("sprite")) return "sprite";
        if (lower.Contains("texture")) return "texture";
        if (lower.Contains("mesh") || lower.Contains("model") || lower.Contains("skinned")) return "mesh";
        if (lower.Contains("prefab") || lower.Contains("gameobject")) return "prefab";
        if (lower.Contains("audio") || lower.Contains("sound")) return "audio";
        if (lower.Contains("material")) return "material";
        return "asset";
    }

    private static string GenerateSuggestedName(string sourceName)
    {
        var lastDot = sourceName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            var prefix = sourceName[..lastDot];
            var suffix = sourceName[(lastDot + 1)..];
            return $"{prefix}.{suffix}_clone";
        }
        return $"{sourceName}_clone";
    }

    private static string GenerateClonedAssetName(string originalAsset, string cloneSuffix)
    {
        var ext = Path.GetExtension(originalAsset);
        var baseName = Path.GetFileNameWithoutExtension(originalAsset);

        // Extract suffix from clone name (e.g., "enemy.captain_elite" -> "_elite")
        var underscorePos = cloneSuffix.LastIndexOf('_');
        var suffix = underscorePos >= 0 ? cloneSuffix[underscorePos..] : "_clone";

        return $"{baseName}{suffix}{ext}";
    }

    // --- Patch Generation ---

    private void GeneratePatches()
    {
        _state.GeneratedPatches.Clear();

        foreach (var refSel in References.Where(r => r.IsSelected))
        {
            JsonObject? patch = refSel.Strategy switch
            {
                CloneInjectionStrategy.AddAlongside =>
                    _patchService.GenerateAppendPatch(refSel.Reference, CloneName),
                CloneInjectionStrategy.ReplaceSource =>
                    _patchService.GenerateReplacePatch(refSel.Reference, CloneName),
                _ => null
            };

            if (patch == null)
                continue;

            var templateType = refSel.Reference.SourceTemplateType;
            var instanceName = refSel.Reference.SourceInstanceName;

            if (!_state.GeneratedPatches.TryGetValue(templateType, out var typePatches))
            {
                typePatches = new Dictionary<string, JsonObject>();
                _state.GeneratedPatches[templateType] = typePatches;
            }

            if (!typePatches.TryGetValue(instanceName, out var instancePatch))
            {
                typePatches[instanceName] = patch;
            }
            else
            {
                // Merge patches for the same instance
                typePatches[instanceName] = PatchGenerationService.MergePatches(
                    new[] { instancePatch, patch });
            }
        }
    }

    private string FormatPatchesPreview()
    {
        var lines = new List<string>();

        lines.Add($"CLONE DEFINITION:");
        lines.Add($"  {_state.SourceTemplateType}/{CloneName} from {_state.SourceInstanceName}");
        lines.Add("");

        if (_state.GeneratedPatches.Count > 0)
        {
            lines.Add("PATCHES TO GENERATE:");
            foreach (var (templateType, instances) in _state.GeneratedPatches)
            {
                foreach (var (instanceName, patch) in instances)
                {
                    lines.Add($"  {templateType}/{instanceName}:");
                    var patchJson = patch.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    foreach (var line in patchJson.Split('\n').Take(10))
                    {
                        lines.Add($"    {line}");
                    }
                    if (patchJson.Split('\n').Length > 10)
                        lines.Add("    ...");
                }
            }
        }
        else
        {
            lines.Add("No patches to generate (no references selected).");
        }

        if (AssetDependencies.Any(a => a.Strategy != AssetCloneStrategy.KeepOriginal))
        {
            lines.Add("");
            lines.Add("ASSET CHANGES:");
            foreach (var dep in AssetDependencies.Where(a => a.Strategy != AssetCloneStrategy.KeepOriginal))
            {
                var action = dep.Strategy switch
                {
                    AssetCloneStrategy.CloneAsset => $"Copy {dep.OriginalAsset} -> {dep.NewAssetName}",
                    AssetCloneStrategy.ReplaceWithCustom => $"Use custom: {dep.CustomAssetPath}",
                    _ => "Keep original"
                };
                lines.Add($"  {dep.FieldName}: {action}");
            }
        }

        return string.Join("\n", lines);
    }

    // --- Result Generation ---

    /// <summary>
    /// Generate the final result from the wizard.
    /// </summary>
    public CloneWizardResult GenerateResult()
    {
        GeneratePatches();

        var result = new CloneWizardResult
        {
            CloneName = CloneName,
            SourceTemplateType = _state.SourceTemplateType,
            SourceInstanceName = _state.SourceInstanceName,
            ModpackName = _state.ModpackName,
            CopyAllProperties = CopyAllProperties,
            Patches = _state.GeneratedPatches
        };

        // Add asset copies
        foreach (var dep in AssetDependencies)
        {
            if (dep.Strategy == AssetCloneStrategy.CloneAsset && !string.IsNullOrEmpty(dep.NewAssetName))
            {
                result.AssetsToCopy[dep.OriginalAsset] = dep.NewAssetName;
            }
            else if (dep.Strategy == AssetCloneStrategy.ReplaceWithCustom && !string.IsNullOrEmpty(dep.CustomAssetPath))
            {
                // Generate proper destination path with category-based subdirectory
                var extension = Path.GetExtension(dep.CustomAssetPath);
                var fileName = Path.GetFileNameWithoutExtension(dep.CustomAssetPath);
                var categoryFolder = GetCategoryFolder(dep.Category);
                var destPath = Path.Combine("Assets", categoryFolder, $"{fileName}{extension}");

                result.AssetsToCopy[dep.CustomAssetPath] = destPath;

                // Also record that the clone's field should reference this new asset
                // (stored in AssetPatches for the caller to apply)
                AddAssetFieldPatch(result, dep.FieldName, fileName);
            }
        }

        return result;
    }

    /// <summary>
    /// Get the asset folder name for a category.
    /// </summary>
    private static string GetCategoryFolder(string category) => category switch
    {
        "sprite" => "Sprite",
        "texture" => "Texture2D",
        "mesh" => "Mesh",
        "audio" => "AudioClip",
        "material" => "Material",
        "prefab" => "Prefab",
        _ => "Other"
    };

    /// <summary>
    /// Add a patch to update the clone's asset field reference.
    /// </summary>
    private void AddAssetFieldPatch(CloneWizardResult result, string fieldName, string newAssetName)
    {
        // Create a patch for the clone itself to update its asset reference
        if (!result.AssetPatches.TryGetValue(_state.SourceTemplateType, out var typePatches))
        {
            typePatches = new Dictionary<string, JsonObject>();
            result.AssetPatches[_state.SourceTemplateType] = typePatches;
        }

        if (!typePatches.TryGetValue(CloneName, out var clonePatch))
        {
            clonePatch = new JsonObject();
            typePatches[CloneName] = clonePatch;
        }

        clonePatch[fieldName] = newAssetName;
    }

    // --- Reference Selection Helpers ---

    public void SelectAllReferences()
    {
        foreach (var r in References)
            r.IsSelected = true;
        OnPropertyChanged(nameof(SelectedReferenceCount));
    }

    public void DeselectAllReferences()
    {
        foreach (var r in References)
            r.IsSelected = false;
        OnPropertyChanged(nameof(SelectedReferenceCount));
    }

    public void OnReferenceSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedReferenceCount));
    }

    // --- INotifyPropertyChanged ---

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
