# Mission

`Menace.SDK.Mission` -- Static class for mission system operations including mission state, objectives, and mission flow control.

## Constants

### Mission Status

```csharp
public const int STATUS_PENDING = 0;   // Mission not yet started
public const int STATUS_ACTIVE = 1;    // Mission in progress
public const int STATUS_COMPLETE = 2;  // Mission completed successfully
public const int STATUS_FAILED = 3;    // Mission failed
```

### Mission Layers

```csharp
public const int LAYER_SURFACE = 0;      // Surface/outdoor map
public const int LAYER_UNDERGROUND = 1;  // Underground/cave map
public const int LAYER_INTERIOR = 2;     // Interior/building map
public const int LAYER_SPACE = 3;        // Space map
public const int LAYER_RANDOM = 4;       // Randomly selected layer
```

## Methods

### GetCurrentMission

```csharp
public static GameObj GetCurrentMission()
```

Get the current active mission object.

**Returns:** `GameObj` representing the current mission, or `GameObj.Null` if no mission is active.

### GetMissionInfo

```csharp
public static MissionInfo GetMissionInfo()
public static MissionInfo GetMissionInfo(GameObj mission)
```

Get detailed information about the current mission or a specific mission object.

**Returns:** `MissionInfo` with mission details, or `null` if no mission is active.

### GetObjectives

```csharp
public static List<ObjectiveInfo> GetObjectives()
public static List<ObjectiveInfo> GetObjectives(GameObj mission)
```

Get all objectives for the current mission or a specific mission.

**Returns:** List of `ObjectiveInfo` objects describing each objective.

### GetStatus

```csharp
public static int GetStatus()
```

Get the current mission status code.

**Returns:** One of the `STATUS_*` constants.

### IsActive / IsComplete / IsFailed

```csharp
public static bool IsActive()
public static bool IsComplete()
public static bool IsFailed()
```

Check if the mission is in a specific state.

### CompleteObjective

```csharp
public static bool CompleteObjective(int index)
```

Complete an objective by its index.

**Parameters:**
- `index` - Zero-based index of the objective to complete

**Returns:** `true` if the objective was completed, `false` otherwise.

### GetStatusName

```csharp
public static string GetStatusName(int status)
```

Get a human-readable name for a status code.

**Parameters:**
- `status` - One of the `STATUS_*` constants

**Returns:** String like "Pending", "Active", "Complete", or "Failed".

### GetLayerName

```csharp
public static string GetLayerName(int layer)
```

Get a human-readable name for a layer code.

**Parameters:**
- `layer` - One of the `LAYER_*` constants

**Returns:** String like "Surface", "Underground", "Interior", "Space", or "Random".

## Types

### MissionInfo

```csharp
public class MissionInfo
{
    public string TemplateName { get; set; }    // Mission template name
    public int Status { get; set; }             // Status code (STATUS_*)
    public string StatusName { get; set; }      // Human-readable status
    public int Layer { get; set; }              // Layer code (LAYER_*)
    public string LayerName { get; set; }       // Human-readable layer
    public int MapWidth { get; set; }           // Map width in tiles
    public int Seed { get; set; }               // Map generation seed
    public string BiomeName { get; set; }       // Biome name (e.g., "Desert", "Forest")
    public string WeatherName { get; set; }     // Weather condition name
    public string LightCondition { get; set; }  // Lighting condition (e.g., "Day", "Night")
    public string DifficultyName { get; set; }  // Difficulty setting name
    public int EnemyArmyPoints { get; set; }    // Enemy army point budget
    public IntPtr Pointer { get; set; }         // Native pointer to mission object
}
```

### ObjectiveInfo

```csharp
public class ObjectiveInfo
{
    public string Name { get; set; }           // Objective title
    public string Description { get; set; }    // Detailed description
    public bool IsComplete { get; set; }       // Whether objective is complete
    public bool IsFailed { get; set; }         // Whether objective has failed
    public bool IsOptional { get; set; }       // Whether objective is optional
    public int Progress { get; set; }          // Current progress count
    public int TargetProgress { get; set; }    // Target progress to complete
    public IntPtr Pointer { get; set; }        // Native pointer to objective object
}
```

## Examples

### Getting mission information

```csharp
var info = Mission.GetMissionInfo();
if (info != null)
{
    DevConsole.Log($"Mission: {info.TemplateName}");
    DevConsole.Log($"Status: {info.StatusName}");
    DevConsole.Log($"Layer: {info.LayerName}");
    DevConsole.Log($"Biome: {info.BiomeName}, Weather: {info.WeatherName}");
    DevConsole.Log($"Enemy Army Points: {info.EnemyArmyPoints}");
}
```

### Checking mission status

```csharp
if (Mission.IsActive())
{
    DevConsole.Log("Mission is in progress");
}
else if (Mission.IsComplete())
{
    DevConsole.Log("Mission completed!");
}
else if (Mission.IsFailed())
{
    DevConsole.Log("Mission failed!");
}
```

### Working with objectives

```csharp
var objectives = Mission.GetObjectives();
foreach (var obj in objectives)
{
    var status = obj.IsComplete ? "DONE" : obj.IsFailed ? "FAIL" : "TODO";
    var optional = obj.IsOptional ? " (optional)" : "";

    if (obj.TargetProgress > 0)
    {
        DevConsole.Log($"[{status}] {obj.Name}{optional} - {obj.Progress}/{obj.TargetProgress}");
    }
    else
    {
        DevConsole.Log($"[{status}] {obj.Name}{optional}");
    }
}
```

### Completing objectives programmatically

```csharp
// Complete the first objective
if (Mission.CompleteObjective(0))
{
    DevConsole.Log("First objective completed!");
}

// Complete all incomplete objectives
var objectives = Mission.GetObjectives();
for (int i = 0; i < objectives.Count; i++)
{
    if (!objectives[i].IsComplete && !objectives[i].IsFailed)
    {
        Mission.CompleteObjective(i);
    }
}
```

### Tracking mission progress

```csharp
var objectives = Mission.GetObjectives();
int complete = objectives.FindAll(o => o.IsComplete).Count;
int failed = objectives.FindAll(o => o.IsFailed).Count;
int remaining = objectives.Count - complete - failed;

DevConsole.Log($"Progress: {complete} complete, {failed} failed, {remaining} remaining");
```

### Responding to mission layer

```csharp
var info = Mission.GetMissionInfo();
if (info != null)
{
    switch (info.Layer)
    {
        case Mission.LAYER_SURFACE:
            DevConsole.Log("Surface mission - watch for air support");
            break;
        case Mission.LAYER_UNDERGROUND:
            DevConsole.Log("Underground mission - limited visibility");
            break;
        case Mission.LAYER_INTERIOR:
            DevConsole.Log("Interior mission - close quarters combat");
            break;
        case Mission.LAYER_SPACE:
            DevConsole.Log("Space mission - zero gravity");
            break;
    }
}
```

## Console Commands

The following console commands are registered by `Mission.RegisterConsoleCommands()`:

- `mission` - Show current mission info (template, status, layer, biome, weather, difficulty, enemy points)
- `objectives` - List all mission objectives with their status, progress, and optional flag
- `completeobjective <index>` - Complete an objective by its index
- `missionstatus` - Show mission status summary with objective counts
