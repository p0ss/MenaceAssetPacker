# Operation

`Menace.SDK.Operation` -- Static class for campaign operation management. Provides safe access to operations, missions, factions, and strategic assets.

## Methods

### GetCurrentOperation

```csharp
public static GameObj GetCurrentOperation()
```

Get the current active operation.

**Returns:** `GameObj` representing the current operation, or `GameObj.Null` if no operation is active.

### GetOperationInfo

```csharp
public static OperationInfo GetOperationInfo()
public static OperationInfo GetOperationInfo(GameObj operation)
```

Get information about the current or specified operation.

**Parameters:**
- `operation` - (Optional) The operation to query. If not provided, uses the current operation.

**Returns:** `OperationInfo` with operation details, or `null` if no operation is active.

### GetCurrentMission

```csharp
public static GameObj GetCurrentMission()
```

Get the current mission from the active operation.

**Returns:** `GameObj` representing the current mission, or `GameObj.Null` if none.

### GetMissions

```csharp
public static List<GameObj> GetMissions()
```

Get all missions in the current operation.

**Returns:** List of `GameObj` representing each mission in the operation.

### HasActiveOperation

```csharp
public static bool HasActiveOperation()
```

Check if there is an active operation.

**Returns:** `true` if an operation is currently active, `false` otherwise.

### GetRemainingTime

```csharp
public static int GetRemainingTime()
```

Get remaining time in the current operation.

**Returns:** Time remaining, or `0` if no operation is active or operation has no time limit.

### CanTimeOut

```csharp
public static bool CanTimeOut()
```

Check if the current operation can time out.

**Returns:** `true` if the operation has a time limit, `false` otherwise.

## Types

### OperationInfo

```csharp
public class OperationInfo
{
    public string TemplateName { get; set; }      // Name of the operation template
    public string EnemyFaction { get; set; }      // Name of the enemy faction
    public string FriendlyFaction { get; set; }   // Name of the allied faction
    public string Planet { get; set; }            // Planet where operation takes place
    public int CurrentMissionIndex { get; set; }  // Index of the current mission (0-based)
    public int MissionCount { get; set; }         // Total number of missions
    public int TimeSpent { get; set; }            // Time already spent on operation
    public int TimeLimit { get; set; }            // Maximum time allowed (0 = unlimited)
    public int TimeRemaining { get; set; }        // Time remaining until timeout
    public bool HasCompletedOnce { get; set; }    // Whether operation was completed before
    public IntPtr Pointer { get; set; }           // Native pointer to operation object
}
```

## Examples

### Checking for active operation

```csharp
if (Operation.HasActiveOperation())
{
    DevConsole.Log("Operation is in progress");
}
else
{
    DevConsole.Log("No active operation");
}
```

### Getting operation details

```csharp
var info = Operation.GetOperationInfo();
if (info != null)
{
    DevConsole.Log($"Operation: {info.TemplateName}");
    DevConsole.Log($"Planet: {info.Planet}");
    DevConsole.Log($"Enemy: {info.EnemyFaction}");
    DevConsole.Log($"Allied: {info.FriendlyFaction}");
    DevConsole.Log($"Mission: {info.CurrentMissionIndex + 1}/{info.MissionCount}");
}
```

### Checking operation time

```csharp
var info = Operation.GetOperationInfo();
if (info != null)
{
    if (info.TimeLimit > 0)
    {
        DevConsole.Log($"Time: {info.TimeSpent}/{info.TimeLimit}");
        DevConsole.Log($"Remaining: {info.TimeRemaining}");

        if (info.TimeRemaining < 10)
            DevConsole.Log("Warning: Operation running out of time!");
    }
    else
    {
        DevConsole.Log("Operation has no time limit");
    }
}
```

### Iterating through missions

```csharp
var missions = Operation.GetMissions();
var info = Operation.GetOperationInfo();

DevConsole.Log($"Operation has {missions.Count} missions:");
for (int i = 0; i < missions.Count; i++)
{
    var missionInfo = Mission.GetMissionInfo(missions[i]);
    var current = i == info?.CurrentMissionIndex ? " <-- CURRENT" : "";
    DevConsole.Log($"  {i + 1}. {missionInfo?.TemplateName ?? "Unknown"}{current}");
}
```

### Working with the current mission

```csharp
var mission = Operation.GetCurrentMission();
if (!mission.IsNull)
{
    var missionInfo = Mission.GetMissionInfo(mission);
    DevConsole.Log($"Current Mission: {missionInfo?.TemplateName}");
    DevConsole.Log($"Status: {missionInfo?.StatusName}");
}
```

### Time-based logic

```csharp
// Check if operation can fail due to time
if (Operation.CanTimeOut())
{
    int remaining = Operation.GetRemainingTime();

    if (remaining <= 0)
        DevConsole.Log("Operation has timed out!");
    else if (remaining < 5)
        DevConsole.Log($"Critical: Only {remaining} time units left!");
    else if (remaining < 15)
        DevConsole.Log($"Warning: {remaining} time units remaining");
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

- `operation` - Show current operation info including template name, planet, factions, mission progress, and time status
- `opmissions` - List all missions in the current operation with their status and indicate the current mission
- `optime` - Show operation time remaining (time spent, time limit, and time remaining)

### Example Console Output

**operation command:**
```
Operation: DesertStrike
Planet: Arrakis
Enemy: EnemyFaction
Allied: PlayerFaction
Missions: 2/5
Time: 12/50 (38 remaining)
Completed Before: False
```

**opmissions command:**
```
Operation Missions (5):
  0. InfiltrationMission [Completed]
  1. SabotageObjective [InProgress] <-- CURRENT
  2. ExtractionMission [NotStarted]
  3. DefenseMission [NotStarted]
  4. FinalAssault [NotStarted]
```

**optime command:**
```
Time: 12/50
Remaining: 38
```
