// =============================================================================
// MENACE REFERENCE CODE - Template Loading System
// =============================================================================
// Reconstructed template loading system showing how game data assets are
// loaded from Resources folders.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Menace
{
    /// <summary>
    /// Base class for all data templates (ScriptableObjects).
    /// Templates define game data like entities, skills, items, etc.
    /// </summary>
    public abstract class DataTemplate : ScriptableObject
    {
        // Unity's ScriptableObject provides:
        // - name: Asset name (used for lookup)
        // - Serialization support
    }

    /// <summary>
    /// Centralized template loading and caching system.
    ///
    /// Key addresses:
    ///   GetBaseFolder: 0x18052dea0
    ///   GetAll<T>: 0x180a58e60
    ///   LoadTemplates<T>: 0x180a595d0
    ///
    /// Templates are loaded via Resources.LoadAll from predefined paths.
    /// Results are cached for subsequent access.
    /// </summary>
    public class DataTemplateLoader
    {
        // =====================================================================
        // SINGLETON
        // =====================================================================

        private static DataTemplateLoader s_Instance;

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

        /// <summary>Cached template arrays by type.</summary>
        private Dictionary<Type, object> m_TemplateArrays = new();

        /// <summary>Cached template name -> instance maps by type.</summary>
        private Dictionary<Type, Dictionary<string, DataTemplate>> m_TemplateMaps = new();

        // =====================================================================
        // PATH MAPPINGS
        // =====================================================================

        /// <summary>
        /// Resource path mappings for template types.
        /// These are the "Data/" subfolder paths where templates are stored.
        ///
        /// Address: 0x18052dea0 (GetBaseFolder)
        /// </summary>
        private static readonly Dictionary<Type, string> TemplatePaths = new()
        {
            // Combat & Entities
            { typeof(EntityTemplate), "Data/Entities/" },
            { typeof(SkillTemplate), "Data/Skills/" },
            { typeof(WeaponTemplate), "Data/Items/Weapons/" },
            { typeof(ArmorTemplate), "Data/Items/Armor/" },
            { typeof(BaseItemTemplate), "Data/Items/" },

            // Units & Progression
            { typeof(UnitLeaderTemplate), "Data/UnitLeaders/" },
            { typeof(UnitRankTemplate), "Data/UnitRanks/" },
            { typeof(PerkTemplate), "Data/Perks/" },
            { typeof(PerkTreeTemplate), "Data/PerkTrees/" },

            // Campaign & Missions
            { typeof(FactionTemplate), "Data/Factions/" },
            { typeof(StoryFactionTemplate), "Data/StoryFactions/" },
            { typeof(OperationTemplate), "Data/Operations/" },
            { typeof(MissionTemplate), "Data/Missions/" },
            { typeof(GenericMissionTemplate), "Data/Missions/" },
            { typeof(MissionDifficultyTemplate), "Data/MissionDifficulty/" },
            { typeof(MissionPOITemplate), "Data/MissionPOI/" },
            { typeof(MissionSetpiece), "Data/MissionSetpieces/" },

            // Army & Enemies
            { typeof(ArmyTemplate), "Data/Armies/" },
            { typeof(EnemyAssetTemplate), "Data/EnemyAssets/" },
            { typeof(StrategicAssetTemplate), "Data/StrategicAssets/" },
            { typeof(OperationAssetTemplate), "Data/OperationAssets/" },

            // World
            { typeof(PlanetTemplate), "Data/Planets/" },
            { typeof(BiomeTemplate), "Data/Biomes/" },
            { typeof(WeatherTemplate), "Data/Weather/" },
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
            { typeof(OperationDurationTemplate), "Data/OperationDurations/" },
            { typeof(OperationIntrosTemplate), "Data/OperationIntros/" },

            // Items & Rewards
            { typeof(ItemListTemplate), "Data/ItemLists/" },
            { typeof(ItemFilterTemplate), "Data/ItemFilters/" },
            { typeof(RewardTableTemplate), "Data/RewardTables/" },

            // Misc
            { typeof(TagTemplate), "Data/Tags/" },
            { typeof(GlobalDifficultyTemplate), "Data/GlobalDifficulty/" },
            { typeof(VideoTemplate), "Data/Videos/" },
            { typeof(PropertyDisplayConfigTemplate), "Data/PropertyDisplayConfigs/" },
            { typeof(AnimationSequenceTemplate), "Data/AnimationSequences/" },
            { typeof(AnimationSoundTemplate), "Data/AnimationSounds/" },
        };

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        /// <summary>
        /// Gets all templates of the specified type.
        /// Results are cached after first load.
        ///
        /// Address: 0x180a58e60
        /// </summary>
        /// <typeparam name="T">Template type (must extend DataTemplate)</typeparam>
        /// <returns>Read-only collection of all templates of this type</returns>
        public static IReadOnlyCollection<T> GetAll<T>() where T : DataTemplate
        {
            Type type = typeof(T);

            // Check cache
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
        /// Gets a specific template by name.
        /// </summary>
        /// <typeparam name="T">Template type</typeparam>
        /// <param name="name">Template name (asset name)</param>
        /// <returns>Template instance or null if not found</returns>
        public static T Get<T>(string name) where T : DataTemplate
        {
            // Ensure templates are loaded
            GetAll<T>();

            Type type = typeof(T);
            if (Instance.m_TemplateMaps.TryGetValue(type, out var map))
            {
                if (map.TryGetValue(name, out var template))
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
        /// </summary>
        public static string GetBaseFolder(Type templateType)
        {
            // Check direct mapping
            if (TemplatePaths.TryGetValue(templateType, out string path))
                return path;

            // Check inheritance (for BaseItemTemplate subclasses, etc.)
            foreach (var kvp in TemplatePaths)
            {
                if (templateType.IsSubclassOf(kvp.Key))
                    return kvp.Value;
            }

            // No path - template is embedded or singleton config
            return string.Empty;
        }

        // =====================================================================
        // INTERNAL LOADING
        // =====================================================================

        /// <summary>
        /// Loads all templates of specified type from Resources.
        ///
        /// Address: 0x180a595d0
        /// </summary>
        private void LoadTemplates<T>(out T[] templates, out Dictionary<string, DataTemplate> map)
            where T : DataTemplate
        {
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

            // Build name -> template map for fast lookup
            map = new Dictionary<string, DataTemplate>(templates.Length);
            foreach (var template in templates)
            {
                map[template.name] = template;
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
    /// These are loaded individually, not from folders.
    ///
    /// Use: var config = Resources.Load&lt;TacticalConfig&gt;("path/to/config");
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
