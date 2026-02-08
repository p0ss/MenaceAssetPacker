# AI Decision Making System

## Overview

The Menace AI uses a utility-based decision system where `Agent` objects evaluate `Behavior` options using a multi-criteria scoring system, then execute the highest-utility action.

## Core Architecture

```
Agent (per Actor)
  ├── RoleData (behavior weights from EntityTemplate)
  ├── Behaviors[] (available actions)
  ├── Tiles{} (scored positions)
  └── Criterions (static evaluators)

AIFaction (per faction)
  ├── Agents[]
  ├── StrategyData (faction-level priorities)
  └── Opponents[] (known enemies)

AIWeightsTemplate (global config)
  └── Criterion weights & scaling
```

## RoleData - Per-Unit AI Configuration

RoleData is defined on each `EntityTemplate` at offset +0x310. It controls how the AI values different actions and positions.

### RoleData Fields

```c
public class RoleData {
    // === Friendly Fire Avoidance ===
    [Range(0, 10)]
    public float TargetFriendlyFireValueMult;  // +0x10
    // How much OTHER units avoid hitting this unit. Higher = more protected.

    // === Criterion Weights ===
    [Range(0, 50)]
    public float UtilityScale;        // +0x14
    // Preference for high-value actions. Higher = aggressive, seeks damage opportunities.

    [Range(0, 2)]
    public float UtilityThresholdScale; // +0x18
    // Minimum usefulness requirement. Higher = won't move to useless positions.

    [Range(0, 50)]
    public float SafetyScale;         // +0x1C
    // Preference for safety. Higher = defensive, seeks cover over offense.

    [Range(0, 50)]
    public float DistanceScale;       // +0x20
    // Preference to stay local. Higher = doesn't roam far.

    [Range(0, 50)]
    public float FriendlyFirePenalty; // +0x24
    // How much to avoid friendly fire. Higher = more careful.

    // === Behavioral Settings ===
    public bool IsAllowedToEvadeEnemies;    // +0x28
    // Can retreat/disengage from fights.

    public bool AttemptToStayOutOfSight;    // +0x29
    // Tries to remain in fog of war.

    public bool PeekInAndOutOfCover;        // +0x2A
    // Will leave cover to attack, then return.

    public bool UseAoeAgainstSingleTargets; // +0x2B
    // Uses AoE on lone targets (enable if no single-target skills).

    // === Behavior Weights ===
    [Range(0, 10)]
    public float Move;                // +0x2C - Movement priority
    public float InflictDamage;       // +0x30 - Damage priority
    public float InflictSuppression;  // +0x34 - Suppression priority
    public float Stun;                // +0x38 - Stun priority

    // === Criterion Toggles ===
    public bool AvoidOpponents;       // +0x3C - Run from enemies (civilians)
    public bool ConsiderSurroundings; // +0x3D - Evaluate reachable tiles
    public bool CoverAgainstOpponents; // +0x3E - Seek cover from enemies
    public bool DistanceToCurrentTile; // +0x3F - Prefer nearby tiles
    public bool ConsiderZones;        // +0x40 - Respect mission zones
    public bool ThreatFromOpponents;  // +0x41 - Avoid dangerous positions
    public bool ExistingTileEffects;  // +0x42 - React to fire/hazards

    public TileEffectType IgnoreTileEffects; // +0x44 - Tile effects to ignore
}
```

### Role Archetypes

Based on RoleData configurations, units fall into archetypes:

| Archetype | UtilityScale | SafetyScale | Key Settings |
|-----------|--------------|-------------|--------------|
| Assault | High (30+) | Low (5-10) | PeekInAndOutOfCover=true |
| Support | Medium (15) | Medium (15) | InflictSuppression high |
| Sniper | High (25) | High (25) | DistanceScale high |
| Tank | Low (10) | Low (5) | FriendlyFirePenalty high |
| Civilian | Low (0) | High (50) | AvoidOpponents=true |

## AIWeightsTemplate - Global Configuration

A ScriptableObject that defines global AI tuning. Found at `Menace/Config/AI Weights`.

### Key Global Weights

```c
public class AIWeightsTemplate : ScriptableObject {
    // === Score Processing ===
    public float BehaviorScorePOW;     // +0x18 - Exponent on final scores
    public int TTL_MAX;                // +0x1C - Max evaluation iterations

    // === Utility Scaling ===
    public float UtilityPOW;           // +0x20
    public float UtilityScale;         // +0x24
    public float UtilityPostPOW;       // +0x28
    public float UtilityPostScale;     // +0x2C

    // === Safety Scaling ===
    public float SafetyPOW;            // +0x30
    public float SafetyScale;          // +0x34

    // === Threat Assessment ===
    public float ThreatFromOpponents;         // +0x74
    public float ThreatFromOpponentsDamage;   // +0x80
    public float ThreatFromOpponentsArmorDamage; // +0x84
    public float ThreatFromOpponentsSuppression; // +0x88
    public float ThreatFromOpponentsStun;     // +0x8C

    // === Threat Modifiers (reduced threat from...) ===
    public float ThreatFromPinnedDownOpponents;    // +0x90
    public float ThreatFromSuppressedOpponents;    // +0x94
    public float ThreatFrom2xStunnedOpponents;     // +0x98
    public float ThreatFromFleeingOpponents;       // +0x9C
    public float ThreatFromOpponentsAlreadyActed;  // +0xA0

    // === Attack Scoring ===
    public float TargetValueDamageScale;      // +0xE4
    public float TargetValueSuppressionScale; // +0xEC
    public float TargetValueThreatScale;      // +0xF4
    public float FriendlyFirePenalty;         // +0x100

    // === Behavior-Specific ===
    public float DamageBaseScore;      // +0x104
    public float DamageScoreMult;      // +0x108
    public float SuppressionBaseScore; // +0x110
    public float SuppressionScoreMult; // +0x114
}
```

## Criterion System

Criterions evaluate tile positions. Each criterion runs for every candidate tile.

### Criterion Base Class

```c
public abstract class Criterion {
    // Is this criterion relevant for this actor?
    abstract bool IsValid(Actor _actor);

    // How many threads to use (default: 1)
    virtual int GetThreads();

    // Pre-evaluation data collection
    virtual void Collect(Actor _actor, Dictionary<Tile, TileScore> _tiles);

    // Score a single tile
    virtual void Evaluate(Actor _actor, TileScore _tile);

    // Post-process all tiles
    virtual void PostProcess(Actor _actor, Dictionary<Tile, TileScore> _tiles);
}
```

### Built-in Criterions

| Criterion | Purpose | Controlled By |
|-----------|---------|---------------|
| CoverAgainstOpponents | Score tiles based on cover from enemies | RoleData.CoverAgainstOpponents |
| DistanceToCurrentTile | Penalize distant tiles | RoleData.DistanceToCurrentTile |
| ThreatFromOpponents | Score tiles by safety from enemy fire | RoleData.ThreatFromOpponents |
| ExistingTileEffects | React to fire, smoke, supply tiles | RoleData.ExistingTileEffects |
| FleeFromOpponents | Score tiles away from enemies | RoleData.AvoidOpponents |
| WakeUp | Force sleeping units to activate | Internal |

### Criterion Evaluation (CoverAgainstOpponents)

```c
// From decompilation
public class CoverAgainstOpponents : Criterion {
    const int ADDITIONAL_RANGE = 3;
    const float NO_COVER_AT_ALL_PENALTY = 10f;
    const float NOT_VISIBLE_TO_OPPONENTS_HERE_MULT = 0.9f;

    float[] COVER_PENALTIES; // Per cover level

    void Evaluate(Actor actor, TileScore tile) {
        // For each known opponent:
        //   Calculate cover at this tile against that opponent
        //   Apply cover penalty based on cover level
        //   Multiply by visibility factor
        // Sum all opponent scores
    }
}
```

### TileScore Structure

```c
public class TileScore {
    public Tile Tile;              // +0x10
    public Tile UltimateTile;      // +0x18 - Final destination
    public float DistanceToCurrentTile; // +0x20
    public float DistanceScore;    // +0x24
    // Additional scoring fields...
}
```

## Agent Evaluation Flow

```
Agent.Evaluate() called each AI turn
│
├── 1. Collect candidate tiles (reachable positions)
│
├── 2. For each Criterion in S_CRITERIONS:
│   ├── if (!criterion.IsValid(actor)) skip
│   ├── criterion.Collect(actor, tiles) - gather data
│   ├── For each tile (parallelized):
│   │   └── criterion.Evaluate(actor, tileScore)
│   └── criterion.PostProcess(actor, tiles)
│
├── 3. Combine tile scores:
│   │  FinalScore = UtilityScore × RoleData.UtilityScale
│   │             + SafetyScore × RoleData.SafetyScale
│   │             - DistanceScore × RoleData.DistanceScale
│   └── Apply AIWeightsTemplate scaling
│
├── 4. For each Behavior:
│   ├── if (!behavior.IsUsable(agent)) skip
│   ├── behavior.Evaluate(agent) - score from best tile
│   └── Apply RoleData behavior weight (Move, InflictDamage, etc.)
│
├── 5. Select highest-scoring Behavior + Tile combination
│
└── 6. Execute: behavior.Execute(agent)
```

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
| TurnArmorTowardsThreat | - | Face armor correctly |

### Support Behaviors

| Behavior | Purpose |
|----------|---------|
| Assist | Help allies |
| Buff | Apply buffs |
| SupplyAmmo | Resupply |
| Reload | Reload weapons |

## Opponent Tracking

```c
public class Opponent {
    // Threat assessment fields
    public Assessment.Range DamageRange;      // +0x18
    public Assessment.Range SuppressionRange; // +0x20
    public List<Skill> Attacks;               // +0x48

    // Current threat calculations
    public Dictionary<Actor, float> CurrentThreatPosed; // +0x58
    public float CurrentThreatPosedTotal;     // +0x60
    public float CurrentThreatPosedMax;       // +0x64
}
```

## Threading

The AI uses multithreading for tile evaluation:
- `m_Tasks` list manages parallel work
- `MIN_TILES_PER_THREAD = 2` - Minimum tiles per thread
- `MAX_ITERATIONS = 16` - Maximum evaluation cycles
- Criterions can specify thread count via `GetThreads()`

### Thread Safety for Modders

**Critical:** Multiple agents within a faction are evaluated **in parallel**. When hooking AI methods, be aware of which hooks run concurrently:

| Hook Target | Threading | Safe to Share State? |
|-------------|-----------|---------------------|
| `AIFaction.OnTurnStart` | Single-threaded | Yes - runs before parallel evaluation |
| `Agent.GetScoreMultForPickingThisAgent` | Parallel | No - called for multiple agents concurrently |
| `Agent.PostProcessTileScores` | Parallel | No - called for multiple agents concurrently |
| `Agent.Execute` | Sequential | Yes - one agent executes at a time |
| `Criterion.Evaluate` | Parallel | No - parallelized across tiles |

**Common Race Condition Pattern (DON'T DO THIS):**

```csharp
// UNSAFE: Multiple threads may enter simultaneously
static void PostProcessTileScores_Postfix(Agent __instance) {
    var state = GetSharedState(factionIndex);
    if (!state.Cache.IsValid) {        // Thread A checks: false
        state.Cache.IsValid = true;     // Thread A sets true
                                        // Thread B checks: true, returns early
        PopulateCache(state.Cache);     // Thread A still populating...
    }                                   // Thread B uses incomplete cache!
    UseCache(state.Cache);
}
```

**Safe Pattern: Pre-compute in OnTurnStart:**

```csharp
// SAFE: Compute shared state before parallel evaluation begins
static void OnTurnStart_Postfix(AIFaction __instance) {
    var state = GetOrCreateState(factionIndex);
    state.Reset();

    // Pre-compute anything that will be shared across agents
    ComputeEnemyPositions(state);
    ComputeFormationData(state);
    state.IsReady = true;
}

static void PostProcessTileScores_Postfix(Agent __instance) {
    var state = GetState(factionIndex);
    if (!state.IsReady) return;  // Just read, never write from here

    // Safe: only reading pre-computed data
    ApplyFormationBonus(state.FormationData);
}
```

**Key Principles:**
1. **Compute once, read many** - Populate shared caches in `OnTurnStart`, only read them in parallel hooks
2. **No lazy initialization** - Don't use "ensure initialized" patterns in parallel code
3. **Avoid shared mutable state** - If you must share state, use locks or make it immutable after initialization
4. **Per-agent state is safe** - Each agent has its own `m_Tiles` dictionary; modifying an agent's own data is safe

## Agent States

```c
public enum Agent.State {
    None = 0,
    EvaluatingTiles = 1,
    EvaluatingBehaviors = 2,
    ReadyToExecute = 3,
    Executing = 4
}
```

## Modding AI Behavior

### Change Unit Role

Modify `EntityTemplate.AIRole` in the asset file to change how a unit behaves.

### Hook Evaluation

```csharp
[HarmonyPatch(typeof(Agent), "Evaluate")]
class AIEvaluatePatch {
    static void Postfix(Agent __instance) {
        // Log or modify AI decisions
        var actor = __instance.GetActor();
        var role = __instance.GetRole();
        Logger.Msg($"{actor.name}: Utility={role.UtilityScale}, Safety={role.SafetyScale}");
    }
}
```

### Modify Global Weights

The AIWeightsTemplate is a ScriptableObject loaded at runtime. You can:
1. Replace it via asset bundle
2. Modify fields at runtime via reflection
3. Patch the criterion evaluation methods

## Debugging

```c
private string m_QueuedDebugString;  // +0x70 on Agent
// Debug info queued during evaluation for dev tools
```

Enable debug output by hooking into the debug string generation.
