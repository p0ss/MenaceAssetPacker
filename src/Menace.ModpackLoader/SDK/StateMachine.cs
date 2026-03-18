#nullable disable
using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// Per-entity state machine system for mod effects.
///
/// Allows tracking named states per entity, with enter/exit callbacks.
///
/// Usage:
///   // Set state
///   StateMachine.SetState(actorPtr, "buff", "charging");
///
///   // Get state
///   var state = StateMachine.GetState(actorPtr, "buff");
///
///   // Check state
///   if (StateMachine.IsInState(actorPtr, "buff", "active")) { ... }
///
///   // Register callbacks
///   StateMachine.OnEnter(actorPtr, "buff", "active", () => { /* apply buff */ });
///   StateMachine.OnExit(actorPtr, "buff", "active", () => { /* remove buff */ });
/// </summary>
public static class StateMachine
{
    // Key: entityPtr, Value: { machineName -> currentState }
    private static readonly Dictionary<IntPtr, Dictionary<string, string>> _entityStates = new();

    // Key: (entityPtr, machine, state), Value: callbacks
    private static readonly Dictionary<(IntPtr, string, string), StateCallbacks> _callbacks = new();

    private static readonly object _lock = new();

    /// <summary>
    /// Cleanup when tactical mission ends.
    /// </summary>
    public static void Cleanup()
    {
        lock (_lock)
        {
            _entityStates.Clear();
            _callbacks.Clear();
        }
    }

    /// <summary>
    /// Get the current state of a state machine for an entity.
    /// </summary>
    /// <param name="entityPtr">Entity pointer</param>
    /// <param name="machine">State machine name</param>
    /// <returns>Current state name, or empty string if not set</returns>
    public static string GetState(IntPtr entityPtr, string machine)
    {
        lock (_lock)
        {
            if (_entityStates.TryGetValue(entityPtr, out var machines) &&
                machines.TryGetValue(machine, out var state))
            {
                return state;
            }
            return "";
        }
    }

    /// <summary>
    /// Check if an entity is in a specific state.
    /// </summary>
    public static bool IsInState(IntPtr entityPtr, string machine, string state)
    {
        return GetState(entityPtr, machine) == state;
    }

    /// <summary>
    /// Set the state of a state machine for an entity.
    /// Fires exit callback for old state and enter callback for new state.
    /// </summary>
    /// <param name="entityPtr">Entity pointer</param>
    /// <param name="machine">State machine name</param>
    /// <param name="newState">New state name</param>
    /// <returns>True if state changed</returns>
    public static bool SetState(IntPtr entityPtr, string machine, string newState)
    {
        string oldState;
        StateCallbacks exitCallbacks = null;
        StateCallbacks enterCallbacks = null;

        lock (_lock)
        {
            // Get or create machine states for this entity
            if (!_entityStates.TryGetValue(entityPtr, out var machines))
            {
                machines = new Dictionary<string, string>();
                _entityStates[entityPtr] = machines;
            }

            // Get old state
            machines.TryGetValue(machine, out oldState!);
            oldState ??= "";

            // No change
            if (oldState == newState)
                return false;

            // Update state
            machines[machine] = newState;

            // Get callbacks
            if (!string.IsNullOrEmpty(oldState))
            {
                _callbacks.TryGetValue((entityPtr, machine, oldState), out exitCallbacks);
            }
            if (!string.IsNullOrEmpty(newState))
            {
                _callbacks.TryGetValue((entityPtr, machine, newState), out enterCallbacks);
            }
        }

        // Fire callbacks outside lock
        try
        {
            exitCallbacks?.OnExit?.Invoke();
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("StateMachine", $"Exit callback failed: {ex.Message}");
        }

        try
        {
            enterCallbacks?.OnEnter?.Invoke();
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("StateMachine", $"Enter callback failed: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// Transition from one state to another only if currently in the expected state.
    /// </summary>
    /// <param name="entityPtr">Entity pointer</param>
    /// <param name="machine">State machine name</param>
    /// <param name="fromState">Expected current state</param>
    /// <param name="toState">New state</param>
    /// <returns>True if transition occurred</returns>
    public static bool Transition(IntPtr entityPtr, string machine, string fromState, string toState)
    {
        if (GetState(entityPtr, machine) != fromState)
            return false;

        return SetState(entityPtr, machine, toState);
    }

    /// <summary>
    /// Register an enter callback for a state.
    /// </summary>
    public static void OnEnter(IntPtr entityPtr, string machine, string state, Action callback)
    {
        lock (_lock)
        {
            var key = (entityPtr, machine, state);
            if (!_callbacks.TryGetValue(key, out var callbacks))
            {
                callbacks = new StateCallbacks();
                _callbacks[key] = callbacks;
            }
            callbacks.OnEnter = callback;
        }
    }

    /// <summary>
    /// Register an exit callback for a state.
    /// </summary>
    public static void OnExit(IntPtr entityPtr, string machine, string state, Action callback)
    {
        lock (_lock)
        {
            var key = (entityPtr, machine, state);
            if (!_callbacks.TryGetValue(key, out var callbacks))
            {
                callbacks = new StateCallbacks();
                _callbacks[key] = callbacks;
            }
            callbacks.OnExit = callback;
        }
    }

    /// <summary>
    /// Clear all states and callbacks for an entity.
    /// </summary>
    public static void ClearEntity(IntPtr entityPtr)
    {
        lock (_lock)
        {
            _entityStates.Remove(entityPtr);

            // Remove all callbacks for this entity
            var keysToRemove = new List<(IntPtr, string, string)>();
            foreach (var key in _callbacks.Keys)
            {
                if (key.Item1 == entityPtr)
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                _callbacks.Remove(key);
            }
        }
    }

    /// <summary>
    /// Get all states for an entity (for debugging).
    /// </summary>
    public static Dictionary<string, string> GetAllStates(IntPtr entityPtr)
    {
        lock (_lock)
        {
            if (_entityStates.TryGetValue(entityPtr, out var machines))
            {
                return new Dictionary<string, string>(machines);
            }
            return new Dictionary<string, string>();
        }
    }

    private class StateCallbacks
    {
        public Action OnEnter { get; set; }
        public Action OnExit { get; set; }
    }
}
