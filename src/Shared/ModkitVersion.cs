// AUTO-GENERATED from third_party/versions.json - DO NOT EDIT MANUALLY
// Run build-redistributables.sh to regenerate

namespace Menace;

/// <summary>
/// Centralized version information for the Menace Modkit ecosystem.
/// This file is generated from versions.json and linked into both
/// the desktop app and the in-game loader.
/// </summary>
public static class ModkitVersion
{
    /// <summary>
    /// The current build number. Derived from ModpackLoader version in versions.json.
    /// </summary>
    public const int BuildNumber = 19;

    /// <summary>
    /// Version string for MelonLoader attribute (must be compile-time constant).
    /// </summary>
    public const string MelonVersion = "19.0.5";

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
}
