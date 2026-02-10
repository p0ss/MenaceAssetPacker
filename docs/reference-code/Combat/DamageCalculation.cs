// =============================================================================
// MENACE REFERENCE CODE - Damage Calculation
// =============================================================================
// Reconstructed damage system showing how weapon damage, armor, and
// armor penetration interact to determine final damage.
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
        /// <summary>Final damage value after all calculations. Offset: +0x2C</summary>
        public int Damage;

        /// <summary>Armor penetration value. Offset: +0x34</summary>
        public int ArmorPenetration;

        /// <summary>Damage applied to armor durability (anti-armor). Offset: +0x38</summary>
        public int ArmorDurabilityDamage;

        /// <summary>Number of shots/hits in this attack. Offset: +0x3C</summary>
        public int TotalShots;

        /// <summary>Whether this attack can cause dismemberment. Offset: +0x4E</summary>
        public bool CanDismember;

        /// <summary>Whether damage was absorbed by armor. Offset: +0x4D</summary>
        public bool AbsorbedByArmor;

        // Additional fields at +0x18, +0x40, +0x44, +0x1C
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
        /// </summary>
        public int GetArmor()
        {
            // Get maximum armor from all zones
            int armor = ArmorBase;
            if (ArmorFront > armor) armor = ArmorFront;
            if (ArmorSide > armor) armor = ArmorSide;

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
    /// Handles damage application from skills to entities.
    /// </summary>
    public class DamageHandler : SkillEventHandler
    {
        /// <summary>
        /// Applies damage from this skill to the target entity.
        ///
        /// Address: 0x180702970
        ///
        /// Flow:
        /// 1. Get attack properties from skill
        /// 2. Calculate damage with range modifiers
        /// 3. Create DamageInfo packet
        /// 4. Apply to target via OnDamageReceived
        /// </summary>
        public void ApplyDamage()
        {
            Entity target = this.GetEntity();
            if (target == null) return;

            EntityProperties attackProps = this.AttackProperties;  // offset +0x18

            // Calculate base damage with distance modifier
            int distance = target.DistanceToAttacker;  // offset +0x54
            float baseDamage = distance * attackProps.BaseDamage;  // +0x68

            float minDamage = attackProps.MinDamage;  // +0x6C
            float damage = Math.Max(minDamage, baseDamage);

            // Calculate armor durability damage
            int elementCount = target.ElementCount;  // +0x58
            float armorDmg = elementCount * attackProps.ArmorDurDmgBase;  // +0x70
            float minArmorDmg = attackProps.MinArmorDmg;  // +0x74
            armorDmg = Math.Max(minArmorDmg, armorDmg);

            // Create DamageInfo
            var dmgInfo = new DamageInfo();

            // Calculate total shots
            int baseShots = attackProps.ShotCount;  // +0x5C
            float shotsPerElement = attackProps.ShotsPerElement;  // +0x60
            int totalShots = (int)Math.Ceiling(elementCount * shotsPerElement) + baseShots;
            totalShots = Math.Max(1, totalShots);

            dmgInfo.TotalShots = totalShots;

            // Set damage values
            float damageBonus = attackProps.DamageBonus;  // +0x64 (100 decimal)
            dmgInfo.Damage = (int)(damage + damageBonus + armorDmg);

            dmgInfo.ArmorPenetration = (int)attackProps.ArmorPenBase;  // +0x78
            dmgInfo.ArmorDurabilityDamage = (int)attackProps.ArmorDurDmg;  // +0x7C

            // Copy additional properties
            dmgInfo.CanDismember = attackProps.CanDismember;  // +0x90

            // Determine if damage goes to parent container (e.g., vehicle)
            bool damageParent = attackProps.DamageParent;  // +0x58

            // Apply damage to appropriate target
            if (damageParent && this.GetActor()?.HasDefaultAttribute == true)
            {
                target.ParentContainer?.OnDamageReceived(this.Skill, dmgInfo);
            }
            else
            {
                target.OnDamageReceived(this.Skill, dmgInfo);
            }

            // Report to combat log
            int healthLost = target.PreviousHealth - target.CurrentHealth;
            DevCombatLog.ReportHit(this.GetActor(), this.Skill, healthLost, target, dmgInfo);
        }
    }
}

namespace Menace.Tactical
{
    /// <summary>
    /// How damage is resolved when an entity receives it.
    ///
    /// Formula:
    ///   EffectiveArmor = Armor - ArmorPenetration
    ///   if (EffectiveArmor > 0) {
    ///       FinalDamage = Damage - EffectiveArmor
    ///   } else {
    ///       FinalDamage = Damage
    ///   }
    ///   FinalDamage = max(0, FinalDamage)
    /// </summary>
    public partial class Entity
    {
        /// <summary>
        /// Receives damage and applies armor reduction.
        /// </summary>
        public virtual void OnDamageReceived(Skill source, DamageInfo damageInfo)
        {
            // Get defense properties
            EntityProperties defProps = this.DefenseProperties;

            // Calculate effective armor after penetration
            int armor = defProps.GetArmor();
            int effectiveArmor = armor - damageInfo.ArmorPenetration;

            int finalDamage;
            if (effectiveArmor > 0)
            {
                // Armor reduces damage
                finalDamage = damageInfo.Damage - effectiveArmor;
                damageInfo.AbsorbedByArmor = true;
            }
            else
            {
                // Armor penetration exceeds armor
                finalDamage = damageInfo.Damage;
            }

            // Minimum 0 damage
            finalDamage = Math.Max(0, finalDamage);

            // Apply armor durability damage
            if (damageInfo.ArmorDurabilityDamage > 0)
            {
                defProps.ArmorDurability -= damageInfo.ArmorDurabilityDamage;
                if (defProps.ArmorDurability < 0)
                {
                    defProps.ArmorDurability = 0;
                }
            }

            // Apply final damage to health
            this.CurrentHealth -= finalDamage;
        }
    }
}
