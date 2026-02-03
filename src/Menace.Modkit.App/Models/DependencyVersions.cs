namespace Menace.Modkit.App.Models;

public class DependencyVersions
{
    public string Modkit { get; set; } = string.Empty;
    public Dictionary<string, DependencyInfo> Dependencies { get; set; } = new();
}

public class DependencyInfo
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string BundledPath { get; set; } = string.Empty;
}
