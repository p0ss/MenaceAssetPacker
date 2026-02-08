# GameState

`Menace.SDK.GameState` -- Static class providing scene awareness, game assembly state, and deferred execution helpers.

## Properties

### CurrentScene

```csharp
public static string CurrentScene { get; }
```

The name of the currently loaded scene. Updated internally when a scene loads. Initialized to an empty string.

### GameAssembly

```csharp
public static Assembly GameAssembly { get; }
```

Returns the `Assembly-CSharp` assembly from the current `AppDomain`, or `null` if it has not been loaded yet. Each access scans `AppDomain.CurrentDomain.GetAssemblies()`.

### IsGameAssemblyLoaded

```csharp
public static bool IsGameAssemblyLoaded { get; }
```

`true` when `GameAssembly` is non-null.

## Methods

### IsScene

```csharp
public static bool IsScene(string sceneName)
```

Check if `CurrentScene` matches the given name (case-insensitive comparison).

### FindManagedType

```csharp
public static Type FindManagedType(string fullName)
```

Find a managed `System.Type` by full name or simple name in `Assembly-CSharp`. Returns `null` if the assembly is not loaded or the type is not found. Failures are logged to `ModError`.

### RunDelayed

```csharp
public static void RunDelayed(int frames, Action callback)
```

Schedule a callback to run after a specified number of frames. The countdown is processed once per frame in the mod loader's `OnUpdate`. If `callback` throws, the exception is caught and logged to `ModError`.

### RunWhen

```csharp
public static void RunWhen(Func<bool> condition, Action callback, int maxAttempts = 30)
```

Schedule a callback to run when a condition becomes `true`. The condition is polled once per frame. If the condition is not met within `maxAttempts` frames, the action is silently dropped.

If the condition function throws, that frame counts as a failed attempt (not a match).

## Events

### SceneLoaded

```csharp
public static event Action<string> SceneLoaded;
```

Fired immediately when a new scene is loaded. The parameter is the scene name. Handler exceptions are caught and routed to `ModError`.

### TacticalReady

```csharp
public static event Action TacticalReady;
```

Fired 30 frames after the `"Tactical"` scene loads. This delay allows Unity to finish initializing scene objects before mods try to query them. The event fires at most once per entry into the Tactical scene (resets when leaving and re-entering).

## Examples

### Reacting to scene loads

```csharp
GameState.SceneLoaded += sceneName =>
{
    DevConsole.Log($"Scene loaded: {sceneName}");

    if (sceneName == "MainMenu")
    {
        // Do main menu setup
    }
};
```

### Applying changes when tactical is ready

```csharp
GameState.TacticalReady += () =>
{
    var agents = GameQuery.FindAll("Agent");
    foreach (var agent in agents)
    {
        agent.WriteInt("health", 200);
    }
    DevConsole.Log($"Buffed {agents.Length} agents");
};
```

### Conditional scene check

```csharp
if (GameState.IsScene("Tactical"))
{
    // We are in the tactical scene
}
```

### Deferring work until something is available

```csharp
GameState.RunWhen(
    () => GameQuery.FindByName("Agent", "Player").IsAlive,
    () =>
    {
        var player = GameQuery.FindByName("Agent", "Player");
        player.WriteInt("health", 999);
        DevConsole.Log("Player buffed");
    },
    maxAttempts: 60  // give up after 60 frames
);
```

### Delayed execution

```csharp
// Run something 10 frames from now
GameState.RunDelayed(10, () =>
{
    DevConsole.Log("10 frames have passed");
});
```

### Checking assembly availability

```csharp
if (!GameState.IsGameAssemblyLoaded)
{
    ModError.Warn("MyMod", "Game assembly not loaded yet");
    return;
}

var type = GameState.FindManagedType("AgentDef");
if (type != null)
    DevConsole.Log($"Found managed type: {type.FullName}");
```
