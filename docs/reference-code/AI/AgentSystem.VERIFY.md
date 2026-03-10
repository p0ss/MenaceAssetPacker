# AgentSystem.cs Verification Report

**Verification Date:** 2026-03-10
**Reference File:** `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/AI/AgentSystem.cs`
**Status:** DISCREPANCIES FOUND

---

## Summary

This report compares the documented reference code against actual decompiled binary code from Ghidra. Several significant discrepancies were found, including incorrect function addresses, oversimplified logic, and missing complexity.

---

## Function Verification

### 1. Evaluate() - Address: 0x18070eb30

**Address Status:** CORRECT
**Logic Status:** PARTIAL MATCH with significant simplifications

#### Verified Aspects:
- Field offset +0x30 (m_SelectedScore) initialized to 0 - CORRECT
- Field offset +0x28 (m_SelectedBehavior) set to null - CORRECT
- Field offset +0x3c (m_State) set to 0 - CORRECT
- Field offset +0x58 (m_Tiles dictionary) cleared - CORRECT
- Field offset +0x60 (m_TilesSecondary) assigned from +0x58 - CORRECT
- Field offset +0x18 (m_Actor) null check - CORRECT
- Actor alive check at +0x48 - CORRECT
- Actor turn done check at +0x164 - CORRECT
- Field offset +0x78 (m_SkipEvaluation) skip flag check - CORRECT
- Field offset +0x48 (m_IterationCount) increment - CORRECT
- MAX_ITERATIONS = 16 comparison - CORRECT

#### Discrepancies:

1. **Waiting Loop Logic:**
   - Reference shows simple `while (m_Faction != null)` with time check
   - Actual code checks `TacticalManager.Instance` singleton at offset +0xb8, verifies state at +0xb8, and has complex nested conditions
   - The field offset +0x4c (m_TimeThreshold) is checked against `*(float*)(param_1 + 0x10 + 0x38)` from faction
   - Additional check for `CurrentStateIndex` at offset +0x60 and field +0x50 in agent

2. **Criterion Collection:**
   - Reference shows simple foreach with `IsValid` and `Collect` calls
   - Actual code uses `S_CRITERIONS` from static field at `Menace_Tactical_AI_Agent_TypeInfo + 0xb8`
   - Virtual method calls through vtable at offset +0x178 for IsValid and +0x198 for Collect

3. **Behavior Evaluation Phase:**
   - Reference shows separate CollectBehaviors and EvaluateBehaviors methods
   - Actual code has inline loops with Attack behavior type checking via IL2CPP runtime type checks
   - Attack behavior detection uses `TypeInfo + 0x130` byte comparison for type hierarchy

4. **Thread Count Evaluation:**
   - Reference shows `criterion.GetThreads()` simple call
   - Actual code calls virtual method at vtable offset +0x188 and divides by 2 if actor is not active

5. **Missing Complexity:**
   - Reference omits the `GetActor().IsActive()` check that affects thread count
   - Reference omits the faction difficulty level check at offset +0x40

#### Corrections Needed:

```csharp
// The waiting loop should be:
while (m_Faction != null)
{
    float factionTimeValue = *(float*)(m_Faction + 0x38);  // Not m_TimeThreshold
    if (m_TimeThreshold <= factionTimeValue || m_TimeThreshold == factionTimeValue)
    {
        var skillContainer = m_Actor.GetSkillContainer();
        if (skillContainer?.IsBusy() != true)
        {
            var tacticalMgr = TacticalManager.Instance;
            if (tacticalMgr == null) break;

            // Check state at offset +0xb8 for additional condition
            if (*(byte*)(tacticalMgr + 0xb8) == 0 ||
                (*(int*)(tacticalMgr + 0x60) == 0 && *(byte*)(this + 0x50) != 0))
            {
                goto StartEvaluation;
            }

            if (!m_Actor.IsActionPointsSpent())
            {
                m_State = AgentState.EvaluatingTiles;
                // Continue to criterion evaluation...
                break;
            }
        }
    }
    Thread.Sleep(1);
}
```

---

### 2. PostProcessTileScores() - Address: 0x180711720

**Address Status:** CORRECT
**Logic Status:** SIGNIFICANTLY OVERSIMPLIFIED

#### Verified Aspects:
- Dictionary iteration over m_Tiles at offset +0x58 - CORRECT
- Actor access at offset +0x18 - CORRECT

#### Discrepancies:

1. **Score Calculation Formula:**
   - Reference shows simple:
     ```csharp
     score.FinalScore =
         score.UtilityScore * role.UtilityScale +
         score.SafetyScore * role.SafetyScale -
         score.DistanceScore * role.DistanceScale;
     ```
   - Actual code is much more complex:
     - Gets role data from Actor at vtable offset +0x398
     - Accesses role weights at +0x310
     - TileScore field at +0x30 is updated with formula involving +0x38
     - Uses AIConfig from static singleton at `AIConfig_TypeInfo + 0xb8 + 8`
     - Applies power function via `FUN_1804bbda0` with config values
     - Multiple scaling passes with different config parameters

2. **TileScore Offsets:**
   - Field +0x30: appears to be FinalScore
   - Field +0x38: additional utility value added to +0x30
   - Field +0x28: safety-related score with separate calculation
   - Field +0x10: tile reference

3. **Secondary Processing Loop:**
   - Reference completely omits the neighbor tile comparison logic
   - Actual code checks `*(byte*)(param_1 + 0x54) & 1` flag
   - If set, iterates through 8 neighbors for each tile
   - Compares scores with neighbor tiles using `GetNextTile` at vtable +0x10
   - Updates +0x50 and +0x58 pointers based on comparison results

4. **AIConfig Integration:**
   - Multiple calls to AIConfig singleton for scaling parameters
   - Parameters at offsets +0x20, +0x24, +0x28, +0x2c, +0x30, +0x34, +0x38, +0x3c

#### Corrections Needed:

```csharp
public void PostProcessTileScores()
{
    var actor = m_Actor;
    if (actor == null) return;

    var role = actor.GetEntityTemplate()?.AIRole;
    if (role == null) return;

    var roleWeights = role.Weights;  // Offset +0x310 from role data

    foreach (var kvp in m_Tiles)
    {
        var tile = kvp.Key;
        var score = kvp.Value;

        // Skip if this is actor's current tile
        if (tile == actor.GetCurrentTile()) continue;

        // First pass: add utility modifier
        score.FinalScore += score.UtilityModifier;  // +0x38 added to +0x30

        // Apply AIConfig power scaling to FinalScore
        var config = AIConfig.Instance.Weights;
        score.FinalScore = Mathf.Pow(score.FinalScore, config.FinalScorePow);

        // Apply role-based scaling
        score.FinalScore = score.FinalScore * roleWeights.PositionWeight * config.PositionMult;

        // Calculate safety score with separate formula
        score.SafetyScore = Mathf.Pow(config.SafetyBase, config.SafetyPow);
        score.SafetyScore = -score.SafetyScore * roleWeights.SafetyWeight * config.SafetyMult;
    }

    // Second pass: neighbor comparison (if flag at +0x54 is set)
    if ((m_Flags & 0x01) != 0)
    {
        int actorMoveRange = actor.GetMoveRange();
        if (actorMoveRange > 0)
        {
            foreach (var kvp in m_Tiles)
            {
                var tile = kvp.Key;
                var score = kvp.Value;
                TileScore bestSafetyNeighbor = score;
                TileScore bestFinalNeighbor = score;

                for (int dir = 0; dir < 8; dir++)
                {
                    if (tile.GetNextTile(dir, out Tile neighbor))
                    {
                        if (m_Tiles.TryGetValue(neighbor, out TileScore neighborScore))
                        {
                            if (neighborScore.SafetyScore > score.SafetyScore)
                                bestSafetyNeighbor = neighborScore;
                            if (neighborScore.FinalScore > score.FinalScore)
                                bestFinalNeighbor = neighborScore;
                        }
                    }
                }

                // Update references if better neighbor found
                float threshold = score.SafetyScore <= 0 ? 1.5f : 1.2f;
                if (threshold * score.SafetyScore <= bestSafetyNeighbor.SafetyScore)
                {
                    score.BetterSafetyTile = bestSafetyNeighbor;  // +0x50
                }

                threshold = score.FinalScore <= 0 ? 1.5f : 1.2f;
                if (threshold * score.FinalScore <= bestFinalNeighbor.FinalScore)
                {
                    score.BetterFinalTile = bestFinalNeighbor;  // +0x58
                }
            }
        }
    }
}
```

---

### 3. PickBehavior() - Address: 0x180710e40 (documented) vs 0x180710ff0 (actual)

**Address Status:** INCORRECT
- Documented: 0x180710e40
- Actual: 0x180710ff0
- 0x180710e40 is actually `OnSkillRemoved`

**Logic Status:** COMPLETELY DIFFERENT IMPLEMENTATION

#### Discrepancies:

1. **Reference Implementation (Simple):**
   ```csharp
   private Behavior PickBehavior()
   {
       Behavior best = null;
       int bestScore = 0;
       foreach (var behavior in m_Behaviors)
       {
           if (behavior.Score > bestScore)
           {
               best = behavior;
               bestScore = behavior.Score;
           }
       }
       return best;
   }
   ```

2. **Actual Implementation (Complex Weighted Random Selection):**
   - Uses AIConfig for scoring thresholds
   - Implements weighted random selection, NOT simple max-score
   - Uses `PseudoRandom.Range()` from +0x40 field
   - Calculates cumulative score threshold
   - Has debug logging integration with deployment phase checks
   - Stores debug string at +0x70 field

#### Actual Algorithm:

```csharp
private Behavior PickBehavior()
{
    var behaviors = m_Behaviors;  // +0x20
    if (behaviors == null || behaviors.Count == 0)
        return null;

    var firstBehavior = behaviors[0];
    var config = AIConfig.Instance.Weights;

    // Calculate minimum score threshold
    float threshold = Mathf.Pow(firstBehavior.Score, config.ScorePow);
    int minScore = (int)(threshold * config.ScoreMult);  // DAT_182d8fd40
    if (minScore < 1) minScore = 1;

    // Sum scores of behaviors meeting threshold
    int totalWeight = 0;
    for (int i = 0; i < behaviors.Count; i++)
    {
        float behaviorScore = Mathf.Pow(behaviors[i].Score, config.ScorePow);
        if ((int)behaviorScore >= minScore)
        {
            totalWeight += (int)behaviorScore;
        }
        else break;  // Behaviors sorted by score, so stop early
    }

    // Random selection based on weights
    var random = m_Random;  // +0x40
    if (random == null) throw new NullReferenceException();

    int roll = random.Range(1, totalWeight);

    for (int i = 0; i < behaviors.Count; i++)
    {
        float behaviorScore = Mathf.Pow(behaviors[i].Score, config.ScorePow);
        if (roll <= (int)behaviorScore)
        {
            // Debug logging if enabled and not deployment phase
            if (LogLevel.Debug && !IsDeploymentPhase())
            {
                m_QueuedDebugString = $"AI: Picked {behaviors[i].Name} (Score: {behaviors[i].Score})";
                // Additional logging logic...
            }
            return behaviors[i];
        }
        roll -= (int)behaviorScore;
    }

    return null;
}
```

#### Corrections Needed:
- **Change address comment to 0x180710ff0**
- **Complete rewrite of the function to match weighted random selection algorithm**

---

### 4. GetScoreMultForPickingThisAgent() - Address: 0x1807114c0 (documented) vs 0x180710120 (actual)

**Address Status:** INCORRECT
- Documented: 0x1807114c0 (this is inside PickBehavior function)
- Actual: 0x180710120

**Logic Status:** COMPLETELY DIFFERENT IMPLEMENTATION

#### Reference Implementation (Simple):
```csharp
public float GetScoreMultForPickingThisAgent()
{
    float mult = 1.0f;
    if (!m_Actor.HasActedThisTurn)
        mult *= 1.2f;
    float apRatio = m_Actor.ActionPoints / (float)m_Actor.MaxActionPoints;
    mult *= 0.8f + (apRatio * 0.4f);
    return mult;
}
```

#### Actual Implementation (Complex):
The actual function is much more sophisticated:

1. **TacticalManager Check:**
   - Returns immediately if current selected unit is this agent's actor

2. **Behavior Evaluation:**
   - If no selected behavior (+0x28), calls `GetOpportunityLevel`
   - Always calls `GetThreatLevel`

3. **Multiple Actor State Checks:**
   - Checks actor state via vtable call
   - Checks actor's target entity at +0x70
   - Checks if actor can move via vtable +0x408
   - Checks Move behavior's completion state at +0x94

4. **Health and Action Points:**
   - Calls `GetHitpointsPct`
   - Calls `GetActionPoints` from entity properties via vtable +0x3d8
   - Calls `IsHiddenToPlayer`

5. **Ammo Check:**
   - Gets ammo count via vtable +0x468
   - If ammo > 1 and not suppressed (byte at EntityProperties +0xec & 1 == 0):
     - Calls another method via vtable +0x478

6. **Final Calculation:**
   - Uses AIConfig power function at offset +0x50

#### Corrections Needed:

```csharp
public float GetScoreMultForPickingThisAgent()
{
    // Skip if this is the player-selected unit
    var tacticalMgr = TacticalManager.Instance;
    if (tacticalMgr != null && tacticalMgr.SelectedUnit == m_Actor)
        return 1.0f;  // Returns without calculation

    // Evaluate opportunity/threat if no behavior selected
    if (m_SelectedBehavior == null)
        GetOpportunityLevel();
    GetThreatLevel();

    if (m_Actor == null) return 1.0f;

    // Complex state checks
    bool canAct = m_Actor.CanAct();  // vtable call
    if (!canAct)
    {
        var target = m_Actor.TargetEntity;
        if (target != null)
        {
            bool targetCanAttack = target.CanAttack();
            if (!targetCanAttack)
            {
                var moveBehavior = GetBehavior<Move>(4);
                if (moveBehavior == null || moveBehavior.IsComplete)
                {
                    // Continue evaluation
                }
            }
            // Actor to target conversion check
            if (target.ToActor() != null)
            {
                // Continue
            }
        }
    }

    // Health factor
    float hpPct = m_Actor.GetHitpointsPct();

    // Action points factor
    int ammoCount = m_Actor.GetAmmoCount();
    var properties = m_Actor.GetEntityProperties();
    int actionPoints = properties.GetActionPoints();

    // Hidden bonus
    bool isHidden = m_Actor.IsHiddenToPlayer();

    // Ammo consideration
    if (ammoCount > 1)
    {
        if (!properties.IsSuppressed)  // (byte at +0xec & 1) == 0
        {
            m_Actor.SomeOtherCheck();  // vtable +0x478
        }
    }

    // Final calculation using AIConfig
    var config = AIConfig.Instance.Weights;
    return Mathf.Pow(calculatedScore, config.AgentPickPow);  // offset +0x50
}
```

---

## Field Offset Verification

| Documented | Actual | Field | Status |
|------------|--------|-------|--------|
| +0x10 | +0x10 | AIFaction parent | CORRECT |
| +0x18 | +0x18 | Actor m_Actor | CORRECT |
| +0x20 | +0x20 | List<Behavior> m_Behaviors | CORRECT |
| +0x28 | +0x28 | Behavior m_SelectedBehavior | CORRECT |
| +0x30 | +0x30 | int m_SelectedScore | CORRECT |
| +0x3C | +0x3C | AgentState m_State | CORRECT |
| +0x48 | +0x48 | int m_IterationCount | CORRECT |
| +0x4C | +0x4C | float m_TimeThreshold | CORRECT |
| +0x58 | +0x58 | Dictionary m_Tiles | CORRECT |
| +0x60 | +0x60 | Dictionary m_TilesSecondary | CORRECT |
| +0x68 | +0x68 | List<Task> m_Tasks | CORRECT |
| +0x70 | +0x70 | string m_QueuedDebugString | CORRECT |
| +0x78 | +0x78 | bool m_SkipEvaluation | CORRECT |
| - | +0x40 | PseudoRandom m_Random | MISSING |
| - | +0x50 | byte m_Flags (or similar) | MISSING |
| - | +0x54 | byte m_PostProcessFlags | MISSING |

---

## Constants Verification

| Documented | Actual | Constant | Status |
|------------|--------|----------|--------|
| 16 | 0x10 (16) | MAX_ITERATIONS | CORRECT |
| 2 | - | MIN_TILES_PER_THREAD | UNVERIFIED (used but value unclear) |

---

## Summary of Required Changes

### Critical Issues:

1. **PickBehavior() Address:** Change from `0x180710e40` to `0x180710ff0`

2. **GetScoreMultForPickingThisAgent() Address:** Change from `0x1807114c0` to `0x180710120`

3. **PickBehavior() Implementation:** Complete rewrite required - actual implementation uses weighted random selection, not simple max-score

4. **GetScoreMultForPickingThisAgent() Implementation:** Complete rewrite required - actual implementation is vastly more complex with threat/opportunity evaluation and multiple state checks

5. **PostProcessTileScores() Implementation:** Major additions needed:
   - AIConfig integration for power functions
   - Neighbor tile comparison loop
   - Additional TileScore fields

### Missing Fields:
- Add `+0x40: PseudoRandom m_Random`
- Add `+0x50: byte field (flags or similar)`
- Add `+0x54: byte m_PostProcessFlags`

### TileScore Structure:
The TileScore structure needs documentation:
- +0x10: Tile reference
- +0x28: Safety score
- +0x30: Final score
- +0x38: Utility modifier (added to final)
- +0x50: Better safety tile reference
- +0x58: Better final score tile reference

---

## Verification Methodology

1. Functions were decompiled using Ghidra MCP tools
2. Field offsets were verified against actual memory accesses in decompiled code
3. Control flow was compared between reference and actual implementations
4. Virtual method calls were traced through vtable offsets

---

## Conclusion

The reference code provides a reasonable high-level overview of the Agent system's structure, with correct field offsets for most documented fields. However, the actual implementations are significantly more complex than documented:

- **Evaluate()**: Mostly correct structure, needs waiting loop and criterion iteration corrections
- **PostProcessTileScores()**: Missing 50% of actual functionality (neighbor comparison, AIConfig integration)
- **PickBehavior()**: Completely wrong - documents simple max-selection but actual uses weighted random
- **GetScoreMultForPickingThisAgent()**: Completely wrong - documents simple formula but actual has complex multi-factor evaluation

The reference code should be considered a simplified abstraction rather than accurate documentation.
