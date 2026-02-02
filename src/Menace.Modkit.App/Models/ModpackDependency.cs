using System;
using System.Text.RegularExpressions;

namespace Menace.Modkit.App.Models;

/// <summary>
/// A modpack dependency with optional version range constraint.
/// Format: "ModName" or "ModName >= 1.0" or "ModName == 2.1.0"
/// </summary>
public class ModpackDependency
{
    public string Name { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;  // "", ">=", "<=", "==", ">", "<"
    public string VersionConstraint { get; set; } = string.Empty;

    private static readonly Regex DependencyPattern = new(
        @"^\s*(?<name>[A-Za-z0-9_.\-]+)\s*(?:(?<op>>=|<=|==|>|<)\s*(?<ver>\S+))?\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parse a dependency string like "MenaceFramework >= 1.0"
    /// </summary>
    public static ModpackDependency Parse(string input)
    {
        var match = DependencyPattern.Match(input);
        if (!match.Success)
            throw new FormatException($"Invalid dependency format: '{input}'");

        return new ModpackDependency
        {
            Name = match.Groups["name"].Value,
            Operator = match.Groups["op"].Value,
            VersionConstraint = match.Groups["ver"].Value
        };
    }

    public static bool TryParse(string input, out ModpackDependency? result)
    {
        try
        {
            result = Parse(input);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Check whether a given version satisfies this dependency constraint.
    /// </summary>
    public bool IsSatisfiedBy(string version)
    {
        if (string.IsNullOrEmpty(Operator) || string.IsNullOrEmpty(VersionConstraint))
            return true; // no constraint

        if (!Version.TryParse(NormalizeVersion(version), out var ver) ||
            !Version.TryParse(NormalizeVersion(VersionConstraint), out var constraint))
            return true; // unparseable versions → assume ok

        return Operator switch
        {
            ">=" => ver >= constraint,
            "<=" => ver <= constraint,
            "==" => ver == constraint,
            ">" => ver > constraint,
            "<" => ver < constraint,
            _ => true
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Operator))
            return Name;
        return $"{Name} {Operator} {VersionConstraint}";
    }

    /// <summary>
    /// Normalize versions like "1.0" to "1.0.0" so System.Version can parse them.
    /// </summary>
    private static string NormalizeVersion(string v)
    {
        var parts = v.Split('.');
        while (parts.Length < 3)
        {
            v += ".0";
            parts = v.Split('.');
        }
        return v;
    }
}
