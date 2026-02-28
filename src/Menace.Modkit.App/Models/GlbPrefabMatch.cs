namespace Menace.Modkit.App.Models;

public enum GlbPrefabMatchConfidence
{
    Verified,
    Likely,
    WeakHeuristic
}

public sealed class GlbPrefabMatch
{
    public string PrefabName { get; init; } = string.Empty;
    public string PrefabPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public GlbPrefabMatchConfidence Confidence { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public int Score { get; init; }
}
