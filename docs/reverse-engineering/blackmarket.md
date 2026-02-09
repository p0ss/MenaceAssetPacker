# BlackMarket System

## Overview

The BlackMarket system provides a shop where players can purchase equipment between operations. Items are generated based on campaign progress and have limited availability that decreases over time (operations). The system integrates with the strategy layer and uses the ISaveStateProcessor pattern for persistence.

## Architecture

```
BlackMarket (shop manager)
├── List<BlackMarketItemStack> Stacks            // +0x10 (available items)
└── ISaveStateProcessor implementation

BlackMarketItemStack (purchasable item entry)
├── BaseItemTemplate Template                     // +0x10 (item definition)
├── int OperationsRemaining                       // +0x18 (turns until removed)
├── List<BaseItem> Items                          // +0x20 (actual item instances)
└── BlackMarketStackType Type                     // +0x28 (stack category)

StrategyConfig (configuration)
├── List<BaseItemTemplate> BlackMarketItems      // +0x198 (possible items)
├── int BlackMarketMinItems                       // +0x1A0 (min items to generate)
├── int BlackMarketMaxItems                       // +0x1A4 (max items to generate)
└── int BlackMarketItemTimeout                    // +0x1A8 (operations until removal)
```

## BlackMarket Class

### Field Layout

```c
public class BlackMarket : ISaveStateProcessor {
    // Object header                              // +0x00 - 0x0F
    List<BlackMarketItemStack> Stacks;            // +0x10 (current inventory)
}
```

### Key Methods

```c
// Constructor
void BlackMarket.ctor();  // @ address varies

// Item management
void FillUp(StrategyConfig config, int progress);  // Generates new items
void OnOperationFinished();                        // Decrements timeouts, refills

// Save/Load
void ProcessSaveState(SaveState state);            // ISaveStateProcessor
```

### Constructor Flow

```c
// BlackMarket.ctor
void BlackMarket.ctor() {
    this.Stacks = new List<BlackMarketItemStack>();  // +0x10
}
```

### FillUp Flow

```c
// BlackMarket.FillUp
void FillUp(StrategyConfig config, int progress) {
    // Get item pool from config
    List<BaseItemTemplate> itemPool = config.BlackMarketItems;  // +0x198

    if (itemPool == null || itemPool.Count == 0) {
        return;
    }

    // Determine how many items to generate
    int minItems = config.BlackMarketMinItems;  // +0x1A0
    int maxItems = config.BlackMarketMaxItems;  // +0x1A4
    int itemCount = Random.Range(minItems, maxItems + 1);

    // Get timeout duration
    int timeout = config.BlackMarketItemTimeout;  // +0x1A8

    // Generate items
    for (int i = 0; i < itemCount; i++) {
        // Filter items by progress/rarity
        List<BaseItemTemplate> validItems = FilterByProgress(itemPool, progress);

        if (validItems.Count == 0) continue;

        // Random selection
        int index = Random.Range(0, validItems.Count);
        BaseItemTemplate template = validItems[index];

        // Check if already in stock (avoid duplicates)
        if (HasTemplate(template)) continue;

        // Create stack
        BlackMarketItemStack stack = new BlackMarketItemStack(
            template,
            timeout,
            BlackMarketStackType.Generated
        );

        this.Stacks.Add(stack);  // +0x10
    }
}

List<BaseItemTemplate> FilterByProgress(List<BaseItemTemplate> pool, int progress) {
    List<BaseItemTemplate> result = new List<BaseItemTemplate>();

    foreach (BaseItemTemplate template in pool) {
        // Check if item is available at current progress
        // Uses template's MinProgress/MaxProgress fields
        if (template.IsAvailableAtProgress(progress)) {
            result.Add(template);
        }
    }

    return result;
}

bool HasTemplate(BaseItemTemplate template) {
    foreach (BlackMarketItemStack stack in this.Stacks) {  // +0x10
        if (stack.Template == template) {  // +0x10
            return true;
        }
    }
    return false;
}
```

### OnOperationFinished Flow

```c
// BlackMarket.OnOperationFinished
void OnOperationFinished() {
    List<BlackMarketItemStack> stacks = this.Stacks;  // +0x10

    // Decrement timeouts and remove expired stacks
    for (int i = stacks.Count - 1; i >= 0; i--) {
        BlackMarketItemStack stack = stacks[i];

        // Decrement operations remaining
        stack.OperationsRemaining--;  // +0x18

        // Remove if expired
        if (stack.OperationsRemaining <= 0) {
            stacks.RemoveAt(i);
        }
    }

    // Refill with new items
    StrategyState strategy = StrategyState.Instance;
    StrategyConfig config = strategy.Config;
    int progress = strategy.GetCampaignProgress();

    this.FillUp(config, progress);
}
```

### ProcessSaveState Flow

```c
// BlackMarket.ProcessSaveState
void ProcessSaveState(SaveState state) {
    // Save/load the stacks list
    state.ProcessObjects(ref this.Stacks);  // +0x10

    // Each BlackMarketItemStack handles its own serialization
}
```

## BlackMarketItemStack Class

### Field Layout

```c
public class BlackMarketItemStack : ISaveStateProcessor {
    // Object header                              // +0x00 - 0x0F
    BaseItemTemplate Template;                    // +0x10 (item type)
    int OperationsRemaining;                      // +0x18 (turns until removal)
    List<BaseItem> Items;                         // +0x20 (purchasable instances)
    BlackMarketStackType Type;                    // +0x28 (category)
}
```

### Key Methods

```c
// Constructor
void BlackMarketItemStack.ctor(BaseItemTemplate template, int timeout,
                                BlackMarketStackType type);

// Item access
BaseItem GetItem();                               // Get first available item
void RemoveItem(BaseItem item);                   // Remove purchased item

// Save/Load
void ProcessSaveState(SaveState state);           // ISaveStateProcessor
```

### Constructor Flow

```c
// BlackMarketItemStack.ctor
void BlackMarketItemStack.ctor(BaseItemTemplate template, int timeout,
                                BlackMarketStackType type) {
    this.Template = template;              // +0x10
    this.OperationsRemaining = timeout;    // +0x18
    this.Items = new List<BaseItem>();     // +0x20
    this.Type = type;                      // +0x28

    // Create initial item instance
    BaseItem item = template.CreateItem();
    this.Items.Add(item);
}
```

### ProcessSaveState Flow

```c
// BlackMarketItemStack.ProcessSaveState
void ProcessSaveState(SaveState state) {
    // Save/load template reference
    state.ProcessTemplate(ref this.Template);       // +0x10

    // Save/load timeout
    state.Process(ref this.OperationsRemaining);    // +0x18

    // Save/load item instances
    state.ProcessObjects(ref this.Items);           // +0x20

    // Save/load stack type
    state.Process(ref this.Type);                   // +0x28
}
```

## BlackMarketStackType Enum

```c
public enum BlackMarketStackType {
    Generated = 0,    // Normal shop item
    Reward = 1,       // Mission/event reward
    Unique = 2,       // Special one-time item
    Permanent = 3     // Never expires
}
```

## StrategyConfig Fields

```c
// BlackMarket configuration in StrategyConfig
public class StrategyConfig {
    // ... other fields ...
    List<BaseItemTemplate> BlackMarketItems;      // +0x198 (item pool)
    int BlackMarketMinItems;                       // +0x1A0 (min generation)
    int BlackMarketMaxItems;                       // +0x1A4 (max generation)
    int BlackMarketItemTimeout;                    // +0x1A8 (operations duration)
}
```

## Integration Points

### StrategyState Integration

```c
// StrategyState holds BlackMarket reference
public class StrategyState {
    BlackMarket BlackMarket;                      // offset varies

    // Called when operation ends
    void OnOperationFinished() {
        // ... other end-of-operation logic ...
        this.BlackMarket.OnOperationFinished();
    }
}
```

### Save System Integration

The BlackMarket is saved as part of StrategyState serialization:

```
StrategyState.ProcessSaveState order:
├── ShipUpgrades
├── OwnedItems
├── BlackMarket          ← BlackMarket saved here
├── StoryFactions
├── Squaddies
├── Roster
├── BattlePlan
├── PlanetManager
└── OperationsManager
```

## Modding Hooks

### Modify Item Pool

```csharp
[HarmonyPatch(typeof(BlackMarket), "FillUp")]
class ItemPoolPatch {
    static void Prefix(ref StrategyConfig config) {
        // Add custom items to the pool
        var customItem = GetCustomItemTemplate();
        config.BlackMarketItems.Add(customItem);
    }
}
```

### Modify Item Timeout

```csharp
[HarmonyPatch(typeof(BlackMarketItemStack), MethodType.Constructor)]
class TimeoutPatch {
    static void Postfix(BlackMarketItemStack __instance, int timeout) {
        // Double all timeouts
        __instance.OperationsRemaining = timeout * 2;
    }
}
```

### Prevent Item Expiration

```csharp
[HarmonyPatch(typeof(BlackMarket), "OnOperationFinished")]
class NoExpirationPatch {
    static bool Prefix(BlackMarket __instance) {
        // Skip timeout decrement, only refill
        var config = StrategyState.Instance.Config;
        var progress = StrategyState.Instance.GetCampaignProgress();
        __instance.FillUp(config, progress);
        return false;  // Skip original
    }
}
```

### Add Guaranteed Items

```csharp
[HarmonyPatch(typeof(BlackMarket), "FillUp")]
class GuaranteedItemPatch {
    static void Postfix(BlackMarket __instance, StrategyConfig config) {
        // Ensure specific item is always available
        var requiredTemplate = GetRequiredTemplate();

        bool hasRequired = false;
        foreach (var stack in __instance.Stacks) {
            if (stack.Template == requiredTemplate) {
                hasRequired = true;
                break;
            }
        }

        if (!hasRequired) {
            var stack = new BlackMarketItemStack(
                requiredTemplate,
                config.BlackMarketItemTimeout,
                BlackMarketStackType.Permanent
            );
            __instance.Stacks.Add(stack);
        }
    }
}
```

### Track Purchases

```csharp
[HarmonyPatch(typeof(BlackMarketItemStack), "RemoveItem")]
class PurchaseTrackerPatch {
    static void Postfix(BlackMarketItemStack __instance, BaseItem item) {
        Logger.Msg($"Purchased: {__instance.Template.name} for {item.GetValue()} credits");
    }
}
```

## Key Constants

```c
// Default configuration values (from StrategyConfig)
const int DEFAULT_MIN_ITEMS = 3;
const int DEFAULT_MAX_ITEMS = 6;
const int DEFAULT_TIMEOUT = 3;  // Operations

// BlackMarket field offsets
const int OFFSET_STACKS = 0x10;

// BlackMarketItemStack field offsets
const int OFFSET_TEMPLATE = 0x10;
const int OFFSET_OPERATIONS_REMAINING = 0x18;
const int OFFSET_ITEMS = 0x20;
const int OFFSET_TYPE = 0x28;

// StrategyConfig BlackMarket offsets
const int OFFSET_BM_ITEMS = 0x198;
const int OFFSET_BM_MIN = 0x1A0;
const int OFFSET_BM_MAX = 0x1A4;
const int OFFSET_BM_TIMEOUT = 0x1A8;
```

## Related Classes

- **StrategyState**: Holds BlackMarket reference, triggers OnOperationFinished
- **StrategyConfig**: Provides item pool and generation parameters
- **BaseItemTemplate**: Item definitions available for purchase
- **BaseItem**: Actual purchasable item instances
- **SaveState**: Handles serialization/deserialization
- **UIBlackMarket**: UI for displaying and purchasing items
