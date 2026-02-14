using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// Scene awareness, game assembly state, and deferred execution helpers.
/// </summary>
public static class GameState
{
    public static string CurrentScene { get; private set; } = "";

    public static event Action<string> SceneLoaded;
    public static event Action TacticalReady;

    private static bool _tacticalFired;

    /// <summary>
    /// Check if the current scene matches the given name (case-insensitive).
    /// </summary>
    public static bool IsScene(string sceneName)
    {
        return string.Equals(CurrentScene, sceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True if currently in the Tactical (combat) scene.
    /// </summary>
    public static bool IsTactical =>
        CurrentScene?.IndexOf("Tactical", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// True if currently in a strategy/campaign scene (not tactical, not menu).
    /// </summary>
    public static bool IsStrategy =>
        !string.IsNullOrEmpty(CurrentScene) &&
        !IsTactical &&
        CurrentScene.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0 &&
        CurrentScene.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) < 0;

    /// <summary>
    /// The Assembly-CSharp assembly, or null if not yet loaded.
    /// </summary>
    public static Assembly GameAssembly
    {
        get
        {
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            }
            catch
            {
                return null;
            }
        }
    }

    public static bool IsGameAssemblyLoaded => GameAssembly != null;

    /// <summary>
    /// Find a managed type by full name in Assembly-CSharp.
    /// </summary>
    public static Type FindManagedType(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return null;

        try
        {
            var asm = GameAssembly;
            if (asm == null) return null;

            return asm.GetTypes().FirstOrDefault(t =>
                t.FullName == fullName || t.Name == fullName);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameState.FindManagedType", $"Failed for '{fullName}'", ex);
            return null;
        }
    }

    // --- Deferred execution ---

    private static readonly List<DelayedAction> _delayedActions = new();
    private static readonly List<ConditionalAction> _conditionalActions = new();

    /// <summary>
    /// Run a callback after a specified number of frames.
    /// </summary>
    public static void RunDelayed(int frames, Action callback)
    {
        if (callback == null) return;
        lock (_delayedActions)
        {
            _delayedActions.Add(new DelayedAction { FramesRemaining = frames, Callback = callback });
        }
    }

    /// <summary>
    /// Run a callback when a condition becomes true, polling once per frame.
    /// Gives up after maxAttempts frames.
    /// </summary>
    public static void RunWhen(Func<bool> condition, Action callback, int maxAttempts = 30)
    {
        if (condition == null || callback == null) return;
        lock (_conditionalActions)
        {
            _conditionalActions.Add(new ConditionalAction
            {
                Condition = condition,
                Callback = callback,
                AttemptsRemaining = maxAttempts
            });
        }
    }

    // --- Internal lifecycle ---

    internal static void NotifySceneLoaded(string sceneName)
    {
        CurrentScene = sceneName ?? "";

        // Reset tactical state on non-tactical scenes
        if (!IsScene("Tactical"))
            _tacticalFired = false;

        try { SceneLoaded?.Invoke(sceneName); }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameState.SceneLoaded", "Event handler failed", ex);
        }

        // Fire TacticalReady 30 frames after entering Tactical scene
        if (IsScene("Tactical") && !_tacticalFired)
        {
            _tacticalFired = true;
            RunDelayed(30, () =>
            {
                try { TacticalReady?.Invoke(); }
                catch (Exception ex)
                {
                    ModError.ReportInternal("GameState.TacticalReady", "Event handler failed", ex);
                }
            });
        }
    }

    internal static void ProcessUpdate()
    {
        // Process delayed actions
        lock (_delayedActions)
        {
            for (int i = _delayedActions.Count - 1; i >= 0; i--)
            {
                var action = _delayedActions[i];
                action.FramesRemaining--;
                if (action.FramesRemaining <= 0)
                {
                    _delayedActions.RemoveAt(i);
                    try { action.Callback(); }
                    catch (Exception ex)
                    {
                        ModError.ReportInternal("GameState.RunDelayed", "Callback failed", ex);
                    }
                }
            }
        }

        // Process conditional actions
        lock (_conditionalActions)
        {
            for (int i = _conditionalActions.Count - 1; i >= 0; i--)
            {
                var action = _conditionalActions[i];
                action.AttemptsRemaining--;

                try
                {
                    if (action.Condition())
                    {
                        _conditionalActions.RemoveAt(i);
                        try { action.Callback(); }
                        catch (Exception ex)
                        {
                            ModError.ReportInternal("GameState.RunWhen", "Callback failed", ex);
                        }
                        continue;
                    }
                }
                catch
                {
                    // condition threw — count as failed attempt
                }

                if (action.AttemptsRemaining <= 0)
                    _conditionalActions.RemoveAt(i);
            }
        }
    }

    private class DelayedAction
    {
        public int FramesRemaining;
        public Action Callback;
    }

    private class ConditionalAction
    {
        public Func<bool> Condition;
        public Action Callback;
        public int AttemptsRemaining;
    }
}
