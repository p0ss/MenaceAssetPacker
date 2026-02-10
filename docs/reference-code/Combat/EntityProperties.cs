// =============================================================================
// MENACE REFERENCE CODE - EntityProperties
// =============================================================================
// This is a reconstructed reference implementation based on reverse engineering.
// It is NOT the original game code, but accurately represents the game's behavior.
//
// Purpose: Help modders understand the game's internals for creating mods.
// License: For educational and modding purposes only.
//
// Based on: Ghidra analysis of GameAssembly.dll
// Game Version: Current Steam build (2025)
// =============================================================================

namespace Menace.Tactical
{
    /// <summary>
    /// Central container for all combat-related stats for an entity.
    /// Used for both attack and defense calculations.
    ///
    /// Instance size: ~0x1D8 bytes
    /// </summary>
    public class EntityProperties
    {
        // =====================================================================
        // ACCURACY STATS (Offsets 0x68-0x7C)
        // =====================================================================

        /// <summary>Base accuracy percentage before multipliers. Offset: +0x68</summary>
        public float BaseAccuracy;

        /// <summary>Accuracy multiplier (1.0 = 100%). Uses AddMult stacking. Offset: +0x6C</summary>
        public float AccuracyMult = 1.0f;

        /// <summary>Base per-tile accuracy penalty. Offset: +0x70</summary>
        public float AccuracyDropoffBase;

        /// <summary>Accuracy dropoff multiplier. Offset: +0x74</summary>
        public float AccuracyDropoffMult = 1.0f;

        /// <summary>Minimum hit chance floor (can't go below this). Offset: +0x78</summary>
        public int MinHitChance;

        // =====================================================================
        // DAMAGE STATS (Offsets 0x7C-0x98)
        // =====================================================================

        /// <summary>Base damage value. Offset: +0x7C</summary>
        public float BaseDamage;

        /// <summary>Damage multiplier (1.0 = 100%). Offset: +0x80</summary>
        public float DamageMult = 1.0f;

        /// <summary>Armor penetration value. Offset: +0x84</summary>
        public float ArmorPenetration;

        /// <summary>Armor penetration multiplier. Offset: +0x88</summary>
        public float ArmorPenetrationMult = 1.0f;

        // =====================================================================
        // DEFENSE STATS (Offsets 0x40-0x60)
        // =====================================================================

        /// <summary>Dodge multiplier (higher = harder to hit). Offset: +0x44</summary>
        public float DodgeMult = 1.0f;

        /// <summary>Armor value for damage reduction. Offset: +0x48</summary>
        public float Armor;

        /// <summary>Armor multiplier. Offset: +0x4C</summary>
        public float ArmorMult = 1.0f;

        /// <summary>Cover usage effectiveness. Offset: +0x88 area</summary>
        public float CoverUsage = 1.0f;

        // =====================================================================
        // METHODS
        // =====================================================================

        /// <summary>
        /// Gets effective accuracy after applying multiplier.
        /// Address: 0x18060b9f0
        /// </summary>
        public float GetAccuracy()
        {
            float clampedMult = FloatExtensions.Clamped(AccuracyMult);
            return (float)Math.Floor(BaseAccuracy * clampedMult);
        }

        /// <summary>
        /// Gets effective accuracy dropoff per tile.
        /// Address: 0x18060b9c0
        /// </summary>
        public float GetAccuracyDropoff()
        {
            float clampedMult = FloatExtensions.Clamped(AccuracyDropoffMult);
            return (float)Math.Floor(AccuracyDropoffBase * clampedMult);
        }

        /// <summary>
        /// Gets effective damage after applying multiplier.
        /// Address: 0x18060ba20
        /// </summary>
        public float GetDamage()
        {
            float clampedMult = FloatExtensions.Clamped(DamageMult);
            return (float)Math.Floor(BaseDamage * clampedMult);
        }

        /// <summary>
        /// Gets effective armor after applying multiplier.
        /// Address: 0x18060b990
        /// </summary>
        public float GetArmor()
        {
            float clampedMult = FloatExtensions.Clamped(ArmorMult);
            return (float)Math.Floor(Armor * clampedMult);
        }

        /// <summary>
        /// Gets effective armor penetration after applying multiplier.
        /// Address: 0x18060b960
        /// </summary>
        public float GetArmorPenetration()
        {
            float clampedMult = FloatExtensions.Clamped(ArmorPenetrationMult);
            return (float)Math.Floor(ArmorPenetration * clampedMult);
        }
    }
}
