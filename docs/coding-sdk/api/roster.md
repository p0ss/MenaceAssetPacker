# Roster

`Menace.SDK.Roster` -- Static class for roster and unit management. Provides safe access to hired units, squaddies, perks, and unit status.

## Constants

### Leader Status Codes

```csharp
public const int STATUS_HIRED = 0;
public const int STATUS_AVAILABLE = 1;
public const int STATUS_DEAD = 2;
public const int STATUS_DISMISSED = 3;
public const int STATUS_AWAITING_BURIAL = 4;
```

## Methods

### GetRoster

```csharp
public static GameObj GetRoster()
```

Get the current roster instance from the StrategyState.

**Returns:** `GameObj` representing the roster, or `GameObj.Null` if unavailable.

### GetHiredLeaders

```csharp
public static List<UnitLeaderInfo> GetHiredLeaders()
```

Get all hired unit leaders in the roster.

**Returns:** List of `UnitLeaderInfo` for all hired leaders.

### GetLeaderInfo

```csharp
public static UnitLeaderInfo GetLeaderInfo(GameObj leader)
```

Get detailed information about a specific unit leader.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** `UnitLeaderInfo` with the leader's details, or `null` if invalid.

### GetHiredCount

```csharp
public static int GetHiredCount()
```

Get the total number of hired units.

**Returns:** Count of hired units.

### GetAvailableCount

```csharp
public static int GetAvailableCount()
```

Get the count of units that are currently deployable.

**Returns:** Count of available (deployable) units.

### FindByNickname

```csharp
public static GameObj FindByNickname(string nickname)
```

Find a unit leader by their nickname.

**Parameters:**
- `nickname` - The nickname to search for (case-insensitive, partial match)

**Returns:** `GameObj` for the matching leader, or `GameObj.Null` if not found.

### GetPerks

```csharp
public static List<string> GetPerks(GameObj leader)
```

Get all perks for a unit leader.

**Parameters:**
- `leader` - The unit leader GameObj

**Returns:** List of perk names.

### GetStatusName

```csharp
public static string GetStatusName(int status)
```

Convert a status code to a human-readable name.

**Parameters:**
- `status` - The status code (0-4)

**Returns:** Status name string ("Hired", "Available", "Dead", "Dismissed", "Awaiting Burial").

## Types

### UnitLeaderInfo

```csharp
public class UnitLeaderInfo
{
    public string TemplateName { get; set; }   // Base template name
    public string Nickname { get; set; }       // Unit's nickname
    public int Status { get; set; }            // Status code (see STATUS_* constants)
    public string StatusName { get; set; }     // Human-readable status
    public int Rank { get; set; }              // Numeric rank value
    public string RankName { get; set; }       // Rank template name
    public int PerkCount { get; set; }         // Number of perks
    public float HealthPercent { get; set; }   // Current HP as percentage (0.0-1.0)
    public bool IsDeployable { get; set; }     // Can be deployed to missions
    public bool IsUnavailable { get; set; }    // Currently unavailable
    public int SquaddieCount { get; set; }     // Number of squaddies (for squad leaders)
    public int DeployCost { get; set; }        // Cost to deploy this unit
    public IntPtr Pointer { get; set; }        // Native pointer (advanced use)
}
```

### SquaddieInfo

```csharp
public class SquaddieInfo
{
    public string FirstName { get; set; }      // Squaddie's first name
    public string LastName { get; set; }       // Squaddie's last name
    public string FullName { get; set; }       // Combined full name
    public string Gender { get; set; }         // Gender
    public string HomePlanet { get; set; }     // Home planet name
    public IntPtr Pointer { get; set; }        // Native pointer (advanced use)
}
```

## Examples

### Listing all hired units

```csharp
var leaders = Roster.GetHiredLeaders();
DevConsole.Log($"You have {leaders.Count} hired units:");

foreach (var leader in leaders)
{
    var status = leader.IsDeployable ? "Ready" : "Unavailable";
    DevConsole.Log($"  {leader.Nickname} - {leader.RankName} [{status}]");
}
```

### Finding a specific unit

```csharp
var leader = Roster.FindByNickname("Razor");
if (!leader.IsNull)
{
    var info = Roster.GetLeaderInfo(leader);
    DevConsole.Log($"Found {info.Nickname} with {info.PerkCount} perks");
}
else
{
    DevConsole.Log("Unit not found");
}
```

### Checking deployment availability

```csharp
var total = Roster.GetHiredCount();
var available = Roster.GetAvailableCount();
DevConsole.Log($"{available} of {total} units ready for deployment");

// List only deployable units
var leaders = Roster.GetHiredLeaders();
foreach (var leader in leaders)
{
    if (leader.IsDeployable)
    {
        DevConsole.Log($"  {leader.Nickname} (Cost: {leader.DeployCost})");
    }
}
```

### Getting unit perks

```csharp
var leader = Roster.FindByNickname("Phoenix");
if (!leader.IsNull)
{
    var perks = Roster.GetPerks(leader);
    var info = Roster.GetLeaderInfo(leader);

    DevConsole.Log($"{info.Nickname}'s perks ({perks.Count}):");
    foreach (var perk in perks)
    {
        DevConsole.Log($"  - {perk}");
    }
}
```

### Checking unit health and status

```csharp
var leaders = Roster.GetHiredLeaders();
foreach (var leader in leaders)
{
    var healthStr = $"{leader.HealthPercent:P0}";
    var squadStr = leader.SquaddieCount > 0 ? $" (+{leader.SquaddieCount} squaddies)" : "";

    DevConsole.Log($"{leader.Nickname}: {healthStr} HP, Rank {leader.Rank}{squadStr}");
}
```

### Working with status codes

```csharp
// Check status using constants
var info = Roster.GetLeaderInfo(someLeader);

if (info.Status == Roster.STATUS_HIRED)
{
    DevConsole.Log($"{info.Nickname} is hired and active");
}
else if (info.Status == Roster.STATUS_DEAD)
{
    DevConsole.Log($"{info.Nickname} has fallen in battle");
}

// Or use the status name directly
DevConsole.Log($"Status: {Roster.GetStatusName(info.Status)}");
```

## Roster Manipulation

### GetHirableLeaders

```csharp
public static List<UnitLeaderTemplateInfo> GetHirableLeaders()
```

Get all unit leader templates available for hire.

**Returns:** List of `UnitLeaderTemplateInfo` for all hirable templates.

### GetTemplateInfo

```csharp
public static UnitLeaderTemplateInfo GetTemplateInfo(GameObj template)
```

Get information about a unit leader template.

**Returns:** `UnitLeaderTemplateInfo` with template details.

### AddHirableLeader

```csharp
public static bool AddHirableLeader(GameObj template)
```

Add a unit leader template to the hirable pool.

**Parameters:**
- `template` - The UnitLeaderTemplate to add

**Returns:** `true` if successfully added.

### HireLeader

```csharp
public static GameObj HireLeader(GameObj template)
```

Hire a unit leader from a template.

**Parameters:**
- `template` - The UnitLeaderTemplate to hire from

**Returns:** `GameObj` for the newly hired leader, or `GameObj.Null` on failure.

### DismissLeader

```csharp
public static bool DismissLeader(GameObj leader)
```

Dismiss a hired unit leader.

**Parameters:**
- `leader` - The leader to dismiss

**Returns:** `true` if successfully dismissed.

### FindHirableByName

```csharp
public static GameObj FindHirableByName(string templateName)
```

Find a hirable leader template by name (partial match, case-insensitive).

### FindByTemplateName

```csharp
public static GameObj FindByTemplateName(string templateName)
```

Find a hired leader by their template name (partial match, case-insensitive).

### GetLeaderTemplate

```csharp
public static GameObj GetLeaderTemplate(GameObj leader)
```

Get the template for a hired leader.

## Types

### UnitLeaderTemplateInfo

```csharp
public class UnitLeaderTemplateInfo
{
    public string TemplateName { get; set; }      // Template asset name
    public string DisplayName { get; set; }       // Localized display name
    public int HiringCost { get; set; }           // Cost to hire
    public int Rarity { get; set; }               // Rarity percentage (0-100)
    public int MinCampaignProgress { get; set; }  // When becomes available
    public IntPtr Pointer { get; set; }           // Native pointer
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

- `roster` - List all hired units with their rank, perk count, status, and squaddie count
- `unit <nickname>` - Show detailed information for a specific unit including template, rank, health, deploy cost, and perks
- `available` - Show count of available units vs total hired units
- `hirable` - List all leaders available for hire
- `hire <template>` - Hire a leader by template name
- `dismiss <nickname>` - Dismiss a hired leader
