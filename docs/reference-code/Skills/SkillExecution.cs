// =============================================================================
// MENACE REFERENCE CODE - Skill Execution Flow
// =============================================================================
// Documents how skills are executed from targeting through effect application.
// This is the main entry point for all skill usage.
//
// VERIFIED against binary at addresses documented below.
// Last updated: 2026-03-10
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.Skills
{
    // =========================================================================
    // USE FLAGS - Used in Skill.Use() method
    // =========================================================================

    /// <summary>
    /// Flags for skill usage control.
    /// These are passed as the second parameter to Skill.Use().
    /// </summary>
    [Flags]
    public enum SkillUseFlags
    {
        None = 0x00,
        /// <summary>Skip affordability check (AP cost)</summary>
        ForceAffordable = 0x01,
        /// <summary>Skip usability checks</summary>
        ForceUsable = 0x02,
        /// <summary>Skill triggered by ally skill (delegates to parent)</summary>
        FromAllySkill = 0x04,
        /// <summary>Hidden skill usage (no visual feedback)</summary>
        Hidden = 0x08,
        /// <summary>Check visibility for tile/actor to camera</summary>
        CheckVisibility = 0x10,
        /// <summary>Triggered from tile effect</summary>
        FromTileEffect = 0x13
    }

    // =========================================================================
    // EXPECTED DAMAGE RESULT STRUCTURE
    // =========================================================================

    /// <summary>
    /// Complex damage calculation result returned by Skill.GetExpectedDamage.
    /// This is NOT a simple float - it contains detailed breakdown.
    ///
    /// Address: Class at Menace.Tactical.Skills.Skill+ExpectedDamage
    /// </summary>
    public class ExpectedDamage
    {
        /// <summary>Base damage before armor. Offset: +0x10</summary>
        public float BaseDamage;

        /// <summary>Damage dealt to armor durability. Offset: +0x14</summary>
        public float ArmorDamage;

        /// <summary>Damage dealt to HP (after armor). Offset: +0x18</summary>
        public float HPDamage;

        /// <summary>Overkill damage. Offset: +0x1C</summary>
        public float OverkillDamage;

        /// <summary>Chance to penetrate armor (0.0-1.0). Offset: +0x20</summary>
        public float PenetrationChance;

        /// <summary>Can this kill the target? Offset: +0x24</summary>
        public bool CanKill;

        /// <summary>Will this definitely kill? Offset: +0x25</summary>
        public bool WillKill;

        /// <summary>Defender properties used in calculation. Offset: +0x28</summary>
        public EntityProperties DefenderProperties;
    }

    // =========================================================================
    // HIT CHANCE RESULT STRUCTURE
    // =========================================================================

    /// <summary>
    /// Result structure returned by Skill.GetHitchance.
    /// Contains full breakdown of hit chance calculation.
    /// </summary>
    public struct HitChanceResult
    {
        /// <summary>Final clamped hit chance (0 to max). Offset: +0x00</summary>
        public float FinalValue;

        /// <summary>Base accuracy from EntityProperties. Offset: +0x04</summary>
        public float Accuracy;

        /// <summary>Cover reduction multiplier (0.0-1.0). Offset: +0x08</summary>
        public float CoverMult;

        /// <summary>Defense multiplier = Flipped(evasion) = 2.0 - evasion. Offset: +0x0C</summary>
        public float DefenseMult;

        /// <summary>AlwaysHits flag from template. Offset: +0x10</summary>
        public bool AlwaysHits;

        /// <summary>Whether distance dropoff was included. Offset: +0x11</summary>
        public bool IncludeDropoff;

        /// <summary>Distance-based accuracy dropoff. Offset: +0x14</summary>
        public float DistanceDropoff;
    }

    // =========================================================================
    // SKILL CLASS
    // =========================================================================

    /// <summary>
    /// Runtime skill instance. Attached to entities via SkillContainer.
    ///
    /// Key field offsets verified from binary:
    /// +0x10: Template (SkillTemplate reference)
    /// +0x18: SkillContainer (owner container)
    /// +0x28: SkillItem (Item reference, for weapon skills)
    /// +0x39: IsRemoved flag (byte)
    /// +0x40: ParentSkill (Skill reference, for allied skills)
    /// +0x48: Handlers (List of ISkillHandler)
    /// +0x58: TargetTilesCache (List of Tile)
    /// +0xA0: ActionPointCost (runtime, int)
    /// +0xA4: MinActionPointCost (int)
    /// +0xA8: Charges (int)
    /// +0xB0: ChargesPerUse (int)
    /// +0xB4: MinRange (runtime, int)
    /// +0xB8: OptimalRange (runtime, int)
    /// +0xBC: MaxRange (runtime, int)
    /// +0xD0: ActorOverride (Actor reference)
    /// +0xD8: UseId (int, incremented globally per use)
    /// </summary>
    public class Skill : BaseSkill
    {
        /// <summary>Template definition. Offset: +0x10</summary>
        public SkillTemplate Template;

        /// <summary>Owner skill container. Offset: +0x18</summary>
        public SkillContainer Container;

        /// <summary>Associated item (for weapon skills). Offset: +0x28</summary>
        public Item SkillItem;

        /// <summary>Parent skill for ally-triggered skills. Offset: +0x40</summary>
        public Skill ParentSkill;

        /// <summary>List of skill handlers. Offset: +0x48</summary>
        public List<ISkillHandler> Handlers;

        /// <summary>Runtime AP cost (base + modifiers). Offset: +0xA0</summary>
        public int ActionPointCost;

        /// <summary>Minimum AP cost floor. Offset: +0xA4</summary>
        public int MinActionPointCost;

        /// <summary>Current charges remaining. Offset: +0xA8</summary>
        public int Charges;

        /// <summary>Charges consumed per use. Offset: +0xB0</summary>
        public int ChargesPerUse;

        /// <summary>Runtime minimum range. Offset: +0xB4</summary>
        public int MinRange;

        /// <summary>Optimal range for accuracy. Offset: +0xB8</summary>
        public int OptimalRange;

        /// <summary>Runtime maximum range. Offset: +0xBC</summary>
        public int MaxRange;

        /// <summary>Actor override (if different from owner). Offset: +0xD0</summary>
        public Actor ActorOverride;

        /// <summary>Global use ID for this activation. Offset: +0xD8</summary>
        public int UseId;

        // =====================================================================
        // MAIN SKILL EXECUTION
        // =====================================================================

        /// <summary>
        /// Main entry point for using a skill.
        ///
        /// Address: 0x1806e2bd0
        ///
        /// Flow:
        /// 1. If FromAllySkill and Template.ShouldUseParentSkill, delegate to ParentSkill
        /// 2. Check IsAffordable (unless ForceAffordable)
        /// 3. Check IsUsable (unless ForceUsable)
        /// 4. Increment global UseId counter
        /// 5. Get actor and final target tile (handlers can transform target)
        /// 6. Get best tile to attack from
        /// 7. Validate targeting, range, LOS, OnVerifyTarget (unless ForceUsable)
        /// 8. Set actor aiming state
        /// 9. Call SkillContainer.OnSkillUsed (unless FromAllySkill)
        /// 10. Deduct AP cost (unless ForceAffordable)
        /// 11. Consume charges if Template.HasLimitedUses
        /// 12. Notify targets via OnBeingTargetedByAttack
        /// 13. Call TacticalManager.InvokeOnSkillUse
        /// 14. Add to busy skills list
        /// 15. Schedule delayed completion callback
        /// </summary>
        /// <param name="targetTile">Target tile</param>
        /// <param name="flags">SkillUseFlags controlling execution</param>
        /// <returns>1 on success, 0 on failure</returns>
        public int Use(Tile targetTile, SkillUseFlags flags)
        {
            // FromAllySkill: delegate to parent skill if configured
            if ((flags & SkillUseFlags.FromAllySkill) != 0)
            {
                if (Template != null && Template.ShouldUseParentSkill)
                {
                    var currentActor = TacticalManager.Instance.CurrentTurnActor;
                    if (currentActor != GetActor() && ParentSkill != null)
                    {
                        return ParentSkill.Use(targetTile, flags);
                    }
                }
            }

            // Check affordability (unless ForceAffordable)
            if ((flags & SkillUseFlags.ForceAffordable) == 0)
            {
                if (!IsAffordable())
                    return 0;
            }

            // Check usability (unless ForceUsable)
            if ((flags & SkillUseFlags.ForceUsable) == 0)
            {
                if (!IsUsable(TargetUsageParams.Default))
                    return 0;
            }

            // Increment global use counter
            UseId = ++Skill.GlobalUseCounter;

            // Get actor
            Actor actor = GetActor();

            // Get final target tile (handlers can transform)
            Tile finalTargetTile = targetTile;
            if (Template != null && !Template.IsTargeted && actor != null)
            {
                finalTargetTile = actor.CurrentTile;
            }
            foreach (var handler in Handlers)
            {
                handler.TransformTargetTile(ref finalTargetTile);
            }

            // Get best tile to attack from
            Tile attackFromTile = actor != null
                ? Template.GetBestTileToAttackFrom(actor, finalTargetTile)
                : finalTargetTile;

            // Validation (unless ForceUsable)
            if ((flags & SkillUseFlags.ForceUsable) == 0)
            {
                if (!IsUsable(TargetUsageParams.Default))
                    return 0;

                if (Template.IsTargeted)
                {
                    // Check LOS
                    if (Template.RequiresLOS && attackFromTile != null)
                    {
                        if (!attackFromTile.HasLineOfSightTo(finalTargetTile))
                            return 0;
                    }

                    // Check range
                    if (!IsInRange(finalTargetTile, attackFromTile, false))
                        return 0;

                    // Verify target
                    if (!OnVerifyTarget(attackFromTile, finalTargetTile, TargetUsageParams.Default))
                        return 0;
                }
            }

            // Set aiming state
            if (actor != null && Template.IsTargeted && !Template.CanAimWhileMoving)
            {
                actor.SetAiming(true, this);
            }

            // OnSkillUsed event (unless FromAllySkill)
            if ((flags & SkillUseFlags.FromAllySkill) == 0 && Container != null)
            {
                Container.OnSkillUsed(this, finalTargetTile);
            }

            // Deduct AP cost (unless ForceAffordable)
            Actor costActor = ActorOverride ?? GetActor();
            if ((flags & SkillUseFlags.ForceAffordable) == 0 && costActor != null)
            {
                int cost = GetActionPointCost();
                if (cost > 0)
                {
                    costActor.SetActionPoints(costActor.GetActionPoints() - cost);
                }
            }

            // Consume charges (unless ForceAffordable)
            if ((flags & SkillUseFlags.ForceAffordable) == 0 && Template.HasLimitedUses)
            {
                if (Actor.ConsumeChargesOnUse)
                {
                    int chargesToUse = ChargesPerUse > 0 ? ChargesPerUse : 0;
                    Charges -= chargesToUse;
                }
            }

            // Process handlers
            foreach (var handler in Handlers)
            {
                if (!handler.CanApply(actor))
                {
                    if (actor != null) actor.SetAiming(false, this);
                    return 0;
                }
            }

            // Notify targets (for AoE)
            if ((flags & SkillUseFlags.FromAllySkill) == 0 && Template.IsTargeted && Template.RequiresLOS)
            {
                NotifyTargetsOfAttack(actor, attackFromTile, finalTargetTile);
            }

            // Notify TacticalManager
            if ((flags & SkillUseFlags.FromAllySkill) == 0)
            {
                TacticalManager.Instance.InvokeOnSkillUse(actor, this, finalTargetTile);
                if (actor != null) actor.UpdateActorState();
            }

            // Add to busy skills
            if (Container != null)
            {
                Container.CurrentSkill = this;
            }
            if ((flags & SkillUseFlags.FromAllySkill) == 0)
            {
                TacticalManager.Instance.AddBusySkill(this);
            }

            // Schedule completion callback with delay based on visibility
            float delay = 0f;
            // ... visibility checks determine delay ...
            Schedule.Delay(delay, OnUseComplete);

            return 1;
        }

        // =====================================================================
        // AFFORDABILITY CHECK
        // =====================================================================

        /// <summary>
        /// Checks if the skill can be afforded (AP cost).
        ///
        /// Address: 0x1806de0d0
        /// </summary>
        public bool IsAffordable()
        {
            Actor actor = ActorOverride ?? GetActor();
            if (actor != null && actor.CanAct())
            {
                int cost = GetActionPointCost();
                int currentAP = actor.GetActionPoints();
                return cost <= currentAP;
            }
            return false;
        }

        // =====================================================================
        // USABILITY CHECK
        // =====================================================================

        /// <summary>
        /// Checks if the skill can be used.
        ///
        /// Address: 0x1806deb10 (with params)
        /// Address: 0x1806ded60 (wrapper, uses default params)
        ///
        /// Checks:
        /// 1. Actor exists and has no blocking states
        /// 2. Limited uses check (if Template.HasLimitedUses at +0xB8)
        /// 3. Deployment requirements satisfied
        /// 4. Mobility state check (Template +0x113 vs Actor +0x16F)
        /// 5. Action state check (value 2 = special handling at +0x115)
        /// 6. HasExecutingElements virtual check
        /// 7. Item slot check for targeting types 1 or 3
        /// 8. All handlers return IsUsable = true
        /// </summary>
        public bool IsUsable(TargetUsageParams usageParams)
        {
            Actor actor = GetActor();
            if (actor == null)
                return false;

            // Check if actor blocked by stylesheet paths state
            if (Template != null && !Template.IgnoreActorState)
            {
                if (actor.HasStylesheetPaths)
                {
                    var element = actor.GetElement();
                    if (element != null && element.IsBlocking())
                        return false;
                }
            }

            // Limited uses check
            if (usageParams.CheckLimitedUses && Template != null && Template.HasLimitedUses)
            {
                int maxCharges = ChargesPerUse > 0 ? ChargesPerUse : 0;
                if (Charges < maxCharges)
                    return false;
            }

            // Deployment requirement check
            if (usageParams.CheckDeployment && Template != null)
            {
                if (!Template.IsDeploymentRequirementSatisfied(actor))
                    return false;

                // Mobility state check
                if (Template.RequiresMobility && !actor.IsMobile)
                    return false;
            }

            // Action state check
            int actionState = actor.GetActionState();
            if (actionState == 2)
            {
                if (Template != null && !Template.AllowsDuringSpecialAction)
                    return false;
            }

            // HasExecutingElements check
            if (!HasExecutingElements())
                return false;

            // Item slot check for certain targeting types
            int targetingType = Template != null ? Template.TargetingType : 0;
            if ((targetingType == 1 || targetingType == 3) && actor != null)
            {
                if (actor.Elements != null && actor.Elements.Count < 2)
                {
                    var itemContainer = actor.GetItemContainer();
                    if (itemContainer != null)
                    {
                        var item = itemContainer.GetItemAtSlot(1);
                        if (item != null)
                            return false;
                    }
                }
            }

            // Check all handlers
            foreach (var handler in Handlers)
            {
                if (!handler.IsUsable())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Simple usability check with default params.
        ///
        /// Address: 0x1806ded60
        /// </summary>
        public bool IsUsable()
        {
            return IsUsable(TargetUsageParams.Default);
        }

        // =====================================================================
        // ACTION POINT COST
        // =====================================================================

        /// <summary>
        /// Gets the action point cost for this skill.
        ///
        /// Address: 0x1806d8e80
        ///
        /// Formula:
        /// - Base cost from +0xA0
        /// - If Template.HasCost (+0xF2):
        ///   - Get cost modifier from actor
        ///   - cost = (baseCost + modifier.FlatBonus) * modifier.Multiplier
        /// - Floor to MinActionPointCost at +0xA4
        /// </summary>
        public int GetActionPointCost()
        {
            int cost = ActionPointCost;  // +0xA0

            if (Template != null && Template.HasCost)  // +0xF2
            {
                Actor actor = ActorOverride ?? GetActor();
                if (actor != null)
                {
                    var modifier = actor.GetCostModifier();
                    if (modifier != null)
                    {
                        // modifier +0x44 = FlatBonus, +0x48 = Multiplier
                        cost = (int)((cost + modifier.FlatBonus) * modifier.Multiplier);
                    }
                }
            }

            // Floor to minimum
            if (cost < MinActionPointCost)
                cost = MinActionPointCost;

            return cost;
        }

        // =====================================================================
        // RANGE CHECK
        // =====================================================================

        /// <summary>
        /// Simple range check with bonus.
        ///
        /// Address: 0x1806de4f0
        /// </summary>
        /// <param name="distance">Distance in tiles (int)</param>
        /// <param name="bonus">Range bonus to add to max range</param>
        public bool IsInRange(int distance, int bonus)
        {
            if (distance < MinRange)  // +0xB4
                return false;
            return distance <= MaxRange + bonus;  // +0xBC
        }

        /// <summary>
        /// Complex range check from tile to tile.
        ///
        /// Address: 0x1806de510
        ///
        /// If shape type 1 (Template +0x11C == 1):
        /// - Calculates distance from entity position to source tile
        /// - Checks against Template.ShapeMaxDistance (+0x120)
        ///
        /// Then standard range check against MinRange/MaxRange.
        /// </summary>
        /// <param name="targetTile">Target tile</param>
        /// <param name="fromTile">Source tile (or null to auto-determine)</param>
        /// <param name="checkShapeDistance">Whether to check shape distance</param>
        public bool IsInRange(Tile targetTile, Tile fromTile, bool checkShapeDistance)
        {
            if (Template == null)
                return false;

            // If not targeted, always in range
            if (!Template.IsTargeted)  // +0xE4
                return true;

            // Shape distance check
            if (checkShapeDistance && Template.ShapeType == 1)  // +0x11C
            {
                Entity entity = GetEntity();
                if (entity != null && fromTile != null)
                {
                    var entityPos = entity.Position;
                    var sourcePos = fromTile.GetPos();
                    float shapeDistance = Vector3.Distance(entityPos, sourcePos);

                    if (shapeDistance > (float)Template.ShapeMaxDistance)  // +0x120
                        return false;
                }
            }

            // Get attack-from tile if not specified
            if (fromTile == null)
            {
                Actor actor = GetActor();
                fromTile = Template.GetBestTileToAttackFrom(actor, targetTile);
            }

            int distance = fromTile.GetDistanceTo(targetTile);

            if (distance < MinRange)  // +0xB4
                return false;

            return distance <= MaxRange;  // +0xBC
        }

        // =====================================================================
        // HIT CHANCE CALCULATION
        // =====================================================================

        /// <summary>
        /// Master hit chance calculation.
        ///
        /// Address: 0x1806dba90
        ///
        /// Final formula: hitChance = accuracy * coverMult * defenseMult + distanceDropoff
        ///
        /// Components:
        /// 1. accuracy = GetAccuracy() from EntityProperties * AccuracyMult
        /// 2. coverMult = cover reduction factor (0.0-1.0)
        /// 3. defenseMult = Flipped(evasion) = 2.0 - evasion
        /// 4. distanceDropoff = |distance - optimalRange| * GetAccuracyDropoff()
        ///
        /// Result clamped to [0, DAT_182d8fd78] and floored to MinAccuracy.
        /// </summary>
        /// <param name="result">Output result struct</param>
        /// <param name="fromTile">Source tile</param>
        /// <param name="targetTile">Target tile</param>
        /// <param name="attackerProps">Attacker properties (or null to build)</param>
        /// <param name="defenderProps">Defender properties (or null to build)</param>
        /// <param name="includeDropoff">Whether to include distance dropoff</param>
        /// <param name="overrideTarget">Target entity override</param>
        /// <param name="forImmediateUse">Whether for immediate use (affects inside-cover)</param>
        /// <returns>Filled HitChanceResult</returns>
        public HitChanceResult GetHitchance(
            Tile fromTile,
            Tile targetTile,
            EntityProperties attackerProps,
            EntityProperties defenderProps,
            bool includeDropoff,
            Entity overrideTarget,
            bool forImmediateUse)
        {
            var result = new HitChanceResult();

            // AlwaysHits check
            if (Template != null && Template.AlwaysHits)  // +0xF3
            {
                result.AlwaysHits = true;
                result.FinalValue = 100f;
                return result;
            }

            // Get target entity
            Entity target = overrideTarget;
            if (target == null && targetTile != null && !targetTile.IsEmpty())
            {
                target = targetTile.GetEntity();
            }

            // Build attacker properties if needed
            if (attackerProps == null)
            {
                if (Container != null)
                {
                    attackerProps = Container.BuildPropertiesForUse(this, fromTile, targetTile, target);
                }
                else
                {
                    attackerProps = new EntityProperties();
                }
            }

            // Get defender multiplier
            float defenseMult = 1.0f;
            if (target != null)
            {
                // Build defender properties if needed
                if (defenderProps == null)
                {
                    var defenderContainer = target.GetSkillContainer();
                    var attackerActor = GetActor();
                    if (defenderContainer != null)
                    {
                        defenderProps = defenderContainer.BuildPropertiesForDefense(
                            attackerActor, fromTile, targetTile, this);
                    }
                }

                // defenseMult = Flipped(evasion) = 2.0 - evasion
                if (defenderProps != null)
                {
                    float evasion = defenderProps.GetEvasion();
                    defenseMult = 2.0f - evasion;  // Flipped()
                }
            }

            // Get accuracy from attacker properties
            float accuracy = attackerProps.GetAccuracy();

            // Check if target is inside another entity
            bool isInsideEntity = false;
            if (!forImmediateUse && targetTile != null && !targetTile.IsEmpty())
            {
                Entity tileEntity = targetTile.GetEntity();
                if (target != tileEntity)
                {
                    isInsideEntity = target.IsContainableWithin(tileEntity);
                }
            }

            // Get cover multiplier
            float coverMult = GetCoverMult(fromTile, targetTile, target, defenderProps, isInsideEntity);

            // Store intermediate values
            result.Accuracy = accuracy;
            result.CoverMult = coverMult;
            result.DefenseMult = defenseMult;

            float hitChance;

            if (!includeDropoff || fromTile == null)
            {
                // Simple formula without dropoff
                float clampedCover = Mathf.Clamp01(coverMult);
                float clampedDefense = Mathf.Clamp01(defenseMult);
                hitChance = accuracy * clampedCover * clampedDefense;
            }
            else
            {
                // Include distance dropoff
                int distance = fromTile.GetDistanceTo(targetTile);
                int optimalRange = OptimalRange;  // +0xB8
                int distanceFromOptimal = Math.Abs(distance - optimalRange);

                float dropoffPerTile = attackerProps.GetAccuracyDropoff();
                float distanceDropoff = (float)distanceFromOptimal * dropoffPerTile;

                result.IncludeDropoff = true;
                result.DistanceDropoff = distanceDropoff;

                float clampedCover = Mathf.Clamp01(coverMult);
                float clampedDefense = Mathf.Clamp01(defenseMult);
                hitChance = accuracy * clampedCover * clampedDefense + distanceDropoff;
            }

            // Clamp to valid range
            if (hitChance < 0f)
                hitChance = 0f;
            else if (hitChance > Config.MaxHitChance)  // DAT_182d8fd78
                hitChance = Config.MaxHitChance;

            // Floor to minimum accuracy if specified
            int minAccuracy = attackerProps.MinAccuracy;  // +0x78 on EntityProperties
            if (hitChance < (float)minAccuracy)
                hitChance = (float)minAccuracy;

            result.FinalValue = hitChance;
            return result;
        }

        // =====================================================================
        // COVER MULTIPLIER
        // =====================================================================

        /// <summary>
        /// Calculates cover damage/accuracy reduction multiplier.
        ///
        /// Address: 0x1806d9bb0
        /// </summary>
        public float GetCoverMult(
            Tile fromTile,
            Tile targetTile,
            Entity target,
            EntityProperties defenderProps,
            bool isInsideEntity)
        {
            float coverMult = 1.0f;

            if (Template == null)
                return coverMult;

            // AlwaysHits ignores cover
            if (Template.AlwaysHits)  // +0xF3
                return coverMult;

            // Get target if needed
            if (target == null && targetTile != null && !targetTile.IsEmpty())
            {
                target = targetTile.GetEntity();
            }

            // Build defender properties if needed
            if (defenderProps == null && target != null)
            {
                var defenderContainer = target.GetSkillContainer();
                var attackerActor = GetActor();
                if (defenderContainer != null)
                {
                    defenderProps = defenderContainer.BuildPropertiesForDefense(
                        attackerActor, fromTile, targetTile, this);
                }
            }

            if (target == null || defenderProps == null)
                return coverMult;

            // Get evasion clamped
            float evasion = Mathf.Clamp01(defenderProps.GetEvasionValue());  // +0x88
            if (evasion <= 0f)
                return coverMult;

            // Check if ignoring cover for inside targets
            if (Template.IsIgnoringCoverInsideForTarget(targetTile.GetEntity()))
            {
                return coverMult;
            }

            // Check for inside entity cover
            if ((isInsideEntity || target.HasStylesheetPaths) &&
                !targetTile.IsEmpty())
            {
                Entity tileEntity = targetTile.GetEntity();
                if (target != tileEntity && tileEntity != null)
                {
                    var actorConfig = tileEntity.GetActorConfig();
                    if (actorConfig != null && actorConfig.CoverTemplate != null)
                    {
                        // Get cover value from containing entity
                        float containerCover = actorConfig.CoverTemplate.CoverValue;  // +0x58
                        float reduction = containerCover / evasion;
                        if (reduction <= 1.0f)
                            coverMult *= reduction;
                        return coverMult;
                    }
                }
            }

            // Standard cover check
            if (!Template.IgnoreCoverInside)  // +0x100
            {
                if (fromTile != null)
                {
                    int distance = fromTile.GetDistanceTo(targetTile);
                    if (distance >= 2)
                    {
                        Direction dir = targetTile.GetDirectionTo(fromTile);
                        Entity tileEntity = targetTile.GetEntity();
                        int coverLevel = targetTile.GetCover(dir, target, defenderProps, target == tileEntity);

                        // Lookup cover multiplier from config
                        float[] coverMultipliers = Config.Instance.CoverDamageMultipliers;  // +0x88
                        float rawCoverMult = coverMultipliers[coverLevel];

                        float reduction = rawCoverMult / evasion;
                        if (reduction <= 1.0f)
                            coverMult *= reduction;
                    }
                }
            }

            return coverMult;
        }

        // =====================================================================
        // EXPECTED DAMAGE CALCULATION
        // =====================================================================

        /// <summary>
        /// Complex damage calculation for preview/AI.
        ///
        /// Address: 0x1806da4b0
        ///
        /// This is a very complex function (~200 lines decompiled) that accounts for:
        /// - Distance-based damage dropoff
        /// - Armor penetration vs armor rating
        /// - Armor durability effects
        /// - Cover damage reduction
        /// - Element count multiplier
        /// - Uses per turn factor
        /// - Handler contributions via IExpectedDamageContributor
        /// </summary>
        public ExpectedDamage GetExpectedDamage(
            Tile fromTile,
            Tile targetTile,
            Tile overrideTile,
            Entity target,
            EntityProperties attackerProps,
            EntityProperties defenderProps,
            int usesPerTurn,
            ExpectedDamage result)
        {
            // Create result if not provided
            if (result == null)
            {
                result = new ExpectedDamage();
            }

            // Get target from tile if not provided
            if (target == null && targetTile != null && !targetTile.IsEmpty())
            {
                target = targetTile.GetEntity();
                if (target == null)
                    return result;
            }

            // Determine effective target tile
            Tile effectiveTile = overrideTile ?? targetTile;

            // Build attacker properties if needed
            if (attackerProps == null)
            {
                if (Container != null)
                {
                    attackerProps = Container.BuildPropertiesForUse(this, fromTile, targetTile, target);
                }
                else
                {
                    attackerProps = new EntityProperties();
                }
            }

            // Check if inside containing entity
            Tile entityTile = target.GetCurrentTile();
            bool isInsideEntity = (targetTile == entityTile) && target.HasStylesheetPaths;
            if (!isInsideEntity && targetTile != null && !targetTile.IsEmpty())
            {
                Entity tileEntity = targetTile.GetEntity();
                if (target != tileEntity)
                {
                    isInsideEntity = target.IsContainableWithin(tileEntity);
                }
            }

            // Build defender properties if needed
            if (defenderProps == null)
            {
                var defenderContainer = target.GetSkillContainer();
                var attackerActor = GetActor();
                if (defenderContainer != null)
                {
                    defenderProps = defenderContainer.BuildPropertiesForDefense(
                        attackerActor, fromTile, targetTile, this);
                }
            }

            result.DefenderProperties = defenderProps;

            // Get evasion/cover reduction
            float evasion = Mathf.Clamp01(defenderProps?.GetEvasionValue() ?? 0f);

            // Calculate distance-based dropoffs
            float damageDistanceDropoff = 0f;
            float armorDropoff = 0f;
            float durabilityDropoff = 0f;

            if (targetTile != effectiveTile && Template != null && Template.AoEType != 0)
            {
                int distance = effectiveTile.GetDistanceTo(targetTile);
                damageDistanceDropoff = distance * (attackerProps?.DamageDistanceDropoff ?? 0f);  // +0x128
                armorDropoff = distance * (attackerProps?.ArmorPenDropoff ?? 0f);  // +0x110
                durabilityDropoff = distance * (attackerProps?.DurabilityDropoff ?? 0f);  // +0x13C
            }

            // Calculate cover multiplier
            Direction dir = targetTile.GetDirectionTo(fromTile);
            float coverDamageMult = 1.0f;

            if (evasion > 0f)
            {
                // Check ignore cover for inside
                Entity tileEntity = targetTile.GetEntity();
                bool ignoreCover = Template.IsIgnoringCoverInsideForTarget(tileEntity);

                if (!ignoreCover && isInsideEntity && target != tileEntity)
                {
                    // Inside entity cover
                    var containerConfig = tileEntity?.GetActorConfig();
                    if (containerConfig?.CoverTemplate != null)
                    {
                        float containerCover = containerConfig.CoverTemplate.DamageReduction;  // +0xE0
                        coverDamageMult *= containerCover / evasion;
                    }
                }
                else if (!Template.IgnoreCoverInside)  // +0x100
                {
                    int distance = fromTile.GetDistanceTo(targetTile);
                    if (distance >= 2)
                    {
                        int coverLevel = targetTile.GetCover(dir, target, defenderProps, true);
                        float[] coverMultipliers = Config.Instance.CoverDamageMultipliers;  // +0x90
                        coverDamageMult *= coverMultipliers[coverLevel] / evasion;
                    }
                }
            }

            // Calculate distance from optimal range
            int distanceFromSource = fromTile.GetDistanceTo(targetTile);
            int distanceFromOptimal = Math.Abs(distanceFromSource - MinRange);  // +0xB4

            // Get base damage values
            if (attackerProps == null)
                return result;

            float baseDamage = attackerProps.GetDamage();
            float damageDropoff = attackerProps.GetDamageDropoff();
            float armorScaling = (float)target.Armor * (attackerProps.ArmorDamageScale);  // +0x144
            float minArmorDamage = attackerProps.MinArmorDamage;  // +0x148
            float hpScaling = (float)target.HP * (attackerProps.HPDamageScale);  // +0x14C
            float minHPDamage = attackerProps.MinHPDamage;  // +0x150

            // Calculate final damage
            float totalDamage = baseDamage + damageDropoff * distanceFromOptimal +
                               damageDistanceDropoff +
                               Math.Max(armorScaling, minArmorDamage) +
                               Math.Max(hpScaling, minHPDamage);

            totalDamage *= Mathf.Clamp01(coverDamageMult);
            totalDamage *= defenderProps.DamageMultiplier;  // +0x8C
            totalDamage *= attackerProps.FinalDamageMult;  // +0x140

            // Apply element count and uses per turn
            int elementCount = GetExecutingElementsAmount(Template.ElementType);
            float elementMult = Template.ElementDamageReduction;  // +0x244
            float effectiveElements = Math.Max(1f - elementCount * elementMult, 1f);

            if (usesPerTurn == 0)
            {
                usesPerTurn = GetAverageUsesPerTurn();
            }

            // Process handler contributions
            foreach (var handler in Handlers)
            {
                if (handler is IExpectedDamageContributor contributor)
                {
                    contributor.OnCalculateExpectedDamage(
                        fromTile, targetTile, effectiveTile, target, usesPerTurn, result);
                }
            }

            // Armor penetration calculation
            if (totalDamage > 0f && target.Elements != null)
            {
                int elementHP = target.Elements.Count;
                float hpPerElement = (float)target.Armor / elementHP;

                // Armor penetration
                float armorPen = attackerProps.GetArmorPenetration();
                float armorPenDropoff = attackerProps.GetArmorPenetrationDropoff();
                float totalPen = armorPen + armorPenDropoff * distanceFromOptimal + armorDropoff;
                if (totalPen < 0f) totalPen = 0f;

                int defenderArmor = defenderProps.GetArmor();
                float armorDurability = target.GetArmorDurabilityPct();

                // Penetration chance calculation
                float penChance = (Config.MaxHitChance - (armorDurability * defenderArmor - totalPen) * Config.ArmorScaling) * Config.ArmorPenMult;
                penChance = Mathf.Clamp(penChance, 0f, 1f);
                result.PenetrationChance = Math.Max(result.PenetrationChance, penChance);

                // Directional armor check
                int directionalArmor = defenderProps.GetArmor((int)dir);
                float armorReduction = (armorDurability * directionalArmor - totalPen * 2) * Config.ArmorMult;
                armorReduction = Mathf.Clamp(armorReduction, 0f, 1f);

                float damageToHP = Math.Max(Config.MinDamagePercent, 1f - armorReduction);
                totalDamage *= damageToHP;

                // Calculate effective damage per use
                int maxElements = Math.Min(elementCount * (int)effectiveElements * usesPerTurn, elementHP);

                // HP damage
                if (penChance > 0f)
                {
                    float damagePerHP = Math.Min(totalDamage, hpPerElement);
                    result.BaseDamage += maxElements * damagePerHP * penChance;
                }

                result.HPDamage = Math.Min(result.BaseDamage / hpPerElement, elementHP);
                result.OverkillDamage = result.BaseDamage / hpPerElement;

                // Armor durability damage
                float durabilityDamage = attackerProps.GetDamageToArmorDurability();
                float durabilityDropoffVal = attackerProps.GetDamageToArmorDurabilityDropoff();
                float totalDurabilityDamage = (durabilityDamage + durabilityDropoffVal * distanceFromOptimal + durabilityDropoff);
                totalDurabilityDamage *= armorDurability * armorDurability;

                float maxDurabilityDamage = (float)target.ArmorDurability / elementHP;
                totalDurabilityDamage = Math.Min(totalDurabilityDamage, maxDurabilityDamage);

                result.ArmorDamage = maxElements * totalDurabilityDamage * ((1f - penChance) + penChance);

                // Can/will kill flags
                result.CanKill = result.CanKill || (elementHP <= result.HPDamage);
                result.WillKill = result.WillKill || (elementHP <= result.BaseDamage / hpPerElement);
            }

            return result;
        }

        // =====================================================================
        // RANGE GETTERS
        // =====================================================================

        /// <summary>
        /// Gets minimum range from template or weapon.
        ///
        /// Address: 0x1806dc980
        ///
        /// If Template.UseWeaponRange (+0x124) is false and SkillItem is a weapon:
        ///   returns WeaponTemplate.MinRange (+0x140 on WeaponTemplate)
        /// Otherwise:
        ///   returns Template.MinRange (+0x128)
        /// </summary>
        public int GetMinRangeBase()
        {
            if (Template == null)
                return 0;

            // Check if using weapon range
            if (!Template.UseWeaponRange && SkillItem != null)  // +0x124
            {
                var itemTemplate = SkillItem.GetTemplate();
                if (itemTemplate != null && itemTemplate.ItemType == 1)  // Weapon type
                {
                    if (itemTemplate is WeaponTemplate weapon)
                    {
                        return weapon.MinRange;  // +0x140 = offset 0x28 * 8
                    }
                }
            }

            return Template.MinRange;  // +0x128
        }

        /// <summary>
        /// Gets maximum range from template or weapon.
        ///
        /// Address: 0x1806dc8b0
        ///
        /// If Template.UseWeaponRange (+0x124) is false and SkillItem is a weapon:
        ///   returns WeaponTemplate.MaxRange (+0x148 on WeaponTemplate)
        /// Otherwise:
        ///   returns Template.MaxRange (+0x130)
        /// </summary>
        public int GetMaxRangeBase()
        {
            if (Template == null)
                return 0;

            // Check if using weapon range
            if (!Template.UseWeaponRange && SkillItem != null)  // +0x124
            {
                var itemTemplate = SkillItem.GetTemplate();
                if (itemTemplate != null && itemTemplate.ItemType == 1)  // Weapon type
                {
                    if (itemTemplate is WeaponTemplate weapon)
                    {
                        return weapon.MaxRange;  // +0x148 = offset 0x29 * 8
                    }
                }
            }

            return Template.MaxRange;  // +0x130
        }

        /// <summary>
        /// Gets current charges.
        ///
        /// Address: 0x1806d9b00
        ///
        /// First checks handlers implementing ISkillChargeHandler.
        /// If none, returns Charges at +0xA8.
        /// </summary>
        public int GetCharges()
        {
            foreach (var handler in Handlers)
            {
                if (handler is ISkillChargeHandler chargeHandler)
                {
                    return chargeHandler.GetCharges();
                }
            }
            return Charges;  // +0xA8
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        /// <summary>Global use counter for unique UseIds.</summary>
        private static int GlobalUseCounter = 0;

        public Actor GetActor() => BaseSkill.GetActor(this);
        public Entity GetEntity() => BaseSkill.GetEntity(this);

        protected virtual bool HasExecutingElements() => true;
        protected virtual int GetExecutingElementsAmount(int elementType) => 1;
        protected virtual int GetAverageUsesPerTurn() => 1;

        private bool OnVerifyTarget(Tile fromTile, Tile targetTile, TargetUsageParams usageParams)
        {
            foreach (var handler in Handlers)
            {
                if (!handler.OnVerifyTarget(fromTile, targetTile, usageParams))
                    return false;
            }
            return true;
        }

        private void NotifyTargetsOfAttack(Actor attacker, Tile fromTile, Tile targetTile)
        {
            // For AoE skills, query all affected tiles
            if (Template.AoEType != 0)
            {
                QueryTargetTiles(fromTile, targetTile, out List<Tile> tiles, 2);
                foreach (var tile in tiles)
                {
                    Entity target = tile.GetEntity();
                    target?.OnBeingTargetedByAttack(this, attacker);
                }
            }
            else if (!targetTile.IsEmpty())
            {
                Entity target = targetTile.GetEntity();
                target?.OnBeingTargetedByAttack(this, attacker);
                if (target.HasStylesheetPaths)
                {
                    Entity container = target.ContainedBy;
                    container?.OnBeingTargetedByAttack(this, attacker);
                }
            }
        }

        private void QueryTargetTiles(Tile fromTile, Tile targetTile, out List<Tile> tiles, int flags)
        {
            tiles = new List<Tile>();
            // Implementation queries tiles based on AoE shape
        }

        private void OnUseComplete()
        {
            // Cleanup after skill use completion
        }
    }

    // =========================================================================
    // SKILL CONTAINER CLASS
    // =========================================================================

    /// <summary>
    /// Container holding all skills for an entity.
    ///
    /// Field offsets verified from binary:
    /// +0x10: Owner (Entity reference)
    /// +0x18: Skills (List of BaseSkill)
    /// +0x30: LastUsedSkill (Skill reference)
    /// +0x38: LastTargetTile (Tile reference)
    /// +0x40: LastTargetEntity (Entity reference)
    /// +0x50: CurrentSkill (Skill reference)
    /// </summary>
    public class SkillContainer
    {
        /// <summary>Owner entity. Offset: +0x10</summary>
        public Entity Owner;

        /// <summary>All skills. Offset: +0x18</summary>
        public List<BaseSkill> Skills;

        /// <summary>Last used skill. Offset: +0x30</summary>
        public Skill LastUsedSkill;

        /// <summary>Last target tile. Offset: +0x38</summary>
        public Tile LastTargetTile;

        /// <summary>Last target entity. Offset: +0x40</summary>
        public Entity LastTargetEntity;

        /// <summary>Currently executing skill. Offset: +0x50</summary>
        public Skill CurrentSkill;

        // =====================================================================
        // SKILL LOOKUP
        // =====================================================================

        /// <summary>
        /// Gets skill by string ID.
        ///
        /// Address: 0x1806e91b0
        ///
        /// Iterates through Skills list (+0x18), skipping removed skills (+0x39 != 0).
        /// Compares via virtual GetID() call.
        /// Optional: filters by SkillSource at +0x28.
        /// </summary>
        public Skill GetSkillByID(string id, SkillSource source = null)
        {
            foreach (var baseSkill in Skills)
            {
                // Skip removed skills
                if (baseSkill.IsRemoved)  // +0x39
                    continue;

                // Compare ID
                if (baseSkill.GetID() == id)
                {
                    // Optional source filter
                    if (source != null && baseSkill.Source != source)  // +0x28
                        continue;

                    return baseSkill as Skill;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets skill by template reference.
        ///
        /// Address: 0x1806e9340
        ///
        /// Similar to GetSkillByID but compares Template reference (+0x10)
        /// using Unity's Object.op_Equality.
        /// </summary>
        public Skill GetSkillByTemplate(SkillTemplate template, SkillSource source = null)
        {
            foreach (var baseSkill in Skills)
            {
                // Skip removed skills
                if (baseSkill.IsRemoved)  // +0x39
                    continue;

                // Compare template reference
                if (baseSkill.Template == template)
                {
                    // Optional source filter
                    if (source != null && baseSkill.Source != source)  // +0x28
                        continue;

                    return baseSkill as Skill;
                }
            }
            return null;
        }

        /// <summary>
        /// Collects all tags from skills into a HashSet.
        ///
        /// Address: 0x1806ed3f0
        ///
        /// For each Skill (type-checked), gets Template.Tags at +0xA8.
        /// For each tag where tag.SomeValue (+0x8C) != 0, adds to output.
        ///
        /// Note: This does NOT filter skills by tag - it collects all tags.
        /// </summary>
        public void QueryTags(HashSet<TagTemplate> outputTags)
        {
            foreach (var baseSkill in Skills)
            {
                // Type check for Skill
                if (!(baseSkill is Skill skill))
                    continue;

                if (skill.Template == null)
                    continue;

                // Iterate tags
                var tags = skill.Template.Tags;  // +0xA8
                if (tags == null)
                    continue;

                foreach (var tag in tags)
                {
                    if (tag.Value != 0)  // +0x8C on TagTemplate
                    {
                        outputTags.Add(tag);
                    }
                }
            }
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        /// <summary>
        /// Called when a skill is used.
        ///
        /// Address: 0x1806ebfa0
        ///
        /// Stores skill and target tile references.
        /// If skill is an attack, stores target entity.
        /// Calls OnAnySkillUsed.
        /// </summary>
        public void OnSkillUsed(Skill skill, Tile targetTile)
        {
            LastUsedSkill = skill;  // +0x30
            LastTargetTile = targetTile;  // +0x38

            if (skill.IsAttack())
            {
                Entity target = targetTile?.GetEntity();
                LastTargetEntity = target;  // +0x40
            }

            OnAnySkillUsed(skill);
        }

        private void OnAnySkillUsed(Skill skill)
        {
            // Notify all skills of this usage
        }

        // =====================================================================
        // PROPERTY BUILDING
        // =====================================================================

        /// <summary>
        /// Builds attacker EntityProperties for skill use.
        ///
        /// Address: 0x1806e81f0
        ///
        /// Clones base properties from Owner, then iterates all skills
        /// calling their ApplyToEntityProperties method.
        /// </summary>
        public EntityProperties BuildPropertiesForUse(
            Skill skill,
            Tile fromTile,
            Tile targetTile,
            Entity target)
        {
            if (Owner == null)
                return null;

            // Clone base properties
            var baseProps = Owner.GetEntityProperties();
            var props = baseProps.GetClone();

            // Apply skill modifiers
            foreach (var baseSkill in Skills)
            {
                if (baseSkill.IsRemoved)  // +0x39
                    continue;

                if (!baseSkill.HasExecutingElements())
                    continue;

                baseSkill.ApplyToEntityProperties(skill, fromTile, targetTile, props, target);
            }

            return props;
        }

        /// <summary>
        /// Builds defender EntityProperties.
        ///
        /// Address: 0x1806e7fd0
        ///
        /// Similar to BuildPropertiesForUse but for defense context.
        /// Also calls OnModifyTargetProperties on defender's container.
        /// </summary>
        public EntityProperties BuildPropertiesForDefense(
            Actor attacker,
            Tile fromTile,
            Tile targetTile,
            Skill attackSkill)
        {
            if (Owner == null)
                return null;

            // Clone base properties
            var baseProps = Owner.GetEntityProperties();
            var props = baseProps.GetClone();

            // Apply skill modifiers (defense context)
            foreach (var baseSkill in Skills)
            {
                if (baseSkill.IsRemoved)  // +0x39
                    continue;

                if (!baseSkill.HasExecutingElements())
                    continue;

                baseSkill.ApplyToEntityPropertiesForDefense(attackSkill, attacker, fromTile, targetTile, props);
            }

            // Let target's container modify
            var targetContainer = Owner.GetSkillContainer();
            if (targetContainer != null)
            {
                targetContainer.OnModifyTargetProperties(props);
            }

            return props;
        }

        private void OnModifyTargetProperties(EntityProperties props)
        {
            // Apply final modifications
        }
    }

    // =========================================================================
    // SKILL TEMPLATE
    // =========================================================================

    /// <summary>
    /// Skill template (ScriptableObject).
    ///
    /// Key field offsets verified from binary:
    /// +0xA8: Tags (List of TagTemplate)
    /// +0xB8: HasLimitedUses (bool)
    /// +0xD0: ElementType (int)
    /// +0xE4: IsTargeted (bool)
    /// +0xF1: RequiresLOS (bool)
    /// +0xF2: HasCost (bool)
    /// +0xF3: AlwaysHits (bool)
    /// +0xF5: ShouldUseParentSkill (bool)
    /// +0x100: IgnoreCoverInside (bool)
    /// +0x110: CanAimWhileMoving (bool)
    /// +0x113: RequiresMobility (bool)
    /// +0x114: IgnoreActorState (bool)
    /// +0x115: AllowsDuringSpecialAction (bool)
    /// +0x11C: ShapeType (int)
    /// +0x120: ShapeMaxDistance (int)
    /// +0x124: UseWeaponRange (bool)
    /// +0x128: MinRange (int)
    /// +0x130: MaxRange (int)
    /// +0x150: IsHidden (bool)
    /// +0x154: UsesPerTurnBase (int)
    /// +0x178: AoEType (int, 0=single, others=AoE)
    /// +0x244: ElementDamageReduction (float)
    /// </summary>
    public class SkillTemplate
    {
        public string name;
        public List<TagTemplate> Tags;  // +0xA8
        public bool HasLimitedUses;  // +0xB8
        public int ElementType;  // +0xD0
        public bool IsTargeted;  // +0xE4
        public bool RequiresLOS;  // +0xF1
        public bool HasCost;  // +0xF2
        public bool AlwaysHits;  // +0xF3
        public bool ShouldUseParentSkill;  // +0xF5
        public bool IgnoreCoverInside;  // +0x100
        public bool CanAimWhileMoving;  // +0x110
        public bool RequiresMobility;  // +0x113
        public bool IgnoreActorState;  // +0x114
        public bool AllowsDuringSpecialAction;  // +0x115
        public int ShapeType;  // +0x11C
        public int ShapeMaxDistance;  // +0x120
        public bool UseWeaponRange;  // +0x124
        public int MinRange;  // +0x128
        public int MaxRange;  // +0x130
        public bool IsHidden;  // +0x150
        public int UsesPerTurnBase;  // +0x154
        public int AoEType;  // +0x178
        public float ElementDamageReduction;  // +0x244
        public int TargetingType;  // +0xD0 (different from ElementType)

        public bool IsDeploymentRequirementSatisfied(Actor actor) => true;
        public bool IsIgnoringCoverInsideForTarget(Entity target) => IgnoreCoverInside;
        public Tile GetBestTileToAttackFrom(Actor actor, Tile targetTile) => actor?.CurrentTile;
    }

    // =========================================================================
    // HANDLER INTERFACES
    // =========================================================================

    /// <summary>
    /// Base interface for skill handlers.
    /// Handlers are stored in Skill.Handlers (+0x48) and process skill behavior.
    /// </summary>
    public interface ISkillHandler
    {
        bool IsUsable();
        bool CanApply(Actor actor);
        bool OnVerifyTarget(Tile fromTile, Tile targetTile, TargetUsageParams usageParams);
        void TransformTargetTile(ref Tile targetTile);
    }

    /// <summary>
    /// Handler providing custom charge behavior.
    /// </summary>
    public interface ISkillChargeHandler
    {
        int GetCharges();
    }

    /// <summary>
    /// Handler contributing to expected damage calculation.
    /// </summary>
    public interface IExpectedDamageContributor
    {
        void OnCalculateExpectedDamage(
            Tile fromTile,
            Tile targetTile,
            Tile effectiveTile,
            Entity target,
            int usesPerTurn,
            ExpectedDamage result);
    }

    // =========================================================================
    // SUPPORTING TYPES
    // =========================================================================

    public struct TargetUsageParams
    {
        public bool CheckLimitedUses;
        public bool CheckDeployment;

        public static TargetUsageParams Default => new TargetUsageParams
        {
            CheckLimitedUses = true,
            CheckDeployment = true
        };
    }

    public class TagTemplate
    {
        public int Value;  // +0x8C
    }

    public class BaseSkill
    {
        public SkillTemplate Template;  // +0x10
        public bool IsRemoved;  // +0x39
        public object Source;  // +0x28

        public virtual string GetID() => Template?.name;
        public virtual bool HasExecutingElements() => true;
        public virtual void ApplyToEntityProperties(Skill skill, Tile from, Tile target, EntityProperties props, Entity targetEntity) { }
        public virtual void ApplyToEntityPropertiesForDefense(Skill skill, Actor attacker, Tile from, Tile target, EntityProperties props) { }

        public static Actor GetActor(BaseSkill skill) => null;
        public static Entity GetEntity(BaseSkill skill) => null;
    }

    public class EntityProperties
    {
        public int MinAccuracy;  // +0x78

        public float GetAccuracy() => 75f;
        public float GetEvasion() => 0f;
        public float GetEvasionValue() => 0f;  // +0x88
        public float GetAccuracyDropoff() => 0f;
        public float GetDamage() => 0f;
        public float GetDamageDropoff() => 0f;
        public float GetArmorPenetration() => 0f;
        public float GetArmorPenetrationDropoff() => 0f;
        public int GetArmor(int direction = 0) => 0;
        public float GetDamageToArmorDurability() => 0f;
        public float GetDamageToArmorDurabilityDropoff() => 0f;

        public float DamageDistanceDropoff;  // +0x128
        public float ArmorPenDropoff;  // +0x110
        public float DurabilityDropoff;  // +0x13C
        public float DamageMultiplier;  // +0x8C
        public float FinalDamageMult;  // +0x140
        public float ArmorDamageScale;  // +0x144
        public float MinArmorDamage;  // +0x148
        public float HPDamageScale;  // +0x14C
        public float MinHPDamage;  // +0x150

        public EntityProperties GetClone() => new EntityProperties();
    }

    public class Entity
    {
        public int Armor;
        public int HP;
        public int ArmorDurability;
        public List<object> Elements;
        public Entity ContainedBy;
        public bool HasStylesheetPaths;
        public Vector3 Position;

        public Tile GetCurrentTile() => null;
        public SkillContainer GetSkillContainer() => null;
        public ActorConfig GetActorConfig() => null;
        public EntityProperties GetEntityProperties() => null;
        public float GetArmorDurabilityPct() => 1f;
        public bool IsContainableWithin(Entity other) => false;
        public void OnBeingTargetedByAttack(Skill skill, Actor attacker) { }
    }

    public class Actor : Entity
    {
        public Tile CurrentTile;
        public bool IsMobile;
        public static bool ConsumeChargesOnUse = true;

        public bool CanAct() => true;
        public int GetActionPoints() => 4;
        public void SetActionPoints(int ap) { }
        public int GetActionState() => 0;
        public void SetAiming(bool aiming, Skill skill) { }
        public void UpdateActorState() { }
        public object GetElement() => null;
        public object GetItemContainer() => null;
        public CostModifier GetCostModifier() => null;
    }

    public class CostModifier
    {
        public int FlatBonus;  // +0x44
        public float Multiplier;  // +0x48
    }

    public class ActorConfig
    {
        public CoverTemplate CoverTemplate;  // +0xE0
    }

    public class CoverTemplate
    {
        public float CoverValue;  // +0x58
        public float DamageReduction;
    }

    public class Tile
    {
        public bool IsEmpty() => true;
        public Entity GetEntity() => null;
        public int GetDistanceTo(Tile other) => 0;
        public Vector3 GetPos() => default;
        public Direction GetDirectionTo(Tile other) => Direction.North;
        public bool HasLineOfSightTo(Tile other) => true;
        public int GetCover(Direction dir, Entity target, EntityProperties props, bool isSelf) => 0;
        public bool IsVisibleToPlayer() => true;
    }

    public class Item
    {
        public ItemTemplate GetTemplate() => null;
    }

    public class ItemTemplate
    {
        public int ItemType;
    }

    public class WeaponTemplate : ItemTemplate
    {
        public int MinRange;  // +0x140
        public int MaxRange;  // +0x148
    }

    public class ItemContainer
    {
        public Item GetItemAtSlot(int slot) => null;
    }

    public class SkillSource { }

    public enum Direction { North, East, South, West }

    public struct Vector3
    {
        public float x, y, z;
        public static float Distance(Vector3 a, Vector3 b) => 0f;
    }

    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public static class Config
    {
        public static Config Instance = new Config();
        public static float MaxHitChance = 100f;  // DAT_182d8fd78
        public static float ArmorScaling = 0.01f;  // DAT_182d8fc3c
        public static float ArmorPenMult = 0.01f;  // DAT_182d8fbd8
        public static float ArmorMult = 0.01f;
        public static float MinDamagePercent = 0.05f;  // DAT_182d8fb98

        public float[] CoverDamageMultipliers = new float[] { 1f, 0.75f, 0.5f, 0.25f };  // +0x88, +0x90
    }

    public static class TacticalManager
    {
        public static TacticalManager Instance = new TacticalManager();
        public Actor CurrentTurnActor;

        public void InvokeOnSkillUse(Actor actor, Skill skill, Tile targetTile) { }
        public void AddBusySkill(Skill skill) { }
    }

    public static class Schedule
    {
        public static void Delay(float seconds, Action callback) { }
    }
}
