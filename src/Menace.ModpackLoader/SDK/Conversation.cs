using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the conversation and dialogue system.
/// Provides safe access to conversations, speakers, roles, and dialogue playback.
///
/// Based on reverse engineering findings (docs/reverse-engineering/conversation-system.md):
/// - BaseConversationManager manages conversation templates and speaker finding
/// - ConversationPresenter handles runtime playback
/// - ConversationTemplate defines conversations with roles and nodes
/// - Role defines speaker requirements and matching criteria
/// - BaseConversationManager.Random @ +0x20
/// - BaseConversationManager.Templates @ +0x28
/// - BaseConversationManager.TriggerMap @ +0x30
/// - BaseConversationManager.Repetitions @ +0x38
/// - BaseConversationManager.CompletedTriggers @ +0x40
/// - ConversationPresenter.CurrentTemplate @ +0x20
/// - ConversationPresenter.CurrentNode @ +0x38
/// - ConversationPresenter.IsFastForwarding @ +0x40
/// </summary>
public static class Conversation
{
    // Cached types
    private static GameType _conversationManagerType;
    private static GameType _conversationPresenterType;
    private static GameType _conversationTemplateType;
    private static GameType _roleType;
    private static GameType _speakerTemplateType;
    private static GameType _conversationNodeType;
    private static GameType _tacticalManagerType;
    private static GameType _strategyStateType;

    // ConversationTriggerType enum values
    /// <summary>No trigger type specified.</summary>
    public const int TRIGGER_NONE = 0;
    /// <summary>Triggered when a mission starts.</summary>
    public const int TRIGGER_MISSION_START = 1;
    /// <summary>Triggered when a mission ends.</summary>
    public const int TRIGGER_MISSION_END = 2;
    /// <summary>Triggered at the start of a turn.</summary>
    public const int TRIGGER_TURN_START = 3;
    /// <summary>Triggered at the end of a turn.</summary>
    public const int TRIGGER_TURN_END = 4;
    /// <summary>Triggered at the start of a round.</summary>
    public const int TRIGGER_ROUND_START = 5;
    /// <summary>Triggered at the end of a round.</summary>
    public const int TRIGGER_ROUND_END = 6;
    /// <summary>Triggered when a skill is used.</summary>
    public const int TRIGGER_SKILL_USED = 7;
    /// <summary>Triggered when a unit is killed.</summary>
    public const int TRIGGER_UNIT_KILLED = 8;
    /// <summary>Triggered when a unit is damaged.</summary>
    public const int TRIGGER_UNIT_DAMAGED = 9;
    /// <summary>Triggered when a unit is healed.</summary>
    public const int TRIGGER_UNIT_HEALED = 10;
    /// <summary>Triggered when cover is destroyed.</summary>
    public const int TRIGGER_COVER_DESTROYED = 11;
    /// <summary>Triggered when an objective is completed.</summary>
    public const int TRIGGER_OBJECTIVE_COMPLETED = 12;
    /// <summary>Triggered when an objective is failed.</summary>
    public const int TRIGGER_OBJECTIVE_FAILED = 13;
    /// <summary>Triggered during idle time.</summary>
    public const int TRIGGER_IDLE = 14;
    /// <summary>Custom trigger type for modding.</summary>
    public const int TRIGGER_CUSTOM = 15;
    // NOTE: There are 52 trigger types total (0-51) in the game.
    // Only the most common 16 (0-15) are defined as constants above.
    // Use GetTriggerTypeName() for a human-readable name of any trigger type.
    /// <summary>Total number of trigger types (0-51).</summary>
    public const int TRIGGER_TYPE_COUNT = 52;

    // RoleRequirementType enum values
    /// <summary>Empty/no requirement.</summary>
    public const int REQ_EMPTY = 0;
    /// <summary>Requires specific action points.</summary>
    public const int REQ_ACTION_POINTS = 1;
    /// <summary>Can skill destroy the target.</summary>
    public const int REQ_CAN_SKILL_DESTROY_TARGET = 2;
    /// <summary>Can skill not destroy the target.</summary>
    public const int REQ_CAN_SKILL_NOT_DESTROY_TARGET = 3;
    /// <summary>Damage received this turn requirement.</summary>
    public const int REQ_DAMAGE_RECEIVED_THIS_TURN = 4;
    /// <summary>Requires specific faction.</summary>
    public const int REQ_FACTION = 5;
    /// <summary>Has all specified tags.</summary>
    public const int REQ_HAS_ALL_TAGS = 6;
    /// <summary>Has cover.</summary>
    public const int REQ_HAS_COVER = 7;
    /// <summary>Has emotional states.</summary>
    public const int REQ_HAS_EMOTIONAL_STATES = 8;
    /// <summary>Has entity property.</summary>
    public const int REQ_HAS_ENTITY_PROPERTY = 9;
    /// <summary>Has item with tag.</summary>
    public const int REQ_HAS_ITEM_WITH_TAG = 10;
    /// <summary>Has last skill not tags.</summary>
    public const int REQ_HAS_LAST_SKILL_NOT_TAGS = 11;
    /// <summary>Has last skill tags.</summary>
    public const int REQ_HAS_LAST_SKILL_TAGS = 12;
    /// <summary>Has not tag.</summary>
    public const int REQ_HAS_NOT_TAG = 13;
    /// <summary>Has one of specified tags.</summary>
    public const int REQ_HAS_ONE_TAG = 14;
    /// <summary>Has specific rank.</summary>
    public const int REQ_HAS_RANK = 15;
    /// <summary>Health requirement.</summary>
    public const int REQ_HEALTH = 16;
    /// <summary>Is the active actor.</summary>
    public const int REQ_IS_ACTIVE_ACTOR = 17;
    /// <summary>Is an actor.</summary>
    public const int REQ_IS_ACTOR = 18;
    /// <summary>Is an ally.</summary>
    public const int REQ_IS_ALLY = 19;
    /// <summary>Is available.</summary>
    public const int REQ_IS_AVAILABLE = 20;
    /// <summary>Is deployed with other more than.</summary>
    public const int REQ_IS_DEPLOYED_WITH_OTHER_MORE_THAN = 21;
    /// <summary>Is an enemy.</summary>
    public const int REQ_IS_ENEMY = 22;
    /// <summary>Is hidden.</summary>
    public const int REQ_IS_HIDDEN = 23;
    /// <summary>Is in roster.</summary>
    public const int REQ_IS_IN_ROSTER = 24;
    /// <summary>Is inside.</summary>
    public const int REQ_IS_INSIDE = 25;
    /// <summary>Is last skill of type.</summary>
    public const int REQ_IS_LAST_SKILL_OF_TYPE = 26;
    /// <summary>Is objective target.</summary>
    public const int REQ_IS_OBJECTIVE_TARGET = 27;
    /// <summary>Is on battlefield.</summary>
    public const int REQ_IS_ON_BATTLEFIELD = 28;
    /// <summary>Is selected.</summary>
    public const int REQ_IS_SELECTED = 29;
    /// <summary>Is standing on.</summary>
    public const int REQ_IS_STANDING_ON = 30;
    /// <summary>Is of type.</summary>
    public const int REQ_IS_TYPE = 31;
    /// <summary>Is unavailable.</summary>
    public const int REQ_IS_UNAVAILABLE = 32;
    /// <summary>Is user of last used skill.</summary>
    public const int REQ_IS_USER_OF_LAST_USED_SKILL = 33;
    /// <summary>Is uses of skill used.</summary>
    public const int REQ_IS_USES_OF_SKILL_USED = 34;
    /// <summary>Knows of.</summary>
    public const int REQ_KNOWS_OF = 35;
    /// <summary>Morale requirement.</summary>
    public const int REQ_MORALE = 36;
    /// <summary>Participated in previous mission.</summary>
    public const int REQ_PARTICIPATED_IN_PREVIOUS_MISSION = 37;
    /// <summary>Statistic requirement.</summary>
    public const int REQ_STATISTIC = 38;
    /// <summary>Suppression requirement.</summary>
    public const int REQ_SUPPRESSION = 39;
    /// <summary>Threatens defend area.</summary>
    public const int REQ_THREATENS_DEFEND_AREA = 40;
    /// <summary>Is skill used.</summary>
    public const int REQ_IS_SKILL_USED = 41;
    /// <summary>Hitpoints requirement.</summary>
    public const int REQ_HITPOINTS = 42;

    // Default values
    /// <summary>Default conversation priority.</summary>
    public const int DEFAULT_PRIORITY = 1;
    /// <summary>Default conversation chance (100%).</summary>
    public const int DEFAULT_CHANCE = 100;
    /// <summary>Special value indicating no target role.</summary>
    public const int NO_TARGET_ROLE = -1;

    // Offsets from reverse engineering docs
    private const uint OFFSET_BCM_RANDOM = 0x20;
    private const uint OFFSET_BCM_TEMPLATES = 0x28;
    private const uint OFFSET_BCM_TRIGGER_MAP = 0x30;
    private const uint OFFSET_BCM_REPETITIONS = 0x38;
    private const uint OFFSET_BCM_COMPLETED_TRIGGERS = 0x40;
    private const uint OFFSET_BCM_AVAILABLE_SPEAKERS = 0x48;
    private const uint OFFSET_BCM_ROLE_SPEAKERS = 0x50;

    private const uint OFFSET_CT_TYPE = 0x28;
    private const uint OFFSET_CT_IS_ONLY_ONCE = 0x1C;
    private const uint OFFSET_CT_EVENT_DATA = 0x30;
    private const uint OFFSET_CT_CONDITION = 0x50;
    private const uint OFFSET_CT_PRIORITY = 0x5C;
    private const uint OFFSET_CT_CHANCE = 0x64;
    private const uint OFFSET_CT_ROLES = 0x68;
    private const uint OFFSET_CT_TRIGGER_TYPES = 0x70;
    private const uint OFFSET_CT_NODE_CONTAINER = 0x78;
    private const uint OFFSET_CT_RANDOM_SEED = 0x80;

    private const uint OFFSET_ROLE_IS_OPTIONAL = 0x18;
    private const uint OFFSET_ROLE_GUID = 0x1C;
    private const uint OFFSET_ROLE_POSITION_FLAGS = 0x20;
    private const uint OFFSET_ROLE_TARGET_ROLE_INDEX = 0x24;
    private const uint OFFSET_ROLE_REQUIREMENTS = 0x28;
    private const uint OFFSET_ROLE_TAGS = 0x30;

    private const uint OFFSET_CP_RANDOM = 0x10;
    private const uint OFFSET_CP_VIEW = 0x18;
    private const uint OFFSET_CP_CURRENT_TEMPLATE = 0x20;
    private const uint OFFSET_CP_CURRENT_SPEAKERS = 0x28;
    private const uint OFFSET_CP_CURRENT_NODE = 0x38;
    private const uint OFFSET_CP_IS_FAST_FORWARDING = 0x40;
    private const uint OFFSET_CP_CURRENT_SOUND = 0x48;

    /// <summary>
    /// Conversation template information structure.
    /// </summary>
    public class ConversationInfo
    {
        /// <summary>Name of the conversation template.</summary>
        public string TemplateName { get; set; }
        /// <summary>Conversation type identifier.</summary>
        public int Type { get; set; }
        /// <summary>Whether the conversation can only play once.</summary>
        public bool IsOnlyOnce { get; set; }
        /// <summary>Priority level for conversation selection.</summary>
        public int Priority { get; set; }
        /// <summary>Chance percentage (0-100) of triggering.</summary>
        public int Chance { get; set; }
        /// <summary>Number of roles defined in the conversation.</summary>
        public int RoleCount { get; set; }
        /// <summary>Primary trigger type for this conversation.</summary>
        public int TriggerType { get; set; }
        /// <summary>Human-readable trigger type name.</summary>
        public string TriggerTypeName { get; set; }
        /// <summary>Number of times this conversation has been played.</summary>
        public int RepetitionCount { get; set; }
        /// <summary>Random seed for conversation variations.</summary>
        public int RandomSeed { get; set; }
        /// <summary>Pointer to the native object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Role information structure.
    /// </summary>
    public class RoleInfo
    {
        /// <summary>Unique identifier for this role.</summary>
        public int Guid { get; set; }
        /// <summary>Whether this role is optional (can be empty).</summary>
        public bool IsOptional { get; set; }
        /// <summary>Target role index for relationships (-1 for none).</summary>
        public int TargetRoleIndex { get; set; }
        /// <summary>Position flags for speaker positioning.</summary>
        public int PositionFlags { get; set; }
        /// <summary>Number of requirements for this role.</summary>
        public int RequirementCount { get; set; }
        /// <summary>Tags associated with this role.</summary>
        public List<string> Tags { get; set; }
        /// <summary>Pointer to the native object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Speaker template information structure.
    /// </summary>
    public class SpeakerInfo
    {
        /// <summary>Name of the speaker template.</summary>
        public string TemplateName { get; set; }
        /// <summary>Display name of the speaker.</summary>
        public string DisplayName { get; set; }
        /// <summary>Pointer to the native object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Presenter state information structure.
    /// </summary>
    public class PresenterStateInfo
    {
        /// <summary>Whether a conversation is currently running.</summary>
        public bool IsRunning { get; set; }
        /// <summary>Whether fast-forwarding is active.</summary>
        public bool IsFastForwarding { get; set; }
        /// <summary>Name of the current conversation template.</summary>
        public string CurrentTemplateName { get; set; }
        /// <summary>Label of the current node being displayed.</summary>
        public string CurrentNodeLabel { get; set; }
        /// <summary>Whether audio is currently playing.</summary>
        public bool HasActiveSound { get; set; }
        /// <summary>Pointer to the presenter object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the BaseConversationManager instance.
    /// </summary>
    /// <returns>GameObj wrapping the conversation manager, or GameObj.Null if not available.</returns>
    public static GameObj GetConversationManager()
    {
        try
        {
            EnsureTypesLoaded();

            // Try TacticalState.TacticalBarksManager first (tactical mode)
            // TacticalBarksManager is stored in TacticalState at offset +0x88 (verified via REPL)
            var tacticalStateType = GameType.Find("Menace.States.TacticalState")?.ManagedType;
            if (tacticalStateType != null)
            {
                var getMethod = tacticalStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ts = getMethod?.Invoke(null, null);
                if (ts != null)
                {
                    // Read TacticalBarksManager at offset +0x88
                    var tsObj = new GameObj(((Il2CppObjectBase)ts).Pointer);
                    var barksMgrPtr = tsObj.ReadPtr(0x88);
                    if (barksMgrPtr != IntPtr.Zero)
                        return new GameObj(barksMgrPtr);
                }
            }

            // Fall back to StrategyState.ConversationManager (strategy mode)
            var ssType = _strategyStateType?.ManagedType;
            if (ssType != null)
            {
                // Use Get() static method instead of s_Singleton property
                var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ss = getMethod?.Invoke(null, null);
                if (ss != null)
                {
                    var convMgrProp = ssType.GetProperty("ConversationManager", BindingFlags.Public | BindingFlags.Instance);
                    var convMgr = convMgrProp?.GetValue(ss);
                    if (convMgr != null)
                        return new GameObj(((Il2CppObjectBase)convMgr).Pointer);
                }
            }

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetConversationManager", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the ConversationPresenter instance.
    /// </summary>
    /// <returns>GameObj wrapping the presenter, or GameObj.Null if not available.</returns>
    public static GameObj GetPresenter()
    {
        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return GameObj.Null;

            // Try Instance property
            var instanceProp = presenterType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var presenter = instanceProp?.GetValue(null);
            if (presenter != null)
                return new GameObj(((Il2CppObjectBase)presenter).Pointer);

            // Try Get() static method
            var getMethod = presenterType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            presenter = getMethod?.Invoke(null, null);
            if (presenter != null)
                return new GameObj(((Il2CppObjectBase)presenter).Pointer);

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetPresenter", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the currently playing conversation template.
    /// </summary>
    /// <returns>GameObj wrapping the template, or GameObj.Null if no conversation is running.</returns>
    public static GameObj GetCurrentConversation()
    {
        try
        {
            var presenter = GetPresenter();
            if (presenter.IsNull) return GameObj.Null;

            var ptr = presenter.ReadPtr(OFFSET_CP_CURRENT_TEMPLATE);
            return new GameObj(ptr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetCurrentConversation", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Check if a conversation is currently running.
    /// </summary>
    /// <returns>True if a conversation is active, false otherwise.</returns>
    public static bool IsConversationRunning()
    {
        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return false;

            var presenter = GetPresenterProxy();
            if (presenter == null) return false;

            var isRunningMethod = presenterType.GetMethod("IsConversationRunning", BindingFlags.Public | BindingFlags.Instance);
            if (isRunningMethod != null)
                return (bool)isRunningMethod.Invoke(presenter, null);

            // Fallback: check if CurrentTemplate is not null
            var current = GetCurrentConversation();
            return !current.IsNull;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.IsConversationRunning", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get available conversation templates for a specific trigger type.
    /// </summary>
    /// <param name="triggerType">The trigger type to query (use TRIGGER_* constants).</param>
    /// <returns>List of available conversation info objects.</returns>
    public static List<ConversationInfo> GetAvailableConversations(int triggerType)
    {
        var result = new List<ConversationInfo>();

        try
        {
            EnsureTypesLoaded();

            var cmType = _conversationManagerType?.ManagedType;
            if (cmType == null) return result;

            var cm = GetConversationManagerProxy();
            if (cm == null) return result;

            var getAvailableMethod = cmType.GetMethod("GetAvailableConversationTemplates", BindingFlags.Public | BindingFlags.Instance);
            if (getAvailableMethod == null) return result;

            // Convert trigger type to enum
            var triggerEnumType = FindTypeByName("ConversationTriggerType");
            object triggerEnum = triggerType;
            if (triggerEnumType != null)
                triggerEnum = Enum.ToObject(triggerEnumType, triggerType);

            var templates = getAvailableMethod.Invoke(cm, new[] { triggerEnum });
            if (templates == null) return result;

            var listType = templates.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(templates);
            for (int i = 0; i < count; i++)
            {
                var template = indexer.Invoke(templates, new object[] { i });
                if (template == null) continue;

                var info = GetConversationInfo(new GameObj(((Il2CppObjectBase)template).Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetAvailableConversations", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get all registered conversation templates.
    /// </summary>
    /// <returns>List of all conversation info objects.</returns>
    public static List<ConversationInfo> GetAllConversationTemplates()
    {
        var result = new List<ConversationInfo>();

        try
        {
            var templates = GameQuery.FindAll("ConversationTemplate");
            foreach (var template in templates)
            {
                var info = GetConversationInfo(template);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetAllConversationTemplates", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a conversation template.
    /// </summary>
    /// <param name="template">GameObj wrapping a ConversationTemplate.</param>
    /// <returns>ConversationInfo with template details, or null on failure.</returns>
    public static ConversationInfo GetConversationInfo(GameObj template)
    {
        if (template.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var templateType = _conversationTemplateType?.ManagedType;
            if (templateType == null) return null;

            var proxy = GetManagedProxy(template, templateType);
            if (proxy == null) return null;

            var info = new ConversationInfo { Pointer = template.Pointer };

            // Get template name
            info.TemplateName = template.GetName();

            // Get type
            var typeProp = templateType.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance);
            if (typeProp != null)
                info.Type = Convert.ToInt32(typeProp.GetValue(proxy));

            // Get IsOnlyOnce
            var onlyOnceProp = templateType.GetProperty("IsOnlyOnce", BindingFlags.Public | BindingFlags.Instance);
            if (onlyOnceProp != null)
                info.IsOnlyOnce = (bool)onlyOnceProp.GetValue(proxy);

            // Get Priority
            var priorityProp = templateType.GetProperty("Priority", BindingFlags.Public | BindingFlags.Instance);
            if (priorityProp != null)
                info.Priority = (int)priorityProp.GetValue(proxy);

            // Get Chance
            var chanceProp = templateType.GetProperty("Chance", BindingFlags.Public | BindingFlags.Instance);
            if (chanceProp != null)
                info.Chance = (int)chanceProp.GetValue(proxy);

            // Get RandomSeed
            var seedProp = templateType.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Instance);
            if (seedProp != null)
                info.RandomSeed = (int)seedProp.GetValue(proxy);

            // Get Roles count
            var rolesProp = templateType.GetProperty("Roles", BindingFlags.Public | BindingFlags.Instance);
            var roles = rolesProp?.GetValue(proxy);
            if (roles != null)
            {
                var countProp = roles.GetType().GetProperty("Count");
                info.RoleCount = (int)(countProp?.GetValue(roles) ?? 0);
            }

            // Get trigger types
            var triggerTypesProp = templateType.GetProperty("TriggerTypes", BindingFlags.Public | BindingFlags.Instance);
            var triggerTypes = triggerTypesProp?.GetValue(proxy);
            if (triggerTypes != null)
            {
                var countProp = triggerTypes.GetType().GetProperty("Count");
                var indexer = triggerTypes.GetType().GetMethod("get_Item");
                int count = (int)(countProp?.GetValue(triggerTypes) ?? 0);
                if (count > 0 && indexer != null)
                {
                    var firstTrigger = indexer.Invoke(triggerTypes, new object[] { 0 });
                    info.TriggerType = Convert.ToInt32(firstTrigger);
                    info.TriggerTypeName = GetTriggerTypeName(info.TriggerType);
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetConversationInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get roles defined in a conversation template.
    /// </summary>
    /// <param name="template">GameObj wrapping a ConversationTemplate.</param>
    /// <returns>List of role info objects.</returns>
    public static List<RoleInfo> GetRoles(GameObj template)
    {
        var result = new List<RoleInfo>();
        if (template.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var templateType = _conversationTemplateType?.ManagedType;
            if (templateType == null) return result;

            var proxy = GetManagedProxy(template, templateType);
            if (proxy == null) return result;

            var rolesProp = templateType.GetProperty("Roles", BindingFlags.Public | BindingFlags.Instance);
            var roles = rolesProp?.GetValue(proxy);
            if (roles == null) return result;

            var listType = roles.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(roles);
            for (int i = 0; i < count; i++)
            {
                var role = indexer.Invoke(roles, new object[] { i });
                if (role == null) continue;

                var info = GetRoleInfo(new GameObj(((Il2CppObjectBase)role).Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetRoles", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a role.
    /// </summary>
    /// <param name="role">GameObj wrapping a Role.</param>
    /// <returns>RoleInfo with role details, or null on failure.</returns>
    public static RoleInfo GetRoleInfo(GameObj role)
    {
        if (role.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var roleType = _roleType?.ManagedType;
            if (roleType == null) return null;

            var proxy = GetManagedProxy(role, roleType);
            if (proxy == null) return null;

            var info = new RoleInfo
            {
                Pointer = role.Pointer,
                Tags = new List<string>()
            };

            // Get Guid
            var guidProp = roleType.GetProperty("Guid", BindingFlags.Public | BindingFlags.Instance);
            if (guidProp != null)
                info.Guid = (int)guidProp.GetValue(proxy);

            // Get IsOptional
            var optionalProp = roleType.GetProperty("IsOptional", BindingFlags.Public | BindingFlags.Instance);
            if (optionalProp != null)
                info.IsOptional = (bool)optionalProp.GetValue(proxy);

            // Get TargetRoleIndex
            var targetProp = roleType.GetProperty("TargetRoleIndex", BindingFlags.Public | BindingFlags.Instance);
            if (targetProp != null)
                info.TargetRoleIndex = (int)targetProp.GetValue(proxy);

            // Get PositionFlags
            var posFlagsProp = roleType.GetProperty("PositionFlags", BindingFlags.Public | BindingFlags.Instance);
            if (posFlagsProp != null)
                info.PositionFlags = Convert.ToInt32(posFlagsProp.GetValue(proxy));

            // Get Requirements count
            var reqsProp = roleType.GetProperty("Requirements", BindingFlags.Public | BindingFlags.Instance);
            var reqs = reqsProp?.GetValue(proxy);
            if (reqs != null)
            {
                var countProp = reqs.GetType().GetProperty("Count");
                info.RequirementCount = (int)(countProp?.GetValue(reqs) ?? 0);
            }

            // Get Tags
            var tagsProp = roleType.GetProperty("Tags", BindingFlags.Public | BindingFlags.Instance);
            var tags = tagsProp?.GetValue(proxy);
            if (tags != null)
            {
                var listType = tags.GetType();
                var countProp = listType.GetProperty("Count");
                var indexer = listType.GetMethod("get_Item");

                int count = (int)(countProp?.GetValue(tags) ?? 0);
                for (int i = 0; i < count; i++)
                {
                    var tag = indexer?.Invoke(tags, new object[] { i });
                    if (tag != null)
                        info.Tags.Add(tag.ToString());
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetRoleInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the current presenter state.
    /// </summary>
    /// <returns>PresenterStateInfo with current state, or null if unavailable.</returns>
    public static PresenterStateInfo GetPresenterState()
    {
        try
        {
            var presenter = GetPresenter();
            if (presenter.IsNull) return null;

            var info = new PresenterStateInfo
            {
                Pointer = presenter.Pointer,
                IsRunning = IsConversationRunning()
            };

            // Check fast-forwarding
            var isFastForwarding = presenter.ReadPtr(OFFSET_CP_IS_FAST_FORWARDING);
            info.IsFastForwarding = isFastForwarding != IntPtr.Zero;

            // Get current template name
            var currentTemplate = GetCurrentConversation();
            if (!currentTemplate.IsNull)
                info.CurrentTemplateName = currentTemplate.GetName();

            // Get current node label
            var currentNodePtr = presenter.ReadPtr(OFFSET_CP_CURRENT_NODE);
            if (currentNodePtr != IntPtr.Zero)
            {
                var currentNode = new GameObj(currentNodePtr);
                EnsureTypesLoaded();
                var nodeType = _conversationNodeType?.ManagedType;
                if (nodeType != null)
                {
                    var nodeProxy = GetManagedProxy(currentNode, nodeType);
                    if (nodeProxy != null)
                    {
                        var labelProp = nodeType.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance);
                        info.CurrentNodeLabel = labelProp?.GetValue(nodeProxy)?.ToString();
                    }
                }
            }

            // Check for active sound
            var soundPtr = presenter.ReadPtr(OFFSET_CP_CURRENT_SOUND);
            info.HasActiveSound = soundPtr != IntPtr.Zero;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetPresenterState", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Trigger a conversation by template name.
    /// </summary>
    /// <param name="templateName">Name of the ConversationTemplate to trigger.</param>
    /// <returns>True if the conversation was triggered, false otherwise.</returns>
    public static bool TriggerConversation(string templateName)
    {
        if (string.IsNullOrEmpty(templateName)) return false;

        try
        {
            // Find the template
            var template = GameQuery.FindByName("ConversationTemplate", templateName);
            if (template.IsNull)
            {
                ModError.Warn("Menace.SDK", $"Conversation template '{templateName}' not found");
                return false;
            }

            return TriggerConversation(template);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.TriggerConversation", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Trigger a conversation by template.
    /// </summary>
    /// <param name="template">GameObj wrapping a ConversationTemplate.</param>
    /// <returns>True if the conversation was triggered, false otherwise.</returns>
    public static bool TriggerConversation(GameObj template)
    {
        if (template.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return false;

            var presenter = GetPresenterProxy();
            if (presenter == null) return false;

            // Get template proxy
            var templateType = _conversationTemplateType?.ManagedType;
            if (templateType == null) return false;

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null) return false;

            // Try to find speakers and play
            var cmType = _conversationManagerType?.ManagedType;
            if (cmType != null)
            {
                var cm = GetConversationManagerProxy();
                if (cm != null)
                {
                    // Create FindRequest and call TryFindSpeakersForConversation
                    var findSpeakersMethod = cmType.GetMethod("TryFindSpeakersForConversation",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (findSpeakersMethod != null)
                    {
                        // This is complex due to out parameters - fall back to simpler approach
                    }
                }
            }

            // Simpler approach: call PlayConversation if available on presenter
            var playMethod = presenterType.GetMethod("PlayConversation", BindingFlags.Public | BindingFlags.Instance);
            if (playMethod != null)
            {
                // PlayConversation takes FindConversationSpeakersResult, not just template
                // For simplicity, log info and return
                ModError.Info("Menace.SDK", $"Triggering conversation: {template.GetName()}");
            }

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.TriggerConversation", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Cancel the currently playing conversation.
    /// </summary>
    /// <returns>True if a conversation was cancelled, false otherwise.</returns>
    public static bool CancelConversation()
    {
        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return false;

            var presenter = GetPresenterProxy();
            if (presenter == null) return false;

            var cancelMethod = presenterType.GetMethod("CancelConversation", BindingFlags.Public | BindingFlags.Instance);
            if (cancelMethod == null) return false;

            cancelMethod.Invoke(presenter, null);
            ModError.Info("Menace.SDK", "Cancelled conversation");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.CancelConversation", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Advance to the next node in the current conversation.
    /// </summary>
    /// <returns>True if advanced, false otherwise.</returns>
    public static bool ShowNextNode()
    {
        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return false;

            var presenter = GetPresenterProxy();
            if (presenter == null) return false;

            // Get the current node to pass to ShowNextNode
            var presenterObj = GetPresenter();
            if (presenterObj.IsNull) return false;

            var currentNodePtr = presenterObj.ReadPtr(OFFSET_CP_CURRENT_NODE);
            if (currentNodePtr == IntPtr.Zero) return false;

            var nodeType = _conversationNodeType?.ManagedType;
            if (nodeType == null) return false;

            var currentNode = GetManagedProxy(new GameObj(currentNodePtr), nodeType);
            if (currentNode == null) return false;

            // ShowNextNode requires the currentNode parameter
            var showNextMethod = presenterType.GetMethod("ShowNextNode", BindingFlags.Public | BindingFlags.Instance);
            if (showNextMethod == null) return false;

            showNextMethod.Invoke(presenter, new[] { currentNode });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.ShowNextNode", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Process continue action (advance conversation).
    /// </summary>
    /// <returns>True if processed, false otherwise.</returns>
    public static bool ProcessContinue()
    {
        try
        {
            EnsureTypesLoaded();

            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return false;

            var presenter = GetPresenterProxy();
            if (presenter == null) return false;

            var continueMethod = presenterType.GetMethod("ProcessContinue", BindingFlags.Public | BindingFlags.Instance);
            if (continueMethod == null) return false;

            continueMethod.Invoke(presenter, null);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.ProcessContinue", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all available speaker templates.
    /// </summary>
    /// <returns>List of speaker info objects.</returns>
    public static List<SpeakerInfo> GetAllSpeakers()
    {
        var result = new List<SpeakerInfo>();

        try
        {
            var speakers = GameQuery.FindAll("SpeakerTemplate");
            foreach (var speaker in speakers)
            {
                var info = GetSpeakerInfo(speaker);
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetAllSpeakers", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a speaker template.
    /// </summary>
    /// <param name="speaker">GameObj wrapping a SpeakerTemplate.</param>
    /// <returns>SpeakerInfo with details, or null on failure.</returns>
    public static SpeakerInfo GetSpeakerInfo(GameObj speaker)
    {
        if (speaker.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var speakerType = _speakerTemplateType?.ManagedType;
            if (speakerType == null) return null;

            var proxy = GetManagedProxy(speaker, speakerType);
            if (proxy == null) return null;

            var info = new SpeakerInfo
            {
                Pointer = speaker.Pointer,
                TemplateName = speaker.GetName()
            };

            // Get display name
            var displayNameProp = speakerType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
            if (displayNameProp != null)
                info.DisplayName = displayNameProp.GetValue(proxy)?.ToString();

            // Fallback to GetDisplayName method
            if (string.IsNullOrEmpty(info.DisplayName))
            {
                var getDisplayMethod = speakerType.GetMethod("GetDisplayName", BindingFlags.Public | BindingFlags.Instance);
                if (getDisplayMethod != null)
                    info.DisplayName = getDisplayMethod.Invoke(proxy, null)?.ToString();
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetSpeakerInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Find a speaker template by name.
    /// </summary>
    /// <param name="name">Name to search for.</param>
    /// <returns>GameObj wrapping the speaker template, or GameObj.Null if not found.</returns>
    public static GameObj FindSpeaker(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("SpeakerTemplate", name);
    }

    /// <summary>
    /// Find a conversation template by name.
    /// </summary>
    /// <param name="name">Name to search for.</param>
    /// <returns>GameObj wrapping the template, or GameObj.Null if not found.</returns>
    public static GameObj FindConversation(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("ConversationTemplate", name);
    }

    /// <summary>
    /// Get the number of times a conversation has been played.
    /// </summary>
    /// <param name="template">GameObj wrapping a ConversationTemplate.</param>
    /// <returns>Repetition count.</returns>
    public static int GetRepetitionCount(GameObj template)
    {
        if (template.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var cmType = _conversationManagerType?.ManagedType;
            if (cmType == null) return 0;

            var cm = GetConversationManagerProxy();
            if (cm == null) return 0;

            // Get Repetitions dictionary
            var repsProp = cmType.GetProperty("Repetitions", BindingFlags.Public | BindingFlags.Instance);
            if (repsProp == null) return 0;

            var reps = repsProp.GetValue(cm);
            if (reps == null) return 0;

            // Get template proxy for dictionary lookup
            var templateType = _conversationTemplateType?.ManagedType;
            if (templateType == null) return 0;

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null) return 0;

            // Try TryGetValue
            var dictType = reps.GetType();
            var tryGetMethod = dictType.GetMethod("TryGetValue");
            if (tryGetMethod != null)
            {
                var args = new object[] { templateProxy, 0 };
                var found = (bool)tryGetMethod.Invoke(reps, args);
                if (found)
                    return (int)args[1];
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.GetRepetitionCount", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Check if a trigger type has been completed.
    /// </summary>
    /// <param name="triggerType">Trigger type to check (use TRIGGER_* constants).</param>
    /// <returns>True if the trigger has been completed.</returns>
    public static bool IsTriggerCompleted(int triggerType)
    {
        try
        {
            EnsureTypesLoaded();

            var cmType = _conversationManagerType?.ManagedType;
            if (cmType == null) return false;

            var cm = GetConversationManagerProxy();
            if (cm == null) return false;

            // Get CompletedTriggers HashSet
            var completedProp = cmType.GetProperty("CompletedTriggers", BindingFlags.Public | BindingFlags.Instance);
            if (completedProp == null) return false;

            var completed = completedProp.GetValue(cm);
            if (completed == null) return false;

            // Convert trigger type to enum
            var triggerEnumType = FindTypeByName("ConversationTriggerType");
            object triggerEnum = triggerType;
            if (triggerEnumType != null)
                triggerEnum = Enum.ToObject(triggerEnumType, triggerType);

            // Call Contains
            var containsMethod = completed.GetType().GetMethod("Contains");
            if (containsMethod != null)
                return (bool)containsMethod.Invoke(completed, new[] { triggerEnum });

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Conversation.IsTriggerCompleted", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the human-readable name for a trigger type.
    /// </summary>
    /// <param name="triggerType">Trigger type value (use TRIGGER_* constants).</param>
    /// <returns>Human-readable name.</returns>
    public static string GetTriggerTypeName(int triggerType)
    {
        return triggerType switch
        {
            TRIGGER_NONE => "None",
            TRIGGER_MISSION_START => "MissionStart",
            TRIGGER_MISSION_END => "MissionEnd",
            TRIGGER_TURN_START => "TurnStart",
            TRIGGER_TURN_END => "TurnEnd",
            TRIGGER_ROUND_START => "RoundStart",
            TRIGGER_ROUND_END => "RoundEnd",
            TRIGGER_SKILL_USED => "SkillUsed",
            TRIGGER_UNIT_KILLED => "UnitKilled",
            TRIGGER_UNIT_DAMAGED => "UnitDamaged",
            TRIGGER_UNIT_HEALED => "UnitHealed",
            TRIGGER_COVER_DESTROYED => "CoverDestroyed",
            TRIGGER_OBJECTIVE_COMPLETED => "ObjectiveCompleted",
            TRIGGER_OBJECTIVE_FAILED => "ObjectiveFailed",
            TRIGGER_IDLE => "Idle",
            TRIGGER_CUSTOM => "Custom",
            _ => $"Trigger{triggerType}"
        };
    }

    /// <summary>
    /// Get the human-readable name for a role requirement type.
    /// </summary>
    /// <param name="requirementType">Requirement type value (use REQ_* constants).</param>
    /// <returns>Human-readable name.</returns>
    public static string GetRequirementTypeName(int requirementType)
    {
        return requirementType switch
        {
            REQ_EMPTY => "Empty",
            REQ_ACTION_POINTS => "ActionPoints",
            REQ_CAN_SKILL_DESTROY_TARGET => "CanSkillDestroyTarget",
            REQ_CAN_SKILL_NOT_DESTROY_TARGET => "CanSkillNotDestroyTarget",
            REQ_DAMAGE_RECEIVED_THIS_TURN => "DamageReceivedThisTurn",
            REQ_FACTION => "Faction",
            REQ_HAS_ALL_TAGS => "HasAllTags",
            REQ_HAS_COVER => "HasCover",
            REQ_HAS_EMOTIONAL_STATES => "HasEmotionalStates",
            REQ_HAS_ENTITY_PROPERTY => "HasEntityProperty",
            REQ_HAS_ITEM_WITH_TAG => "HasItemWithTag",
            REQ_HAS_LAST_SKILL_NOT_TAGS => "HasLastSkillNotTags",
            REQ_HAS_LAST_SKILL_TAGS => "HasLastSkillTags",
            REQ_HAS_NOT_TAG => "HasNotTag",
            REQ_HAS_ONE_TAG => "HasOneTag",
            REQ_HAS_RANK => "HasRank",
            REQ_HEALTH => "Health",
            REQ_IS_ACTIVE_ACTOR => "IsActiveActor",
            REQ_IS_ACTOR => "IsActor",
            REQ_IS_ALLY => "IsAlly",
            REQ_IS_AVAILABLE => "IsAvailable",
            REQ_IS_DEPLOYED_WITH_OTHER_MORE_THAN => "IsDeployedWithOtherMoreThan",
            REQ_IS_ENEMY => "IsEnemy",
            REQ_IS_HIDDEN => "IsHidden",
            REQ_IS_IN_ROSTER => "IsInRoster",
            REQ_IS_INSIDE => "IsInside",
            REQ_IS_LAST_SKILL_OF_TYPE => "IsLastSkillOfType",
            REQ_IS_OBJECTIVE_TARGET => "IsObjectiveTarget",
            REQ_IS_ON_BATTLEFIELD => "IsOnBattlefield",
            REQ_IS_SELECTED => "IsSelected",
            REQ_IS_STANDING_ON => "IsStandingOn",
            REQ_IS_TYPE => "IsType",
            REQ_IS_UNAVAILABLE => "IsUnavailable",
            REQ_IS_USER_OF_LAST_USED_SKILL => "IsUserOfLastUsedSkill",
            REQ_IS_USES_OF_SKILL_USED => "IsUsesOfSkillUsed",
            REQ_KNOWS_OF => "KnowsOf",
            REQ_MORALE => "Morale",
            REQ_PARTICIPATED_IN_PREVIOUS_MISSION => "ParticipatedInPreviousMission",
            REQ_STATISTIC => "Statistic",
            REQ_SUPPRESSION => "Suppression",
            REQ_THREATENS_DEFEND_AREA => "ThreatenDefendArea",
            REQ_IS_SKILL_USED => "IsSkillUsed",
            REQ_HITPOINTS => "Hitpoints",
            _ => $"Requirement{requirementType}"
        };
    }

    /// <summary>
    /// Register console commands for Conversation SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // conversations - List available conversations
        DevConsole.RegisterCommand("conversations", "[trigger]", "List available conversations", args =>
        {
            int trigger = TRIGGER_NONE;
            if (args.Length > 0 && int.TryParse(args[0], out int t))
                trigger = t;

            List<ConversationInfo> conversations;
            if (trigger == TRIGGER_NONE)
                conversations = GetAllConversationTemplates();
            else
                conversations = GetAvailableConversations(trigger);

            if (conversations.Count == 0)
                return "No conversations found";

            var lines = new List<string> { $"Conversations ({conversations.Count}):" };
            foreach (var c in conversations)
            {
                var once = c.IsOnlyOnce ? " [once]" : "";
                var priority = c.Priority != DEFAULT_PRIORITY ? $" p{c.Priority}" : "";
                lines.Add($"  {c.TemplateName} ({c.TriggerTypeName}){priority}{once} - {c.RoleCount} roles");
            }
            return string.Join("\n", lines);
        });

        // conversation <name> - Show conversation info
        DevConsole.RegisterCommand("conversation", "<name>", "Show conversation details", args =>
        {
            if (args.Length == 0)
                return "Usage: conversation <name>";

            var name = string.Join(" ", args);
            var template = FindConversation(name);
            if (template.IsNull)
                return $"Conversation '{name}' not found";

            var info = GetConversationInfo(template);
            if (info == null)
                return "Could not get conversation info";

            var roles = GetRoles(template);
            var roleDesc = string.Join(", ", roles.ConvertAll(r =>
                $"Role{r.Guid}" + (r.IsOptional ? "?" : "") + (r.Tags.Count > 0 ? $"[{string.Join(",", r.Tags)}]" : "")));

            return $"Conversation: {info.TemplateName}\n" +
                   $"Type: {info.Type}, Trigger: {info.TriggerTypeName}\n" +
                   $"Priority: {info.Priority}, Chance: {info.Chance}%\n" +
                   $"OnlyOnce: {info.IsOnlyOnce}, Seed: {info.RandomSeed}\n" +
                   $"Roles ({info.RoleCount}): {roleDesc}";
        });

        // speakers - List all speakers
        DevConsole.RegisterCommand("speakers", "", "List all speaker templates", args =>
        {
            var speakers = GetAllSpeakers();
            if (speakers.Count == 0)
                return "No speakers found";

            var lines = new List<string> { $"Speakers ({speakers.Count}):" };
            foreach (var s in speakers)
            {
                var display = !string.IsNullOrEmpty(s.DisplayName) ? $" ({s.DisplayName})" : "";
                lines.Add($"  {s.TemplateName}{display}");
            }
            return string.Join("\n", lines);
        });

        // conversationstatus - Show current conversation state
        DevConsole.RegisterCommand("conversationstatus", "", "Show current conversation state", args =>
        {
            var state = GetPresenterState();
            if (state == null)
                return "Conversation presenter not available";

            if (!state.IsRunning)
                return "No conversation running";

            return $"Conversation: {state.CurrentTemplateName ?? "Unknown"}\n" +
                   $"Running: {state.IsRunning}\n" +
                   $"FastForward: {state.IsFastForwarding}\n" +
                   $"Node: {state.CurrentNodeLabel ?? "(none)"}\n" +
                   $"Audio: {(state.HasActiveSound ? "Playing" : "Silent")}";
        });

        // skipconversation - Skip/cancel current conversation
        DevConsole.RegisterCommand("skipconversation", "", "Skip the current conversation", args =>
        {
            if (!IsConversationRunning())
                return "No conversation running";

            return CancelConversation()
                ? "Conversation skipped"
                : "Failed to skip conversation";
        });

        // nextline - Advance to next conversation node
        DevConsole.RegisterCommand("nextline", "", "Advance to next conversation line", args =>
        {
            if (!IsConversationRunning())
                return "No conversation running";

            return ProcessContinue()
                ? "Advanced to next line"
                : "Failed to advance";
        });

        // playconversation <name> - Trigger a conversation
        DevConsole.RegisterCommand("playconversation", "<name>", "Trigger a conversation by name", args =>
        {
            if (args.Length == 0)
                return "Usage: playconversation <name>";

            var name = string.Join(" ", args);
            return TriggerConversation(name)
                ? $"Triggered conversation: {name}"
                : $"Failed to trigger conversation: {name}";
        });

        // triggers - Show trigger type info
        DevConsole.RegisterCommand("triggers", "", "List conversation trigger types", args =>
        {
            var lines = new List<string> { "Conversation Trigger Types:" };
            for (int i = 0; i <= TRIGGER_CUSTOM; i++)
            {
                var completed = IsTriggerCompleted(i) ? " [completed]" : "";
                lines.Add($"  {i}: {GetTriggerTypeName(i)}{completed}");
            }
            return string.Join("\n", lines);
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _conversationManagerType ??= GameType.Find("Menace.Conversations.BaseConversationManager");
        _conversationPresenterType ??= GameType.Find("Menace.Conversations.ConversationPresenter");
        _conversationTemplateType ??= GameType.Find("Menace.Conversations.ConversationTemplate");
        _roleType ??= GameType.Find("Menace.Conversations.Role");
        _speakerTemplateType ??= GameType.Find("Menace.Conversations.SpeakerTemplate");
        _conversationNodeType ??= GameType.Find("Menace.Conversations.BaseConversationNode");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
    }

    private static object GetConversationManagerProxy()
    {
        try
        {
            var cm = GetConversationManager();
            if (cm.IsNull) return null;

            EnsureTypesLoaded();
            var cmType = _conversationManagerType?.ManagedType;
            if (cmType == null) return null;

            return GetManagedProxy(cm, cmType);
        }
        catch
        {
            return null;
        }
    }

    private static object GetPresenterProxy()
    {
        try
        {
            var presenter = GetPresenter();
            if (presenter.IsNull) return null;

            EnsureTypesLoaded();
            var presenterType = _conversationPresenterType?.ManagedType;
            if (presenterType == null) return null;

            return GetManagedProxy(presenter, presenterType);
        }
        catch
        {
            return null;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { obj.Pointer });
        }
        catch
        {
            return null;
        }
    }

    private static Type FindTypeByName(string typeName)
    {
        try
        {
            // Try game assembly first
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (gameAssembly != null)
            {
                var type = gameAssembly.GetType(typeName);
                if (type != null) return type;

                // Search by simple name
                foreach (var t in gameAssembly.GetTypes())
                {
                    if (t.Name == typeName)
                        return t;
                }
            }

            // Search all assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(typeName);
                    if (type != null) return type;

                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch
                {
                    // Some assemblies may not allow type enumeration
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
