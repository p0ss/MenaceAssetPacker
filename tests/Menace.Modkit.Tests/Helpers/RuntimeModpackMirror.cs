using System.Collections.Generic;
using Newtonsoft.Json;

namespace Menace.Modkit.Tests.Helpers;

/// <summary>
/// Mirror of the runtime ModpackLoader's Modpack class.
/// Uses Newtonsoft.Json attributes to match the runtime's deserialization contract.
/// This allows tests to verify that JSON produced by the App's deploy pipeline
/// can be correctly deserialized by the runtime without referencing the net6.0 ModpackLoader project.
/// </summary>
public class RuntimeModpackMirror
{
    [JsonProperty("manifestVersion")]
    public int ManifestVersion { get; set; } = 1;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("loadOrder")]
    public int LoadOrder { get; set; } = 100;

    /// <summary>
    /// V1 format: templates → instanceName → field → value
    /// </summary>
    [JsonProperty("templates")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>>? Templates { get; set; }

    /// <summary>
    /// V2 format: patches → instanceName → field → value
    /// </summary>
    [JsonProperty("patches")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>>? Patches { get; set; }

    [JsonProperty("assets")]
    public Dictionary<string, string>? Assets { get; set; }

    [JsonProperty("bundles")]
    public List<string>? Bundles { get; set; }

    [JsonProperty("code")]
    public RuntimeCodeManifest? Code { get; set; }

    [JsonProperty("securityStatus")]
    public string? SecurityStatus { get; set; }

    public bool HasCode => Code?.PrebuiltDlls?.Count > 0 || Code?.Sources?.Count > 0;
}

public class RuntimeCodeManifest
{
    [JsonProperty("sources")]
    public List<string>? Sources { get; set; }

    [JsonProperty("references")]
    public List<string>? References { get; set; }

    [JsonProperty("prebuiltDlls")]
    public List<string>? PrebuiltDlls { get; set; }
}
