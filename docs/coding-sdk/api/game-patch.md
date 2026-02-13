# GamePatch

`Menace.SDK.GamePatch` -- Static helpers for applying Harmony patches at runtime with safe error handling.

## Overview

GamePatch simplifies Harmony patching with error-safe wrappers:

- **Type Resolution** - Find target types by name or GameType
- **Method Discovery** - Locates methods across the class hierarchy with broad binding flags
- **Error Isolation** - Failed patches log to ModError and return false, never crash
- **Prefix/Postfix** - Standard Harmony patch types with consistent API

Use GamePatch when you need to hook game methods. The API handles the complexity of finding methods in IL2CppInterop proxy types and gracefully handles failures during batch patching.

All methods return `false` on failure and log details to `ModError` instead of throwing exceptions. This is by design: a single failed patch should not crash the mod loader or prevent other patches from being applied.

## Methods

### Postfix (by type name)

```csharp
public static bool Postfix(
    HarmonyLib.Harmony harmony,
    string typeName,
    string methodName,
    MethodInfo patchMethod)
```

Apply a Harmony Postfix patch to `typeName.methodName`. The type is resolved from `Assembly-CSharp` by simple name or full name.

### Prefix (by type name)

```csharp
public static bool Prefix(
    HarmonyLib.Harmony harmony,
    string typeName,
    string methodName,
    MethodInfo patchMethod)
```

Apply a Harmony Prefix patch to `typeName.methodName`.

### Postfix (by GameType)

```csharp
public static bool Postfix(
    HarmonyLib.Harmony harmony,
    GameType type,
    string methodName,
    MethodInfo patchMethod)
```

Apply a Harmony Postfix using a pre-resolved `GameType`. The `GameType.ManagedType` is used to locate the method.

### Prefix (by GameType)

```csharp
public static bool Prefix(
    HarmonyLib.Harmony harmony,
    GameType type,
    string methodName,
    MethodInfo patchMethod)
```

Apply a Harmony Prefix using a pre-resolved `GameType`.

## Method Resolution

The target method is resolved with broad binding flags: `Public | NonPublic | Instance | Static`. If the initial lookup fails, the method walker tries `DeclaredOnly` on each type in the class hierarchy from the target type up to `System.Object`.

## Error Handling

Every failure path logs to `ModError` with context and returns `false`:

- `null` or empty type name
- `Assembly-CSharp` not loaded
- Type not found in `Assembly-CSharp`
- Invalid `GameType` or missing `ManagedType`
- Method not found on the resolved type
- Harmony `Patch()` call throws an exception

## Examples

### Basic postfix patch

```csharp
public class MyMod : MelonMod
{
    private HarmonyLib.Harmony _harmony;

    public override void OnInitializeMelon()
    {
        _harmony = new HarmonyLib.Harmony("com.mymod");

        var patchMethod = typeof(MyMod).GetMethod(
            nameof(AfterApplyDamage),
            BindingFlags.Static | BindingFlags.NonPublic);

        bool ok = GamePatch.Postfix(_harmony, "ActorComponent", "ApplyDamage", patchMethod);
        if (!ok)
            ModError.Warn("MyMod", "Failed to patch ActorComponent.ApplyDamage");
    }

    private static void AfterApplyDamage()
    {
        DevConsole.Log("Damage was applied!");
    }
}
```

### Prefix patch with GameType

```csharp
var actorType = GameType.Find("TacticalActor");

var prefixMethod = typeof(MyMod).GetMethod(
    nameof(BeforeExecuteSkill),
    BindingFlags.Static | BindingFlags.NonPublic);

GamePatch.Prefix(_harmony, actorType, "ExecuteSkill", prefixMethod);
```

### Checking return values for batch patching

```csharp
int applied = 0;
int failed = 0;

string[] methods = { "ApplyDamage", "ApplyHealing", "OnDeath", "OnRevive" };
foreach (var method in methods)
{
    var patch = typeof(MyMod).GetMethod(
        $"After{method}",
        BindingFlags.Static | BindingFlags.NonPublic);

    if (GamePatch.Postfix(_harmony, "ActorComponent", method, patch))
        applied++;
    else
        failed++;
}

DevConsole.Log($"Applied {applied}/{applied + failed} patches");
```
