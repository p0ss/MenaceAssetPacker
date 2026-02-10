using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the Emotional State system.
/// Provides safe access to temporary morale and psychological effects on unit leaders.
/// Emotions are triggered by in-game events (kills, injuries, ally deaths) and apply
/// skill modifiers that affect combat performance.
///
/// Based on reverse engineering findings (docs/reverse-engineering/emotional-system.md):
/// - EmotionalStates collection per BaseUnitLeader @ +0x58
/// - EmotionalStates.Owner @ +0x10
/// - EmotionalStates.States @ +0x18
/// - EmotionalState.Template @ +0x10
/// - EmotionalState.Trigger @ +0x18
/// - EmotionalState.TargetLeader @ +0x20
/// - EmotionalState.RemainingDuration @ +0x28
/// - EmotionalState.IsFirstMission @ +0x2C
/// </summary>
public static class Emotions
{
    // Cached types
    private static GameType _emotionalStatesType;
    private static GameType _emotionalStateType;
    private static GameType _emotionalStateTemplateType;
    private static GameType _baseUnitLeaderType;
    private static GameType _strategyStateType;
    private static GameType _rosterType;
    private static GameType _pseudoRandomType;

    // EmotionalStates field offsets
    private const uint OFFSET_ES_OWNER = 0x10;
    private const uint OFFSET_ES_STATES = 0x18;
    private const uint OFFSET_ES_LAST_MISSION_PARTICIPATION = 0x20;
    private const uint OFFSET_ES_LAST_OPERATION_PARTICIPATION = 0x24;

    // EmotionalState field offsets
    private const uint OFFSET_STATE_TEMPLATE = 0x10;
    private const uint OFFSET_STATE_TRIGGER = 0x18;
    private const uint OFFSET_STATE_TARGET_LEADER = 0x20;
    private const uint OFFSET_STATE_REMAINING_DURATION = 0x28;
    private const uint OFFSET_STATE_IS_FIRST_MISSION = 0x2C;

    // EmotionalStateTemplate field offsets
    private const uint OFFSET_TEMPLATE_TYPE = 0x78;
    private const uint OFFSET_TEMPLATE_ICON = 0x80;
    private const uint OFFSET_TEMPLATE_SKILL = 0x98;
    private const uint OFFSET_TEMPLATE_DURATION = 0xC0;
    private const uint OFFSET_TEMPLATE_IS_NEGATIVE = 0xCC;
    private const uint OFFSET_TEMPLATE_IS_STACKABLE = 0xCD;
    private const uint OFFSET_TEMPLATE_REPLACEMENT = 0xD0;

    // BaseUnitLeader offset for Emotions
    private const uint OFFSET_LEADER_EMOTIONS = 0x58;

    // Initialization constants
    private const int INIT_LAST_MISSION = -1;
    private const int INIT_LAST_OPERATION = -1;

    // Save version threshold for operation tracking
    private const int VERSION_OPERATION_TRACKING = 101;

    /// <summary>
    /// Emotional state types that can affect unit leaders.
    /// </summary>
    public enum EmotionalStateType
    {
        /// <summary>No emotional state.</summary>
        None = 0,

        /// <summary>Angry - typically provides combat bonuses but may reduce accuracy.</summary>
        Angry = 1,

        /// <summary>Confident - general combat performance boost.</summary>
        Confident = 2,

        /// <summary>Targeted - directed at a specific enemy leader (requires target).</summary>
        Targeted = 3,

        /// <summary>Grief - negative emotion from ally death, reduces performance.</summary>
        Grief = 4,

        /// <summary>Fear - negative emotion reducing combat effectiveness.</summary>
        Fear = 5,

        /// <summary>Inspired - positive boost from leadership or heroic actions.</summary>
        Inspired = 6,

        /// <summary>Vengeful - focused hatred toward specific target.</summary>
        Vengeful = 7,

        /// <summary>Traumatized - severe negative effect from extreme combat stress.</summary>
        Traumatized = 8
    }

    /// <summary>
    /// Triggers that can cause emotional states to be applied.
    /// </summary>
    public enum EmotionalTrigger
    {
        /// <summary>No trigger.</summary>
        None = 0,

        /// <summary>Unit killed an enemy.</summary>
        KilledEnemy = 1,

        /// <summary>Unit was wounded in combat.</summary>
        WasWounded = 2,

        /// <summary>An ally was killed.</summary>
        AllyKilled = 3,

        /// <summary>An ally was wounded.</summary>
        AllyWounded = 4,

        /// <summary>Mission completed successfully.</summary>
        MissionSuccess = 5,

        /// <summary>Mission failed.</summary>
        MissionFailure = 6,

        /// <summary>Operation started.</summary>
        OperationStart = 7,

        /// <summary>Operation ended.</summary>
        OperationEnd = 8
    }

    /// <summary>
    /// Information about a single active emotional state.
    /// </summary>
    public class EmotionalStateInfo
    {
        /// <summary>The type of emotion.</summary>
        public EmotionalStateType Type { get; set; }

        /// <summary>Name of the emotion type.</summary>
        public string TypeName { get; set; }

        /// <summary>Name of the emotion template.</summary>
        public string TemplateName { get; set; }

        /// <summary>What triggered this emotion.</summary>
        public EmotionalTrigger Trigger { get; set; }

        /// <summary>Name of the trigger.</summary>
        public string TriggerName { get; set; }

        /// <summary>Target leader for targeted emotions (may be null).</summary>
        public string TargetLeaderName { get; set; }

        /// <summary>Missions remaining until this emotion expires.</summary>
        public int RemainingDuration { get; set; }

        /// <summary>True if this emotion was just applied this mission.</summary>
        public bool IsFirstMission { get; set; }

        /// <summary>True if this is a negative emotion.</summary>
        public bool IsNegative { get; set; }

        /// <summary>True if this emotion can stack with itself.</summary>
        public bool IsStackable { get; set; }

        /// <summary>Name of the skill modifier applied by this emotion.</summary>
        public string SkillName { get; set; }

        /// <summary>Pointer to the EmotionalState object.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Information about a unit leader's emotional states collection.
    /// </summary>
    public class EmotionalStatesInfo
    {
        /// <summary>Name of the owning unit leader.</summary>
        public string OwnerName { get; set; }

        /// <summary>Pointer to the owner BaseUnitLeader.</summary>
        public IntPtr OwnerPointer { get; set; }

        /// <summary>List of all active emotional states.</summary>
        public List<EmotionalStateInfo> ActiveStates { get; set; } = new();

        /// <summary>Last mission this unit participated in (-1 if never).</summary>
        public int LastMissionParticipation { get; set; }

        /// <summary>Last operation this unit participated in (-1 if never).</summary>
        public int LastOperationParticipation { get; set; }

        /// <summary>Total count of active emotions.</summary>
        public int StateCount => ActiveStates.Count;

        /// <summary>Count of positive (non-negative) emotions.</summary>
        public int PositiveCount => ActiveStates.FindAll(s => !s.IsNegative).Count;

        /// <summary>Count of negative emotions.</summary>
        public int NegativeCount => ActiveStates.FindAll(s => s.IsNegative).Count;

        /// <summary>Pointer to the EmotionalStates collection.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Result from emotional state operations.
    /// </summary>
    public class EmotionResult
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error message if operation failed.</summary>
        public string Error { get; set; }

        /// <summary>The emotional state type involved.</summary>
        public EmotionalStateType StateType { get; set; }

        /// <summary>Action taken (Added, Extended, Replaced, Reduced, Removed).</summary>
        public string Action { get; set; }

        /// <summary>Create a failed result.</summary>
        public static EmotionResult Failed(string error) =>
            new() { Success = false, Error = error };

        /// <summary>Create a successful result.</summary>
        public static EmotionResult Ok(EmotionalStateType type, string action) =>
            new() { Success = true, StateType = type, Action = action };
    }

    /// <summary>
    /// Get the EmotionalStates collection for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>GameObj representing the EmotionalStates collection, or Null if not found.</returns>
    public static GameObj GetEmotionalStates(GameObj leader)
    {
        if (leader.IsNull)
            return GameObj.Null;

        try
        {
            var emotionsPtr = leader.ReadPtr(OFFSET_LEADER_EMOTIONS);
            return new GameObj(emotionsPtr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.GetEmotionalStates", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get detailed information about all emotional states for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>EmotionalStatesInfo with all active emotions, or null if not available.</returns>
    public static EmotionalStatesInfo GetEmotionalStatesInfo(GameObj leader)
    {
        if (leader.IsNull)
            return null;

        try
        {
            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return null;

            EnsureTypesLoaded();

            var info = new EmotionalStatesInfo
            {
                Pointer = emotions.Pointer,
                OwnerPointer = leader.Pointer,
                OwnerName = leader.GetName() ?? "Unknown",
                LastMissionParticipation = emotions.ReadInt(OFFSET_ES_LAST_MISSION_PARTICIPATION),
                LastOperationParticipation = emotions.ReadInt(OFFSET_ES_LAST_OPERATION_PARTICIPATION)
            };

            // Get the States list
            var statesListPtr = emotions.ReadPtr(OFFSET_ES_STATES);
            if (statesListPtr != IntPtr.Zero)
            {
                var statesList = new GameObj(statesListPtr);
                var states = GetStatesFromList(statesList);
                info.ActiveStates = states;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.GetEmotionalStatesInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if a unit leader has a specific emotional state type.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to check for.</param>
    /// <returns>True if the leader has an active emotion of that type.</returns>
    public static bool HasEmotion(GameObj leader, EmotionalStateType type)
    {
        if (leader.IsNull)
            return false;

        try
        {
            EnsureTypesLoaded();

            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return false;

            var emotionsType = _emotionalStatesType?.ManagedType;
            if (emotionsType == null)
                return false;

            var proxy = GetManagedProxy(emotions, emotionsType);
            if (proxy == null)
                return false;

            var hasStateMethod = emotionsType.GetMethod("HasState",
                BindingFlags.Public | BindingFlags.Instance);
            if (hasStateMethod == null)
                return false;

            return (bool)hasStateMethod.Invoke(proxy, new object[] { (int)type });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.HasEmotion", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if a unit leader has any of the specified emotional state types.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="types">Array of emotional state types to check for.</param>
    /// <returns>True if the leader has any of the specified emotion types.</returns>
    public static bool HasAnyEmotion(GameObj leader, params EmotionalStateType[] types)
    {
        foreach (var type in types)
        {
            if (HasEmotion(leader, type))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if a unit leader has all of the specified emotional state types.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="types">Array of emotional state types to check for.</param>
    /// <returns>True if the leader has all of the specified emotion types.</returns>
    public static bool HasAllEmotions(GameObj leader, params EmotionalStateType[] types)
    {
        foreach (var type in types)
        {
            if (!HasEmotion(leader, type))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get the set of all active emotional state types for a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>HashSet of active EmotionalStateType values.</returns>
    public static HashSet<EmotionalStateType> GetStateSet(GameObj leader)
    {
        var result = new HashSet<EmotionalStateType>();

        if (leader.IsNull)
            return result;

        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null)
                return result;

            foreach (var state in info.ActiveStates)
            {
                result.Add(state.Type);
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.GetStateSet", "Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Get information about a specific active emotion type on a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to get info for.</param>
    /// <returns>EmotionalStateInfo if found, null otherwise.</returns>
    public static EmotionalStateInfo GetEmotionInfo(GameObj leader, EmotionalStateType type)
    {
        var info = GetEmotionalStatesInfo(leader);
        return info?.ActiveStates.Find(s => s.Type == type);
    }

    /// <summary>
    /// Trigger an emotion on a unit leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="trigger">The trigger event causing the emotion.</param>
    /// <param name="target">Optional target leader for targeted emotions.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult TriggerEmotion(GameObj leader, EmotionalTrigger trigger, GameObj target = default)
    {
        if (leader.IsNull)
            return EmotionResult.Failed("Invalid leader");

        try
        {
            EnsureTypesLoaded();

            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsType = _emotionalStatesType?.ManagedType;
            if (emotionsType == null)
                return EmotionResult.Failed("EmotionalStates type not available");

            var proxy = GetManagedProxy(emotions, emotionsType);
            if (proxy == null)
                return EmotionResult.Failed("Failed to create EmotionalStates proxy");

            // Get target leader template if provided
            object targetTemplate = null;
            if (!target.IsNull)
            {
                var leaderType = _baseUnitLeaderType?.ManagedType;
                if (leaderType != null)
                {
                    var targetProxy = GetManagedProxy(target, leaderType);
                    if (targetProxy != null)
                    {
                        var templateProp = leaderType.GetProperty("Template",
                            BindingFlags.Public | BindingFlags.Instance);
                        targetTemplate = templateProp?.GetValue(targetProxy);
                    }
                }
            }

            var triggerMethod = emotionsType.GetMethod("TriggerEmotion",
                BindingFlags.Public | BindingFlags.Instance);
            if (triggerMethod == null)
                return EmotionResult.Failed("TriggerEmotion method not found");

            triggerMethod.Invoke(proxy, new object[] { (int)trigger, targetTemplate });

            ModError.Info("Menace.SDK", $"Triggered emotion: {trigger} on {leader.GetName()}");
            return EmotionResult.Ok(EmotionalStateType.None, "Triggered");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.TriggerEmotion", "Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply a specific emotional state template to a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="templateName">Name of the EmotionalStateTemplate to apply.</param>
    /// <param name="trigger">The trigger causing this emotion.</param>
    /// <param name="target">Optional target leader for targeted emotions.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult ApplyEmotion(GameObj leader, string templateName,
        EmotionalTrigger trigger = EmotionalTrigger.None, GameObj target = default)
    {
        if (leader.IsNull)
            return EmotionResult.Failed("Invalid leader");

        if (string.IsNullOrEmpty(templateName))
            return EmotionResult.Failed("Template name required");

        try
        {
            EnsureTypesLoaded();

            // Find the template
            var template = GameQuery.FindByName("EmotionalStateTemplate", templateName);
            if (template.IsNull)
                return EmotionResult.Failed($"Template '{templateName}' not found");

            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsType = _emotionalStatesType?.ManagedType;
            var templateType = _emotionalStateTemplateType?.ManagedType;
            if (emotionsType == null || templateType == null)
                return EmotionResult.Failed("Required types not available");

            var proxy = GetManagedProxy(emotions, emotionsType);
            var templateProxy = GetManagedProxy(template, templateType);
            if (proxy == null || templateProxy == null)
                return EmotionResult.Failed("Failed to create proxies");

            // Get target leader template if provided
            object targetTemplate = null;
            if (!target.IsNull)
            {
                var leaderType = _baseUnitLeaderType?.ManagedType;
                if (leaderType != null)
                {
                    var targetProxy = GetManagedProxy(target, leaderType);
                    if (targetProxy != null)
                    {
                        var templateProp = leaderType.GetProperty("Template",
                            BindingFlags.Public | BindingFlags.Instance);
                        targetTemplate = templateProp?.GetValue(targetProxy);
                    }
                }
            }

            // Get a random instance for duration calculation
            var random = GetPseudoRandom();

            var applyMethod = emotionsType.GetMethod("TryApplyEmotionalState",
                BindingFlags.Public | BindingFlags.Instance);
            if (applyMethod == null)
                return EmotionResult.Failed("TryApplyEmotionalState method not found");

            var result = (bool)applyMethod.Invoke(proxy,
                new object[] { templateProxy, (int)trigger, targetTemplate, random });

            if (result)
            {
                var stateType = (EmotionalStateType)template.ReadInt(OFFSET_TEMPLATE_TYPE);
                ModError.Info("Menace.SDK",
                    $"Applied emotion '{templateName}' to {leader.GetName()}");
                return EmotionResult.Ok(stateType, "Applied");
            }
            else
            {
                return EmotionResult.Failed("TryApplyEmotionalState returned false");
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ApplyEmotion", "Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a specific emotional state type from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to remove.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult RemoveEmotion(GameObj leader, EmotionalStateType type)
    {
        if (leader.IsNull)
            return EmotionResult.Failed("Invalid leader");

        try
        {
            EnsureTypesLoaded();

            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsType = _emotionalStatesType?.ManagedType;
            if (emotionsType == null)
                return EmotionResult.Failed("EmotionalStates type not available");

            var proxy = GetManagedProxy(emotions, emotionsType);
            if (proxy == null)
                return EmotionResult.Failed("Failed to create proxy");

            // Get state index
            var getIdxMethod = emotionsType.GetMethod("GetStateIdx",
                BindingFlags.Public | BindingFlags.Instance);
            if (getIdxMethod == null)
                return EmotionResult.Failed("GetStateIdx method not found");

            var idx = (int)getIdxMethod.Invoke(proxy, new object[] { (int)type });
            if (idx < 0)
                return EmotionResult.Failed($"No active emotion of type {type}");

            // Remove at index
            var removeMethod = emotionsType.GetMethod("RemoveState",
                BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod == null)
                return EmotionResult.Failed("RemoveState method not found");

            removeMethod.Invoke(proxy, new object[] { idx });

            ModError.Info("Menace.SDK", $"Removed emotion {type} from {leader.GetName()}");
            return EmotionResult.Ok(type, "Removed");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.RemoveEmotion", "Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove all emotional states from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of emotions removed.</returns>
    public static int ClearEmotions(GameObj leader)
    {
        if (leader.IsNull)
            return 0;

        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null || info.StateCount == 0)
                return 0;

            int removed = 0;
            // Remove in reverse order to avoid index shifting issues
            for (int i = info.ActiveStates.Count - 1; i >= 0; i--)
            {
                var result = RemoveEmotion(leader, info.ActiveStates[i].Type);
                if (result.Success)
                    removed++;
            }

            return removed;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ClearEmotions", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Clear all negative emotions from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of negative emotions removed.</returns>
    public static int ClearNegativeEmotions(GameObj leader)
    {
        if (leader.IsNull)
            return 0;

        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null)
                return 0;

            int removed = 0;
            foreach (var state in info.ActiveStates)
            {
                if (state.IsNegative)
                {
                    var result = RemoveEmotion(leader, state.Type);
                    if (result.Success)
                        removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ClearNegativeEmotions", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Clear all positive emotions from a leader.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <returns>Number of positive emotions removed.</returns>
    public static int ClearPositiveEmotions(GameObj leader)
    {
        if (leader.IsNull)
            return 0;

        try
        {
            var info = GetEmotionalStatesInfo(leader);
            if (info == null)
                return 0;

            int removed = 0;
            foreach (var state in info.ActiveStates)
            {
                if (!state.IsNegative)
                {
                    var result = RemoveEmotion(leader, state.Type);
                    if (result.Success)
                        removed++;
                }
            }

            return removed;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ClearPositiveEmotions", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Extend the duration of an active emotion.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to extend.</param>
    /// <param name="missions">Number of missions to add to duration.</param>
    /// <returns>EmotionResult indicating success/failure.</returns>
    public static EmotionResult ExtendDuration(GameObj leader, EmotionalStateType type, int missions = 1)
    {
        if (leader.IsNull)
            return EmotionResult.Failed("Invalid leader");

        try
        {
            EnsureTypesLoaded();

            var emotions = GetEmotionalStates(leader);
            if (emotions.IsNull)
                return EmotionResult.Failed("Leader has no EmotionalStates");

            var emotionsType = _emotionalStatesType?.ManagedType;
            var stateType = _emotionalStateType?.ManagedType;
            if (emotionsType == null || stateType == null)
                return EmotionResult.Failed("Required types not available");

            var proxy = GetManagedProxy(emotions, emotionsType);
            if (proxy == null)
                return EmotionResult.Failed("Failed to create proxy");

            // Get the States list
            var statesProp = emotionsType.GetProperty("States",
                BindingFlags.Public | BindingFlags.Instance);
            if (statesProp == null)
                return EmotionResult.Failed("States property not found");

            var statesList = statesProp.GetValue(proxy);
            if (statesList == null)
                return EmotionResult.Failed("States list is null");

            // Find and update the state
            var countProp = statesList.GetType().GetProperty("Count");
            var indexer = statesList.GetType().GetMethod("get_Item");
            int count = (int)countProp.GetValue(statesList);

            for (int i = 0; i < count; i++)
            {
                var state = indexer.Invoke(statesList, new object[] { i });
                if (state == null) continue;

                var templateProp = stateType.GetProperty("Template",
                    BindingFlags.Public | BindingFlags.Instance);
                var template = templateProp?.GetValue(state);
                if (template == null) continue;

                var typeProp = template.GetType().GetProperty("Type",
                    BindingFlags.Public | BindingFlags.Instance);
                var stateTypeValue = typeProp?.GetValue(template);
                if (stateTypeValue == null) continue;

                if ((int)stateTypeValue == (int)type)
                {
                    var extendMethod = stateType.GetMethod("ExtendDuration",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (extendMethod != null)
                    {
                        extendMethod.Invoke(state, new object[] { missions });
                        ModError.Info("Menace.SDK",
                            $"Extended {type} duration by {missions} on {leader.GetName()}");
                        return EmotionResult.Ok(type, "Extended");
                    }
                }
            }

            return EmotionResult.Failed($"No active emotion of type {type}");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ExtendDuration", "Failed", ex);
            return EmotionResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the remaining duration of an active emotion.
    /// </summary>
    /// <param name="leader">The BaseUnitLeader GameObj.</param>
    /// <param name="type">The emotional state type to check.</param>
    /// <returns>Remaining missions, or -1 if emotion not found.</returns>
    public static int GetRemainingDuration(GameObj leader, EmotionalStateType type)
    {
        var info = GetEmotionInfo(leader, type);
        return info?.RemainingDuration ?? -1;
    }

    /// <summary>
    /// Check if an emotion type is negative.
    /// </summary>
    /// <param name="type">The emotional state type to check.</param>
    /// <returns>True if the emotion type is typically negative.</returns>
    public static bool IsNegativeType(EmotionalStateType type)
    {
        return type switch
        {
            EmotionalStateType.Grief => true,
            EmotionalStateType.Fear => true,
            EmotionalStateType.Traumatized => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if an emotion type requires a target leader.
    /// </summary>
    /// <param name="type">The emotional state type to check.</param>
    /// <returns>True if the emotion type requires a target.</returns>
    public static bool RequiresTarget(EmotionalStateType type)
    {
        return type == EmotionalStateType.Targeted || type == EmotionalStateType.Vengeful;
    }

    /// <summary>
    /// Get the name of an emotional state type.
    /// </summary>
    /// <param name="type">The emotional state type.</param>
    /// <returns>Human-readable name of the type.</returns>
    public static string GetTypeName(EmotionalStateType type)
    {
        return type switch
        {
            EmotionalStateType.None => "None",
            EmotionalStateType.Angry => "Angry",
            EmotionalStateType.Confident => "Confident",
            EmotionalStateType.Targeted => "Targeted",
            EmotionalStateType.Grief => "Grief",
            EmotionalStateType.Fear => "Fear",
            EmotionalStateType.Inspired => "Inspired",
            EmotionalStateType.Vengeful => "Vengeful",
            EmotionalStateType.Traumatized => "Traumatized",
            _ => $"Unknown ({(int)type})"
        };
    }

    /// <summary>
    /// Get the name of an emotional trigger.
    /// </summary>
    /// <param name="trigger">The emotional trigger.</param>
    /// <returns>Human-readable name of the trigger.</returns>
    public static string GetTriggerName(EmotionalTrigger trigger)
    {
        return trigger switch
        {
            EmotionalTrigger.None => "None",
            EmotionalTrigger.KilledEnemy => "Killed Enemy",
            EmotionalTrigger.WasWounded => "Was Wounded",
            EmotionalTrigger.AllyKilled => "Ally Killed",
            EmotionalTrigger.AllyWounded => "Ally Wounded",
            EmotionalTrigger.MissionSuccess => "Mission Success",
            EmotionalTrigger.MissionFailure => "Mission Failure",
            EmotionalTrigger.OperationStart => "Operation Start",
            EmotionalTrigger.OperationEnd => "Operation End",
            _ => $"Unknown ({(int)trigger})"
        };
    }

    /// <summary>
    /// Get all available emotion templates.
    /// </summary>
    /// <returns>Array of template names.</returns>
    public static string[] GetAvailableTemplates()
    {
        try
        {
            var templates = GameQuery.FindAll("EmotionalStateTemplate");
            var names = new List<string>();
            foreach (var template in templates)
            {
                var name = template.GetName();
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            names.Sort();
            return names.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.GetAvailableTemplates", "Failed", ex);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Register console commands for emotional state debugging.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // emotions <nickname> - Show emotions for a unit
        DevConsole.RegisterCommand("emotions", "<nickname>", "Show emotional states for a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: emotions <nickname>";

            var nickname = string.Join(" ", args);
            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            var info = GetEmotionalStatesInfo(leader);
            if (info == null)
                return "Could not get emotional states";

            if (info.StateCount == 0)
                return $"{info.OwnerName} has no active emotions";

            var lines = new List<string>
            {
                $"Emotional States for {info.OwnerName} ({info.StateCount} active):",
                $"  Positive: {info.PositiveCount}, Negative: {info.NegativeCount}"
            };

            foreach (var state in info.ActiveStates)
            {
                var polarity = state.IsNegative ? "-" : "+";
                var target = !string.IsNullOrEmpty(state.TargetLeaderName)
                    ? $" -> {state.TargetLeaderName}"
                    : "";
                var first = state.IsFirstMission ? " [NEW]" : "";
                lines.Add($"  [{polarity}] {state.TypeName}: {state.RemainingDuration} missions{target}{first}");
            }

            return string.Join("\n", lines);
        });

        // triggeremotion <nickname> <trigger> - Trigger an emotion
        DevConsole.RegisterCommand("triggeremotion", "<nickname> <trigger>",
            "Trigger an emotion (KilledEnemy, WasWounded, AllyKilled, etc.)", args =>
        {
            if (args.Length < 2)
                return "Usage: triggeremotion <nickname> <trigger>";

            var nickname = args[0];
            var triggerName = args[1];

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalTrigger>(triggerName, true, out var trigger))
                return $"Unknown trigger '{triggerName}'. Valid: KilledEnemy, WasWounded, AllyKilled, AllyWounded, MissionSuccess, MissionFailure";

            var result = TriggerEmotion(leader, trigger);
            return result.Success
                ? $"Triggered {trigger} on {leader.GetName()}"
                : $"Failed: {result.Error}";
        });

        // applyemotion <nickname> <template> - Apply an emotion template
        DevConsole.RegisterCommand("applyemotion", "<nickname> <template>",
            "Apply an emotion template to a unit", args =>
        {
            if (args.Length < 2)
                return "Usage: applyemotion <nickname> <template>";

            var nickname = args[0];
            var templateName = string.Join(" ", args, 1, args.Length - 1);

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            var result = ApplyEmotion(leader, templateName);
            return result.Success
                ? $"Applied '{templateName}' to {leader.GetName()}: {result.Action}"
                : $"Failed: {result.Error}";
        });

        // removeemotion <nickname> <type> - Remove an emotion
        DevConsole.RegisterCommand("removeemotion", "<nickname> <type>",
            "Remove an emotion type (Angry, Confident, Grief, etc.)", args =>
        {
            if (args.Length < 2)
                return "Usage: removeemotion <nickname> <type>";

            var nickname = args[0];
            var typeName = args[1];

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'. Valid: Angry, Confident, Targeted, Grief, Fear, Inspired, Vengeful, Traumatized";

            var result = RemoveEmotion(leader, type);
            return result.Success
                ? $"Removed {type} from {leader.GetName()}"
                : $"Failed: {result.Error}";
        });

        // clearemotions <nickname> [negative|positive] - Clear emotions
        DevConsole.RegisterCommand("clearemotions", "<nickname> [negative|positive]",
            "Clear all, negative, or positive emotions from a unit", args =>
        {
            if (args.Length == 0)
                return "Usage: clearemotions <nickname> [negative|positive]";

            var nickname = args[0];
            var filter = args.Length > 1 ? args[1].ToLowerInvariant() : "all";

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            int removed = filter switch
            {
                "negative" => ClearNegativeEmotions(leader),
                "positive" => ClearPositiveEmotions(leader),
                _ => ClearEmotions(leader)
            };

            return $"Removed {removed} {filter} emotion(s) from {leader.GetName()}";
        });

        // emotemplates - List available emotion templates
        DevConsole.RegisterCommand("emotemplates", "", "List available emotion templates", args =>
        {
            var templates = GetAvailableTemplates();
            if (templates.Length == 0)
                return "No emotion templates found";

            var lines = new List<string> { $"Emotion Templates ({templates.Length}):" };
            foreach (var t in templates)
            {
                lines.Add($"  {t}");
            }
            return string.Join("\n", lines);
        });

        // hasemotion <nickname> <type> - Check if unit has emotion
        DevConsole.RegisterCommand("hasemotion", "<nickname> <type>",
            "Check if a unit has a specific emotion type", args =>
        {
            if (args.Length < 2)
                return "Usage: hasemotion <nickname> <type>";

            var nickname = args[0];
            var typeName = args[1];

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'";

            var has = HasEmotion(leader, type);
            if (has)
            {
                var duration = GetRemainingDuration(leader, type);
                return $"{leader.GetName()} HAS {type} ({duration} missions remaining)";
            }
            return $"{leader.GetName()} does NOT have {type}";
        });

        // extendemotion <nickname> <type> [missions] - Extend emotion duration
        DevConsole.RegisterCommand("extendemotion", "<nickname> <type> [missions]",
            "Extend the duration of an active emotion", args =>
        {
            if (args.Length < 2)
                return "Usage: extendemotion <nickname> <type> [missions]";

            var nickname = args[0];
            var typeName = args[1];
            var missions = args.Length > 2 && int.TryParse(args[2], out int m) ? m : 1;

            var leader = Roster.FindByNickname(nickname);
            if (leader.IsNull)
                return $"Unit '{nickname}' not found";

            if (!Enum.TryParse<EmotionalStateType>(typeName, true, out var type))
                return $"Unknown emotion type '{typeName}'";

            var result = ExtendDuration(leader, type, missions);
            return result.Success
                ? $"Extended {type} by {missions} mission(s)"
                : $"Failed: {result.Error}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _emotionalStatesType ??= GameType.Find("Menace.Strategy.EmotionalStates");
        _emotionalStateType ??= GameType.Find("Menace.Strategy.EmotionalState");
        _emotionalStateTemplateType ??= GameType.Find("Menace.Strategy.EmotionalStateTemplate");
        _baseUnitLeaderType ??= GameType.Find("Menace.Strategy.BaseUnitLeader");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
        _rosterType ??= GameType.Find("Menace.Strategy.Roster");
        _pseudoRandomType ??= GameType.Find("Menace.Tools.PseudoRandom");
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

    private static object GetPseudoRandom()
    {
        try
        {
            EnsureTypesLoaded();

            var randomType = _pseudoRandomType?.ManagedType;
            if (randomType == null) return null;

            // Try to get Instance property
            var instanceProp = randomType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
                return instanceProp.GetValue(null);

            // Try static Get method
            var getMethod = randomType.GetMethod("Get",
                BindingFlags.Public | BindingFlags.Static);
            if (getMethod != null)
                return getMethod.Invoke(null, null);

            // Create a new instance
            var ctor = randomType.GetConstructor(Type.EmptyTypes);
            return ctor?.Invoke(null);
        }
        catch
        {
            return null;
        }
    }

    private static List<EmotionalStateInfo> GetStatesFromList(GameObj statesList)
    {
        var result = new List<EmotionalStateInfo>();
        if (statesList.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var listType = typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<>);
            var stateType = _emotionalStateType?.ManagedType;

            // Try to iterate via reflection on the Il2Cpp list
            var listProxy = GetListProxy(statesList);
            if (listProxy == null) return result;

            var countProp = listProxy.GetType().GetProperty("Count");
            var indexer = listProxy.GetType().GetMethod("get_Item");

            if (countProp == null || indexer == null) return result;

            int count = (int)countProp.GetValue(listProxy);
            for (int i = 0; i < count; i++)
            {
                var state = indexer.Invoke(listProxy, new object[] { i });
                if (state == null) continue;

                var info = ExtractStateInfo(state);
                if (info != null)
                    result.Add(info);
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.GetStatesFromList", "Failed", ex);
        }

        return result;
    }

    private static object GetListProxy(GameObj list)
    {
        if (list.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            // Try generic List<EmotionalState>
            var stateType = _emotionalStateType?.ManagedType;
            if (stateType == null) return null;

            var listType = typeof(Il2CppSystem.Collections.Generic.List<>).MakeGenericType(stateType);
            var ptrCtor = listType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { list.Pointer });
        }
        catch
        {
            return null;
        }
    }

    private static EmotionalStateInfo ExtractStateInfo(object state)
    {
        if (state == null) return null;

        try
        {
            var stateObj = new GameObj(((Il2CppObjectBase)state).Pointer);
            var info = new EmotionalStateInfo
            {
                Pointer = stateObj.Pointer
            };

            // Read template
            var templatePtr = stateObj.ReadPtr(OFFSET_STATE_TEMPLATE);
            if (templatePtr != IntPtr.Zero)
            {
                var template = new GameObj(templatePtr);
                info.TemplateName = template.GetName();
                info.Type = (EmotionalStateType)template.ReadInt(OFFSET_TEMPLATE_TYPE);
                info.TypeName = GetTypeName(info.Type);
                info.IsNegative = ReadBoolAtOffset(template, OFFSET_TEMPLATE_IS_NEGATIVE);
                info.IsStackable = ReadBoolAtOffset(template, OFFSET_TEMPLATE_IS_STACKABLE);

                // Get skill name
                var skillPtr = template.ReadPtr(OFFSET_TEMPLATE_SKILL);
                if (skillPtr != IntPtr.Zero)
                {
                    var skill = new GameObj(skillPtr);
                    info.SkillName = skill.GetName();
                }
            }

            // Read trigger
            info.Trigger = (EmotionalTrigger)stateObj.ReadInt(OFFSET_STATE_TRIGGER);
            info.TriggerName = GetTriggerName(info.Trigger);

            // Read target leader
            var targetPtr = stateObj.ReadPtr(OFFSET_STATE_TARGET_LEADER);
            if (targetPtr != IntPtr.Zero)
            {
                var target = new GameObj(targetPtr);
                info.TargetLeaderName = target.GetName();
            }

            // Read duration and first mission flag
            info.RemainingDuration = stateObj.ReadInt(OFFSET_STATE_REMAINING_DURATION);
            info.IsFirstMission = ReadBoolAtOffset(stateObj, OFFSET_STATE_IS_FIRST_MISSION);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Emotions.ExtractStateInfo", "Failed", ex);
            return null;
        }
    }

    private static bool ReadBoolAtOffset(GameObj obj, uint offset)
    {
        if (obj.IsNull || offset == 0) return false;

        try
        {
            return Marshal.ReadByte(obj.Pointer + (int)offset) != 0;
        }
        catch
        {
            return false;
        }
    }
}
