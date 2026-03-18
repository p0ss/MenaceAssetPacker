#nullable disable
using System;
using System.Collections.Generic;

namespace Menace.SDK;

/// <summary>
/// Manages scheduled/delayed execution for mod effects.
///
/// Hooks into TacticalEventHooks.OnRoundStart to tick scheduled coroutines.
///
/// Usage:
///   // Delay execution by 2 rounds
///   Coroutine.Delay(actorPtr, "myEffect", 2, () => {
///       actor.AddEffect("damage", 10, 1);
///   });
///
///   // Repeat every round, 3 times
///   Coroutine.Repeat(actorPtr, "dot", 1, 3, () => {
///       actor.ApplyDamage(5);
///   });
/// </summary>
public static class Coroutine
{
    private static bool _initialized;
    private static readonly List<ScheduledTask> _tasks = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Initialize the coroutine system. Called automatically by SDK.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        TacticalEventHooks.OnRoundStart += OnRoundStart;
        SdkLogger.Msg("[Coroutine] Initialized");
    }

    /// <summary>
    /// Cleanup when tactical mission ends.
    /// </summary>
    public static void Cleanup()
    {
        lock (_lock)
        {
            _tasks.Clear();
        }
    }

    /// <summary>
    /// Schedule a one-time delayed execution.
    /// </summary>
    /// <param name="entityPtr">Entity context (0 for global)</param>
    /// <param name="key">Unique key for this task (allows cancellation/override)</param>
    /// <param name="delayRounds">Rounds to wait before executing</param>
    /// <param name="action">Action to execute</param>
    public static void Delay(IntPtr entityPtr, string key, int delayRounds, Action action)
    {
        lock (_lock)
        {
            // Remove existing task with same key for this entity
            _tasks.RemoveAll(t => t.EntityPtr == entityPtr && t.Key == key);

            _tasks.Add(new ScheduledTask
            {
                EntityPtr = entityPtr,
                Key = key,
                RoundsRemaining = delayRounds,
                Interval = 0,
                RemainingIterations = 1,
                Action = action
            });
        }
    }

    /// <summary>
    /// Schedule repeating execution.
    /// </summary>
    /// <param name="entityPtr">Entity context (0 for global)</param>
    /// <param name="key">Unique key for this task</param>
    /// <param name="intervalRounds">Rounds between executions</param>
    /// <param name="count">Total number of executions (0 = infinite)</param>
    /// <param name="action">Action to execute</param>
    /// <param name="executeImmediately">If true, execute once immediately</param>
    public static void Repeat(IntPtr entityPtr, string key, int intervalRounds, int count, Action action, bool executeImmediately = false)
    {
        lock (_lock)
        {
            _tasks.RemoveAll(t => t.EntityPtr == entityPtr && t.Key == key);

            var task = new ScheduledTask
            {
                EntityPtr = entityPtr,
                Key = key,
                RoundsRemaining = executeImmediately ? 0 : intervalRounds,
                Interval = intervalRounds,
                RemainingIterations = count,
                Action = action
            };

            _tasks.Add(task);

            if (executeImmediately)
            {
                ExecuteTask(task);
            }
        }
    }

    /// <summary>
    /// Cancel a scheduled task.
    /// </summary>
    public static void Cancel(IntPtr entityPtr, string key)
    {
        lock (_lock)
        {
            _tasks.RemoveAll(t => t.EntityPtr == entityPtr && t.Key == key);
        }
    }

    /// <summary>
    /// Cancel all tasks for an entity.
    /// </summary>
    public static void CancelAll(IntPtr entityPtr)
    {
        lock (_lock)
        {
            _tasks.RemoveAll(t => t.EntityPtr == entityPtr);
        }
    }

    /// <summary>
    /// Check if a task is scheduled.
    /// </summary>
    public static bool IsScheduled(IntPtr entityPtr, string key)
    {
        lock (_lock)
        {
            return _tasks.Exists(t => t.EntityPtr == entityPtr && t.Key == key);
        }
    }

    private static void OnRoundStart(int roundNumber)
    {
        List<ScheduledTask> toExecute;
        List<ScheduledTask> toRemove = new();

        lock (_lock)
        {
            // Decrement all counters and collect tasks to execute
            toExecute = new List<ScheduledTask>();

            foreach (var task in _tasks)
            {
                task.RoundsRemaining--;

                if (task.RoundsRemaining <= 0)
                {
                    toExecute.Add(task);
                }
            }
        }

        // Execute outside lock to prevent deadlocks
        foreach (var task in toExecute)
        {
            ExecuteTask(task);
        }

        // Clean up completed tasks
        lock (_lock)
        {
            _tasks.RemoveAll(t => t.IsComplete);
        }
    }

    private static void ExecuteTask(ScheduledTask task)
    {
        try
        {
            task.Action?.Invoke();
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Coroutine", $"Task {task.Key} failed: {ex.Message}");
        }

        // Handle repeat
        if (task.Interval > 0 && (task.RemainingIterations == 0 || task.RemainingIterations > 1))
        {
            task.RoundsRemaining = task.Interval;
            if (task.RemainingIterations > 0)
                task.RemainingIterations--;
        }
        else
        {
            task.IsComplete = true;
        }
    }

    private class ScheduledTask
    {
        public IntPtr EntityPtr { get; set; }
        public string Key { get; set; } = "";
        public int RoundsRemaining { get; set; }
        public int Interval { get; set; }  // 0 = one-shot, >0 = repeat every N rounds
        public int RemainingIterations { get; set; }  // 0 = infinite
        public Action Action { get; set; }
        public bool IsComplete { get; set; }
    }
}
