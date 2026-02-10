# Inventory

`Menace.SDK.Inventory` -- Static class for item and inventory operations including equipment, containers, and trade values.

## Constants

### Slot Types

```csharp
public const int SLOT_WEAPON1 = 0;
public const int SLOT_WEAPON2 = 1;
public const int SLOT_ARMOR = 2;
public const int SLOT_ACCESSORY1 = 3;
public const int SLOT_ACCESSORY2 = 4;
public const int SLOT_CONSUMABLE1 = 5;
public const int SLOT_CONSUMABLE2 = 6;
public const int SLOT_GRENADE = 7;
public const int SLOT_VEHICLE_WEAPON = 8;
public const int SLOT_VEHICLE_ARMOR = 9;
public const int SLOT_VEHICLE_ACCESSORY = 10;
public const int SLOT_TYPE_COUNT = 11;
```

## Methods

### GetOwnedItems

```csharp
public static GameObj GetOwnedItems()
```

Get the global OwnedItems manager for accessing all owned items in the game.

**Returns:** `GameObj` representing the OwnedItems manager, or `GameObj.Null` if not available.

### GetContainer

```csharp
public static GameObj GetContainer(GameObj entity)
```

Get the item container for an entity (actor).

**Parameters:**
- `entity` - The entity to get the container for

**Returns:** `GameObj` representing the ItemContainer, or `GameObj.Null` if not found.

### GetAllItems

```csharp
public static List<ItemInfo> GetAllItems(GameObj container)
```

Get all items in a container.

**Parameters:**
- `container` - The container to query

**Returns:** List of `ItemInfo` objects for all items in the container.

### GetItemsInSlot

```csharp
public static List<ItemInfo> GetItemsInSlot(GameObj container, int slotType)
```

Get items in a specific slot type.

**Parameters:**
- `container` - The container to query
- `slotType` - The slot type constant (0-10)

**Returns:** List of `ItemInfo` objects for items in the specified slot.

### GetItemAt

```csharp
public static GameObj GetItemAt(GameObj container, int slotType, int index)
```

Get the item at a specific slot and index.

**Parameters:**
- `container` - The container to query
- `slotType` - The slot type constant (0-10)
- `index` - The index within the slot

**Returns:** `GameObj` representing the item, or `GameObj.Null` if not found.

### GetItemInfo

```csharp
public static ItemInfo GetItemInfo(GameObj item)
```

Get detailed information about an item.

**Parameters:**
- `item` - The item to get information for

**Returns:** `ItemInfo` object with item details, or `null` if not found.

### GetContainerInfo

```csharp
public static ContainerInfo GetContainerInfo(GameObj container)
```

Get information about a container including slot counts.

**Parameters:**
- `container` - The container to query

**Returns:** `ContainerInfo` object with container details, or `null` if not found.

### FindByGUID

```csharp
public static GameObj FindByGUID(string guid)
```

Find an item by its GUID.

**Parameters:**
- `guid` - The unique identifier of the item

**Returns:** `GameObj` representing the item, or `GameObj.Null` if not found.

### HasItemWithTag

```csharp
public static bool HasItemWithTag(GameObj container, string tag)
```

Check if a container has an item with a specific tag.

**Parameters:**
- `container` - The container to search
- `tag` - The tag to search for

**Returns:** `true` if an item with the tag exists, `false` otherwise.

### GetItemsWithTag

```csharp
public static List<ItemInfo> GetItemsWithTag(GameObj container, string tag)
```

Get all items with a specific tag.

**Parameters:**
- `container` - The container to search
- `tag` - The tag to filter by

**Returns:** List of `ItemInfo` objects for items with the specified tag.

### GetEquippedWeapons

```csharp
public static List<ItemInfo> GetEquippedWeapons(GameObj entity)
```

Get equipped weapons for an entity (items in SLOT_WEAPON1 and SLOT_WEAPON2).

**Parameters:**
- `entity` - The entity to query

**Returns:** List of `ItemInfo` objects for equipped weapons.

### GetEquippedArmor

```csharp
public static ItemInfo GetEquippedArmor(GameObj entity)
```

Get equipped armor for an entity.

**Parameters:**
- `entity` - The entity to query

**Returns:** `ItemInfo` for the equipped armor, or `null` if none equipped.

### GetTotalTradeValue

```csharp
public static int GetTotalTradeValue(GameObj container)
```

Get total trade value of all items in a container.

**Parameters:**
- `container` - The container to calculate value for

**Returns:** Total trade value as an integer.

### GetSlotTypeName

```csharp
public static string GetSlotTypeName(int slotType)
```

Get the human-readable name for a slot type.

**Parameters:**
- `slotType` - The slot type constant (0-10)

**Returns:** String name like "Weapon1", "Armor", "Grenade", etc.

## Types

### ItemInfo

```csharp
public class ItemInfo
{
    public string GUID { get; set; }           // Unique identifier
    public string TemplateName { get; set; }   // Item template/type name
    public int SlotType { get; set; }          // Slot type constant (0-10)
    public string SlotTypeName { get; set; }   // Human-readable slot name
    public int TradeValue { get; set; }        // Trade/sell value
    public string Rarity { get; set; }         // Item rarity (e.g., "Common", "Rare")
    public int SkillCount { get; set; }        // Number of skills on the item
    public bool IsTemporary { get; set; }      // Whether item is temporary
    public IntPtr Pointer { get; set; }        // Native pointer
}
```

### ContainerInfo

```csharp
public class ContainerInfo
{
    public int TotalItems { get; set; }        // Total number of items
    public int[] SlotCounts { get; set; }      // Item count per slot type (length 11)
    public bool HasModularVehicle { get; set; } // Whether container has a modular vehicle
    public IntPtr Pointer { get; set; }        // Native pointer
}
```

## Examples

### Listing all inventory items

```csharp
var actor = TacticalController.GetActiveActor();
var container = Inventory.GetContainer(actor);
var items = Inventory.GetAllItems(container);

foreach (var item in items)
{
    DevConsole.Log($"[{item.SlotTypeName}] {item.TemplateName} - ${item.TradeValue}");
}
```

### Getting equipped weapons

```csharp
var actor = TacticalController.GetActiveActor();
var weapons = Inventory.GetEquippedWeapons(actor);

foreach (var weapon in weapons)
{
    DevConsole.Log($"Weapon: {weapon.TemplateName} ({weapon.Rarity ?? "Common"})");
    DevConsole.Log($"  Skills: {weapon.SkillCount}");
}
```

### Checking for specific items

```csharp
var actor = TacticalController.GetActiveActor();
var container = Inventory.GetContainer(actor);

// Check if actor has any items with "Medkit" tag
if (Inventory.HasItemWithTag(container, "Medkit"))
{
    var medkits = Inventory.GetItemsWithTag(container, "Medkit");
    DevConsole.Log($"Found {medkits.Count} medkit(s)");
}
```

### Working with specific slots

```csharp
var actor = TacticalController.GetActiveActor();
var container = Inventory.GetContainer(actor);

// Get items in the grenade slot
var grenades = Inventory.GetItemsInSlot(container, Inventory.SLOT_GRENADE);
foreach (var grenade in grenades)
{
    DevConsole.Log($"Grenade: {grenade.TemplateName}");
}

// Get armor
var armor = Inventory.GetEquippedArmor(actor);
if (armor != null)
{
    DevConsole.Log($"Armor: {armor.TemplateName} - Rarity: {armor.Rarity}");
}
```

### Calculating inventory value

```csharp
var actor = TacticalController.GetActiveActor();
var container = Inventory.GetContainer(actor);

var totalValue = Inventory.GetTotalTradeValue(container);
var containerInfo = Inventory.GetContainerInfo(container);

DevConsole.Log($"Inventory Value: ${totalValue}");
DevConsole.Log($"Total Items: {containerInfo.TotalItems}");

// Show breakdown by slot
for (int i = 0; i < Inventory.SLOT_TYPE_COUNT; i++)
{
    if (containerInfo.SlotCounts[i] > 0)
    {
        DevConsole.Log($"  {Inventory.GetSlotTypeName(i)}: {containerInfo.SlotCounts[i]} items");
    }
}
```

### Finding an item by GUID

```csharp
// Find a specific item by its unique ID
var item = Inventory.FindByGUID("item-guid-12345");
if (!item.IsNull)
{
    var info = Inventory.GetItemInfo(item);
    DevConsole.Log($"Found: {info.TemplateName}");
}
```

## Console Commands

The following console commands are available:

- `inventory` - List all inventory items for the selected actor
- `weapons` - List equipped weapons for the selected actor
- `armor` - Show equipped armor for the selected actor
- `slot <type>` - List items in a specific slot (0-10 or name like "Weapon1", "Armor", "Grenade")
- `itemvalue` - Get total trade value of the selected actor's inventory
- `hastag <tag>` - Check if the selected actor has an item with a specific tag
