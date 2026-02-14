# Vehicle

`Menace.SDK.Vehicle` -- Static class for vehicle operations including health, armor, modular equipment, and twin-fire detection.

## Overview

The Vehicle SDK provides safe access to vehicle-specific data and mechanics. It wraps the underlying game types for Vehicle, ItemsModularVehicle, and Slot to expose vehicle health, armor durability, modular equipment slots, and twin-fire capability detection.

Based on reverse engineering findings:
- `Vehicle.m_HitpointsPct` @ +0x20
- `Vehicle.m_ArmorDurabilityPct` @ +0x24
- `Vehicle.EquipmentSkills` @ +0x28
- `ItemsModularVehicle.Slots` @ +0x18
- `ItemsModularVehicle.IsTwinFire` @ +0x20

## Constants

### Modular Slot Types

```csharp
public const int MODULAR_WEAPON = 0;
public const int MODULAR_ARMOR = 1;
public const int MODULAR_ACCESSORY = 2;
```

## Methods

### GetVehicleInfo

```csharp
public static VehicleInfo GetVehicleInfo(GameObj entity)
```

Get comprehensive vehicle information for an entity.

**Parameters:**
- `entity` - The vehicle entity to query

**Returns:** `VehicleInfo` object with vehicle data, or `null` if entity is not a vehicle or query fails.

### GetModularVehicle

```csharp
public static ModularVehicleInfo GetModularVehicle(GameObj entity)
```

Get modular vehicle information including slots and twin-fire status.

**Parameters:**
- `entity` - The vehicle entity to query

**Returns:** `ModularVehicleInfo` object, or `null` if not available.

### GetSlotInfo

```csharp
public static SlotInfo GetSlotInfo(GameObj slot)
```

Get information about a specific equipment slot.

**Parameters:**
- `slot` - The slot game object

**Returns:** `SlotInfo` with slot details, or `null` if query fails.

### IsVehicle

```csharp
public static bool IsVehicle(GameObj entity)
```

Check if an entity is a vehicle.

**Parameters:**
- `entity` - The entity to check

**Returns:** `true` if entity is a vehicle, `false` otherwise.

### GetSlotTypeName

```csharp
public static string GetSlotTypeName(int slotType)
```

Get the human-readable name for a slot type.

**Parameters:**
- `slotType` - The slot type integer (0=Weapon, 1=Armor, 2=Accessory)

**Returns:** String name of the slot type.

## Types

### VehicleInfo

```csharp
public class VehicleInfo
{
    public string TemplateName { get; set; }
    public float HitpointsPct { get; set; }
    public float ArmorDurabilityPct { get; set; }
    public int BaseHp { get; set; }
    public int MaxHp { get; set; }
    public int Armor { get; set; }
    public int EquippedSlots { get; set; }
    public bool HasTwinFire { get; set; }
    public List<SlotInfo> Slots { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| TemplateName | string | The vehicle template name |
| HitpointsPct | float | Current HP as percentage (0.0-1.0) |
| ArmorDurabilityPct | float | Current armor durability as percentage (0.0-1.0) |
| BaseHp | int | Base hitpoints value |
| MaxHp | int | Maximum hitpoints |
| Armor | int | Current armor value |
| EquippedSlots | int | Number of slots with equipment |
| HasTwinFire | bool | Whether twin-fire is active |
| Slots | List\<SlotInfo\> | List of equipment slots |
| Pointer | IntPtr | Native pointer to the vehicle object |

### ModularVehicleInfo

```csharp
public class ModularVehicleInfo
{
    public bool HasTwinFire { get; set; }
    public int EquippedCount { get; set; }
    public List<SlotInfo> Slots { get; set; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| HasTwinFire | bool | Whether twin-fire is active |
| EquippedCount | int | Number of equipped slots |
| Slots | List\<SlotInfo\> | List of equipment slots |

### SlotInfo

```csharp
public class SlotInfo
{
    public int SlotType { get; set; }
    public string SlotTypeName { get; set; }
    public bool IsEnabled { get; set; }
    public string EquippedItem { get; set; }
    public bool HasItem { get; set; }
    public IntPtr Pointer { get; set; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| SlotType | int | Slot type ID (0=Weapon, 1=Armor, 2=Accessory) |
| SlotTypeName | string | Human-readable slot type name |
| IsEnabled | bool | Always true (slots are always enabled) |
| EquippedItem | string | Name of the mounted weapon template |
| HasItem | bool | Whether a weapon is mounted in this slot |
| Pointer | IntPtr | Native pointer to the slot object |

## Examples

### Checking if an actor is a vehicle

```csharp
var actor = TacticalController.GetActiveActor();
if (Vehicle.IsVehicle(actor))
{
    DevConsole.Log("Selected actor is a vehicle");
}
else
{
    DevConsole.Log("Selected actor is not a vehicle");
}
```

### Getting vehicle information

```csharp
var actor = TacticalController.GetActiveActor();
var info = Vehicle.GetVehicleInfo(actor);
if (info != null)
{
    DevConsole.Log($"Vehicle: {info.TemplateName}");
    DevConsole.Log($"HP: {info.BaseHp}/{info.MaxHp} ({info.HitpointsPct:P0})");
    DevConsole.Log($"Armor: {info.Armor} (Durability: {info.ArmorDurabilityPct:P0})");
    DevConsole.Log($"Twin-Fire: {info.HasTwinFire}");
}
```

### Inspecting equipment slots

```csharp
var actor = TacticalController.GetActiveActor();
var info = Vehicle.GetVehicleInfo(actor);
if (info != null && info.Slots.Count > 0)
{
    DevConsole.Log($"Equipped Slots: {info.EquippedSlots}");
    foreach (var slot in info.Slots)
    {
        var item = slot.HasItem ? slot.EquippedItem : "(empty)";
        var enabled = slot.IsEnabled ? "" : " [disabled]";
        DevConsole.Log($"  [{slot.SlotTypeName}] {item}{enabled}");
    }
}
```

### Checking twin-fire status

```csharp
var actor = TacticalController.GetActiveActor();
var modular = Vehicle.GetModularVehicle(actor);
if (modular != null)
{
    if (modular.HasTwinFire)
    {
        DevConsole.Log("Twin-fire is active!");
    }
    DevConsole.Log($"Equipped slots: {modular.EquippedCount}");
}
```

### Filtering vehicles from entity list

```csharp
var entities = EntitySpawner.ListEntities();
var vehicles = entities.Where(e => Vehicle.IsVehicle(e)).ToArray();
DevConsole.Log($"Found {vehicles.Length} vehicles on the battlefield");

foreach (var vehicle in vehicles)
{
    var info = Vehicle.GetVehicleInfo(vehicle);
    if (info != null)
    {
        DevConsole.Log($"  {info.TemplateName} - HP: {info.HitpointsPct:P0}");
    }
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

### vehicle

```
vehicle
```

Show vehicle information for the currently selected actor.

**Output includes:**
- Vehicle template name
- Current HP and max HP with percentage
- Armor value and durability percentage
- Number of equipped slots
- Twin-fire status
- List of all slots with equipped items and enabled/disabled status

**Example output:**
```
Vehicle: Tank_Heavy_01
HP: 450/500 (90%)
Armor: 15 (Durability: 75%)
Equipped Slots: 3
Twin-Fire: True
Slots:
  [Weapon] HeavyCannon [disabled]
  [Weapon] MachineGun
  [Armor] ReactivePlating
  [Accessory] (empty)
```

### twinfire

```
twinfire
```

Check twin-fire status for the currently selected vehicle.

**Example output:**
```
Twin-Fire Active: True
Equipped Slots: 3
```
