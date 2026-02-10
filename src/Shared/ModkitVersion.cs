// This file is shared between Menace.Modkit.App and Menace.ModpackLoader
// Update version here once - both projects will use it

namespace Menace;

/// <summary>
/// Centralized version information for the Menace Modkit ecosystem.
/// This file is linked into both the desktop app and the in-game loader.
/// </summary>
public static class ModkitVersion
{
    /// <summary>
    /// The current build number. Increment this for each shared release.
    /// </summary>
    public const int BuildNumber = 19;

    /// <summary>
    /// Version string for MelonLoader attribute (must be compile-time constant).
    /// Format: "build.0.0" for compatibility with semver parsers.
    /// </summary>
    public const string MelonVersion = "19.0.0";

    /// <summary>
    /// Short display version (e.g., "v19").
    /// </summary>
    public const string Short = "v19";

    /// <summary>
    /// Full version for the Modkit App.
    /// </summary>
    public const string AppFull = "Menace Modkit v19";

    /// <summary>
    /// Full version for the Modpack Loader.
    /// </summary>
    public const string LoaderFull = "Menace Modpack Loader v19";

    /// <summary>
    /// Tools bundle version. Increment when AssetRipper is updated.
    /// This triggers re-download of cached tools.
    /// </summary>
    public const int ToolsVersion = 2;
}
