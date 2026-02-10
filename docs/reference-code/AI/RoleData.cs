// =============================================================================
// MENACE REFERENCE CODE - AI Role Data
// =============================================================================
// Per-unit AI configuration that controls behavior weights and preferences.
// Defined on EntityTemplate and determines how units prioritize actions.
// =============================================================================

using System;

namespace Menace.Tactical.AI
{
    /// <summary>
    /// Tile effect types that AI can ignore.
    /// </summary>
    [Flags]
    public enum TileEffectType
    {
        None = 0,
        Fire = 1,
        Smoke = 2,
        Poison = 4,
        // Additional types...
    }

    /// <summary>
    /// Per-unit AI configuration. Assigned via EntityTemplate.AIRole (+0x310).
    ///
    /// Controls:
    /// - How the AI values different criteria (utility vs safety)
    /// - Which behaviors are preferred (damage vs suppression)
    /// - Special behavior flags (evade, stay hidden, peek cover)
    ///
    /// Instance size: ~0x48 bytes
    /// </summary>
    [Serializable]
    public class RoleData
    {
        // =====================================================================
        // FRIENDLY FIRE AVOIDANCE
        // =====================================================================

        /// <summary>
        /// How much OTHER units avoid hitting this unit.
        /// Higher = more protected from friendly fire.
        /// Range: 0-10
        /// Offset: +0x10
        /// </summary>
        [Range(0, 10)]
        public float TargetFriendlyFireValueMult = 1.0f;

        // =====================================================================
        // CRITERION WEIGHTS
        // =====================================================================

        /// <summary>
        /// Preference for high-value actions.
        /// Higher = aggressive, seeks damage opportunities.
        /// Range: 0-50
        /// Offset: +0x14
        /// </summary>
        [Range(0, 50)]
        public float UtilityScale = 15f;

        /// <summary>
        /// Minimum usefulness requirement.
        /// Higher = won't move to useless positions.
        /// Range: 0-2
        /// Offset: +0x18
        /// </summary>
        [Range(0, 2)]
        public float UtilityThresholdScale = 1f;

        /// <summary>
        /// Preference for safety.
        /// Higher = defensive, seeks cover over offense.
        /// Range: 0-50
        /// Offset: +0x1C
        /// </summary>
        [Range(0, 50)]
        public float SafetyScale = 15f;

        /// <summary>
        /// Preference to stay near current position.
        /// Higher = doesn't roam far.
        /// Range: 0-50
        /// Offset: +0x20
        /// </summary>
        [Range(0, 50)]
        public float DistanceScale = 10f;

        /// <summary>
        /// How much to avoid friendly fire.
        /// Higher = more careful when shooting near allies.
        /// Range: 0-50
        /// Offset: +0x24
        /// </summary>
        [Range(0, 50)]
        public float FriendlyFirePenalty = 20f;

        // =====================================================================
        // BEHAVIORAL FLAGS
        // =====================================================================

        /// <summary>
        /// Can retreat/disengage from fights.
        /// Offset: +0x28
        /// </summary>
        public bool IsAllowedToEvadeEnemies = true;

        /// <summary>
        /// Tries to remain in fog of war.
        /// Offset: +0x29
        /// </summary>
        public bool AttemptToStayOutOfSight = false;

        /// <summary>
        /// Will leave cover to attack, then return.
        /// Good for aggressive units with high mobility.
        /// Offset: +0x2A
        /// </summary>
        public bool PeekInAndOutOfCover = false;

        /// <summary>
        /// Uses AoE skills on single targets.
        /// Enable for units without single-target options.
        /// Offset: +0x2B
        /// </summary>
        public bool UseAoeAgainstSingleTargets = false;

        // =====================================================================
        // BEHAVIOR WEIGHTS
        // =====================================================================

        /// <summary>
        /// Weight for movement behaviors.
        /// Offset: +0x2C
        /// </summary>
        [Range(0, 10)]
        public float Move = 1f;

        /// <summary>
        /// Weight for damage-dealing behaviors.
        /// Offset: +0x30
        /// </summary>
        [Range(0, 10)]
        public float InflictDamage = 1f;

        /// <summary>
        /// Weight for suppression behaviors.
        /// Offset: +0x34
        /// </summary>
        [Range(0, 10)]
        public float InflictSuppression = 0.5f;

        /// <summary>
        /// Weight for stun behaviors.
        /// Offset: +0x38
        /// </summary>
        [Range(0, 10)]
        public float Stun = 0.5f;

        // =====================================================================
        // CRITERION TOGGLES
        // =====================================================================

        /// <summary>
        /// Run from enemies (for civilians).
        /// Offset: +0x3C
        /// </summary>
        public bool AvoidOpponents = false;

        /// <summary>
        /// Evaluate reachable tiles (vs just current position).
        /// Offset: +0x3D
        /// </summary>
        public bool ConsiderSurroundings = true;

        /// <summary>
        /// Seek cover relative to enemy positions.
        /// Offset: +0x3E
        /// </summary>
        public bool CoverAgainstOpponents = true;

        /// <summary>
        /// Prefer tiles closer to current position.
        /// Offset: +0x3F
        /// </summary>
        public bool DistanceToCurrentTile = true;

        /// <summary>
        /// Respect mission objective zones.
        /// Offset: +0x40
        /// </summary>
        public bool ConsiderZones = true;

        /// <summary>
        /// Avoid dangerous positions.
        /// Offset: +0x41
        /// </summary>
        public bool ThreatFromOpponents = true;

        /// <summary>
        /// React to fire, smoke, supply tiles.
        /// Offset: +0x42
        /// </summary>
        public bool ExistingTileEffects = true;

        /// <summary>
        /// Tile effects to ignore when evaluating positions.
        /// Offset: +0x44
        /// </summary>
        public TileEffectType IgnoreTileEffects = TileEffectType.None;
    }

    /// <summary>
    /// Common role archetypes with example configurations.
    /// </summary>
    public static class RoleArchetypes
    {
        /// <summary>
        /// Aggressive front-line fighter.
        /// High damage priority, low safety concern.
        /// </summary>
        public static RoleData Assault => new RoleData
        {
            UtilityScale = 30f,
            SafetyScale = 10f,
            DistanceScale = 5f,
            InflictDamage = 2f,
            PeekInAndOutOfCover = true
        };

        /// <summary>
        /// Suppression specialist.
        /// Focuses on pinning enemies.
        /// </summary>
        public static RoleData Support => new RoleData
        {
            UtilityScale = 15f,
            SafetyScale = 20f,
            InflictSuppression = 2f,
            InflictDamage = 0.5f
        };

        /// <summary>
        /// Long-range precision unit.
        /// High safety, prefers distance.
        /// </summary>
        public static RoleData Sniper => new RoleData
        {
            UtilityScale = 25f,
            SafetyScale = 25f,
            DistanceScale = 5f,  // Low = willing to reposition
            AttemptToStayOutOfSight = true
        };

        /// <summary>
        /// Heavy armor unit.
        /// Low safety concern (armored), protects allies.
        /// </summary>
        public static RoleData Tank => new RoleData
        {
            UtilityScale = 10f,
            SafetyScale = 5f,
            FriendlyFirePenalty = 30f,  // Very careful about allies
            TargetFriendlyFireValueMult = 0.5f  // Can be hit by friendly fire
        };

        /// <summary>
        /// Non-combatant that flees danger.
        /// </summary>
        public static RoleData Civilian => new RoleData
        {
            UtilityScale = 0f,
            SafetyScale = 50f,
            AvoidOpponents = true,
            ThreatFromOpponents = true,
            InflictDamage = 0f,
            Move = 3f  // High movement priority for fleeing
        };
    }
}
