# Perks

`Menace.SDK.Perks` -- Static class for perk and skill management. Provides safe access to perk trees, perk manipulation, and skill inspection.

## Perk Queries

### GetLeaderPerks

```csharp
public static List<PerkInfo> GetLeaderPerks(GameObj leader)
```

Get all perks for a unit leader with detailed info.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** List of `PerkInfo` for all learned perks.

### GetPerkInfo

```csharp
public static PerkInfo GetPerkInfo(GameObj perkTemplate)
```

Get detailed information about a perk template.

**Parameters:**
- `perkTemplate` - The PerkTemplate GameObj

**Returns:** `PerkInfo` with perk details.

### GetPerkTrees

```csharp
public static List<PerkTreeInfo> GetPerkTrees(GameObj leader)
```

Get perk trees available to a unit leader from their template.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** List of `PerkTreeInfo` with all available perk trees.

### GetPerkTreeInfo

```csharp
public static PerkTreeInfo GetPerkTreeInfo(GameObj perkTree)
```

Get information about a perk tree.

**Parameters:**
- `perkTree` - The PerkTreeTemplate GameObj

**Returns:** `PerkTreeInfo` with tree details and all perks.

## Perk Manipulation

### CanBePromoted

```csharp
public static bool CanBePromoted(GameObj leader)
```

Check if a leader can be promoted (has room for more perks).

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** `true` if the leader can learn more perks.

### CanBeDemoted

```csharp
public static bool CanBeDemoted(GameObj leader)
```

Check if a leader can be demoted (has perks to remove).

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** `true` if the leader has perks that can be removed.

### AddPerk

```csharp
public static bool AddPerk(GameObj leader, GameObj perkTemplate, bool spendPromotionPoints = true)
```

Add a perk to a unit leader.

**Parameters:**
- `leader` - The leader to add the perk to
- `perkTemplate` - The perk template to add
- `spendPromotionPoints` - Whether to spend promotion points (default true)

**Returns:** `true` if the perk was successfully added.

### RemoveLastPerk

```csharp
public static bool RemoveLastPerk(GameObj leader)
```

Remove the last perk from a unit leader.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** `true` if a perk was successfully removed.

### HasPerk

```csharp
public static bool HasPerk(GameObj leader, GameObj perkTemplate)
```

Check if a leader has a specific perk.

**Parameters:**
- `leader` - The unit leader GameObj
- `perkTemplate` - The perk template to check for

**Returns:** `true` if the leader has the perk.

### GetLastPerk

```csharp
public static GameObj GetLastPerk(GameObj leader)
```

Get the last perk added to a leader.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** `GameObj` for the last perk, or `GameObj.Null` if none.

## Perk Finding

### FindPerkByName

```csharp
public static GameObj FindPerkByName(GameObj leader, string perkName)
```

Find a perk template by name from all perk trees of a leader.

**Parameters:**
- `leader` - The unit leader to search perk trees for
- `perkName` - The perk name to search for (partial match, case-insensitive)

**Returns:** `GameObj` for the matching perk template, or `GameObj.Null`.

### GetAvailablePerks

```csharp
public static List<PerkInfo> GetAvailablePerks(GameObj leader)
```

Get available perks (not yet learned) for a leader.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** List of `PerkInfo` for perks the leader can still learn.

## Types

### PerkInfo

```csharp
public class PerkInfo
{
    public string Name { get; set; }            // Template asset name
    public string Title { get; set; }           // Localized display title
    public string Description { get; set; }     // Localized description
    public int Tier { get; set; }               // Perk tier (1-4)
    public int ActionPointCost { get; set; }    // AP cost to use (if active)
    public bool IsActive { get; set; }          // Is this an active ability
    public IntPtr Pointer { get; set; }         // Native pointer
}
```

### PerkTreeInfo

```csharp
public class PerkTreeInfo
{
    public string Name { get; set; }            // Tree asset name
    public int PerkCount { get; set; }          // Total perks in tree
    public List<PerkInfo> Perks { get; set; }   // All perks with tier info
    public IntPtr Pointer { get; set; }         // Native pointer
}
```

## Examples

### Listing a unit's learned perks

```csharp
var leader = Roster.FindByNickname("Phoenix");
if (!leader.IsNull)
{
    var perks = Perks.GetLeaderPerks(leader);
    DevConsole.Log($"Perks ({perks.Count}):");
    foreach (var p in perks)
    {
        var active = p.IsActive ? " [Active]" : "";
        DevConsole.Log($"  {p.Title}{active}");
    }
}
```

### Checking available perks

```csharp
var leader = Roster.FindByNickname("Razor");
if (!leader.IsNull)
{
    var available = Perks.GetAvailablePerks(leader);
    var canPromote = Perks.CanBePromoted(leader);

    DevConsole.Log($"Can promote: {canPromote}");
    DevConsole.Log($"Available perks: {available.Count}");

    foreach (var p in available)
    {
        DevConsole.Log($"  T{p.Tier}: {p.Title}");
    }
}
```

### Adding a perk without cost

```csharp
var leader = Roster.FindByNickname("Ghost");
var perk = Perks.FindPerkByName(leader, "Suppression");

if (!perk.IsNull)
{
    // Add without spending promotion points
    if (Perks.AddPerk(leader, perk, spendPromotionPoints: false))
    {
        var info = Perks.GetPerkInfo(perk);
        DevConsole.Log($"Added: {info.Title}");
    }
}
```

### Viewing perk trees

```csharp
var leader = Roster.FindByNickname("Viper");
var trees = Perks.GetPerkTrees(leader);

foreach (var tree in trees)
{
    DevConsole.Log($"Tree: {tree.Name} ({tree.PerkCount} perks)");
    foreach (var perk in tree.Perks)
    {
        DevConsole.Log($"  T{perk.Tier}: {perk.Title}");
    }
}
```

### Removing perks

```csharp
var leader = Roster.FindByNickname("Hawk");

while (Perks.CanBeDemoted(leader))
{
    var lastPerk = Perks.GetLastPerk(leader);
    var info = Perks.GetPerkInfo(lastPerk);

    if (Perks.RemoveLastPerk(leader))
    {
        DevConsole.Log($"Removed: {info?.Title}");
    }
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

| Command | Usage | Description |
|---------|-------|-------------|
| `perks` | `perks <nickname>` | Show a unit's learned perks |
| `perktrees` | `perktrees <nickname>` | Show a unit's perk trees with all available perks |
| `availableperks` | `availableperks <nickname>` | Show perks a unit can still learn |
| `addperk` | `addperk <nickname> <perk>` | Add a perk to a unit (no cost) |
| `removeperk` | `removeperk <nickname>` | Remove the last perk from a unit |

## See Also

- [Roster](roster.md) - Unit leader management
- [Inventory](inventory.md) - Item management
