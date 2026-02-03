# Templates

`Menace.SDK.Templates` -- Static API for reading, writing, and cloning game templates (ScriptableObjects managed by `DataTemplateLoader`).

Templates uses managed reflection through IL2CppInterop proxy types, so field access goes through .NET property getters/setters rather than raw memory reads. This makes it safer for complex types but requires that a managed proxy exists for the template type.

## Methods

### Find

```csharp
public static GameObj Find(string templateTypeName, string instanceName)
```

Find a specific template instance by its type name and Unity object name. Delegates to `GameQuery.FindByName`. Returns `GameObj.Null` if not found or if either argument is null/empty.

### FindAll

```csharp
public static GameObj[] FindAll(string templateTypeName)
```

Find all template instances of a given type. Delegates to `GameQuery.FindAll`. Returns an empty array on failure.

### Exists

```csharp
public static bool Exists(string templateTypeName, string instanceName)
```

Check whether a template with the given type and name exists. Equivalent to `!Find(typeName, name).IsNull`.

### ReadField

```csharp
public static object ReadField(GameObj template, string fieldName)
```

Read a field value from a template using managed reflection. The field name can be a **dotted path** (e.g., `"weaponStats.damage"`) to traverse nested objects.

Returns `null` on failure. Failures are logged to `ModError`.

The method:
1. Resolves the `GameType` and its `ManagedType`.
2. Creates a managed proxy wrapper via the `IntPtr` constructor.
3. Traverses the dotted path by reading .NET properties.

### WriteField

```csharp
public static bool WriteField(GameObj template, string fieldName, object value)
```

Write a field value on a template using managed reflection. Supports dotted paths -- navigates to the parent object, then sets the leaf property.

Automatic type conversion is applied for common types: `int`, `float`, `double`, `bool`, `string`, and enums. Returns `false` on failure.

### WriteFields

```csharp
public static int WriteFields(GameObj template, Dictionary<string, object> fields)
```

Write multiple fields on a template. Returns the number of fields successfully written.

### Clone

```csharp
public static GameObj Clone(string templateTypeName, string sourceName, string newName)
```

Clone an existing template via `UnityEngine.Object.Instantiate`. The clone is given the new name and marked with `HideFlags.DontUnloadUnusedAsset` to prevent garbage collection.

**Note:** This method does NOT register the clone in `DataTemplateLoader`. The main modpack loader cloning pipeline handles registration separately. Use this for low-level cloning only.

Returns `GameObj.Null` on failure.

## Examples

### Reading template fields

```csharp
var rifle = Templates.Find("WeaponDef", "AssaultRifle");
if (!rifle.IsNull)
{
    var damage = Templates.ReadField(rifle, "damage");
    var range = Templates.ReadField(rifle, "effectiveRange");
    DevConsole.Log($"Rifle: {damage} dmg, {range} range");
}
```

### Reading nested fields with dotted paths

```csharp
var agent = Templates.Find("AgentDef", "Rookie");
var accuracy = Templates.ReadField(agent, "combatStats.accuracy");
DevConsole.Log($"Rookie accuracy: {accuracy}");
```

### Modifying a template

```csharp
var rifle = Templates.Find("WeaponDef", "AssaultRifle");
Templates.WriteField(rifle, "damage", 50);
Templates.WriteField(rifle, "effectiveRange", 25.0f);
```

### Batch modification

```csharp
var rifle = Templates.Find("WeaponDef", "AssaultRifle");
int written = Templates.WriteFields(rifle, new Dictionary<string, object>
{
    { "damage", 50 },
    { "effectiveRange", 25.0f },
    { "clipSize", 30 },
    { "reloadTime", 2.5f }
});
DevConsole.Log($"Modified {written} fields on AssaultRifle");
```

### Cloning a template

```csharp
var clone = Templates.Clone("WeaponDef", "AssaultRifle", "HeavyRifle");
if (!clone.IsNull)
{
    Templates.WriteField(clone, "damage", 80);
    Templates.WriteField(clone, "clipSize", 20);
    DevConsole.Log("Created HeavyRifle from AssaultRifle");
}
```

### Checking existence

```csharp
if (!Templates.Exists("WeaponDef", "LaserRifle"))
{
    DevConsole.Log("LaserRifle does not exist yet, creating...");
    Templates.Clone("WeaponDef", "AssaultRifle", "LaserRifle");
}
```
