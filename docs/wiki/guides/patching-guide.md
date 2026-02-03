# Harmony Patching Guide

This guide covers applying Harmony patches via the Menace SDK. The SDK provides `GamePatch` as a simplified wrapper for common cases, while still allowing direct Harmony usage for advanced scenarios.

All SDK types live in the `Menace.SDK` namespace. Your mod receives a `HarmonyLib.Harmony` instance via `IModpackPlugin.OnInitialize`.

---

## GamePatch Basics

`GamePatch.Prefix` and `GamePatch.Postfix` resolve the target type and method by name, apply the patch, and return `bool` indicating success. They never throw -- failures are routed to `ModError`.

### Postfix Example

```csharp
public void OnSceneLoaded(int buildIndex, string sceneName)
{
    if (sceneName != "Tactical") return;

    bool ok = GamePatch.Postfix(
        _harmony,
        "AgentController",       // target type name in Assembly-CSharp
        "TakeDamage",            // target method name
        typeof(MyPlugin).GetMethod(nameof(TakeDamage_Postfix))
    );

    if (!ok)
        _log.Warning("Failed to patch AgentController.TakeDamage");
}

public static void TakeDamage_Postfix(object __instance)
{
    // runs after every TakeDamage call
}
```

### Prefix Example

```csharp
bool ok = GamePatch.Prefix(
    _harmony,
    "SuppressionHandler",
    "OnRoundStart",
    typeof(MyPlugin).GetMethod(nameof(OnRoundStart_Prefix))
);

// Prefix can cancel the original method by returning false
public static bool OnRoundStart_Prefix(object __instance)
{
    // return false to skip the original method
    // return true to let it run
    return true;
}
```

### Using GameType

If you already have a `GameType` reference, pass it directly instead of a string:

```csharp
var type = GameType.Find("CrawlHandler");
if (!type.IsValid) return;

GamePatch.Postfix(_harmony, type, "IsEnabled",
    typeof(MyPlugin).GetMethod(nameof(IsEnabled_Postfix)));
```

This avoids a redundant type lookup if you need the `GameType` for other purposes.

---

## Type Resolution

`GamePatch` resolves type names by scanning `Assembly-CSharp` for a type whose `Name` or `FullName` matches the string you provide. It searches both public and non-public, instance and static methods, and walks the type hierarchy if the method is declared on a base class.

For types outside Assembly-CSharp, resolve the managed `Type` yourself and use Harmony directly:

```csharp
Type type = GameState.FindManagedType("SomeManager");
// or
var gameType = GameType.Find("SomeManager", "MyAssembly.dll");
Type managedType = gameType.ManagedType;
```

---

## When to Use Raw Harmony

Use `GamePatch` for simple prefix/postfix patches on methods with no overloads or ambiguous signatures. Drop down to raw Harmony when you need:

### Transpilers

`GamePatch` does not support transpilers. Use Harmony directly:

```csharp
var method = AccessTools.Method(targetType, "Calculate");
var transpiler = new HarmonyMethod(typeof(MyPlugin), nameof(MyTranspiler));
_harmony.Patch(method, transpiler: transpiler);
```

### Methods With Overloads

When multiple overloads exist and you need a specific signature:

```csharp
Type targetType = GameState.FindManagedType("AgentController");
var method = targetType.GetMethod("TakeDamage", new[] { typeof(int), typeof(bool) });
_harmony.Patch(method, postfix: new HarmonyMethod(typeof(MyPlugin), nameof(MyPostfix)));
```

### Complex Patch Signatures

When your patch method uses `__instance`, `__result`, `__state`, or parameter injection with specific types, verify the managed proxy type exposes what you need:

```csharp
// Verify method exists and check its parameter types
var type = GameType.Find("AgentController");
var method = type.FindMethod("TakeDamage");
if (method != null)
{
    var parameters = method.GetParameters();
    _log.Msg($"TakeDamage params: {string.Join(", ",
        parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
}
```

### Multiple Patches on the Same Method

When applying both prefix and postfix to the same method, either make two `GamePatch` calls or use Harmony directly:

```csharp
// Two separate calls
GamePatch.Prefix(_harmony, "AgentController", "TakeDamage", myPrefix);
GamePatch.Postfix(_harmony, "AgentController", "TakeDamage", myPostfix);

// Or one Harmony call
var method = AccessTools.Method(targetType, "TakeDamage");
_harmony.Patch(method,
    prefix: new HarmonyMethod(myPrefix),
    postfix: new HarmonyMethod(myPostfix));
```

---

## Scene-Aware Patching

Patches should be applied at the right time. IL2CPP types may not be fully initialized until a specific scene loads. The recommended pattern:

```csharp
public class MyPlugin : IModpackPlugin
{
    private HarmonyLib.Harmony _harmony;
    private bool _patchesApplied;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _harmony = harmony;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (_patchesApplied) return;

        // Apply patches when entering the tactical scene
        if (sceneName == "Tactical")
        {
            ApplyPatches();
        }
    }

    private void ApplyPatches()
    {
        bool ok = GamePatch.Postfix(_harmony, "AgentController", "TakeDamage",
            typeof(MyPlugin).GetMethod(nameof(TakeDamage_Postfix)));

        if (ok)
            _patchesApplied = true;
    }
}
```

### Delayed Patching

If types are not immediately available when the scene loads, use `GameState.RunDelayed` or MelonCoroutines:

```csharp
public void OnSceneLoaded(int buildIndex, string sceneName)
{
    if (sceneName == "Tactical" && !_patchesApplied)
    {
        // Wait 10 frames for types to initialize
        GameState.RunDelayed(10, () => ApplyPatches());
    }
}
```

### Conditional Patching

Use `GameState.RunWhen` to wait for a precondition:

```csharp
GameState.RunWhen(
    () => GameState.FindManagedType("AgentController") != null,
    () => ApplyPatches(),
    maxAttempts: 60  // give up after 60 frames
);
```

---

## Error Handling

### Check Return Values

`GamePatch.Prefix` and `GamePatch.Postfix` return `false` on failure. Always check:

```csharp
if (!GamePatch.Postfix(_harmony, "AgentController", "TakeDamage", myMethod))
{
    ModError.Report("MyMod", "Failed to patch AgentController.TakeDamage");
    return;
}
```

### Failure Reasons

Common reasons `GamePatch` returns `false`:

| Reason | Diagnostic |
|--------|-----------|
| Type not found | Assembly-CSharp not loaded yet, or type was renamed |
| Method not found | Method was renamed, or it exists on a different type |
| No managed proxy | IL2CppInterop did not generate a proxy for this type |
| Harmony exception | Incompatible patch signature, method already patched, etc. |

All failures are logged to `ModError` with context. Open the DevConsole (~ key) and check the Errors tab to see details.

### Safe Patch Methods

Your patch methods should be defensive. The `__instance` parameter is an `object` -- cast carefully:

```csharp
public static void MyPostfix(object __instance)
{
    try
    {
        if (__instance is not Il2CppObjectBase il2cppObj) return;

        var obj = new GameObj(il2cppObj.Pointer);
        if (obj.IsNull || !obj.IsAlive) return;

        int hp = obj.ReadInt("HitPoints");
        // ... your logic ...
    }
    catch (Exception ex)
    {
        ModError.Report("MyMod", "MyPostfix failed", ex);
    }
}
```

---

## Cleanup

When your plugin is unloaded, unpatch everything:

```csharp
public void OnUnload()
{
    _harmony.UnpatchSelf();
}
```

This removes all patches applied through your `Harmony` instance without affecting other mods.
