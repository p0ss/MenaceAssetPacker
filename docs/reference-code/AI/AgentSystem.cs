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
    ///   +0x48: int evaluation iteration count
    ///   +0x58: Dictionary&lt;Tile, TileScore&gt; evaluated tiles
    ///   +0x68: List&lt;Task&gt; parallel evaluation tasks
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

        /// <summary>Evaluation iteration counter (max 16). Offset: +0x48</summary>
        private int m_IterationCount;

        /// <summary>Time threshold for evaluation. Offset: +0x4C</summary>
        private float m_TimeThreshold;

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
        /// </summary>
        public void PostProcessTileScores()
        {
            // Apply role-based scaling to tile scores
            var role = GetRole();

            foreach (var kvp in m_Tiles)
            {
                var score = kvp.Value;

                // Combine scores with role weights
                score.FinalScore =
                    score.UtilityScore * role.UtilityScale +
                    score.SafetyScore * role.SafetyScale -
                    score.DistanceScore * role.DistanceScale;

                // Apply global scaling from AIWeightsTemplate
                score.FinalScore = ApplyGlobalScaling(score.FinalScore);
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
        /// Picks the best behavior to execute.
        ///
        /// Address: 0x180710e40 (estimated)
        /// </summary>
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

        /// <summary>
        /// Gets score multiplier for faction-level agent selection.
        ///
        /// Address: 0x1807114c0
        ///
        /// IMPORTANT: Called in parallel for multiple agents.
        /// Must be thread-safe!
        /// </summary>
        public float GetScoreMultForPickingThisAgent()
        {
            // Base multiplier
            float mult = 1.0f;

            // Prefer agents that haven't acted
            if (!m_Actor.HasActedThisTurn)
                mult *= 1.2f;

            // Prefer agents with more AP
            float apRatio = m_Actor.ActionPoints / (float)m_Actor.MaxActionPoints;
            mult *= 0.8f + (apRatio * 0.4f);

            return mult;
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        public Actor GetActor() => m_Actor;
        public RoleData GetRole() => m_Actor?.EntityTemplate?.AIRole;

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
}
