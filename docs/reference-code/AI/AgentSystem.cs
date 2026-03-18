// =============================================================================
// MENACE REFERENCE CODE - AI Agent System
// =============================================================================
// Reconstructed utility-based AI decision system showing how agents evaluate
// positions, score behaviors, and execute actions.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Menace.Tactical.AI
{
    /// <summary>
    /// Agent evaluation state machine.
    /// </summary>
    public enum AgentState
    {
        None = 0,
        EvaluatingTiles = 1,
        EvaluatingBehaviors = 2,
        ReadyToExecute = 3,
        Executing = 4
    }

    /// <summary>
    /// Per-unit AI controller that evaluates positions and behaviors.
    ///
    /// Each Actor has one Agent. Agents are managed by AIFaction.
    ///
    /// Key fields:
    ///   +0x10: AIFaction parent
    ///   +0x18: Actor being controlled
    ///   +0x20: List&lt;Behavior&gt; available behaviors
    ///   +0x28: Behavior selected behavior
    ///   +0x30: int selected behavior score
    ///   +0x3C: AgentState current state
    ///   +0x40: PseudoRandom m_Random (for weighted behavior selection)
    ///   +0x48: int evaluation iteration count
    ///   +0x4C: float m_TimeThreshold
    ///   +0x50: byte m_Flags
    ///   +0x54: byte m_PostProcessFlags (bit 0 = enable neighbor comparison)
    ///   +0x58: Dictionary&lt;Tile, TileScore&gt; evaluated tiles
    ///   +0x60: Dictionary&lt;Tile, TileScore&gt; secondary tiles
    ///   +0x68: List&lt;Task&gt; parallel evaluation tasks
    ///   +0x70: string m_QueuedDebugString
    ///   +0x78: bool m_SkipEvaluation
    /// </summary>
    public class Agent
    {
        // =====================================================================
        // FIELDS
        // =====================================================================

        /// <summary>Parent faction. Offset: +0x10</summary>
        private AIFaction m_Faction;

        /// <summary>Actor being controlled. Offset: +0x18</summary>
        private Actor m_Actor;

        /// <summary>Available behaviors. Offset: +0x20</summary>
        private List<Behavior> m_Behaviors;

        /// <summary>Selected behavior for execution. Offset: +0x28</summary>
        private Behavior m_SelectedBehavior;

        /// <summary>Score of selected behavior. Offset: +0x30</summary>
        private int m_SelectedScore;

        /// <summary>Current evaluation state. Offset: +0x3C</summary>
        private AgentState m_State;

        /// <summary>Random number generator for weighted selection. Offset: +0x40</summary>
        private PseudoRandom m_Random;

        /// <summary>Evaluation iteration counter (max 16). Offset: +0x48</summary>
        private int m_IterationCount;

        /// <summary>Time threshold for evaluation. Offset: +0x4C</summary>
        private float m_TimeThreshold;

        /// <summary>Agent flags. Offset: +0x50</summary>
        private byte m_Flags;

        /// <summary>Post-process flags (bit 0 = neighbor comparison). Offset: +0x54</summary>
        private byte m_PostProcessFlags;

        /// <summary>Evaluated tile positions. Offset: +0x58</summary>
        private Dictionary<Tile, TileScore> m_Tiles;

        /// <summary>Secondary tile dictionary. Offset: +0x60</summary>
        private Dictionary<Tile, TileScore> m_TilesSecondary;

        /// <summary>Parallel evaluation tasks. Offset: +0x68</summary>
        private List<Task> m_Tasks;

        /// <summary>Debug string for dev tools. Offset: +0x70</summary>
        private string m_QueuedDebugString;

        /// <summary>Skip evaluation flag. Offset: +0x78</summary>
        private bool m_SkipEvaluation;

        /// <summary>Static list of criterions. Shared by all agents.</summary>
        private static List<Criterion> S_CRITERIONS;

        // =====================================================================
        // CONSTANTS
        // =====================================================================

        private const int MAX_ITERATIONS = 16;
        private const int MIN_TILES_PER_THREAD = 2;

        // =====================================================================
        // MAIN EVALUATION LOOP
        // =====================================================================

        /// <summary>
        /// Main AI evaluation entry point. Called each AI turn.
        ///
        /// Address: 0x18070eb30
        ///
        /// Flow:
        /// 1. Validate actor state (alive, not busy)
        /// 2. Collect candidate tiles
        /// 3. Run criterions on tiles (parallelized)
        /// 4. Collect behaviors
        /// 5. Evaluate behaviors
        /// 6. Post-process tile scores
        /// 7. Score and sort behaviors
        /// 8. Pick best behavior
        /// </summary>
        public void Evaluate()
        {
            // Reset state
            m_SelectedScore = 0;
            m_SelectedBehavior = null;
            m_State = AgentState.None;

            // Clear tile evaluations
            m_Tiles.Clear();
            m_TilesSecondary = m_Tiles;

            // Validate actor
            if (m_Actor == null || !m_Actor.IsAlive || m_Actor.IsTurnDone)
                return;

            // Check for skip flag (used for debugging)
            if (m_SkipEvaluation)
            {
                Debug.LogWarning($"AI: Skipping evaluation for {m_Actor.Id}");
                m_Actor.SetTurnDone(true);
                return;
            }

            // Wait for previous actions to complete
            while (m_Faction != null)
            {
                if (Time.time >= m_TimeThreshold)
                {
                    var skillContainer = m_Actor.GetSkillContainer();
                    if (skillContainer?.IsBusy() != true)
                    {
                        // Check if in middle of tactical state
                        if (TacticalManager.Instance?.CurrentStateIndex == 0 ||
                            !m_Actor.IsActionPointsSpent())
                        {
                            break;  // Ready to evaluate
                        }
                    }
                }
                Thread.Sleep(1);
            }

            // Increment iteration counter
            m_IterationCount++;
            if (m_IterationCount > MAX_ITERATIONS)
            {
                Debug.LogWarning("AI: Max iterations exceeded");
                m_Actor.SetTurnDone(true);
                return;
            }

            // Phase 1: Evaluate tiles with criterions
            m_State = AgentState.EvaluatingTiles;
            EvaluateTilesWithCriterions();

            // Phase 2: Collect behaviors
            CollectBehaviors();

            // Phase 3: Evaluate tiles for each criterion (second pass)
            EvaluateTilesSecondPass();

            // Post-process tile scores
            PostProcessTileScores();

            // Run criterion post-processing
            foreach (var criterion in S_CRITERIONS)
            {
                if (criterion.IsValid(m_Actor))
                {
                    criterion.PostProcess(m_Actor, m_Tiles);
                }
            }

            // Phase 4: Evaluate behaviors
            m_State = AgentState.EvaluatingBehaviors;
            EvaluateBehaviors();

            // Sort behaviors by score
            m_Behaviors.Sort(SortBehaviors);

            // Pick best behavior
            m_SelectedBehavior = PickBehavior();

            if (m_SelectedBehavior != null)
            {
                float scoreMult = GetScoreMultForPickingThisAgent();
                m_SelectedScore = Math.Max(1, (int)(m_SelectedBehavior.Score * scoreMult));
                m_State = AgentState.ReadyToExecute;
            }
        }

        // =====================================================================
        // TILE EVALUATION
        // =====================================================================

        private void EvaluateTilesWithCriterions()
        {
            foreach (var criterion in S_CRITERIONS)
            {
                if (!criterion.IsValid(m_Actor))
                    continue;

                // Collect data for this criterion
                criterion.Collect(m_Actor, m_Tiles);
            }
        }

        private void EvaluateTilesSecondPass()
        {
            int tileCount = m_Tiles.Count;

            foreach (var criterion in S_CRITERIONS)
            {
                if (!criterion.IsValid(m_Actor))
                    continue;

                int threadCount = criterion.GetThreads();

                // Parallelize if beneficial
                if (threadCount > 1 && threadCount * MIN_TILES_PER_THREAD < tileCount)
                {
                    // Parallel evaluation
                    int tilesPerThread = tileCount / threadCount;
                    for (int i = 0; i < threadCount - 1; i++)
                    {
                        var task = ScheduleCriterionEvaluation(i, tilesPerThread, criterion);
                        m_Tasks.Add(task);
                    }

                    // Evaluate remaining tiles on main thread
                    int startIdx = (threadCount - 1) * tilesPerThread;
                    for (int i = startIdx; i < tileCount; i++)
                    {
                        var tile = GetTileAt(i);
                        criterion.Evaluate(m_Actor, tile.Value);
                    }

                    // Wait for parallel tasks
                    WaitForTasks();
                }
                else
                {
                    // Sequential evaluation
                    foreach (var kvp in m_Tiles)
                    {
                        criterion.Evaluate(m_Actor, kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Post-processes tile scores after all criterions have run.
        ///
        /// Address: 0x180711720
        ///
        /// IMPORTANT: This runs in parallel for multiple agents.
        /// Do not modify shared state here!
        ///
        /// Algorithm:
        /// 1. For each tile, add UtilityModifier (+0x38) to FinalScore (+0x30)
        /// 2. Apply AIConfig power scaling with config offset +0x20
        /// 3. Apply role-based scaling using role weights at +0x14 and config +0x24/+0x28
        /// 4. Multiply by config +0x2c
        /// 5. Calculate SafetyScore (+0x28) using config +0x30 power
        /// 6. Apply safety scaling with role +0x1c, config +0x34/+0x38, multiply by config +0x3c
        /// 7. If m_PostProcessFlags bit 0 is set, perform neighbor comparison pass
        /// </summary>
        public void PostProcessTileScores()
        {
            var actor = m_Actor;
            if (actor == null) return;

            // Get role weights from actor via vtable +0x398, then access +0x310
            var role = actor.GetEntityTemplate()?.AIRole;
            if (role == null) return;
            var roleWeights = role.Weights;  // Offset +0x310 from role data

            foreach (var kvp in m_Tiles)
            {
                var tile = kvp.Key;
                var score = kvp.Value;

                // Skip if this is actor's current tile (vtable +0x388)
                if (tile == actor.GetCurrentTile()) continue;

                // Step 1: Add utility modifier to final score
                // *(float*)(score + 0x30) += *(float*)(score + 0x38)
                score.FinalScore += score.UtilityModifier;

                // Step 2: Apply AIConfig power scaling
                // FUN_1804bbda0 is Mathf.Pow
                // Uses AIConfig singleton at TypeInfo+0xb8+8, parameter at +0x20
                var config = AIConfig.Instance.Weights;
                score.FinalScore = Mathf.Pow(score.FinalScore, config.FinalScorePow);  // +0x20

                // Step 3: Apply role-based position scaling
                // score.FinalScore * roleWeights.PositionWeight * config.PositionMult
                // Then apply power from config +0x28, multiply by config +0x2c
                float scaledScore = score.FinalScore * roleWeights.PositionWeight * config.PositionMult;
                score.FinalScore = Mathf.Pow(scaledScore, config.PositionPow) * config.PositionScale;

                // Step 4: Calculate SafetyScore
                // Uses config +0x30 for power
                score.SafetyScore = Mathf.Pow(score.SafetyScore, config.SafetyPow);

                // Step 5: Apply safety scaling with role weight and config
                // roleWeights.SafetyWeight (+0x1c) * config.SafetyMult (+0x34)
                // Then pow with config +0x38, XOR sign, multiply by config +0x3c
                float safetyScaled = score.SafetyScore * roleWeights.SafetyWeight * config.SafetyMult;
                score.SafetyScore = -Mathf.Pow(safetyScaled, config.SafetyScalePow) * config.SafetyScale;
            }

            // Second pass: neighbor comparison (if m_PostProcessFlags bit 0 is set)
            // Checks *(byte*)(param_1 + 0x54) & 1
            if ((m_PostProcessFlags & 0x01) != 0)
            {
                // Get actor move range via vtable +0x458
                int actorMoveRange = m_Actor.GetMoveRange();
                if (actorMoveRange > 0)
                {
                    // Constants from binary: DAT_182d8fc2c = 1.2f, DAT_182d8fbfc = 1.5f
                    const float THRESHOLD_POSITIVE = 1.2f;
                    const float THRESHOLD_NEGATIVE = 1.5f;

                    foreach (var kvp in m_Tiles)
                    {
                        var score = kvp.Value;
                        var originalTile = score.Tile;  // +0x10
                        TileScore bestSafetyNeighbor = score;
                        TileScore bestFinalNeighbor = score;

                        // Check all 8 neighbors
                        for (int dir = 0; dir < 8; dir++)
                        {
                            // Tile.GetNextTile at +0x10 of score
                            if (score.Tile.GetNextTile(dir, out Tile neighbor))
                            {
                                if (m_Tiles.TryGetValue(neighbor, out TileScore neighborScore))
                                {
                                    // Compare safety scores - strictly greater, not equal
                                    if (score.SafetyScore <= neighborScore.SafetyScore &&
                                        neighborScore.SafetyScore != score.SafetyScore)
                                    {
                                        bestSafetyNeighbor = neighborScore;
                                    }

                                    // Compare final scores - strictly greater, not equal
                                    if (score.FinalScore <= neighborScore.FinalScore &&
                                        neighborScore.FinalScore != score.FinalScore)
                                    {
                                        bestFinalNeighbor = neighborScore;
                                    }
                                }
                            }
                        }

                        // Update BetterSafetyTile (+0x50) if better neighbor found
                        if (bestSafetyNeighbor.Tile != originalTile)
                        {
                            // Threshold depends on sign of current score
                            float threshold = (score.SafetyScore <= 0.0f && score.SafetyScore != 0.0f)
                                ? THRESHOLD_NEGATIVE : THRESHOLD_POSITIVE;

                            if (threshold * score.SafetyScore <= bestSafetyNeighbor.SafetyScore)
                            {
                                score.BetterSafetyTile = bestSafetyNeighbor;  // +0x50
                            }
                        }

                        // Update BetterFinalTile (+0x58) if better neighbor found
                        if (bestFinalNeighbor.Tile != originalTile)
                        {
                            float threshold = (score.FinalScore <= 0.0f && score.FinalScore != 0.0f)
                                ? THRESHOLD_NEGATIVE : THRESHOLD_POSITIVE;

                            if (threshold * score.FinalScore <= bestFinalNeighbor.FinalScore)
                            {
                                score.BetterFinalTile = bestFinalNeighbor;  // +0x58
                            }
                        }
                    }
                }
            }
        }

        // =====================================================================
        // BEHAVIOR EVALUATION
        // =====================================================================

        private void CollectBehaviors()
        {
            // Sort behaviors by type first
            m_Behaviors.Sort(SortBehaviorsByType);

            int? lastAttackType = null;

            foreach (var behavior in m_Behaviors)
            {
                // Skip duplicate attack behaviors with same skill type
                if (behavior is Attack attack)
                {
                    if (lastAttackType == attack.SkillType)
                        continue;
                    lastAttackType = attack.SkillType;
                }

                behavior.Collect(m_Actor);
            }
        }

        private void EvaluateBehaviors()
        {
            int? lastAttackType = null;

            foreach (var behavior in m_Behaviors)
            {
                behavior.Reset();

                if (behavior is Attack attack)
                {
                    if (lastAttackType == attack.SkillType)
                        continue;
                    lastAttackType = attack.SkillType;
                }

                if (behavior.Evaluate(m_Actor, m_Tiles) && behavior.Score > 0)
                {
                    // Behavior is valid and has positive score
                }
            }
        }

        /// <summary>
        /// Picks a behavior using weighted random selection.
        ///
        /// Address: 0x180710ff0
        ///
        /// IMPORTANT: This is NOT simple max-score selection!
        /// The algorithm uses weighted random selection based on behavior scores.
        ///
        /// Algorithm:
        /// 1. Get first behavior's score (behaviors are pre-sorted by score descending)
        /// 2. Calculate minimum threshold: (int)(Pow(firstScore, config.ScorePow) * DAT_182d8fd40)
        /// 3. If threshold less than 1, set to 1
        /// 4. Sum scores of all behaviors meeting threshold (using Pow with config.ScorePow)
        /// 5. Generate random number in range [1, totalWeight]
        /// 6. Iterate through behaviors, subtracting weighted scores until roll reaches 0
        /// 7. Return the behavior where roll becomes &lt;= weighted score
        ///
        /// This allows lower-scored behaviors to occasionally be selected,
        /// introducing unpredictability in AI behavior.
        /// </summary>
        private Behavior PickBehavior()
        {
            var behaviors = m_Behaviors;  // +0x20
            if (behaviors == null || behaviors.Count == 0)
                return null;

            int behaviorCount = behaviors.Count;
            var firstBehavior = behaviors[0];
            if (firstBehavior == null)
                return null;

            // Get AIConfig singleton and calculate threshold
            var config = AIConfig.Instance;
            if (config?.Weights == null)
                return null;

            // Calculate minimum score threshold using power function
            // FUN_1804bbda0 is Mathf.Pow
            float poweredScore = Mathf.Pow(firstBehavior.Score, config.Weights.ScorePow);
            int minScore = (int)(poweredScore * config.Weights.ScoreMult);  // DAT_182d8fd40
            if (minScore < 1)
                minScore = 1;

            // Debug logging loop (if debug enabled and in tactical combat, not deployment)
            bool debugEnabled = LogLevel.Debug && TacticalManager.Instance?.CurrentStateIndex != 0;
            string debugString = "";
            if (debugEnabled)
            {
                // Build debug string listing all behaviors and scores
                for (int i = 0; i < behaviorCount; i++)
                {
                    var b = behaviors[i];
                    float bScore = Mathf.Pow(b.Score, config.Weights.ScorePow);
                    debugString += b.GetName() + ": " + b.Score.ToString();
                    if (bScore >= minScore)
                        debugString += "*";  // Mark behaviors meeting threshold
                }
            }

            // Sum weighted scores of behaviors meeting threshold
            int totalWeight = 0;
            for (int i = 0; i < behaviorCount; i++)
            {
                float behaviorScore = Mathf.Pow(behaviors[i].Score, config.Weights.ScorePow);
                if ((int)behaviorScore < minScore)
                    break;  // Behaviors are sorted, so stop early
                totalWeight += (int)behaviorScore;
            }

            // Random selection based on weights
            if (m_Random == null)
                return null;

            // PseudoRandom.Range(1, totalWeight) - inclusive range
            int roll = m_Random.Range(1, totalWeight);

            // Iterate through behaviors, selecting based on weighted random
            for (int i = 0; i < behaviorCount; i++)
            {
                float behaviorScore = Mathf.Pow(behaviors[i].Score, config.Weights.ScorePow);

                if (roll <= (int)behaviorScore)
                {
                    // This behavior is selected

                    // Debug logging if enabled and not deployment phase
                    if (debugEnabled && !Criterion.IsDeploymentPhase())
                    {
                        // Build selection log string
                        m_QueuedDebugString = $"AI: {debugString} -> Picked {behaviors[i].GetName()}: {behaviors[i].Score})";

                        // If this actor is the selected unit, log immediately
                        var selectedUnit = TacticalManager.Instance?.SelectedUnit;
                        if (selectedUnit == m_Actor)
                        {
                            Debug.Log(m_QueuedDebugString);
                            m_QueuedDebugString = null;
                        }
                    }

                    return behaviors[i];
                }

                // Subtract this behavior's weight and continue
                roll -= (int)behaviorScore;
            }

            return null;
        }

        /// <summary>
        /// Gets score multiplier for faction-level agent selection.
        ///
        /// Address: 0x180710120
        ///
        /// This complex method evaluates multiple factors to determine
        /// how likely this agent should be selected for the next action.
        ///
        /// Algorithm:
        /// 1. Return early (1.0f) if this actor is the player-selected unit
        /// 2. If no behavior selected, call GetOpportunityLevel()
        /// 3. Always call GetThreatLevel()
        /// 4. Check actor can act state (vtable call)
        /// 5. Check actor's target entity at +0x70
        /// 6. Check if actor can move (vtable +0x408)
        /// 7. Check Move behavior completion state at +0x94
        /// 8. Call GetHitpointsPct()
        /// 9. Get action points via EntityProperties (vtable +0x3d8)
        /// 10. Check IsHiddenToPlayer()
        /// 11. If ammo count (vtable +0x468) &gt; 1 and not suppressed (+0xec &amp; 1 == 0):
        ///     - Call additional method via vtable +0x478
        /// 12. Apply AIConfig power function with offset +0x50
        /// </summary>
        public float GetScoreMultForPickingThisAgent()
        {
            // Check TacticalManager singleton
            var tacticalMgr = TacticalManager.Instance;
            if (tacticalMgr == null)
                return 1.0f;

            // Skip calculation if this is the player-selected unit (+0x50 offset from TacticalManager)
            if (tacticalMgr.SelectedUnit == m_Actor)
                return 1.0f;

            // Evaluate opportunity if no behavior selected
            if (m_SelectedBehavior == null)
            {
                GetOpportunityLevel();
            }

            // Always evaluate threat level
            GetThreatLevel();

            if (m_Actor == null)
                return 1.0f;

            // Complex state evaluation chain
            bool canAct = m_Actor.HasStylesheetPaths();  // Abstracted vtable call
            if (canAct)
            {
                // Continue with full evaluation
            }
            else
            {
                // Check actor's target entity at +0x70
                var target = m_Actor.TargetEntity;
                if (target != null)
                {
                    // Check if target can attack (vtable +0x408)
                    bool targetCanAttack = target.CanAttack();
                    if (targetCanAttack)
                    {
                        // Check Move behavior completion
                        var moveBehavior = GetBehavior<Move>(4);
                        if (moveBehavior != null && !moveBehavior.IsComplete)  // +0x94
                        {
                            // Continue evaluation
                        }
                    }

                    // Check if target can convert to Actor
                    if (target.ToActor() == null)
                    {
                        return 1.0f;
                    }
                }
                else
                {
                    return 1.0f;
                }
            }

            // Health evaluation
            float hpPct = m_Actor.GetHitpointsPct();

            // Get entity properties and action points
            m_Actor.GetMoveRange();  // vtable +0x458
            var properties = m_Actor.GetEntityProperties();  // vtable +0x3d8
            if (properties == null)
                return 1.0f;

            int actionPoints = properties.GetActionPoints();

            // Hidden state check
            bool isHidden = m_Actor.IsHiddenToPlayer();

            // Ammo consideration (vtable +0x468)
            int ammoCount = m_Actor.GetAmmoCount();
            if (ammoCount > 1)
            {
                // Check suppression state: (byte at EntityProperties +0xec & 1) == 0
                if (!properties.IsSuppressed)
                {
                    // Call additional evaluation method (vtable +0x478)
                    m_Actor.AdditionalAmmoCheck();
                }
            }

            // Final calculation using AIConfig power function
            // AIConfig singleton at TypeInfo+0xb8+8, parameter at +0x50
            var config = AIConfig.Instance.Weights;
            float calculatedScore = CalculateFinalAgentScore(hpPct, actionPoints, isHidden, ammoCount);
            return Mathf.Pow(calculatedScore, config.AgentPickPow);  // +0x50
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        public Actor GetActor() => m_Actor;
        public RoleData GetRole() => m_Actor?.EntityTemplate?.AIRole;

        /// <summary>
        /// Gets a behavior by type index.
        /// </summary>
        private T GetBehavior<T>(int typeIndex) where T : Behavior
        {
            // Implementation searches m_Behaviors for matching type
            return null;
        }

        /// <summary>
        /// Evaluates opportunity level for agent prioritization.
        /// Called when no behavior is currently selected.
        /// </summary>
        private void GetOpportunityLevel()
        {
            // Implementation evaluates attack opportunities
        }

        /// <summary>
        /// Evaluates threat level for agent prioritization.
        /// Always called during agent scoring.
        /// </summary>
        private void GetThreatLevel()
        {
            // Implementation evaluates threats to this agent
        }

        /// <summary>
        /// Calculates final agent score from multiple factors.
        /// </summary>
        private float CalculateFinalAgentScore(float hpPct, int actionPoints, bool isHidden, int ammoCount)
        {
            // Implementation combines all factors into final score
            float score = 1.0f;
            score *= hpPct;
            score *= actionPoints / 100.0f;
            if (isHidden) score *= 1.2f;
            if (ammoCount > 0) score *= 1.1f;
            return score;
        }

        private int SortBehaviors(Behavior a, Behavior b)
        {
            return b.Score.CompareTo(a.Score);
        }

        private int SortBehaviorsByType(Behavior a, Behavior b)
        {
            return a.GetType().Name.CompareTo(b.GetType().Name);
        }

        private Task ScheduleCriterionEvaluation(int index, int count, Criterion criterion)
        {
            return Task.Run(() =>
            {
                int start = index * count;
                int end = start + count;
                for (int i = start; i < end; i++)
                {
                    var tile = GetTileAt(i);
                    criterion.Evaluate(m_Actor, tile.Value);
                }
            });
        }

        private void WaitForTasks()
        {
            foreach (var task in m_Tasks)
            {
                while (!task.IsCompleted)
                    Thread.Sleep(1);
            }
            m_Tasks.Clear();
        }

        private KeyValuePair<Tile, TileScore> GetTileAt(int index)
        {
            // Use LINQ ElementAt - inefficient but matches game
            return m_Tiles.ElementAt(index);
        }

        private float ApplyGlobalScaling(float score)
        {
            var weights = AIWeightsTemplate.Instance;
            float scaled = (float)Math.Pow(score, weights.BehaviorScorePOW);
            return scaled;
        }
    }

    // =========================================================================
    // SUPPORTING TYPES
    // =========================================================================

    /// <summary>
    /// TileScore structure storing evaluation data for a tile.
    ///
    /// Key offsets:
    ///   +0x10: Tile reference
    ///   +0x28: SafetyScore (float)
    ///   +0x30: FinalScore (float)
    ///   +0x38: UtilityModifier (float) - added to FinalScore in post-processing
    ///   +0x50: BetterSafetyTile (TileScore reference)
    ///   +0x58: BetterFinalTile (TileScore reference)
    /// </summary>
    public class TileScore
    {
        /// <summary>Reference to the tile. Offset: +0x10</summary>
        public Tile Tile;

        /// <summary>Safety evaluation score. Offset: +0x28</summary>
        public float SafetyScore;

        /// <summary>Final combined score. Offset: +0x30</summary>
        public float FinalScore;

        /// <summary>Utility modifier added during post-processing. Offset: +0x38</summary>
        public float UtilityModifier;

        /// <summary>Reference to neighbor with better safety. Offset: +0x50</summary>
        public TileScore BetterSafetyTile;

        /// <summary>Reference to neighbor with better final score. Offset: +0x58</summary>
        public TileScore BetterFinalTile;
    }

    /// <summary>
    /// AIConfig weights structure with scoring parameters.
    ///
    /// Key offsets (from Weights field):
    ///   +0x20: FinalScorePow (float) - power applied to final tile score
    ///   +0x24: PositionMult (float) - position weight multiplier
    ///   +0x28: PositionPow (float) - power applied after position scaling
    ///   +0x2c: PositionScale (float) - final position scale multiplier
    ///   +0x30: SafetyPow (float) - power applied to safety score
    ///   +0x34: SafetyMult (float) - safety weight multiplier
    ///   +0x38: SafetyScalePow (float) - power applied after safety scaling
    ///   +0x3c: SafetyScale (float) - final safety scale multiplier (negated)
    ///   +0x50: AgentPickPow (float) - power for agent selection scoring
    ///
    /// Behavior selection:
    ///   ScorePow: power applied to behavior scores
    ///   ScoreMult: multiplier for minimum score threshold (DAT_182d8fd40)
    /// </summary>
    public class AIConfigWeights
    {
        public float FinalScorePow;     // +0x20
        public float PositionMult;      // +0x24
        public float PositionPow;       // +0x28
        public float PositionScale;     // +0x2c
        public float SafetyPow;         // +0x30
        public float SafetyMult;        // +0x34
        public float SafetyScalePow;    // +0x38
        public float SafetyScale;       // +0x3c
        public float AgentPickPow;      // +0x50
        public float ScorePow;          // Used in PickBehavior
        public float ScoreMult;         // DAT_182d8fd40
    }
}
