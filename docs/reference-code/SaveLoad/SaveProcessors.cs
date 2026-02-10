// =============================================================================
// MENACE REFERENCE CODE - Save Processors
// =============================================================================
// Individual processors that handle saving/loading specific game systems.
// Each processor knows how to serialize its domain's state.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Menace.SaveLoad
{
    // =========================================================================
    // PROCESSOR INTERFACE
    // =========================================================================

    /// <summary>
    /// Interface for save/load processors.
    /// Each processor handles a specific game system.
    ///
    /// Processors are registered with SaveManager and called in order.
    /// </summary>
    public interface ISaveProcessor
    {
        /// <summary>Unique identifier for this processor.</summary>
        string ProcessorId { get; }

        /// <summary>Order in which to process (lower = earlier).</summary>
        int Priority { get; }

        /// <summary>Save this system's state to the writer.</summary>
        void Save(BinaryWriter writer, SaveContext context);

        /// <summary>Load this system's state from the reader.</summary>
        void Load(BinaryReader reader, LoadContext context);
    }

    /// <summary>
    /// Context provided to processors during save.
    /// </summary>
    public class SaveContext
    {
        /// <summary>The save state being built.</summary>
        public SaveState State;

        /// <summary>Map of object -> save ID for references.</summary>
        public Dictionary<object, int> ObjectIds;

        /// <summary>Current object ID counter.</summary>
        public int NextObjectId;

        /// <summary>Register an object and get its save ID.</summary>
        public int RegisterObject(object obj)
        {
            if (ObjectIds.TryGetValue(obj, out int id))
                return id;

            id = NextObjectId++;
            ObjectIds[obj] = id;
            return id;
        }
    }

    /// <summary>
    /// Context provided to processors during load.
    /// </summary>
    public class LoadContext
    {
        /// <summary>The loaded save state.</summary>
        public SaveState State;

        /// <summary>Map of save ID -> loaded object.</summary>
        public Dictionary<int, object> LoadedObjects;

        /// <summary>Register a loaded object by its save ID.</summary>
        public void RegisterLoaded(int id, object obj)
        {
            LoadedObjects[id] = obj;
        }

        /// <summary>Get a previously loaded object.</summary>
        public T GetObject<T>(int id) where T : class
        {
            if (LoadedObjects.TryGetValue(id, out object obj))
                return obj as T;
            return null;
        }
    }

    // =========================================================================
    // ENTITY PROCESSOR
    // =========================================================================

    /// <summary>
    /// Saves and loads entity states.
    ///
    /// Entities are saved with:
    /// - Template reference (name)
    /// - Runtime state (HP, position, etc.)
    /// - Property overrides
    /// - Status effects
    /// - Equipment
    ///
    /// Address: 0x180718000
    /// </summary>
    public class EntityProcessor : ISaveProcessor
    {
        public string ProcessorId => "entities";
        public int Priority => 100;

        public void Save(BinaryWriter writer, SaveContext context)
        {
            var entities = EntityManager.Instance.AllEntities;

            // Write entity count
            writer.Write(entities.Count);

            foreach (var entity in entities)
            {
                SaveEntity(writer, entity, context);
            }
        }

        public void Load(BinaryReader reader, LoadContext context)
        {
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                LoadEntity(reader, context);
            }
        }

        private void SaveEntity(BinaryWriter writer, Entity entity, SaveContext context)
        {
            // Register entity for cross-references
            int entityId = context.RegisterObject(entity);

            // Basic info
            writer.Write(entityId);
            writer.Write(entity.UniqueId ?? "");
            writer.Write(entity.Template.name);

            // State
            writer.Write(entity.Hitpoints);
            writer.Write(entity.HitpointsMax);
            writer.Write(entity.CurrentAP);
            writer.Write(entity.MaxAP);

            // Position
            SavePosition(writer, entity.CurrentTile);

            // Suppression/Morale
            writer.Write(entity.CurrentSuppression);
            writer.Write(entity.CurrentMorale);

            // Properties
            SaveProperties(writer, entity.Properties);

            // Status effects
            SaveStatuses(writer, entity.StatusContainer);

            // Equipment
            SaveEquipment(writer, entity.Equipment);

            // Skills
            SaveSkills(writer, entity.SkillContainer);
        }

        private void LoadEntity(BinaryReader reader, LoadContext context)
        {
            int saveId = reader.ReadInt32();
            string uniqueId = reader.ReadString();
            string templateName = reader.ReadString();

            // Handle template redirection
            templateName = TemplateRedirector.GetRedirect(templateName, out bool ignore);
            if (ignore)
            {
                SkipEntityData(reader);
                return;
            }

            // Create or get entity
            var entity = EntityManager.Instance.GetOrCreateEntity(uniqueId, templateName);
            context.RegisterLoaded(saveId, entity);

            // State
            entity.Hitpoints = reader.ReadInt32();
            entity.HitpointsMax = reader.ReadInt32();
            entity.CurrentAP = reader.ReadInt32();
            entity.MaxAP = reader.ReadInt32();

            // Position
            LoadPosition(reader, entity);

            // Suppression/Morale
            entity.CurrentSuppression = reader.ReadSingle();
            entity.CurrentMorale = reader.ReadSingle();

            // Properties
            LoadProperties(reader, entity.Properties);

            // Status effects
            LoadStatuses(reader, entity.StatusContainer);

            // Equipment
            LoadEquipment(reader, entity.Equipment);

            // Skills
            LoadSkills(reader, entity.SkillContainer);
        }

        private void SavePosition(BinaryWriter writer, Tile tile)
        {
            if (tile == null)
            {
                writer.Write(false);
                return;
            }

            writer.Write(true);
            writer.Write(tile.GridX);
            writer.Write(tile.GridY);
            writer.Write(tile.GridZ);
        }

        private void LoadPosition(BinaryReader reader, Entity entity)
        {
            bool hasPosition = reader.ReadBoolean();
            if (!hasPosition) return;

            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int z = reader.ReadInt32();

            var tile = TacticalMap.Instance?.GetTile(x, y, z);
            if (tile != null)
            {
                entity.SetPosition(tile);
            }
        }

        private void SaveProperties(BinaryWriter writer, EntityProperties props)
        {
            // Get all modified properties
            var modified = props.GetModifiedProperties();

            writer.Write(modified.Count);
            foreach (var kvp in modified)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        private void LoadProperties(BinaryReader reader, EntityProperties props)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                float value = reader.ReadSingle();
                props.SetProperty(name, value);
            }
        }

        private void SaveStatuses(BinaryWriter writer, StatusContainer container)
        {
            var statuses = container.GetActiveStatuses();

            writer.Write(statuses.Count);
            foreach (var status in statuses)
            {
                writer.Write(status.Template.name);
                writer.Write(status.RemainingDuration);
                writer.Write(status.Stacks);
            }
        }

        private void LoadStatuses(BinaryReader reader, StatusContainer container)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string templateName = reader.ReadString();
                int duration = reader.ReadInt32();
                int stacks = reader.ReadInt32();

                templateName = TemplateRedirector.GetRedirect(templateName, out bool ignore);
                if (!ignore)
                {
                    container.AddStatusByName(templateName, duration, stacks);
                }
            }
        }

        private void SaveEquipment(BinaryWriter writer, EquipmentContainer equipment)
        {
            var slots = equipment.GetAllSlots();

            writer.Write(slots.Count);
            foreach (var slot in slots)
            {
                writer.Write(slot.SlotType.ToString());
                writer.Write(slot.Item?.Template.name ?? "");
            }
        }

        private void LoadEquipment(BinaryReader reader, EquipmentContainer equipment)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string slotType = reader.ReadString();
                string itemName = reader.ReadString();

                if (!string.IsNullOrEmpty(itemName))
                {
                    itemName = TemplateRedirector.GetRedirect(itemName, out bool ignore);
                    if (!ignore)
                    {
                        equipment.EquipByName(slotType, itemName);
                    }
                }
            }
        }

        private void SaveSkills(BinaryWriter writer, SkillContainer skills)
        {
            var allSkills = skills.GetAllSkills();

            writer.Write(allSkills.Count);
            foreach (var skill in allSkills)
            {
                writer.Write(skill.Template.name);
                writer.Write(skill.RemainingCooldown);
            }
        }

        private void LoadSkills(BinaryReader reader, SkillContainer skills)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string skillName = reader.ReadString();
                int cooldown = reader.ReadInt32();

                var skill = skills.GetSkillByName(skillName);
                if (skill != null)
                {
                    skill.RemainingCooldown = cooldown;
                }
            }
        }

        private void SkipEntityData(BinaryReader reader)
        {
            // Skip all entity data when template is ignored
            // This needs to match the exact read pattern
            reader.ReadInt32(); // hitpoints
            reader.ReadInt32(); // hitpointsMax
            reader.ReadInt32(); // currentAP
            reader.ReadInt32(); // maxAP

            // Position
            if (reader.ReadBoolean())
            {
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
            }

            reader.ReadSingle(); // suppression
            reader.ReadSingle(); // morale

            // Properties
            int propCount = reader.ReadInt32();
            for (int i = 0; i < propCount; i++)
            {
                reader.ReadString();
                reader.ReadSingle();
            }

            // Statuses
            int statusCount = reader.ReadInt32();
            for (int i = 0; i < statusCount; i++)
            {
                reader.ReadString();
                reader.ReadInt32();
                reader.ReadInt32();
            }

            // Equipment
            int equipCount = reader.ReadInt32();
            for (int i = 0; i < equipCount; i++)
            {
                reader.ReadString();
                reader.ReadString();
            }

            // Skills
            int skillCount = reader.ReadInt32();
            for (int i = 0; i < skillCount; i++)
            {
                reader.ReadString();
                reader.ReadInt32();
            }
        }
    }

    // =========================================================================
    // TACTICAL MAP PROCESSOR
    // =========================================================================

    /// <summary>
    /// Saves and loads tactical map state.
    ///
    /// Saves:
    /// - Tile destruction state
    /// - Active tile effects (fire, smoke, etc.)
    /// - Cover state changes
    ///
    /// Address: 0x18071a000
    /// </summary>
    public class TacticalMapProcessor : ISaveProcessor
    {
        public string ProcessorId => "tactical_map";
        public int Priority => 50;

        public void Save(BinaryWriter writer, SaveContext context)
        {
            var map = TacticalMap.Instance;
            if (map == null)
            {
                writer.Write(false);
                return;
            }

            writer.Write(true);
            writer.Write(map.Width);
            writer.Write(map.Height);

            // Save only modified tiles
            var modifiedTiles = map.GetModifiedTiles();
            writer.Write(modifiedTiles.Count);

            foreach (var tile in modifiedTiles)
            {
                writer.Write(tile.GridX);
                writer.Write(tile.GridY);
                writer.Write(tile.GridZ);
                writer.Write(tile.IsDestroyed);
                writer.Write(tile.CoverLevel);

                // Tile effects
                var effects = tile.GetTileEffects();
                writer.Write(effects.Count);
                foreach (var effect in effects)
                {
                    writer.Write(effect.Template.name);
                    writer.Write(effect.RemainingDuration);
                    writer.Write(effect.Intensity);
                }
            }
        }

        public void Load(BinaryReader reader, LoadContext context)
        {
            bool hasMap = reader.ReadBoolean();
            if (!hasMap) return;

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();

            // Validate map dimensions match
            var map = TacticalMap.Instance;
            if (map == null || map.Width != width || map.Height != height)
            {
                // Map mismatch - skip tile data
                SkipTileData(reader);
                return;
            }

            int tileCount = reader.ReadInt32();
            for (int i = 0; i < tileCount; i++)
            {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                int z = reader.ReadInt32();
                bool destroyed = reader.ReadBoolean();
                int coverLevel = reader.ReadInt32();

                var tile = map.GetTile(x, y, z);
                if (tile != null)
                {
                    tile.IsDestroyed = destroyed;
                    tile.CoverLevel = coverLevel;
                }

                // Tile effects
                int effectCount = reader.ReadInt32();
                for (int j = 0; j < effectCount; j++)
                {
                    string effectName = reader.ReadString();
                    int duration = reader.ReadInt32();
                    float intensity = reader.ReadSingle();

                    if (tile != null)
                    {
                        effectName = TemplateRedirector.GetRedirect(effectName, out bool ignore);
                        if (!ignore)
                        {
                            tile.AddTileEffectByName(effectName, duration, intensity);
                        }
                    }
                }
            }
        }

        private void SkipTileData(BinaryReader reader)
        {
            int tileCount = reader.ReadInt32();
            for (int i = 0; i < tileCount; i++)
            {
                reader.ReadInt32(); // x
                reader.ReadInt32(); // y
                reader.ReadInt32(); // z
                reader.ReadBoolean(); // destroyed
                reader.ReadInt32(); // coverLevel

                int effectCount = reader.ReadInt32();
                for (int j = 0; j < effectCount; j++)
                {
                    reader.ReadString();
                    reader.ReadInt32();
                    reader.ReadSingle();
                }
            }
        }
    }

    // =========================================================================
    // CAMPAIGN PROCESSOR
    // =========================================================================

    /// <summary>
    /// Saves and loads campaign/strategy layer state.
    ///
    /// Saves:
    /// - Current day/time
    /// - Planet positions
    /// - Operation progress
    /// - Faction relationships
    /// - Ship upgrades
    ///
    /// Address: 0x18071c000
    /// </summary>
    public class CampaignProcessor : ISaveProcessor
    {
        public string ProcessorId => "campaign";
        public int Priority => 10;

        public void Save(BinaryWriter writer, SaveContext context)
        {
            var campaign = CampaignManager.Instance;

            // Time
            writer.Write(campaign.CurrentDay);
            writer.Write(campaign.CurrentPlanet?.name ?? "");

            // Completed operations
            writer.Write(campaign.CompletedOperations.Count);
            foreach (var op in campaign.CompletedOperations)
            {
                writer.Write(op);
            }

            // Active operations
            SaveActiveOperations(writer, campaign.ActiveOperations);

            // Faction states
            SaveFactionStates(writer, campaign.Factions);

            // Ship upgrades
            writer.Write(campaign.ShipUpgrades.Count);
            foreach (var upgrade in campaign.ShipUpgrades)
            {
                writer.Write(upgrade);
            }

            // Resources
            SaveResources(writer, campaign.Resources);
        }

        public void Load(BinaryReader reader, LoadContext context)
        {
            var campaign = CampaignManager.Instance;

            // Time
            campaign.CurrentDay = reader.ReadInt32();
            string planetName = reader.ReadString();
            campaign.SetPlanet(planetName);

            // Completed operations
            int completedCount = reader.ReadInt32();
            campaign.CompletedOperations.Clear();
            for (int i = 0; i < completedCount; i++)
            {
                campaign.CompletedOperations.Add(reader.ReadString());
            }

            // Active operations
            LoadActiveOperations(reader, campaign);

            // Faction states
            LoadFactionStates(reader, campaign.Factions);

            // Ship upgrades
            int upgradeCount = reader.ReadInt32();
            campaign.ShipUpgrades.Clear();
            for (int i = 0; i < upgradeCount; i++)
            {
                string upgrade = reader.ReadString();
                upgrade = TemplateRedirector.GetRedirect(upgrade, out bool ignore);
                if (!ignore)
                {
                    campaign.ShipUpgrades.Add(upgrade);
                }
            }

            // Resources
            LoadResources(reader, campaign.Resources);
        }

        private void SaveActiveOperations(BinaryWriter writer, List<Operation> operations)
        {
            writer.Write(operations.Count);
            foreach (var op in operations)
            {
                writer.Write(op.Template.name);
                writer.Write(op.Progress);
                writer.Write(op.AssignedUnits.Count);
                foreach (var unit in op.AssignedUnits)
                {
                    writer.Write(unit.UniqueId);
                }
            }
        }

        private void LoadActiveOperations(BinaryReader reader, CampaignManager campaign)
        {
            int count = reader.ReadInt32();
            campaign.ActiveOperations.Clear();

            for (int i = 0; i < count; i++)
            {
                string templateName = reader.ReadString();
                int progress = reader.ReadInt32();
                int unitCount = reader.ReadInt32();

                var unitIds = new List<string>();
                for (int j = 0; j < unitCount; j++)
                {
                    unitIds.Add(reader.ReadString());
                }

                templateName = TemplateRedirector.GetRedirect(templateName, out bool ignore);
                if (!ignore)
                {
                    campaign.StartOperation(templateName, progress, unitIds);
                }
            }
        }

        private void SaveFactionStates(BinaryWriter writer, List<Faction> factions)
        {
            writer.Write(factions.Count);
            foreach (var faction in factions)
            {
                writer.Write(faction.Template.name);
                writer.Write(faction.Relationship);
                writer.Write(faction.IsDiscovered);

                // Known units
                writer.Write(faction.KnownUnits.Count);
                foreach (var unit in faction.KnownUnits)
                {
                    writer.Write(unit.UniqueId);
                }
            }
        }

        private void LoadFactionStates(BinaryReader reader, List<Faction> factions)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string factionName = reader.ReadString();
                int relationship = reader.ReadInt32();
                bool discovered = reader.ReadBoolean();

                int unitCount = reader.ReadInt32();
                var unitIds = new List<string>();
                for (int j = 0; j < unitCount; j++)
                {
                    unitIds.Add(reader.ReadString());
                }

                var faction = factions.Find(f => f.Template.name == factionName);
                if (faction != null)
                {
                    faction.Relationship = relationship;
                    faction.IsDiscovered = discovered;
                    // Restore known units after entities are loaded
                }
            }
        }

        private void SaveResources(BinaryWriter writer, Dictionary<string, int> resources)
        {
            writer.Write(resources.Count);
            foreach (var kvp in resources)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        private void LoadResources(BinaryReader reader, Dictionary<string, int> resources)
        {
            resources.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                int value = reader.ReadInt32();
                resources[key] = value;
            }
        }
    }

    // =========================================================================
    // PLACEHOLDER TYPES
    // =========================================================================

    public partial class Entity
    {
        public int HitpointsMax;
        public int MaxAP;
        public float CurrentSuppression;
        public float CurrentMorale;
        public SkillContainer SkillContainer;
        public void SetPosition(Tile tile) { }
    }

    public partial class EntityProperties
    {
        public Dictionary<string, float> GetModifiedProperties() => new();
        public void SetProperty(string name, float value) { }
    }

    public partial class StatusContainer
    {
        public List<StatusInstance> GetActiveStatuses() => new();
        public void AddStatusByName(string name, int duration, int stacks) { }
    }

    public class StatusInstance
    {
        public StatusTemplate Template;
        public int RemainingDuration;
        public int Stacks;
    }

    public class StatusTemplate
    {
        public string name;
    }

    public partial class EquipmentContainer
    {
        public List<EquipmentSlot> GetAllSlots() => new();
        public void EquipByName(string slot, string item) { }
    }

    public class EquipmentSlot
    {
        public EquipmentSlotType SlotType;
        public Item Item;
    }

    public enum EquipmentSlotType { Weapon, Armor, Accessory }

    public partial class Item
    {
        public ItemTemplate Template;
    }

    public class ItemTemplate
    {
        public string name;
    }

    public partial class SkillContainer
    {
        public List<Skill> GetAllSkills() => new();
        public Skill GetSkillByName(string name) => null;
    }

    public class Skill
    {
        public SkillTemplate Template;
        public int RemainingCooldown;
    }

    public class SkillTemplate
    {
        public string name;
    }

    public partial class Tile
    {
        public int GridX;
        public int GridY;
        public int GridZ;
        public bool IsDestroyed;
        public int CoverLevel;
        public List<TileEffectInstance> GetTileEffects() => new();
        public void AddTileEffectByName(string name, int duration, float intensity) { }
    }

    public class TileEffectInstance
    {
        public TileEffectTemplate Template;
        public int RemainingDuration;
        public float Intensity;
    }

    public class TileEffectTemplate
    {
        public string name;
    }

    public class TacticalMap
    {
        public static TacticalMap Instance;
        public int Width;
        public int Height;
        public List<Tile> GetModifiedTiles() => new();
        public Tile GetTile(int x, int y, int z) => null;
    }

    public partial class CampaignManager
    {
        public Dictionary<string, int> Resources;
        public void StartOperation(string template, int progress, List<string> unitIds) { }
    }

    public partial class Operation
    {
        public OperationTemplate Template;
        public int Progress;
        public List<Entity> AssignedUnits;
    }

    public class OperationTemplate
    {
        public string name;
    }

    public partial class Faction
    {
        public FactionTemplate Template;
        public int Relationship;
        public bool IsDiscovered;
        public List<Entity> KnownUnits;
    }

    public class FactionTemplate
    {
        public string name;
    }
}
