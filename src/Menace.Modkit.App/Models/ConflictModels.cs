using System.Collections.Generic;

namespace Menace.Modkit.App.Models;

/// <summary>
/// A field-level conflict: two or more modpacks change the same field on the same instance.
/// </summary>
public class FieldConflict
{
    public string TemplateType { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The modpacks that conflict, in load order. Last one wins by default.
    /// </summary>
    public List<ConflictingMod> ConflictingMods { get; set; } = new();

    /// <summary>
    /// The modpack that wins (last in load order, unless overridden).
    /// </summary>
    public string Winner { get; set; } = string.Empty;

    public string DisplayKey => $"{TemplateType}/{InstanceName}.{FieldName}";
}

/// <summary>
/// One side of a conflict: which modpack sets which value.
/// </summary>
public class ConflictingMod
{
    public string ModpackName { get; set; } = string.Empty;
    public int LoadOrder { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Two modpacks ship a DLL with the same assembly name.
/// </summary>
public class DllConflict
{
    public string AssemblyName { get; set; } = string.Empty;
    public List<string> ModpackNames { get; set; } = new();
}

/// <summary>
/// A dependency is missing or the available version doesn't satisfy the constraint.
/// </summary>
public class DependencyIssue
{
    public string ModpackName { get; set; } = string.Empty;
    public string RequiredDependency { get; set; } = string.Empty;
    public DependencyIssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
}

public enum DependencyIssueSeverity
{
    Missing,
    IncompatibleVersion,
    Warning
}

/// <summary>
/// User-defined override for a specific conflict: choose a specific modpack as winner.
/// </summary>
public class ConflictOverride
{
    public string TemplateType { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string WinnerModpack { get; set; } = string.Empty;
}
