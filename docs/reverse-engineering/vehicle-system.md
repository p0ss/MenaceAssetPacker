# Vehicle System

## Overview

The Vehicle system manages vehicle entities with modular equipment slots. Vehicles can have weapons, armor, and accessories installed in configurable slots, with support for twin-fire detection (duplicate weapons) and visual slot mapping.

## Architecture

```
Vehicle (BaseUnitLeader subclass)
├── VehicleTemplate                       // Definition
├── EntityTemplate                        // Base entity data
├── HitpointsPct / ArmorDurabilityPct     // Health state
└── List<SkillTemplate>                   // Skills from equipment

ItemsModularVehicle (equipment manager)
├── BaseUnitLeader Owner                  // +0x10
├── Slot[] Slots                          // +0x18 (array)
└── bool HasTwinFire                      // +0x20

Slot (single equipment slot)
├── bool IsEnabled                        // +0x10
├── byte SlotTypeIndex                    // +0x11
├── ModularVehicleSlotTemplate Template   // +0x18
├── Item EquippedItem                     // +0x20
├── List<AnimatorParameters> Animators    // +0x28
└── List<Transform> Muzzles               // +0x30
```

## Vehicle Class

### Vehicle Field Layout

```c
public class Vehicle : BaseUnitLeader {
    // BaseUnitLeader fields...           // +0x00 - 0x0F
    VehicleTemplate Template;             // +0x10 (vehicle definition)
    EntityTemplate EntityTemplate;        // +0x18 (base entity)
    float HitpointsPct;                   // +0x20 (0.0-1.0, init: 1.0)
    float ArmorDurabilityPct;             // +0x24 (0.0-1.0, init: 1.0)
    List<SkillTemplate> EquipmentSkills;  // +0x28 (skills from modular items)
}
```

### Key Vehicle Methods

```c
// Constructor
void Vehicle.ctor(VehicleTemplate template, EntityTemplate entity);  // @ 1805c8f30

// Health
int GetBaseHp();                          // @ 1805c8a00
int GetBaseMaxHp();                       // @ 1805c8a50
float GetHitpointsPct();                  // (Field at +0x20)
void HealAndClearDamageEffects();         // @ 1805c8a80

// Armor
int GetArmor();                           // @ 1805c89d0
void SetArmorDurabilityPct(float pct);    // @ 1805c8f00

// Mission events
void OnMissionStarted();                  // @ 1805c8d20
void OnMissionFinished();                 // @ 1805c8af0

// Persistence
void ProcessSaveState(SaveState state);   // @ 1805c8e60
```

## ItemsModularVehicle Class

Manages modular equipment slots for vehicles.

### ItemsModularVehicle Field Layout

```c
public class ItemsModularVehicle {
    // Object header                      // +0x00 - 0x0F
    BaseUnitLeader Owner;                 // +0x10 (owning vehicle)
    Slot[] Slots;                         // +0x18 (equipment slots array)
    bool HasTwinFire;                     // +0x20 (duplicate weapon detected)
}
```

### Key ItemsModularVehicle Methods

```c
// Constructor
void ItemsModularVehicle.ctor(BaseUnitLeader owner);  // @ 1805ba9e0

// Equipment management
bool Add(Item item);                      // @ 1805b92e0
void Place(Item item, int slotIndex);     // @ 1805ba0f0
void Remove(Item item);                   // @ 1805ba400
void RemoveAllWeapons();                  // @ 1805ba390

// Slot queries
Slot GetSlot(int index);                  // @ 1805b9dc0
Slot GetFreeSlot(int slotType);           // @ 1805b9ac0
Slot GetSlotForSkill(Skill skill);        // @ 1805b9c30
Slot GetTwinFireSlot();                   // @ 1805b9e60
int GetSlotTypeIndex(int slotType);       // @ 1805b9ca0
int GetEquippedCount();                   // @ 1805b99e0
int GetDifferentVisualSlot(int current);  // @ 1805b98b0

// State queries
bool IsItemEquipped(Item item);           // @ 1805b9fb0
bool IsContainerDisabled(int slotType);   // @ 1805b9eb0
bool IsLightSlotDeactivated();            // @ 1805ba010

// Twin-fire detection
void CheckForTwinFire();                  // @ 1805b95a0

// Muzzle access
Transform GetMuzzle(int slotIndex);       // @ 1805b9b60

// Animation
void SetAnimatorBool(string param, bool value);  // @ 1805ba630
void SetAnimatorBoolTwinFire(string param, bool value);  // @ 1805ba470
void SetAnimatorTrigger(string param);    // @ 1805ba800

// Property access
VehicleTemplate get_VehicleTemplate();    // @ 1805bac90
```

## Slot Class

Represents a single modular equipment slot.

### Slot Field Layout

```c
public class Slot {
    // Object header                      // +0x00 - 0x0F
    bool IsEnabled;                       // +0x10 (slot can accept items)
    byte SlotTypeIndex;                   // +0x11 (index within slot type)
    // padding                            // +0x12 - 0x17
    ModularVehicleSlotTemplate Template;  // +0x18 (slot definition)
    Item EquippedItem;                    // +0x20 (currently equipped item)
    List<AnimatorParameters> Animators;   // +0x28 (animation parameters)
    List<Transform> Muzzles;              // +0x30 (weapon muzzle transforms)
}
```

## Modular Slot Types

```c
// ModularVehicleSlotTemplate.SlotType (+0x10)
enum ModularSlotType {
    Weapon = 0,      // Maps to ItemSlotType.VehicleWeapon (8)
    Armor = 1,       // Maps to ItemSlotType.VehicleArmor (9)
    Accessory = 2    // Maps to ItemSlotType.VehicleArmor (9) or VehicleAccessory (10)
}

// Item slot type mapping:
// ItemSlotType 8 (VehicleWeapon) → ModularSlotType.Weapon
// ItemSlotType 9 (VehicleArmor) → ModularSlotType.Armor or Accessory
// ItemSlotType 10 (VehicleAccessory) → ModularSlotType.Accessory
```

## Add Item Flow

```c
// @ 1805b92e0
bool Add(Item item) {
    if (item == null) return false;

    ItemTemplate template = item.GetTemplate();
    if (!(template is ModularVehicleWeaponTemplate weaponTemplate)) {
        LogError("Item is not a vehicle weapon template");
        return false;
    }

    int itemSlotType = weaponTemplate.SlotType;  // +0xE8

    // Check light slot restrictions
    if (itemSlotType == 8 && IsLightSlotDeactivated()) {
        return false;
    }

    // Exclusive weapons remove all others
    if (weaponTemplate.IsExclusive) {  // +0x189
        RemoveAllWeapons();
    }

    // Find matching slot
    foreach (Slot slot in Slots) {
        if (slot.Template == null) continue;

        int slotType = slot.Template.SlotType;  // +0x10

        bool matches = false;
        if (slotType == 0) {  // Weapon slot
            matches = (itemSlotType == 8);
        } else if (slotType == 1) {  // Armor slot
            matches = (itemSlotType == 9);
        } else if (slotType == 2) {  // Accessory slot
            matches = (itemSlotType == 9 || itemSlotType == 10);
        }

        if (matches && slot.EquippedItem == null) {
            slot.EquippedItem = item;
            return true;
        }
    }

    LogWarning($"No free slot for {template.name} on {Owner.name}");
    return false;
}
```

## Twin-Fire Detection

Detects when two identical weapons are equipped for twin-fire bonuses.

```c
// @ 1805b95a0
void CheckForTwinFire() {
    HasTwinFire = false;  // +0x20

    if (Slots == null || Slots.Length == 0) return;
    if (Slots[0].EquippedItem == null) return;

    ModularVehicleWeaponTemplate firstWeapon =
        Slots[0].EquippedItem.GetTemplate() as ModularVehicleWeaponTemplate;

    if (firstWeapon == null) return;

    int matchCount = 0;
    foreach (Slot slot in Slots) {
        if (slot.EquippedItem != null) {
            ItemTemplate slotTemplate = slot.EquippedItem.GetTemplate();
            if (slotTemplate == firstWeapon) {
                matchCount++;
            }
        }
    }

    if (matchCount > 1) {
        HasTwinFire = true;
        Debug.Log($"{Owner.name} has twin-fire with {firstWeapon.name}");
    }
}
```

## Constructor Flow

```c
// @ 1805ba9e0
void ItemsModularVehicle.ctor(BaseUnitLeader owner) {
    this.Owner = owner;  // +0x10

    VehicleTemplate vehicleTemplate = owner.Template;
    if (vehicleTemplate == null) return;

    ModularVehicleConfig config = vehicleTemplate.ModularConfig;  // +0x2F0
    if (config?.Slots == null) return;

    int slotCount = config.Slots.Count;  // +0x80
    Slots = new Slot[slotCount];  // +0x18

    byte weaponIndex = 0, armorIndex = 0, accessoryIndex = 0;
    byte enabledIndex = 1;

    for (int i = 0; i < slotCount; i++) {
        Slot slot = new Slot();
        slot.Animators = new List<AnimatorParameters>();
        slot.Muzzles = new List<Transform>();
        slot.Template = config.Slots[i];
        slot.IsEnabled = enabledIndex++;  // Sequential enabling

        // Assign type-specific index
        int slotType = slot.Template.SlotType;
        byte typeIndex;
        if (slotType == 0) {
            typeIndex = weaponIndex++;
        } else if (slotType == 1) {
            typeIndex = armorIndex++;
        } else if (slotType == 2) {
            typeIndex = accessoryIndex++;
        } else {
            typeIndex = slot.SlotTypeIndex;
        }
        slot.SlotTypeIndex = typeIndex;

        Slots[i] = slot;
    }
}
```

## Save State Serialization

### Vehicle Save Order

```
ProcessSaveState order:
1. HitpointsPct        @ +0x20 (float, version >= 24)
   - If version < 24: Read int, set to 1.0
2. ArmorDurabilityPct  @ +0x24 (float)
3. EquipmentSkills     @ +0x28 (List<SkillTemplate>)
```

## Modding Hooks

### Intercept Equipment Addition

```csharp
[HarmonyPatch(typeof(ItemsModularVehicle), "Add")]
class VehicleEquipPatch {
    static void Postfix(ItemsModularVehicle __instance, Item item, bool __result) {
        if (__result) {
            Logger.Msg($"Equipped {item.GetTemplate()?.Name} to vehicle");
            __instance.CheckForTwinFire();
        }
    }
}
```

### Modify Twin-Fire Detection

```csharp
[HarmonyPatch(typeof(ItemsModularVehicle), "CheckForTwinFire")]
class TwinFirePatch {
    static void Postfix(ItemsModularVehicle __instance) {
        // Force twin-fire for testing
        // __instance.HasTwinFire = true;
    }
}
```

### Override Vehicle Health

```csharp
[HarmonyPatch(typeof(Vehicle), "GetBaseMaxHp")]
class VehicleHpPatch {
    static void Postfix(ref int __result) {
        // Double vehicle HP
        __result *= 2;
    }
}
```

### Custom Slot Validation

```csharp
[HarmonyPatch(typeof(ItemsModularVehicle), "GetFreeSlot")]
class SlotValidationPatch {
    static void Postfix(ItemsModularVehicle __instance, int slotType, ref Slot __result) {
        // Custom slot assignment logic
    }
}
```

## Key Constants

```c
// Default health percentages
const float DEFAULT_HP_PCT = 1.0f;        // 0x3F800000
const float DEFAULT_ARMOR_PCT = 1.0f;     // 0x3F800000

// Save version threshold for HP format
const int VERSION_FLOAT_HP = 24;

// Modular slot types
const int MODULAR_WEAPON = 0;
const int MODULAR_ARMOR = 1;
const int MODULAR_ACCESSORY = 2;

// Item slot types for vehicles
const int ITEM_SLOT_VEHICLE_WEAPON = 8;
const int ITEM_SLOT_VEHICLE_ARMOR = 9;
const int ITEM_SLOT_VEHICLE_ACCESSORY = 10;
```

## Related Classes

- **VehicleTemplate**: Vehicle definition with modular config
- **ModularVehicleConfig**: Defines available slots
- **ModularVehicleSlotTemplate**: Individual slot definition
- **ModularVehicleWeaponTemplate**: Vehicle weapon item template
- **BaseUnitLeader**: Parent class for vehicles
- **ItemContainer**: Standard item storage (non-modular)
