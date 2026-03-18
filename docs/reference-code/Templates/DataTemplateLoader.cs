// =============================================================================
// MENACE REFERENCE CODE - Template Loading System
// =============================================================================
// Reconstructed template loading system showing how game data assets are
// loaded from Resources folders.
//
// VERIFICATION STATUS: Verified against binary 2026-03-10
// Key corrections applied:
//   - Template keys use GetID() not .name (verified at 0x180a595d0)
//   - GetID() returns cached ID at offset +0x68, falls back to .name
//   - Added missing template types: GlobalAnimatorConfig, MissionPreviewConfigTemplate
//   - Removed non-existent separate paths for WeaponTemplate/ArmorTemplate
//   - Added duplicate detection error logging
//   - Added BasePlayerSettingTemplate with inheritance support
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Menace
{
    /// <summary>
    /// Base class for all data templates (ScriptableObjects).
    /// Templates define game data like entities, skills, items, etc.
    ///
    /// Address: 0x18052fd50 (GetID)
    /// </summary>
    public abstract class DataTemplate : ScriptableObject
    {
        // Unity's ScriptableObject provides:
        // - name: Asset name (fallback for GetID())
        // - Serialization support

        /// <summary>
        /// Cached ID value. Offset: +0x68
        /// If null/empty, GetID() will populate it from the asset name.
        /// </summary>
        [SerializeField]
        private string m_CachedID;

        /// <summary>
        /// Returns the template's unique identifier used for dictionary lookup.
        ///
        /// Address: 0x18052fd50
        ///
        /// IMPORTANT: This method is used (not .name) when building the
        /// template lookup dictionaries. The ID is cached at offset +0x68.
        /// If the cached ID is null/empty, it falls back to the asset name.
        /// </summary>
        /// <returns>The template's unique identifier string</returns>
        public virtual string GetID()
        {
            // Actual implementation at 0x18052fd50:
            // 1. Check if m_CachedID (offset +0x68) is null or empty
            // 2. If so, populate it from UnityEngine.Object.name
            // 3. Return the cached ID
            if (string.IsNullOrEmpty(m_CachedID))
            {
                m_CachedID = name;
            }
            return m_CachedID;
        }
    }

    /// <summary>
    /// Centralized template loading and caching system.
    ///
    /// Key addresses:
    ///   GetBaseFolder: 0x18052dea0
    ///   GetAll&lt;T&gt;: 0x180a58e60
    ///   LoadTemplates&lt;T&gt;: 0x180a595d0
    ///   GetSingleton: 0x18052f570
    ///
    /// Templates are loaded via Resources.LoadAll from predefined paths.
    /// Results are cached for subsequent access.
    ///
    /// NOTE: The actual binary uses a large if-else chain for type-to-path mapping,
    /// not a Dictionary. The Dictionary shown here is a conceptual simplification.
    /// </summary>
    public class DataTemplateLoader
    {
        // =====================================================================
        // SINGLETON
        // =====================================================================

        private static DataTemplateLoader s_Instance;

        /// <summary>
        /// Gets the singleton instance.
        /// Actual implementation uses GetSingleton() method at 0x18052f570.
        /// </summary>
        public static DataTemplateLoader Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new DataTemplateLoader();
                return s_Instance;
            }
        }

        // =====================================================================
        // CACHE
        // =====================================================================

        /// <summary>
        /// Cached template arrays by type.
        /// Offset: +0x10 from instance
        /// </summary>
        private Dictionary<Type, object> m_TemplateArrays = new();

        /// <summary>
        /// Cached template ID -> instance maps by type.
        /// Offset: +0x18 from instance
        ///
        /// IMPORTANT: Keys are template.GetID(), NOT template.name
        /// </summary>
        private Dictionary<Type, Dictionary<string, DataTemplate>> m_TemplateMaps = new();

        // =====================================================================
        // PATH MAPPINGS
        // =====================================================================

        /// <summary>
        /// Resource path mappings for template types.
        /// These are the "Data/" subfolder paths where templates are stored.
        ///
        /// Address: 0x18052dea0 (GetBaseFolder)
        ///
        /// NOTE: Actual implementation uses sequential type comparisons via
        /// Unity_Burst_Unsafe__AreSame&lt;&gt;() in a large if-else chain,
        /// not a Dictionary lookup. This is shown as a Dictionary for clarity.
        ///
        /// For types with inheritance support (BaseItemTemplate, BasePlayerSettingTemplate,
        /// TileEffectTemplate), the actual code uses IsSubclassOf checks via virtual
        /// method calls at offset +0x2a8.
        /// </summary>
        private static readonly Dictionary<Type, string> TemplatePaths = new()
        {
            // Animation
            { typeof(AnimationSequenceTemplate), "Data/AnimationSequences/" },
            { typeof(AnimationSoundTemplate), "Data/AnimationSounds/" },

            // Combat & Entities
            { typeof(EntityTemplate), "Data/Entities/" },
            { typeof(SkillTemplate), "Data/Skills/" },

            // Items (BaseItemTemplate with inheritance support)
            // NOTE: WeaponTemplate, ArmorTemplate etc. inherit from BaseItemTemplate
            // and use the same path - there are NO separate paths for subtypes
            { typeof(BaseItemTemplate), "Data/Items/" },
            { typeof(ItemListTemplate), "Data/ItemLists/" },
            { typeof(ItemFilterTemplate), "Data/ItemFilters/" },

            // Units & Progression
            { typeof(UnitLeaderTemplate), "Data/UnitLeaders/" },
            { typeof(UnitRankTemplate), "Data/UnitRanks/" },
            { typeof(PerkTemplate), "Data/Perks/" },
            { typeof(PerkTreeTemplate), "Data/PerkTrees/" },

            // Player Settings (BasePlayerSettingTemplate with inheritance support)
            { typeof(BasePlayerSettingTemplate), "Data/PlayerSettings/" },

            // Campaign & Missions
            { typeof(FactionTemplate), "Data/Factions/" },
            { typeof(StoryFactionTemplate), "Data/StoryFactions/" },
            { typeof(OperationTemplate), "Data/Operations/" },
            { typeof(OperationIntrosTemplate), "Data/OperationIntros/" },
            { typeof(OperationDurationTemplate), "Data/OperationDurations/" },
            { typeof(MissionTemplate), "Data/Missions/" },
            { typeof(GenericMissionTemplate), "Data/Missions/" },
            { typeof(MissionDifficultyTemplate), "Data/MissionDifficulty/" },
            { typeof(MissionPOITemplate), "Data/MissionPOI/" },
            { typeof(MissionSetpiece), "Data/MissionSetpieces/" },
            { typeof(MissionPreviewConfigTemplate), "Data/MissionPreviews/" },

            // Army & Enemies
            { typeof(ArmyTemplate), "Data/Armies/" },
            { typeof(EnemyAssetTemplate), "Data/EnemyAssets/" },
            { typeof(StrategicAssetTemplate), "Data/StrategicAssets/" },
            { typeof(OperationAssetTemplate), "Data/OperationAssets/" },

            // World
            { typeof(PlanetTemplate), "Data/Planets/" },
            { typeof(BiomeTemplate), "Data/Biomes/" },
            { typeof(WeatherTemplate), "Data/Weather/" },
            // TileEffectTemplate has inheritance support
            { typeof(TileEffectTemplate), "Data/TileEffects/" },
            { typeof(SurfaceTypeTemplate), "Data/SurfaceTypes/" },

            // Vehicles
            { typeof(ModularVehicleTemplate), "Data/ModularVehicles/" },

            // Conversations & Events
            { typeof(ConversationStageTemplate), "Data/ConversationStages/" },
            { typeof(ConversationEffectsTemplate), "Data/ConversationEffects/" },
            { typeof(SpeakerTemplate), "Data/Speakers/" },
            { typeof(EmotionalStateTemplate), "Data/EmotionalStates/" },

            // Strategy
            { typeof(ShipUpgradeTemplate), "Data/ShipUpgrades/" },
            { typeof(ShipUpgradeSlotTemplate), "Data/ShipUpgradeSlots/" },
            { typeof(OffmapAbilityTemplate), "Data/OffmapAbilities/" },

            // Rewards
            { typeof(RewardTableTemplate), "Data/RewardTables/" },

            // Misc
            { typeof(TagTemplate), "Data/Tags/" },
            { typeof(GlobalDifficultyTemplate), "Data/GlobalDifficulty/" },
            { typeof(VideoTemplate), "Data/Videos/" },
            { typeof(PropertyDisplayConfigTemplate), "Data/PropertyDisplayConfigs/" },
        };

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>
        /// Gets all templates of the specified type.
        /// Results are cached after first load.
        ///
        /// Address: 0x180a58e60
        ///
        /// Implementation details:
        /// - Calls GetSingleton() to get instance
        /// - Uses dictionary at offset +0x10 for cache lookup
        /// - Includes verbose logging when LogVerbose(7,0) returns true
        /// </summary>
        /// <typeparam name="T">Template type (must extend DataTemplate)</typeparam>
        /// <returns>Read-only collection of all templates of this type</returns>
        public static IReadOnlyCollection<T> GetAll<T>() where T : DataTemplate
        {
            Type type = typeof(T);

            // Check cache (offset +0x10 from instance)
            if (Instance.m_TemplateArrays.TryGetValue(type, out var cached))
            {
                return (T[])cached;
            }

            // Load and cache
            Instance.LoadTemplates<T>(out T[] templates, out var map);
            Instance.m_TemplateArrays[type] = templates;
            Instance.m_TemplateMaps[type] = map;

            return templates;
        }

        /// <summary>
        /// Gets a specific template by ID.
        ///
        /// IMPORTANT: The lookup key is template.GetID(), not template.name.
        /// For most templates these are the same, but some may have custom IDs.
        /// </summary>
        /// <typeparam name="T">Template type</typeparam>
        /// <param name="id">Template ID (from GetID(), typically same as asset name)</param>
        /// <returns>Template instance or null if not found</returns>
        public static T Get<T>(string id) where T : DataTemplate
        {
            // Ensure templates are loaded
            GetAll<T>();

            Type type = typeof(T);
            if (Instance.m_TemplateMaps.TryGetValue(type, out var map))
            {
                if (map.TryGetValue(id, out var template))
                {
                    return template as T;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the resource folder path for a template type.
        ///
        /// Address: 0x18052dea0
        ///
        /// Actual implementation uses sequential type comparisons via
        /// Unity_Burst_Unsafe__AreSame&lt;&gt;() in a large if-else chain.
        ///
        /// For certain types (BaseItemTemplate, BasePlayerSettingTemplate, TileEffectTemplate),
        /// inheritance is checked via IsSubclassOf (virtual call at +0x2a8).
        ///
        /// If no match found, logs warning and returns empty string (StringLiteral_7655).
        /// </summary>
        public static string GetBaseFolder(Type templateType)
        {
            // Check direct mapping
            if (TemplatePaths.TryGetValue(templateType, out string path))
                return path;

            // Check inheritance (for BaseItemTemplate, BasePlayerSettingTemplate,
            // TileEffectTemplate subclasses)
            foreach (var kvp in TemplatePaths)
            {
                if (templateType.IsSubclassOf(kvp.Key))
                    return kvp.Value;
            }

            // No path found - actual implementation logs warning here
            Debug.LogWarning($"No resource path for template type: {templateType}");
            return string.Empty;
        }

        // =====================================================================
        // INTERNAL LOADING
        // =====================================================================

        /// <summary>
        /// Loads all templates of specified type from Resources.
        ///
        /// Address: 0x180a595d0
        ///
        /// Implementation details:
        /// - Creates StopwatchScope for performance measurement
        /// - Logs debug info when LogDebug(7,0) returns true
        /// - If templates.Length &lt; 1, logs warning (not shown in reference)
        /// - Uses GetID() NOT .name for dictionary keys
        /// - Checks for duplicate IDs and logs ERROR (does not silently overwrite)
        /// - Stores arrays at instance offset +0x10
        /// - Stores maps at instance offset +0x18
        /// </summary>
        private void LoadTemplates<T>(out T[] templates, out Dictionary<string, DataTemplate> map)
            where T : DataTemplate
        {
            // Note: Actual implementation uses StopwatchScope for timing
            string folder = GetBaseFolder(typeof(T));

            if (string.IsNullOrEmpty(folder))
            {
                // No folder path - template type has no loadable assets
                templates = Array.Empty<T>();
                map = new Dictionary<string, DataTemplate>();
                return;
            }

            // Load all assets from Resources folder
            templates = Resources.LoadAll<T>(folder);

            // Actual implementation logs warning if no templates found
            if (templates.Length < 1)
            {
                Debug.LogWarning($"No {typeof(T).Name} found in folder {folder}");
            }

            // Build ID -> template map for fast lookup
            // IMPORTANT: Uses GetID() not .name
            map = new Dictionary<string, DataTemplate>(templates.Length);
            foreach (var template in templates)
            {
                string id = template.GetID();

                // Actual implementation checks for duplicates and logs ERROR
                if (map.ContainsKey(id))
                {
                    Debug.LogError($"Duplicate template ID in {typeof(T).Name}: '{id}'");
                }

                map[id] = template;
            }
        }

        /// <summary>
        /// Clears the template cache.
        /// Call when templates may have changed (e.g., after mod loading).
        /// </summary>
        public static void ClearCache()
        {
            Instance.m_TemplateArrays.Clear();
            Instance.m_TemplateMaps.Clear();
        }
    }

    // =========================================================================
    // SINGLETON CONFIGS
    // =========================================================================

    /// <summary>
    /// Singleton configuration templates.
    /// These are loaded individually via GetBaseFolder returning their paths,
    /// or loaded directly via Resources.Load.
    ///
    /// Note: GlobalAnimatorConfig was found in the binary but not in original reference.
    /// </summary>
    public static class SingletonConfigs
    {
        // Combat
        public static TacticalConfig TacticalConfig =>
            Resources.Load<TacticalConfig>("Config/TacticalConfig");

        // Strategy
        public static StrategyConfig StrategyConfig =>
            Resources.Load<StrategyConfig>("Config/StrategyConfig");

        public static CampaignProgressConfig CampaignProgress =>
            Resources.Load<CampaignProgressConfig>("Config/CampaignProgressConfig");

        // UI
        public static UIConfig UIConfig =>
            Resources.Load<UIConfig>("Config/UIConfig");

        public static TextTooltipsConfig TextTooltips =>
            Resources.Load<TextTooltipsConfig>("Config/TextTooltipsConfig");

        // Shop
        public static BlackMarketConfig BlackMarket =>
            Resources.Load<BlackMarketConfig>("Config/BlackMarketConfig");

        // Misc
        public static GameConfig GameConfig =>
            Resources.Load<GameConfig>("Config/GameConfig");

        public static EmotionalStatesConfig EmotionalStates =>
            Resources.Load<EmotionalStatesConfig>("Config/EmotionalStatesConfig");

        public static SquaddiesConfig Squaddies =>
            Resources.Load<SquaddiesConfig>("Config/SquaddiesConfig");

        public static LoadingQuoteConfig LoadingQuotes =>
            Resources.Load<LoadingQuoteConfig>("Config/LoadingQuoteConfig");

        public static AIWeightsTemplate AIWeights =>
            Resources.Load<AIWeightsTemplate>("Config/AIWeights");

        // Added from binary verification - was missing in original reference
        public static GlobalAnimatorConfig GlobalAnimator =>
            Resources.Load<GlobalAnimatorConfig>("Config/GlobalAnimatorConfig");
    }

    // =========================================================================
    // TEMPLATE REDIRECTION (for save compatibility)
    // =========================================================================

    /// <summary>
    /// Redirection action for deprecated templates.
    /// </summary>
    public enum DataTemplateRedirectAction
    {
        /// <summary>Template needs migration handling</summary>
        ToDo = 0,

        /// <summary>Replace with NewTemplate</summary>
        ReplaceWith = 1,

        /// <summary>Silently ignore (template was removed)</summary>
        Ignore = 2
    }

    /// <summary>
    /// Defines a template redirection for save compatibility.
    /// When loading saves with old template references, the game
    /// can redirect them to new templates.
    /// </summary>
    [Serializable]
    public class DataTemplateRedirection
    {
        /// <summary>Original folder path. Offset: +0x10</summary>
        public string BaseFolder;

        /// <summary>Old template name. Offset: +0x18</summary>
        public string OldName;

        /// <summary>What to do with this template. Offset: +0x20</summary>
        public DataTemplateRedirectAction Action;

        /// <summary>Replacement template (if Action == ReplaceWith). Offset: +0x28</summary>
        public DataTemplate NewTemplate;
    }
}
