// =============================================================================
// MENACE REFERENCE CODE - Damage Calculation
// =============================================================================
// Reconstructed damage system showing how weapon damage, armor, and
// armor penetration interact to determine final damage.
//
// VERIFIED against binary via Ghidra - 2024
// =============================================================================

using Menace.Tools;

namespace Menace.Tactical
{
    /// <summary>
    /// Damage packet passed to targets when they receive damage.
    /// Created by DamageHandler and consumed by target's OnDamageReceived.
    ///
    /// Instance size: ~0x50 bytes
    /// </summary>
    public class DamageInfo
    {
        /// <summary>Fatality/death animation type (0=normal, higher=special). Offset: +0x18</summary>
        public int FatalityType;

        /// <summary>Minimum element index to start hitting. Offset: +0x1C</summary>
        public int ElementHitMinIndex;

        /// <summary>Final damage value after all calculations. Offset: +0x2C</summary>
        public int Damage;

        /// <summary>Flat armor durability damage. Offset: +0x34</summary>
        public int ArmorDmgFlat;

        /// <summary>Percentage of current armor as durability damage. Offset: +0x38</summary>
        public int ArmorDmgPct;

        /// <summary>Number of shots/hits in this attack. Offset: +0x3C</summary>
        public int TotalShots;

        /// <summary>Armor penetration value. Offset: +0x40</summary>
        public float ArmorPenetration;

        /// <summary>Armor damage scaled by elements hit. Offset: +0x44</summary>
        public float ArmorDmgFromElements;

        /// <summary>Whether armor blocked any penetration rolls. Offset: +0x4A</summary>
        public bool ArmorBlockedPenetration;

        /// <summary>Whether any hit penetrated armor. Offset: +0x4B</summary>
        public bool HitPenetrated;

        /// <summary>Whether all elements are dead after this damage. Offset: +0x4C</summary>
        public bool AllElementsDead;

        /// <summary>Whether damage was absorbed by armor. Offset: +0x4D</summary>
        public bool AbsorbedByArmor;

        /// <summary>Whether this attack can critically strike. Offset: +0x4E</summary>
        public bool CanCrit;
    }

    /// <summary>
    /// Extended EntityProperties with damage-related fields.
    /// See EntityProperties.cs for accuracy-related fields.
    /// </summary>
    public partial class EntityProperties
    {
        // =====================================================================
        // DAMAGE STATS (Offsets 0x118-0x138)
        // =====================================================================

        /// <summary>Base damage value. Offset: +0x118</summary>
        public float BaseDamage;

        /// <summary>Damage multiplier (1.0 = 100%). Offset: +0x11C</summary>
        public float DamageMult = 1.0f;

        /// <summary>Damage reduction per tile from ideal range. Offset: +0x120</summary>
        public float DamageDropoffBase;

        /// <summary>Damage dropoff multiplier. Offset: +0x124</summary>
        public float DamageDropoffMult = 1.0f;

        // =====================================================================
        // ARMOR PENETRATION (Offsets 0x100-0x10C)
        // =====================================================================

        /// <summary>Base armor penetration. Offset: +0x100</summary>
        public float ArmorPenBase;

        /// <summary>Armor penetration multiplier. Offset: +0x104</summary>
        public float ArmorPenMult = 1.0f;

        /// <summary>AP reduction per tile. Offset: +0x108</summary>
        public float ArmorPenDropoff;

        // =====================================================================
        // ARMOR DURABILITY DAMAGE (Offsets 0x12C-0x138)
        // =====================================================================

        /// <summary>Anti-armor damage base. Offset: +0x12C</summary>
        public float ArmorDurDmgBase;

        /// <summary>Anti-armor damage multiplier. Offset: +0x130</summary>
        public float ArmorDurDmgMult = 1.0f;

        /// <summary>Anti-armor damage dropoff base. Offset: +0x134</summary>
        public float ArmorDurDmgDropoff;

        /// <summary>Anti-armor damage dropoff multiplier. Offset: +0x138</summary>
        public float ArmorDurDmgDropoffMult = 1.0f;

        // =====================================================================
        // ARMOR (Offsets 0x1C-0x30)
        // =====================================================================

        /// <summary>Base armor value. Offset: +0x1C</summary>
        public int ArmorBase;

        /// <summary>Frontal armor. Offset: +0x20</summary>
        public int ArmorFront;

        /// <summary>Side armor. Offset: +0x24</summary>
        public int ArmorSide;

        /// <summary>Armor multiplier. Offset: +0x28</summary>
        public float ArmorMult = 1.0f;

        /// <summary>Current armor durability. Offset: +0x2C</summary>
        public float ArmorDurability;

        // =====================================================================
        // DAMAGE METHODS
        // =====================================================================

        /// <summary>
        /// Gets effective damage after applying multiplier.
        /// Address: 0x18060bd60
        /// </summary>
        public float GetDamage()
        {
            float clampedMult = FloatExtensions.Clamped(DamageMult);
            return BaseDamage * clampedMult;
        }

        /// <summary>
        /// Gets damage dropoff per tile from ideal range.
        /// Address: 0x18060bcd0
        /// </summary>
        public float GetDamageDropoff()
        {
            float clampedMult = FloatExtensions.Clamped(DamageDropoffMult);
            return DamageDropoffBase * clampedMult;
        }

        /// <summary>
        /// Gets effective armor (max of all zones).
        /// Address: 0x18060bb00
        ///
        /// Note: Actual comparison order is Front -> Side -> Base
        /// </summary>
        public int GetArmor()
        {
            // Actual order from decompiled code: start with Front, compare to Side, then Base
            int armor = ArmorFront;
            if (ArmorSide > armor) armor = ArmorSide;
            if (ArmorBase > armor) armor = ArmorBase;

            float clampedMult = FloatExtensions.Clamped(ArmorMult);
            return (int)(armor * clampedMult);
        }

        /// <summary>
        /// Gets armor for a specific facing direction.
        /// Address: 0x18060bae0
        /// </summary>
        /// <param name="direction">0=Base, 1=Front, 2=Side</param>
        public int GetArmorValue(int direction)
        {
            return direction switch
            {
                1 => ArmorFront,
                2 => ArmorSide,
                _ => ArmorBase
            };
        }

        /// <summary>
        /// Gets effective armor penetration.
        /// Address: 0x18060bab0
        /// </summary>
        public float GetArmorPenetration()
        {
            float clampedMult = FloatExtensions.Clamped(ArmorPenMult);
            return ArmorPenBase * clampedMult;
        }

        /// <summary>
        /// Gets anti-armor (armor durability) damage.
        /// Address: 0x18060bd30
        /// </summary>
        public float GetDamageToArmorDurability()
        {
            float clampedMult = FloatExtensions.Clamped(ArmorDurDmgMult);
            return ArmorDurDmgBase * clampedMult;
        }
    }
}

namespace Menace.Tactical.Skills.Effects
{
    /// <summary>
    /// Effect data fields for the Damage effect (at handler+0x18).
    /// These control how damage is calculated and applied.
    /// </summary>
    public class DamageEffectData
    {
        /// <summary>If true, damage targets passenger inside container, not container itself. Offset: +0x58</summary>
        public bool IsAppliedOnlyToPassengers;

        /// <summary>Base flat number added to hit count calculation. Offset: +0x5C</summary>
        public int FlatDamageBase;

        /// <summary>Fraction 0.0-1.0 of target's elements to hit. Formula: ceil(elementCount * pct). Offset: +0x60</summary>
        public float ElementsHitPercentage;

        /// <summary>Flat HP damage added to damage total. Offset: +0x64</summary>
        public float DamageFlatAmount;

        /// <summary>Percentage of target's CURRENT HP as damage (0.0-1.0 scale). Offset: +0x68</summary>
        public float DamagePctCurrentHitpoints;

        /// <summary>Minimum floor for current HP% damage. Offset: +0x6C</summary>
        public float DamagePctCurrentHitpointsMin;

        /// <summary>Percentage of target's MAX HP as damage (0.0-1.0 scale). Offset: +0x70</summary>
        public float DamagePctMaxHitpoints;

        /// <summary>Minimum floor for max HP% damage. Offset: +0x74</summary>
        public float DamagePctMaxHitpointsMin;

        /// <summary>Flat armor durability damage. Offset: +0x78</summary>
        public float DamageToArmor;

        /// <summary>Percentage of current armor durability to damage (0.0-1.0). Offset: +0x7C</summary>
        public float ArmorDmgPctCurrent;

        /// <summary>Death animation type enum (0=normal, higher=special). Offset: +0x80</summary>
        public int FatalityType;

        /// <summary>Armor penetration value - reduces effective armor. Offset: +0x84</summary>
        public float ArmorPenetration;

        /// <summary>Armor damage multiplied by number of elements hit. Offset: +0x88</summary>
        public float ArmorDmgFromElementCount;

        /// <summary>First element index to start hitting (skip first N elements). Offset: +0x8C</summary>
        public int ElementHitMinIndex;

        /// <summary>Whether damage can critically strike. Offset: +0x90</summary>
        public bool CanCrit;
    }

    /// <summary>
    /// Handles damage application from skills to entities.
    /// </summary>
    public class DamageHandler : SkillEventHandler
    {
        /// <summary>
        /// Applies damage from this skill to the target entity.
        ///
        /// Address: 0x180702970
        ///
        /// DAMAGE FORMULA (HP percentage-based, NOT distance-based):
        ///   hitCount = FlatDamageBase + ceil(elementCount * ElementsHitPercentage)
        ///   hitCount = max(1, hitCount)
        ///
        ///   currentHpDmg = max(currentHP * DamagePctCurrentHitpoints, DamagePctCurrentHitpointsMin)
        ///   maxHpDmg = max(maxHP * DamagePctMaxHitpoints, DamagePctMaxHitpointsMin)
        ///   totalDamage = currentHpDmg + DamageFlatAmount + maxHpDmg
        ///
        /// Flow:
        /// 1. Get effect data from handler+0x18
        /// 2. Calculate HP percentage damage with minimums
        /// 3. Create DamageInfo packet with all fields
        /// 4. Apply to target or passenger via OnDamageReceived
        /// </summary>
        public void ApplyDamage()
        {
            Entity target = this.GetEntity();
            if (target == null) return;

            DamageEffectData effectData = this.EffectData;  // handler+0x18

            // Get target HP values
            int currentHP = target.CurrentHitpoints;  // entity+0x54
            int maxHP = target.MaxHitpoints;          // entity[0xB] (via element list)
            int elementCount = target.ElementCount;   // entity[4]+0x18

            // Calculate current HP percentage damage
            float currentHpDmg = currentHP * effectData.DamagePctCurrentHitpoints;  // +0x68
            float minCurrentDmg = effectData.DamagePctCurrentHitpointsMin;          // +0x6C
            if (minCurrentDmg > currentHpDmg) currentHpDmg = minCurrentDmg;

            // Calculate max HP percentage damage
            float maxHpDmg = maxHP * effectData.DamagePctMaxHitpoints;  // +0x70
            float minMaxDmg = effectData.DamagePctMaxHitpointsMin;      // +0x74
            if (minMaxDmg > maxHpDmg) maxHpDmg = minMaxDmg;

            // Create DamageInfo
            var dmgInfo = new DamageInfo();

            // Calculate hit count: flatBase + ceil(elements * percentage)
            int flatBase = effectData.FlatDamageBase;         // +0x5C
            float elemPct = effectData.ElementsHitPercentage; // +0x60
            int hitCount = flatBase + (int)Math.Ceiling(elementCount * elemPct);
            hitCount = Math.Max(1, hitCount);
            dmgInfo.TotalShots = hitCount;  // DamageInfo+0x3C

            // Calculate total damage: currentHpDmg + flat + maxHpDmg
            float flatAmount = effectData.DamageFlatAmount;  // +0x64
            dmgInfo.Damage = (int)(currentHpDmg + flatAmount + maxHpDmg);  // DamageInfo+0x2C

            // Set armor-related values
            dmgInfo.ArmorDmgFlat = (int)effectData.DamageToArmor;               // +0x34 from +0x78
            dmgInfo.ArmorDmgPct = (int)effectData.ArmorDmgPctCurrent;           // +0x38 from +0x7C
            dmgInfo.FatalityType = effectData.FatalityType;                     // +0x18 from +0x80
            dmgInfo.ArmorPenetration = effectData.ArmorPenetration;             // +0x40 from +0x84
            dmgInfo.ArmorDmgFromElements = effectData.ArmorDmgFromElementCount; // +0x44 from +0x88
            dmgInfo.ElementHitMinIndex = effectData.ElementHitMinIndex;         // +0x1C from +0x8C
            dmgInfo.CanCrit = effectData.CanCrit;                               // +0x4E from +0x90

            // Apply to target or passenger based on IsAppliedOnlyToPassengers flag
            if (effectData.IsAppliedOnlyToPassengers)  // +0x58
            {
                Actor actor = this.GetActor();
                if (actor?.HasDefaultAttribute == true)
                {
                    Entity passenger = target.PassengerContainer;  // entity[0xD]
                    if (passenger != null)
                    {
                        passenger.OnDamageReceived(this.Skill, dmgInfo);
                        return;
                    }
                }
            }

            // Normal damage application
            target.OnDamageReceived(this.Skill, dmgInfo);

            // Report to combat log
            int healthLost = target.PreviousElementCount - target.ElementCount;
            DevCombatLog.ReportHit(this.GetActor(), this.Skill, healthLost, target, dmgInfo);
        }
    }
}

namespace Menace.Tactical
{
    /// <summary>
    /// How damage is resolved when an entity receives it.
    /// Address: 0x180613030
    ///
    /// This is a complex function (~700 lines decompiled) that handles:
    /// - Armor penetration chance rolls per hit
    /// - Per-element damage distribution
    /// - Armor durability reduction
    /// - Counterattack skill triggers
    /// - Death event invocation
    ///
    /// Simplified armor penetration formula:
    ///   effectiveArmor = (armor * armorDurabilityRatio) - ArmorPenetration
    ///   penetrationChance = 100 - (effectiveArmor * 3)
    ///   penetrationChance = max(0, penetrationChance)
    ///
    /// For each hit in TotalShots:
    ///   - Roll penetration check vs penetrationChance
    ///   - If penetrated: Apply damage to random element
    ///   - If blocked: Apply reduced armor durability damage
    /// </summary>
    public partial class Entity
    {
        /// <summary>
        /// Receives damage and applies armor reduction.
        /// Address: 0x180613030
        /// </summary>
        public virtual void OnDamageReceived(Skill source, DamageInfo damageInfo)
        {
            if (!this.IsAlive) return;  // entity[9] != 0
            if (this.ElementCount == 0) return;  // entity[4]+0x18

            // Get defense properties
            EntityProperties defProps = this.DefenseProperties;

            // Get armor value based on hit direction (damageInfo+0x30)
            int direction = damageInfo.HitDirection;
            int armor;
            switch (direction)
            {
                case 1: armor = defProps.ArmorFront; break;
                case 2: armor = defProps.ArmorSide; break;
                case 3: armor = Math.Max(defProps.ArmorFront, Math.Max(defProps.ArmorSide, defProps.ArmorBase)); break;
                default: armor = defProps.ArmorBase; break;
            }

            float armorMult = FloatExtensions.Clamped(defProps.ArmorMult);
            int effectiveArmorBase = (int)(armor * armorMult);

            // Calculate armor durability ratio
            float maxArmorDurability = (float)this.MaxArmorDurability;  // entity[0xC]
            if (maxArmorDurability < 1.0f) maxArmorDurability = 1.0f;
            float durabilityRatio = (float)this.CurrentArmorDurability / maxArmorDurability;  // entity+0x5C

            // Calculate effective armor after durability and penetration
            float scaledArmor = effectiveArmorBase * durabilityRatio;
            float effectiveArmor = scaledArmor - damageInfo.ArmorPenetration;
            if (effectiveArmor < 0) effectiveArmor = 0;

            // Calculate penetration chance: 100 - (effectiveArmor * 3)
            int penetrationChance = (int)(100.0f - effectiveArmor * 3.0f);
            if (penetrationChance < 0) penetrationChance = 0;

            damageInfo.ArmorBlockedPenetration = penetrationChance > 0;

            // Process each hit
            int hitCount = Math.Min(damageInfo.TotalShots, this.ElementCount);
            int totalDamageDealt = 0;
            int armorDurabilityBefore = this.CurrentArmorDurability;

            for (int i = 0; i < hitCount; i++)
            {
                int roll = PseudoRandom.NextPercent();

                if (roll >= penetrationChance)
                {
                    // Armor blocked - apply reduced armor durability damage
                    // Formula involves durability ratio squared and ArmorDmgPct
                }
                else
                {
                    // Penetration successful - apply damage to random element
                    Element targetElement = this.GetRandomElement();
                    int elementDamage = damageInfo.Damage;  // Can be modified by crit

                    int actualDamage = Math.Min(elementDamage, targetElement.Hitpoints);
                    totalDamageDealt += actualDamage;
                    targetElement.SetHitpoints(targetElement.Hitpoints - actualDamage);
                    damageInfo.HitPenetrated = true;

                    // Apply armor durability damage on penetration
                    // Uses ArmorDmgPct * durabilityRatio^2 formula
                }
            }

            // Update final damage dealt in DamageInfo
            damageInfo.Damage = totalDamageDealt;
            damageInfo.ArmorDmgFlat = armorDurabilityBefore - this.CurrentArmorDurability;

            // Check for counterattack skill at entity+0x6D
            // ... (complex counterattack logic)

            // Invoke OnDamageReceived event on TacticalManager
            TacticalManager.Instance.InvokeOnDamageReceived(this, source, damageInfo);

            // Check if all elements dead
            if (this.ElementCount == 0)
            {
                damageInfo.AllElementsDead = true;
                TacticalManager.Instance.InvokeOnDeath(this, source);
            }
            else
            {
                this.UpdateHitpoints();
                foreach (var element in this.Elements)
                {
                    element.UpdateDamageShader();
                }
            }
        }
    }
}
