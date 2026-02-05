using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Menace.Modkit.App.Services
{
    /// <summary>
    /// Resolves asset references (instance IDs) to human-readable names and file paths
    /// </summary>
    public class AssetReferenceResolver
    {
        private Dictionary<long, AssetReference> _references = new();
        private bool _isLoaded = false;

        public class AssetReference
        {
            public long InstanceId { get; set; }
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string AssetPath { get; set; } = "";
        }

        /// <summary>
        /// Load asset references from ExtractedData
        /// </summary>
        public void LoadReferences(string gameInstallPath)
        {
            try
            {
                var referencesPath = Path.Combine(gameInstallPath, "UserData", "ExtractedData", "AssetReferences.json");

                if (!File.Exists(referencesPath))
                {
                    ModkitLog.Info($"AssetReferences.json not found at {referencesPath}");
                    return;
                }

                var json = File.ReadAllText(referencesPath);
                var references = JsonConvert.DeserializeObject<List<AssetReference>>(json);

                if (references != null)
                {
                    _references = references
                        .GroupBy(r => r.InstanceId)
                        .ToDictionary(g => g.Key, g => g.First());
                    _isLoaded = true;
                    ModkitLog.Info($"Loaded {_references.Count} asset references");
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"Failed to load asset references: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolve a field value that might be an asset reference
        /// </summary>
        public ResolvedAsset Resolve(object value)
        {
            if (!_isLoaded)
                return new ResolvedAsset { DisplayValue = value?.ToString() ?? "null", IsReference = false };

            // Check if it's a long/int that could be an instance ID
            long instanceId = 0;
            if (value is long l)
                instanceId = l;
            else if (value is int i)
                instanceId = i;
            else if (value is string s && long.TryParse(s, out var parsed))
                instanceId = parsed;
            else
                return new ResolvedAsset { DisplayValue = value?.ToString() ?? "null", IsReference = false };

            // Try to resolve
            if (_references.TryGetValue(instanceId, out var reference))
            {
                return new ResolvedAsset
                {
                    DisplayValue = reference.Name,
                    IsReference = true,
                    AssetName = reference.Name,
                    AssetType = reference.Type,
                    AssetPath = reference.AssetPath,
                    InstanceId = instanceId,
                    HasAssetFile = !string.IsNullOrEmpty(reference.AssetPath)
                };
            }

            // Not found in reference table - treat as a regular number, not a reference
            return new ResolvedAsset
            {
                DisplayValue = instanceId.ToString(),
                IsReference = false,
                InstanceId = instanceId,
                HasAssetFile = false
            };
        }

        /// <summary>
        /// Get asset reference by instance ID
        /// </summary>
        public AssetReference? GetReference(long instanceId)
        {
            return _references.TryGetValue(instanceId, out var reference) ? reference : null;
        }

        public class ResolvedAsset
        {
            /// <summary>Display value for UI</summary>
            public string DisplayValue { get; set; } = "";

            /// <summary>Is this an asset reference?</summary>
            public bool IsReference { get; set; }

            /// <summary>Asset name from runtime</summary>
            public string AssetName { get; set; } = "";

            /// <summary>Asset type (Sprite, AudioClip, etc.)</summary>
            public string AssetType { get; set; } = "";

            /// <summary>Path to asset file from AssetRipper</summary>
            public string AssetPath { get; set; } = "";

            /// <summary>Runtime instance ID</summary>
            public long InstanceId { get; set; }

            /// <summary>Whether we have the actual asset file</summary>
            public bool HasAssetFile { get; set; }

            /// <summary>Can this be linked to asset browser?</summary>
            public bool CanLinkToAssetBrowser => IsReference && !string.IsNullOrEmpty(AssetPath);
        }
    }
}
