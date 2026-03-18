using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// Tracks "once" execution - ensuring something only happens once per entity or per combat.
///
/// Usage:
///   // Execute once per entity
///   if (OnceTracker.TryExecute(actorPtr, "myOnceEffect"))
///   {
///       // First time for this actor
///       actor.AddEffect("bonus", 5, 999);
///   }
///
///   // Execute once per combat (global)
///   if (OnceTracker.TryExecuteGlobal("missionStartBonus"))
///   {
///       // First time this combat
///   }
/// </summary>
public static class OnceTracker
{
    // Per-entity tracking: entityPtr -> set of executed keys
    private static readonly Dictionary<IntPtr, HashSet<string>> _entityExecuted = new();

    // Global (per-combat) tracking
    private static readonly HashSet<string> _globalExecuted = new();

    private static readonly object _lock = new();

    /// <summary>
    /// Cleanup when tactical mission ends.
    /// </summary>
    public static void Cleanup()
    {
        lock (_lock)
        {
            _entityExecuted.Clear();
            _globalExecuted.Clear();
        }
    }

    /// <summary>
    /// Check if this key has already been executed for this entity.
    /// If not, mark it as executed and return true.
    /// </summary>
    /// <param name="entityPtr">Entity pointer</param>
    /// <param name="key">Unique key for this once-check</param>
    /// <returns>True if this is the first execution (should proceed)</returns>
    public static bool TryExecute(IntPtr entityPtr, string key)
    {
        lock (_lock)
        {
            if (!_entityExecuted.TryGetValue(entityPtr, out var executed))
            {
                executed = new HashSet<string>();
                _entityExecuted[entityPtr] = executed;
            }

            if (executed.Contains(key))
                return false;

            executed.Add(key);
            return true;
        }
    }

    /// <summary>
    /// Check if this key has been executed for this entity (without marking).
    /// </summary>
    public static bool HasExecuted(IntPtr entityPtr, string key)
    {
        lock (_lock)
        {
            return _entityExecuted.TryGetValue(entityPtr, out var executed) && executed.Contains(key);
        }
    }

    /// <summary>
    /// Reset a once-key for an entity (allow re-execution).
    /// </summary>
    public static void Reset(IntPtr entityPtr, string key)
    {
        lock (_lock)
        {
            if (_entityExecuted.TryGetValue(entityPtr, out var executed))
            {
                executed.Remove(key);
            }
        }
    }

    /// <summary>
    /// Check if this key has already been executed globally (per-combat).
    /// If not, mark it as executed and return true.
    /// </summary>
    /// <param name="key">Unique key for this once-check</param>
    /// <returns>True if this is the first execution</returns>
    public static bool TryExecuteGlobal(string key)
    {
        lock (_lock)
        {
            if (_globalExecuted.Contains(key))
                return false;

            _globalExecuted.Add(key);
            return true;
        }
    }

    /// <summary>
    /// Check if this key has been executed globally (without marking).
    /// </summary>
    public static bool HasExecutedGlobal(string key)
    {
        lock (_lock)
        {
            return _globalExecuted.Contains(key);
        }
    }

    /// <summary>
    /// Reset a global once-key (allow re-execution).
    /// </summary>
    public static void ResetGlobal(string key)
    {
        lock (_lock)
        {
            _globalExecuted.Remove(key);
        }
    }

    /// <summary>
    /// Clear all tracking for an entity.
    /// </summary>
    public static void ClearEntity(IntPtr entityPtr)
    {
        lock (_lock)
        {
            _entityExecuted.Remove(entityPtr);
        }
    }
}
