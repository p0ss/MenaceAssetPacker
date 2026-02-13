# AI

The `AI` class provides access to the game's AI decision-making system. The Menace AI uses a utility-based decision system where `Agent` objects evaluate `Behavior` options using a multi-criteria scoring system, then execute the highest-utility action.

## Overview

```
Agent (per Actor)
  |-- RoleData (behavior weights from EntityTemplate)
  |-- Behaviors[] (available actions)
  |-- Tiles{} (scored positions)
  +-- Criterions (static evaluators)

AIFaction (per faction)
  |-- Agents[]
  |-- StrategyData (faction-level priorities)
  +-- Opponents[] (known enemies)
```

## Agent States

| Constant | Value | Description |
|----------|-------|-------------|
| `STATE_NONE` | 0 | No state |
| `STATE_EVALUATING_TILES` | 1 | Scoring tile positions |
| `STATE_EVALUATING_BEHAVIORS` | 2 | Scoring available behaviors |
| `STATE_READY_TO_EXECUTE` | 3 | Decision made, ready to act |
| `STATE_EXECUTING` | 4 | Currently executing action |

## Classes

### AgentInfo

AI agent state for a unit.

| Property | Type | Description |
|----------|------|-------------|
| `HasAgent` | bool | Whether the actor has an AI agent |
| `State` | int | Current state (see constants) |
| `StateName` | string | Human-readable state name |
| `SelectedBehavior` | string | Name of selected behavior |
| `BehaviorScore` | int | Score of selected behavior |
| `TargetTileX` | int | Target tile X coordinate |
| `TargetTileY` | int | Target tile Y coordinate |
| `TargetActorName` | string | Target actor name (if any) |
| `EvaluatedTileCount` | int | Number of tiles evaluated |
| `AvailableBehaviorCount` | int | Number of available behaviors |

### RoleDataInfo

Per-unit AI configuration from the EntityTemplate. Controls how the AI values different actions and positions.

| Property | Type | Description |
|----------|------|-------------|
| `UtilityScale` | float | Preference for high-value actions (higher = aggressive) |
| `SafetyScale` | float | Preference for safety (higher = defensive) |
| `DistanceScale` | float | Preference to stay local (higher = less roaming) |
| `FriendlyFirePenalty` | float | How much to avoid friendly fire |
| `MoveWeight` | float | Movement priority |
| `InflictDamageWeight` | float | Damage priority |
| `InflictSuppressionWeight` | float | Suppression priority |
| `StunWeight` | float | Stun priority |
| `IsAllowedToEvadeEnemies` | bool | Can retreat/disengage |
| `AttemptToStayOutOfSight` | bool | Tries to remain in fog of war |
| `PeekInAndOutOfCover` | bool | Will leave cover to attack, then return |
| `AvoidOpponents` | bool | Run from enemies (civilians) |
| `CoverAgainstOpponents` | bool | Seek cover from enemies |
| `ThreatFromOpponents` | bool | Avoid dangerous positions |

### TileScoreInfo

Tile score from AI evaluation.

| Property | Type | Description |
|----------|------|-------------|
| `X` | int | Tile X coordinate |
| `Y` | int | Tile Y coordinate |
| `UtilityScore` | float | Aggregated utility (damage potential) |
| `SafetyScore` | float | Aggregated safety (cover, threat avoidance) |
| `DistanceScore` | float | Distance penalty |
| `FinalScore` | float | Combined score after weighting |
| `CoverLevel` | int | Cover level at this tile |
| `VisibleToOpponents` | bool | Whether visible to enemies |

### BehaviorInfo

Behavior info from AI evaluation.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Behavior name |
| `TypeName` | string | Behavior class name |
| `Score` | int | Behavior score |
| `TargetTileX` | int | Target tile X |
| `TargetTileY` | int | Target tile Y |
| `TargetActorName` | string | Target actor name |
| `IsSelected` | bool | Whether this is the selected behavior |

### AIFactionInfo

AIFaction info for a faction.

| Property | Type | Description |
|----------|------|-------------|
| `FactionIndex` | int | Faction index |
| `AgentCount` | int | Number of AI agents |
| `OpponentCount` | int | Number of known opponents |
| `IsEvaluating` | bool | Whether faction is evaluating |

## Methods

### GetAgent

```csharp
public static GameObj GetAgent(GameObj actor)
```

Get the AI Agent for an actor. Returns `GameObj.Null` if the actor has no AI agent (e.g., player units).

### GetAgentInfo

```csharp
public static AgentInfo GetAgentInfo(GameObj actor)
```

Get AI agent state for an actor.

**Example:**
```csharp
var actor = TacticalController.GetActiveActor();
var info = AI.GetAgentInfo(actor);

if (info.HasAgent && info.State == AI.STATE_READY_TO_EXECUTE)
{
    DevConsole.Log($"AI will use {info.SelectedBehavior} on {info.TargetActorName}");
}
```

### GetRoleData

```csharp
public static RoleDataInfo GetRoleData(GameObj actor)
```

Get the RoleData (AI configuration) for an actor. RoleData is defined on the EntityTemplate.

**Example:**
```csharp
var actor = TacticalController.GetActiveActor();
var role = AI.GetRoleData(actor);

DevConsole.Log($"Utility: {role.UtilityScale}, Safety: {role.SafetyScale}");
DevConsole.Log($"Damage weight: {role.InflictDamageWeight}");
```

### GetBehaviors

```csharp
public static List<BehaviorInfo> GetBehaviors(GameObj actor)
```

Get all behaviors available to an actor's AI agent.

**Example:**
```csharp
var behaviors = AI.GetBehaviors(actor);
foreach (var b in behaviors)
{
    string marker = b.IsSelected ? " [SELECTED]" : "";
    DevConsole.Log($"{b.TypeName}: score {b.Score}{marker}");
}
```

### GetTileScores

```csharp
public static List<TileScoreInfo> GetTileScores(GameObj actor, int maxTiles = 10)
```

Get tile scores from an actor's AI evaluation. Returns the top N tiles by score.

**Example:**
```csharp
var tiles = AI.GetTileScores(actor, 5);
foreach (var t in tiles)
{
    DevConsole.Log($"({t.X}, {t.Y}): {t.FinalScore} (utility={t.UtilityScore}, safety={t.SafetyScore})");
}
```

### GetAIFactionInfo

```csharp
public static AIFactionInfo GetAIFactionInfo(int factionIndex)
```

Get AIFaction info for a faction index.

**Example:**
```csharp
var factionInfo = AI.GetAIFactionInfo(1); // Enemy faction
DevConsole.Log($"Enemy AI has {factionInfo.AgentCount} agents tracking {factionInfo.OpponentCount} opponents");
```

### GetAIIntent

```csharp
public static string GetAIIntent(GameObj actor)
```

Convenience method that returns a summary of the AI's current intent.

**Returns:** A human-readable string like `"InflictDamage (score: 150) -> Player1"` or `"Evaluating (EvaluatingTiles)"`.

**Example:**
```csharp
var enemies = EntitySpawner.ListEntities(1); // Faction 1
foreach (var enemy in enemies)
{
    DevConsole.Log($"{enemy.GetName()}: {AI.GetAIIntent(enemy)}");
}
```

## Write Methods (Modifying AI Behavior)

These methods modify AI state at runtime. **They have strict threading requirements.**

### Threading Safety

The AI evaluates agents in **parallel** across multiple threads. Writing during evaluation causes race conditions and crashes.

**Safe to call:**
- Before faction turn begins (e.g., in `AIFaction.OnTurnStart` hook)
- After faction turn ends
- When `IsAnyFactionEvaluating()` returns false

**NOT safe to call:**
- During `Agent.PostProcessTileScores` (parallel)
- During `Criterion.Evaluate` (parallel)
- Any time during AI evaluation phase

### IsAnyFactionEvaluating

```csharp
public static bool IsAnyFactionEvaluating()
```

Check if any AI faction is currently in parallel evaluation. When true, writes are NOT safe.

**Example:**
```csharp
if (!AI.IsAnyFactionEvaluating())
{
    // Safe to modify AI state
    AI.SetRoleDataFloat(actor, "UtilityScale", 50f);
}
```

### GetRoleDataObject

```csharp
public static GameObj GetRoleDataObject(GameObj actor)
```

Get the raw RoleData object for direct field modification. Returns `GameObj.Null` if actor has no RoleData.

### SetRoleDataFloat

```csharp
public static bool SetRoleDataFloat(GameObj actor, string fieldName, float value)
```

Set a float field on an actor's RoleData. Returns false if write failed or if called during evaluation.

**Common fields:** `UtilityScale`, `SafetyScale`, `DistanceScale`, `FriendlyFirePenalty`, `Move`, `InflictDamage`, `InflictSuppression`, `Stun`

**Example:**
```csharp
// Make unit more aggressive
AI.SetRoleDataFloat(actor, "UtilityScale", 40f);
AI.SetRoleDataFloat(actor, "SafetyScale", 5f);
```

### SetRoleDataBool

```csharp
public static bool SetRoleDataBool(GameObj actor, string fieldName, bool value)
```

Set a bool field on an actor's RoleData.

**Common fields:** `IsAllowedToEvadeEnemies`, `AttemptToStayOutOfSight`, `PeekInAndOutOfCover`, `AvoidOpponents`, `CoverAgainstOpponents`, `ThreatFromOpponents`

**Example:**
```csharp
// Allow unit to retreat
AI.SetRoleDataBool(actor, "IsAllowedToEvadeEnemies", true);
```

### ApplyRoleData

```csharp
public static bool ApplyRoleData(GameObj actor, RoleDataInfo newRole)
```

Apply a complete RoleData configuration to an actor. All fields are written.

**Example:**
```csharp
var role = AI.GetRoleData(actor);
role.UtilityScale = 50f;
role.SafetyScale = 10f;
role.PeekInAndOutOfCover = true;
AI.ApplyRoleData(actor, role);
```

### SetBehaviorScore

```csharp
public static bool SetBehaviorScore(GameObj actor, string behaviorTypeName, int score)
```

Force-set a behavior's score. Use with caution - this overrides AI decisions.

**Example:**
```csharp
// Force unit to prioritize movement
AI.SetBehaviorScore(actor, "Move", 9999);
```

## Console Commands

The AI SDK registers these console commands:

| Command | Usage | Description |
|---------|-------|-------------|
| `ai` | `ai [actor_name]` | Show AI agent info for actor |
| `airole` | `airole [actor_name]` | Show AI RoleData for actor |
| `aibehaviors` | `aibehaviors [actor_name]` | List AI behaviors for actor |
| `aitiles` | `aitiles [actor_name] [count]` | Show top tile scores for actor |
| `aiintent` | `aiintent [actor_name]` | Show what the AI is planning |

## Role Archetypes

Based on RoleData configurations, units fall into archetypes:

| Archetype | UtilityScale | SafetyScale | Key Settings |
|-----------|--------------|-------------|--------------|
| Assault | High (30+) | Low (5-10) | PeekInAndOutOfCover=true |
| Support | Medium (15) | Medium (15) | InflictSuppression high |
| Sniper | High (25) | High (25) | DistanceScale high |
| Tank | Low (10) | Low (5) | FriendlyFirePenalty high |
| Civilian | Low (0) | High (50) | AvoidOpponents=true |

## Behavior Types

### Combat Behaviors

| Behavior | RoleData Weight | Purpose |
|----------|-----------------|---------|
| InflictDamage | InflictDamage | Attack to deal damage |
| InflictSuppression | InflictSuppression | Attack to suppress |
| Stun | Stun | Stun attacks |
| Attack | (base) | Generic attack |

### Movement Behaviors

| Behavior | RoleData Weight | Purpose |
|----------|-----------------|---------|
| Move | Move | Standard movement |
| MovementSkill | Move | Jetpack, teleport |
| Deploy | - | Set up heavy weapon |

### Support Behaviors

| Behavior | Purpose |
|----------|---------|
| Assist | Help allies |
| Buff | Apply buffs |
| SupplyAmmo | Resupply |
| Reload | Reload weapons |

## Example: AI Monitor

```csharp
public class AIMonitorPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // Watch all enemy AI decisions
        DevConsole.Watch("Enemy AI", () =>
        {
            var enemies = EntitySpawner.ListEntities(1);
            var intents = new List<string>();

            foreach (var enemy in enemies)
            {
                var info = EntitySpawner.GetEntityInfo(enemy);
                if (info == null || !info.IsAlive) continue;

                var intent = AI.GetAIIntent(enemy);
                intents.Add($"{info.Name}: {intent}");
            }

            return string.Join("\n", intents);
        });
    }
}
```

## Example: Safe AI Modification

This pattern shows how to safely modify AI behavior without causing crashes:

```csharp
public class AIModifierPlugin : IModpackPlugin
{
    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // SAFE: Hook OnTurnStart - runs before parallel evaluation
        harmony.Patch(
            typeof(AIFaction).GetMethod("OnTurnStart"),
            postfix: new HarmonyMethod(typeof(AIModifierPlugin), nameof(OnTurnStart_Postfix))
        );
    }

    // This hook runs BEFORE parallel evaluation begins - safe to write
    static void OnTurnStart_Postfix(object __instance)
    {
        // Make all wounded enemies more defensive
        var enemies = EntitySpawner.ListEntities(1);
        foreach (var enemy in enemies)
        {
            var entityInfo = EntitySpawner.GetEntityInfo(enemy);
            if (entityInfo == null) continue;

            // If badly wounded, switch to defensive behavior
            float healthPercent = entityInfo.Health / (float)entityInfo.MaxHealth;
            if (healthPercent < 0.3f)
            {
                AI.SetRoleDataFloat(enemy, "SafetyScale", 40f);
                AI.SetRoleDataFloat(enemy, "UtilityScale", 10f);
                AI.SetRoleDataBool(enemy, "IsAllowedToEvadeEnemies", true);
            }
        }
    }
}
```

**WRONG - Will crash:**

```csharp
// DON'T DO THIS - PostProcessTileScores runs in parallel!
[HarmonyPatch(typeof(Agent), "PostProcessTileScores")]
static void PostProcessTileScores_Postfix(Agent __instance)
{
    // CRASH: Multiple threads may be modifying AI state simultaneously
    var actor = __instance.GetActor();
    AI.SetRoleDataFloat(actor, "UtilityScale", 50f); // Race condition!
}
```

## Threading Considerations

The AI system uses multithreading for tile evaluation. When hooking AI methods:

| Hook Target | Threading | Safe to Share State? |
|-------------|-----------|---------------------|
| `AIFaction.OnTurnStart` | Single-threaded | Yes |
| `Agent.PostProcessTileScores` | Parallel | No |
| `Agent.Execute` | Sequential | Yes |
| `Criterion.Evaluate` | Parallel | No |

**Safe Pattern:** Pre-compute shared state in `OnTurnStart`, only read it in parallel hooks.

## See Also

- [TacticalController](tactical-controller.md) - Turn management
- [EntityCombat](entity-combat.md) - Combat actions
- [Pathfinding](pathfinding.md) - Movement costs and paths
- [LineOfSight](line-of-sight.md) - Visibility checks
