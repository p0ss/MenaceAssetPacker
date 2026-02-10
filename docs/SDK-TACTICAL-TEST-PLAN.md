# SDK Tactical Extensions Test Plan

This document outlines manual and automated tests to verify the new SDK tactical control extensions work correctly in-game.

## Prerequisites

1. Game installed with MelonLoader
2. `Menace.ModpackLoader.dll` deployed to `Mods/`
3. Start a tactical mission (any mission works)
4. Press `~` to open dev console

---

## Phase 1: Human Testing (Console Commands)

### 1.1 Basic Queries

**Test: Verify game state queries work**

```
status
```
Expected: Shows round number, faction, player/enemy counts

```
round
```
Expected: Shows current round number (should be 1+ at mission start)

```
faction
```
Expected: Shows current faction (0=Player or 1=Enemy)

```
actors
```
Expected: Lists all actors with faction, name, ID

```
enemies
```
Expected: Lists enemy actors only

**Pass Criteria:** All commands return sensible data matching what's visible on screen.

---

### 1.2 Actor Selection & Position

**Test: Verify position/facing queries**

1. Select a player unit in game
2. Run:
```
pos
```
Expected: Shows tile coordinates (x, y) and facing direction

```
facing
```
Expected: Shows facing direction 0-7 with name

```
combat
```
Expected: Shows HP, suppression, morale, AP, stun status

```
skills
```
Expected: Lists available skills with AP cost and range

**Pass Criteria:** Values match what's shown in game UI.

---

### 1.3 Movement Commands

**Test: Move command**

1. Select a player unit
2. Note current position with `pos`
3. Find an empty tile nearby (visually)
4. Run:
```
move <targetX> <targetY>
```
Expected: Unit pathfinds and moves to target tile

**Test: Teleport command**

1. Select a player unit
2. Run:
```
teleport <x> <y>
```
Expected: Unit instantly appears at target tile (no animation)

**Test: Facing command**

1. Select a player unit
2. Run:
```
facing N
facing SE
facing 4
```
Expected: Unit's facing direction changes each time

**Pass Criteria:** Movement and teleport actually move the unit. Facing changes visible on unit model.

---

### 1.4 Combat Status Manipulation

**Test: HP manipulation**

1. Select a player unit
2. Check HP: `combat`
3. Apply damage:
```
damage 10
```
4. Verify: `combat` (HP should be lower)
5. Heal:
```
heal 10
```
6. Verify: `combat` (HP should be restored)

**Test: Suppression manipulation**

1. Select a unit
2. Check: `suppression`
3. Set:
```
suppression 50
```
4. Verify: `suppression` (should show ~50%, "Suppressed" state)
5. Set:
```
suppression 80
```
6. Verify: (should show "Pinned" state)
7. Clear:
```
suppression 0
```

**Test: Morale manipulation**

1. Select a unit
2. Check: `morale`
3. Set:
```
morale 0
```
4. Verify: Unit should show panic indicators
5. Restore:
```
morale 100
```

**Test: Stun toggle**

1. Select a unit
2. Run:
```
stun
```
3. Verify: `combat` shows IsStunned: True
4. Run:
```
stun
```
5. Verify: `combat` shows IsStunned: False

**Pass Criteria:** All status changes reflect in game UI (HP bar, suppression indicator, etc.)

---

### 1.5 Action Point Manipulation

**Test: AP modification**

1. Select a player unit
2. Check AP: `ap`
3. Set AP:
```
ap 100
```
4. Verify: `ap` shows 100
5. Try moving further than normal - should work with extra AP

**Pass Criteria:** AP value changes and affects movement range.

---

### 1.6 Entity Spawning

**Test: Spawn enemy**

1. Find empty tile coordinates visually
2. Run:
```
spawn Grunt <x> <y> 1
```
Expected: Enemy "Grunt" appears at tile

**Test: Spawn player unit**

```
spawn Grunt <x> <y> 0
```
Expected: Player-faction unit appears at tile

**Test: Verify spawned entities appear in lists**

```
enemies
actors
```
Expected: Newly spawned units appear in the lists

**Pass Criteria:** Units spawn at correct locations with correct factions.

---

### 1.7 Entity Destruction

**Test: Kill selected actor**

1. Select an enemy (or spawn one)
2. Run:
```
kill
```
Expected: Selected unit dies immediately

**Test: Clear all enemies**

1. Ensure enemies are on the map
2. Run:
```
clearwave
```
Expected: All enemies removed, console shows count

3. Verify:
```
enemies
```
Expected: "No enemies on map"

**Pass Criteria:** Entities are properly removed from game.

---

### 1.8 Turn/Round Control

**Test: End turn**

1. Ensure it's player turn: `faction`
2. Run:
```
endturn
```
Expected: Turn ends, AI begins (or next round if AI has no units)

**Test: Skip AI turn**

1. Wait for or trigger AI turn
2. Run:
```
skipai
```
Expected: AI turn ends immediately, player turn begins

**Test: Advance round**

```
round
nextround
round
```
Expected: Round number increases by 1

**Pass Criteria:** Turn flow works correctly.

---

### 1.9 Time Control

**Test: Pause toggle**

```
pause
```
Expected: Game pauses, units freeze

```
pause
```
Expected: Game resumes

**Test: Time scale**

```
timescale
```
Expected: Shows current scale (1.0)

```
timescale 2
```
Expected: Game runs at 2x speed

```
timescale 0.5
```
Expected: Game runs at half speed

```
timescale 1
```
Expected: Normal speed restored

**Pass Criteria:** Time manipulation affects game speed visibly.

---

### 1.10 Mission Control

**Test: Win mission**

```
win
```
Expected: Mission ends, victory screen appears

**Pass Criteria:** Mission terminates correctly.

---

## Phase 2: Programmatic Testing (AI/Mod Code)

Create a test plugin to verify SDK APIs work from code.

### 2.1 Test Plugin Structure

Create `TestTacticalSDK.cs` in a test modpack:

```csharp
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using System.Collections.Generic;

public class TestTacticalSDK : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private int _testPhase = 0;
    private int _frameCount = 0;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _log.Msg("TestTacticalSDK initialized");

        // Register console command to run tests
        DevConsole.RegisterCommand("runtests", "", "Run SDK tactical tests", args =>
        {
            _testPhase = 1;
            _frameCount = 0;
            return "Starting tactical SDK tests...";
        });
    }

    public void OnUpdate()
    {
        if (_testPhase == 0) return;

        _frameCount++;

        // Wait a few frames between tests
        if (_frameCount % 30 != 0) return;

        switch (_testPhase)
        {
            case 1: TestTacticalState(); break;
            case 2: TestEntityQueries(); break;
            case 3: TestMovement(); break;
            case 4: TestCombat(); break;
            case 5: TestSpawning(); break;
            case 6: TestTurnControl(); break;
            default:
                _log.Msg("=== ALL TESTS COMPLETE ===");
                _testPhase = 0;
                break;
        }

        _testPhase++;
    }

    private void TestTacticalState()
    {
        _log.Msg("=== TEST: TacticalController State ===");

        var state = TacticalController.GetTacticalState();
        _log.Msg($"  Round: {state.RoundNumber}");
        _log.Msg($"  Faction: {state.CurrentFactionName}");
        _log.Msg($"  IsPlayerTurn: {state.IsPlayerTurn}");
        _log.Msg($"  Players: {state.PlayerAliveCount} alive");
        _log.Msg($"  Enemies: {state.EnemyAliveCount} alive");
        _log.Msg($"  IsPaused: {state.IsPaused}");
        _log.Msg($"  TimeScale: {state.TimeScale}");

        bool pass = state.RoundNumber >= 1 &&
                    state.PlayerAliveCount > 0;
        _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
    }

    private void TestEntityQueries()
    {
        _log.Msg("=== TEST: Entity Queries ===");

        var allActors = EntitySpawner.ListEntities();
        _log.Msg($"  Total actors: {allActors.Length}");

        var players = EntitySpawner.ListEntities(factionFilter: 0);
        _log.Msg($"  Player actors: {players.Length}");

        var enemies = EntitySpawner.ListEntities(factionFilter: 1);
        _log.Msg($"  Enemy actors: {enemies.Length}");

        // Test GetEntityInfo on first actor
        if (allActors.Length > 0)
        {
            var info = EntitySpawner.GetEntityInfo(allActors[0]);
            _log.Msg($"  First actor: {info?.Name} (faction {info?.FactionIndex})");
        }

        bool pass = allActors.Length > 0;
        _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
    }

    private void TestMovement()
    {
        _log.Msg("=== TEST: EntityMovement ===");

        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull)
        {
            var players = EntitySpawner.ListEntities(factionFilter: 0);
            if (players.Length > 0) actor = players[0];
        }

        if (actor.IsNull)
        {
            _log.Msg("  No actor to test - SKIP");
            return;
        }

        var pos = EntityMovement.GetPosition(actor);
        _log.Msg($"  Position: {pos?.x}, {pos?.y}");

        var facing = EntityMovement.GetFacing(actor);
        _log.Msg($"  Facing: {facing}");

        var ap = EntityMovement.GetRemainingAP(actor);
        _log.Msg($"  AP: {ap}");

        var isMoving = EntityMovement.IsMoving(actor);
        _log.Msg($"  IsMoving: {isMoving}");

        var info = EntityMovement.GetMovementInfo(actor);
        _log.Msg($"  MovementInfo: Dir={info?.DirectionName}, Mode={info?.MovementMode}");

        // Test facing change
        var newFacing = (facing + 2) % 8;
        var facingResult = EntityMovement.SetFacing(actor, newFacing);
        var verifyFacing = EntityMovement.GetFacing(actor);
        _log.Msg($"  SetFacing({newFacing}): {facingResult}, verify={verifyFacing}");

        bool pass = pos.HasValue && facing >= 0;
        _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
    }

    private void TestCombat()
    {
        _log.Msg("=== TEST: EntityCombat ===");

        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull)
        {
            var players = EntitySpawner.ListEntities(factionFilter: 0);
            if (players.Length > 0) actor = players[0];
        }

        if (actor.IsNull)
        {
            _log.Msg("  No actor to test - SKIP");
            return;
        }

        var combatInfo = EntityCombat.GetCombatInfo(actor);
        _log.Msg($"  HP: {combatInfo?.CurrentHP}/{combatInfo?.MaxHP}");
        _log.Msg($"  Suppression: {combatInfo?.Suppression} ({combatInfo?.SuppressionState})");
        _log.Msg($"  Morale: {combatInfo?.Morale}");
        _log.Msg($"  IsStunned: {combatInfo?.IsStunned}");

        var skills = EntityCombat.GetSkills(actor);
        _log.Msg($"  Skills: {skills.Count}");
        foreach (var skill in skills)
        {
            _log.Msg($"    - {skill.Name} (AP:{skill.APCost}, Range:{skill.Range}, CanUse:{skill.CanUse})");
        }

        // Test suppression manipulation
        var origSuppression = EntityCombat.GetSuppression(actor);
        EntityCombat.SetSuppression(actor, 50f);
        var newSuppression = EntityCombat.GetSuppression(actor);
        EntityCombat.SetSuppression(actor, origSuppression); // Restore
        _log.Msg($"  Suppression test: {origSuppression} -> {newSuppression} -> {origSuppression}");

        bool pass = combatInfo != null && combatInfo.MaxHP > 0;
        _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
    }

    private void TestSpawning()
    {
        _log.Msg("=== TEST: EntitySpawner ===");

        // Count enemies before
        var beforeCount = EntitySpawner.ListEntities(factionFilter: 1).Length;
        _log.Msg($"  Enemies before: {beforeCount}");

        // Try to spawn at a likely-empty tile
        // Note: This may fail if tile is occupied or invalid template name
        var result = EntitySpawner.SpawnUnit("Grunt", 5, 5, factionIndex: 1);
        _log.Msg($"  Spawn result: Success={result.Success}, Error={result.Error}");

        if (result.Success)
        {
            var afterCount = EntitySpawner.ListEntities(factionFilter: 1).Length;
            _log.Msg($"  Enemies after: {afterCount}");

            // Clean up - destroy the spawned unit
            EntitySpawner.DestroyEntity(result.Entity, immediate: true);
            var cleanupCount = EntitySpawner.ListEntities(factionFilter: 1).Length;
            _log.Msg($"  Enemies after cleanup: {cleanupCount}");

            bool pass = afterCount == beforeCount + 1 && cleanupCount == beforeCount;
            _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
        }
        else
        {
            _log.Msg("  Spawn failed - may need valid template name or tile");
            _log.Msg("  RESULT: INCONCLUSIVE");
        }
    }

    private void TestTurnControl()
    {
        _log.Msg("=== TEST: Turn Control ===");

        var round = TacticalController.GetCurrentRound();
        _log.Msg($"  Current round: {round}");

        var faction = TacticalController.GetCurrentFaction();
        _log.Msg($"  Current faction: {faction}");

        var isPlayerTurn = TacticalController.IsPlayerTurn();
        _log.Msg($"  IsPlayerTurn: {isPlayerTurn}");

        var isPaused = TacticalController.IsPaused();
        _log.Msg($"  IsPaused: {isPaused}");

        // Test pause toggle
        TacticalController.SetPaused(true);
        var pausedAfter = TacticalController.IsPaused();
        TacticalController.SetPaused(false);
        var unpausedAfter = TacticalController.IsPaused();
        _log.Msg($"  Pause test: {isPaused} -> {pausedAfter} -> {unpausedAfter}");

        // Test time scale
        var origScale = TacticalController.GetTimeScale();
        TacticalController.SetTimeScale(2.0f);
        var newScale = TacticalController.GetTimeScale();
        TacticalController.SetTimeScale(origScale);
        _log.Msg($"  TimeScale test: {origScale} -> {newScale} -> {origScale}");

        bool pass = round >= 1 && pausedAfter && !unpausedAfter;
        _log.Msg($"  RESULT: {(pass ? "PASS" : "FAIL")}");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
}
```

### 2.2 Running Programmatic Tests

1. Build the test plugin
2. Deploy to a modpack's `dlls/` folder
3. Start a tactical mission
4. Open console (`~`)
5. Run: `runtests`
6. Watch log output for PASS/FAIL results

---

## Phase 3: Integration Tests

### 3.1 Combined Arms Scenario

Test a realistic mod scenario:

1. **Setup**: Start mission normally
2. **Spawn reinforcements**:
   ```
   spawn HeavyTrooper 10 10 1
   spawn HeavyTrooper 11 10 1
   spawn HeavyTrooper 12 10 1
   ```
3. **Verify they appear**: `enemies`
4. **Apply suppression to player**:
   - Select player unit
   - `suppression 30`
5. **Speed up time**: `timescale 2`
6. **Let AI act**: `endturn`
7. **Skip AI if stuck**: `skipai`
8. **Check state**: `status`
9. **Clear enemies**: `clearwave`
10. **Verify**: `enemies`

### 3.2 Stress Test

Spawn many units rapidly:

```
spawn Grunt 5 5 1
spawn Grunt 6 5 1
spawn Grunt 7 5 1
spawn Grunt 8 5 1
spawn Grunt 9 5 1
spawn Grunt 10 5 1
spawn Grunt 5 6 1
spawn Grunt 6 6 1
spawn Grunt 7 6 1
spawn Grunt 8 6 1
```

Then: `clearwave`

Verify no crashes, memory issues, or lingering entities.

---

## Known Issues to Watch For

1. **Template names**: Spawn may fail if template name doesn't match game data
2. **Tile coordinates**: Invalid tiles cause spawn failures
3. **Timing**: Some operations may need frame delays to take effect
4. **Active actor**: Some commands require a selected actor
5. **Turn state**: Some commands only work during specific turn phases
6. **Managed proxy availability**: Some types may lack IL2CppInterop proxies

---

## Test Results Template

| Test | Phase | Expected | Actual | Status |
|------|-------|----------|--------|--------|
| `status` | 1.1 | Shows state | | |
| `pos` | 1.2 | Shows position | | |
| `move` | 1.3 | Unit moves | | |
| `damage` | 1.4 | HP decreases | | |
| `spawn` | 1.6 | Unit appears | | |
| `clearwave` | 1.7 | Enemies gone | | |
| `endturn` | 1.8 | Turn ends | | |
| `pause` | 1.9 | Game pauses | | |
| Programmatic | 2.x | All PASS | | |

---

## Debugging Tips

If tests fail:

1. **Check Log panel** for Menace.SDK errors
2. **Check MelonLoader console** for exceptions
3. **Verify scene**: Tests only work in Tactical scene
4. **Verify selection**: Many commands need an active actor
5. **Check template names**: Use `templates Entity` to find valid names
6. **Check tile validity**: Invalid tiles fail silently

To get valid template names:
```
templates Entity
```

To inspect game types:
```
find Actor
find TacticalManager
```
