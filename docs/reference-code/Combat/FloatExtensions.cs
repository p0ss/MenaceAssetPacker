// =============================================================================
// MENACE REFERENCE CODE - FloatExtensions
// =============================================================================
// Reconstructed utility class for multiplier math used throughout combat system.
// =============================================================================

namespace Menace.Tools
{
    /// <summary>
    /// Extension methods for float math operations.
    /// These methods implement Menace's unique multiplier stacking system.
    /// </summary>
    public static class FloatExtensions
    {
        /// <summary>
        /// Adds a multiplier to an accumulator using ADDITIVE stacking.
        /// This is the core of Menace's stat system - multipliers don't compound!
        ///
        /// Example: 1.0.AddMult(1.2).AddMult(1.3) = 1.5, NOT 1.56
        ///
        /// Address: 0x1805320a0
        /// </summary>
        /// <param name="accumulator">Current multiplier accumulator</param>
        /// <param name="mult">Multiplier to add (1.0 = neutral)</param>
        /// <returns>New accumulator value</returns>
        public static float AddMult(this float accumulator, float mult)
        {
            // Key insight: multipliers stack ADDITIVELY
            // +20% and +30% = +50%, not +56%
            return accumulator + (mult - 1.0f);
        }

        /// <summary>
        /// Clamps a value to minimum 0.
        /// Used to prevent negative multipliers from inverting effects.
        ///
        /// Address: 0x1805320c0
        /// </summary>
        public static float Clamped(float value)
        {
            return Math.Max(0.0f, value);
        }

        /// <summary>
        /// Inverts a multiplier around 1.0.
        /// Used to convert dodge (attacker perspective) to hit chance modifier.
        ///
        /// Examples:
        ///   0.8 (20% less) -> 1.2 (20% more)
        ///   1.3 (30% more) -> 0.7 (30% less)
        ///
        /// Address: 0x1805320d0
        /// </summary>
        public static float Flipped(float value)
        {
            // Mathematically: 2.0 - value, or equivalently: 1.0 - (value - 1.0)
            return 2.0f - value;
        }
    }
}
