# Item System

## Overview

The Item system manages equipment, weapons, armor, and consumables. Items are created from templates, stored in ItemContainers, and provide skills/stats to their owners. The system uses slot-based inventory with 11 slot types.

## Architecture

```
BaseItemTemplate (definition)
├── ItemTemplate (standard items)
├── WeaponTemplate
├── ArmorTemplate
└── VehicleItemTemplate

BaseItem (runtime instance)
├── Item (standard item)
└── ListItem (stackable)

ItemContainer (inventory)
├── List<Item>[11] (slots by type)
├── Owner reference
└── ModularVehicle slots (optional)
```

## Item Class

### Item Field Layout

```c
public class Item : BaseItem {
    // BaseItem header                    // +0x00 - 0x0F
    string GUID;                          // +0x10 (unique identifier)
    BaseItemTemplate Template;            // +0x18 (item definition)
    // padding                            // +0x20
    ItemContainer Container;              // +0x28 (owning container)
    List<BaseSkill> Skills;               // +0x30 (skills provided by item)
}
```

### Key Item Methods

```c
// Constructor
void Item.ctor(BaseItemTemplate template, string guid);  // @ 180826350

// Template access
BaseItemTemplate GetTemplate();           // @ 180825f70
string GetID();                           // @ 180825f50

// Skill management
void AddSkills();                         // @ 180825cc0
void RemoveSkills();                      // @ 180826120

// Container events
void OnAddedToContainer(ItemContainer container);      // @ 180826000
void OnBeforeRemovedFromContainer(ItemContainer container);  // @ 180826070
void OnRemovedFromContainer(ItemContainer container);  // @ 1808260c0
```

## BaseItem Class

Base class for all item instances.

### Key BaseItem Methods

```c
// Trade value
int GetTradeValue();                      // @ 18081fec0
int GetHighestTradeValue();               // @ 18081f630
int GetLowestTradeValue();                // @ 18081fa40
int SumTradeValue();                      // @ 180820060

// Rarity
Rarity GetHighestRarity();                // @ 18081f440
Rarity GetLowestRarity();                 // @ 18081f820

// Tags
int CountTag(string tag);                 // @ 18081f240
bool IsTemporary();                       // @ 180820020

// Display
TooltipData AppendTooltipData(...);       // @ 18081f0a0
TooltipData AppendTooltipStats(...);      // @ 18081f0e0
TooltipData GetSimpleTooltipData(...);    // @ 18081fc50
string ToString();                        // @ 180820250
```

## ItemContainer Class

Manages item storage with slot-based organization.

### ItemContainer Field Layout

```c
public class ItemContainer {
    // Object header                      // +0x00 - 0x0F
    List<Item>[11] SlotLists;             // +0x10 (array of 11 slot lists)
    IEntityProperties Owner;              // +0x18 (entity owning this container)
    ItemsModularVehicle ModularVehicle;   // +0x20 (vehicle slots if applicable)
    GameObject[] VisualSlots;             // +0x28 (array of 13 GameObjects)
}
```

### Item Slot Types

```c
enum ItemSlotType {
    Weapon1 = 0,
    Weapon2 = 1,
    Armor = 2,
    Accessory1 = 3,
    Accessory2 = 4,
    Consumable1 = 5,
    Consumable2 = 6,
    Grenade = 7,
    VehicleWeapon = 8,      // Modular vehicle
    VehicleArmor = 9,       // Modular vehicle
    VehicleAccessory = 10   // Modular vehicle
}
```

### Key ItemContainer Methods

```c
// Constructor
void ItemContainer.ctor(IEntityProperties owner, ItemContainerConfig config);  // @ 180825900

// Adding items
bool Add(Item item, bool expandSlots);    // @ 180821c80
void Place(Item item, int slotType, int index);  // @ 180823b10
void AddSlots(int slotType, int count);   // @ 180821ab0
void AddModularVehicleSlots(ModularSlotConfig config);  // @ 180821990

// Removing items
void Remove(Item item);                   // @ 1808250f0
void Remove(int slotType, int index, bool returnToOwned);  // @ 1808252b0
void RemoveAll();                         // @ 180824af0
void RemoveModularVehicleSlots();         // @ 180824c10

// Queries
Item GetItemAtSlot(int slotType, int index);  // @ 1808228e0
Item GetItemByID(string guid);            // @ 1808229f0
List<Item> GetAllItems();                 // @ 180822610
List<Item> GetAllItemsAtSlot(int slotType);  // @ 1808225d0
List<Item> GetAllItemsAtSlotCopy(int slotType);  // @ 1808222d0
List<Item> GetAllItemsAtSlotNoDuplicates(int slotType);  // @ 180822350
List<Item> GetItemsWithTag(string tag);   // @ 180822c30
int GetItemSlotCount(int slotType);       // @ 180822bb0
bool HasItem(Item item);                  // @ 180823000
bool ContainsTag(string tag);             // @ 180821f10
int GetExclusiveItemIndex(Item item);     // @ 180822800

// Template queries
Item GetListItemWithTemplate(BaseItemTemplate template);  // @ 180822e50

// Iteration
void ForEachItem(Action<Item> callback);  // @ 180822130

// Tags
void QueryTags(TagQuery query);           // @ 180824850

// Events
void OnItemAdded(Item item, bool addSkills);  // @ 1808233d0
void OnItemRemoved(Item item);            // @ 1808238c0
void OnItemAddModularVehicles(Item item); // @ 1808231a0
void OnItemRemovedModularVehicles(Item item);  // @ 180823700

// Audio/Visual
void PlayAnimationSound(Item item);       // @ 180823ff0

// Persistence
void ProcessSaveState(SaveState state);   // @ 180824480
```

## BaseItemTemplate Class

Template defining item properties.

### Key BaseItemTemplate Fields

```c
public class BaseItemTemplate : DataTemplate {
    // DataTemplate fields...
    // ...
    int SlotType;                         // +0xE8 (ItemSlotType enum)
    // ...
    List<SkillTemplate> Skills;           // +0x100 (skills provided)
}
```

### Key BaseItemTemplate Methods

```c
// Constructor
void BaseItemTemplate.ctor();             // @ 18054cae0

// Item creation
BaseItem CreateItem(string guid);         // @ 18054c360
string CreateGuid();                      // @ 18054c320

// Tags
bool HasTag(string tag);                  // @ 18054cad0
void ForEachTag(Action<string> callback); // @ 18054c3d0

// Display
string GetShortName();                    // @ 18054c8e0
TooltipData AppendTooltipData(...);       // @ 18054c280
TooltipData GetSimpleTooltipData(...);    // @ 18054c950
TooltipData GetRewardTooltipData(...);    // @ 18054c780
List<LocalizedString> GetLocalizedStrings();  // @ 18054c540
```

## AddSkills Flow

When an item is added to a container, skills are granted:

```c
// @ 180825cc0
void AddSkills() {
    // Create base ItemSkill (represents the item itself as usable)
    ItemSkill baseSkill = new ItemSkill(null, this);

    ItemContainer container = this.Container;  // +0x28
    if (container == null || container.Owner == null) return;

    // Get owner's skill container
    SkillContainer skills = container.Owner.GetSkillContainer();
    skills.Add(baseSkill);

    // Track skill in item's skill list
    this.Skills.Add(baseSkill);  // +0x30

    // Add template-defined skills
    ItemTemplate template = GetTemplate();
    if (template != null && template.Skills != null) {  // +0x100
        foreach (SkillTemplate skillTemplate in template.Skills) {
            Skill skill = skillTemplate.CreateSkill();
            skill.SourceItem = this;  // +0x28

            if (skills.Add(skill)) {
                this.Skills.Add(skill);
            }
        }
    }
}
```

## ItemContainer.Add Flow

```c
// @ 180821c80
bool Add(Item item, bool expandSlots) {
    if (item == null) {
        LogError("Adding null item");
        return false;
    }

    if (item.Container != null) {
        return false;  // Already in a container
    }

    ItemTemplate template = item.GetTemplate();
    int slotType = template.SlotType;  // +0xE8

    // Handle modular vehicle slots (8, 9, 10)
    if (slotType >= 8 && slotType <= 10) {
        if (ModularVehicle != null) {
            if (ModularVehicle.Add(item)) {
                UpdateModularVehicleItems(true);
                return true;
            }
        }
        return false;
    }

    // Find empty slot
    List<Item> slotList = SlotLists[slotType];  // +0x10
    for (int i = 0; i < slotList.Count; i++) {
        if (slotList[i] == null) {
            slotList[i] = item;
            OnItemAdded(item, true);
            return true;
        }
    }

    // No empty slot - expand if allowed
    if (expandSlots) {
        AddSlots(slotType, 1);
        return Add(item, false);  // Retry
    }

    return false;
}
```

## Save State Serialization

```c
// @ 180824480
void ProcessSaveState(SaveState state) {
    if (state.IsLoading) {
        OwnedItems ownedItems = StrategyState.Instance.OwnedItems;

        int slotCount = state.ReadInt();  // Always 11
        for (int slotType = 0; slotType < slotCount; slotType++) {
            state.WriteInt(slotType);
            int itemCount = state.ReadInt();

            for (int i = 0; i < itemCount; i++) {
                string guid = state.ReadString();
                if (guid != "") {
                    Item item = ownedItems.GetItemByGuid(guid);
                    if (item != null) {
                        Place(item, slotType, i);
                    } else {
                        Remove(slotType, i, true);
                    }
                }
            }
        }
    } else {
        // Saving
        state.WriteInt(11);  // Slot count
        for (int slotType = 0; slotType < 11; slotType++) {
            state.WriteInt(slotType);
            List<Item> items = SlotLists[slotType];
            state.WriteInt(items.Count);

            foreach (Item item in items) {
                string guid = (item != null) ? item.GUID : "";
                state.WriteString(guid);
            }
        }
    }
}
```

## OwnedItems Integration

Items are owned by the global OwnedItems manager:

```c
// Access via StrategyState
OwnedItems ownedItems = StrategyState.Instance.OwnedItems;  // +0x80

// Find item by GUID
Item item = ownedItems.GetItemByGuid(guid);

// Items reference their container
item.Container;  // +0x28
```

## Modding Hooks

### Intercept Item Addition

```csharp
[HarmonyPatch(typeof(ItemContainer), "Add")]
class ItemAddPatch {
    static void Postfix(ItemContainer __instance, Item item, bool __result) {
        if (__result) {
            Logger.Msg($"Item added: {item.GetTemplate()?.Name}");
        }
    }
}
```

### Modify Item Skills

```csharp
[HarmonyPatch(typeof(Item), "AddSkills")]
class ItemSkillsPatch {
    static void Postfix(Item __instance) {
        // Add custom skill to all items
        var template = DataTemplateLoader.Get<SkillTemplate>("BonusSkill");
        if (template != null) {
            var skill = template.CreateSkill();
            // Add to owner's skill container
        }
    }
}
```

### Custom Item Creation

```csharp
[HarmonyPatch(typeof(BaseItemTemplate), "CreateItem")]
class CreateItemPatch {
    static void Postfix(BaseItemTemplate __instance, ref BaseItem __result) {
        Logger.Msg($"Created item: {__instance.Name}");
    }
}
```

### Override Trade Values

```csharp
[HarmonyPatch(typeof(BaseItem), "GetTradeValue")]
class TradeValuePatch {
    static void Postfix(ref int __result) {
        // Double all trade values
        __result *= 2;
    }
}
```

## Key Constants

```c
// Number of slot types
const int SLOT_TYPE_COUNT = 11;

// Visual slot count (includes extras)
const int VISUAL_SLOT_COUNT = 13;

// Modular vehicle slot types
const int SLOT_VEHICLE_WEAPON = 8;
const int SLOT_VEHICLE_ARMOR = 9;
const int SLOT_VEHICLE_ACCESSORY = 10;
```

## Related Classes

- **OwnedItems**: Global item ownership tracking (StrategyState+0x80)
- **ItemsModularVehicle**: Vehicle-specific equipment slots
- **SkillContainer**: Manages skills from items
- **ItemTemplate**: Standard item definition
- **WeaponTemplate**: Weapon-specific properties
- **ArmorTemplate**: Armor-specific properties
