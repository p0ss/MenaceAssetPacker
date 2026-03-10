# RoleData.cs Verification Report

**Generated:** 2026-03-10
**Binary Analysis Tool:** Ghidra MCP
**Reference Files Analyzed:**
- `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/AI/RoleData.cs`
- `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/AI/AgentSystem.cs`
- `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/AI/Behaviors.cs`

---

## Summary

| Function | Documented Address | Actual Address | Status |
|----------|-------------------|----------------|--------|
| Agent.Evaluate() | 0x18070eb30 | 0x18070eb30 | PARTIAL MATCH |
| Agent.PostProcessTileScores() | 0x180711720 | 0x180711720 | DISCREPANCY |
| Agent.PickBehavior() | 0x180710e40 (estimated) | 0x180710ff0 | INCORRECT ADDRESS |
| Agent.GetScoreMultForPickingThisAgent() | 0x1807114c0 | 0x180710120 | INCORRECT ADDRESS |
| InflictDamage.GetTargetValue() | 0x18072db70 | 0x18072db70 | DISCREPANCY |

---

## Detailed Analysis

### 1. Agent.Evaluate() - Address: 0x18070eb30

**Status:** PARTIAL MATCH - Logic flow is correct but implementation details differ

**Reference Code Claims:**
- Resets m_SelectedScore to 0, m_SelectedBehavior to null, m_State to None
- Clears m_Tiles dictionary
- Validates actor (alive, not busy)
- Checks skip flag
- Waits for previous actions
- MAX_ITERATIONS = 16
- Calls EvaluateTilesWithCriterions, CollectBehaviors, EvaluateTilesSecondPass
- Calls PostProcessTileScores
- Evaluates and sorts behaviors
- Picks best behavior with score multiplier

**Actual Implementation Findings:**
- CORRECT: Resets score at offset +0x30, behavior at +0x28, state at +0x3c
- CORRECT: Clears tiles dictionary at +0x58
- CORRECT: Checks actor at +0x18 for validity (alive check at +0x48, turn done at +0x164)
- CORRECT: Skip evaluation flag at +0x78
- CORRECT: MAX_ITERATIONS = 16 (0x10 in hex)
- CORRECT: Iteration count at +0x48
- CORRECT: Parallelized criterion evaluation with thread scheduling
- CORRECT: Calls PostProcessTileScores, PickBehavior, GetScoreMultForPickingThisAgent
- CORRECT: Score calculation: `Math.Max(1, (int)(behavior.Score * scoreMult))`

**Discrepancies Found:**
1. Field offset for time threshold appears to be +0x4c (matches reference)
2. Faction reference is at +0x10 (matches reference)
3. The wait loop checks TacticalManager state index at +0x60, not just 0

**Corrections Needed:**
- The reference code's wait loop logic is simplified; actual implementation also checks TacticalManager.CurrentStateIndex == 0 condition
- Add field: m_DeploymentPhaseFlag at +0x50 (used in actual implementation)

---

### 2. Agent.PostProcessTileScores() - Address: 0x180711720

**Status:** DISCREPANCY - Significant differences in logic

**Reference Code Claims:**
```csharp
score.FinalScore =
    score.UtilityScore * role.UtilityScale +
    score.SafetyScore * role.SafetyScale -
    score.DistanceScore * role.DistanceScale;
```

**Actual Implementation Findings:**

The decompiled code shows a much more complex implementation:

1. **First Pass - Score Modification:**
   - Gets RoleData from EntityTemplate at offset +0x310 (CORRECT)
   - Applies multiple power functions via `FUN_1804bbda0` (likely `Math.Pow`)
   - Uses AIConfig multipliers from offsets: +0x20, +0x24, +0x28, +0x2c, +0x30, +0x34, +0x38, +0x3c

2. **Actual Formula Pattern (reconstructed):**
   ```csharp
   // At TileScore offset +0x30 (FinalScore)
   // At TileScore offset +0x28 (UtilityScore)
   // At TileScore offset +0x38 (appears to be additional score component)

   // First adds additional component to FinalScore
   tileScore.FinalScore = tileScore.FinalScore + tileScore[+0x38];

   // Then applies power function with AIConfig.BehaviorScorePOW
   tileScore.FinalScore = Pow(tileScore.FinalScore, AIConfig[+0x20]);

   // Role-based scaling:
   tileScore.FinalScore = role.UtilityScale * tileScore.FinalScore * AIConfig[+0x24];
   tileScore.FinalScore = Pow(tileScore.FinalScore, AIConfig[+0x28]) * AIConfig[+0x2c];

   // Safety score processing:
   tileScore.UtilityScore = Pow(tileScore.UtilityScore, AIConfig[+0x30]);
   tileScore.UtilityScore = Pow(role.SafetyScale * tileScore.UtilityScore * AIConfig[+0x34], AIConfig[+0x38]);
   // Note: XOR operation with constant (negation handling)
   tileScore.UtilityScore = tileScore.UtilityScore * AIConfig[+0x3c];
   ```

3. **Second Pass - Neighbor Tile Comparison (NOT IN REFERENCE):**
   - Checks flag at +0x54 (byte, bitwise AND with 1)
   - If enabled, iterates through 8 neighboring tiles
   - Compares utility and final scores with neighbors
   - Sets "better neighbor" references at offsets +0x50 and +0x58 in TileScore

**Corrections Needed:**
- The reference code formula is OVERSIMPLIFIED
- Add neighbor tile comparison logic
- Add AIConfig power function applications
- Correct TileScore structure to include:
  - +0x38: Additional score component
  - +0x50: Better utility neighbor reference
  - +0x58: Better final score neighbor reference

---

### 3. Agent.PickBehavior() - Address: 0x180710ff0 (NOT 0x180710e40)

**Status:** INCORRECT ADDRESS + DISCREPANCY

**Reference Code Claims:**
- Simple iteration finding highest score behavior
- Returns behavior with highest score

**Actual Implementation Findings:**

The actual implementation is SIGNIFICANTLY more complex:

1. **Weighted Random Selection (NOT Simple Maximum):**
   ```csharp
   // Gets threshold from AIConfig
   float threshold = FUN_1804bbda0() * DAT_182d8fd40;
   if (threshold < 1) threshold = 1;

   // Accumulates scores for behaviors above threshold
   int totalScore = 0;
   foreach (behavior in behaviors) {
       float score = FUN_1804bbda0(behavior);  // Applies power function
       if (score >= threshold) {
           totalScore += (int)score;
       }
   }

   // Random selection using PseudoRandom at +0x40
   int randomValue = PseudoRandom.Range(1, totalScore);

   // Selects behavior based on cumulative probability
   int cumulative = 0;
   foreach (behavior in behaviors) {
       float score = FUN_1804bbda0(behavior);
       if (randomValue <= (int)score) {
           return behavior;
       }
       randomValue -= (int)score;
   }
   ```

2. **Debug Logging:**
   - Extensive debug string building for selected behavior
   - Checks IsDeploymentPhase condition
   - Stores debug string at +0x70

3. **Key Fields Used:**
   - +0x20: Behaviors list
   - +0x40: PseudoRandom instance
   - +0x70: Debug string (m_QueuedDebugString)

**Corrections Needed:**
- CRITICAL: The selection is NOT deterministic "pick highest score"
- It uses weighted random selection among behaviors above a threshold
- This allows AI variability while still preferring high-scoring behaviors
- Add PseudoRandom field documentation at +0x40
- Update address to 0x180710ff0

---

### 4. Agent.GetScoreMultForPickingThisAgent() - Address: 0x180710120 (NOT 0x1807114c0)

**Status:** INCORRECT ADDRESS + DISCREPANCY

**Reference Code Claims:**
```csharp
float mult = 1.0f;
if (!m_Actor.HasActedThisTurn) mult *= 1.2f;
float apRatio = m_Actor.ActionPoints / (float)m_Actor.MaxActionPoints;
mult *= 0.8f + (apRatio * 0.4f);
return mult;
```

**Actual Implementation Findings:**

The actual function is much more complex:

1. **Checks if this is the selected unit in TacticalManager:**
   ```csharp
   if (TacticalManager.Instance.SelectedUnit == m_Actor) {
       return; // Early return, no modification
   }
   ```

2. **Calculates opportunity and threat levels:**
   ```csharp
   if (m_SelectedBehavior == null) {
       GetOpportunityLevel();
   }
   GetThreatLevel();
   ```

3. **Movement behavior check:**
   - Checks if actor can move
   - Gets Move behavior via generic method
   - Checks if movement is prevented (+0x94 flag on behavior)

4. **Health and action point factors:**
   ```csharp
   GetHitpointsPct();
   GetActionPoints();
   IsHiddenToPlayer();
   ```

5. **Turn count handling:**
   - Gets turn count, special handling if > 1
   - Checks action type at +0x478 method
   - Checks whether actor has acted this turn

6. **Final calculation:**
   - Applies power function from AIConfig at +0x50

**Corrections Needed:**
- The reference implementation is HIGHLY SIMPLIFIED
- Actual implementation considers:
  - Whether this is the currently selected unit
  - Opportunity level
  - Threat level
  - Movement capability
  - Health percentage
  - Action points
  - Hidden status
  - Turn count
  - Previous action type
- Update address to 0x180710120

---

### 5. InflictDamage.GetTargetValue() - Address: 0x18072db70

**Status:** DISCREPANCY - Reference is wrapper, actual logic is in SkillBehavior.GetTargetValue

**Reference Code Claims:**
```csharp
var hitResult = skill.GetHitchance(...);
float hitChance = hitResult.FinalValue / 100f;
float damage = skill.GetExpectedDamage(target);
float value = hitChance * damage;
value *= 1f + (target.ThreatLevel * weights.TargetValueThreatScale);
// Kill bonus handling
```

**Actual Implementation Findings:**

1. **InflictDamage.GetTargetValue at 0x18072db70 is a WRAPPER:**
   ```csharp
   void GetTargetValue(param_1, param_2, ...) {
       if (param_2 == false) {
           usesLeft = 0;
       } else {
           actor = this[+0x10].Actor[+0x18];
           usesLeft = actor.GetRemainingTurnCount();  // +0x458 vtable
           skill = this[+0x20];
           usesLeft = skill.GetUsesLeftThisTurn(usesLeft);
       }
       // Delegates to base class
       SkillBehavior.GetTargetValue(this, param_2, usesLeft, ...);
   }
   ```

2. **Actual logic is in SkillBehavior.GetTargetValue at 0x180733460:**
   - ~600 lines of decompiled code
   - Handles damage, suppression, morale, armor damage, kill potential
   - Complex multi-phase calculations

3. **Key calculations from SkillBehavior.GetTargetValue:**

   **Hit Chance:**
   ```csharp
   hitResult = Skill.GetHitchance(skill, fromTile, targetTile, properties, defenderProps, true, target, true);
   hitChance = hitResult * 0.01f;  // DAT_182d8fbd8 = 0.01
   ```

   **Expected Damage:**
   ```csharp
   damageResult = Skill.GetExpectedDamage(skill, fromTile, targetTile, ultimateTile, target, attackProps, defenderProps, usesLeft, false);
   ```

   **Armor Damage Calculation:**
   ```csharp
   if (hitChance * damageResult[+0x14] > 0) {
       armor = GetArmor(target, 3);
       armorDurability = GetArmorDurabilityPct(target);
       hitpoints = target.Hitpoints;
       expectedDamage = hitChance * damageResult[+0x14];
       remaining = hitpoints - expectedDamage;
       if (remaining < 0) remaining = 0;

       armorScore = (armorDurability * armor - (remaining / maxHP) * armor)
                    * AIConfig[+0xE8] * min(armor, maxArmorCap) * 0.01f;
   }
   ```

   **Kill Bonus:**
   ```csharp
   if (hitChance * damageResult[+0x1C] >= currentHP) {
       value *= 1.5f;  // DAT_182d8fc24
   }
   ```

   **Health Threshold Bonuses:**
   ```csharp
   for (threshold = 0; threshold < Config.HealthThresholds.Count; threshold++) {
       thresholdValue = Config.HealthThresholds[threshold];
       projectedHealth = (hitpoints - hitChance * damage) / maxHP;
       if (thresholdValue < projectedHealth) break;
       if (thresholdValue < currentHealthPct) {
           bonus = Config.ThresholdBonuses[threshold] * 0.01f * 0.01f + 1.0f;
           value *= bonus;
       }
   }
   ```

   **Morale Damage:**
   ```csharp
   if (actor.HasMorale && actor.Morale > 0) {
       // Complex morale damage calculation
       // Considers discipline, suppression, expected damage
       // Uses AIConfig[+0xEC] for morale multiplier
   }
   ```

   **Suppression Value:**
   ```csharp
   expectedSuppression = Skill.GetExpectedSuppression(...);
   suppressionValue = expectedSuppression * 0.01f;
   // Applies cover bonuses, discipline penalties
   // AIConfig[+0xEC] for suppression multiplier
   ```

**Corrections Needed:**
- Reference code is HIGHLY SIMPLIFIED
- Actual implementation includes:
  - Armor damage calculation with durability
  - Health threshold bonus system (multiple thresholds)
  - Morale damage complex calculation
  - Suppression state changes
  - Cover usage penalties
  - Opponent tracking with damage history at +0x58 (Dictionary<Actor, float>)
  - Tile effect spawning consideration
  - Target prioritization based on previous damage dealt
  - Uses AIConfig multipliers at offsets:
    - +0xE4: Damage score base multiplier
    - +0xE8: Armor damage multiplier
    - +0xEC: Morale/suppression multiplier
    - +0xF0: Stun score multiplier
    - +0xF8: Damage history bonus multiplier

---

## RoleData Structure Verification

The RoleData structure in RoleData.cs appears to be a DATA-ONLY class with no methods requiring address verification. However, based on the decompiled code usage, we can verify field offsets:

| Field | Documented Offset | Verified |
|-------|------------------|----------|
| TargetFriendlyFireValueMult | +0x10 | NEEDS VERIFICATION |
| UtilityScale | +0x14 | CONFIRMED (used in PostProcessTileScores) |
| UtilityThresholdScale | +0x18 | NEEDS VERIFICATION |
| SafetyScale | +0x1C | CONFIRMED (used in PostProcessTileScores) |
| DistanceScale | +0x20 | NEEDS VERIFICATION |
| FriendlyFirePenalty | +0x24 | NEEDS VERIFICATION |
| IsAllowedToEvadeEnemies | +0x28 | NEEDS VERIFICATION |
| AttemptToStayOutOfSight | +0x29 | NEEDS VERIFICATION |
| PeekInAndOutOfCover | +0x2A | NEEDS VERIFICATION |
| UseAoeAgainstSingleTargets | +0x2B | NEEDS VERIFICATION |
| Move | +0x2C | NEEDS VERIFICATION |
| InflictDamage | +0x30 | NEEDS VERIFICATION |
| InflictSuppression | +0x34 | NEEDS VERIFICATION |
| Stun | +0x38 | NEEDS VERIFICATION |
| AvoidOpponents | +0x3C | NEEDS VERIFICATION |
| ConsiderSurroundings | +0x3D | NEEDS VERIFICATION |
| CoverAgainstOpponents | +0x3E | NEEDS VERIFICATION |
| DistanceToCurrentTile | +0x3F | NEEDS VERIFICATION |
| ConsiderZones | +0x40 | NEEDS VERIFICATION |
| ThreatFromOpponents | +0x41 | NEEDS VERIFICATION |
| ExistingTileEffects | +0x42 | NEEDS VERIFICATION |
| IgnoreTileEffects | +0x44 | NEEDS VERIFICATION |

**Note:** EntityTemplate.AIRole is at offset +0x310 (CONFIRMED)

---

## Critical Issues Summary

1. **PickBehavior uses WEIGHTED RANDOM SELECTION, not deterministic maximum selection**
   - This is a fundamental difference in AI behavior
   - The AI has intentional variability

2. **PostProcessTileScores applies multiple power functions and neighbor comparisons**
   - The simple linear formula in reference is incorrect
   - AIConfig contains exponent values for non-linear scoring

3. **GetScoreMultForPickingThisAgent is much more complex**
   - Considers opportunity level, threat level, health, action points, etc.

4. **InflictDamage.GetTargetValue is a wrapper**
   - Actual logic is in SkillBehavior.GetTargetValue
   - Much more complex with armor, morale, suppression calculations

5. **Address corrections needed:**
   - PickBehavior: 0x180710e40 -> 0x180710ff0
   - GetScoreMultForPickingThisAgent: 0x1807114c0 -> 0x180710120

---

## Recommendations

1. **Update addresses** in reference code comments
2. **Expand PickBehavior documentation** to show weighted random selection
3. **Add AIConfig structure documentation** with offset mappings for all multipliers
4. **Document the neighbor tile comparison** in PostProcessTileScores
5. **Split GetTargetValue documentation** to show wrapper and actual implementation
6. **Add TileScore additional fields** (+0x38, +0x50, +0x58)
7. **Document PseudoRandom usage** in Agent at +0x40
