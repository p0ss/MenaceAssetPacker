// =============================================================================
// MENACE REFERENCE CODE - Save System Architecture
// =============================================================================
// This file documents the actual save/load system as decompiled from the binary.
// The system uses a unified SaveState class with mode-based operation (save vs load).
// Individual game objects implement ProcessSaveState methods that use SaveState's
// Process* methods to serialize/deserialize their state.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Menace.Strategy
{
    // =========================================================================
    // SAVE STATE CLASS
    // =========================================================================

    /// <summary>
    /// Central save state manager that handles both saving and loading.
    /// Uses mode-based operation where the same code path handles both directions.
    ///
    /// Address: 0x1805a77e0 (.ctor)
    ///
    /// Key offsets:
    ///   +0x10: Stream reference
    ///   +0x18: Mode (1 = saving, 0 = loading)
    ///   +0x1c: Valid flag (bool)
    ///   +0x20: Version number (current version = 0x65 / 101)
    ///   +0x24: Save type/subversion
    ///   +0x28: DateTime (ticks)
    ///   +0x30: Planet name string
    ///   +0x38: Operation name string
    ///   +0x40: Completed missions count
    ///   +0x44: Total missions count
    ///   +0x48: GlobalDifficultyTemplate ID string
    ///   +0x50: Double (unknown purpose)
    ///   +0x58: Description string
    ///   +0x60: StrategyConfig ID string (version > 0x1b)
    ///   +0x68: Operation reference
    ///   +0x70: Screenshot path string
    ///   +0x78: BinaryWriter (when saving)
    ///   +0x80: BinaryReader (when loading)
    ///
    /// Version history:
    ///   - Valid range: 0x16 to 0x65 (22 to 101)
    ///   - 0x18 (24): Vehicle durability changed from int to float
    ///   - 0x19 (25): BaseUnitLeader attributes changed from int[] to float[]
    ///   - 0x1a (26): BaseUnitLeader attribute resize, OwnedItems int array added
    ///   - 0x1b (27): StrategyConfig ID field added
    ///   - 0x1c (28): Operation bool field added
    /// </summary>
    public class SaveState
    {
        // Field offsets
        public const int OFFSET_STREAM = 0x10;
        public const int OFFSET_MODE = 0x18;
        public const int OFFSET_VALID = 0x1c;
        public const int OFFSET_VERSION = 0x20;
        public const int OFFSET_SAVE_TYPE = 0x24;
        public const int OFFSET_DATETIME = 0x28;
        public const int OFFSET_PLANET_NAME = 0x30;
        public const int OFFSET_OPERATION_NAME = 0x38;
        public const int OFFSET_COMPLETED_MISSIONS = 0x40;
        public const int OFFSET_TOTAL_MISSIONS = 0x44;
        public const int OFFSET_DIFFICULTY_ID = 0x48;
        public const int OFFSET_DESCRIPTION = 0x58;
        public const int OFFSET_CONFIG_ID = 0x60;
        public const int OFFSET_OPERATION_REF = 0x68;
        public const int OFFSET_SCREENSHOT_PATH = 0x70;
        public const int OFFSET_WRITER = 0x78;
        public const int OFFSET_READER = 0x80;

        public const int CURRENT_VERSION = 0x65; // 101
        public const int MIN_VERSION = 0x16;     // 22
        public const int MAX_VERSION = 0x65;     // 101

        /// <summary>
        /// Returns true if currently loading (mode != 1).
        /// Address: 0x18055b340
        /// </summary>
        public bool IsLoading() => false; // mode at +0x18 == 0

        // =====================================================================
        // PRIMITIVE PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process an integer value.
        /// Address: 0x1805a6240
        ///
        /// If saving (mode == 1): writes value via Writer->Write(int)
        /// If loading: reads value via Reader->ReadInt32()
        /// </summary>
        public void ProcessInt(ref int value) { }

        /// <summary>
        /// Process a boolean value.
        /// Address: 0x1805a5460
        /// </summary>
        public void ProcessBool(ref bool value) { }

        /// <summary>
        /// Process a float value.
        /// Address: 0x1805a60c0
        /// </summary>
        public void ProcessFloat(ref float value) { }

        /// <summary>
        /// Process a double value.
        /// Address: 0x1805a5f30
        /// </summary>
        public void ProcessDouble(ref double value) { }

        /// <summary>
        /// Process a string value.
        /// Address: 0x1805a6930
        /// </summary>
        public void ProcessString(ref string value) { }

        /// <summary>
        /// Process an unsigned integer value.
        /// Address: 0x1805a6c40
        /// </summary>
        public void ProcessUInt(ref uint value) { }

        // =====================================================================
        // ARRAY PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process an integer array (fixed size).
        /// Address: 0x1805a6130
        /// </summary>
        public void ProcessIntArray(int[] array) { }

        /// <summary>
        /// Process a list of integers.
        /// Address: 0x1805a62a0 (variant 1)
        /// Address: 0x1805a6500 (variant 2)
        /// </summary>
        public void ProcessInts(List<int> list) { }

        /// <summary>
        /// Process a list of unsigned integers.
        /// Address: 0x1805a6ca0
        /// </summary>
        public void ProcessUInts(List<uint> list) { }

        /// <summary>
        /// Process a float array.
        /// Address: 0x1805a5fa0
        /// </summary>
        public void ProcessFloatArray(float[] array) { }

        /// <summary>
        /// Process a list of strings.
        /// Address: 0x1805a69a0
        /// </summary>
        public void ProcessStrings(List<string> list) { }

        // =====================================================================
        // STRUCT/VECTOR PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process a Vector2 (two floats).
        /// Address: 0x1805a70f0
        /// </summary>
        public void ProcessVector2(ref Vector2 value) { }

        /// <summary>
        /// Process a Vector2Int (two ints).
        /// Address: 0x1805a7020
        /// </summary>
        public void ProcessVector2Int(ref Vector2Int value) { }

        /// <summary>
        /// Process a Vector3 (three floats).
        /// Address: 0x1805a71c0
        /// </summary>
        public void ProcessVector3(ref Vector3 value) { }

        /// <summary>
        /// Process a RectInt (four ints).
        /// Address: 0x1805a6720
        /// </summary>
        public void ProcessRectInt(ref RectInt value) { }

        // =====================================================================
        // TEMPLATE PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process a single DataTemplate reference.
        /// Address: 0x180b83940
        ///
        /// Saving: Writes bool (exists), then template ID string
        /// Loading: Reads bool, then template ID, looks up via DataTemplateLoader
        /// </summary>
        public void ProcessDataTemplate<T>(ref T template) where T : DataTemplate { }

        /// <summary>
        /// Process a DataTemplate array.
        /// Address: 0x180b837b0
        /// </summary>
        public void ProcessDataTemplateArray<T>(T[] array) where T : DataTemplate { }

        /// <summary>
        /// Process a list of DataTemplates.
        /// Address: 0x180b83ac0 (variant 1)
        /// Address: 0x180b83d50 (variant 2)
        /// </summary>
        public void ProcessDataTemplates<T>(List<T> list) where T : DataTemplate { }

        /// <summary>
        /// Process a ConversationTemplate reference.
        /// Address: 0x1805a54d0
        /// </summary>
        public void ProcessConversationTemplate(ref ConversationTemplate template) { }

        /// <summary>
        /// Process a list of ConversationTemplates.
        /// Address: 0x1805a55b0
        /// </summary>
        public void ProcessConversationTemplates(List<ConversationTemplate> list) { }

        // =====================================================================
        // ENUM PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process a byte-sized enum.
        /// Address: 0x180b843e0
        /// </summary>
        public void ProcessEnum<T>(ref T value) where T : Enum { }

        /// <summary>
        /// Process an int-sized enum.
        /// Address: 0x180b84690
        /// </summary>
        public void ProcessEnumInt32<T>(ref T value) where T : Enum { }

        /// <summary>
        /// Process a list of int-sized enums.
        /// Address: 0x180b84ef0 (variant 1)
        /// Address: 0x180b85210 (variant 2)
        /// </summary>
        public void ProcessEnums<T>(List<T> list) where T : Enum { }

        // =====================================================================
        // OBJECT PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process a single object with ProcessSaveState method.
        /// Address: 0x180b85560
        /// </summary>
        public void ProcessObject<T>(ref T obj) where T : class { }

        /// <summary>
        /// Process a list of objects with ProcessSaveState methods.
        /// Address: 0x180b85660
        /// </summary>
        public void ProcessObjects<T>(List<T> list) where T : class { }

        /// <summary>
        /// Process a struct value.
        /// Address: 0x180b85ac0 (generic)
        /// Specialized versions:
        ///   - MissionResultStats: 0x180b85c90
        ///   - OperationResources: 0x180b85d20
        ///   - StrategicDuration: 0x180b85d90
        /// </summary>
        public void ProcessStruct<T>(ref T value) where T : struct { }

        // =====================================================================
        // DICTIONARY PROCESS METHODS
        // =====================================================================

        /// <summary>
        /// Process a dictionary.
        /// Address: 0x1805a5860 (variant 1)
        /// Address: 0x1805a5c50 (variant 2)
        /// Address: 0x180b83fe0 (generic)
        /// </summary>
        public void ProcessDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict) { }

        // =====================================================================
        // UTILITY METHODS
        // =====================================================================

        /// <summary>
        /// Read-only methods for direct reading without ref parameter.
        /// </summary>
        public bool ReadBool() => false;     // 0x1805a72e0
        public int ReadInt() => 0;            // 0x1805a73c0
        public float ReadFloat() => 0f;       // 0x1805a7390
        public string ReadString() => "";     // 0x1805a73f0

        /// <summary>
        /// Write-only methods for direct writing.
        /// </summary>
        public void WriteBool(bool value) { }     // 0x1805a7660
        public void WriteInt(int value) { }       // 0x1805a7780
        public void WriteFloat(float value) { }   // 0x1805a7750
        public void WriteString(string value) { } // 0x1805a77b0

        /// <summary>
        /// Check for corruption marker.
        /// Address: 0x1805a50d0
        /// Reads/writes a magic value to verify save integrity.
        /// </summary>
        public void CheckCorruption() { }

        /// <summary>
        /// Close the save state and flush streams.
        /// Address: 0x1805a51e0
        /// </summary>
        public void Close() { }

        /// <summary>
        /// Process an unused/deprecated DataTemplate (skips data).
        /// Address: 0x1805a6f00
        /// </summary>
        public void ProcessUnusedDataTemplate() { }

        /// <summary>
        /// Process unused/deprecated DataTemplates list (skips data).
        /// Address: 0x1805a6f70
        /// </summary>
        public void ProcessUnusedDataTemplates() { }
    }

    // =========================================================================
    // SQUADDIE SAVE STATE
    // =========================================================================

    /// <summary>
    /// Squaddie save state processing.
    /// Address: 0x1805c2130
    ///
    /// Squaddies represent crew members on the ship (not deployed soldiers).
    ///
    /// Field offsets:
    ///   +0x10: int - Squaddie ID
    ///   +0x14: Gender (ByteEnum)
    ///   +0x15: SkinColor (ByteEnum)
    ///   +0x18: HomePlanetType (Int32Enum)
    ///   +0x20: EntityTemplate reference
    ///   +0x28: string - First name
    ///   +0x30: string - Callsign/nickname
    ///   +0x38: int - Unknown (possibly age or experience)
    /// </summary>
    public partial class Squaddie
    {
        public const int OFFSET_ID = 0x10;
        public const int OFFSET_GENDER = 0x14;
        public const int OFFSET_SKIN_COLOR = 0x15;
        public const int OFFSET_HOME_PLANET = 0x18;
        public const int OFFSET_TEMPLATE = 0x20;
        public const int OFFSET_FIRST_NAME = 0x28;
        public const int OFFSET_CALLSIGN = 0x30;
        public const int OFFSET_UNKNOWN_INT = 0x38;

        public void ProcessSaveState(SaveState state)
        {
            // Process in this exact order:
            // state.ProcessInt(ref id);              // +0x10
            // state.ProcessEnum<Gender>(ref gender); // +0x14 (ByteEnum)
            // state.ProcessEnum<SkinColor>(ref skin);// +0x15 (ByteEnum)
            // state.ProcessEnumInt32<HomePlanetType>(ref planet); // +0x18 (Int32Enum)
            // state.ProcessString(ref firstName);    // +0x28
            // state.ProcessString(ref callsign);     // +0x30
            // state.ProcessInt(ref unknown);         // +0x38
            // state.ProcessDataTemplate<EntityTemplate>(ref template); // +0x20
        }
    }

    // =========================================================================
    // VEHICLE SAVE STATE
    // =========================================================================

    /// <summary>
    /// Vehicle save state processing.
    /// Address: 0x1805c8e60
    ///
    /// Field offsets:
    ///   +0x10: EntityTemplate reference (from base class)
    ///   +0x18: string - GUID
    ///   +0x20: float - Durability (was int before version 0x18)
    ///   +0x24: float - Unknown (possibly fuel or cargo)
    ///   +0x28: List of SkillTemplates
    ///
    /// Version migration:
    ///   Before 0x18: Durability was stored as int, converted to float 1.0f
    /// </summary>
    public partial class Vehicle
    {
        public const int OFFSET_TEMPLATE = 0x10;
        public const int OFFSET_GUID = 0x18;
        public const int OFFSET_DURABILITY = 0x20;
        public const int OFFSET_UNKNOWN_FLOAT = 0x24;
        public const int OFFSET_SKILLS = 0x28;

        public void ProcessSaveState(SaveState state)
        {
            // Version check for durability migration
            // if (version < 0x18) {
            //     int oldDurability = state.ReadInt();
            //     durability = 1.0f; // Reset to full
            // } else {
            //     state.ProcessFloat(ref durability); // +0x20
            // }
            // state.ProcessFloat(ref unknownFloat);  // +0x24
            // state.ProcessDataTemplates<SkillTemplate>(skillList); // +0x28
        }
    }

    // =========================================================================
    // OPERATION SAVE STATE
    // =========================================================================

    /// <summary>
    /// Operation save state processing.
    /// Address: 0x18058f9e0
    ///
    /// Operations are multi-mission campaign events.
    ///
    /// Field offsets:
    ///   +0x10: OperationTemplate reference
    ///   +0x18: StoryFactionTemplate reference
    ///   +0x20: FactionTemplate reference
    ///   +0x28: OperationResult object reference
    ///   +0x30: PlanetTemplate reference
    ///   +0x38: OperationDurationTemplate reference
    ///   +0x40: int - Unknown
    ///   +0x48: List of ints (mission indices)
    ///   +0x50: List of Mission objects
    ///   +0x58: int - Unknown
    ///   +0x5c: int - Unknown
    ///   +0x60: int - Unknown
    ///   +0x68: PseudoRandom object reference
    ///   +0x70: List of OperationAssetTemplates
    ///   +0x78: List of OperationAssetTemplates
    ///   +0x80: OperationProperties (calculated on load, not saved)
    ///   +0x9c: int - Unknown
    ///   +0xa0: bool - Unknown (version > 0x1c only)
    /// </summary>
    public partial class Operation
    {
        public const int OFFSET_OPERATION_TEMPLATE = 0x10;
        public const int OFFSET_STORY_FACTION_TEMPLATE = 0x18;
        public const int OFFSET_FACTION_TEMPLATE = 0x20;
        public const int OFFSET_OPERATION_RESULT = 0x28;
        public const int OFFSET_PLANET_TEMPLATE = 0x30;
        public const int OFFSET_DURATION_TEMPLATE = 0x38;
        public const int OFFSET_INT_40 = 0x40;
        public const int OFFSET_MISSION_INDICES = 0x48;
        public const int OFFSET_MISSIONS = 0x50;
        public const int OFFSET_INT_58 = 0x58;
        public const int OFFSET_INT_5C = 0x5c;
        public const int OFFSET_INT_60 = 0x60;
        public const int OFFSET_PSEUDO_RANDOM = 0x68;
        public const int OFFSET_ASSETS_1 = 0x70;
        public const int OFFSET_ASSETS_2 = 0x78;
        public const int OFFSET_PROPERTIES = 0x80;
        public const int OFFSET_INT_9C = 0x9c;
        public const int OFFSET_BOOL_A0 = 0xa0;

        public void ProcessSaveState(SaveState state)
        {
            // state.ProcessDataTemplate<OperationTemplate>(ref template);         // +0x10
            // state.ProcessDataTemplate<StoryFactionTemplate>(ref storyFaction);  // +0x18
            // state.ProcessDataTemplate<FactionTemplate>(ref faction);            // +0x20
            // state.ProcessObject<OperationResult>(ref result);                   // +0x28
            // state.ProcessDataTemplate<PlanetTemplate>(ref planet);              // +0x30
            // state.ProcessDataTemplate<OperationDurationTemplate>(ref duration); // +0x38
            // state.ProcessInt(ref int58);                                        // +0x58
            // state.ProcessInt(ref int5c);                                        // +0x5c
            // state.ProcessInt(ref int40);                                        // +0x40
            // state.ProcessInts(missionIndices);                                  // +0x48
            // state.ProcessInt(ref int60);                                        // +0x60
            // state.ProcessObject<PseudoRandom>(ref random);                      // +0x68
            // state.ProcessInt(ref int9c);                                        // +0x9c
            // state.ProcessDataTemplates<OperationAssetTemplate>(assets1);        // +0x70
            // state.ProcessDataTemplates<OperationAssetTemplate>(assets2);        // +0x78
            // if (version > 0x1c) {
            //     state.ProcessBool(ref boolA0);                                  // +0xa0
            // }
            // if (state.IsLoading()) {
            //     properties = new OperationProperties();                         // +0x80
            //     CalculateOperationProperties();
            // }
            // state.ProcessObjects<Mission>(missions);                            // +0x50
        }
    }

    // =========================================================================
    // MISSION SAVE STATE
    // =========================================================================

    /// <summary>
    /// Mission save state processing.
    /// Address: 0x1805866b0
    ///
    /// Field offsets:
    ///   +0x10: MissionTemplate reference
    ///   +0x18: int - Unknown
    ///   +0x1c: MissionLayer (Int32Enum)
    ///   +0x20: int - Unknown
    ///   +0x24: int - Unknown
    ///   +0x38: MissionDifficultyTemplate reference
    ///   +0x40: ObjectiveManager reference (used for objective states)
    ///   +0x48: Vector2 - Position
    ///   +0x50: Vector2Int - Grid position
    ///   +0x58: LightConditionType (Int32Enum)
    ///   +0x60: WeatherTemplate reference
    ///   +0x68: BiomeTemplate reference
    ///   +0x78: OperationAssetTemplate reference
    ///   +0x80: FactionTemplate reference
    ///   +0xa8: OperationResources struct
    ///   +0xb0: List of ints (next mission indices)
    ///   +0xb8: MissionStatus (Int32Enum)
    ///   +0xd0: RectInt - Map bounds
    /// </summary>
    public partial class Mission
    {
        public const int OFFSET_MISSION_TEMPLATE = 0x10;
        public const int OFFSET_INT_18 = 0x18;
        public const int OFFSET_MISSION_LAYER = 0x1c;
        public const int OFFSET_INT_20 = 0x20;
        public const int OFFSET_INT_24 = 0x24;
        public const int OFFSET_DIFFICULTY_TEMPLATE = 0x38;
        public const int OFFSET_OBJECTIVE_MANAGER = 0x40;
        public const int OFFSET_POSITION = 0x48;
        public const int OFFSET_GRID_POSITION = 0x50;
        public const int OFFSET_LIGHT_CONDITION = 0x58;
        public const int OFFSET_WEATHER_TEMPLATE = 0x60;
        public const int OFFSET_BIOME_TEMPLATE = 0x68;
        public const int OFFSET_ASSET_TEMPLATE = 0x78;
        public const int OFFSET_FACTION_TEMPLATE = 0x80;
        public const int OFFSET_OPERATION_RESOURCES = 0xa8;
        public const int OFFSET_NEXT_MISSIONS = 0xb0;
        public const int OFFSET_MISSION_STATUS = 0xb8;
        public const int OFFSET_MAP_BOUNDS = 0xd0;

        public void ProcessSaveState(SaveState state)
        {
            // state.ProcessDataTemplate<MissionTemplate>(ref template);           // +0x10
            // state.ProcessInt(ref int18);                                        // +0x18
            // state.ProcessEnumInt32<MissionLayer>(ref layer);                    // +0x1c
            // state.ProcessInt(ref int20);                                        // +0x20
            // state.ProcessInt(ref int24);                                        // +0x24
            // state.ProcessDataTemplate<MissionDifficultyTemplate>(ref difficulty); // +0x38
            // state.ProcessDataTemplate<FactionTemplate>(ref faction);            // +0x80
            // state.ProcessVector2(ref position);                                 // +0x48
            // state.ProcessVector2Int(ref gridPosition);                          // +0x50
            // state.ProcessEnumInt32<LightConditionType>(ref lightCondition);     // +0x58
            // state.ProcessDataTemplate<WeatherTemplate>(ref weather);            // +0x60
            // state.ProcessDataTemplate<BiomeTemplate>(ref biome);                // +0x68
            // state.ProcessDataTemplate<OperationAssetTemplate>(ref asset);       // +0x78
            // state.ProcessStruct<OperationResources>(ref resources);             // +0xa8
            // state.ProcessEnumInt32<MissionStatus>(ref status);                  // +0xb8
            // state.ProcessRectInt(ref mapBounds);                                // +0xd0
            //
            // if (state.IsLoading()) {
            //     List<int> nextMissions = new List<int>();
            //     state.ProcessInts(nextMissions);
            //     List<ObjectiveState> states = new List<ObjectiveState>();
            //     state.ProcessEnums<ObjectiveState>(states);
            //     Init();
            //     // Apply next missions and objective states
            // } else {
            //     state.ProcessInts(nextMissionsList);                            // +0xb0
            //     // Collect and save objective states from ObjectiveManager
            // }
        }
    }

    // =========================================================================
    // STORY FACTION SAVE STATE
    // =========================================================================

    /// <summary>
    /// StoryFaction save state processing.
    /// Address: 0x1805929c0
    ///
    /// Field offsets:
    ///   +0x10: StoryFactionTemplate reference
    ///   +0x18: int - Relationship value
    ///   +0x1c: StoryFactionStatus (Int32Enum)
    ///   +0x20: List of ShipUpgradeTemplates
    ///   +0x28: HashSet of ints
    /// </summary>
    public partial class StoryFaction
    {
        public const int OFFSET_TEMPLATE = 0x10;
        public const int OFFSET_RELATIONSHIP = 0x18;
        public const int OFFSET_STATUS = 0x1c;
        public const int OFFSET_SHIP_UPGRADES = 0x20;
        public const int OFFSET_INT_SET = 0x28;

        public void ProcessSaveState(SaveState state)
        {
            // state.ProcessDataTemplate<StoryFactionTemplate>(ref template);      // +0x10
            // state.ProcessInt(ref relationship);                                 // +0x18
            // state.ProcessEnumInt32<StoryFactionStatus>(ref status);             // +0x1c
            // state.ProcessDataTemplates<ShipUpgradeTemplate>(shipUpgrades);      // +0x20
            // state.ProcessInts(intSet);                                          // +0x28 (HashSet as List)
        }
    }

    // =========================================================================
    // STORY FACTIONS SAVE STATE (COLLECTION)
    // =========================================================================

    /// <summary>
    /// StoryFactions collection save state processing.
    /// Address: 0x1805937d0
    ///
    /// Field offsets:
    ///   +0x10: Dictionary of StoryFactionType -> StoryFaction
    /// </summary>
    public partial class StoryFactions
    {
        public void ProcessSaveState(SaveState state)
        {
            // if (state.IsLoading()) {
            //     dictionary.Clear();
            //     int count = state.ReadInt();
            //     for (int i = 0; i < count; i++) {
            //         StoryFaction faction = new StoryFaction();
            //         state.ProcessObject<StoryFaction>(ref faction);
            //         dictionary[faction.Template.Type] = faction;
            //     }
            // } else {
            //     state.WriteInt(dictionary.Count);
            //     foreach (var faction in dictionary.Values) {
            //         state.ProcessObject<StoryFaction>(ref faction);
            //     }
            // }
        }
    }

    // =========================================================================
    // BASE UNIT LEADER SAVE STATE
    // =========================================================================

    /// <summary>
    /// BaseUnitLeader save state processing.
    /// Address: 0x1805b2300
    ///
    /// This is the base class for playable unit leaders (soldiers).
    ///
    /// Field offsets (via array indexing, each element is 8 bytes):
    ///   [2] / +0x10: UnitLeaderTemplate reference
    ///   [3] / +0x18: LeaderHealthStatus (ByteEnum)
    ///   [6] / +0x30: Attributes object
    ///     Attributes +0x10: float[] array (7 elements)
    ///   [7] / +0x38: SkillContainer
    ///   [8] / +0x40: ItemContainer (Equipment)
    ///   [9] / +0x48: List of PerkTemplates
    ///   [10] / +0x50: UnitStatistics object
    ///   [11] / +0x58: EmotionalStates object
    ///   [12] / +0x60: List of ints
    ///   [13] / +0x68: StrategicDuration struct
    ///   [14] / +0x70: ConversationTemplate reference
    ///
    /// Version migration:
    ///   Before 0x19: Attributes were int[], converted to float[] by dividing by 100.0f
    ///   At 0x1a: Attributes array resized to 7 elements
    /// </summary>
    public partial class BaseUnitLeader
    {
        public const int OFFSET_TEMPLATE = 0x10;
        public const int OFFSET_HEALTH_STATUS = 0x18;
        public const int OFFSET_ATTRIBUTES = 0x30;
        public const int OFFSET_SKILL_CONTAINER = 0x38;
        public const int OFFSET_ITEM_CONTAINER = 0x40;
        public const int OFFSET_PERKS = 0x48;
        public const int OFFSET_STATISTICS = 0x50;
        public const int OFFSET_EMOTIONAL_STATES = 0x58;
        public const int OFFSET_INT_LIST = 0x60;
        public const int OFFSET_STRATEGIC_DURATION = 0x68;
        public const int OFFSET_CONVERSATION = 0x70;

        public const int ATTRIBUTE_COUNT = 7;
        public const float ATTRIBUTE_CONVERSION_DIVISOR = 100.0f;

        public void ProcessSaveState(SaveState state)
        {
            // Attributes object at +0x30, array at Attributes+0x10
            // if (version < 0x1a) {
            //     if (version > 0x18) {
            //         int[] intArray = new int[7];
            //         state.ProcessIntArray(intArray);
            //         // Convert each int to float by dividing by 100.0f
            //         for (int i = 0; i < attributes.Length; i++) {
            //             attributes[i] = intArray[i] / 100.0f;
            //         }
            //     }
            // } else {
            //     state.ProcessFloatArray(attributes);
            // }
            // ArrayHelper.Resize(ref attributes, 7);
            //
            // state.ProcessDataTemplates<PerkTemplate>(perks);                    // +0x48
            // ItemContainer.ProcessSaveState(state);                              // +0x40
            // state.ProcessObject<UnitStatistics>(ref statistics);                // +0x50
            // state.ProcessObject<EmotionalStates>(ref emotionalStates);          // +0x58
            // state.ProcessStruct<StrategicDuration>(ref duration);               // +0x68
            // state.ProcessConversationTemplate(ref conversation);                // +0x70
            // state.ProcessEnum<LeaderHealthStatus>(ref healthStatus);            // +0x18
            // state.ProcessInts(intList);                                         // +0x60
            //
            // if (state.IsLoading()) {
            //     emotionalStates.Owner = this;
            //     // Re-add skills from emotional states
            //     // Re-add skills from perks (if different from default template perk)
            //     OnLoaded(); // Virtual call
            // }
        }
    }

    // =========================================================================
    // ITEM CONTAINER SAVE STATE
    // =========================================================================

    /// <summary>
    /// ItemContainer save state processing.
    /// Address: 0x180824480
    ///
    /// Saves item references by GUID, not full item data.
    /// Items are looked up from OwnedItems on load.
    ///
    /// Field offsets:
    ///   +0x10: Array of item slot lists
    ///
    /// Save format:
    ///   int - slot count (always 11)
    ///   For each slot:
    ///     int - slot index
    ///     int - item count in slot
    ///     For each item:
    ///       string - item GUID (or "NULL" marker)
    /// </summary>
    public partial class ItemContainer
    {
        public const int SLOT_COUNT = 11;
        public const string NULL_MARKER = "NULL";

        public void ProcessSaveState(SaveState state)
        {
            // if (!state.IsLoading()) {
            //     state.WriteInt(SLOT_COUNT);
            //     for (int slot = 0; slot < SLOT_COUNT; slot++) {
            //         state.WriteInt(slot);
            //         state.WriteInt(items[slot].Count);
            //         foreach (var item in items[slot]) {
            //             state.WriteString(item?.Guid ?? "NULL");
            //         }
            //     }
            // } else {
            //     OwnedItems ownedItems = StrategyState.Instance.OwnedItems;
            //     int slotCount = state.ReadInt();
            //     for (int i = 0; i < slotCount; i++) {
            //         int slot = state.ReadInt();
            //         int itemCount = state.ReadInt();
            //         for (int j = 0; j < itemCount; j++) {
            //             string guid = state.ReadString();
            //             if (guid != "NULL") {
            //                 Item item = ownedItems.GetItemByGuid(guid);
            //                 if (item is Item) {
            //                     Place(item, slot);
            //                 } else {
            //                     Remove(slot, j, silent: true);
            //                 }
            //             }
            //         }
            //     }
            // }
        }
    }

    // =========================================================================
    // SHIP UPGRADES SAVE STATE
    // =========================================================================

    /// <summary>
    /// ShipUpgrades save state processing.
    /// Address: 0x1805ac430
    ///
    /// Field offsets:
    ///   +0x10: ShipUpgradeTemplate[] array (available upgrades)
    ///   +0x18: int[] array (unknown purpose)
    ///   +0x20: int[] array (purchase counts)
    ///   +0x28: Dictionary of ShipUpgradeTemplate -> int (owned counts)
    ///   +0x30: List of ShipUpgradeTemplates (unlocked)
    /// </summary>
    public partial class ShipUpgrades
    {
        public const int OFFSET_AVAILABLE_ARRAY = 0x10;
        public const int OFFSET_INT_ARRAY_18 = 0x18;
        public const int OFFSET_PURCHASE_COUNTS = 0x20;
        public const int OFFSET_OWNED_DICT = 0x28;
        public const int OFFSET_UNLOCKED_LIST = 0x30;

        public void ProcessSaveState(SaveState state)
        {
            // state.ProcessDataTemplateArray<ShipUpgradeTemplate>(availableArray); // +0x10
            // state.ProcessDataTemplates<ShipUpgradeTemplate>(unlockedList);       // +0x30
            // state.ProcessIntArray(purchaseCounts);                               // +0x20
            //
            // if (!state.IsLoading()) {
            //     int count = ownedDict.Count;
            //     state.WriteInt(count);
            //     foreach (var kvp in ownedDict) {
            //         state.ProcessDataTemplate<ShipUpgradeTemplate>(ref kvp.Key);
            //         state.WriteInt(kvp.Value);
            //     }
            // } else {
            //     ownedDict.Clear();
            //     int count = state.ReadInt();
            //     for (int i = 0; i < count; i++) {
            //         ShipUpgradeTemplate template = null;
            //         state.ProcessDataTemplate<ShipUpgradeTemplate>(ref template);
            //         int owned = state.ReadInt();
            //         ownedDict[template] = owned;
            //     }
            // }
        }
    }

    // =========================================================================
    // OWNED ITEMS SAVE STATE
    // =========================================================================

    /// <summary>
    /// OwnedItems save state processing.
    /// Address: 0x18059d4e0
    ///
    /// Manages all items and vehicles owned by the player.
    ///
    /// Field offsets:
    ///   +0x10: Dictionary of BaseItemTemplate -> List of BaseItems
    ///   +0x18: List of BaseItemTemplates (recently seen/used)
    ///   +0x20: List of Vehicles
    ///   +0x28: int[] array (version > 0x1a only)
    /// </summary>
    public partial class OwnedItems
    {
        public const int OFFSET_ITEM_DICT = 0x10;
        public const int OFFSET_RECENT_TEMPLATES = 0x18;
        public const int OFFSET_VEHICLES = 0x20;
        public const int OFFSET_INT_ARRAY = 0x28;

        public void ProcessSaveState(SaveState state)
        {
            // if (version > 0x1a) {
            //     state.ProcessIntArray(intArray);                                // +0x28
            // }
            //
            // if (!state.IsLoading()) {
            //     // Save vehicles
            //     state.WriteInt(vehicles.Count);
            //     foreach (var vehicle in vehicles) {
            //         state.ProcessDataTemplate<EntityTemplate>(ref vehicle.Template);
            //         state.WriteString(vehicle.Guid);
            //         vehicle.ProcessSaveState(state);
            //     }
            //     // Save item dictionary
            //     state.WriteInt(itemDict.Count);
            //     foreach (var kvp in itemDict) {
            //         state.WriteDataTemplate(kvp.Key);
            //         state.WriteInt(kvp.Value.Count);
            //         foreach (var item in kvp.Value) {
            //             state.WriteString(item.Guid);
            //         }
            //     }
            // } else {
            //     // Load vehicles
            //     vehicles.Clear();
            //     int vehicleCount = state.ReadInt();
            //     for (int i = 0; i < vehicleCount; i++) {
            //         EntityTemplate template = null;
            //         state.ProcessDataTemplate<EntityTemplate>(ref template);
            //         string guid = state.ReadString();
            //         Vehicle vehicle = new Vehicle(template, guid);
            //         vehicle.ProcessSaveState(state);
            //         if (template != null) {
            //             vehicles.Add(vehicle);
            //         }
            //     }
            //     // Clear item lists in dictionary
            //     foreach (var list in itemDict.Values) {
            //         list.Clear();
            //     }
            //     // Load items
            //     int templateCount = state.ReadInt();
            //     for (int i = 0; i < templateCount; i++) {
            //         BaseItemTemplate template = state.ReadDataTemplate<BaseItemTemplate>();
            //         int itemCount = state.ReadInt();
            //         for (int j = 0; j < itemCount; j++) {
            //             string guid = state.ReadString();
            //             if (template != null) {
            //                 BaseItem item = template.CreateItem(guid);
            //                 itemDict[template].Add(item);
            //             }
            //         }
            //     }
            // }
            // state.ProcessDataTemplates<BaseItemTemplate>(recentTemplates);      // +0x18
        }
    }

    // =========================================================================
    // ROSTER SAVE STATE
    // =========================================================================

    /// <summary>
    /// Roster save state processing.
    /// Address: 0x1805a4930
    ///
    /// Manages unit leader lists (active, reserve, wounded, dead).
    ///
    /// Field offsets:
    ///   +0x10: List (active leaders)
    ///   +0x18: List of UnitLeaderTemplates (templates used)
    ///   +0x20: List (reserve leaders)
    ///   +0x28: List (wounded leaders)
    ///   +0x30: List (dead leaders)
    /// </summary>
    public partial class Roster
    {
        public const int OFFSET_ACTIVE = 0x10;
        public const int OFFSET_TEMPLATES = 0x18;
        public const int OFFSET_RESERVE = 0x20;
        public const int OFFSET_WOUNDED = 0x28;
        public const int OFFSET_DEAD = 0x30;

        public void ProcessSaveState(SaveState state)
        {
            // ProcessLeaderList(state, activeList);    // +0x10
            // ProcessLeaderList(state, reserveList);   // +0x20
            // ProcessLeaderList(state, woundedList);   // +0x28
            // ProcessLeaderList(state, deadList);      // +0x30
            // state.ProcessDataTemplates<UnitLeaderTemplate>(templates); // +0x18
        }
    }

    // =========================================================================
    // STRATEGY STATE SAVE STATE (MAIN ORCHESTRATOR)
    // =========================================================================

    /// <summary>
    /// StrategyState save state processing (main orchestrator).
    /// Address: 0x180641b30 (coroutine entry)
    /// Address: 0x18064c130 (MoveNext - actual implementation)
    ///
    /// This is the main save/load coordinator implemented as a coroutine.
    /// It processes all game systems in a specific order with corruption
    /// checks and UI progress updates between each step.
    ///
    /// StrategyState field offsets:
    ///   +0x20: double - Campaign time
    ///   +0x28: bool - Unknown
    ///   +0x30: string - Unknown
    ///   +0x38: int - Current day
    ///   +0x3c: bool - Unknown
    ///   +0x3d: bool - Unknown
    ///   +0x48: GlobalDifficultyTemplate reference
    ///   +0x50: PlanetManager reference
    ///   +0x58: OperationsManager reference
    ///   +0x60: MissionResult reference
    ///   +0x68: Squaddies reference
    ///   +0x70: Roster reference
    ///   +0x78: BattlePlan reference
    ///   +0x80: OwnedItems reference
    ///   +0x88: BlackMarket reference
    ///   +0x90: BarksManager reference
    ///   +0x98: List of OffmapAbilityTemplates
    ///   +0xa0: ShipUpgrades reference
    ///   +0xa8: ConversationManager reference
    ///   +0xb0: EventManager reference
    ///   +0xb8: StoryFactions reference
    ///   +0xc0: int[] array (purpose unknown)
    ///   +0xc8: Dictionary long -> ConversationIntVar
    ///   +0xd0: List of ConversationEffects
    ///
    /// Processing order:
    ///   1. Basic state: Double, Bool, String, Int, Bool, Bool, GlobalDifficultyTemplate, IntArray
    ///   2. Corruption check
    ///   3. ShipUpgrades (+0xa0)
    ///   4. Corruption check
    ///   5. OwnedItems (+0x80)
    ///   6. BlackMarket (+0x88)
    ///   7. StoryFactions (+0xb8)
    ///   8. Corruption check
    ///   9. Squaddies (+0x68)
    ///   10. Roster (+0x70)
    ///   11. Corruption check
    ///   12. BattlePlan (+0x78)
    ///   13. Corruption check
    ///   14. PlanetManager (+0x50)
    ///   15. Corruption check
    ///   16. OperationsManager (+0x58)
    ///   17. Corruption check
    ///   18. MissionResult (+0x60)
    ///   19. Corruption check
    ///   20. ConversationIntVars dictionary (+0xc8)
    ///   21. ConversationManager (+0xa8) - virtual call
    ///   22. ConversationEffects (+0xd0)
    ///   23. Corruption check
    ///   24. EventManager (+0xb0) - virtual call
    ///   25. BarksManager (+0x90) - virtual call
    ///   26. OffmapAbilityTemplates (+0x98)
    ///   27. Final corruption check
    ///   28. Close
    /// </summary>
    public partial class StrategyState
    {
        public const int OFFSET_CAMPAIGN_TIME = 0x20;
        public const int OFFSET_BOOL_28 = 0x28;
        public const int OFFSET_STRING_30 = 0x30;
        public const int OFFSET_CURRENT_DAY = 0x38;
        public const int OFFSET_BOOL_3C = 0x3c;
        public const int OFFSET_BOOL_3D = 0x3d;
        public const int OFFSET_DIFFICULTY = 0x48;
        public const int OFFSET_PLANET_MANAGER = 0x50;
        public const int OFFSET_OPERATIONS_MANAGER = 0x58;
        public const int OFFSET_MISSION_RESULT = 0x60;
        public const int OFFSET_SQUADDIES = 0x68;
        public const int OFFSET_ROSTER = 0x70;
        public const int OFFSET_BATTLE_PLAN = 0x78;
        public const int OFFSET_OWNED_ITEMS = 0x80;
        public const int OFFSET_BLACK_MARKET = 0x88;
        public const int OFFSET_BARKS_MANAGER = 0x90;
        public const int OFFSET_OFFMAP_ABILITIES = 0x98;
        public const int OFFSET_SHIP_UPGRADES = 0xa0;
        public const int OFFSET_CONVERSATION_MANAGER = 0xa8;
        public const int OFFSET_EVENT_MANAGER = 0xb0;
        public const int OFFSET_STORY_FACTIONS = 0xb8;
        public const int OFFSET_INT_ARRAY = 0xc0;
        public const int OFFSET_CONVERSATION_VARS = 0xc8;
        public const int OFFSET_CONVERSATION_EFFECTS = 0xd0;

        public IEnumerator ProcessSaveState(SaveState state)
        {
            // Step 0: Initialize (on load, init ShipUpgrades and EventManager)
            //
            // Step 1-3: Basic state
            // state.ProcessDouble(ref campaignTime);                             // +0x20
            // state.ProcessBool(ref bool28);                                     // +0x28
            // state.ProcessString(ref string30);                                 // +0x30
            // state.ProcessInt(ref currentDay);                                  // +0x38
            // state.ProcessBool(ref bool3c);                                     // +0x3c
            // state.ProcessBool(ref bool3d);                                     // +0x3d
            // state.ProcessDataTemplate<GlobalDifficultyTemplate>(ref difficulty); // +0x48
            // if (difficulty == null) difficulty = DataTemplateLoader.GetAll<GlobalDifficultyTemplate>().First();
            // state.ProcessIntArray(intArray);                                   // +0xc0
            // state.CheckCorruption();
            //
            // Step 4: ShipUpgrades
            // shipUpgrades.ProcessSaveState(state);                              // +0xa0
            // state.CheckCorruption();
            //
            // Step 5: OwnedItems
            // ownedItems.ProcessSaveState(state);                                // +0x80
            //
            // Step 6: BlackMarket
            // blackMarket.ProcessSaveState(state);                               // +0x88
            //
            // Step 7: StoryFactions
            // storyFactions.ProcessSaveState(state);                             // +0xb8
            // state.CheckCorruption();
            //
            // Step 8: Squaddies & Roster
            // squaddies.ProcessSaveState(state);                                 // +0x68
            // roster.ProcessSaveState(state);                                    // +0x70
            // state.CheckCorruption();
            //
            // Step 9: BattlePlan
            // battlePlan.ProcessSaveState(state);                                // +0x78
            // state.CheckCorruption();
            //
            // Step 10: PlanetManager
            // planetManager.ProcessSaveState(state);                             // +0x50
            // state.CheckCorruption();
            //
            // Step 11: OperationsManager
            // operationsManager.ProcessSaveState(state);                         // +0x58
            // state.CheckCorruption();
            //
            // Step 12: MissionResult
            // state.ProcessObject<MissionResult>(ref missionResult);             // +0x60
            // state.CheckCorruption();
            //
            // Step 13: ConversationIntVars (dictionary with hashed string keys)
            // if (state.IsLoading()) {
            //     conversationVars.Clear();
            //     int count = state.ReadInt();
            //     for (int i = 0; i < count; i++) {
            //         string name = state.ReadString();
            //         int value = state.ReadInt();
            //         long hash = ComputeHash(name); // Hash algorithm: h = h * 0x17 + charCode
            //         conversationVars[hash] = new ConversationIntVar { Name = name, Value = value };
            //     }
            // } else {
            //     state.WriteInt(conversationVars.Count);
            //     foreach (var kvp in conversationVars) {
            //         state.WriteString(kvp.Value.Name);
            //         state.WriteInt(kvp.Value.Value);
            //     }
            // }
            //
            // Step 14: ConversationManager (virtual call)
            // conversationManager.ProcessSaveState(state);                       // +0xa8
            //
            // Step 15: ConversationEffects
            // state.ProcessObjects<ConversationEffects>(conversationEffects);    // +0xd0
            // state.CheckCorruption();
            //
            // Step 16: EventManager (virtual call)
            // eventManager.ProcessSaveState(state);                              // +0xb0
            //
            // Step 17: BarksManager (virtual call)
            // barksManager.ProcessSaveState(state);                              // +0x90
            //
            // Step 18: OffmapAbilityTemplates
            // state.ProcessDataTemplates<OffmapAbilityTemplate>(offmapAbilities); // +0x98
            // state.CheckCorruption();
            //
            // Step 19: Finalize
            // if (state.IsLoading()) {
            //     ForEachActiveGameEffectOfType<BaseEntitiesEffect>(callback);
            // }
            // state.Close();

            yield break;
        }
    }

    // =========================================================================
    // SUPPORTING ENUMS
    // =========================================================================

    public enum Gender : byte { }
    public enum SkinColor : byte { }
    public enum HomePlanetType { }
    public enum LeaderHealthStatus : byte { }
    public enum MissionLayer { }
    public enum MissionStatus { }
    public enum LightConditionType { }
    public enum StoryFactionStatus { }
    public enum ObjectiveState { }

    // =========================================================================
    // SUPPORTING STRUCTS
    // =========================================================================

    public struct Vector2 { public float x, y; }
    public struct Vector2Int { public int x, y; }
    public struct Vector3 { public float x, y, z; }
    public struct RectInt { public int x, y, width, height; }
    public struct OperationResources { }
    public struct StrategicDuration { }
    public struct MissionResultStats { }

    // =========================================================================
    // SUPPORTING CLASSES (PLACEHOLDER DECLARATIONS)
    // =========================================================================

    public class DataTemplate { }
    public class EntityTemplate : DataTemplate { }
    public class UnitLeaderTemplate : DataTemplate { }
    public class SkillTemplate : DataTemplate { }
    public class PerkTemplate : DataTemplate { }
    public class MissionTemplate : DataTemplate { }
    public class MissionDifficultyTemplate : DataTemplate { }
    public class OperationTemplate : DataTemplate { }
    public class OperationDurationTemplate : DataTemplate { }
    public class OperationAssetTemplate : DataTemplate { }
    public class PlanetTemplate : DataTemplate { }
    public class BiomeTemplate : DataTemplate { }
    public class WeatherTemplate : DataTemplate { }
    public class FactionTemplate : DataTemplate { }
    public class StoryFactionTemplate : DataTemplate { }
    public class ShipUpgradeTemplate : DataTemplate { }
    public class BaseItemTemplate : DataTemplate { }
    public class GlobalDifficultyTemplate : DataTemplate { }
    public class OffmapAbilityTemplate : DataTemplate { }
    public class ConversationTemplate { }

    public class OperationResult { public void ProcessSaveState(SaveState state) { } }
    public class PseudoRandom { public void ProcessSaveState(SaveState state) { } }
    public class UnitStatistics { public void ProcessSaveState(SaveState state) { } }
    public class EmotionalStates { public void ProcessSaveState(SaveState state) { } }
    public class ConversationEffects { public void ProcessSaveState(SaveState state) { } }
    public class SkillContainer { }
    public class OperationProperties { }
    public class BaseItem { public string Guid; }
    public class Item : BaseItem { }

    // =========================================================================
    // ADDITIONAL PROCESSSAVESTATE FUNCTION ADDRESSES
    // =========================================================================
    //
    // For reference, here are additional ProcessSaveState function addresses:
    //
    // BaseConversationManager::ProcessSaveState     @ 0x180549280
    // ConversationInstance::ProcessSaveState        @ 0x180550290
    // EmotionalState::ProcessSaveState              @ 0x1805b4530
    // EmotionalStates::ProcessSaveState             @ 0x1805b4fd0
    // EventManager::ProcessSaveState                @ 0x18056d5d0
    // ConversationEffects::ProcessSaveState         @ 0x18054ff20
    // BarksManager::ProcessSaveState                @ 0x180564970
    // BattlePlan::ProcessSaveState                  @ 0x180566c10
    // BlackMarket::ProcessSaveState                 @ 0x180569870
    // BlackMarket.BlackMarketItemStack::ProcessSaveState @ 0x1805677a0
    // MissionResult::ProcessSaveState               @ 0x180584cd0
    // MissionResultStats::ProcessSaveState          @ 0x180584700
    // OperationResources::ProcessSaveState          @ 0x180596ce0
    // OperationResult::ProcessSaveState             @ 0x180597400
    // OperationsManager::ProcessSaveState           @ 0x18059ac60
    // Planet::ProcessSaveState                      @ 0x1805a0aa0
    // PlanetManager::ProcessSaveState               @ 0x18059f2f0
    // Squaddies::ProcessSaveState                   @ 0x1805c39e0
    //
    // =========================================================================
}
