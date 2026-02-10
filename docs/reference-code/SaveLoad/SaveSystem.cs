// =============================================================================
// MENACE REFERENCE CODE - Save/Load System
// =============================================================================
// How game state is serialized and deserialized for saving/loading.
// The game uses a custom binary format with versioned processors.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Menace.SaveLoad
{
    // =========================================================================
    // SAVE MANAGER
    // =========================================================================

    /// <summary>
    /// Main entry point for save/load operations.
    ///
    /// Save files are stored in:
    ///   Windows: %USERPROFILE%/AppData/LocalLow/[Publisher]/[GameName]/Saves/
    ///   Linux: ~/.config/unity3d/[Publisher]/[GameName]/Saves/
    ///
    /// Address: 0x180712000 (class start)
    /// </summary>
    public class SaveManager
    {
        // =====================================================================
        // SINGLETON
        // =====================================================================

        private static SaveManager s_Instance;
        public static SaveManager Instance => s_Instance ??= new SaveManager();

        // =====================================================================
        // PATHS
        // =====================================================================

        /// <summary>
        /// Gets the save directory path.
        ///
        /// Address: 0x180712100
        /// </summary>
        public static string GetSaveDirectory()
        {
            return Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "Saves");
        }

        /// <summary>
        /// Gets full path for a save file.
        ///
        /// Address: 0x180712180
        /// </summary>
        public static string GetSavePath(string saveName)
        {
            return Path.Combine(GetSaveDirectory(), saveName + ".sav");
        }

        // =====================================================================
        // SAVE OPERATIONS
        // =====================================================================

        /// <summary>
        /// Saves the current game state.
        ///
        /// Address: 0x180712300
        /// </summary>
        /// <param name="saveName">Name for the save file</param>
        /// <param name="isAutoSave">Is this an autosave?</param>
        /// <returns>True if save succeeded</returns>
        public bool SaveGame(string saveName, bool isAutoSave = false)
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(GetSaveDirectory());

                // Create save state
                var saveState = new SaveState
                {
                    Header = CreateHeader(saveName, isAutoSave),
                    Campaign = SaveCampaignState(),
                    Tactical = SaveTacticalState(),
                    Entities = SaveEntityStates(),
                    Inventory = SaveInventoryState()
                };

                // Serialize to bytes
                byte[] data = SaveSerializer.Serialize(saveState);

                // Write to file
                string path = GetSavePath(saveName);
                File.WriteAllBytes(path, data);

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a game from save file.
        ///
        /// Address: 0x180712600
        /// </summary>
        /// <param name="saveName">Name of save file</param>
        /// <returns>True if load succeeded</returns>
        public bool LoadGame(string saveName)
        {
            try
            {
                string path = GetSavePath(saveName);
                if (!File.Exists(path))
                {
                    UnityEngine.Debug.LogError($"Save file not found: {path}");
                    return false;
                }

                // Read file
                byte[] data = File.ReadAllBytes(path);

                // Deserialize
                var saveState = SaveSerializer.Deserialize(data);

                // Validate version
                if (!ValidateVersion(saveState.Header))
                {
                    UnityEngine.Debug.LogError("Save file version incompatible");
                    return false;
                }

                // Restore state
                RestoreCampaignState(saveState.Campaign);
                RestoreTacticalState(saveState.Tactical);
                RestoreEntityStates(saveState.Entities);
                RestoreInventoryState(saveState.Inventory);

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Load failed: {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        // SAVE STATE CREATION
        // =====================================================================

        private SaveHeader CreateHeader(string saveName, bool isAutoSave)
        {
            return new SaveHeader
            {
                Version = SaveVersion.Current,
                SaveName = saveName,
                Timestamp = DateTime.UtcNow,
                IsAutoSave = isAutoSave,
                PlayTime = GameSession.Instance.TotalPlayTime,
                CampaignDay = CampaignManager.Instance.CurrentDay,
                MissionName = TacticalManager.Instance?.CurrentMission?.name ?? ""
            };
        }

        private CampaignSaveState SaveCampaignState()
        {
            var campaign = CampaignManager.Instance;
            return new CampaignSaveState
            {
                CurrentDay = campaign.CurrentDay,
                CurrentPlanet = campaign.CurrentPlanet?.name,
                CompletedOperations = new List<string>(campaign.CompletedOperations),
                ActiveOperations = SaveOperations(campaign.ActiveOperations),
                FactionStates = SaveFactionStates(campaign.Factions),
                ShipUpgrades = new List<string>(campaign.ShipUpgrades)
            };
        }

        private TacticalSaveState SaveTacticalState()
        {
            var tactical = TacticalManager.Instance;
            if (tactical == null || tactical.CurrentMission == null)
                return null;

            return new TacticalSaveState
            {
                MissionTemplate = tactical.CurrentMission.name,
                CurrentTurn = tactical.CurrentTurn,
                TurnPhase = tactical.CurrentPhase,
                TileStates = SaveTileStates(tactical.Map),
                DeployedUnits = SaveDeployedUnits(tactical.AllUnits)
            };
        }

        private List<EntitySaveState> SaveEntityStates()
        {
            var states = new List<EntitySaveState>();

            foreach (var entity in EntityManager.Instance.AllEntities)
            {
                states.Add(new EntitySaveState
                {
                    EntityId = entity.UniqueId,
                    TemplateName = entity.Template.name,
                    Hitpoints = entity.Hitpoints,
                    Position = SavePosition(entity.CurrentTile),
                    StatusEffects = SaveStatuses(entity.StatusContainer),
                    Properties = SaveProperties(entity.Properties),
                    EquippedItems = SaveEquipment(entity.Equipment)
                });
            }

            return states;
        }

        private InventorySaveState SaveInventoryState()
        {
            var inventory = InventoryManager.Instance;
            return new InventorySaveState
            {
                Items = SaveInventoryItems(inventory.AllItems),
                Resources = new Dictionary<string, int>(inventory.Resources)
            };
        }

        // =====================================================================
        // STATE RESTORATION
        // =====================================================================

        private bool ValidateVersion(SaveHeader header)
        {
            // Check major version compatibility
            return header.Version.Major == SaveVersion.Current.Major;
        }

        private void RestoreCampaignState(CampaignSaveState state)
        {
            var campaign = CampaignManager.Instance;
            campaign.CurrentDay = state.CurrentDay;
            campaign.SetPlanet(state.CurrentPlanet);
            campaign.CompletedOperations = new HashSet<string>(state.CompletedOperations);
            RestoreOperations(state.ActiveOperations);
            RestoreFactionStates(state.FactionStates);
            campaign.ShipUpgrades = new List<string>(state.ShipUpgrades);
        }

        private void RestoreTacticalState(TacticalSaveState state)
        {
            if (state == null) return;

            var tactical = TacticalManager.Instance;
            tactical.LoadMission(state.MissionTemplate);
            tactical.CurrentTurn = state.CurrentTurn;
            tactical.CurrentPhase = state.TurnPhase;
            RestoreTileStates(state.TileStates);
            RestoreDeployedUnits(state.DeployedUnits);
        }

        private void RestoreEntityStates(List<EntitySaveState> states)
        {
            foreach (var state in states)
            {
                var entity = EntityManager.Instance.GetOrCreateEntity(
                    state.EntityId,
                    state.TemplateName);

                entity.Hitpoints = state.Hitpoints;
                RestorePosition(entity, state.Position);
                RestoreStatuses(entity.StatusContainer, state.StatusEffects);
                RestoreProperties(entity.Properties, state.Properties);
                RestoreEquipment(entity.Equipment, state.EquippedItems);
            }
        }

        private void RestoreInventoryState(InventorySaveState state)
        {
            var inventory = InventoryManager.Instance;
            inventory.Clear();
            RestoreInventoryItems(state.Items);
            inventory.Resources = new Dictionary<string, int>(state.Resources);
        }

        // =====================================================================
        // HELPER METHODS (stubs)
        // =====================================================================

        private List<OperationSaveState> SaveOperations(List<Operation> operations) => new();
        private void RestoreOperations(List<OperationSaveState> states) { }
        private Dictionary<string, FactionSaveState> SaveFactionStates(List<Faction> factions) => new();
        private void RestoreFactionStates(Dictionary<string, FactionSaveState> states) { }
        private List<TileSaveState> SaveTileStates(TacticalMap map) => new();
        private void RestoreTileStates(List<TileSaveState> states) { }
        private List<string> SaveDeployedUnits(List<Entity> units) => new();
        private void RestoreDeployedUnits(List<string> unitIds) { }
        private Vector3Save SavePosition(Tile tile) => new();
        private void RestorePosition(Entity entity, Vector3Save pos) { }
        private List<StatusSaveState> SaveStatuses(StatusContainer container) => new();
        private void RestoreStatuses(StatusContainer container, List<StatusSaveState> states) { }
        private Dictionary<string, float> SaveProperties(EntityProperties props) => new();
        private void RestoreProperties(EntityProperties props, Dictionary<string, float> saved) { }
        private List<string> SaveEquipment(EquipmentContainer equipment) => new();
        private void RestoreEquipment(EquipmentContainer equipment, List<string> items) { }
        private List<ItemSaveState> SaveInventoryItems(List<Item> items) => new();
        private void RestoreInventoryItems(List<ItemSaveState> states) { }
    }

    // =========================================================================
    // SAVE STATE STRUCTURES
    // =========================================================================

    /// <summary>
    /// Root save state containing all game data.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        /// <summary>Save metadata. Offset: +0x10</summary>
        public SaveHeader Header;

        /// <summary>Campaign/strategy layer. Offset: +0x18</summary>
        public CampaignSaveState Campaign;

        /// <summary>Tactical combat state (if in mission). Offset: +0x20</summary>
        public TacticalSaveState Tactical;

        /// <summary>All entity states. Offset: +0x28</summary>
        public List<EntitySaveState> Entities;

        /// <summary>Global inventory. Offset: +0x30</summary>
        public InventorySaveState Inventory;
    }

    /// <summary>
    /// Save file header with metadata.
    /// </summary>
    [Serializable]
    public class SaveHeader
    {
        /// <summary>Save format version. Offset: +0x10</summary>
        public SaveVersion Version;

        /// <summary>Display name. Offset: +0x18</summary>
        public string SaveName;

        /// <summary>When save was created. Offset: +0x20</summary>
        public DateTime Timestamp;

        /// <summary>Is this an autosave? Offset: +0x28</summary>
        public bool IsAutoSave;

        /// <summary>Total play time. Offset: +0x30</summary>
        public TimeSpan PlayTime;

        /// <summary>Campaign day number. Offset: +0x38</summary>
        public int CampaignDay;

        /// <summary>Current mission (if in tactical). Offset: +0x40</summary>
        public string MissionName;
    }

    /// <summary>
    /// Save format version for compatibility.
    /// </summary>
    [Serializable]
    public struct SaveVersion
    {
        public int Major;
        public int Minor;
        public int Patch;

        public static SaveVersion Current => new SaveVersion { Major = 1, Minor = 0, Patch = 0 };

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }

    /// <summary>
    /// Campaign/strategy layer state.
    /// </summary>
    [Serializable]
    public class CampaignSaveState
    {
        public int CurrentDay;
        public string CurrentPlanet;
        public List<string> CompletedOperations;
        public List<OperationSaveState> ActiveOperations;
        public Dictionary<string, FactionSaveState> FactionStates;
        public List<string> ShipUpgrades;
    }

    /// <summary>
    /// Tactical mission state.
    /// </summary>
    [Serializable]
    public class TacticalSaveState
    {
        public string MissionTemplate;
        public int CurrentTurn;
        public TurnPhase TurnPhase;
        public List<TileSaveState> TileStates;
        public List<string> DeployedUnits;
    }

    /// <summary>
    /// Individual entity state.
    /// </summary>
    [Serializable]
    public class EntitySaveState
    {
        public string EntityId;
        public string TemplateName;
        public int Hitpoints;
        public Vector3Save Position;
        public List<StatusSaveState> StatusEffects;
        public Dictionary<string, float> Properties;
        public List<string> EquippedItems;
    }

    /// <summary>
    /// Inventory state.
    /// </summary>
    [Serializable]
    public class InventorySaveState
    {
        public List<ItemSaveState> Items;
        public Dictionary<string, int> Resources;
    }

    // =========================================================================
    // SUB-STATE STRUCTURES
    // =========================================================================

    [Serializable]
    public class OperationSaveState
    {
        public string TemplateName;
        public int Progress;
        public List<string> AssignedUnits;
    }

    [Serializable]
    public class FactionSaveState
    {
        public string FactionName;
        public int Relationship;
        public List<string> KnownUnits;
    }

    [Serializable]
    public class TileSaveState
    {
        public int X;
        public int Y;
        public List<string> TileEffects;
        public bool IsDestroyed;
    }

    [Serializable]
    public class StatusSaveState
    {
        public string StatusTemplate;
        public int RemainingDuration;
        public int Stacks;
    }

    [Serializable]
    public class ItemSaveState
    {
        public string ItemTemplate;
        public int Quantity;
        public Dictionary<string, object> CustomData;
    }

    [Serializable]
    public struct Vector3Save
    {
        public float X;
        public float Y;
        public float Z;
    }

    public enum TurnPhase
    {
        PlayerTurn = 0,
        EnemyTurn = 1,
        Environment = 2
    }

    // =========================================================================
    // SERIALIZER
    // =========================================================================

    /// <summary>
    /// Handles binary serialization of save states.
    ///
    /// Uses a custom format:
    /// - 4 bytes: Magic number "MSAV"
    /// - 4 bytes: Data length
    /// - N bytes: Compressed data (LZ4 or similar)
    ///
    /// Address: 0x180715000 (class start)
    /// </summary>
    public static class SaveSerializer
    {
        private static readonly byte[] MagicNumber = { (byte)'M', (byte)'S', (byte)'A', (byte)'V' };

        /// <summary>
        /// Serializes save state to bytes.
        ///
        /// Address: 0x180715100
        /// </summary>
        public static byte[] Serialize(SaveState state)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Write magic number
            writer.Write(MagicNumber);

            // Serialize to JSON, then compress
            string json = SerializeToJson(state);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] compressed = Compress(jsonBytes);

            // Write length and data
            writer.Write(compressed.Length);
            writer.Write(compressed);

            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes bytes to save state.
        ///
        /// Address: 0x180715300
        /// </summary>
        public static SaveState Deserialize(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            // Validate magic number
            byte[] magic = reader.ReadBytes(4);
            for (int i = 0; i < 4; i++)
            {
                if (magic[i] != MagicNumber[i])
                    throw new InvalidDataException("Invalid save file format");
            }

            // Read and decompress
            int length = reader.ReadInt32();
            byte[] compressed = reader.ReadBytes(length);
            byte[] jsonBytes = Decompress(compressed);

            // Deserialize JSON
            string json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            return DeserializeFromJson(json);
        }

        private static string SerializeToJson(SaveState state)
        {
            // Uses Unity's JsonUtility or Newtonsoft.Json
            return UnityEngine.JsonUtility.ToJson(state, true);
        }

        private static SaveState DeserializeFromJson(string json)
        {
            return UnityEngine.JsonUtility.FromJson<SaveState>(json);
        }

        private static byte[] Compress(byte[] data)
        {
            // LZ4 or similar compression
            // For simplicity, returning uncompressed
            return data;
        }

        private static byte[] Decompress(byte[] data)
        {
            // LZ4 or similar decompression
            return data;
        }
    }

    // =========================================================================
    // TEMPLATE REDIRECTION (save compatibility)
    // =========================================================================

    /// <summary>
    /// Handles renamed/removed templates in old saves.
    ///
    /// When loading a save that references a template that no longer exists,
    /// this system can redirect to a replacement or skip gracefully.
    ///
    /// Address: 0x180716000
    /// </summary>
    public static class TemplateRedirector
    {
        private static Dictionary<string, TemplateRedirect> s_Redirects;

        static TemplateRedirector()
        {
            s_Redirects = new Dictionary<string, TemplateRedirect>();
            LoadRedirects();
        }

        private static void LoadRedirects()
        {
            // Load from Resources/Config/TemplateRedirects.json
            // Example redirects:
            // "OldWeapon_Rifle_v1" -> "StandardRifle"
            // "RemovedEnemy_Type" -> null (ignore)
        }

        /// <summary>
        /// Gets the redirected template name, or null if should be ignored.
        ///
        /// Address: 0x180716100
        /// </summary>
        public static string GetRedirect(string originalName, out bool shouldIgnore)
        {
            if (s_Redirects.TryGetValue(originalName, out var redirect))
            {
                shouldIgnore = redirect.Action == RedirectAction.Ignore;
                return redirect.NewName;
            }

            shouldIgnore = false;
            return originalName;
        }
    }

    public class TemplateRedirect
    {
        public string OldName;
        public string NewName;
        public RedirectAction Action;
    }

    public enum RedirectAction
    {
        Replace = 0,
        Ignore = 1
    }

    // =========================================================================
    // PLACEHOLDER TYPES
    // =========================================================================

    public class CampaignManager
    {
        public static CampaignManager Instance;
        public int CurrentDay;
        public Planet CurrentPlanet;
        public HashSet<string> CompletedOperations;
        public List<Operation> ActiveOperations;
        public List<Faction> Factions;
        public List<string> ShipUpgrades;
        public void SetPlanet(string name) { }
    }

    public class TacticalManager
    {
        public static TacticalManager Instance;
        public Mission CurrentMission;
        public int CurrentTurn;
        public TurnPhase CurrentPhase;
        public TacticalMap Map;
        public List<Entity> AllUnits;
        public void LoadMission(string name) { }
    }

    public class EntityManager
    {
        public static EntityManager Instance;
        public List<Entity> AllEntities;
        public Entity GetOrCreateEntity(string id, string template) => null;
    }

    public class InventoryManager
    {
        public static InventoryManager Instance;
        public List<Item> AllItems;
        public Dictionary<string, int> Resources;
        public void Clear() { }
    }

    public class GameSession
    {
        public static GameSession Instance;
        public TimeSpan TotalPlayTime;
    }

    public class Entity
    {
        public string UniqueId;
        public EntityTemplate Template;
        public int Hitpoints;
        public Tile CurrentTile;
        public StatusContainer StatusContainer;
        public EntityProperties Properties;
        public EquipmentContainer Equipment;
    }

    public class EntityTemplate { public string name; }
    public class Tile { }
    public class StatusContainer { }
    public class EntityProperties { }
    public class EquipmentContainer { }
    public class Planet { public string name; }
    public class Operation { }
    public class Faction { }
    public class Mission { public string name; }
    public class TacticalMap { }
    public class Item { }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    public static class Debug
    {
        public static void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj, bool prettyPrint) => "";
        public static T FromJson<T>(string json) => default;
    }
}
