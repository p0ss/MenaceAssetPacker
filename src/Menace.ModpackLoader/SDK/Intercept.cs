using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

// ═══════════════════════════════════════════════════════════════════════════════
//  DELEGATE TYPES - Define signatures for each interceptor
// ═══════════════════════════════════════════════════════════════════════════════

#region Delegate Types

/// <summary>
/// Interceptor for float properties (Damage, Accuracy base values).
/// </summary>
/// <param name="props">The EntityProperties instance being queried</param>
/// <param name="owner">The Entity that owns these properties (may be null)</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void FloatIntercept(GameObj props, GameObj owner, ref float result);

/// <summary>
/// Interceptor for integer properties (Armor, Concealment, Detection, Vision).
/// </summary>
/// <param name="props">The EntityProperties instance being queried</param>
/// <param name="owner">The Entity that owns these properties (may be null)</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void IntIntercept(GameObj props, GameObj owner, ref int result);

/// <summary>
/// Result structure for hit chance calculations.
/// Contains all components that make up the final hit chance.
/// </summary>
public struct HitChanceResult
{
    /// <summary>Final computed hit chance (0.0 to 1.0, or 0 to 100 depending on game version)</summary>
    public float FinalChance;
    /// <summary>Base accuracy from attacker's EntityProperties</summary>
    public float BaseAccuracy;
    /// <summary>Cover multiplier applied to target (1.0 = no cover)</summary>
    public float CoverMult;
    /// <summary>Evasion/defense multiplier from target</summary>
    public float DefenseMult;
    /// <summary>Distance penalty applied</summary>
    public float DistancePenalty;
    /// <summary>Whether distance falloff was applied</summary>
    public bool HasDistanceFalloff;
    /// <summary>Whether target is guaranteed hit (e.g., AlwaysHit skill flag)</summary>
    public bool IsGuaranteedHit;
}

/// <summary>
/// Interceptor for skill hit chance calculations.
/// </summary>
/// <param name="skill">The skill being used</param>
/// <param name="attacker">The attacking entity</param>
/// <param name="target">The target entity (may be null for tile-targeted skills)</param>
/// <param name="result">The hit chance result - modify components to change calculation</param>
public delegate void HitChanceInterceptor(GameObj skill, GameObj attacker, GameObj target, ref HitChanceResult result);

/// <summary>
/// Result structure for expected damage calculations.
/// </summary>
public struct ExpectedDamageResult
{
    /// <summary>Final expected damage value</summary>
    public float Damage;
    /// <summary>Base damage from weapon/skill</summary>
    public float BaseDamage;
    /// <summary>Damage multiplier from attacker properties</summary>
    public float DamageMult;
    /// <summary>Armor penetration value</summary>
    public float ArmorPenetration;
    /// <summary>Target's armor value</summary>
    public float TargetArmor;
}

/// <summary>
/// Interceptor for skill expected damage calculations.
/// </summary>
/// <param name="skill">The skill being used</param>
/// <param name="attacker">The attacking entity</param>
/// <param name="target">The target entity</param>
/// <param name="result">The expected damage result - modify to change calculation</param>
public delegate void ExpectedDamageInterceptor(GameObj skill, GameObj attacker, GameObj target, ref ExpectedDamageResult result);

/// <summary>
/// Interceptor for cover multiplier calculations.
/// </summary>
/// <param name="skill">The skill being used</param>
/// <param name="attacker">The attacking entity</param>
/// <param name="target">The target entity</param>
/// <param name="result">The cover multiplier (1.0 = no cover effect)</param>
public delegate void CoverMultInterceptor(GameObj skill, GameObj attacker, GameObj target, ref float result);

/// <summary>
/// Interceptor for line of sight checks.
/// </summary>
/// <param name="observer">The observing actor</param>
/// <param name="target">The target entity being observed</param>
/// <param name="result">Whether line of sight exists - set to false to block, true to grant</param>
public delegate void LineOfSightInterceptor(GameObj observer, GameObj target, ref bool result);

/// <summary>
/// Interceptor for boolean skill properties (IsMovementSkill, IsInRange, etc.).
/// </summary>
/// <param name="skill">The skill instance being queried</param>
/// <param name="result">The boolean result - modify via ref to change the value</param>
public delegate void BoolInterceptor(GameObj skill, ref bool result);

/// <summary>
/// Interceptor for float skill properties (GetExpectedSuppression).
/// </summary>
/// <param name="skill">The skill instance being queried</param>
/// <param name="result">The float result - modify via ref to change the value</param>
public delegate void FloatSkillInterceptor(GameObj skill, ref float result);

/// <summary>
/// Interceptor for integer skill properties (GetActionPointCost, range values).
/// </summary>
/// <param name="skill">The skill instance being queried</param>
/// <param name="result">The integer result - modify via ref to change the value</param>
public delegate void IntSkillInterceptor(GameObj skill, ref int result);

/// <summary>
/// Interceptor for skill usability checks.
/// Fires when checking if a skill/behavior is usable by an actor.
/// </summary>
/// <param name="skill">The skill being checked</param>
/// <param name="actor">The actor attempting to use the skill (may be null)</param>
/// <param name="result">Whether the skill is usable (can be modified)</param>
public delegate void SkillUsableInterceptor(GameObj skill, GameObj actor, ref bool result);

/// <summary>
/// Interceptor for Entity float state methods (health percentage, armor durability).
/// </summary>
/// <param name="entity">The Entity instance being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void EntityFloatStateInterceptor(GameObj entity, ref float result);

/// <summary>
/// Interceptor for Entity int/enum state methods (cover usage).
/// </summary>
/// <param name="entity">The Entity instance being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void EntityIntStateInterceptor(GameObj entity, ref int result);

/// <summary>
/// Interceptor for Entity bool state methods (discovery state).
/// </summary>
/// <param name="entity">The Entity instance being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void EntityBoolStateInterceptor(GameObj entity, ref bool result);

/// <summary>
/// Interceptor for Entity object state methods (Cover, Skill).
/// Returns IntPtr to allow modders to cast to appropriate type.
/// </summary>
/// <param name="entity">The Entity instance being queried</param>
/// <param name="result">The computed result pointer - modify via ref to change the value</param>
public delegate void EntityObjectStateInterceptor(GameObj entity, ref IntPtr result);

/// <summary>
/// Result structure for Vector2 values (scale range, etc.).
/// </summary>
public struct Vector2Result
{
    /// <summary>X component (min for range)</summary>
    public float X;
    /// <summary>Y component (max for range)</summary>
    public float Y;
}

/// <summary>
/// Interceptor for Entity Vector2 state methods (scale range).
/// </summary>
/// <param name="entity">The Entity instance being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void EntityVector2StateInterceptor(GameObj entity, ref Vector2Result result);

/// <summary>
/// Interceptor for generic property value getter (takes EntityPropertyType parameter).
/// </summary>
/// <param name="props">The EntityProperties instance being queried</param>
/// <param name="owner">The Entity that owns these properties (may be null)</param>
/// <param name="propertyType">The EntityPropertyType enum value being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void PropertyValueInterceptor(GameObj props, GameObj owner, int propertyType, ref float result);

// ═══════════════════════════════════════════════════════════════════════════════
//  TILE/MAP DELEGATE TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interceptor for tile-to-tile line of sight checks (Tile.HasLineOfSightTo).
/// </summary>
/// <param name="fromTile">The source tile</param>
/// <param name="toTile">The destination tile</param>
/// <param name="result">Whether LoS exists between tiles</param>
public delegate void TileLoSInterceptor(GameObj fromTile, GameObj toTile, ref bool result);

/// <summary>
/// Interceptor for tile LoS blocker checks (Tile.IsBlockingLineOfSight).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="result">Whether the tile blocks line of sight</param>
public delegate void TileBlockerInterceptor(GameObj tile, ref bool result);

/// <summary>
/// Interceptor for tile cover queries (Tile.GetCover).
/// </summary>
/// <param name="tile">The tile being queried</param>
/// <param name="direction">The direction (0-7) for cover check</param>
/// <param name="entity">The entity checking cover (may be null)</param>
/// <param name="result">The cover value (0-3)</param>
public delegate void TileCoverInterceptor(GameObj tile, int direction, GameObj entity, ref int result);

/// <summary>
/// Interceptor for tile cover mask queries (Tile.GetCoverMask).
/// </summary>
/// <param name="tile">The tile being queried</param>
/// <param name="result">Bitmask of cover directions (bits 0-3 = N/E/S/W)</param>
public delegate void TileCoverMaskInterceptor(GameObj tile, ref int result);

/// <summary>
/// Interceptor for entity-provided cover queries (Tile.GetEntityProvidedCover).
/// </summary>
/// <param name="tile">The tile being queried</param>
/// <param name="result">The cover value from entities on the tile</param>
public delegate void TileEntityCoverInterceptor(GameObj tile, ref int result);

/// <summary>
/// Interceptor for tile entry permission checks (Tile.CanBeEntered).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="result">Whether the tile can be entered</param>
public delegate void TileEntryInterceptor(GameObj tile, ref bool result);

/// <summary>
/// Interceptor for entity-specific tile entry checks (Tile.CanBeEnteredBy).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="entity">The entity attempting to enter</param>
/// <param name="result">Whether the entity can enter the tile</param>
public delegate void TileEntityEntryInterceptor(GameObj tile, GameObj entity, ref bool result);

/// <summary>
/// Interceptor for cover presence checks (BaseTile.HasCover).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="result">Whether the tile has any cover</param>
public delegate void BaseTileCoverCheckInterceptor(GameObj tile, ref bool result);

/// <summary>
/// Interceptor for half cover checks (BaseTile.HasHalfCover).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="result">Whether the tile has half cover</param>
public delegate void BaseTileHalfCoverInterceptor(GameObj tile, ref bool result);

/// <summary>
/// Interceptor for directional half cover checks (BaseTile.HasHalfCoverInDir).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="direction">The direction to check (0-7)</param>
/// <param name="result">Whether half cover exists in that direction</param>
public delegate void BaseTileDirHalfCoverInterceptor(GameObj tile, int direction, ref bool result);

/// <summary>
/// Interceptor for movement blocking checks (BaseTile.IsMovementBlocked).
/// </summary>
/// <param name="tile">The tile being checked</param>
/// <param name="direction">The direction to check (0-7)</param>
/// <param name="result">Whether movement is blocked in that direction</param>
public delegate void BaseTileMovementBlockedInterceptor(GameObj tile, int direction, ref bool result);

/// <summary>
/// Interceptor for pathfinding traversability checks (PathfindingProcess.IsTraversable).
/// Called extremely frequently during pathfinding - keep handlers fast!
/// </summary>
/// <param name="process">The pathfinding process performing the check</param>
/// <param name="tile">The tile being checked for traversability</param>
/// <param name="result">Whether the tile can be traversed (can be modified)</param>
public delegate void TraversableCheckInterceptor(GameObj process, GameObj tile, ref bool result);

/// <summary>
/// Interceptor for core LoS algorithm (LineOfSight.HasLineOfSight / RayTrace).
/// Static method - no instance parameter.
/// </summary>
/// <param name="fromTile">Source tile</param>
/// <param name="toTile">Target tile</param>
/// <param name="flags">LoS flags</param>
/// <param name="result">Whether LoS exists</param>
public delegate void LineOfSightRayTraceInterceptor(GameObj fromTile, GameObj toTile, int flags, ref bool result);

/// <summary>
/// Interceptor for corner detection (LineOfSight.IsNearTileCorner).
/// Static method - checks if position is near a tile corner.
/// </summary>
/// <param name="posX">X coordinate of position</param>
/// <param name="posY">Y coordinate of position</param>
/// <param name="result">Whether the position is near a corner</param>
public delegate void LineOfSightCornerInterceptor(float posX, float posY, ref bool result);

// -------------------------------------------------------------------------------
//  ACTOR STATE QUERY DELEGATES
// -------------------------------------------------------------------------------

/// <summary>
/// Interceptor for Actor integer state queries (morale state, tiles moved, etc.).
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void ActorIntStateInterceptor(GameObj actor, ref int result);

/// <summary>
/// Interceptor for Actor float state queries (morale percentage, suppression percentage).
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void ActorFloatStateInterceptor(GameObj actor, ref float result);

/// <summary>
/// Interceptor for Actor boolean state queries (IsActive, IsDying, IsStunned, etc.).
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void ActorBoolStateInterceptor(GameObj actor, ref bool result);

/// <summary>
/// Interceptor for Actor.IsDetectedByFaction which takes a faction parameter.
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="faction">The faction ID checking detection</param>
/// <param name="result">Whether the actor is detected by the faction</param>
public delegate void ActorFactionDetectionInterceptor(GameObj actor, int faction, ref bool result);

/// <summary>
/// Interceptor for Actor.GetTurningCost which takes a target direction parameter.
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="targetDirection">The target direction (0-7)</param>
/// <param name="result">The AP cost to turn to that direction</param>
public delegate void ActorTurningCostInterceptor(GameObj actor, int targetDirection, ref int result);

/// <summary>
/// Interceptor for Actor.GetSuppressionState which takes an additional suppression parameter.
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="additionalSuppression">Additional suppression to factor in</param>
/// <param name="result">The suppression state (0=Normal, 1=Suppressed, 2=PinnedDown)</param>
public delegate void ActorSuppressionStateInterceptor(GameObj actor, float additionalSuppression, ref int result);

/// <summary>
/// Interceptor for Actor.ApplySuppression which fires before suppression is applied to an actor.
/// Allows modifying the amount, detecting friendly fire, and canceling the suppression application.
/// </summary>
/// <param name="actor">The Actor receiving suppression</param>
/// <param name="attacker">The Actor or entity causing suppression (may be null)</param>
/// <param name="amount">The suppression amount to apply (modify via ref)</param>
/// <param name="isFriendlyFire">Whether this is friendly fire suppression</param>
/// <param name="cancel">Set to true to cancel the suppression application entirely</param>
public delegate void SuppressionApplicationInterceptor(GameObj actor, GameObj attacker, ref float amount, ref bool isFriendlyFire, ref bool cancel);

/// <summary>
/// Interceptor for Actor.GetMoraleMax which takes a multiplier parameter.
/// </summary>
/// <param name="actor">The Actor being queried</param>
/// <param name="multiplier">External multiplier applied to the result</param>
/// <param name="result">The maximum morale value</param>
public delegate void ActorMoraleMaxInterceptor(GameObj actor, float multiplier, ref int result);

/// <summary>
/// Interceptor for Actor.ApplyMorale which fires before morale is applied to an actor.
/// Allows modifying the morale amount and canceling the morale application entirely.
/// Useful for implementing morale immunity, leader bonuses, and rally mechanics.
/// </summary>
/// <param name="actor">The Actor receiving morale change</param>
/// <param name="eventType">The MoraleEventType bitmask indicating the cause</param>
/// <param name="amount">The morale amount to apply (modify via ref, can be positive or negative)</param>
/// <param name="cancel">Set to true to cancel the morale application entirely</param>
public delegate void MoraleApplicationInterceptor(GameObj actor, int eventType, ref float amount, ref bool cancel);

// ═══════════════════════════════════════════════════════════════════════════════
//  MOVEMENT DELEGATE TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interceptor for MovementType float properties that take a MovementMode parameter.
/// </summary>
/// <param name="movementType">The MovementType instance being queried</param>
/// <param name="movementMode">The movement mode (0=Walk, 1=Run, 2=Sprint)</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void MovementFloatInterceptor(GameObj movementType, int movementMode, ref float result);

/// <summary>
/// Interceptor for path cost calculations.
/// </summary>
/// <param name="movementType">The MovementType instance</param>
/// <param name="path">The path being evaluated</param>
/// <param name="actor">The actor traversing the path (may be null)</param>
/// <param name="result">The computed path cost - modify via ref to change the value</param>
public delegate void PathCostInterceptor(GameObj movementType, GameObj path, GameObj actor, ref int result);

/// <summary>
/// Result structure for path clipping operations.
/// </summary>
public struct ClipPathResult
{
    /// <summary>The actual cost consumed after clipping</summary>
    public int ActualCost;
    /// <summary>The maximum cost budget that was provided</summary>
    public int MaxCost;
    /// <summary>The index in the path where clipping occurred (-1 if not clipped)</summary>
    public int ClipIndex;
}

/// <summary>
/// Interceptor for path clipping by AP cost.
/// </summary>
/// <param name="movementType">The MovementType instance</param>
/// <param name="path">The path being clipped</param>
/// <param name="actor">The actor traversing the path</param>
/// <param name="result">The clip result - modify to change behavior</param>
public delegate void ClipPathInterceptor(GameObj movementType, GameObj path, GameObj actor, ref ClipPathResult result);

/// <summary>
/// Interceptor for Actor.MoveTo which fires before an actor attempts to move to a tile.
/// This is the master hook for ALL actor movement, allowing complete control over
/// movement restrictions, teleportation, and movement modifications.
/// </summary>
/// <param name="actor">The actor attempting to move</param>
/// <param name="tile">The destination tile (may be null for some movement modes)</param>
/// <param name="flags">Movement flags bitfield (mode, sprint, etc.)</param>
/// <param name="cancel">Set to true to prevent movement entirely</param>
public delegate void MoveToInterceptor(GameObj actor, GameObj tile, int flags, ref bool cancel);

/// <summary>
/// Interceptor for pathfinding calculations.
/// </summary>
/// <param name="process">The pathfinding process instance</param>
/// <param name="start">Start tile for the path</param>
/// <param name="end">End/destination tile for the path</param>
/// <param name="pathResult">Pointer to the path result (can be modified)</param>
/// <param name="cancel">Set to true to cancel pathfinding calculation</param>
public delegate void FindPathInterceptor(GameObj process, GameObj start, GameObj end, ref IntPtr pathResult, ref bool cancel);

// ═══════════════════════════════════════════════════════════════════════════════
//  STRATEGY LAYER DELEGATE TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interceptor for Strategy layer integer properties (AP, HP, Armor).
/// </summary>
/// <param name="instance">The Strategy object instance (UnitLeaderAttributes, BaseUnitLeader, Vehicle)</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void StrategyIntIntercept(GameObj instance, ref int result);

/// <summary>
/// Interceptor for Strategy layer float properties (damage mult, health pct).
/// </summary>
/// <param name="instance">The Strategy object instance</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void StrategyFloatIntercept(GameObj instance, ref float result);

/// <summary>
/// Interceptor for Strategy layer boolean properties (promotion/demotion eligibility).
/// </summary>
/// <param name="instance">The Strategy object instance</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void StrategyBoolIntercept(GameObj instance, ref bool result);

/// <summary>
/// Interceptor for Strategy layer GetEntityProperty calls.
/// </summary>
/// <param name="instance">The BaseUnitLeader instance</param>
/// <param name="propertyType">The EntityPropertyType enum value being queried</param>
/// <param name="result">The computed result - modify via ref to change the value</param>
public delegate void StrategyEntityIntercept(GameObj instance, int propertyType, ref float result);

// ═══════════════════════════════════════════════════════════════════════════════
//  AI BEHAVIOR DELEGATE TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interceptor for AI float value calculations (attack scoring, threat evaluation).
/// </summary>
/// <param name="ai">The AI instance (AIBrain or evaluator)</param>
/// <param name="target">The target being evaluated (may be null)</param>
/// <param name="result">The computed value - modify via ref to change AI decision</param>
public delegate void AIFloatValueInterceptor(GameObj ai, GameObj target, ref float result);

/// <summary>
/// Interceptor for AI integer value calculations (priority scoring).
/// </summary>
/// <param name="ai">The AI instance</param>
/// <param name="target">The target being evaluated (may be null)</param>
/// <param name="result">The computed value - modify via ref to change AI decision</param>
public delegate void AIIntValueInterceptor(GameObj ai, GameObj target, ref int result);

/// <summary>
/// Interceptor for AI boolean decisions (should flee, should attack, etc.).
/// </summary>
/// <param name="ai">The AI instance</param>
/// <param name="context">Context object (skill, target, etc. - may be null)</param>
/// <param name="result">The decision result - modify via ref to change behavior</param>
public delegate void AIBoolDecisionInterceptor(GameObj ai, GameObj context, ref bool result);

/// <summary>
/// Interceptor for Agent.Evaluate which fires when an AI agent begins evaluating actions.
/// WARNING: May be called in parallel for multiple agents - ensure thread-safe handlers!
/// </summary>
/// <param name="agent">The AI Agent instance evaluating actions</param>
/// <param name="cancel">Set to true to cancel evaluation and skip AI turn</param>
public delegate void AgentEvaluateInterceptor(GameObj agent, ref bool cancel);

/// <summary>
/// Interceptor for AI criterion position evaluation (Criterion.Evaluate).
/// Fires when AI evaluates a tile for positioning (movement, deployment, etc.).
/// WARNING: Called in PARALLEL for multiple tiles/criteria - handlers MUST be thread-safe and read-only!
/// This is called VERY frequently during pathfinding - keep handlers fast and simple.
/// </summary>
/// <param name="criterion">The criterion evaluating the position (e.g., AvoidOpponents, DistanceToCurrentTile)</param>
/// <param name="tile">The tile being scored for positioning</param>
/// <param name="score">The position score (can be modified to influence AI positioning)</param>
public delegate void CriterionEvaluateInterceptor(GameObj criterion, GameObj tile, ref float score);

/// <summary>
/// Interceptor for DamageHandler.ApplyDamage @ 0x180702970 - fires when damage is applied to an entity.
/// This is the core damage application hook, executing during skill effect processing.
/// Allows modification of damage values and complete cancellation of damage application.
/// </summary>
/// <param name="handler">The DamageHandler instance (effect data at handler+0x18)</param>
/// <param name="target">The entity receiving damage</param>
/// <param name="attacker">The entity dealing damage (skill owner from handler+0x10)</param>
/// <param name="skill">The skill being used (may be null for direct damage)</param>
/// <param name="damage">The total HP damage being applied (modify via ref)</param>
/// <param name="cancel">Set to true to completely prevent damage application</param>
public delegate void DamageApplicationInterceptor(GameObj handler, GameObj target, GameObj attacker, GameObj skill, ref float damage, ref bool cancel);

/// <summary>
/// Interceptor for EntityProperties.UpdateProperty @ 0x18060d320 - master hook for ALL additive stat modifications.
/// Fires when additive property bonuses are applied from items, skills, passive effects, and other sources.
/// Covers 70+ property types including damage, HP, movement, accuracy, concealment, and more.
/// </summary>
/// <param name="properties">The EntityProperties instance being modified</param>
/// <param name="propertyType">The property type enum (EntityPropertyType: 0=MaxHitpoints, 1=Accuracy, 2=SightRange, etc.)</param>
/// <param name="amount">The additive amount being applied (modify via ref to change bonus magnitude)</param>
public delegate void PropertyUpdateInterceptor(GameObj properties, int propertyType, ref int amount);

/// <summary>
/// Interceptor for EntityProperties.UpdateMultProperty @ 0x18060cc80 - master hook for ALL multiplicative stat modifiers.
/// Fires when multiplicative property bonuses are applied from items, skills, passive effects, and difficulty settings.
/// Multipliers stack additively as percentages: two +50% bonuses (1.5, 1.5) = 2.0x total, not 2.25x.
/// Formula: value += (mult - 1.0), so 1.5 adds 0.5 to the accumulated multiplier.
/// Covers multiplicative properties: AccuracyMult, DamageMult, MovementRangeMult, ActionPointsMult, etc.
/// </summary>
/// <param name="properties">The EntityProperties instance being modified</param>
/// <param name="propertyType">The property type enum (EntityPropertyType: 9=MovementRangeMult, 10=AccuracyMult, 11=DamageMult, etc.)</param>
/// <param name="multiplier">The multiplier being applied (1.0 = no change, 1.5 = +50%, 2.0 = +100%; modify via ref)</param>
public delegate void PropertyUpdateMultInterceptor(GameObj properties, int propertyType, ref float multiplier);

/// <summary>
/// Interceptor for item container add operations.
/// </summary>
/// <param name="container">The ItemContainer instance</param>
/// <param name="item">The Item being added</param>
/// <param name="expandSlots">Whether to expand slots if container is full (modify via ref)</param>
/// <param name="cancel">Set to true to prevent item addition</param>
public delegate void ItemAddInterceptor(GameObj container, GameObj item, ref bool expandSlots, ref bool cancel);

#endregion

// ═══════════════════════════════════════════════════════════════════════════════
//  INTERCEPT - Central event registry and patch manager
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Central system for intercepting and modifying game property calculations.
///
/// This is the foundation of the Menace SDK's property modification system.
/// It allows modders to hook into property getters and modify results after
/// the game computes them, enabling powerful gameplay modifications.
///
/// Architecture:
/// - Static events for each interceptable property
/// - Automatic Harmony patching on Initialize()
/// - Dual C#/Lua support via LuaScriptEngine integration
/// - Context passing (entity owners, skill context, etc.)
///
/// C# Usage:
///   Intercept.OnGetDamage += (props, owner, ref result) => {
///       result *= 1.5f;  // 50% more damage
///   };
///
///   Intercept.OnGetConcealment += (props, owner, ref result) => {
///       if (IsNightTime()) result += 5;  // Bonus concealment at night
///   };
///
///   Intercept.OnGetHitChance += (skill, attacker, target, ref result) => {
///       result.FinalChance = Math.Min(result.FinalChance + 0.1f, 1.0f);  // +10% hit
///   };
///
/// Lua Usage:
///   on("property_damage", function(data)
///       data.result = data.result * 1.5
///   end)
///
/// Design Notes:
/// - All interceptors fire AFTER the game computes the base value
/// - Multiple interceptors stack (each sees the result of previous)
/// - Use ref parameters to modify values in place
/// - Owner resolution is best-effort (may be null in some contexts)
///
/// Based on Ghidra reverse engineering:
/// - EntityProperties.GetDamage @ 0x18060bd60: Damage (0x118) * DamageMult (0x11C)
/// - EntityProperties.GetAccuracy @ 0x18060b9f0: Accuracy (0x68) * AccuracyMult (0x6C)
/// - EntityProperties.GetArmor @ 0x18060bb40: max(0x1c, max(0x20, 0x24)) * ArmorMult (0x28)
/// - EntityProperties.GetConcealment @ 0x18060bc90: Concealment (0xD4) * ConcealMult (0xD8)
/// - EntityProperties.GetDetection @ 0x18060bd90: Detection (0xCC) * DetectMult (0xD0)
/// - Skill.GetHitchance @ 0x1806dba90: Complex calculation with accuracy, cover, defense
/// - Skill.GetCoverMult @ 0x1806d9bb0: Cover value lookup and evasion calculation
/// - Actor.HasLineOfSightTo @ 0x1805dfa10: Vision vs concealment + distance + cover
/// - Entity.GetHitpointsPct @ 0x180611720: Current HP / Max HP percentage
/// - Entity.GetArmorDurabilityPct @ 0x180610d10: Current armor / Max armor percentage
/// - Entity.GetCoverUsage @ 0x180610f10: Current cover state enum
/// - Entity.GetProvidedCover @ 0x180611870: Cover object this entity provides
/// - Entity.IsDiscovered @ 0x180612c90: Whether entity has been discovered
/// - Entity.GetLastSkillUsed @ 0x180611780: Last skill action tracking
/// - Entity.GetScaleRange @ 0x180611940: Min/max scale bounds as Vector2
/// </summary>
public static class Intercept
{
    private static bool _initialized;
    private static HarmonyLib.Harmony _harmony;

    // Cached types for performance
    private static Type _entityPropertiesType;
    private static Type _skillType;
    private static Type _actorType;
    private static Type _entityType;
    private static Type _tileType;
    private static Type _baseTileType;
    private static Type _lineOfSightType;
    // Movement types
    private static Type _movementTypeType;
    // Strategy layer types
    private static Type _unitLeaderAttributesType;
    private static Type _baseUnitLeaderType;
    private static Type _vehicleType;
    // AI types
    private static Type _aiBrainType;
    private static Type _agentType;
    // Pathfinding types
    private static Type _pathfindingProcessType;

    // Cached field offsets for owner resolution
    private const uint OFFSET_SKILL_CONTAINER = 0x18;  // Skill -> SkillContainer
    private const uint OFFSET_CONTAINER_ENTITY = 0x10; // SkillContainer -> Entity

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 1 EVENTS - EntityProperties getters (most commonly needed)
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 1: EntityProperties Events

    /// <summary>
    /// Fires after EntityProperties.GetDamage() computes the damage value.
    /// Formula: Damage (offset 0x118) * DamageMult (offset 0x11C, clamped to >=0)
    /// </summary>
    public static event FloatIntercept OnGetDamage;

    /// <summary>
    /// Fires after EntityProperties.GetAccuracy() computes the accuracy value.
    /// Formula: floor(Accuracy (0x68) * AccuracyMult (0x6C, clamped))
    /// </summary>
    public static event FloatIntercept OnGetAccuracy;

    /// <summary>
    /// Fires after EntityProperties.GetArmor() computes the armor value.
    /// Formula: max(Armor1, Armor2, Armor3) * ArmorMult (clamped)
    /// </summary>
    public static event IntIntercept OnGetArmor;

    /// <summary>
    /// Fires after EntityProperties.GetConcealment() computes the concealment value.
    /// Formula: floor(Concealment (0xD4) * ConcealMult (0xD8, clamped))
    /// Higher concealment = harder to detect.
    /// </summary>
    public static event IntIntercept OnGetConcealment;

    /// <summary>
    /// Fires after EntityProperties.GetDetection() computes the detection value.
    /// Formula: max(0, floor(Detection (0xCC) * DetectMult (0xD0, clamped)))
    /// Higher detection = better at spotting concealed units.
    /// </summary>
    public static event IntIntercept OnGetDetection;

    /// <summary>
    /// Fires after EntityProperties.GetVision() computes the vision range.
    /// Base vision at offset 0xC4, multiplier at 0xC8.
    /// </summary>
    public static event IntIntercept OnGetVision;

    /// <summary>
    /// Fires when EntityProperties.UpdateProperty applies additive stat bonuses from items/skills/effects.
    /// Address: 0x18060d320. Master hook for ALL additive property modifications across all templates.
    ///
    /// PropertyType enum values (EntityPropertyType):
    ///   0 = MaxHitpoints (+0xC4)    - Maximum health points
    ///   1 = Accuracy (+0xA0, float) - Base hit chance
    ///   2 = SightRange (+0xD4)      - Vision distance
    ///   3 = MovementRange (+0x68, float) - Movement distance per turn
    ///   4 = ActionPoints (+0x1C)    - Actions available per turn
    ///   5 = MovementCost (+0x14)    - AP cost to move
    ///   6 = Initiative (+0x34)      - Turn order priority
    ///   7 = Concealment (+0xCC)     - Stealth rating
    ///   8 = Discipline (+0x10)      - Morale/suppression resistance
    ///   ... (and 60+ more property types)
    ///
    /// Use Cases:
    /// - Scale equipment bonuses: Multiply damage bonuses by difficulty
    /// - Cap stat bonuses: Prevent excessive stacking (e.g., max +50% movement)
    /// - Conditional modifiers: Apply bonuses only for specific units or conditions
    /// - Item set bonuses: Grant extra bonuses when multiple items are equipped
    /// - Difficulty scaling: Adjust all stat gains based on game mode
    ///
    /// Example:
    /// <code>
    /// // Double all HP bonuses from items
    /// Intercept.OnPropertyUpdate += (properties, propertyType, ref amount) => {
    ///     if (propertyType == 0) {  // MaxHitpoints
    ///         amount *= 2;
    ///     }
    /// };
    ///
    /// // Cap movement bonuses at +3
    /// Intercept.OnPropertyUpdate += (properties, propertyType, ref amount) => {
    ///     if (propertyType == 3) {  // MovementRange
    ///         amount = Math.Min(amount, 3);
    ///     }
    /// };
    ///
    /// // Scale damage bonuses by difficulty (hard mode = 50% bonuses)
    /// Intercept.OnPropertyUpdate += (properties, propertyType, ref amount) => {
    ///     if (propertyType == 29 && IsHardMode()) {  // Damage property
    ///         amount = (int)(amount * 0.5f);
    ///     }
    /// };
    /// </code>
    /// </summary>
    public static event PropertyUpdateInterceptor OnPropertyUpdate;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 1 EVENTS - Skill calculations (critical for combat mods)
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 1: Skill Events

    /// <summary>
    /// Fires after Skill.GetHitchance() computes the hit chance.
    /// This is the central hit calculation combining accuracy, cover, evasion, and distance.
    /// Result contains all component values for fine-grained modification.
    /// </summary>
    public static event HitChanceInterceptor OnGetHitChance;

    /// <summary>
    /// Fires after Skill.GetExpectedDamage() computes expected damage.
    /// Factors in base damage, multipliers, armor penetration, and target armor.
    /// </summary>
    public static event ExpectedDamageInterceptor OnGetExpectedDamage;

    /// <summary>
    /// Fires after Skill.GetCoverMult() computes the cover multiplier.
    /// Lower values = more protection from cover.
    /// </summary>
    public static event CoverMultInterceptor OnGetCoverMult;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 1 EVENTS - Actor visibility (stealth/detection mods)
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 1: Actor Events

    /// <summary>
    /// Fires after Actor.HasLineOfSightTo() determines visibility.
    /// Considers vision range, detection vs concealment, cover, and elevation.
    /// Set result to false to hide targets, true to reveal them.
    /// </summary>
    public static event LineOfSightInterceptor OnHasLineOfSightTo;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  ACTOR STATE QUERY EVENTS - Morale/Suppression
    // ═══════════════════════════════════════════════════════════════════

    #region Actor State: Morale/Suppression

    /// <summary>
    /// Fires after Actor.GetMoraleMax() computes maximum morale.
    /// Formula: (BaseMorale + BonusMorale) * MoraleMultiplier * param
    /// Address: 0x1805df330
    /// </summary>
    public static event ActorMoraleMaxInterceptor OnActorGetMoraleMax;

    /// <summary>
    /// Fires after Actor.GetMoralePct() computes morale percentage.
    /// Returns current morale (offset 0x160) divided by GetMoraleMax result.
    /// Address: 0x1805df4a0
    /// </summary>
    public static event ActorFloatStateInterceptor OnActorGetMoralePct;

    /// <summary>
    /// Fires after Actor.GetMoraleState() computes morale state.
    /// Returns: 1=Panicked (morale&lt;=0), 2=Shaken (morale&lt;=threshold), 3=Steady.
    /// Commanders (actor+0x4C=1) are immune to Panic.
    /// Address: 0x1805df4d0
    /// </summary>
    public static event ActorIntStateInterceptor OnActorGetMoraleState;

    /// <summary>
    /// Fires after Actor.GetSuppressionPct() computes suppression percentage.
    /// Returns suppression value (offset 0x15C) multiplied by a constant.
    /// Address: 0x1805df710
    /// </summary>
    public static event ActorFloatStateInterceptor OnActorGetSuppressionPct;

    /// <summary>
    /// Fires after Actor.GetSuppressionState() computes suppression state.
    /// Returns: 0=Normal, 1=Suppressed (if >= threshold1), 2=PinnedDown (if >= threshold2).
    /// Address: 0x1805df730
    /// </summary>
    public static event ActorSuppressionStateInterceptor OnActorGetSuppressionState;

    /// <summary>
    /// Fires when Actor.ApplySuppression() is called, BEFORE the suppression value is applied.
    /// This allows mods to:
    /// - Modify the suppression amount being applied
    /// - Detect friendly fire suppression incidents
    /// - Grant suppression immunity by setting cancel=true
    /// - Implement cascading suppression to nearby units
    /// - Track suppression application for analytics
    ///
    /// Formula (from Ghidra @ 0x1805ddda0):
    /// - Base suppression modified by discipline: amount * (1 - discipline * 0.01)
    /// - Faction modifiers applied based on isFriendlyFire flag
    /// - Final value stored at Actor+0x15C (current suppression)
    /// - TacticalManager.InvokeOnSuppressionApplied fires after update
    ///
    /// Address: 0x1805ddda0
    ///
    /// Use cases:
    /// - Veteran immunity: Reduce suppression for experienced units
    /// - Morale interaction: Link suppression to morale state
    /// - Suppression chains: Apply partial suppression to adjacent allies
    /// - Suppression tracking: Log all suppression events for AI analysis
    /// </summary>
    public static event SuppressionApplicationInterceptor OnSuppressionApplied;

    /// <summary>
    /// Fires when Actor.ApplyMorale() is called, BEFORE the morale value is applied.
    /// This allows mods to:
    /// - Modify the morale amount being applied
    /// - Grant morale immunity by setting cancel=true
    /// - Implement leader bonuses (nearby leaders reduce morale loss)
    /// - Create rally mechanics (prevent morale loss when rallied)
    /// - Track morale changes for analytics and mission scoring
    /// - Implement morale cascading to nearby units
    ///
    /// Formula (from Ghidra @ 0x1805dd240):
    /// - Checks morale immunity flag (EntityProps+0xEC bit 7)
    /// - Validates eventType against allowed morale events (EntityProps+0xA8 bitmask)
    /// - Applies morale multiplier from EntityProps+0xBC
    /// - Calls SkillContainer.OnMoraleEvent(eventType, amount, 0)
    /// - Clamps final morale between 0.0 and GetMoraleMax()
    /// - Stores at Actor+0x160 via SetMorale()
    ///
    /// Address: 0x1805dd240
    ///
    /// Use cases:
    /// - Leader morale protection: Leaders resist morale loss
    /// - Rally mechanics: Nearby leaders prevent morale loss
    /// - Elite immunity: Veterans ignore certain morale events
    /// - Morale cascading: Spread morale changes to nearby allies
    /// - Achievement tracking: Monitor morale events for mission objectives
    /// </summary>
    public static event MoraleApplicationInterceptor OnMoraleApplied;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  ACTOR STATE QUERY EVENTS - Action Points
    // ═══════════════════════════════════════════════════════════════════

    #region Actor State: Action Points

    /// <summary>
    /// Fires after Actor.GetActionPointsAtTurnStart() returns AP at turn start.
    /// Simple field read from offset 0x14C.
    /// Address: 0x1805df0c0
    /// </summary>
    public static event ActorIntStateInterceptor OnActorGetActionPointsAtTurnStart;

    /// <summary>
    /// Fires after Actor.GetTurningCost() computes AP cost to turn.
    /// Calculates based on direction difference and unit properties.
    /// Address: 0x1805df810
    /// </summary>
    public static event ActorTurningCostInterceptor OnActorGetTurningCost;

    /// <summary>
    /// Fires after Actor.GetTilesMovedThisTurn() returns tiles moved count.
    /// Simple field read from offset 0x154.
    /// Address: 0x1805df7c0
    /// </summary>
    public static event ActorIntStateInterceptor OnActorGetTilesMovedThisTurn;

    /// <summary>
    /// Fires after Actor.GetTimesAttackedSinceLastTurn() returns attack count.
    /// Simple field read from offset 0x140.
    /// Address: 0x1805df7d0
    /// </summary>
    public static event ActorIntStateInterceptor OnActorGetTimesAttackedSinceLastTurn;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  ACTOR STATE QUERY EVENTS - Boolean State Queries
    // ═══════════════════════════════════════════════════════════════════

    #region Actor State: Boolean Queries

    /// <summary>
    /// Fires after Actor.IsActive() checks if actor is the active/selected unit.
    /// Compares actor pointer against TacticalManager's active actor.
    /// Address: 0x1805e0110
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsActive;

    /// <summary>
    /// Fires after Actor.IsActionPointsSpent() checks if actor can still act.
    /// Complex check involving AP, skills, and movement options.
    /// Address: 0x1805dfe90
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsActionPointsSpent;

    /// <summary>
    /// Fires after Actor.IsDetectedByFaction() checks faction detection.
    /// Uses bitmask at offset 0x138 (DetectedByFactionMask).
    /// Address: 0x1805e0160
    /// </summary>
    public static event ActorFactionDetectionInterceptor OnActorIsDetectedByFaction;

    /// <summary>
    /// Fires after Actor.IsDying() checks if actor is in dying state.
    /// Simple field read from offset 0x16A.
    /// Address: 0x1805e0180
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsDying;

    /// <summary>
    /// Fires after Actor.IsHeavyWeaponDeployed() checks deployment state.
    /// Simple field read from offset 0x16F.
    /// Address: 0x1805e0190
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsHeavyWeaponDeployed;

    /// <summary>
    /// Fires after Actor.IsHiddenToAI() checks if hidden from AI factions.
    /// Uses cached state at offset 0x1A4 (0=unknown, 1=visible, 2=hidden).
    /// Address: 0x1805e01a0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsHiddenToAI;

    /// <summary>
    /// Fires after Actor.IsHiddenToPlayer() checks if hidden from player.
    /// Delegates to IsHiddenToPlayerAtTile with current tile.
    /// Address: 0x1805e0750
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsHiddenToPlayer;

    /// <summary>
    /// Fires after Actor.IsInfantry() checks unit type.
    /// Returns true if UnitType (offset 0x88) == 2 and SubType (offset 0x8C) == 0.
    /// Address: 0x1805e0780
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsInfantry;

    /// <summary>
    /// Fires after Actor.IsLeavingMap() checks if actor is leaving the map.
    /// Simple field read from offset 0x16B.
    /// Address: 0x1805e07e0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsLeavingMap;

    /// <summary>
    /// Fires after Actor.IsMinion() checks if actor is a minion.
    /// Returns true if MinionID (offset 0x1B0) != 0.
    /// Minions are spawned entities controlled by another actor's skills.
    /// Address: 0x1805e07f0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsMinion;

    /// <summary>
    /// Fires after Actor.IsMoving() checks if actor is currently moving.
    /// Simple field read from offset 0x167.
    /// Address: 0x1805e0810
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsMoving;

    /// <summary>
    /// Fires after Actor.IsSelectableByPlayer() checks if player can select.
    /// Checks player control and turn-done status.
    /// Address: 0x1805e0820
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsSelectableByPlayer;

    /// <summary>
    /// Fires after Actor.IsStunned() checks stun status.
    /// Returns true if actor+0x16C flag set OR EntityProperties+0xEC bit 0 set.
    /// Address: 0x1805e0860
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsStunned;

    /// <summary>
    /// Fires after Actor.IsTurnDone() checks if turn is complete.
    /// Simple field read from offset 0x164.
    /// Address: 0x1805e08a0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsTurnDone;

    /// <summary>
    /// Fires after Actor.IsTurret() checks if actor is a turret.
    /// Returns true if UnitType (offset 0x88) == 2 and SubType (offset 0x8C) == 2.
    /// Address: 0x1805e08b0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsTurret;

    /// <summary>
    /// Fires after Actor.IsVehicle() checks if actor is a vehicle.
    /// Returns true if UnitType (offset 0x88) == 2 and SubType (offset 0x8C) == 1.
    /// Address: 0x1805e0900
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorIsVehicle;

    /// <summary>
    /// Fires after Actor.CanEnterAnyAdjacentVehicle() checks vehicle entry.
    /// Checks if infantry can enter an adjacent allied vehicle.
    /// Address: 0x1805de260
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorCanEnterAnyAdjacentVehicle;

    /// <summary>
    /// Fires after Actor.HasActed() checks if actor has acted this turn.
    /// Compares AP at turn start with current AP, or checks acted flag (offset 0x171).
    /// Address: 0x1805df9f0
    /// </summary>
    public static event ActorBoolStateInterceptor OnActorHasActed;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 2 EVENTS - Skill range and cost properties
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 2: Skill Range and Cost Events

    /// <summary>
    /// Fires after Skill.GetExpectedSuppression() computes suppression preview.
    /// @ 0x1806db2d0: Complex calculation involving cover, discipline, and skill modifiers.
    /// </summary>
    public static event FloatSkillInterceptor OnGetExpectedSuppression;

    /// <summary>
    /// Fires after Skill.GetActionPointCost() computes the AP cost.
    /// @ 0x1806d8e80: Base cost (0xa0) modified by actor properties (0x44, 0x48).
    /// </summary>
    public static event IntSkillInterceptor OnGetActionPointCost;

    /// <summary>
    /// Fires after Skill.GetIdealRangeBase() computes the optimal engagement range.
    /// @ 0x1806dbea0: Returns weapon's ideal range (0x144) or skill template range (0x12C).
    /// </summary>
    public static event IntSkillInterceptor OnGetIdealRangeBase;

    /// <summary>
    /// Fires after Skill.GetMaxRangeBase() computes the maximum engagement range.
    /// @ 0x1806dc8b0: Returns weapon's max range or skill template max range (0x130).
    /// </summary>
    public static event IntSkillInterceptor OnGetMaxRangeBase;

    /// <summary>
    /// Fires after Skill.GetMinRangeBase() computes the minimum engagement range.
    /// @ 0x1806dc980: Returns weapon's min range or skill template min range (0x128).
    /// </summary>
    public static event IntSkillInterceptor OnGetMinRangeBase;

    /// <summary>
    /// Fires after Skill.IsInRange() checks if target is within range.
    /// @ 0x1806de4f0: Checks distance >= min range (0xb4) and <= max range (0xbc) + bonus.
    /// </summary>
    public static event BoolInterceptor OnIsInRange;

    /// <summary>
    /// Fires after Skill.IsInRangeShape() checks shape-based range validity.
    /// @ 0x1806de390: Checks if target tile is within skill's shape range (0x120).
    /// </summary>
    public static event BoolInterceptor OnIsInRangeShape;

    /// <summary>
    /// Fires after Skill.IsMovementSkill() checks if skill is a movement type.
    /// @ 0x1806de730: Iterates skill handlers looking for IMovementSkill interface.
    /// </summary>
    public static event BoolInterceptor OnIsMovementSkill;

    /// <summary>
    /// Fires after Skill.IsUsable() checks if a skill is usable by an actor.
    /// This hook allows mods to add custom skill restrictions, cooldown modifications,
    /// or conditional availability based on actor state, equipment, or environmental factors.
    ///
    /// Address: 0x1806deb10
    ///
    /// The function performs several checks:
    /// - Actor death/dying state
    /// - Cooldown/charge availability
    /// - Deployment requirements
    /// - Required stance validation
    /// - AI state restrictions
    /// - Effect handler checks
    ///
    /// Use cases:
    /// - Class-based skill restrictions (limit skills to specific unit types)
    /// - Dynamic cooldown modifications (reduce cooldown for veterans)
    /// - Conditional abilities (enable/disable based on morale, suppression, etc.)
    /// - Equipment requirements (require specific items to use skills)
    /// - Environmental restrictions (disable skills in certain terrain/zones)
    ///
    /// Threading: Main thread only - no threading concerns
    /// </summary>
    public static event SkillUsableInterceptor OnSkillIsUsable;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 2 EVENTS - EntityProperties combat dropoff and penetration
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 2: EntityProperties Combat Events

    /// <summary>
    /// Fires after EntityProperties.GetDamageDropoff() computes damage falloff over distance.
    /// Address: 0x18060bcd0
    /// Used for weapons that lose damage at range.
    /// </summary>
    public static event FloatIntercept OnGetDamageDropoff;

    /// <summary>
    /// Fires after EntityProperties.GetDamageToArmorDurability() computes armor-piercing damage.
    /// Address: 0x18060bd30
    /// Determines how much damage is dealt to armor durability on hit.
    /// </summary>
    public static event FloatIntercept OnGetDamageToArmorDurability;

    /// <summary>
    /// Fires after EntityProperties.GetDamageToArmorDurabilityDropoff() computes armor damage over range.
    /// Address: 0x18060bd00
    /// Armor durability damage falloff at distance.
    /// </summary>
    public static event FloatIntercept OnGetDamageToArmorDurabilityDropoff;

    /// <summary>
    /// Fires after EntityProperties.GetAccuracyDropoff() computes accuracy loss over distance.
    /// Address: 0x18060b9c0
    /// Higher values = faster accuracy decay at range.
    /// </summary>
    public static event FloatIntercept OnGetAccuracyDropoff;

    /// <summary>
    /// Fires after EntityProperties.GetArmorPenetration() computes AP values.
    /// Address: 0x18060bab0
    /// Determines ability to bypass target armor.
    /// </summary>
    public static event FloatIntercept OnGetArmorPenetration;

    /// <summary>
    /// Fires after EntityProperties.GetArmorPenetrationDropoff() computes AP falloff.
    /// Address: 0x18060ba80
    /// Armor penetration decay at distance.
    /// </summary>
    public static event FloatIntercept OnGetArmorPenetrationDropoff;

    /// <summary>
    /// Fires after EntityProperties.GetSuppression() computes suppression application rate.
    /// Address: 0x18060c780
    /// How much suppression this entity applies when attacking.
    /// </summary>
    public static event FloatIntercept OnGetSuppression;

    /// <summary>
    /// Fires after EntityProperties.GetDiscipline() computes morale resistance.
    /// Address: 0x18060bdd0
    /// Higher discipline = more resistant to suppression and panic.
    /// </summary>
    public static event FloatIntercept OnGetDiscipline;

    /// <summary>
    /// Fires after EntityProperties.GetHitpointsPerElement() computes HP per squad member.
    /// Address: 0x18060be10
    /// For squad-based entities, HP of each individual member.
    /// </summary>
    public static event IntIntercept OnGetHitpointsPerElement;

    /// <summary>
    /// Fires after EntityProperties.GetMaxHitpoints() computes total maximum HP.
    /// Address: 0x18060be40
    /// Total hitpoint pool for the entity.
    /// </summary>
    public static event IntIntercept OnGetMaxHitpoints;

    /// <summary>
    /// Fires after EntityProperties.GetActionPoints() computes AP pool size.
    /// Address: 0x18060ba20
    /// Maximum action points available per turn.
    /// </summary>
    public static event IntIntercept OnGetActionPoints;

    /// <summary>
    /// Fires after EntityProperties.GetMovementCostModifier() computes movement cost multiplier.
    /// Address: 0x18060bec0
    /// Multiplier applied to tile movement costs.
    /// </summary>
    public static event FloatIntercept OnGetMovementCostModifier;

    /// <summary>
    /// Fires after EntityProperties.GetPropertyValue() computes a generic property value.
    /// Address: 0x18060bef0
    /// Takes an EntityPropertyType parameter to determine which property to query.
    /// Use propertyType to filter for specific properties.
    /// </summary>
    public static event PropertyValueInterceptor OnGetPropertyValue;

    /// <summary>
    /// Fired when multiplicative property modifications are applied (UpdateMultProperty).
    /// Address: 0x18060cc80. Master hook for ALL multiplier bonuses.
    ///
    /// STACKING FORMULA: Multipliers stack additively as percentages using value += (mult - 1.0).
    /// This means two +50% bonuses (1.5, 1.5) result in 2.0x total, NOT 2.25x.
    /// Example: Base 1.0, first +50% -> 1.0 + 0.5 = 1.5, second +50% -> 1.5 + 0.5 = 2.0x
    ///
    /// MULTIPLIER VALUES:
    /// - 1.0 = no change (base)
    /// - 1.5 = +50% bonus
    /// - 2.0 = +100% bonus (doubles value)
    /// - 0.5 = -50% penalty (halves value)
    ///
    /// PROPERTY TYPES (multiplicative):
    /// - 9 = MovementRangeMult (movement distance)
    /// - 10 = AccuracyMult (hit chance)
    /// - 11 = DamageMult (damage output)
    /// - 12 = ArmorPenMult (armor penetration)
    /// - 13 = ActionPointsMult (AP regeneration)
    /// - 14 = VisionMult (sight range)
    /// - 15-28, 35-36, 41, 43-44, 46-47, 49, 52, 55, 57, 60, 64, 67, 71 = Other multiplier types
    ///
    /// USE CASES:
    /// - Difficulty-based accuracy scaling (easy mode +20% hit chance)
    /// - Damage multiplier nerfs/buffs (elite units +50% damage)
    /// - Conditional multipliers (veteran bonus: +25% accuracy when in cover)
    /// - Equipment bonuses (scope: +15% accuracy)
    /// </summary>
    /// <param name="properties">The EntityProperties being modified</param>
    /// <param name="propertyType">The multiplier property type (EntityPropertyType enum)</param>
    /// <param name="multiplier">The multiplier (1.0 = no change, 1.5 = +50%, can be modified)</param>
    public static event PropertyUpdateMultInterceptor OnPropertyUpdateMult;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TIER 2 EVENTS - Entity state methods (combat status tracking)
    // ═══════════════════════════════════════════════════════════════════

    #region Tier 2: Entity State Events

    /// <summary>
    /// Fires after Entity.GetHitpointsPct() computes the health percentage.
    /// Address: 0x180611720
    /// Returns: float (0.0 to 1.0 representing current HP / max HP)
    /// Use cases: Health-based ability triggers, UI modifications, AI behavior
    /// </summary>
    public static event EntityFloatStateInterceptor OnGetHitpointsPct;

    /// <summary>
    /// Fires after Entity.GetArmorDurabilityPct() computes the armor condition.
    /// Address: 0x180610d10
    /// Returns: float (0.0 to 1.0 representing current armor / max armor)
    /// Use cases: Armor degradation effects, repair triggers, visual feedback
    /// </summary>
    public static event EntityFloatStateInterceptor OnGetArmorDurabilityPct;

    /// <summary>
    /// Fires after Entity.GetCoverUsage() returns the cover state enum.
    /// Address: 0x180610f10
    /// Returns: int (CoverUsage enum: None=0, Half=1, Full=2, etc.)
    /// Use cases: Cover-based bonuses, tactical AI, stealth mechanics
    /// </summary>
    public static event EntityIntStateInterceptor OnGetCoverUsage;

    /// <summary>
    /// Fires after Entity.GetProvidedCover() returns the cover object this entity provides.
    /// Address: 0x180611870
    /// Returns: IntPtr to Cover object (may be null/zero if no cover provided)
    /// Use cases: Dynamic cover systems, destructible cover, cover quality mods
    /// </summary>
    public static event EntityObjectStateInterceptor OnGetProvidedCover;

    /// <summary>
    /// Fires after Entity.IsDiscovered() returns the discovery state.
    /// Address: 0x180612c90
    /// Returns: bool (whether the entity has been discovered by enemies)
    /// Use cases: Stealth systems, fog of war, ambush mechanics
    /// </summary>
    public static event EntityBoolStateInterceptor OnIsDiscovered;

    /// <summary>
    /// Fires after Entity.GetLastSkillUsed() returns the last skill action.
    /// Address: 0x180611780
    /// Returns: IntPtr to Skill object (may be null if no skill used)
    /// Use cases: Combo systems, action tracking, reaction triggers
    /// </summary>
    public static event EntityObjectStateInterceptor OnGetLastSkillUsed;

    /// <summary>
    /// Fires after Entity.GetScaleRange() returns the scale bounds.
    /// Address: 0x180611940
    /// Returns: Vector2Result (X=min scale, Y=max scale)
    /// Use cases: Size-based mechanics, visual scaling, hitbox modifications
    /// </summary>
    public static event EntityVector2StateInterceptor OnGetScaleRange;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  TILE EVENTS - Tile query interceptors
    // ═══════════════════════════════════════════════════════════════════

    #region Tile Events

    /// <summary>
    /// Fires after Tile.HasLineOfSightTo() checks LoS between tiles.
    /// Address: 0x180681d70. Handles structures with cover type 4.
    /// Calls LineOfSight algorithm bidirectionally.
    /// </summary>
    public static event TileLoSInterceptor OnTileHasLineOfSightTo;

    /// <summary>
    /// Fires after Tile.IsBlockingLineOfSight() checks if tile blocks LoS.
    /// Address: 0x180681e50. Simple check of boolean flag at offset 0x60.
    /// </summary>
    public static event TileBlockerInterceptor OnTileIsBlockingLineOfSight;

    /// <summary>
    /// Fires after Tile.GetCover() retrieves cover value for direction.
    /// Address: 0x180680b20. Cover array at 0x28, checks adjacent tiles
    /// for actors providing cover. Returns 0-3 cover level.
    /// </summary>
    public static event TileCoverInterceptor OnTileGetCover;

    /// <summary>
    /// Fires after Tile.GetCoverMask() computes cover bitmask.
    /// Address: 0x180680a50. Returns bitmask where bits 0-3 represent
    /// cover in N/E/S/W directions.
    /// </summary>
    public static event TileCoverMaskInterceptor OnTileGetCoverMask;

    /// <summary>
    /// Fires after Tile.GetEntityProvidedCover() gets cover from entities.
    /// Address: 0x180681a30. Returns cover value provided by entities
    /// occupying the tile.
    /// </summary>
    public static event TileEntityCoverInterceptor OnTileGetEntityProvidedCover;

    /// <summary>
    /// Fires after Tile.CanBeEntered() checks entry permission.
    /// Address: 0x180680950. Checks if tile's structure allows entry.
    /// </summary>
    public static event TileEntryInterceptor OnTileCanBeEntered;

    /// <summary>
    /// Fires after Tile.CanBeEnteredBy() checks entity-specific entry.
    /// Address: 0x180680920. Checks if specific entity can enter the tile.
    /// </summary>
    public static event TileEntityEntryInterceptor OnTileCanBeEnteredBy;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  BASETILE EVENTS - BaseTile query interceptors
    // ═══════════════════════════════════════════════════════════════════

    #region BaseTile Events

    /// <summary>
    /// Fires after BaseTile.HasCover() checks cover presence.
    /// Address: 0x1805caa80. Iterates cover array at 0x28 checking
    /// for any non-zero cover values.
    /// </summary>
    public static event BaseTileCoverCheckInterceptor OnBaseTileHasCover;

    /// <summary>
    /// Fires after BaseTile.HasHalfCover() checks for half cover.
    /// Address: 0x1805cab30. Checks if tile has half cover in any direction.
    /// </summary>
    public static event BaseTileHalfCoverInterceptor OnBaseTileHasHalfCover;

    /// <summary>
    /// Fires after BaseTile.HasHalfCoverInDir() checks directional half cover.
    /// Address: 0x1805caaf0. Half cover array at 0x30, indexed by direction/2.
    /// </summary>
    public static event BaseTileDirHalfCoverInterceptor OnBaseTileHasHalfCoverInDir;

    /// <summary>
    /// Fires after BaseTile.IsMovementBlocked() checks movement obstruction.
    /// Address: 0x1805cae00. Movement block array at 0x38, indexed by direction.
    /// </summary>
    public static event BaseTileMovementBlockedInterceptor OnBaseTileIsMovementBlocked;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  PATHFINDING EVENTS - PathfindingProcess method interceptors
    // ═══════════════════════════════════════════════════════════════════

    #region Pathfinding Events

    /// <summary>
    /// Fires after PathfindingProcess.IsTraversable() checks if a tile can be traversed.
    /// Address: 0x180662860. Core pathfinding traversability check.
    /// WARNING: Called very frequently during pathfinding - keep handlers fast!
    /// Use for dynamic terrain restrictions, weather effects, or custom tile blocking.
    /// </summary>
    /// <param name="process">The pathfinding process</param>
    /// <param name="tile">The tile being checked</param>
    /// <param name="result">Whether the tile is traversable (can be modified)</param>
    public static event TraversableCheckInterceptor OnTileTraversable;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  LINEOFSIGHT EVENTS - LineOfSight static method interceptors
    // ═══════════════════════════════════════════════════════════════════

    #region LineOfSight Events

    /// <summary>
    /// Fires after LineOfSight.HasLineOfSight() (RayTrace) performs LoS check.
    /// Address: 0x18051df40. Core LoS raycast algorithm. Iterates along line
    /// from source to target checking blocking flags (bit 11 at offset 0x1c).
    /// Handles structures and directional blockers. Max 1000 iterations.
    /// </summary>
    public static event LineOfSightRayTraceInterceptor OnLineOfSightRayTrace;

    /// <summary>
    /// Fires after LineOfSight.IsNearTileCorner() checks corner proximity.
    /// Address: 0x18051e4b0. Used for cover calculations and positioning.
    /// </summary>
    public static event LineOfSightCornerInterceptor OnLineOfSightIsNearTileCorner;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  MOVEMENT EVENTS - MovementType calculations
    // ═══════════════════════════════════════════════════════════════════

    #region Movement Events

    /// <summary>
    /// Fires after MovementType.GetMaxMovementSpeed() computes movement speed.
    /// Controls how fast units can move on the tactical map.
    /// </summary>
    public static event MovementFloatInterceptor OnGetMaxMovementSpeed;

    /// <summary>
    /// Fires after MovementType.GetTotalPathCost() computes total AP cost for a path.
    /// Used for pathfinding and movement planning.
    /// </summary>
    public static event PathCostInterceptor OnGetTotalPathCost;

    /// <summary>
    /// Fires after MovementType.GetTurnSpeed() computes turn animation speed.
    /// </summary>
    public static event MovementFloatInterceptor OnGetTurnSpeed;

    /// <summary>
    /// Fires after MovementType.GetSlowdownDistance() computes deceleration distance.
    /// </summary>
    public static event MovementFloatInterceptor OnGetSlowdownDistance;

    /// <summary>
    /// Fires after MovementType.GetMaxAngleTurnSlowdown() computes turn penalty.
    /// </summary>
    public static event MovementFloatInterceptor OnGetMaxAngleTurnSlowdown;

    /// <summary>
    /// Fires after MovementType.ClipPathToCost() clips a path to a max AP budget.
    /// Used to limit movement to available action points.
    /// </summary>
    public static event ClipPathInterceptor OnClipPathToCost;

    /// <summary>
    /// Fires when PathfindingProcess.FindPath() calculates a route.
    /// Address: 0x180660c20
    ///
    /// Fires before pathfinding algorithm executes, allowing complete control over path calculation.
    /// Setting cancel=true will prevent pathfinding and return no path found.
    ///
    /// Use Cases:
    /// - Custom pathfinding algorithms (override with pathResult modification)
    /// - Restricted zones (cancel pathfinding through forbidden areas)
    /// - Forced routes (inject predetermined paths)
    /// - Movement tracking (log all pathfinding requests for AI analysis)
    /// - Pathfinding debugging (inspect start/end tiles)
    ///
    /// Note: This is a low-level hook that fires for ALL pathfinding including AI.
    /// </summary>
    /// <param name="process">The pathfinding process instance</param>
    /// <param name="start">Start tile for the path</param>
    /// <param name="end">End/destination tile for the path</param>
    /// <param name="pathResult">Pointer to path result (can be modified)</param>
    /// <param name="cancel">Set to true to cancel pathfinding</param>
    public static event FindPathInterceptor OnPathfinding;

    /// <summary>
    /// Fires BEFORE Actor.MoveTo() executes movement.
    /// This is the master movement hook that catches ALL actor movement attempts.
    /// Address: 0x1805e0a60
    ///
    /// Fires before pathfinding validation, AP deduction, and movement execution.
    /// Setting cancel=true will prevent movement entirely with no state changes.
    ///
    /// Use Cases:
    /// - Movement restrictions (terrain, faction, mission objectives)
    /// - Teleportation (modify destination tile)
    /// - Terrain penalties (modify movement flags)
    /// - Movement tracking (analytics, achievements)
    ///
    /// Note: Game also fires InvokeOnMovement during movement execution.
    /// This event fires BEFORE (prefix), game event fires DURING.
    /// </summary>
    /// <param name="actor">The actor attempting to move</param>
    /// <param name="tile">The destination tile</param>
    /// <param name="flags">Movement flags (mode, sprint, etc.)</param>
    /// <param name="cancel">Set to true to prevent movement</param>
    public static event MoveToInterceptor OnMoveTo;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  STRATEGY LAYER EVENTS - UnitLeaderAttributes, BaseUnitLeader, Vehicle
    // ═══════════════════════════════════════════════════════════════════

    #region Strategy: UnitLeaderAttributes Events

    /// <summary>
    /// Fires after UnitLeaderAttributes.GetActionPoints() computes base AP.
    /// This is the base action points for the unit leader on the strategy map.
    /// </summary>
    public static event StrategyIntIntercept OnStrategyGetActionPoints;

    /// <summary>
    /// Fires after UnitLeaderAttributes.GetHitpointsPerElement() computes HP per element.
    /// Determines hitpoints for each element/soldier in the squad.
    /// </summary>
    public static event StrategyIntIntercept OnStrategyGetHitpointsPerElement;

    /// <summary>
    /// Fires after UnitLeaderAttributes.GetDamageSustainedMult() computes damage taken modifier.
    /// Multiplier applied to incoming damage (lower = more resistant).
    /// </summary>
    public static event StrategyFloatIntercept OnStrategyGetDamageSustainedMult;

    #endregion

    #region Strategy: BaseUnitLeader Events

    /// <summary>
    /// Fires after BaseUnitLeader.GetHitpointsPct() computes leader health percentage.
    /// Returns current health as a percentage (0.0 to 1.0).
    /// </summary>
    public static event StrategyFloatIntercept OnStrategyGetHitpointsPct;

    /// <summary>
    /// Fires after BaseUnitLeader.CanBePromoted() determines promotion eligibility.
    /// Set to true to allow promotion, false to prevent.
    /// </summary>
    public static event StrategyBoolIntercept OnStrategyCanBePromoted;

    /// <summary>
    /// Fires after BaseUnitLeader.CanBeDemoted() determines demotion eligibility.
    /// Set to true to allow demotion, false to prevent.
    /// </summary>
    public static event StrategyBoolIntercept OnStrategyCanBeDemoted;

    /// <summary>
    /// Fires after BaseUnitLeader.GetEntityProperty() retrieves a generic property.
    /// This is a flexible property getter that takes an EntityPropertyType parameter.
    /// </summary>
    public static event StrategyEntityIntercept OnStrategyGetEntityProperty;

    #endregion

    #region Strategy: Vehicle Events

    /// <summary>
    /// Fires after Vehicle.GetArmor() computes vehicle armor value.
    /// Returns the armor rating for the vehicle on the strategy map.
    /// </summary>
    public static event StrategyIntIntercept OnStrategyGetVehicleArmor;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  COMBAT ACTION EVENTS - Damage and effect application
    // ═══════════════════════════════════════════════════════════════════

    #region Combat Action Events

    /// <summary>
    /// Fires when DamageHandler.ApplyDamage() executes, applying damage to an entity.
    /// This is the core damage application intercept point in the game's combat system.
    ///
    /// Firing Point: During skill effect execution, when the Damage effect handler processes
    ///
    /// Damage Calculation (from decompiled code @ 0x180702970):
    /// - HP damage = DamageFlatAmount (0x64) + max(currentHP * pctCurrent, minCurrent) + max(maxHP * pctMax, minMax)
    /// - Hit count = FlatDamageBase (0x5c) + ceil(elementCount * elementsHitPct)
    /// - Armor damage = DamageToArmor (0x78) + (currentArmor * ArmorDmgPctCurrent (0x7c))
    ///
    /// Use Cases:
    /// - Critical hits: Multiply damage on random chance
    /// - Damage immunity: Cancel damage for specific units or conditions
    /// - Damage reflection: Apply partial damage back to attacker
    /// - Damage logging: Track all damage events for analytics
    /// - Conditional resistance: Reduce damage based on armor, buffs, or distance
    ///
    /// Example:
    /// <code>
    /// // 10% critical hit chance with 2x damage
    /// Intercept.OnDamageApplied += (handler, target, attacker, skill, ref damage, ref cancel) => {
    ///     if (Random.value < 0.1f) {
    ///         damage *= 2.0f;
    ///         DevConsole.Log("CRITICAL HIT!");
    ///     }
    /// };
    ///
    /// // Boss immunity
    /// Intercept.OnDamageApplied += (handler, target, attacker, skill, ref damage, ref cancel) => {
    ///     if (target.GetName().Contains("Boss")) {
    ///         cancel = true;
    ///     }
    /// };
    /// </code>
    /// </summary>
    public static event DamageApplicationInterceptor OnDamageApplied;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  EQUIPMENT SYSTEMS - Item container and inventory management
    // ═══════════════════════════════════════════════════════════════════

    #region Equipment Systems

    /// <summary>
    /// Fired when an item is added to a container.
    /// Address: 0x180821c80
    /// Use for equipment restrictions, class-based loadouts, inventory limits.
    ///
    /// Use cases:
    /// - Equipment restrictions: Prevent certain items from being equipped
    /// - Class-based loadouts: Restrict items based on unit class
    /// - Inventory management: Implement custom slot logic or weight limits
    /// - Item type filtering: Block specific item types from containers
    /// - Slot expansion control: Override auto-expand behavior
    ///
    /// Example:
    /// <code>
    /// // Prevent heavy weapons for scout class
    /// Intercept.OnItemAdd += (container, item, ref expandSlots, ref cancel) => {
    ///     var owner = container.GetOwner();
    ///     if (owner?.GetClass() == "Scout") {
    ///         var template = item.GetTemplate();
    ///         if (template?.GetItemType() == ItemType.HeavyWeapon) {
    ///             cancel = true;
    ///             DevConsole.Log("Scouts cannot use heavy weapons!");
    ///         }
    ///     }
    /// };
    ///
    /// // Disable auto-expand for specific containers
    /// Intercept.OnItemAdd += (container, item, ref expandSlots, ref cancel) => {
    ///     if (container.GetName().Contains("LimitedStorage")) {
    ///         expandSlots = false;
    ///     }
    /// };
    /// </code>
    /// </summary>
    /// <param name="container">The item container</param>
    /// <param name="item">The item being added</param>
    /// <param name="expandSlots">Whether to expand slots if full (can be modified)</param>
    /// <param name="cancel">Set to true to prevent item addition</param>
    public static event ItemAddInterceptor OnItemAdd;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  AI BEHAVIOR EVENTS - AIBrain decision making
    // ═══════════════════════════════════════════════════════════════════

    #region AI Behavior Events

    /// <summary>
    /// Fires after AI evaluates attack scoring for a target.
    /// Higher values make the target more attractive.
    /// </summary>
    public static event AIFloatValueInterceptor OnAIGetAttackScore;

    /// <summary>
    /// Fires after AI evaluates threat level of a target.
    /// Higher values indicate more dangerous targets.
    /// </summary>
    public static event AIFloatValueInterceptor OnAIGetThreatValue;

    /// <summary>
    /// Fires after AI evaluates priority of an action.
    /// Higher values increase likelihood of action being chosen.
    /// </summary>
    public static event AIIntValueInterceptor OnAIGetActionPriority;

    /// <summary>
    /// Fires after AI decides whether to flee.
    /// Return true to force flee, false to prevent.
    /// </summary>
    public static event AIBoolDecisionInterceptor OnAIShouldFlee;

    /// <summary>
    /// Fired when AI agent begins evaluating possible actions for this turn.
    /// WARNING: May be called in parallel - ensure thread-safe handlers!
    /// Use this to implement AI difficulty modifiers or force specific behaviors.
    ///
    /// Lua event name: ai_evaluate
    /// Address: Menace.Tactical.AI.Agent$$Evaluate @ 0x18070eb30
    ///
    /// Threading: This function uses System.Threading.Tasks for parallel criterion evaluation.
    /// Multiple agents may call this simultaneously. Handler code MUST be thread-safe.
    /// </summary>
    /// <param name="agent">The AI agent evaluating actions</param>
    /// <param name="cancel">Set to true to cancel evaluation and skip AI turn</param>
    public static event AgentEvaluateInterceptor OnAIEvaluate;

    /// <summary>
    /// Fired when AI criterion evaluates a position/tile for movement or deployment.
    /// WARNING: Called in PARALLEL - handlers MUST be thread-safe and read-only!
    /// This fires for EACH tile evaluation during pathfinding (20-50+ tiles per agent).
    /// Keep handlers EXTREMELY fast - avoid heavy computation, I/O, or state modification.
    ///
    /// Lua event name: position_score
    /// Address: Menace.Tactical.AI.Behaviors.Criterions.* @ various (see specific implementations)
    ///
    /// Threading: Called from parallel job threads during criterion evaluation.
    /// Multiple criterions evaluate multiple tiles simultaneously.
    /// DO NOT modify game state - only read and modify the score parameter.
    ///
    /// Use cases:
    /// - Custom positioning logic (e.g., prefer high ground)
    /// - Flanking preferences (bonus for attacking from sides/rear)
    /// - Environmental bonuses (stay near cover, water, etc.)
    /// - Formation-based scoring (maintain unit cohesion)
    /// </summary>
    /// <param name="criterion">The criterion evaluating the position</param>
    /// <param name="tile">The tile being scored</param>
    /// <param name="score">The position score (can be modified)</param>
    public static event CriterionEvaluateInterceptor OnPositionScore;

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize the Intercept system.
    /// Call this from ModpackLoaderMod after game assembly is loaded.
    /// </summary>
    /// <param name="harmony">Harmony instance for patching</param>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;

        _harmony = harmony;

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                SdkLogger.Warning("[Intercept] Assembly-CSharp not found");
                return;
            }

            // Cache types for postfix patches
            _entityPropertiesType = gameAssembly.GetType("Menace.Tactical.EntityProperties");
            _skillType = gameAssembly.GetType("Menace.Tactical.Skills.Skill");
            _actorType = gameAssembly.GetType("Menace.Tactical.Actor");
            _entityType = gameAssembly.GetType("Menace.Tactical.Entity");
            _tileType = gameAssembly.GetType("Menace.Tactical.Tile");
            _baseTileType = gameAssembly.GetType("Menace.Tactical.BaseTile");
            _lineOfSightType = gameAssembly.GetType("Tactical.LineOfSight");
            // Movement types
            _movementTypeType = gameAssembly.GetType("Menace.Tactical.MovementType");
            // Strategy layer types
            _unitLeaderAttributesType = gameAssembly.GetType("Menace.Strategy.UnitLeaderAttributes");
            _baseUnitLeaderType = gameAssembly.GetType("Menace.Strategy.BaseUnitLeader");
            _vehicleType = gameAssembly.GetType("Menace.Strategy.Vehicle");
            // AI types
            _aiBrainType = gameAssembly.GetType("Menace.Tactical.AI.AIBrain");
            _agentType = gameAssembly.GetType("Menace.Tactical.AI.Agent");
            // Pathfinding types
            _pathfindingProcessType = gameAssembly.GetType("Menace.Tactical.PathfindingProcess");

            if (_entityPropertiesType == null)
            {
                SdkLogger.Warning("[Intercept] EntityProperties type not found");
                return;
            }

            // Apply Tier 1 patches
            int patchCount = 0;

            // EntityProperties patches - Tier 1
            patchCount += PatchEntityPropertyMethod("GetDamage", nameof(GetDamage_Postfix));
            patchCount += PatchEntityPropertyMethod("GetAccuracy", nameof(GetAccuracy_Postfix));
            patchCount += PatchEntityPropertyMethod("GetArmor", nameof(GetArmor_Postfix));
            patchCount += PatchEntityPropertyMethod("GetConcealment", nameof(GetConcealment_Postfix));
            patchCount += PatchEntityPropertyMethod("GetDetection", nameof(GetDetection_Postfix));
            patchCount += PatchEntityPropertyMethod("GetVision", nameof(GetVision_Postfix));

            // EntityProperties patches - Tier 2: Combat dropoff and penetration
            patchCount += PatchEntityPropertyMethod("GetDamageDropoff", nameof(GetDamageDropoff_Postfix));
            patchCount += PatchEntityPropertyMethod("GetDamageToArmorDurability", nameof(GetDamageToArmorDurability_Postfix));
            patchCount += PatchEntityPropertyMethod("GetDamageToArmorDurabilityDropoff", nameof(GetDamageToArmorDurabilityDropoff_Postfix));
            patchCount += PatchEntityPropertyMethod("GetAccuracyDropoff", nameof(GetAccuracyDropoff_Postfix));
            patchCount += PatchEntityPropertyMethod("GetArmorPenetration", nameof(GetArmorPenetration_Postfix));
            patchCount += PatchEntityPropertyMethod("GetArmorPenetrationDropoff", nameof(GetArmorPenetrationDropoff_Postfix));
            patchCount += PatchEntityPropertyMethod("GetSuppression", nameof(GetSuppression_Postfix));
            patchCount += PatchEntityPropertyMethod("GetDiscipline", nameof(GetDiscipline_Postfix));
            patchCount += PatchEntityPropertyMethod("GetHitpointsPerElement", nameof(GetHitpointsPerElement_Postfix));
            patchCount += PatchEntityPropertyMethod("GetMaxHitpoints", nameof(GetMaxHitpoints_Postfix));
            patchCount += PatchEntityPropertyMethod("GetActionPoints", nameof(GetActionPoints_Postfix));
            patchCount += PatchEntityPropertyMethod("GetMovementCostModifier", nameof(GetMovementCostModifier_Postfix));
            patchCount += PatchEntityPropertyMethodWithParam("GetPropertyValue", nameof(GetPropertyValue_Postfix));
            patchCount += PatchEntityPropertyMethodWithParamPrefix("UpdateProperty", nameof(UpdateProperty_Prefix));
            patchCount += PatchEntityPropertyMethodWithParamPrefix("UpdateMultProperty", nameof(UpdateMultProperty_Prefix));

            // Skill patches (Tier 1)
            if (_skillType != null)
            {
                patchCount += PatchSkillMethod("GetHitchance", nameof(GetHitchance_Postfix));
                patchCount += PatchSkillMethod("GetCoverMult", nameof(GetCoverMult_Postfix));
            // TODO Phase 5:                 // GetExpectedDamage has overloads - patch both
                patchCount += PatchSkillMethodOverloads("GetExpectedDamage", nameof(GetExpectedDamage_Postfix));

                // Tier 2: Skill range and cost patches
                patchCount += PatchSkillMethodOverloads("GetExpectedSuppression", nameof(GetExpectedSuppression_Postfix));
                patchCount += PatchSkillMethod("GetActionPointCost", nameof(GetActionPointCost_Postfix));
                patchCount += PatchSkillMethod("GetIdealRangeBase", nameof(GetIdealRangeBase_Postfix));
                patchCount += PatchSkillMethod("GetMaxRangeBase", nameof(GetMaxRangeBase_Postfix));
                patchCount += PatchSkillMethod("GetMinRangeBase", nameof(GetMinRangeBase_Postfix));
                patchCount += PatchSkillMethod("IsInRange", nameof(IsInRange_Postfix));
                patchCount += PatchSkillMethod("IsInRangeShape", nameof(IsInRangeShape_Postfix));
                patchCount += PatchSkillMethod("IsMovementSkill", nameof(IsMovementSkill_Postfix));
                patchCount += PatchSkillMethod("IsUsable", nameof(IsUsable_Postfix));
            }

            // Actor patches
            if (_actorType != null)
            {
                patchCount += PatchActorMethod("HasLineOfSightTo", nameof(HasLineOfSightTo_Postfix));
                patchCount += PatchActorMethodPrefix("ApplySuppression", nameof(ApplySuppression_Prefix));
                // Actor action interceptors - Morale/Suppression application
                patchCount += PatchActorMethodPrefix("ApplyMorale", nameof(ApplyMorale_Prefix));
                // Actor movement interceptor - Phase 2
                patchCount += PatchActorMethodPrefix("MoveTo", nameof(MoveTo_Prefix));
                // TODO: Actor state interceptors (Morale, Suppression, ActionPoints, Boolean queries)
                // require postfix implementations before enabling
            }

            // Entity state patches
            if (_entityType != null)
            {
                patchCount += PatchEntityMethod("GetHitpointsPct", nameof(GetHitpointsPct_Postfix));
                patchCount += PatchEntityMethod("GetArmorDurabilityPct", nameof(GetArmorDurabilityPct_Postfix));
                patchCount += PatchEntityMethod("GetCoverUsage", nameof(GetCoverUsage_Postfix));
                patchCount += PatchEntityMethod("GetProvidedCover", nameof(GetProvidedCover_Postfix));
                patchCount += PatchEntityMethod("IsDiscovered", nameof(IsDiscovered_Postfix));
                patchCount += PatchEntityMethod("GetLastSkillUsed", nameof(GetLastSkillUsed_Postfix));
                patchCount += PatchEntityMethod("GetScaleRange", nameof(GetScaleRange_Postfix));
            }

            // Tile patches
            if (_tileType != null)
            {
                patchCount += PatchTileMethod("HasLineOfSightTo", nameof(TileHasLineOfSightTo_Postfix));
                patchCount += PatchTileMethod("IsBlockingLineOfSight", nameof(TileIsBlockingLineOfSight_Postfix));
                patchCount += PatchTileMethod("GetCover", nameof(TileGetCover_Postfix));
                patchCount += PatchTileMethod("GetCoverMask", nameof(TileGetCoverMask_Postfix));
                patchCount += PatchTileMethod("GetEntityProvidedCover", nameof(TileGetEntityProvidedCover_Postfix));
                patchCount += PatchTileMethod("CanBeEntered", nameof(TileCanBeEntered_Postfix));
                patchCount += PatchTileMethod("CanBeEnteredBy", nameof(TileCanBeEnteredBy_Postfix));
            }

            // BaseTile patches
            if (_baseTileType != null)
            {
                patchCount += PatchBaseTileMethod("HasCover", nameof(BaseTileHasCover_Postfix));
                patchCount += PatchBaseTileMethod("HasHalfCover", nameof(BaseTileHasHalfCover_Postfix));
                patchCount += PatchBaseTileMethod("HasHalfCoverInDir", nameof(BaseTileHasHalfCoverInDir_Postfix));
                patchCount += PatchBaseTileMethod("IsMovementBlocked", nameof(BaseTileIsMovementBlocked_Postfix));
            }

            // PathfindingProcess patches
            if (_pathfindingProcessType != null)
            {
                patchCount += PatchPathfindingMethod(_pathfindingProcessType, "FindPath", nameof(FindPath_Prefix));
                patchCount += PatchPathfindingProcessMethod("IsTraversable", nameof(PathfindingProcessIsTraversable_Postfix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] PathfindingProcess not found - pathfinding patches skipped");
            }

            // LineOfSight patches (static methods)
            if (_lineOfSightType != null)
            {
                patchCount += PatchLineOfSightStaticMethod("HasLineOfSight", nameof(LineOfSightRayTrace_Postfix));
                patchCount += PatchLineOfSightStaticMethod("IsNearTileCorner", nameof(LineOfSightIsNearTileCorner_Postfix));
            }
            else
            {
                // Try alternative namespace
                _lineOfSightType = gameAssembly.GetType("Menace.Tactical.LineOfSight");
                if (_lineOfSightType != null)
                {
                    patchCount += PatchLineOfSightStaticMethod("HasLineOfSight", nameof(LineOfSightRayTrace_Postfix));
                    patchCount += PatchLineOfSightStaticMethod("IsNearTileCorner", nameof(LineOfSightIsNearTileCorner_Postfix));
                }
            }

            // Apply Movement patches
            if (_movementTypeType != null)
            {
                patchCount += PatchMovementTypeMethod("GetMaxMovementSpeed", nameof(GetMaxMovementSpeed_Postfix));
                patchCount += PatchMovementTypeMethod("GetTotalPathCost", nameof(GetTotalPathCost_Postfix));
                patchCount += PatchMovementTypeMethod("GetTurnSpeed", nameof(GetTurnSpeed_Postfix));
                patchCount += PatchMovementTypeMethod("GetSlowdownDistance", nameof(GetSlowdownDistance_Postfix));
                patchCount += PatchMovementTypeMethod("GetMaxAngleTurnSlowdown", nameof(GetMaxAngleTurnSlowdown_Postfix));
                patchCount += PatchMovementTypeMethod("ClipPathToCost", nameof(ClipPathToCost_Postfix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] MovementType not found - movement patches skipped");
            }

            // Apply Strategy layer patches
            if (_unitLeaderAttributesType != null)
            {
                patchCount += PatchStrategyMethod(_unitLeaderAttributesType, "GetActionPoints", nameof(StrategyGetActionPoints_Postfix));
                patchCount += PatchStrategyMethod(_unitLeaderAttributesType, "GetHitpointsPerElement", nameof(StrategyGetHitpointsPerElement_Postfix));
                patchCount += PatchStrategyMethod(_unitLeaderAttributesType, "GetDamageSustainedMult", nameof(StrategyGetDamageSustainedMult_Postfix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] UnitLeaderAttributes not found - Strategy patches skipped");
            }

            if (_baseUnitLeaderType != null)
            {
                patchCount += PatchStrategyMethod(_baseUnitLeaderType, "GetHitpointsPct", nameof(StrategyGetHitpointsPct_Postfix));
                patchCount += PatchStrategyMethod(_baseUnitLeaderType, "CanBePromoted", nameof(StrategyCanBePromoted_Postfix));
                patchCount += PatchStrategyMethod(_baseUnitLeaderType, "CanBeDemoted", nameof(StrategyCanBeDemoted_Postfix));
                patchCount += PatchStrategyMethodWithParam(_baseUnitLeaderType, "GetEntityProperty", nameof(StrategyGetEntityProperty_Postfix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] BaseUnitLeader not found - Strategy patches skipped");
            }

            if (_vehicleType != null)
            {
                patchCount += PatchStrategyMethod(_vehicleType, "GetArmor", nameof(StrategyGetVehicleArmor_Postfix));
            }

            // Apply AI behavior patches
            if (_aiBrainType != null)
            {
                patchCount += PatchAIMethod("GetAttackScore", nameof(AIGetAttackScore_Postfix));
                patchCount += PatchAIMethod("GetThreatValue", nameof(AIGetThreatValue_Postfix));
                patchCount += PatchAIMethod("GetActionPriority", nameof(AIGetActionPriority_Postfix));
                patchCount += PatchAIMethod("ShouldFlee", nameof(AIShouldFlee_Postfix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] AIBrain not found - AI patches skipped");
            }

            // Apply Agent patches
            if (_agentType != null)
            {
                patchCount += PatchAgentMethod("Evaluate", nameof(AgentEvaluate_Prefix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] Agent not found - Agent.Evaluate patch skipped");
            }

            // Apply Criterion.Evaluate patches
            patchCount += PatchCriterionEvaluate(gameAssembly);

            // Apply equipment system patches
            var itemContainerType = gameAssembly.GetType("Menace.Items.ItemContainer");
            if (itemContainerType != null)
            {
                patchCount += PatchItemContainerMethod(itemContainerType, "Add", nameof(ItemContainerAdd_Prefix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] ItemContainer not found - item add intercept skipped");
            }

            // Apply combat action patches
            var damageHandlerType = gameAssembly.GetType("Menace.Tactical.Skills.Effects.DamageHandler");
            if (damageHandlerType != null)
            {
                patchCount += PatchDamageHandlerMethod(damageHandlerType, "ApplyDamage", nameof(ApplyDamage_Prefix));
            }
            else
            {
                SdkLogger.Warning("[Intercept] DamageHandler not found - damage intercept skipped");
            }

            _initialized = true;
            SdkLogger.Msg($"[Intercept] Initialized with {patchCount} property hooks");

            // Register console commands for debugging
            RegisterConsoleCommands();
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[Intercept] Failed to initialize: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PATCH HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private static int PatchEntityPropertyMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _entityPropertiesType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: EntityProperties.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch EntityProperties.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchEntityPropertyMethodWithParam(string methodName, string patchMethodName)
    {
        try
        {
            // Find method with EntityPropertyType parameter
            var targetMethod = _entityPropertiesType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length >= 1);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: EntityProperties.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch EntityProperties.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchEntityPropertyMethodWithParamPrefix(string methodName, string patchMethodName)
    {
        try
        {
            // Find method with parameters (propertyType, multiplier)
            var targetMethod = _entityPropertiesType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length >= 2);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: EntityProperties.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            SdkLogger.Msg($"[Intercept] Patched EntityProperties.{methodName} @ 0x18060cc80 (Prefix)");
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch EntityProperties.{methodName} (prefix): {ex.Message}");
            return 0;
        }
    }

    private static int PatchSkillMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _skillType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Skill.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Skill.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchSkillMethodOverloads(string methodName, string patchMethodName)
    {
        try
        {
            var methods = _skillType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName)
                .ToList();

            if (methods.Count == 0)
            {
                SdkLogger.Warning($"[Intercept] No overloads found: Skill.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            int count = 0;
            foreach (var method in methods)
            {
                try
                {
                    _harmony.Patch(method, postfix: new HarmonyMethod(patchMethod));
                    count++;
                }
                catch
                {
                    // Some overloads may not be patchable, continue
                }
            }
            return count;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Skill.{methodName} overloads: {ex.Message}");
            return 0;
        }
    }

    private static int PatchActorMethod(string methodName, string patchMethodName)
    {
        try
        {
            // HasLineOfSightTo has specific signature, find the right overload
            var targetMethod = _actorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length >= 1);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Actor.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Actor.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchActorMethodSimple(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _actorType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);

            if (targetMethod == null)
            {
                // Try without parameter restriction
                targetMethod = _actorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
            }

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Actor.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Actor.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchActorMethodWithParam(string methodName, string patchMethodName, Type paramType)
    {
        try
        {
            var targetMethod = _actorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == paramType);

            if (targetMethod == null)
            {
                // Try any single-parameter overload
                targetMethod = _actorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 1);
            }

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Actor.{methodName}({paramType.Name})");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Actor.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchActorMethodPrefix(string methodName, string patchMethodName)
    {
        try
        {
            // Find the method with any number of parameters
            var targetMethod = _actorType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Actor.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Actor.{methodName} (prefix): {ex.Message}");
            return 0;
        }
    }

    private static int PatchEntityMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _entityType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Entity.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Entity.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchTileMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _tileType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Tile.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Tile.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchBaseTileMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _baseTileType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: BaseTile.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch BaseTile.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchLineOfSightStaticMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _lineOfSightType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: LineOfSight.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch LineOfSight.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchMovementTypeMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _movementTypeType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: MovementType.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch MovementType.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchPathfindingProcessMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _pathfindingProcessType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: PathfindingProcess.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch PathfindingProcess.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchStrategyMethod(Type strategyType, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = strategyType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: {strategyType.Name}.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch {strategyType.Name}.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchStrategyMethodWithParam(Type strategyType, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = strategyType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length >= 1);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: {strategyType.Name}.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch {strategyType.Name}.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchAIMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _aiBrainType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: AIBrain.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch AIBrain.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchDamageHandlerMethod(Type handlerType, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = handlerType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: DamageHandler.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            // Use Prefix patch to intercept before damage is applied
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            SdkLogger.Msg($"[Intercept] Patched DamageHandler.{methodName} @ 0x180702970");
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch DamageHandler.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchItemContainerMethod(Type containerType, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = containerType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: ItemContainer.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            // Use Prefix patch to intercept before item is added
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            SdkLogger.Msg($"[Intercept] Patched ItemContainer.{methodName} @ 0x180821c80");
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch ItemContainer.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchPathfindingMethod(Type pathfindingType, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = pathfindingType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: PathfindingProcess.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            // Use Prefix patch to intercept before pathfinding executes
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            SdkLogger.Msg($"[Intercept] Patched PathfindingProcess.{methodName} @ 0x180660c20");
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch PathfindingProcess.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchAgentMethod(string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _agentType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Method not found: Agent.{methodName}");
                return 0;
            }

            var patchMethod = typeof(Intercept).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[Intercept] Patch method not found: {patchMethodName}");
                return 0;
            }

            // Use Prefix patch to intercept before evaluation starts (allows cancellation)
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            SdkLogger.Msg($"[Intercept] Patched Agent.{methodName} @ 0x18070eb30");
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Agent.{methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchCriterionEvaluate(Assembly gameAssembly)
    {
        try
        {
            int patchCount = 0;

            // Patch concrete Criterion implementations found via Ghidra
            string[] criterionTypes = new[]
            {
                "Menace.Tactical.AI.Behaviors.Criterions.AvoidOpponents",
                "Menace.Tactical.AI.Behaviors.Criterions.DistanceToCurrentTile",
                "Menace.Tactical.AI.Behaviors.Criterions.FleeFromOpponents",
                "Menace.Tactical.AI.Behaviors.Criterions.ThreatFromOpponents",
                "Menace.Tactical.AI.Behaviors.Criterions.CoverAgainstOpponents",
                "Menace.Tactical.AI.Behaviors.Criterions.ConsiderZones",
                "Menace.Tactical.AI.Behaviors.Criterions.ExistingTileEffects"
            };

            foreach (var typeName in criterionTypes)
            {
                var criterionType = gameAssembly.GetType(typeName);
                if (criterionType == null)
                {
                    SdkLogger.Warning($"[Intercept] Criterion type not found: {typeName}");
                    continue;
                }

                var evaluateMethod = criterionType.GetMethod("Evaluate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (evaluateMethod == null)
                {
                    SdkLogger.Warning($"[Intercept] Evaluate method not found on {typeName}");
                    continue;
                }

                var patchMethod = typeof(Intercept).GetMethod(nameof(CriterionEvaluate_Postfix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (patchMethod == null)
                {
                    SdkLogger.Warning("[Intercept] CriterionEvaluate_Postfix patch method not found");
                    break;
                }

                // Use Postfix - observe and modify score after evaluation
                _harmony.Patch(evaluateMethod, postfix: new HarmonyMethod(patchMethod));
                patchCount++;
            }

            if (patchCount > 0)
            {
                SdkLogger.Msg($"[Intercept] Patched {patchCount} Criterion.Evaluate implementations");
            }
            else
            {
                SdkLogger.Warning("[Intercept] No Criterion.Evaluate patches applied");
            }

            return patchCount;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Intercept] Failed to patch Criterion.Evaluate: {ex.Message}");
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    private static IntPtr GetPointer(object obj)
    {
        if (obj == null) return IntPtr.Zero;
        if (obj is Il2CppObjectBase il2cppObj)
            return il2cppObj.Pointer;
        return IntPtr.Zero;
    }

    /// <summary>
    /// Attempt to resolve the Entity that owns an EntityProperties instance.
    /// This is a best-effort lookup - may return null in some contexts.
    /// </summary>
    private static GameObj TryResolveOwner(IntPtr propsPtr)
    {
        // EntityProperties doesn't have a direct back-reference to its owner
        // In combat calculations, the owner is typically available as a separate parameter
        // This method exists for cases where we only have the properties pointer

        // For now, return null - owner resolution happens in the patch methods
        // where context is available
        return GameObj.Null;
    }

    /// <summary>
    /// Fire a Lua event for property interception.
    /// </summary>
    private static void FireLuaEvent(string eventName, Dictionary<string, object> data)
    {
        try
        {
            LuaScriptEngine.Instance?.FireEventWithTable(eventName, data);
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"Lua event '{eventName}' failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - EntityProperties
    // ═══════════════════════════════════════════════════════════════════

    #region EntityProperties Postfixes

    private static void GetDamage_Postfix(object __instance, ref float __result)
    {
        if (OnGetDamage == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDamage.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDamage handler failed: {ex.Message}");
                }
            }

            __result = result;

            // Fire Lua event
            FireLuaEvent("property_damage", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDamage_Postfix failed: {ex.Message}");
        }
    }

    private static void GetAccuracy_Postfix(object __instance, ref float __result)
    {
        if (OnGetAccuracy == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetAccuracy.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetAccuracy handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_accuracy", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetAccuracy_Postfix failed: {ex.Message}");
        }
    }

    private static void GetArmor_Postfix(object __instance, ref int __result)
    {
        if (OnGetArmor == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetArmor.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetArmor handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_armor", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetArmor_Postfix failed: {ex.Message}");
        }
    }

    private static void GetConcealment_Postfix(object __instance, ref int __result)
    {
        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            // Run C# handlers
            if (OnGetConcealment != null)
            {
                foreach (var handler in OnGetConcealment.GetInvocationList().Cast<IntIntercept>())
                {
                    try
                    {
                        handler(props, owner, ref result);
                    }
                    catch (Exception ex)
                    {
                        ModError.WarnInternal("Intercept", $"OnGetConcealment handler failed: {ex.Message}");
                    }
                }
            }

            // Apply EffectSystem modifiers
            var ownerPtr = owner.IsNull ? 0L : owner.Pointer.ToInt64();
            if (!owner.IsNull)
            {
                result += EffectSystem.GetModifier(owner.Pointer, "concealment");
            }

            // Run Lua interceptors - they can modify the value
            result = LuaScriptEngine.Instance?.InvokeLuaInterceptors("concealment", ownerPtr, result) ?? result;

            __result = result;

            FireLuaEvent("property_concealment", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["owner_ptr"] = ownerPtr,
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetConcealment_Postfix failed: {ex.Message}");
        }
    }

    private static void GetDetection_Postfix(object __instance, ref int __result)
    {
        if (OnGetDetection == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDetection.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDetection handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_detection", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDetection_Postfix failed: {ex.Message}");
        }
    }

    private static void GetVision_Postfix(object __instance, ref int __result)
    {
        if (OnGetVision == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetVision.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetVision handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_vision", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetVision_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - EntityProperties (Tier 2)
    // ═══════════════════════════════════════════════════════════════════

    #region EntityProperties Postfixes (Tier 2)

    private static void GetDamageDropoff_Postfix(object __instance, ref float __result)
    {
        if (OnGetDamageDropoff == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDamageDropoff.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDamageDropoff handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_damage_dropoff", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDamageDropoff_Postfix failed: {ex.Message}");
        }
    }

    private static void GetDamageToArmorDurability_Postfix(object __instance, ref float __result)
    {
        if (OnGetDamageToArmorDurability == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDamageToArmorDurability.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDamageToArmorDurability handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_damage_to_armor_durability", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDamageToArmorDurability_Postfix failed: {ex.Message}");
        }
    }

    private static void GetDamageToArmorDurabilityDropoff_Postfix(object __instance, ref float __result)
    {
        if (OnGetDamageToArmorDurabilityDropoff == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDamageToArmorDurabilityDropoff.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDamageToArmorDurabilityDropoff handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_damage_to_armor_durability_dropoff", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDamageToArmorDurabilityDropoff_Postfix failed: {ex.Message}");
        }
    }

    private static void GetAccuracyDropoff_Postfix(object __instance, ref float __result)
    {
        if (OnGetAccuracyDropoff == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetAccuracyDropoff.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetAccuracyDropoff handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_accuracy_dropoff", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetAccuracyDropoff_Postfix failed: {ex.Message}");
        }
    }

    private static void GetArmorPenetration_Postfix(object __instance, ref float __result)
    {
        if (OnGetArmorPenetration == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetArmorPenetration.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetArmorPenetration handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_armor_penetration", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetArmorPenetration_Postfix failed: {ex.Message}");
        }
    }

    private static void GetArmorPenetrationDropoff_Postfix(object __instance, ref float __result)
    {
        if (OnGetArmorPenetrationDropoff == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetArmorPenetrationDropoff.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetArmorPenetrationDropoff handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_armor_penetration_dropoff", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetArmorPenetrationDropoff_Postfix failed: {ex.Message}");
        }
    }

    private static void GetSuppression_Postfix(object __instance, ref float __result)
    {
        if (OnGetSuppression == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetSuppression.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetSuppression handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_suppression", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetSuppression_Postfix failed: {ex.Message}");
        }
    }

    private static void GetDiscipline_Postfix(object __instance, ref float __result)
    {
        if (OnGetDiscipline == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetDiscipline.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetDiscipline handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_discipline", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetDiscipline_Postfix failed: {ex.Message}");
        }
    }

    private static void GetHitpointsPerElement_Postfix(object __instance, ref int __result)
    {
        if (OnGetHitpointsPerElement == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetHitpointsPerElement.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetHitpointsPerElement handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_hitpoints_per_element", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetHitpointsPerElement_Postfix failed: {ex.Message}");
        }
    }

    private static void GetMaxHitpoints_Postfix(object __instance, ref int __result)
    {
        if (OnGetMaxHitpoints == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetMaxHitpoints.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetMaxHitpoints handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_max_hitpoints", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetMaxHitpoints_Postfix failed: {ex.Message}");
        }
    }

    private static void GetActionPoints_Postfix(object __instance, ref int __result)
    {
        if (OnGetActionPoints == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetActionPoints.GetInvocationList().Cast<IntIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetActionPoints handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_action_points", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetActionPoints_Postfix failed: {ex.Message}");
        }
    }

    private static void GetMovementCostModifier_Postfix(object __instance, ref float __result)
    {
        if (OnGetMovementCostModifier == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetMovementCostModifier.GetInvocationList().Cast<FloatIntercept>())
            {
                try
                {
                    handler(props, owner, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetMovementCostModifier handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_movement_cost_modifier", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetMovementCostModifier_Postfix failed: {ex.Message}");
        }
    }

    private static void GetPropertyValue_Postfix(object __instance, ref float __result, int propertyType)
    {
        if (OnGetPropertyValue == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var owner = TryResolveOwner(propsPtr);
            var result = __result;

            foreach (var handler in OnGetPropertyValue.GetInvocationList().Cast<PropertyValueInterceptor>())
            {
                try
                {
                    handler(props, owner, propertyType, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetPropertyValue handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("property_value", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["property_type"] = propertyType,
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetPropertyValue_Postfix failed: {ex.Message}");
        }
    }

    private static void UpdateProperty_Prefix(object __instance, int propertyType, ref int amount)
    {
        if (OnPropertyUpdate == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var amt = amount;

            foreach (var handler in OnPropertyUpdate.GetInvocationList().Cast<PropertyUpdateInterceptor>())
            {
                try
                {
                    handler(props, propertyType, ref amt);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnPropertyUpdate handler failed: {ex.Message}");
                }
            }

            amount = amt;

            FireLuaEvent("property_update", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["property_type"] = propertyType,
                ["amount"] = amt
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"UpdateProperty_Prefix failed: {ex.Message}");
        }
    }

    private static void UpdateMultProperty_Prefix(object __instance, int propertyType, ref float multiplier)
    {
        if (OnPropertyUpdateMult == null) return;

        try
        {
            var propsPtr = GetPointer(__instance);
            var props = new GameObj(propsPtr);
            var mult = multiplier;

            foreach (var handler in OnPropertyUpdateMult.GetInvocationList().Cast<PropertyUpdateMultInterceptor>())
            {
                try
                {
                    handler(props, propertyType, ref mult);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnPropertyUpdateMult handler failed: {ex.Message}");
                }
            }

            multiplier = mult;

            FireLuaEvent("property_update_mult", new Dictionary<string, object>
            {
                ["props_ptr"] = propsPtr.ToInt64(),
                ["property_type"] = propertyType,
                ["multiplier"] = mult
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"UpdateMultProperty_Prefix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - Skill (Tier 1)
    // ═══════════════════════════════════════════════════════════════════

    #region Skill Postfixes (Tier 1)

    private static void GetHitchance_Postfix(object __instance, object __result,
        object fromTile, object toTile, object target, object attackProps, object defenseProps,
        bool useDropoff, object targetEntity, bool targetInsideActor)
    {
        if (OnGetHitChance == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);

            // Extract attacker from skill
            var attacker = GameObj.Null;
            try
            {
                // skill.GetActor() or skill.SkillContainer.Entity
                var skillObj = new GameObj(skillPtr);
                var containerPtr = skillObj.ReadPtr(OFFSET_SKILL_CONTAINER);
                if (containerPtr != IntPtr.Zero)
                {
                    var entityPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(containerPtr + (int)OFFSET_CONTAINER_ENTITY);
                    if (entityPtr != IntPtr.Zero)
                        attacker = new GameObj(entityPtr);
                }
            }
            catch { }

            var targetObj = new GameObj(GetPointer(targetEntity ?? target));

            // Parse result struct - GetHitchance returns a HitchanceResult struct
            // The struct has: FinalChance, BaseAccuracy, CoverMult, DefenseMult, DistancePenalty, flags
            var result = new HitChanceResult();

            if (__result is Il2CppObjectBase resultObj)
            {
                var resultPtr = resultObj.Pointer;
                // HitchanceResult layout (from Ghidra analysis):
                // 0x00: float FinalChance
                // 0x04: float BaseAccuracy
                // 0x08: float CoverMult
                // 0x0C: float DefenseMult
                // 0x10: float DistancePenalty
                // 0x14: byte flags (HasDistanceFalloff, IsGuaranteedHit)
                result.FinalChance = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr);
                result.BaseAccuracy = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr + 4);
                result.CoverMult = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr + 8);
                result.DefenseMult = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr + 12);
                result.DistancePenalty = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr + 20);
                var flags = System.Runtime.InteropServices.Marshal.ReadByte(resultPtr + 16);
                result.IsGuaranteedHit = (flags & 1) != 0;
                result.HasDistanceFalloff = System.Runtime.InteropServices.Marshal.ReadByte(resultPtr + 17) != 0;
            }

            foreach (var handler in OnGetHitChance.GetInvocationList().Cast<HitChanceInterceptor>())
            {
                try
                {
                    handler(skill, attacker, targetObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetHitChance handler failed: {ex.Message}");
                }
            }

            // Write back modified result
            if (__result is Il2CppObjectBase resultObjWrite)
            {
                var resultPtr = resultObjWrite.Pointer;
                System.Runtime.InteropServices.Marshal.StructureToPtr(result.FinalChance, resultPtr, false);
                // Note: We only modify FinalChance; other fields are informational
            }

            FireLuaEvent("skill_hitchance", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["final_chance"] = result.FinalChance,
                ["base_accuracy"] = result.BaseAccuracy,
                ["cover_mult"] = result.CoverMult,
                ["defense_mult"] = result.DefenseMult
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetHitchance_Postfix failed: {ex.Message}");
        }
    }

    private static void GetCoverMult_Postfix(object __instance, ref float __result,
        object fromTile, object toTile, object target, object defenseProps, bool targetInsideActor)
    {
        if (OnGetCoverMult == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var targetObj = new GameObj(GetPointer(target));

            // Extract attacker
            var attacker = GameObj.Null;
            try
            {
                var skillObj = new GameObj(skillPtr);
                var containerPtr = skillObj.ReadPtr(OFFSET_SKILL_CONTAINER);
                if (containerPtr != IntPtr.Zero)
                {
                    var entityPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(containerPtr + (int)OFFSET_CONTAINER_ENTITY);
                    if (entityPtr != IntPtr.Zero)
                        attacker = new GameObj(entityPtr);
                }
            }
            catch { }

            var result = __result;

            foreach (var handler in OnGetCoverMult.GetInvocationList().Cast<CoverMultInterceptor>())
            {
                try
                {
                    handler(skill, attacker, targetObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetCoverMult handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_covermult", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["cover_mult"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetCoverMult_Postfix failed: {ex.Message}");
        }
    }

    private static void GetExpectedDamage_Postfix(object __instance, ref float __result)
    {
        if (OnGetExpectedDamage == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);

            // Limited context in this overload
            var result = new ExpectedDamageResult { Damage = __result };

            foreach (var handler in OnGetExpectedDamage.GetInvocationList().Cast<ExpectedDamageInterceptor>())
            {
                try
                {
                    handler(skill, GameObj.Null, GameObj.Null, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetExpectedDamage handler failed: {ex.Message}");
                }
            }

            __result = result.Damage;

            FireLuaEvent("skill_expected_damage", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["damage"] = result.Damage
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetExpectedDamage_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - Skill (Tier 2)
    // ═══════════════════════════════════════════════════════════════════

    #region Skill Postfixes (Tier 2)

    private static void GetExpectedSuppression_Postfix(object __instance, ref float __result)
    {
        if (OnGetExpectedSuppression == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnGetExpectedSuppression.GetInvocationList().Cast<FloatSkillInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetExpectedSuppression handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_expected_suppression", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetExpectedSuppression_Postfix failed: {ex.Message}");
        }
    }

    private static void GetActionPointCost_Postfix(object __instance, ref int __result)
    {
        if (OnGetActionPointCost == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnGetActionPointCost.GetInvocationList().Cast<IntSkillInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetActionPointCost handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_ap_cost", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetActionPointCost_Postfix failed: {ex.Message}");
        }
    }

    private static void GetIdealRangeBase_Postfix(object __instance, ref int __result)
    {
        if (OnGetIdealRangeBase == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnGetIdealRangeBase.GetInvocationList().Cast<IntSkillInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetIdealRangeBase handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_ideal_range", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetIdealRangeBase_Postfix failed: {ex.Message}");
        }
    }

    private static void GetMaxRangeBase_Postfix(object __instance, ref int __result)
    {
        if (OnGetMaxRangeBase == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnGetMaxRangeBase.GetInvocationList().Cast<IntSkillInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetMaxRangeBase handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_max_range", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetMaxRangeBase_Postfix failed: {ex.Message}");
        }
    }

    private static void GetMinRangeBase_Postfix(object __instance, ref int __result)
    {
        if (OnGetMinRangeBase == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnGetMinRangeBase.GetInvocationList().Cast<IntSkillInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetMinRangeBase handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_min_range", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetMinRangeBase_Postfix failed: {ex.Message}");
        }
    }

    private static void IsInRange_Postfix(object __instance, ref bool __result)
    {
        if (OnIsInRange == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnIsInRange.GetInvocationList().Cast<BoolInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnIsInRange handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_is_in_range", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"IsInRange_Postfix failed: {ex.Message}");
        }
    }

    private static void IsInRangeShape_Postfix(object __instance, ref bool __result)
    {
        if (OnIsInRangeShape == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnIsInRangeShape.GetInvocationList().Cast<BoolInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnIsInRangeShape handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_is_in_range_shape", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"IsInRangeShape_Postfix failed: {ex.Message}");
        }
    }

    private static void IsMovementSkill_Postfix(object __instance, ref bool __result)
    {
        if (OnIsMovementSkill == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);
            var result = __result;

            foreach (var handler in OnIsMovementSkill.GetInvocationList().Cast<BoolInterceptor>())
            {
                try
                {
                    handler(skill, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnIsMovementSkill handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_is_movement", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"IsMovementSkill_Postfix failed: {ex.Message}");
        }
    }

    private static void IsUsable_Postfix(object __instance, ref bool __result)
    {
        if (OnSkillIsUsable == null) return;

        try
        {
            var skillPtr = GetPointer(__instance);
            var skill = new GameObj(skillPtr);

            // Extract actor from skill (skill.SkillContainer.Entity)
            var actor = GameObj.Null;
            try
            {
                var containerPtr = skill.ReadPtr(OFFSET_SKILL_CONTAINER);
                if (containerPtr != IntPtr.Zero)
                {
                    var entityPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(containerPtr + (int)OFFSET_CONTAINER_ENTITY);
                    if (entityPtr != IntPtr.Zero)
                        actor = new GameObj(entityPtr);
                }
            }
            catch { }

            var result = __result;

            foreach (var handler in OnSkillIsUsable.GetInvocationList().Cast<SkillUsableInterceptor>())
            {
                try
                {
                    handler(skill, actor, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnSkillIsUsable handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("skill_is_usable", new Dictionary<string, object>
            {
                ["skill_ptr"] = skillPtr.ToInt64(),
                ["actor_ptr"] = actor.Pointer.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"IsUsable_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - Actor
    // ═══════════════════════════════════════════════════════════════════

    #region Actor Postfixes

    /// <summary>
    /// Prefix patch for Actor.ApplySuppression - fires BEFORE suppression is applied.
    /// Allows modification of amount, friendly fire detection, and cancellation.
    /// Signature: void ApplySuppression(float amount, bool isFriendlyFire, object attacker, object context)
    /// </summary>
    private static bool ApplySuppression_Prefix(object __instance, ref float amount, ref bool isFriendlyFire, object attacker)
    {
        if (OnSuppressionApplied == null) return true;

        try
        {
            var actorPtr = GetPointer(__instance);
            var actor = new GameObj(actorPtr);

            // Get attacker pointer (may be null)
            IntPtr attackerPtr = IntPtr.Zero;
            if (attacker != null)
            {
                try
                {
                    attackerPtr = GetPointer(attacker);
                }
                catch
                {
                    // Attacker might not be a valid pointer type
                }
            }
            var attackerObj = new GameObj(attackerPtr);

            var modifiedAmount = amount;
            var modifiedFriendlyFire = isFriendlyFire;
            var cancel = false;

            foreach (var handler in OnSuppressionApplied.GetInvocationList().Cast<SuppressionApplicationInterceptor>())
            {
                try
                {
                    handler(actor, attackerObj, ref modifiedAmount, ref modifiedFriendlyFire, ref cancel);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnSuppressionApplied handler failed: {ex.Message}");
                }
            }

            // Apply modifications
            amount = modifiedAmount;
            isFriendlyFire = modifiedFriendlyFire;

            // Fire Lua event
            FireLuaEvent("actor_suppression_applied", new Dictionary<string, object>
            {
                ["actor_ptr"] = actorPtr.ToInt64(),
                ["attacker_ptr"] = attackerPtr.ToInt64(),
                ["amount"] = modifiedAmount,
                ["is_friendly_fire"] = modifiedFriendlyFire,
                ["cancelled"] = cancel
            });

            // Return false to cancel the original method if cancel flag is set
            return !cancel;
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"ApplySuppression_Prefix failed: {ex.Message}");
            return true; // Don't block the original method on error
        }
    }

    private static void HasLineOfSightTo_Postfix(object __instance, ref bool __result, object entity)
    {
        if (OnHasLineOfSightTo == null) return;

        try
        {
            var observerPtr = GetPointer(__instance);
            var targetPtr = GetPointer(entity);
            var observer = new GameObj(observerPtr);
            var target = new GameObj(targetPtr);
            var result = __result;

            foreach (var handler in OnHasLineOfSightTo.GetInvocationList().Cast<LineOfSightInterceptor>())
            {
                try
                {
                    handler(observer, target, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnHasLineOfSightTo handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("actor_los", new Dictionary<string, object>
            {
                ["observer_ptr"] = observerPtr.ToInt64(),
                ["target_ptr"] = targetPtr.ToInt64(),
                ["has_los"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"HasLineOfSightTo_Postfix failed: {ex.Message}");
        }
    }

    // Actor state query postfixes - Morale/Suppression
    private static void GetMoraleMax_Postfix(object __instance, ref int __result, float multiplier) { if (OnActorGetMoraleMax == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetMoraleMax.GetInvocationList().Cast<ActorMoraleMaxInterceptor>()) { try { h(actor, multiplier, ref r); } catch { } } __result = r; } catch { } }
    private static void GetMoralePct_Postfix(object __instance, ref float __result) { if (OnActorGetMoralePct == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetMoralePct.GetInvocationList().Cast<ActorFloatStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void GetMoraleState_Postfix(object __instance, ref int __result) { if (OnActorGetMoraleState == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetMoraleState.GetInvocationList().Cast<ActorIntStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void GetSuppressionPct_Postfix(object __instance, ref float __result) { if (OnActorGetSuppressionPct == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetSuppressionPct.GetInvocationList().Cast<ActorFloatStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void GetSuppressionState_Postfix(object __instance, ref int __result, float additionalSuppression) { if (OnActorGetSuppressionState == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetSuppressionState.GetInvocationList().Cast<ActorSuppressionStateInterceptor>()) { try { h(actor, additionalSuppression, ref r); } catch { } } __result = r; } catch { } }

    // Actor action prefixes - Morale/Suppression application
    private static bool ApplyMorale_Prefix(object __instance, ref uint eventType, ref float amount)
    {
        if (OnMoraleApplied == null) return true;

        try
        {
            var actorPtr = GetPointer(__instance);
            var actor = new GameObj(actorPtr);
            var modifiedAmount = amount;
            var cancel = false;

            foreach (var handler in OnMoraleApplied.GetInvocationList().Cast<MoraleApplicationInterceptor>())
            {
                try
                {
                    handler(actor, (int)eventType, ref modifiedAmount, ref cancel);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnMoraleApplied handler failed: {ex.Message}");
                }
            }

            amount = modifiedAmount;

            // Fire Lua event for script integration
            FireLuaEvent("morale_applied", new Dictionary<string, object>
            {
                ["actor_ptr"] = actorPtr.ToInt64(),
                ["event_type"] = (int)eventType,
                ["amount"] = modifiedAmount,
                ["canceled"] = cancel
            });

            // Return false to skip original method if cancelled
            return !cancel;
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"ApplyMorale_Prefix failed: {ex.Message}");
            return true;
        }
    }


    /// <summary>
    /// Prefix patch for Actor.MoveTo @ 0x1805e0a60 - fires BEFORE movement execution.
    /// This is the master movement hook, catching ALL actor movement attempts.
    /// Allows modification of movement parameters and complete cancellation.
    ///
    /// Signature: bool MoveTo(Tile tile, MovementAction action, MovementFlags flags)
    /// - param_1: Actor instance (this)
    /// - param_2: Destination Tile pointer
    /// - param_3: Pointer to MovementAction enum
    /// - param_4: MovementFlags bitfield
    ///
    /// Returns false to cancel movement, true to proceed.
    /// </summary>
    private static bool MoveTo_Prefix(object __instance, object tile, object action, uint flags)
    {
        if (OnMoveTo == null) return true;

        try
        {
            var actorPtr = GetPointer(__instance);
            var actor = new GameObj(actorPtr);

            // Extract tile pointer (may be null for some movement modes)
            IntPtr tilePtr = IntPtr.Zero;
            if (tile != null)
            {
                try
                {
                    tilePtr = GetPointer(tile);
                }
                catch
                {
                    // Tile may be null or invalid - game handles this internally
                }
            }
            var tileObj = new GameObj(tilePtr);

            // Extract movement action from pointer (param_3 is pointer to uint)
            // However, Harmony may have already dereferenced it for us
            // The action parameter should be the MovementAction enum value
            // Based on ApplySuppression pattern, flags is direct uint

            var cancel = false;

            foreach (var handler in OnMoveTo.GetInvocationList().Cast<MoveToInterceptor>())
            {
                try
                {
                    handler(actor, tileObj, (int)flags, ref cancel);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnMoveTo handler failed: {ex.Message}");
                }
            }

            // Fire Lua event for script integration
            FireLuaEvent("actor_move_to", new Dictionary<string, object>
            {
                ["actor_ptr"] = actorPtr.ToInt64(),
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["flags"] = flags,
                ["cancelled"] = cancel
            });

            // Return false to cancel movement (skip original method)
            return !cancel;
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"MoveTo_Prefix failed: {ex.Message}");
            return true; // Don't block movement on error
        }
    }

    // Actor state query postfixes - Action Points
    private static void GetActionPointsAtTurnStart_Postfix(object __instance, ref int __result) { if (OnActorGetActionPointsAtTurnStart == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetActionPointsAtTurnStart.GetInvocationList().Cast<ActorIntStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void GetTurningCost_Postfix(object __instance, ref int __result, int targetDirection) { if (OnActorGetTurningCost == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetTurningCost.GetInvocationList().Cast<ActorTurningCostInterceptor>()) { try { h(actor, targetDirection, ref r); } catch { } } __result = r; } catch { } }
    private static void GetTilesMovedThisTurn_Postfix(object __instance, ref int __result) { if (OnActorGetTilesMovedThisTurn == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetTilesMovedThisTurn.GetInvocationList().Cast<ActorIntStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void GetTimesAttackedSinceLastTurn_Postfix(object __instance, ref int __result) { if (OnActorGetTimesAttackedSinceLastTurn == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorGetTimesAttackedSinceLastTurn.GetInvocationList().Cast<ActorIntStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }

    // Actor state query postfixes - Boolean state queries
    private static void ActorIsActive_Postfix(object __instance, ref bool __result) { if (OnActorIsActive == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsActive.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsActionPointsSpent_Postfix(object __instance, ref bool __result) { if (OnActorIsActionPointsSpent == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsActionPointsSpent.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsDetectedByFaction_Postfix(object __instance, ref bool __result, byte faction) { if (OnActorIsDetectedByFaction == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsDetectedByFaction.GetInvocationList().Cast<ActorFactionDetectionInterceptor>()) { try { h(actor, faction, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsDying_Postfix(object __instance, ref bool __result) { if (OnActorIsDying == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsDying.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsHeavyWeaponDeployed_Postfix(object __instance, ref bool __result) { if (OnActorIsHeavyWeaponDeployed == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsHeavyWeaponDeployed.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsHiddenToAI_Postfix(object __instance, ref bool __result) { if (OnActorIsHiddenToAI == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsHiddenToAI.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsHiddenToPlayer_Postfix(object __instance, ref bool __result) { if (OnActorIsHiddenToPlayer == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsHiddenToPlayer.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsInfantry_Postfix(object __instance, ref bool __result) { if (OnActorIsInfantry == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsInfantry.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsLeavingMap_Postfix(object __instance, ref bool __result) { if (OnActorIsLeavingMap == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsLeavingMap.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsMinion_Postfix(object __instance, ref bool __result) { if (OnActorIsMinion == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsMinion.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsMoving_Postfix(object __instance, ref bool __result) { if (OnActorIsMoving == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsMoving.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsSelectableByPlayer_Postfix(object __instance, ref bool __result) { if (OnActorIsSelectableByPlayer == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsSelectableByPlayer.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsStunned_Postfix(object __instance, ref bool __result) { if (OnActorIsStunned == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsStunned.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsTurnDone_Postfix(object __instance, ref bool __result) { if (OnActorIsTurnDone == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsTurnDone.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsTurret_Postfix(object __instance, ref bool __result) { if (OnActorIsTurret == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsTurret.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorIsVehicle_Postfix(object __instance, ref bool __result) { if (OnActorIsVehicle == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorIsVehicle.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorCanEnterAnyAdjacentVehicle_Postfix(object __instance, ref bool __result) { if (OnActorCanEnterAnyAdjacentVehicle == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorCanEnterAnyAdjacentVehicle.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }
    private static void ActorHasActed_Postfix(object __instance, ref bool __result) { if (OnActorHasActed == null) return; try { var a = GetPointer(__instance); var actor = new GameObj(a); var r = __result; foreach (var h in OnActorHasActed.GetInvocationList().Cast<ActorBoolStateInterceptor>()) { try { h(actor, ref r); } catch { } } __result = r; } catch { } }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - Entity State Methods
    // ═══════════════════════════════════════════════════════════════════

    #region Entity State Postfixes

    private static void GetHitpointsPct_Postfix(object __instance, ref float __result)
    {
        if (OnGetHitpointsPct == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnGetHitpointsPct.GetInvocationList().Cast<EntityFloatStateInterceptor>())
            {
                try
                {
                    handler(entity, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetHitpointsPct handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("entity_hitpoints_pct", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetHitpointsPct_Postfix failed: {ex.Message}");
        }
    }

    private static void GetArmorDurabilityPct_Postfix(object __instance, ref float __result)
    {
        if (OnGetArmorDurabilityPct == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnGetArmorDurabilityPct.GetInvocationList().Cast<EntityFloatStateInterceptor>())
            {
                try
                {
                    handler(entity, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetArmorDurabilityPct handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("entity_armor_durability_pct", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetArmorDurabilityPct_Postfix failed: {ex.Message}");
        }
    }

    private static void GetCoverUsage_Postfix(object __instance, ref int __result)
    {
        if (OnGetCoverUsage == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnGetCoverUsage.GetInvocationList().Cast<EntityIntStateInterceptor>())
            {
                try
                {
                    handler(entity, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetCoverUsage handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("entity_cover_usage", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetCoverUsage_Postfix failed: {ex.Message}");
        }
    }

    private static void GetProvidedCover_Postfix(object __instance, ref object __result)
    {
        if (OnGetProvidedCover == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var resultPtr = GetPointer(__result);

            foreach (var handler in OnGetProvidedCover.GetInvocationList().Cast<EntityObjectStateInterceptor>())
            {
                try
                {
                    handler(entity, ref resultPtr);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetProvidedCover handler failed: {ex.Message}");
                }
            }

            // Note: Cannot easily change the object reference in postfix for Il2Cpp objects
            // The interceptor can read/log but modification requires prefix or native patching

            FireLuaEvent("entity_provided_cover", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["cover_ptr"] = resultPtr.ToInt64()
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetProvidedCover_Postfix failed: {ex.Message}");
        }
    }

    private static void IsDiscovered_Postfix(object __instance, ref bool __result)
    {
        if (OnIsDiscovered == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnIsDiscovered.GetInvocationList().Cast<EntityBoolStateInterceptor>())
            {
                try
                {
                    handler(entity, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnIsDiscovered handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("entity_is_discovered", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"IsDiscovered_Postfix failed: {ex.Message}");
        }
    }

    private static void GetLastSkillUsed_Postfix(object __instance, ref object __result)
    {
        if (OnGetLastSkillUsed == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);
            var resultPtr = GetPointer(__result);

            foreach (var handler in OnGetLastSkillUsed.GetInvocationList().Cast<EntityObjectStateInterceptor>())
            {
                try
                {
                    handler(entity, ref resultPtr);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetLastSkillUsed handler failed: {ex.Message}");
                }
            }

            // Note: Cannot easily change the object reference in postfix for Il2Cpp objects
            // The interceptor can read/log but modification requires prefix or native patching

            FireLuaEvent("entity_last_skill_used", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["skill_ptr"] = resultPtr.ToInt64()
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetLastSkillUsed_Postfix failed: {ex.Message}");
        }
    }

    private static void GetScaleRange_Postfix(object __instance, ref object __result)
    {
        if (OnGetScaleRange == null) return;

        try
        {
            var entityPtr = GetPointer(__instance);
            var entity = new GameObj(entityPtr);

            // Parse Vector2 result
            var result = new Vector2Result();
            if (__result is Il2CppObjectBase resultObj)
            {
                var resultPtr = resultObj.Pointer;
                // Vector2 layout: X at offset 0, Y at offset 4
                result.X = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr);
                result.Y = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(resultPtr + 4);
            }
            else if (__result != null)
            {
                // Try to extract from value type via reflection
                var resultType = __result.GetType();
                var xField = resultType.GetField("x") ?? resultType.GetField("X");
                var yField = resultType.GetField("y") ?? resultType.GetField("Y");
                if (xField != null) result.X = Convert.ToSingle(xField.GetValue(__result));
                if (yField != null) result.Y = Convert.ToSingle(yField.GetValue(__result));
            }

            foreach (var handler in OnGetScaleRange.GetInvocationList().Cast<EntityVector2StateInterceptor>())
            {
                try
                {
                    handler(entity, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnGetScaleRange handler failed: {ex.Message}");
                }
            }

            // Write back modified result if possible
            if (__result is Il2CppObjectBase resultObjWrite)
            {
                var resultPtr = resultObjWrite.Pointer;
                System.Runtime.InteropServices.Marshal.StructureToPtr(result.X, resultPtr, false);
                System.Runtime.InteropServices.Marshal.StructureToPtr(result.Y, resultPtr + 4, false);
            }

            FireLuaEvent("entity_scale_range", new Dictionary<string, object>
            {
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["min_scale"] = result.X,
                ["max_scale"] = result.Y
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"GetScaleRange_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - Tile
    // ═══════════════════════════════════════════════════════════════════

    #region Tile Postfixes

    private static void TileHasLineOfSightTo_Postfix(object __instance, ref bool __result, object toTile, byte flags)
    {
        if (OnTileHasLineOfSightTo == null) return;

        try
        {
            var fromTilePtr = GetPointer(__instance);
            var toTilePtr = GetPointer(toTile);
            var fromTileObj = new GameObj(fromTilePtr);
            var toTileObj = new GameObj(toTilePtr);
            var result = __result;

            foreach (var handler in OnTileHasLineOfSightTo.GetInvocationList().Cast<TileLoSInterceptor>())
            {
                try
                {
                    handler(fromTileObj, toTileObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileHasLineOfSightTo handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_has_los", new Dictionary<string, object>
            {
                ["from_tile_ptr"] = fromTilePtr.ToInt64(),
                ["to_tile_ptr"] = toTilePtr.ToInt64(),
                ["has_los"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileHasLineOfSightTo_Postfix failed: {ex.Message}");
        }
    }

    private static void TileIsBlockingLineOfSight_Postfix(object __instance, ref bool __result)
    {
        if (OnTileIsBlockingLineOfSight == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnTileIsBlockingLineOfSight.GetInvocationList().Cast<TileBlockerInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileIsBlockingLineOfSight handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_blocking_los", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["is_blocking"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileIsBlockingLineOfSight_Postfix failed: {ex.Message}");
        }
    }

    private static void TileGetCover_Postfix(object __instance, ref int __result, int direction, object entity, object param4, bool param5)
    {
        if (OnTileGetCover == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var entityPtr = GetPointer(entity);
            var tile = new GameObj(tilePtr);
            var entityObj = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnTileGetCover.GetInvocationList().Cast<TileCoverInterceptor>())
            {
                try
                {
                    handler(tile, direction, entityObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileGetCover handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_get_cover", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["direction"] = direction,
                ["cover"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileGetCover_Postfix failed: {ex.Message}");
        }
    }

    private static void TileGetCoverMask_Postfix(object __instance, ref int __result)
    {
        if (OnTileGetCoverMask == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnTileGetCoverMask.GetInvocationList().Cast<TileCoverMaskInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileGetCoverMask handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_get_cover_mask", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["cover_mask"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileGetCoverMask_Postfix failed: {ex.Message}");
        }
    }

    private static void TileGetEntityProvidedCover_Postfix(object __instance, ref int __result)
    {
        if (OnTileGetEntityProvidedCover == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnTileGetEntityProvidedCover.GetInvocationList().Cast<TileEntityCoverInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileGetEntityProvidedCover handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_entity_cover", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["cover"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileGetEntityProvidedCover_Postfix failed: {ex.Message}");
        }
    }

    private static void TileCanBeEntered_Postfix(object __instance, ref bool __result)
    {
        if (OnTileCanBeEntered == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnTileCanBeEntered.GetInvocationList().Cast<TileEntryInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileCanBeEntered handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_can_enter", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["can_enter"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileCanBeEntered_Postfix failed: {ex.Message}");
        }
    }

    private static void TileCanBeEnteredBy_Postfix(object __instance, ref bool __result, object entity)
    {
        if (OnTileCanBeEnteredBy == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var entityPtr = GetPointer(entity);
            var tile = new GameObj(tilePtr);
            var entityObj = new GameObj(entityPtr);
            var result = __result;

            foreach (var handler in OnTileCanBeEnteredBy.GetInvocationList().Cast<TileEntityEntryInterceptor>())
            {
                try
                {
                    handler(tile, entityObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileCanBeEnteredBy handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("tile_can_enter_by", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["entity_ptr"] = entityPtr.ToInt64(),
                ["can_enter"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"TileCanBeEnteredBy_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - BaseTile
    // ═══════════════════════════════════════════════════════════════════

    #region BaseTile Postfixes

    private static void BaseTileHasCover_Postfix(object __instance, ref bool __result)
    {
        if (OnBaseTileHasCover == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnBaseTileHasCover.GetInvocationList().Cast<BaseTileCoverCheckInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnBaseTileHasCover handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("basetile_has_cover", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["has_cover"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"BaseTileHasCover_Postfix failed: {ex.Message}");
        }
    }

    private static void BaseTileHasHalfCover_Postfix(object __instance, ref bool __result)
    {
        if (OnBaseTileHasHalfCover == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnBaseTileHasHalfCover.GetInvocationList().Cast<BaseTileHalfCoverInterceptor>())
            {
                try
                {
                    handler(tile, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnBaseTileHasHalfCover handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("basetile_has_half_cover", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["has_half_cover"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"BaseTileHasHalfCover_Postfix failed: {ex.Message}");
        }
    }

    private static void BaseTileHasHalfCoverInDir_Postfix(object __instance, ref bool __result, int direction)
    {
        if (OnBaseTileHasHalfCoverInDir == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnBaseTileHasHalfCoverInDir.GetInvocationList().Cast<BaseTileDirHalfCoverInterceptor>())
            {
                try
                {
                    handler(tile, direction, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnBaseTileHasHalfCoverInDir handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("basetile_has_half_cover_dir", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["direction"] = direction,
                ["has_half_cover"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"BaseTileHasHalfCoverInDir_Postfix failed: {ex.Message}");
        }
    }

    private static void BaseTileIsMovementBlocked_Postfix(object __instance, ref bool __result, int direction)
    {
        if (OnBaseTileIsMovementBlocked == null) return;

        try
        {
            var tilePtr = GetPointer(__instance);
            var tile = new GameObj(tilePtr);
            var result = __result;

            foreach (var handler in OnBaseTileIsMovementBlocked.GetInvocationList().Cast<BaseTileMovementBlockedInterceptor>())
            {
                try
                {
                    handler(tile, direction, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnBaseTileIsMovementBlocked handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("basetile_movement_blocked", new Dictionary<string, object>
            {
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["direction"] = direction,
                ["is_blocked"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"BaseTileIsMovementBlocked_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - PathfindingProcess
    // ═══════════════════════════════════════════════════════════════════

    #region PathfindingProcess Postfixes

    private static void PathfindingProcessIsTraversable_Postfix(object __instance, ref bool __result, object tile)
    {
        // CRITICAL PERFORMANCE: Early exit if no subscribers
        if (OnTileTraversable == null) return;

        try
        {
            var processPtr = GetPointer(__instance);
            var process = new GameObj(processPtr);
            var tilePtr = GetPointer(tile);
            var tileObj = new GameObj(tilePtr);
            var result = __result;

            // Invoke C# handlers
            foreach (var handler in OnTileTraversable.GetInvocationList().Cast<TraversableCheckInterceptor>())
            {
                try
                {
                    handler(process, tileObj, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnTileTraversable handler failed: {ex.Message}");
                }
            }

            __result = result;

            // Fire Lua event
            FireLuaEvent("tile_traversable", new Dictionary<string, object>
            {
                ["process_ptr"] = processPtr.ToInt64(),
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["is_traversable"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"PathfindingProcessIsTraversable_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  HARMONY POSTFIX PATCHES - LineOfSight
    // ═══════════════════════════════════════════════════════════════════

    #region LineOfSight Postfixes

    private static void LineOfSightRayTrace_Postfix(ref bool __result, object fromTile, object toTile, byte flags)
    {
        if (OnLineOfSightRayTrace == null) return;

        try
        {
            var fromTilePtr = GetPointer(fromTile);
            var toTilePtr = GetPointer(toTile);
            var fromTileObj = new GameObj(fromTilePtr);
            var toTileObj = new GameObj(toTilePtr);
            var result = __result;

            foreach (var handler in OnLineOfSightRayTrace.GetInvocationList().Cast<LineOfSightRayTraceInterceptor>())
            {
                try
                {
                    handler(fromTileObj, toTileObj, flags, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnLineOfSightRayTrace handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("los_raytrace", new Dictionary<string, object>
            {
                ["from_tile_ptr"] = fromTilePtr.ToInt64(),
                ["to_tile_ptr"] = toTilePtr.ToInt64(),
                ["flags"] = (int)flags,
                ["has_los"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"LineOfSightRayTrace_Postfix failed: {ex.Message}");
        }
    }

    private static void LineOfSightIsNearTileCorner_Postfix(ref bool __result, float posX, float posY)
    {
        if (OnLineOfSightIsNearTileCorner == null) return;

        try
        {
            var result = __result;

            foreach (var handler in OnLineOfSightIsNearTileCorner.GetInvocationList().Cast<LineOfSightCornerInterceptor>())
            {
                try
                {
                    handler(posX, posY, ref result);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnLineOfSightIsNearTileCorner handler failed: {ex.Message}");
                }
            }

            __result = result;

            FireLuaEvent("los_near_corner", new Dictionary<string, object>
            {
                ["pos_x"] = posX,
                ["pos_y"] = posY,
                ["is_near_corner"] = result
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"LineOfSightIsNearTileCorner_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  MOVEMENT POSTFIXES
    // ═══════════════════════════════════════════════════════════════════

    #region Movement Postfixes

    private static void GetMaxMovementSpeed_Postfix(object __instance, ref float __result, int mode)
    {
        if (OnGetMaxMovementSpeed == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnGetMaxMovementSpeed.GetInvocationList().Cast<MovementFloatInterceptor>())
            {
                try { handler(instance, mode, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnGetMaxMovementSpeed handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("movement_max_speed", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["movement_mode"] = mode,
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"GetMaxMovementSpeed_Postfix failed: {ex.Message}"); }
    }

    private static void GetTotalPathCost_Postfix(object __instance, ref int __result, object path, object movementFlags, object actor, object facing)
    {
        if (OnGetTotalPathCost == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var pathPtr = GetPointer(path);
            var actorPtr = GetPointer(actor);
            var instance = new GameObj(instancePtr);
            var pathObj = new GameObj(pathPtr);
            var actorObj = new GameObj(actorPtr);
            var result = __result;

            foreach (var handler in OnGetTotalPathCost.GetInvocationList().Cast<PathCostInterceptor>())
            {
                try { handler(instance, pathObj, actorObj, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnGetTotalPathCost handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("movement_path_cost", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["path_ptr"] = pathPtr.ToInt64(),
                ["actor_ptr"] = actorPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"GetTotalPathCost_Postfix failed: {ex.Message}"); }
    }

    private static void GetTurnSpeed_Postfix(object __instance, ref float __result, int mode)
    {
        if (OnGetTurnSpeed == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnGetTurnSpeed.GetInvocationList().Cast<MovementFloatInterceptor>())
            {
                try { handler(instance, mode, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnGetTurnSpeed handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("movement_turn_speed", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["movement_mode"] = mode,
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"GetTurnSpeed_Postfix failed: {ex.Message}"); }
    }

    private static void GetSlowdownDistance_Postfix(object __instance, ref float __result, int mode)
    {
        if (OnGetSlowdownDistance == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnGetSlowdownDistance.GetInvocationList().Cast<MovementFloatInterceptor>())
            {
                try { handler(instance, mode, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnGetSlowdownDistance handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("movement_slowdown_distance", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["movement_mode"] = mode,
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"GetSlowdownDistance_Postfix failed: {ex.Message}"); }
    }

    private static void GetMaxAngleTurnSlowdown_Postfix(object __instance, ref float __result, int mode)
    {
        if (OnGetMaxAngleTurnSlowdown == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnGetMaxAngleTurnSlowdown.GetInvocationList().Cast<MovementFloatInterceptor>())
            {
                try { handler(instance, mode, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnGetMaxAngleTurnSlowdown handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("movement_max_angle_turn_slowdown", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["movement_mode"] = mode,
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"GetMaxAngleTurnSlowdown_Postfix failed: {ex.Message}"); }
    }

    private static void ClipPathToCost_Postfix(object __instance, ref int __result, object path, object lastTile, object lastTileIndex, object movementFlags, int maxCost, object actor, object facing, bool modifyPath)
    {
        if (OnClipPathToCost == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var pathPtr = GetPointer(path);
            var actorPtr = GetPointer(actor);
            var instance = new GameObj(instancePtr);
            var pathObj = new GameObj(pathPtr);
            var actorObj = new GameObj(actorPtr);

            var clipResult = new ClipPathResult
            {
                ActualCost = __result,
                MaxCost = maxCost,
                ClipIndex = -1
            };

            foreach (var handler in OnClipPathToCost.GetInvocationList().Cast<ClipPathInterceptor>())
            {
                try { handler(instance, pathObj, actorObj, ref clipResult); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnClipPathToCost handler failed: {ex.Message}"); }
            }

            __result = clipResult.ActualCost;
            FireLuaEvent("movement_clip_path", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["path_ptr"] = pathPtr.ToInt64(),
                ["actor_ptr"] = actorPtr.ToInt64(),
                ["actual_cost"] = clipResult.ActualCost,
                ["max_cost"] = clipResult.MaxCost,
                ["clip_index"] = clipResult.ClipIndex
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"ClipPathToCost_Postfix failed: {ex.Message}"); }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  STRATEGY POSTFIXES
    // ═══════════════════════════════════════════════════════════════════

    #region Strategy UnitLeaderAttributes Postfixes

    private static void StrategyGetActionPoints_Postfix(object __instance, ref int __result)
    {
        if (OnStrategyGetActionPoints == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyGetActionPoints.GetInvocationList().Cast<StrategyIntIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetActionPoints handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_action_points", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetActionPoints_Postfix failed: {ex.Message}"); }
    }

    private static void StrategyGetHitpointsPerElement_Postfix(object __instance, ref int __result)
    {
        if (OnStrategyGetHitpointsPerElement == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyGetHitpointsPerElement.GetInvocationList().Cast<StrategyIntIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetHitpointsPerElement handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_hitpoints_per_element", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetHitpointsPerElement_Postfix failed: {ex.Message}"); }
    }

    private static void StrategyGetDamageSustainedMult_Postfix(object __instance, ref float __result)
    {
        if (OnStrategyGetDamageSustainedMult == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyGetDamageSustainedMult.GetInvocationList().Cast<StrategyFloatIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetDamageSustainedMult handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_damage_sustained_mult", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetDamageSustainedMult_Postfix failed: {ex.Message}"); }
    }

    #endregion

    #region Strategy BaseUnitLeader Postfixes

    private static void StrategyGetHitpointsPct_Postfix(object __instance, ref float __result)
    {
        if (OnStrategyGetHitpointsPct == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyGetHitpointsPct.GetInvocationList().Cast<StrategyFloatIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetHitpointsPct handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_hitpoints_pct", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetHitpointsPct_Postfix failed: {ex.Message}"); }
    }

    private static void StrategyCanBePromoted_Postfix(object __instance, ref bool __result)
    {
        if (OnStrategyCanBePromoted == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyCanBePromoted.GetInvocationList().Cast<StrategyBoolIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyCanBePromoted handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_can_be_promoted", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyCanBePromoted_Postfix failed: {ex.Message}"); }
    }

    private static void StrategyCanBeDemoted_Postfix(object __instance, ref bool __result)
    {
        if (OnStrategyCanBeDemoted == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyCanBeDemoted.GetInvocationList().Cast<StrategyBoolIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyCanBeDemoted handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_can_be_demoted", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyCanBeDemoted_Postfix failed: {ex.Message}"); }
    }

    private static void StrategyGetEntityProperty_Postfix(object __instance, ref float __result, object propertyType)
    {
        if (OnStrategyGetEntityProperty == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            int propertyTypeInt = 0;
            if (propertyType != null)
            {
                try { propertyTypeInt = Convert.ToInt32(propertyType); }
                catch { }
            }

            foreach (var handler in OnStrategyGetEntityProperty.GetInvocationList().Cast<StrategyEntityIntercept>())
            {
                try { handler(instance, propertyTypeInt, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetEntityProperty handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_entity_property", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["property_type"] = propertyTypeInt,
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetEntityProperty_Postfix failed: {ex.Message}"); }
    }

    #endregion

    #region Strategy Vehicle Postfixes

    private static void StrategyGetVehicleArmor_Postfix(object __instance, ref int __result)
    {
        if (OnStrategyGetVehicleArmor == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnStrategyGetVehicleArmor.GetInvocationList().Cast<StrategyIntIntercept>())
            {
                try { handler(instance, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnStrategyGetVehicleArmor handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("strategy_vehicle_armor", new Dictionary<string, object>
            {
                ["instance_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"StrategyGetVehicleArmor_Postfix failed: {ex.Message}"); }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  AI BEHAVIOR POSTFIXES
    // ═══════════════════════════════════════════════════════════════════

    #region AI Behavior Postfixes

    private static void AIGetAttackScore_Postfix(object __instance, ref float __result, object target)
    {
        if (OnAIGetAttackScore == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var targetPtr = GetPointer(target);
            var instance = new GameObj(instancePtr);
            var targetObj = new GameObj(targetPtr);
            var result = __result;

            foreach (var handler in OnAIGetAttackScore.GetInvocationList().Cast<AIFloatValueInterceptor>())
            {
                try { handler(instance, targetObj, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnAIGetAttackScore handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("ai_attack_score", new Dictionary<string, object>
            {
                ["ai_ptr"] = instancePtr.ToInt64(),
                ["target_ptr"] = targetPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"AIGetAttackScore_Postfix failed: {ex.Message}"); }
    }

    private static void AIGetThreatValue_Postfix(object __instance, ref float __result, object target)
    {
        if (OnAIGetThreatValue == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var targetPtr = GetPointer(target);
            var instance = new GameObj(instancePtr);
            var targetObj = new GameObj(targetPtr);
            var result = __result;

            foreach (var handler in OnAIGetThreatValue.GetInvocationList().Cast<AIFloatValueInterceptor>())
            {
                try { handler(instance, targetObj, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnAIGetThreatValue handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("ai_threat_value", new Dictionary<string, object>
            {
                ["ai_ptr"] = instancePtr.ToInt64(),
                ["target_ptr"] = targetPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"AIGetThreatValue_Postfix failed: {ex.Message}"); }
    }

    private static void AIGetActionPriority_Postfix(object __instance, ref int __result, object action)
    {
        if (OnAIGetActionPriority == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var actionPtr = GetPointer(action);
            var instance = new GameObj(instancePtr);
            var actionObj = new GameObj(actionPtr);
            var result = __result;

            foreach (var handler in OnAIGetActionPriority.GetInvocationList().Cast<AIIntValueInterceptor>())
            {
                try { handler(instance, actionObj, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnAIGetActionPriority handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("ai_action_priority", new Dictionary<string, object>
            {
                ["ai_ptr"] = instancePtr.ToInt64(),
                ["action_ptr"] = actionPtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"AIGetActionPriority_Postfix failed: {ex.Message}"); }
    }

    private static void AIShouldFlee_Postfix(object __instance, ref bool __result)
    {
        if (OnAIShouldFlee == null) return;

        try
        {
            var instancePtr = GetPointer(__instance);
            var instance = new GameObj(instancePtr);
            var result = __result;

            foreach (var handler in OnAIShouldFlee.GetInvocationList().Cast<AIBoolDecisionInterceptor>())
            {
                try { handler(instance, GameObj.Null, ref result); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnAIShouldFlee handler failed: {ex.Message}"); }
            }

            __result = result;
            FireLuaEvent("ai_should_flee", new Dictionary<string, object>
            {
                ["ai_ptr"] = instancePtr.ToInt64(),
                ["result"] = result
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"AIShouldFlee_Postfix failed: {ex.Message}"); }
    }

    private static void AgentEvaluate_Prefix(object __instance, ref bool __runOriginal)
    {
        if (OnAIEvaluate == null) return;

        try
        {
            var agentPtr = GetPointer(__instance);
            var agent = new GameObj(agentPtr);
            var cancel = false;

            foreach (var handler in OnAIEvaluate.GetInvocationList().Cast<AgentEvaluateInterceptor>())
            {
                try { handler(agent, ref cancel); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnAIEvaluate handler failed: {ex.Message}"); }
            }

            if (cancel)
            {
                __runOriginal = false;
            }

            FireLuaEvent("ai_evaluate", new Dictionary<string, object>
            {
                ["agent_ptr"] = agentPtr.ToInt64(),
                ["cancel"] = cancel
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"AgentEvaluate_Prefix failed: {ex.Message}"); }
    }

    /// <summary>
    /// Postfix for Criterion.Evaluate - observes and modifies position scoring.
    /// This hooks ALL criterion evaluations for AI positioning decisions.
    ///
    /// Based on Ghidra analysis:
    /// - Signature: void Evaluate(criterion, context, tile)
    /// - param_1: this (Criterion instance)
    /// - param_2: context (Agent/Entity)
    /// - param_3: tile being evaluated (has score at offset 0x28)
    ///
    /// Threading: Called in PARALLEL by AI evaluation jobs - MUST be thread-safe!
    /// </summary>
    private static void CriterionEvaluate_Postfix(object __instance, object __1, object __2)
    {
        // Early exit if no subscribers (performance optimization for parallel calls)
        if (OnPositionScore == null) return;

        try
        {
            var criterionPtr = GetPointer(__instance);
            var tilePtr = GetPointer(__2);  // Third parameter is the tile

            // Null check - bail if we don't have valid pointers
            if (criterionPtr == IntPtr.Zero || tilePtr == IntPtr.Zero) return;

            var criterion = new GameObj(criterionPtr);
            var tile = new GameObj(tilePtr);

            // Extract score from tile object (offset 0x28 based on Ghidra decompilation)
            // Use Marshal to read/write the float at this offset
            var scoreAddress = IntPtr.Add(tilePtr, 0x28);
            float score = System.Runtime.InteropServices.Marshal.PtrToStructure<float>(scoreAddress);

            // Invoke all handlers
            foreach (var handler in OnPositionScore.GetInvocationList().Cast<CriterionEvaluateInterceptor>())
            {
                try
                {
                    handler(criterion, tile, ref score);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("Intercept", $"OnPositionScore handler failed: {ex.Message}");
                }
            }

            // Write modified score back to tile
            System.Runtime.InteropServices.Marshal.StructureToPtr(score, scoreAddress, false);

            // Fire Lua event (note: may have performance impact due to parallel calls)
            FireLuaEvent("position_score", new Dictionary<string, object>
            {
                ["criterion_ptr"] = criterionPtr.ToInt64(),
                ["tile_ptr"] = tilePtr.ToInt64(),
                ["score"] = score
            });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("Intercept", $"CriterionEvaluate_Postfix failed: {ex.Message}");
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  PATHFINDING PREFIXES - PathfindingProcess hooks
    // ═══════════════════════════════════════════════════════════════════

    #region Pathfinding Prefixes

    private static void FindPath_Prefix(object __instance, object __0, object __1, object __2, object __3, ref bool __runOriginal)
    {
        if (OnPathfinding == null) return;

        try
        {
            // Extract PathfindingProcess instance (this pointer = param_1)
            var processPtr = GetPointer(__instance);
            var process = new GameObj(processPtr);

            // Extract start tile (__0 = param_2)
            var startPtr = GetPointer(__0);
            var start = new GameObj(startPtr);

            // Extract end tile (__1 = param_3)
            var endPtr = GetPointer(__1);
            var end = new GameObj(endPtr);

            // Extract entity parameter (__2 = param_4)
            // var entityPtr = GetPointer(__2);  // Not currently exposed in delegate

            // Extract path result pointer (__3 = param_5)
            var pathResultPtr = GetPointer(__3);

            bool cancel = false;

            foreach (var handlerFunc in OnPathfinding.GetInvocationList().Cast<FindPathInterceptor>())
            {
                try { handlerFunc(process, start, end, ref pathResultPtr, ref cancel); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnPathfinding handler failed: {ex.Message}"); }
            }

            if (cancel)
            {
                __runOriginal = false;
                return;
            }

            FireLuaEvent("pathfinding", new Dictionary<string, object>
            {
                ["process_ptr"] = processPtr.ToInt64(),
                ["start_ptr"] = startPtr.ToInt64(),
                ["end_ptr"] = endPtr.ToInt64(),
                ["path_result_ptr"] = pathResultPtr.ToInt64()
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"FindPath_Prefix failed: {ex.Message}"); }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  ACTION PREFIXES - Damage/Morale/Suppression Application
    // ═══════════════════════════════════════════════════════════════════

    #region Action Prefixes

    private static void ApplyDamage_Prefix(object __instance, ref bool __runOriginal)
    {
        if (OnDamageApplied == null) return;

        try
        {
            var handlerPtr = GetPointer(__instance);
            var handler = new GameObj(handlerPtr);

            // Extract target entity from handler via GetEntity(0)
            // Based on decompiled code @ 0x180702970
            var targetPtr = IntPtr.Zero;
            var target = GameObj.Null;

            // Call GetEntity(0) on the skill event handler
            var getEntityMethod = __instance.GetType().GetMethod("GetEntity",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (getEntityMethod != null)
            {
                var targetObj = getEntityMethod.Invoke(__instance, new object[] { 0 });
                if (targetObj != null)
                {
                    targetPtr = GetPointer(targetObj);
                    target = new GameObj(targetPtr);
                }
            }

            // Extract attacker and skill from handler context (handler+0x10)
            var skillContextPtr = handler.ReadPtr(0x10);
            var attacker = GameObj.Null;
            var skill = GameObj.Null;

            if (skillContextPtr != IntPtr.Zero)
            {
                skill = new GameObj(skillContextPtr);
                // Skill owner at +0xE8
                var attackerPtr = skill.ReadPtr(0xE8);
                if (attackerPtr != IntPtr.Zero)
                {
                    attacker = new GameObj(attackerPtr);
                }
            }

            // Calculate damage from effect data (handler+0x18)
            var effectDataPtr = handler.ReadPtr(0x18);
            float damage = 0f;

            if (effectDataPtr != IntPtr.Zero && targetPtr != IntPtr.Zero)
            {
                var effectData = new GameObj(effectDataPtr);

                // Read damage components from effect data offsets
                var flatDamage = effectData.ReadFloat(0x64);  // DamageFlatAmount
                var currentHP = target.ReadFloat(0x54);       // Entity current HP
                var maxHP = target.ReadInt(0x58);             // Entity max HP

                var pctCurrent = effectData.ReadFloat(0x68);   // DamagePctCurrentHitpoints
                var pctCurrentMin = effectData.ReadFloat(0x6c); // DamagePctCurrentHitpointsMin
                var pctMax = effectData.ReadFloat(0x70);        // DamagePctMaxHitpoints
                var pctMaxMin = effectData.ReadFloat(0x74);     // DamagePctMaxHitpointsMin

                var currentHPDmg = Math.Max(currentHP * pctCurrent, pctCurrentMin);
                var maxHPDmg = Math.Max(maxHP * pctMax, pctMaxMin);

                damage = flatDamage + currentHPDmg + maxHPDmg;
            }

            bool cancel = false;

            foreach (var handlerFunc in OnDamageApplied.GetInvocationList().Cast<DamageApplicationInterceptor>())
            {
                try { handlerFunc(handler, target, attacker, skill, ref damage, ref cancel); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnDamageApplied handler failed: {ex.Message}"); }
            }

            if (cancel)
            {
                __runOriginal = false;
                return;
            }

            // Write modified damage back to effect data
            if (effectDataPtr != IntPtr.Zero)
            {
                var effectData = new GameObj(effectDataPtr);
                // Set flat damage to the modified total
                effectData.WriteFloat("DamageFlatAmount", damage);
                // Zero out percentage damages to prevent double-application
                effectData.WriteFloat("DamagePctCurrentHitpoints", 0f);
                effectData.WriteFloat("DamagePctMaxHitpoints", 0f);
            }

            FireLuaEvent("damage_applied", new Dictionary<string, object>
            {
                ["handler_ptr"] = handlerPtr.ToInt64(),
                ["target_ptr"] = targetPtr.ToInt64(),
                ["attacker_ptr"] = (attacker.IsNull ? 0 : attacker.Pointer.ToInt64()),
                ["skill_ptr"] = (skill.IsNull ? 0 : skill.Pointer.ToInt64()),
                ["damage"] = damage,
                ["cancelled"] = cancel
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"ApplyDamage_Prefix failed: {ex.Message}"); }
    }

    /// <summary>
    /// Prefix patch for ItemContainer.Add @ 0x180821c80
    /// Signature: bool Add(ItemContainer this, Item item, bool expandSlots)
    /// </summary>
    private static void ItemContainerAdd_Prefix(object __instance, object __0, ref bool __1, ref bool __runOriginal)
    {
        if (OnItemAdd == null) return;

        try
        {
            var containerPtr = GetPointer(__instance);
            var container = new GameObj(containerPtr);

            // Extract item parameter (__0)
            var itemPtr = IntPtr.Zero;
            var item = GameObj.Null;

            if (__0 != null)
            {
                itemPtr = GetPointer(__0);
                item = new GameObj(itemPtr);
            }

            // expandSlots parameter is __1 (ref bool)
            bool expandSlots = __1;
            bool cancel = false;

            foreach (var handlerFunc in OnItemAdd.GetInvocationList().Cast<ItemAddInterceptor>())
            {
                try { handlerFunc(container, item, ref expandSlots, ref cancel); }
                catch (Exception ex) { ModError.WarnInternal("Intercept", $"OnItemAdd handler failed: {ex.Message}"); }
            }

            // Write modified expandSlots back
            __1 = expandSlots;

            if (cancel)
            {
                __runOriginal = false;
                return;
            }

            FireLuaEvent("item_add", new Dictionary<string, object>
            {
                ["container_ptr"] = containerPtr.ToInt64(),
                ["item_ptr"] = itemPtr.ToInt64(),
                ["expand_slots"] = expandSlots,
                ["cancelled"] = cancel
            });
        }
        catch (Exception ex) { ModError.WarnInternal("Intercept", $"ItemContainerAdd_Prefix failed: {ex.Message}"); }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  CONSOLE COMMANDS
    // ═══════════════════════════════════════════════════════════════════

    private static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("propertyinterceptors", "", "List registered property interceptors", args =>
        {
            var lines = new List<string> { "Registered Property Interceptors:" };

            void AddEventInfo(string name, Delegate evt)
            {
                var count = evt?.GetInvocationList()?.Length ?? 0;
                if (count > 0)
                    lines.Add($"  {name}: {count} handler(s)");
            }

            // Tier 1: EntityProperties
            AddEventInfo("OnGetDamage", OnGetDamage);
            AddEventInfo("OnGetAccuracy", OnGetAccuracy);
            AddEventInfo("OnGetArmor", OnGetArmor);
            AddEventInfo("OnGetConcealment", OnGetConcealment);
            AddEventInfo("OnGetDetection", OnGetDetection);
            AddEventInfo("OnGetVision", OnGetVision);

            // Tier 1: Skill
            AddEventInfo("OnGetHitChance", OnGetHitChance);
            AddEventInfo("OnGetExpectedDamage", OnGetExpectedDamage);
            AddEventInfo("OnGetCoverMult", OnGetCoverMult);

            // Tier 1: Actor
            AddEventInfo("OnHasLineOfSightTo", OnHasLineOfSightTo);
            AddEventInfo("OnSuppressionApplied", OnSuppressionApplied);
            AddEventInfo("OnMoraleApplied", OnMoraleApplied);

            // Tier 2: Skill Range and Cost
            AddEventInfo("OnGetExpectedSuppression", OnGetExpectedSuppression);
            AddEventInfo("OnGetActionPointCost", OnGetActionPointCost);
            AddEventInfo("OnGetIdealRangeBase", OnGetIdealRangeBase);
            AddEventInfo("OnGetMaxRangeBase", OnGetMaxRangeBase);
            AddEventInfo("OnGetMinRangeBase", OnGetMinRangeBase);
            AddEventInfo("OnIsInRange", OnIsInRange);
            AddEventInfo("OnIsInRangeShape", OnIsInRangeShape);
            AddEventInfo("OnIsMovementSkill", OnIsMovementSkill);
            AddEventInfo("OnSkillIsUsable", OnSkillIsUsable);

            // Tier 2: Entity State
            AddEventInfo("OnGetHitpointsPct", OnGetHitpointsPct);
            AddEventInfo("OnGetArmorDurabilityPct", OnGetArmorDurabilityPct);
            AddEventInfo("OnGetCoverUsage", OnGetCoverUsage);
            AddEventInfo("OnGetProvidedCover", OnGetProvidedCover);
            AddEventInfo("OnIsDiscovered", OnIsDiscovered);
            AddEventInfo("OnGetLastSkillUsed", OnGetLastSkillUsed);
            AddEventInfo("OnGetScaleRange", OnGetScaleRange);

            // Tile
            AddEventInfo("OnTileHasLineOfSightTo", OnTileHasLineOfSightTo);
            AddEventInfo("OnTileIsBlockingLineOfSight", OnTileIsBlockingLineOfSight);
            AddEventInfo("OnTileGetCover", OnTileGetCover);
            AddEventInfo("OnTileGetCoverMask", OnTileGetCoverMask);
            AddEventInfo("OnTileGetEntityProvidedCover", OnTileGetEntityProvidedCover);
            AddEventInfo("OnTileCanBeEntered", OnTileCanBeEntered);
            AddEventInfo("OnTileCanBeEnteredBy", OnTileCanBeEnteredBy);

            // BaseTile
            AddEventInfo("OnBaseTileHasCover", OnBaseTileHasCover);
            AddEventInfo("OnBaseTileHasHalfCover", OnBaseTileHasHalfCover);
            AddEventInfo("OnBaseTileHasHalfCoverInDir", OnBaseTileHasHalfCoverInDir);
            AddEventInfo("OnBaseTileIsMovementBlocked", OnBaseTileIsMovementBlocked);

            // LineOfSight
            AddEventInfo("OnLineOfSightRayTrace", OnLineOfSightRayTrace);
            AddEventInfo("OnLineOfSightIsNearTileCorner", OnLineOfSightIsNearTileCorner);

            // Movement
            AddEventInfo("OnGetMaxMovementSpeed", OnGetMaxMovementSpeed);
            AddEventInfo("OnGetTotalPathCost", OnGetTotalPathCost);
            AddEventInfo("OnGetTurnSpeed", OnGetTurnSpeed);
            AddEventInfo("OnGetSlowdownDistance", OnGetSlowdownDistance);
            AddEventInfo("OnGetMaxAngleTurnSlowdown", OnGetMaxAngleTurnSlowdown);
            AddEventInfo("OnClipPathToCost", OnClipPathToCost);
            AddEventInfo("OnMoveTo", OnMoveTo);
            AddEventInfo("OnPathfinding", OnPathfinding);
            AddEventInfo("OnTileTraversable", OnTileTraversable);

            // Strategy Layer
            AddEventInfo("OnStrategyGetActionPoints", OnStrategyGetActionPoints);
            AddEventInfo("OnStrategyGetHitpointsPerElement", OnStrategyGetHitpointsPerElement);
            AddEventInfo("OnStrategyGetDamageSustainedMult", OnStrategyGetDamageSustainedMult);
            AddEventInfo("OnStrategyGetHitpointsPct", OnStrategyGetHitpointsPct);
            AddEventInfo("OnStrategyCanBePromoted", OnStrategyCanBePromoted);
            AddEventInfo("OnStrategyCanBeDemoted", OnStrategyCanBeDemoted);
            AddEventInfo("OnStrategyGetEntityProperty", OnStrategyGetEntityProperty);
            AddEventInfo("OnStrategyGetVehicleArmor", OnStrategyGetVehicleArmor);

            // Combat Actions
            AddEventInfo("OnDamageApplied", OnDamageApplied);

            // Equipment Systems
            AddEventInfo("OnItemAdd", OnItemAdd);
            AddEventInfo("OnPropertyUpdate", OnPropertyUpdate);
            AddEventInfo("OnPropertyUpdateMult", OnPropertyUpdateMult);

            // AI Behavior
            AddEventInfo("OnAIGetAttackScore", OnAIGetAttackScore);
            AddEventInfo("OnAIGetThreatValue", OnAIGetThreatValue);
            AddEventInfo("OnAIGetActionPriority", OnAIGetActionPriority);
            AddEventInfo("OnAIShouldFlee", OnAIShouldFlee);
            AddEventInfo("OnAIEvaluate", OnAIEvaluate);
            AddEventInfo("OnPositionScore", OnPositionScore);

            return lines.Count > 1 ? string.Join("\n", lines) : "No interceptors registered";
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXTENSION GUIDE - How to add more interceptors
    // ═══════════════════════════════════════════════════════════════════

    /*
     * ADDING NEW PROPERTY INTERCEPTORS
     *
     * To add interception for a new property (Tier 2-4), follow these steps:
     *
     * 1. DELEGATE TYPE
     *    Define a delegate with the appropriate signature:
     *
     *    public delegate void MyIntercept(GameObj context, ref ResultType result);
     *
     * 2. EVENT
     *    Add a static event in the appropriate tier region:
     *
     *    public static event MyIntercept OnGetMyProperty;
     *
     * 3. PATCH METHOD
     *    Add a postfix patch method following the pattern:
     *
     *    private static void GetMyProperty_Postfix(object __instance, ref ReturnType __result)
     *    {
     *        if (OnGetMyProperty == null) return;
     *
     *        try
     *        {
     *            var ptr = GetPointer(__instance);
     *            var obj = new GameObj(ptr);
     *            var result = __result;
     *
     *            foreach (var handler in OnGetMyProperty.GetInvocationList().Cast<MyIntercept>())
     *            {
     *                try { handler(obj, ref result); }
     *                catch (Exception ex) { ModError.WarnInternal(...); }
     *            }
     *
     *            __result = result;
     *            FireLuaEvent("property_myproperty", new Dictionary<string, object> { ... });
     *        }
     *        catch (Exception ex) { ModError.WarnInternal(...); }
     *    }
     *
     * 4. INITIALIZATION
     *    Add the patch call in Initialize():
     *
     *    patchCount += PatchEntityPropertyMethod("GetMyProperty", nameof(GetMyProperty_Postfix));
     *
     * 5. DOCUMENTATION
     *    Add XML documentation explaining:
     *    - What the property represents
     *    - The formula used to compute it (from Ghidra analysis)
     *    - Relevant memory offsets
     *    - Use cases for modification
     *
     * TIER GUIDELINES:
     * - Tier 1: Core combat properties (damage, accuracy, armor, hit chance, LOS)
     * - Tier 2: Movement properties (AP costs, movement range, terrain modifiers)
     * - Tier 3: Status effect properties (suppression, morale, stun duration)
     * - Tier 4: Specialized properties (malfunction chance, cooldown reduction, etc.)
     *
     * GHIDRA ANALYSIS WORKFLOW:
     * 1. Use mcp__ghidra__search_functions_by_name to find the method
     * 2. Use mcp__ghidra__decompile_function to understand the formula
     * 3. Document the offsets and calculations in comments
     * 4. Test with the console commands to verify patching works
     */
}

// ═══════════════════════════════════════════════════════════════════════════════
//  TODO: TIER 3-4 PROPERTIES (spawn agents to implement these)
// ═══════════════════════════════════════════════════════════════════════════════

/*
 * TIER 2: Movement Properties (DONE - Skill range/cost interceptors implemented)
 * - Skill.GetExpectedSuppression @ 0x1806db2d0: Suppression preview
 * - Skill.GetActionPointCost @ 0x1806d8e80: AP cost calculation
 * - Skill.GetIdealRangeBase @ 0x1806dbea0: Optimal engagement range
 * - Skill.GetMaxRangeBase @ 0x1806dc8b0: Maximum range
 * - Skill.GetMinRangeBase @ 0x1806dc980: Minimum range
 * - Skill.IsInRange @ 0x1806de4f0: Range validity check
 * - Skill.IsInRangeShape @ 0x1806de390: Shape-based range check
 * - Skill.IsMovementSkill @ 0x1806de730: Movement skill type check
 *
 * TIER 3: Combat Status Properties
 * - EntityProperties.GetSuppression @ 0x18060c780
 * - EntityProperties.GetDiscipline @ 0x18060bdd0
 * - EntityProperties.GetArmorPenetration @ 0x18060bab0
 * - EntityProperties.GetDamageDropoff @ 0x18060bcd0
 * - EntityProperties.GetAccuracyDropoff @ 0x18060b9c0
 *
 * TIER 4: Specialized Properties
 * - EntityProperties.GetHitpointsPerElement @ 0x18060be10
 * - EntityProperties.GetMaxHitpoints @ 0x18060be40
 * - EntityProperties.GetArmorDurabilityPerElement @ 0x18060ba50
 * - EntityProperties.GetDamageToArmorDurability @ 0x18060bd30
 * - Skill.GetCooldown
 *
 * Each tier should be implemented by a separate agent to parallelize work.
 */
