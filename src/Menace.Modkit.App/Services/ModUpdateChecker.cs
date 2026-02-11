using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Result of checking a mod for updates.
/// </summary>
public record ModUpdateInfo
{
    public string ModName { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public bool HasUpdate { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Checks modpacks for available updates from their configured repositories.
/// Currently supports GitHub; other platforms can be added via the adapter pattern.
/// </summary>
public class ModUpdateChecker : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(12);
    private readonly Dictionary<string, (ModUpdateInfo info, DateTime checkedAt)> _cache = new();
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MenaceModkit", "update_cache.json");

    public ModUpdateChecker()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MenaceModkit", ModkitVersion.Short));
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Check a single modpack for updates.
    /// </summary>
    public async Task<ModUpdateInfo> CheckForUpdateAsync(
        ModpackManifest manifest,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(manifest.RepositoryUrl))
        {
            return new ModUpdateInfo
            {
                ModName = manifest.Name,
                CurrentVersion = manifest.Version,
                LatestVersion = manifest.Version,
                HasUpdate = false,
                Error = "No repository configured"
            };
        }

        // Check cache
        var cacheKey = $"{manifest.Name}:{manifest.RepositoryUrl}";
        if (_cache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.checkedAt < _cacheDuration)
        {
            return cached.info;
        }

        // Determine repository type (auto-detect if not specified)
        var repoType = manifest.RepositoryType;
        if (repoType == RepositoryType.None)
        {
            repoType = DetectRepositoryType(manifest.RepositoryUrl);
        }

        ModUpdateInfo result;
        try
        {
            result = repoType switch
            {
                RepositoryType.GitHub => await CheckGitHubAsync(manifest, cancellationToken),
                // TODO: Add adapters for other platforms
                // RepositoryType.Nexus => await CheckNexusAsync(manifest, cancellationToken),
                // RepositoryType.GameBanana => await CheckGameBananaAsync(manifest, cancellationToken),
                _ => new ModUpdateInfo
                {
                    ModName = manifest.Name,
                    CurrentVersion = manifest.Version,
                    LatestVersion = manifest.Version,
                    HasUpdate = false,
                    Error = $"Unsupported repository type: {repoType}"
                }
            };
        }
        catch (Exception ex)
        {
            result = new ModUpdateInfo
            {
                ModName = manifest.Name,
                CurrentVersion = manifest.Version,
                LatestVersion = manifest.Version,
                HasUpdate = false,
                Error = ex.Message
            };
        }

        // Cache result
        _cache[cacheKey] = (result, DateTime.UtcNow);
        return result;
    }

    /// <summary>
    /// Check multiple modpacks for updates in parallel.
    /// </summary>
    public async Task<List<ModUpdateInfo>> CheckAllForUpdatesAsync(
        IEnumerable<ModpackManifest> manifests,
        CancellationToken cancellationToken = default)
    {
        var tasks = manifests
            .Where(m => !string.IsNullOrEmpty(m.RepositoryUrl))
            .Select(m => CheckForUpdateAsync(m, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Get only modpacks that have updates available.
    /// </summary>
    public async Task<List<ModUpdateInfo>> GetAvailableUpdatesAsync(
        IEnumerable<ModpackManifest> manifests,
        CancellationToken cancellationToken = default)
    {
        var all = await CheckAllForUpdatesAsync(manifests, cancellationToken);
        return all.Where(u => u.HasUpdate).ToList();
    }

    /// <summary>
    /// Clear the update cache, forcing fresh checks.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        try { File.Delete(CacheFilePath); } catch { }
    }

    /// <summary>
    /// Load cached update info from disk.
    /// </summary>
    public void LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return;

            var json = File.ReadAllText(CacheFilePath);
            var entries = JsonSerializer.Deserialize<List<CacheEntry>>(json);
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (DateTime.UtcNow - entry.CheckedAt < _cacheDuration)
                {
                    _cache[entry.Key] = (entry.Info, entry.CheckedAt);
                }
            }
        }
        catch
        {
            // Ignore cache load failures
        }
    }

    /// <summary>
    /// Save cached update info to disk.
    /// </summary>
    public void SaveCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var entries = _cache.Select(kvp => new CacheEntry
            {
                Key = kvp.Key,
                Info = kvp.Value.info,
                CheckedAt = kvp.Value.checkedAt
            }).ToList();

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CacheFilePath, json);
        }
        catch
        {
            // Ignore cache save failures
        }
    }

    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public ModUpdateInfo Info { get; set; } = new();
        public DateTime CheckedAt { get; set; }
    }

    // ---------------------------------------------------------------
    // GitHub Adapter
    // ---------------------------------------------------------------

    private static readonly Regex GitHubUrlRegex = new(
        @"github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private async Task<ModUpdateInfo> CheckGitHubAsync(
        ModpackManifest manifest,
        CancellationToken cancellationToken)
    {
        var match = GitHubUrlRegex.Match(manifest.RepositoryUrl!);
        if (!match.Success)
        {
            return new ModUpdateInfo
            {
                ModName = manifest.Name,
                CurrentVersion = manifest.Version,
                LatestVersion = manifest.Version,
                HasUpdate = false,
                Error = "Invalid GitHub URL format"
            };
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value.TrimEnd('/').Replace(".git", "");

        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = response.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => "Repository or release not found",
                System.Net.HttpStatusCode.Forbidden => "Rate limited - try again later",
                _ => $"GitHub API error: {response.StatusCode}"
            };

            return new ModUpdateInfo
            {
                ModName = manifest.Name,
                CurrentVersion = manifest.Version,
                LatestVersion = manifest.Version,
                HasUpdate = false,
                Error = errorMsg
            };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        if (release == null)
        {
            return new ModUpdateInfo
            {
                ModName = manifest.Name,
                CurrentVersion = manifest.Version,
                LatestVersion = manifest.Version,
                HasUpdate = false,
                Error = "Failed to parse GitHub response"
            };
        }

        var latestVersion = NormalizeVersion(release.TagName);
        var currentVersion = NormalizeVersion(manifest.Version);
        var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;

        // Find the best download URL (prefer .zip modpack, fall back to source)
        string? downloadUrl = null;
        if (release.Assets != null)
        {
            var modpackAsset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                !a.Name.Contains("source", StringComparison.OrdinalIgnoreCase));

            downloadUrl = modpackAsset?.BrowserDownloadUrl ?? release.ZipballUrl;
        }
        else
        {
            downloadUrl = release.ZipballUrl;
        }

        return new ModUpdateInfo
        {
            ModName = manifest.Name,
            CurrentVersion = manifest.Version,
            LatestVersion = release.TagName,
            HasUpdate = hasUpdate,
            DownloadUrl = downloadUrl,
            ReleaseNotes = release.Body,
            PublishedAt = release.PublishedAt
        };
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static RepositoryType DetectRepositoryType(string url)
    {
        if (string.IsNullOrEmpty(url))
            return RepositoryType.None;

        var uri = url.ToLowerInvariant();

        if (uri.Contains("github.com"))
            return RepositoryType.GitHub;

        // TODO: Add detection for other platforms
        // if (uri.Contains("nexusmods.com"))
        //     return RepositoryType.Nexus;
        // if (uri.Contains("gamebanana.com"))
        //     return RepositoryType.GameBanana;
        // if (uri.Contains("moddb.com"))
        //     return RepositoryType.ModDB;

        return RepositoryType.None;
    }

    /// <summary>
    /// Normalize version string by removing 'v' prefix and trimming.
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return "0.0.0";

        version = version.Trim();
        if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            version = version[1..];

        return version;
    }

    /// <summary>
    /// Compare two semver-ish version strings.
    /// Returns: positive if a > b, negative if a < b, 0 if equal.
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.', '-', '+');
        var partsB = b.Split('.', '-', '+');

        var maxParts = Math.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < maxParts; i++)
        {
            var partA = i < partsA.Length ? partsA[i] : "0";
            var partB = i < partsB.Length ? partsB[i] : "0";

            // Try numeric comparison first
            if (int.TryParse(partA, out var numA) && int.TryParse(partB, out var numB))
            {
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            else
            {
                // Fall back to string comparison
                var cmp = string.Compare(partA, partB, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;
            }
        }

        return 0;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // ---------------------------------------------------------------
    // GitHub API Models
    // ---------------------------------------------------------------

    private class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string? Body { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("zipball_url")]
        public string? ZipballUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
