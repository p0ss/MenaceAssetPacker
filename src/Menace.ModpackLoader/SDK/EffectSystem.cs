using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.SDK;

/// <summary>
/// Temporary effect modifier system with automatic round-based expiry.
///
/// Effects are modifiers applied to entity properties that automatically expire
/// after a specified number of rounds. Multiple effects can stack on the same property.
///
/// Usage:
///   // Add a -3 concealment penalty that expires after 1 round
///   EffectSystem.AddEffect(actorPtr, "concealment", -3, rounds: 1);
///
///   // Query total modifier for an entity's property
///   int mod = EffectSystem.GetModifier(actorPtr, "concealment");
///
///   // Wire into round end (done automatically via TacticalEventHooks)
///   TacticalEventHooks.OnRoundEnd += _ => EffectSystem.OnRoundEnd();
///
/// The effect system is designed to integrate with the Intercept system - property
/// postfixes can call GetModifier() to apply active effects to calculated values.
/// </summary>
public static class EffectSystem
{
    /// <summary>
    /// Represents a single active effect.
    /// </summary>
    public class Effect
    {
        public IntPtr EntityPtr { get; set; }
        public string Property { get; set; } = "";
        public int Modifier { get; set; }
        public int RoundsRemaining { get; set; }
        public string Source { get; set; } = "";  // Optional: track what applied this effect
    }

    // All active effects, keyed by (entityPtr, property)
    private static readonly Dictionary<(long, string), List<Effect>> _effects = new();

    // Lock for thread safety (game may access from multiple threads)
    private static readonly object _lock = new();

    /// <summary>
    /// Add a temporary effect to an entity's property.
    /// </summary>
    /// <param name="entityPtr">The entity to apply the effect to</param>
    /// <param name="property">The property being modified (e.g., "concealment", "accuracy")</param>
    /// <param name="modifier">The modifier value (positive or negative)</param>
    /// <param name="rounds">How many rounds until expiry (1 = expires at end of current round)</param>
    /// <param name="source">Optional source identifier for debugging/tracking</param>
    public static void AddEffect(IntPtr entityPtr, string property, int modifier, int rounds, string source = "")
    {
        if (entityPtr == IntPtr.Zero || string.IsNullOrEmpty(property) || rounds <= 0)
            return;

        var key = (entityPtr.ToInt64(), property);
        var effect = new Effect
        {
            EntityPtr = entityPtr,
            Property = property,
            Modifier = modifier,
            RoundsRemaining = rounds,
            Source = source
        };

        lock (_lock)
        {
            if (!_effects.TryGetValue(key, out var list))
            {
                list = new List<Effect>();
                _effects[key] = list;
            }
            list.Add(effect);
        }

        SdkLogger.Msg($"[EffectSystem] Added {modifier:+#;-#;0} {property} for {rounds} round(s) on 0x{entityPtr.ToInt64():X}");
    }

    /// <summary>
    /// Get the total modifier for an entity's property from all active effects.
    /// </summary>
    /// <param name="entityPtr">The entity to query</param>
    /// <param name="property">The property to check</param>
    /// <returns>Sum of all active modifiers, or 0 if none</returns>
    public static int GetModifier(IntPtr entityPtr, string property)
    {
        if (entityPtr == IntPtr.Zero || string.IsNullOrEmpty(property))
            return 0;

        var key = (entityPtr.ToInt64(), property);

        lock (_lock)
        {
            if (_effects.TryGetValue(key, out var list))
            {
                return list.Sum(e => e.Modifier);
            }
        }

        return 0;
    }

    /// <summary>
    /// Get the total modifier using a pointer value directly (for Lua interop).
    /// </summary>
    public static int GetModifier(long entityPtrValue, string property)
    {
        return GetModifier(new IntPtr(entityPtrValue), property);
    }

    /// <summary>
    /// Check if an entity has any active effects on a property.
    /// </summary>
    public static bool HasEffect(IntPtr entityPtr, string property)
    {
        if (entityPtr == IntPtr.Zero || string.IsNullOrEmpty(property))
            return false;

        var key = (entityPtr.ToInt64(), property);

        lock (_lock)
        {
            return _effects.TryGetValue(key, out var list) && list.Count > 0;
        }
    }

    /// <summary>
    /// Get all active effects on an entity.
    /// </summary>
    public static List<Effect> GetEffects(IntPtr entityPtr)
    {
        var result = new List<Effect>();

        lock (_lock)
        {
            foreach (var kvp in _effects)
            {
                if (kvp.Key.Item1 == entityPtr.ToInt64())
                {
                    result.AddRange(kvp.Value);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get all active effects on an entity for a specific property.
    /// </summary>
    public static List<Effect> GetEffects(IntPtr entityPtr, string property)
    {
        if (entityPtr == IntPtr.Zero || string.IsNullOrEmpty(property))
            return new List<Effect>();

        var key = (entityPtr.ToInt64(), property);

        lock (_lock)
        {
            if (_effects.TryGetValue(key, out var list))
            {
                return new List<Effect>(list);  // Return copy
            }
        }

        return new List<Effect>();
    }

    /// <summary>
    /// Remove all effects from an entity (e.g., when entity dies or is removed).
    /// </summary>
    public static void ClearEffects(IntPtr entityPtr)
    {
        if (entityPtr == IntPtr.Zero)
            return;

        var ptrValue = entityPtr.ToInt64();

        lock (_lock)
        {
            var keysToRemove = _effects.Keys.Where(k => k.Item1 == ptrValue).ToList();
            foreach (var key in keysToRemove)
            {
                _effects.Remove(key);
            }
        }

        SdkLogger.Msg($"[EffectSystem] Cleared all effects on 0x{entityPtr.ToInt64():X}");
    }

    /// <summary>
    /// Remove all effects with a specific source identifier.
    /// </summary>
    public static void ClearEffectsBySource(string source)
    {
        if (string.IsNullOrEmpty(source))
            return;

        lock (_lock)
        {
            foreach (var list in _effects.Values)
            {
                list.RemoveAll(e => e.Source == source);
            }

            // Clean up empty lists
            var emptyKeys = _effects.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
            foreach (var key in emptyKeys)
            {
                _effects.Remove(key);
            }
        }
    }

    /// <summary>
    /// Called at the end of each round to decrement effect timers and remove expired effects.
    /// Wire this to TacticalEventHooks.OnRoundEnd.
    /// </summary>
    public static void OnRoundEnd()
    {
        int expired = 0;
        int remaining = 0;

        lock (_lock)
        {
            foreach (var list in _effects.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    list[i].RoundsRemaining--;
                    if (list[i].RoundsRemaining <= 0)
                    {
                        list.RemoveAt(i);
                        expired++;
                    }
                    else
                    {
                        remaining++;
                    }
                }
            }

            // Clean up empty lists
            var emptyKeys = _effects.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
            foreach (var key in emptyKeys)
            {
                _effects.Remove(key);
            }
        }

        if (expired > 0 || remaining > 0)
        {
            SdkLogger.Msg($"[EffectSystem] Round end: {expired} expired, {remaining} remaining");
        }
    }

    /// <summary>
    /// Clear all effects (e.g., when mission ends).
    /// </summary>
    public static void ClearAll()
    {
        lock (_lock)
        {
            _effects.Clear();
        }
        SdkLogger.Msg("[EffectSystem] All effects cleared");
    }

    /// <summary>
    /// Get count of all active effects.
    /// </summary>
    public static int ActiveEffectCount
    {
        get
        {
            lock (_lock)
            {
                return _effects.Values.Sum(list => list.Count);
            }
        }
    }
}
