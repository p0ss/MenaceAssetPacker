// =============================================================================
// MENACE REFERENCE CODE - Common Template Types
// =============================================================================
// Reconstructed template structures for the most commonly modded data types.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Menace.Tactical;
using Menace.Tactical.AI;

namespace Menace
{
    // =========================================================================
    // ENTITY TEMPLATE
    // =========================================================================

    /// <summary>
    /// Template defining a unit type (soldier, enemy, vehicle, structure).
    ///
    /// This is one of the most important templates for modding as it defines
    /// all stats, visuals, and behaviors for a unit type.
    /// </summary>
    [Serializable]
    public class EntityTemplate : DataTemplate
    {
        // =====================================================================
        // IDENTITY
        // =====================================================================

        /// <summary>Display name. Offset: +0x78</summary>
        public LocalizedLine DisplayName;

        /// <summary>Description text. Offset: +0x80</summary>
        public LocalizedMultiLine Description;

        /// <summary>Portrait sprite. Offset: +0x88</summary>
        public Sprite Portrait;

        /// <summary>3D model prefab. Offset: +0x90</summary>
        public GameObject Prefab;

        // =====================================================================
        // FACTION
        // =====================================================================

        /// <summary>Faction type (Player, Enemy, Neutral). Offset: +0xA0</summary>
        public FactionType FactionType;

        /// <summary>Faction template reference. Offset: +0xA8</summary>
        public FactionTemplate Faction;

        // =====================================================================
        // BASE STATS
        // =====================================================================

        /// <summary>Base health points. Offset: +0xB0</summary>
        public int BaseHealth = 100;

        /// <summary>Base armor value. Offset: +0xB4</summary>
        public int BaseArmor = 0;

        /// <summary>Base movement points per turn. Offset: +0xB8</summary>
        public int BaseMovement = 6;

        /// <summary>Base action points per turn. Offset: +0xBC</summary>
        public int BaseActionPoints = 2;

        /// <summary>Base sight range in tiles. Offset: +0xC0</summary>
        public int BaseSightRange = 12;

        // =====================================================================
        // COMBAT STATS
        // =====================================================================

        /// <summary>Base accuracy percentage. Offset: +0xC4</summary>
        public int BaseAccuracy = 70;

        /// <summary>Base dodge percentage. Offset: +0xC8</summary>
        public int BaseDodge = 0;

        /// <summary>Base morale value. Offset: +0xCC</summary>
        public int BaseMorale = 100;

        /// <summary>Base suppression resistance. Offset: +0xD0</summary>
        public int BaseSuppressionResist = 0;

        /// <summary>Base discipline (reduces suppression). Offset: +0xD4</summary>
        public int BaseDiscipline = 0;

        // =====================================================================
        // EQUIPMENT SLOTS
        // =====================================================================

        /// <summary>Default weapon template. Offset: +0x100</summary>
        public WeaponTemplate DefaultWeapon;

        /// <summary>Default armor template. Offset: +0x108</summary>
        public ArmorTemplate DefaultArmor;

        /// <summary>Available equipment slots. Offset: +0x110</summary>
        public List<EquipmentSlot> EquipmentSlots;

        // =====================================================================
        // SKILLS
        // =====================================================================

        /// <summary>Default skills for this entity type. Offset: +0x120</summary>
        public List<SkillTemplate> DefaultSkills;

        // =====================================================================
        // AI CONFIGURATION
        // =====================================================================

        /// <summary>AI role data (behavior weights). Offset: +0x310</summary>
        public RoleData AIRole;

        // =====================================================================
        // FLAGS
        // =====================================================================

        /// <summary>Can this unit be suppressed? Offset: +0x320</summary>
        public bool CanBeSuppressed = true;

        /// <summary>Does this unit provide cover to others? Offset: +0x321</summary>
        public bool ProvidesCover = false;

        /// <summary>Is this a vehicle? Offset: +0x322</summary>
        public bool IsVehicle = false;

        /// <summary>Can this unit be hired/recruited? Offset: +0x323</summary>
        public bool IsHirable = false;
    }

    // =========================================================================
    // SKILL TEMPLATE
    // =========================================================================

    /// <summary>
    /// Template defining a usable ability/skill.
    /// </summary>
    [Serializable]
    public class SkillTemplate : DataTemplate
    {
        // =====================================================================
        // IDENTITY
        // =====================================================================

        /// <summary>Display name. Offset: +0x78</summary>
        public LocalizedLine DisplayName;

        /// <summary>Description. Offset: +0x80</summary>
        public LocalizedMultiLine Description;

        /// <summary>Skill icon. Offset: +0x88</summary>
        public Sprite Icon;

        // =====================================================================
        // TARGETING
        // =====================================================================

        /// <summary>Minimum range in tiles. Offset: +0x90</summary>
        public int MinRange = 1;

        /// <summary>Ideal range (no accuracy penalty). Offset: +0x94</summary>
        public int IdealRange = 5;

        /// <summary>Maximum range in tiles. Offset: +0x98</summary>
        public int MaxRange = 10;

        /// <summary>Area of effect radius (0 = single target). Offset: +0x9C</summary>
        public int AreaOfEffect = 0;

        /// <summary>Targeting mode. Offset: +0xA0</summary>
        public TargetingMode TargetingMode = TargetingMode.Enemy;

        // =====================================================================
        // COSTS
        // =====================================================================

        /// <summary>Action point cost. Offset: +0xA8</summary>
        public int ActionPointCost = 1;

        /// <summary>Ammo cost per use. Offset: +0xAC</summary>
        public int AmmoCost = 1;

        /// <summary>Cooldown in rounds. Offset: +0xB0</summary>
        public int Cooldown = 0;

        /// <summary>Uses per turn limit. Offset: +0xB4</summary>
        public int UsesPerTurn = -1;  // -1 = unlimited

        // =====================================================================
        // COMBAT MODIFIERS
        // =====================================================================

        /// <summary>Accuracy bonus. Offset: +0xC0</summary>
        public float AccuracyBonus = 0f;

        /// <summary>Accuracy multiplier. Offset: +0xC4</summary>
        public float AccuracyMult = 1f;

        /// <summary>Damage bonus. Offset: +0xC8</summary>
        public float DamageBonus = 0f;

        /// <summary>Damage multiplier. Offset: +0xCC</summary>
        public float DamageMult = 1f;

        // =====================================================================
        // FLAGS
        // =====================================================================

        /// <summary>Always hits (ignores accuracy). Offset: +0xF3</summary>
        public bool AlwaysHits = false;

        /// <summary>Ignores cover. Offset: +0x100</summary>
        public bool IgnoresCoverAtRange = false;

        /// <summary>Ends turn after use. Offset: +0x101</summary>
        public bool EndsTurn = false;

        /// <summary>Requires line of sight. Offset: +0x102</summary>
        public bool RequiresLOS = true;

        // =====================================================================
        // EFFECTS
        // =====================================================================

        /// <summary>Effects applied on use. Offset: +0x110</summary>
        public List<SkillEffect> Effects;

        /// <summary>Tags for AI/filtering. Offset: +0x118</summary>
        public List<TagTemplate> Tags;
    }

    // =========================================================================
    // WEAPON TEMPLATE
    // =========================================================================

    /// <summary>
    /// Template defining a weapon item.
    /// Extends BaseItemTemplate with weapon-specific properties.
    /// </summary>
    [Serializable]
    public class WeaponTemplate : BaseItemTemplate
    {
        // =====================================================================
        // RANGE
        // =====================================================================

        /// <summary>Minimum range. Offset: +0x13C</summary>
        public int MinRange = 1;

        /// <summary>Ideal range (no penalty). Offset: +0x140</summary>
        public int IdealRange = 5;

        /// <summary>Maximum range. Offset: +0x144</summary>
        public int MaxRange = 10;

        // =====================================================================
        // ACCURACY
        // =====================================================================

        /// <summary>Accuracy bonus added to user's accuracy. Offset: +0x14C</summary>
        public float AccuracyBonus = 0f;

        /// <summary>Accuracy dropoff per tile from ideal range. Offset: +0x150</summary>
        public float AccuracyDropoff = -2f;

        // =====================================================================
        // DAMAGE
        // =====================================================================

        /// <summary>Base damage bonus. Offset: +0x154</summary>
        public float DamageBonus = 10f;

        /// <summary>Damage dropoff per tile. Offset: +0x158</summary>
        public float DamageDropoff = -1f;

        // =====================================================================
        // ARMOR PENETRATION
        // =====================================================================

        /// <summary>Armor penetration bonus. Offset: +0x16C</summary>
        public float ArmorPenBonus = 0f;

        /// <summary>Armor pen dropoff per tile. Offset: +0x170</summary>
        public float ArmorPenDropoff = 0f;

        // =====================================================================
        // ANTI-ARMOR
        // =====================================================================

        /// <summary>Damage to armor durability. Offset: +0x174</summary>
        public float ArmorDurabilityDamage = 0f;

        /// <summary>Anti-armor damage multiplier. Offset: +0x178</summary>
        public float ArmorDurabilityDamageMult = 1f;

        // =====================================================================
        // AMMUNITION
        // =====================================================================

        /// <summary>Magazine capacity. Offset: +0x180</summary>
        public int MagazineSize = 10;

        /// <summary>Ammo type. Offset: +0x188</summary>
        public AmmoTemplate AmmoType;

        // =====================================================================
        // SKILLS PROVIDED
        // =====================================================================

        /// <summary>Skills this weapon provides. Offset: +0x190</summary>
        public List<SkillTemplate> Skills;
    }

    // =========================================================================
    // ARMOR TEMPLATE
    // =========================================================================

    /// <summary>
    /// Template defining an armor item.
    /// </summary>
    [Serializable]
    public class ArmorTemplate : BaseItemTemplate
    {
        // =====================================================================
        // PROTECTION
        // =====================================================================

        /// <summary>Armor value added to all zones. Offset: +0x190</summary>
        public int ArmorBonus = 0;

        /// <summary>Armor durability bonus. Offset: +0x194</summary>
        public int ArmorDurabilityBonus = 100;

        // =====================================================================
        // MOBILITY
        // =====================================================================

        /// <summary>Dodge penalty (reduces dodge). Offset: +0x198</summary>
        public float DodgePenalty = 0f;

        /// <summary>Movement multiplier. Offset: +0x1D0</summary>
        public float MovementMult = 1f;

        // =====================================================================
        // HEALTH
        // =====================================================================

        /// <summary>Health bonus. Offset: +0x19C</summary>
        public int HealthBonus = 0;

        /// <summary>Health multiplier. Offset: +0x1A0</summary>
        public float HealthMult = 1f;

        // =====================================================================
        // COMBAT MODIFIERS
        // =====================================================================

        /// <summary>Accuracy bonus (some armor affects aim). Offset: +0x1A4</summary>
        public int AccuracyBonus = 0;

        /// <summary>Accuracy multiplier. Offset: +0x1A8</summary>
        public float AccuracyMult = 1f;

        /// <summary>Evasion multiplier. Offset: +0x1AC</summary>
        public float EvasionMult = 1f;

        // =====================================================================
        // MORALE & SUPPRESSION
        // =====================================================================

        /// <summary>Morale bonus. Offset: +0x1B8</summary>
        public int MoraleBonus = 0;

        /// <summary>Morale multiplier. Offset: +0x1BC</summary>
        public float MoraleMult = 1f;

        /// <summary>Suppression resistance bonus. Offset: +0x1C0</summary>
        public int SuppressionResistBonus = 0;

        /// <summary>Suppression resistance multiplier. Offset: +0x1C4</summary>
        public float SuppressionResistMult = 1f;

        // =====================================================================
        // VISION
        // =====================================================================

        /// <summary>Sight range bonus. Offset: +0x1D4</summary>
        public int SightRangeBonus = 0;

        /// <summary>Sight range multiplier. Offset: +0x1D8</summary>
        public float SightRangeMult = 1f;

        // =====================================================================
        // ACTION ECONOMY
        // =====================================================================

        /// <summary>Action point bonus. Offset: +0x1DC</summary>
        public int ActionPointBonus = 0;

        /// <summary>Movement point bonus. Offset: +0x1E4</summary>
        public int MovementBonus = 0;

        /// <summary>Movement multiplier. Offset: +0x1E0</summary>
        public float MovementPointMult = 1f;
    }

    // =========================================================================
    // BASE ITEM TEMPLATE
    // =========================================================================

    /// <summary>
    /// Base class for all item templates.
    /// </summary>
    [Serializable]
    public abstract class BaseItemTemplate : DataTemplate
    {
        /// <summary>Display name. Offset: +0x78</summary>
        public LocalizedLine DisplayName;

        /// <summary>Description. Offset: +0x80</summary>
        public LocalizedMultiLine Description;

        /// <summary>Inventory icon. Offset: +0x88</summary>
        public Sprite Icon;

        /// <summary>Equipment slot type. Offset: +0x90</summary>
        public EquipmentSlotType SlotType;

        /// <summary>Value in credits. Offset: +0x94</summary>
        public int Value = 100;

        /// <summary>Rarity tier. Offset: +0x98</summary>
        public ItemRarity Rarity = ItemRarity.Common;

        /// <summary>Tags for filtering. Offset: +0xA0</summary>
        public List<TagTemplate> Tags;
    }

    // =========================================================================
    // SUPPORTING TYPES
    // =========================================================================

    public enum FactionType
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2,
        Allied = 3
    }

    public enum TargetingMode
    {
        Self = 0,
        Ally = 1,
        Enemy = 2,
        Any = 3,
        Tile = 4
    }

    public enum EquipmentSlotType
    {
        PrimaryWeapon = 0,
        SecondaryWeapon = 1,
        Armor = 2,
        Helmet = 3,
        Accessory = 4,
        Consumable = 5
    }

    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    [Serializable]
    public class EquipmentSlot
    {
        public EquipmentSlotType SlotType;
        public BaseItemTemplate DefaultItem;
    }

    [Serializable]
    public class LocalizedLine
    {
        public string Key;
        public string DefaultText;
    }

    [Serializable]
    public class LocalizedMultiLine
    {
        public string Key;
        public string DefaultText;
    }

    // Placeholder types
    public class SkillEffect { }
    public class TagTemplate : DataTemplate { }
    public class AmmoTemplate : DataTemplate { }
    public class FactionTemplate : DataTemplate { }
    public class TacticalConfig : ScriptableObject { }
    public class StrategyConfig : ScriptableObject { }
    public class CampaignProgressConfig : ScriptableObject { }
    public class UIConfig : ScriptableObject { }
    public class TextTooltipsConfig : ScriptableObject { }
    public class BlackMarketConfig : ScriptableObject { }
    public class GameConfig : ScriptableObject { }
    public class EmotionalStatesConfig : ScriptableObject { }
    public class SquaddiesConfig : ScriptableObject { }
    public class LoadingQuoteConfig : ScriptableObject { }
    public class AIWeightsTemplate : ScriptableObject { public static AIWeightsTemplate Instance; }

    // Additional template placeholders
    public class StoryFactionTemplate : DataTemplate { }
    public class OperationTemplate : DataTemplate { }
    public class MissionTemplate : DataTemplate { }
    public class GenericMissionTemplate : DataTemplate { }
    public class MissionDifficultyTemplate : DataTemplate { }
    public class MissionPOITemplate : DataTemplate { }
    public class MissionSetpiece : DataTemplate { }
    public class ArmyTemplate : DataTemplate { }
    public class EnemyAssetTemplate : DataTemplate { }
    public class StrategicAssetTemplate : DataTemplate { }
    public class OperationAssetTemplate : DataTemplate { }
    public class PlanetTemplate : DataTemplate { }
    public class BiomeTemplate : DataTemplate { }
    public class WeatherTemplate : DataTemplate { }
    public class TileEffectTemplate : DataTemplate { }
    public class SurfaceTypeTemplate : DataTemplate { }
    public class ModularVehicleTemplate : DataTemplate { }
    public class ConversationStageTemplate : DataTemplate { }
    public class ConversationEffectsTemplate : DataTemplate { }
    public class SpeakerTemplate : DataTemplate { }
    public class EmotionalStateTemplate : DataTemplate { }
    public class ShipUpgradeTemplate : DataTemplate { }
    public class ShipUpgradeSlotTemplate : DataTemplate { }
    public class OffmapAbilityTemplate : DataTemplate { }
    public class OperationDurationTemplate : DataTemplate { }
    public class OperationIntrosTemplate : DataTemplate { }
    public class ItemListTemplate : DataTemplate { }
    public class ItemFilterTemplate : DataTemplate { }
    public class RewardTableTemplate : DataTemplate { }
    public class GlobalDifficultyTemplate : DataTemplate { }
    public class VideoTemplate : DataTemplate { }
    public class PropertyDisplayConfigTemplate : DataTemplate { }
    public class AnimationSequenceTemplate : DataTemplate { }
    public class AnimationSoundTemplate : DataTemplate { }
    public class UnitLeaderTemplate : DataTemplate { }
    public class UnitRankTemplate : DataTemplate { }
    public class PerkTemplate : DataTemplate { }
    public class PerkTreeTemplate : DataTemplate { }
}
