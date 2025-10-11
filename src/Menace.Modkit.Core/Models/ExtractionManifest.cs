using System;
using System.Collections.Generic;

namespace Menace.Modkit.Core.Models;

/// <summary>
/// Tracks extraction state and game version to avoid redundant operations
/// </summary>
public class ExtractionManifest
{
    /// <summary>
    /// Unity version of the game
    /// </summary>
    public string UnityVersion { get; set; } = string.Empty;

    /// <summary>
    /// Hash of GameAssembly.so/dll (main IL2CPP binary)
    /// </summary>
    public string GameAssemblyHash { get; set; } = string.Empty;

    /// <summary>
    /// Hash of global-metadata.dat
    /// </summary>
    public string MetadataHash { get; set; } = string.Empty;

    /// <summary>
    /// When IL2CPP dump was last generated
    /// </summary>
    public DateTime? IL2CppDumpTimestamp { get; set; }

    /// <summary>
    /// When template extraction code was last generated
    /// </summary>
    public DateTime? TemplateCodeGenerationTimestamp { get; set; }

    /// <summary>
    /// When game data was last extracted
    /// </summary>
    public DateTime? DataExtractionTimestamp { get; set; }

    /// <summary>
    /// When assets were last ripped
    /// </summary>
    public DateTime? AssetRipTimestamp { get; set; }

    /// <summary>
    /// List of template types that were successfully extracted
    /// </summary>
    public List<string> ExtractedTemplates { get; set; } = new();

    /// <summary>
    /// Path to the minimal IL2CPP dump (templates only)
    /// </summary>
    public string? MinimalDumpPath { get; set; }

    /// <summary>
    /// Path to the full IL2CPP dump (if exists)
    /// </summary>
    public string? FullDumpPath { get; set; }

    /// <summary>
    /// Hash of the DataExtractor mod DLL
    /// </summary>
    public string? DataExtractorHash { get; set; }

    /// <summary>
    /// Game executable path for verification
    /// </summary>
    public string? GameExecutablePath { get; set; }

    /// <summary>
    /// Check if IL2CPP dump needs regeneration
    /// </summary>
    public bool NeedsIL2CppDumpUpdate(string currentGameAssemblyHash, string currentMetadataHash)
    {
        return GameAssemblyHash != currentGameAssemblyHash ||
               MetadataHash != currentMetadataHash ||
               IL2CppDumpTimestamp == null;
    }

    /// <summary>
    /// Check if template extraction code needs regeneration
    /// </summary>
    public bool NeedsTemplateCodeUpdate(string currentGameAssemblyHash, string currentMetadataHash)
    {
        return NeedsIL2CppDumpUpdate(currentGameAssemblyHash, currentMetadataHash) ||
               TemplateCodeGenerationTimestamp == null;
    }

    /// <summary>
    /// Check if data extraction needs to run
    /// </summary>
    public bool NeedsDataExtraction(string currentGameAssemblyHash, string? currentDataExtractorHash)
    {
        return GameAssemblyHash != currentGameAssemblyHash ||
               DataExtractorHash != currentDataExtractorHash ||
               DataExtractionTimestamp == null;
    }

    /// <summary>
    /// Check if asset ripping needs to run
    /// </summary>
    public bool NeedsAssetRip(string currentGameAssemblyHash, string? currentProfileHash = null)
    {
        return GameAssemblyHash != currentGameAssemblyHash ||
               AssetRipProfileHash != currentProfileHash ||
               AssetRipTimestamp == null;
    }

    /// <summary>
    /// Hash of the asset ripper profile (settings for what to rip)
    /// </summary>
    public string? AssetRipProfileHash { get; set; }

    /// <summary>
    /// Path to cached extracted data
    /// </summary>
    public string? CachedDataPath { get; set; }

    /// <summary>
    /// Path to cached ripped assets
    /// </summary>
    public string? CachedAssetsPath { get; set; }
}
