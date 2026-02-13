# BlackMarket

`Menace.SDK.BlackMarket` -- Static class for accessing the in-game shop system, querying available items, and managing item stacks.

## Overview

The BlackMarket SDK provides safe access to the game's shop system during strategy layer gameplay. It allows modders to query available items, inspect item stacks, check prices, and monitor item expiration. This is useful for creating automated purchasing systems, shop UI enhancements, or economy-related mods.

**Note:** The BlackMarket is only available during strategy layer gameplay. Methods will return null or empty results during tactical combat.

## Constants

### Default Configuration Values

```csharp
public const int DEFAULT_MIN_ITEMS = 3;   // Minimum items generated during FillUp
public const int DEFAULT_MAX_ITEMS = 6;   // Maximum items generated during FillUp
public const int DEFAULT_TIMEOUT = 3;     // Operations until item removal
```

## Enums

### StackType

```csharp
public enum StackType
{
    Generated = 0,  // Normal shop item generated during FillUp
    Reward = 1,     // Item added as a mission or event reward
    Unique = 2,     // Special one-time unique item
    Permanent = 3   // Item that never expires (OperationsRemaining ignored)
}
```

## Methods

### GetBlackMarket

```csharp
public static GameObj GetBlackMarket()
```

Get the BlackMarket instance from StrategyState.

**Returns:** `GameObj` representing the BlackMarket, or `GameObj.Null` if unavailable.

### GetBlackMarketInfo

```csharp
public static BlackMarketInfo GetBlackMarketInfo()
```

Get detailed information about the BlackMarket state including configuration and statistics.

**Returns:** `BlackMarketInfo` containing shop state, or null if unavailable.

### GetAvailableStacks

```csharp
public static List<ItemStackInfo> GetAvailableStacks()
```

Get all available item stacks currently in the BlackMarket.

**Returns:** List of `ItemStackInfo` for each available stack.

### GetStackInfo

```csharp
public static ItemStackInfo GetStackInfo(int index)
```

Get information about a specific item stack by index.

**Parameters:**
- `index` - Index of the stack in the Stacks list

**Returns:** `ItemStackInfo` for the stack, or null if invalid index.

### GetStackAt

```csharp
public static GameObj GetStackAt(int index)
```

Get the GameObj for a stack by index.

**Parameters:**
- `index` - Index of the stack in the Stacks list

**Returns:** `GameObj` representing the stack, or `GameObj.Null` if invalid.

### GetItemsInStack

```csharp
public static List<ItemInfo> GetItemsInStack(GameObj stack)
```

Get all individual items within a specific stack.

**Parameters:**
- `stack` - The stack GameObj to inspect

**Returns:** List of `ItemInfo` for items in the stack.

### FindStackByTemplate

```csharp
public static ItemStackInfo FindStackByTemplate(string templateName)
```

Find a stack by its item template name.

**Parameters:**
- `templateName` - Name of the item template to find

**Returns:** `ItemStackInfo` for the matching stack, or null if not found.

### HasTemplate

```csharp
public static bool HasTemplate(string templateName)
```

Check if the BlackMarket contains a specific item template.

**Parameters:**
- `templateName` - Name of the item template to check

**Returns:** True if the template is available for purchase.

### GetStackCount

```csharp
public static int GetStackCount()
```

Get the total number of available stacks.

**Returns:** Number of stacks in the BlackMarket.

### GetExpiringStacks

```csharp
public static List<ItemStackInfo> GetExpiringStacks()
```

Get stacks that are expiring soon (1 operation remaining).

**Returns:** List of `ItemStackInfo` for stacks about to expire.

### GetPermanentStacks

```csharp
public static List<ItemStackInfo> GetPermanentStacks()
```

Get permanent stacks that never expire.

**Returns:** List of `ItemStackInfo` for permanent stacks.

### GetStacksByType

```csharp
public static List<ItemStackInfo> GetStacksByType(StackType type)
```

Get stacks of a specific type.

**Parameters:**
- `type` - Stack type to filter by

**Returns:** List of `ItemStackInfo` matching the type.

### GetTotalTradeValue

```csharp
public static int GetTotalTradeValue()
```

Get the total trade value of all items in the BlackMarket.

**Returns:** Sum of trade values for all available items.

### GetStackTypeName

```csharp
public static string GetStackTypeName(StackType type)
public static string GetStackTypeName(int typeValue)
```

Get a human-readable name for a stack type.

**Parameters:**
- `type` / `typeValue` - StackType enum or integer value

**Returns:** Human-readable name ("Generated", "Reward", "Unique", or "Permanent").

## Types

### BlackMarketInfo

```csharp
public class BlackMarketInfo
{
    public int StackCount { get; set; }        // Number of item stacks available
    public int TotalItemCount { get; set; }    // Total items across all stacks
    public int MinItems { get; set; }          // Min items generated per FillUp
    public int MaxItems { get; set; }          // Max items generated per FillUp
    public int ItemTimeout { get; set; }       // Operations until item removal
    public int ItemPoolSize { get; set; }      // Templates available in item pool
    public int CampaignProgress { get; set; }  // Current campaign progress level
    public IntPtr Pointer { get; set; }        // Pointer to BlackMarket instance
}
```

### ItemStackInfo

```csharp
public class ItemStackInfo
{
    public string TemplateName { get; set; }      // Name of the item template
    public int OperationsRemaining { get; set; }  // Operations before removal
    public int ItemCount { get; set; }            // Items available in stack
    public StackType Type { get; set; }           // Category type of stack
    public string TypeName { get; set; }          // Display name for stack type
    public int TradeValue { get; set; }           // Trade value per item
    public string Rarity { get; set; }            // Rarity tier of items
    public bool WillExpire { get; set; }          // True if Type != Permanent
    public IntPtr Pointer { get; set; }           // Pointer to stack instance
}
```

### ItemInfo

```csharp
public class ItemInfo
{
    public string GUID { get; set; }          // Unique identifier for item
    public string TemplateName { get; set; }  // Name of item template
    public int TradeValue { get; set; }       // Trade value of item
    public string Rarity { get; set; }        // Rarity tier of item
    public int SkillCount { get; set; }       // Number of skills on item
    public IntPtr Pointer { get; set; }       // Pointer to BaseItem instance
}
```

## Examples

### Querying BlackMarket status

```csharp
var info = BlackMarket.GetBlackMarketInfo();
if (info != null)
{
    DevConsole.Log($"BlackMarket has {info.StackCount} stacks ({info.TotalItemCount} items)");
    DevConsole.Log($"Config: {info.MinItems}-{info.MaxItems} items, {info.ItemTimeout} ops timeout");
    DevConsole.Log($"Item Pool: {info.ItemPoolSize} templates");
}
else
{
    DevConsole.Log("BlackMarket not available (strategy layer not active?)");
}
```

### Listing all available items

```csharp
var stacks = BlackMarket.GetAvailableStacks();
foreach (var stack in stacks)
{
    var expiry = stack.WillExpire ? $"({stack.OperationsRemaining} ops left)" : "[PERMANENT]";
    DevConsole.Log($"{stack.TemplateName} x{stack.ItemCount} - ${stack.TradeValue} {expiry}");
}
```

### Finding a specific item

```csharp
// Check if a specific item is available
if (BlackMarket.HasTemplate("accessory.ammo_armor_piercing"))
{
    var stack = BlackMarket.FindStackByTemplate("accessory.ammo_armor_piercing");
    DevConsole.Log($"Found {stack.TemplateName} for ${stack.TradeValue}!");
}
```

### Checking expiring items

```csharp
var expiring = BlackMarket.GetExpiringStacks();
if (expiring.Count > 0)
{
    DevConsole.Log("Items expiring after next operation:");
    foreach (var stack in expiring)
    {
        DevConsole.Log($"  {stack.TemplateName} x{stack.ItemCount} - ${stack.TradeValue}");
    }
}
```

### Filtering by stack type

```csharp
// Get all reward items
var rewards = BlackMarket.GetStacksByType(BlackMarket.StackType.Reward);
DevConsole.Log($"Found {rewards.Count} reward items");

// Get all permanent items
var permanent = BlackMarket.GetPermanentStacks();
foreach (var stack in permanent)
{
    DevConsole.Log($"[PERM] {stack.TemplateName} x{stack.ItemCount}");
}
```

### Inspecting individual items in a stack

```csharp
var stackObj = BlackMarket.GetStackAt(0);
if (!stackObj.IsNull)
{
    var items = BlackMarket.GetItemsInStack(stackObj);
    DevConsole.Log($"Stack contains {items.Count} items:");
    foreach (var item in items)
    {
        DevConsole.Log($"  {item.TemplateName} ({item.Rarity}) - ${item.TradeValue}");
        if (item.SkillCount > 0)
            DevConsole.Log($"    Skills: {item.SkillCount}");
    }
}
```

### Calculating total shop value

```csharp
var totalValue = BlackMarket.GetTotalTradeValue();
var stacks = BlackMarket.GetAvailableStacks();
int itemCount = 0;
foreach (var s in stacks)
    itemCount += s.ItemCount;

DevConsole.Log($"Total BlackMarket Value: ${totalValue}");
DevConsole.Log($"Items: {itemCount} across {stacks.Count} stacks");
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

| Command | Arguments | Description |
|---------|-----------|-------------|
| `blackmarket` | | Show BlackMarket overview (stacks, config, pool size) |
| `bmitems` | | List all BlackMarket items with prices and expiry |
| `bmstack` | `<index>` | Show detailed info for a specific stack |
| `bmexpiring` | | List items that will expire after next operation |
| `bmpermanent` | | List all permanent (non-expiring) items |
| `bmfind` | `<name>` | Search for items by name (partial match) |
| `bmvalue` | | Show total trade value of all items |
| `bmbytype` | `<type>` | Filter items by type (Generated/Reward/Unique/Permanent or 0-3) |

### Console Command Examples

```
> blackmarket
BlackMarket Status:
  Stacks: 5 (12 total items)
  Config: 3-6 items, 3 ops timeout
  Item Pool: 45 templates
  Campaign Progress: 2

> bmitems
BlackMarket Items (5 stacks):
  0. accessory.ammo_armor_piercing x2 - $35 (2 ops)
  1. accessory.ammo_bag x3 - $30 (3 ops)
  2. accessory.armor_plates x1 - $50 (1 ops)
  3. accessory.disposable_rocket_launcher x1 - $75 [PERM] [Unique]
  4. accessory.frag_grenade x5 - $15 (2 ops)

> bmstack 2
Stack 2: accessory.armor_plates
  Type: Generated
  Items: 1
  Trade Value: $50 each
  Rarity: Rare
  Expiry: 1 operations remaining

> bmfind ammo
Found 2 matching items:
  accessory.ammo_armor_piercing x2 - $35 (2 ops)
  accessory.ammo_bag x3 - $30 (3 ops)

> bmbytype Reward
No Reward items in BlackMarket
```
