# TacticalController

`Menace.SDK.TacticalController` -- Static class for controlling tactical game state including rounds, turns, time scale, and mission flow.

## Constants

### Faction Constants

```csharp
public const int FACTION_PLAYER = 0;
public const int FACTION_ENEMY = 1;
public const int FACTION_NEUTRAL = 2;
```

## Methods

### Round Management

#### GetCurrentRound

```csharp
public static int GetCurrentRound()
```

Get the current round number (1-indexed).

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

Get the currently active faction index.

#### IsPlayerTurn

```csharp
public static bool IsPlayerTurn()
```

Check if it's the player's turn.

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

Skip the AI turn (immediately end enemy turn).

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

Get or set the currently active (selected) actor.

### Unit Counts

#### GetActorCount

```csharp
public static int GetActorCount(int factionIndex)
```

Get count of actors for a faction.

#### GetDeadCount

```csharp
public static int GetDeadCount(int factionIndex)
```

Get count of dead actors for a faction.

#### IsAnyPlayerUnitAlive / IsAnyEnemyAlive

```csharp
public static bool IsAnyPlayerUnitAlive()
public static bool IsAnyEnemyAlive()
```

Check if any player or enemy units are still alive.

### Mission State

#### IsMissionRunning

```csharp
public static bool IsMissionRunning()
```

Check if the mission is still running.

#### FinishMission

```csharp
public static bool FinishMission()
```

Finish the mission (trigger victory/defeat screen).

### Wave Control

#### ClearAllEnemies

```csharp
public static int ClearAllEnemies()
```

Clear all enemies from the battlefield. Returns number cleared.

#### SpawnWave

```csharp
public static int SpawnWave(string templateName, List<(int x, int y)> positions)
```

Spawn a wave of enemies at specified positions. Returns number successfully spawned.

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
    public string CurrentFactionName { get; set; }
    public bool IsPlayerTurn { get; set; }
    public bool IsPaused { get; set; }
    public float TimeScale { get; set; }
    public bool IsMissionRunning { get; set; }
    public string ActiveActorName { get; set; }
    public int PlayerAliveCount { get; set; }
    public int PlayerDeadCount { get; set; }
    public int EnemyAliveCount { get; set; }
    public int EnemyDeadCount { get; set; }
}
```

## Examples

### Checking tactical state

```csharp
var state = TacticalController.GetTacticalState();
DevConsole.Log($"Round {state.RoundNumber}, {state.CurrentFactionName}'s turn");
DevConsole.Log($"Players: {state.PlayerAliveCount} alive, {state.PlayerDeadCount} dead");
DevConsole.Log($"Enemies: {state.EnemyAliveCount} alive, {state.EnemyDeadCount} dead");
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

var spawned = TacticalController.SpawnWave("Grunt", positions);
DevConsole.Log($"Spawned wave of {spawned} enemies");
```

### Victory condition check

```csharp
if (!TacticalController.IsAnyEnemyAlive())
{
    DevConsole.Log("All enemies defeated!");
    TacticalController.FinishMission();
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
