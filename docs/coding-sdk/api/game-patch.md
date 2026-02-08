# GamePatch

`Menace.SDK.GamePatch` -- Static helpers for applying Harmony patches at runtime with safe error handling.

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
            nameof(AfterTakeDamage),
            BindingFlags.Static | BindingFlags.NonPublic);

        bool ok = GamePatch.Postfix(_harmony, "Agent", "TakeDamage", patchMethod);
        if (!ok)
            ModError.Warn("MyMod", "Failed to patch Agent.TakeDamage");
    }

    private static void AfterTakeDamage()
    {
        DevConsole.Log("Someone took damage!");
    }
}
```

### Prefix patch with GameType

```csharp
var agentType = GameType.Find("Agent");

var prefixMethod = typeof(MyMod).GetMethod(
    nameof(BeforeMove),
    BindingFlags.Static | BindingFlags.NonPublic);

GamePatch.Prefix(_harmony, agentType, "StartMove", prefixMethod);
```

### Checking return values for batch patching

```csharp
int applied = 0;
int failed = 0;

string[] methods = { "TakeDamage", "Heal", "Die", "Revive" };
foreach (var method in methods)
{
    var patch = typeof(MyMod).GetMethod(
        $"After{method}",
        BindingFlags.Static | BindingFlags.NonPublic);

    if (GamePatch.Postfix(_harmony, "Agent", method, patch))
        applied++;
    else
        failed++;
}

DevConsole.Log($"Applied {applied}/{applied + failed} patches");
```
