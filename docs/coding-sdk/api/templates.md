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

### FindAllManaged

```csharp
public static object[] FindAllManaged(string templateTypeName)
```

Find all template instances of a given type and return them as properly-typed IL2CPP proxy objects. Unlike `FindAll` which returns `GameObj[]`, this method returns actual managed proxy instances (e.g., `Il2CppMenace.Strategy.UnitLeaderTemplate`).

Use this when you need to work with the templates via reflection or pass them to game APIs that expect typed objects. Returns an empty array on failure.

### Get&lt;T&gt;

```csharp
public static T Get<T>(string templateTypeName, string instanceName) where T : class
```

Find a specific template instance and return it as a typed IL2CPP proxy object. This is the preferred method when you know the type at compile time.

Returns `null` if not found or if type conversion fails.

### GetAll&lt;T&gt;

```csharp
public static List<T> GetAll<T>(string templateTypeName) where T : class
```

Find all template instances of a given type and return them as typed IL2CPP proxy objects. This is the preferred method when you know the type at compile time and need a strongly-typed collection.

Returns an empty list on failure.

### GetManaged

```csharp
public static object GetManaged(string templateTypeName, string instanceName)
```

Find a specific template instance and return it as a managed proxy object. Use this when you need to work with the template via reflection but don't know the exact type at compile time.

Returns `null` if not found.

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
var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (!rifle.IsNull)
{
    var damage = Templates.ReadField(rifle, "Damage");
    var maxRange = Templates.ReadField(rifle, "MaxRange");
    DevConsole.Log($"ARC-762: {damage} dmg, {maxRange} range");
}
```

### Reading nested fields with dotted paths

```csharp
var leader = Templates.Find("UnitLeaderTemplate", "squad_leader.pike");
var hiringCost = Templates.ReadField(leader, "HiringCosts");
DevConsole.Log($"Pike hiring cost: {hiringCost}");
```

### Modifying a template

```csharp
var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
Templates.WriteField(rifle, "Damage", 15.0f);
Templates.WriteField(rifle, "MaxRange", 9);
```

### Batch modification

```csharp
var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
int written = Templates.WriteFields(rifle, new Dictionary<string, object>
{
    { "Damage", 15.0f },
    { "MaxRange", 9 },
    { "AccuracyBonus", 5.0f },
    { "ArmorPenetration", 25.0f }
});
DevConsole.Log($"Modified {written} fields on ARC-762");
```

### Cloning a template

```csharp
var clone = Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_heavy_rifle");
if (!clone.IsNull)
{
    Templates.WriteField(clone, "Damage", 20.0f);
    Templates.WriteField(clone, "ArmorPenetration", 40.0f);
    DevConsole.Log("Created weapon.custom_heavy_rifle from ARC-762");
}
```

### Checking existence

```csharp
if (!Templates.Exists("WeaponTemplate", "weapon.custom_laser_rifle"))
{
    DevConsole.Log("Custom laser rifle does not exist yet, creating...");
    Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_laser_rifle");
}
```

### Getting typed templates (compile-time type known)

```csharp
// When you have the IL2CppInterop proxy type available
var rifle = Templates.Get<Il2CppMenace.Strategy.WeaponTemplate>("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (rifle != null)
{
    DevConsole.Log($"Rifle damage: {rifle.Damage}");
}
```

### Getting all templates as typed objects

```csharp
// Get all unit leaders as typed objects
var allLeaders = Templates.GetAll<Il2CppMenace.Strategy.UnitLeaderTemplate>("Menace.Strategy.UnitLeaderTemplate");
foreach (var leader in allLeaders)
{
    DevConsole.Log($"Leader: {leader.UnitTitle}");
}
```

### Working with templates via reflection

```csharp
// When you need to use reflection (e.g., passing to game APIs)
var allLeaders = Templates.FindAllManaged("Menace.Strategy.UnitLeaderTemplate");
foreach (var leader in allLeaders)
{
    // leader is an actual Il2CppMenace.Strategy.UnitLeaderTemplate instance
    var titleProp = leader.GetType().GetProperty("UnitTitle");
    var title = titleProp?.GetValue(leader);
    DevConsole.Log($"Leader: {title}");
}
```

### Getting a single template for reflection

```csharp
// Get a specific template as a managed object
var leader = Templates.GetManaged("Menace.Strategy.UnitLeaderTemplate", "pilot.bog");
if (leader != null)
{
    // Use reflection to access properties
    var hirableList = GetHirableLeadersList(); // hypothetical game API
    hirableList.Add(leader); // Pass typed object to game API
}
```
