# TacticalController

`Menace.SDK.TacticalController` -- Static class for controlling tactical game state including rounds, turns, time scale, and mission flow.

## Enums

### FactionType

```csharp
public enum FactionType
{
    Neutral = 0,
    Player = 1,
    PlayerAI = 2,
    Civilian = 3,
    AlliedLocalForces = 4,
    EnemyLocalForces = 5,
    Pirates = 6,
    Wildlife = 7,
    Constructs = 8,
    RogueArmy = 9
}
```

Faction types matching the game's internal `FactionType` enum.

### TacticalFinishReason

```csharp
public enum TacticalFinishReason
{
    None = 0,
    AllPlayerUnitsDead = 1,
    Leave = 2,
    LoadingSavegame = 3
}
```

Reason for finishing a tactical mission.

## Methods

### Round Management

#### GetCurrentRound

```csharp
public static int GetCurrentRound()
```

Get the current round number (1-indexed). Uses `TacticalManager.GetRound()` internally.

#### NextRound

```csharp
public static bool NextRound()
```

Advance to the next round.

### Faction/Turn Management

#### GetCurrentFaction

```csharp
public static int GetCurrentFaction()
```

Get the currently active faction ID. Uses `TacticalManager.GetActiveFactionID()` internally.

#### GetCurrentFactionType

```csharp
public static FactionType GetCurrentFactionType()
```

Get the current faction as a `FactionType` enum value.

#### GetFactionName

```csharp
public static string GetFactionName(FactionType faction)
```

Get the display name for a faction type.

#### IsPlayerTurn

```csharp
public static bool IsPlayerTurn()
```

Check if it's the player's turn (faction is `FactionType.Player`).

#### NextFaction

```csharp
public static bool NextFaction()
```

Advance to the next faction's turn.

#### EndTurn

```csharp
public static bool EndTurn()
```

End the current turn (for player faction).

#### SkipAITurn

```csharp
public static bool SkipAITurn()
```

Skip the AI turn (immediately end non-player faction turn).

### Pause/Time Control

#### IsPaused / SetPaused / TogglePause

```csharp
public static bool IsPaused()
public static bool SetPaused(bool paused)
public static bool TogglePause()
```

Control game pause state.

#### GetTimeScale / SetTimeScale

```csharp
public static float GetTimeScale()
public static bool SetTimeScale(float scale)
```

Control game speed. Scale of 1.0 is normal speed, 2.0 is 2x speed, etc. Clamped to [0, 10].

### Actor Management

#### GetActiveActor / SetActiveActor

```csharp
public static GameObj GetActiveActor()
public static bool SetActiveActor(GameObj actor)
```

Get or set the currently active (selected) actor. Uses `TacticalManager.GetActiveActor()` internally.

### Unit Counts

#### GetTotalEnemyCount

```csharp
public static int GetTotalEnemyCount()
```

Get total count of enemy actors. Uses `TacticalManager.GetTotalEnemyCount()` internally.

#### GetDeadEnemyCount

```csharp
public static int GetDeadEnemyCount()
```

Get count of dead enemy actors. Uses `TacticalManager.GetDeadEnemyCount()` internally.

#### IsAnyPlayerUnitAlive

```csharp
public static bool IsAnyPlayerUnitAlive()
```

Check if any player units are still alive. Uses `TacticalManager.IsAnyPlayerUnitAlive()` internally.

#### IsAnyEnemyAlive

```csharp
public static bool IsAnyEnemyAlive()
```

Check if any AI/enemy units are still alive. Uses `TacticalManager.IsAnyAIUnitAlive()` internally.

### Mission State

#### IsMissionRunning

```csharp
public static bool IsMissionRunning()
```

Check if the mission is still running.

#### FinishMission

```csharp
public static bool FinishMission(TacticalFinishReason reason = TacticalFinishReason.Leave)
```

Finish the mission with the specified reason. Uses `TacticalManager.Finish(TacticalFinishReason)` internally.

### Wave Control

#### ClearAllEnemies

```csharp
public static int ClearAllEnemies()
```

Clear all enemies from the battlefield. Returns number cleared.

#### SpawnWave

```csharp
public static int SpawnWave(string templateName, List<(int x, int y)> positions, FactionType faction = FactionType.EnemyLocalForces)
```

Spawn a wave of units at specified positions for the given faction. Returns number successfully spawned.

### State Info

#### GetTacticalState

```csharp
public static TacticalStateInfo GetTacticalState()
```

Get comprehensive tactical state info.

## Types

### TacticalStateInfo

```csharp
public class TacticalStateInfo
{
    public int RoundNumber { get; set; }
    public int CurrentFaction { get; set; }
    public FactionType CurrentFactionType { get; set; }
    public string CurrentFactionName { get; set; }
    public bool IsPlayerTurn { get; set; }
    public bool IsPaused { get; set; }
    public float TimeScale { get; set; }
    public bool IsMissionRunning { get; set; }
    public string ActiveActorName { get; set; }
    public bool IsAnyPlayerAlive { get; set; }
    public bool IsAnyEnemyAlive { get; set; }
    public int TotalEnemyCount { get; set; }
    public int DeadEnemyCount { get; set; }
    public int AliveEnemyCount { get; set; }
}
```

## Examples

### Checking tactical state

```csharp
var state = TacticalController.GetTacticalState();
DevConsole.Log($"Round {state.RoundNumber}, {state.CurrentFactionName}'s turn");
DevConsole.Log($"Players alive: {state.IsAnyPlayerAlive}");
DevConsole.Log($"Enemies: {state.AliveEnemyCount} alive, {state.DeadEnemyCount} dead");
```

### Working with factions

```csharp
var faction = TacticalController.GetCurrentFactionType();
DevConsole.Log($"Current faction: {TacticalController.GetFactionName(faction)}");

// Check specific faction types
if (faction == FactionType.Pirates)
{
    DevConsole.Log("It's the pirates' turn!");
}
```

### Controlling game speed

```csharp
// Speed up the game
TacticalController.SetTimeScale(2.0f);

// Pause
TacticalController.SetPaused(true);

// Resume at normal speed
TacticalController.SetPaused(false);
TacticalController.SetTimeScale(1.0f);
```

### Advancing turns

```csharp
// End player turn
if (TacticalController.IsPlayerTurn())
{
    TacticalController.EndTurn();
}

// Skip AI turn entirely
if (!TacticalController.IsPlayerTurn())
{
    TacticalController.SkipAITurn();
}
```

### Spawning a wave of enemies

```csharp
var positions = new List<(int, int)>
{
    (5, 10), (6, 10), (7, 10),
    (5, 11), (6, 11), (7, 11)
};

// Spawn as pirates
var spawned = TacticalController.SpawnWave("enemy.pirate_boarding_commandos", positions, FactionType.Pirates);
DevConsole.Log($"Spawned wave of {spawned} pirates");
```

### Victory condition check

```csharp
if (!TacticalController.IsAnyEnemyAlive())
{
    DevConsole.Log("All enemies defeated!");
    TacticalController.FinishMission(TacticalFinishReason.Leave);
}
```

### Player defeat check

```csharp
if (!TacticalController.IsAnyPlayerUnitAlive())
{
    DevConsole.Log("All player units lost!");
    TacticalController.FinishMission(TacticalFinishReason.AllPlayerUnitsDead);
}
```

## Console Commands

The following console commands are available:

- `round` - Show current round number
- `nextround` - Advance to next round
- `faction` - Show current faction
- `endturn` - End the current turn
- `skipai` - Skip the AI turn
- `pause` - Toggle pause
- `timescale [value]` - Get or set time scale (1.0 = normal)
- `status` - Show tactical state summary
- `clearwave` - Clear all enemies
- `win` - Finish mission (victory)
