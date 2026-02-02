using System.Collections.Generic;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Code section of a modpack manifest: source files, assembly references, and prebuilt DLLs.
/// </summary>
public class CodeManifest
{
    /// <summary>
    /// Relative paths to .cs source files (e.g. "src/CombinedArmsMod.cs")
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>
    /// Assembly references needed for compilation (e.g. "MelonLoader", "Il2CppInterop.Runtime")
    /// </summary>
    public List<string> References { get; set; } = new();

    /// <summary>
    /// Relative paths to prebuilt DLL files included with the modpack
    /// </summary>
    public List<string> PrebuiltDlls { get; set; } = new();

    public bool HasAnySources => Sources.Count > 0;
    public bool HasAnyPrebuilt => PrebuiltDlls.Count > 0;
    public bool HasAnyCode => HasAnySources || HasAnyPrebuilt;
}
