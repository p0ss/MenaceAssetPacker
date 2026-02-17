using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Persists state between extraction trigger and mod redeploy.
/// Saved to {GamePath}/UserData/ExtractedData/_pending_redeploy.json
/// </summary>
public class PendingRedeployState
{
    /// <summary>
    /// Names of modpacks that were deployed when extraction was triggered.
    /// </summary>
    public List<string> DeployedModpacks { get; set; } = new();

    /// <summary>
    /// Timestamp when mods were undeployed (UTC).
    /// Used to compare against extraction fingerprint timestamp.
    /// </summary>
    public DateTime UndeployTimestamp { get; set; }

    /// <summary>
    /// Flag indicating redeploy is still pending.
    /// </summary>
    public bool RedeployPending { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Get the path to the pending redeploy state file for a given game install path.
    /// </summary>
    public static string GetFilePath(string gameInstallPath)
    {
        return Path.Combine(gameInstallPath, "UserData", "ExtractedData", "_pending_redeploy.json");
    }

    /// <summary>
    /// Load pending redeploy state from disk.
    /// Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static PendingRedeployState? LoadFrom(string gameInstallPath)
    {
        var filePath = GetFilePath(gameInstallPath);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<PendingRedeployState>(json, ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save pending redeploy state to disk.
    /// </summary>
    public void SaveTo(string gameInstallPath)
    {
        var filePath = GetFilePath(gameInstallPath);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>
    /// Delete pending redeploy state from disk.
    /// </summary>
    public static void Delete(string gameInstallPath)
    {
        var filePath = GetFilePath(gameInstallPath);
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
        }
    }

    /// <summary>
    /// Check if extraction has completed by comparing fingerprint timestamp to undeploy timestamp.
    /// </summary>
    public bool IsExtractionComplete(string gameInstallPath)
    {
        var fingerprintPath = Path.Combine(gameInstallPath, "UserData", "ExtractedData", "_extraction_fingerprint.txt");
        if (!File.Exists(fingerprintPath))
            return false;

        try
        {
            var fingerprintTime = File.GetLastWriteTimeUtc(fingerprintPath);
            return fingerprintTime > UndeployTimestamp;
        }
        catch
        {
            return false;
        }
    }
}
