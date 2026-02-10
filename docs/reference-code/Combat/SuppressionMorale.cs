// =============================================================================
// MENACE REFERENCE CODE - Suppression & Morale System
// =============================================================================
// Reconstructed psychological warfare system showing how suppression and
// morale affect unit behavior and effectiveness.
// =============================================================================

using Menace.Tools;

namespace Menace.Tactical
{
    /// <summary>
    /// Suppression states that affect unit capabilities.
    /// </summary>
    public enum SuppressionState
    {
        /// <summary>Normal state, full capabilities</summary>
        None = 0,

        /// <summary>Taking cover, reduced effectiveness</summary>
        Suppressed = 1,

        /// <summary>Severely impaired, cannot act normally</summary>
        PinnedDown = 2
    }

    /// <summary>
    /// Morale states that affect unit behavior.
    /// </summary>
    public enum MoraleState
    {
        /// <summary>Routing, attempting to flee combat</summary>
        Fleeing = 1,

        /// <summary>Shaky, may break under pressure</summary>
        Wavering = 2,

        /// <summary>Normal morale, full combat effectiveness</summary>
        Neutral = 3
    }

    /// <summary>
    /// Configuration thresholds for suppression and morale.
    /// Values from TacticalConfig.
    /// </summary>
    public static class SuppressionConfig
    {
        /// <summary>Suppression percentage to become Suppressed. DAT_182d8fe40</summary>
        public const float SuppressedThreshold = 0.5f;  // 50%

        /// <summary>Suppression percentage to become PinnedDown. DAT_182d8fe48</summary>
        public const float PinnedDownThreshold = 0.8f;  // 80%

        /// <summary>Morale percentage threshold for Wavering. DAT_182d8fe40</summary>
        public const float WaveringThreshold = 0.5f;  // 50%

        /// <summary>Maximum suppression value. DAT_182d8fd78</summary>
        public const float MaxSuppression = 100f;

        /// <summary>Suppression to percentage conversion. DAT_182d8fbd8</summary>
        public const float SuppressionToPercentMult = 0.01f;

        /// <summary>Discipline reduction per point. DAT_182d8fbd8</summary>
        public const float DisciplineReductionPerPoint = 0.01f;
    }

    /// <summary>
    /// Actor class - represents units with suppression and morale.
    /// Extends Entity with psychological state management.
    /// </summary>
    public partial class Actor : Entity
    {
        // =====================================================================
        // SUPPRESSION FIELDS (Offset 0x15C)
        // =====================================================================

        /// <summary>Current suppression value (0-100). Offset: +0x15C</summary>
        private float m_Suppression;

        /// <summary>Current morale value. Offset: +0x160</summary>
        private float m_Morale;

        /// <summary>Last morale state for change detection. Offset: +0xD4</summary>
        private MoraleState m_LastMoraleState;

        /// <summary>Unit's HUD reference. Offset: +0xE0</summary>
        private UnitHUD m_HUD;

        // =====================================================================
        // SUPPRESSION METHODS
        // =====================================================================

        /// <summary>
        /// Gets raw suppression value.
        /// Address: 0x5DF7B0
        /// </summary>
        public float GetSuppression()
        {
            return m_Suppression;
        }

        /// <summary>
        /// Gets suppression as percentage (0.0 - 1.0).
        /// Address: 0x5DF710
        /// </summary>
        public float GetSuppressionPct()
        {
            return m_Suppression * SuppressionConfig.SuppressionToPercentMult;
        }

        /// <summary>
        /// Gets current suppression state based on thresholds.
        ///
        /// Address: 0x1805df730
        /// </summary>
        /// <param name="additionalPct">Additional suppression to consider (for preview)</param>
        public SuppressionState GetSuppressionState(float additionalPct = 0f)
        {
            float suppressionPct = GetSuppressionPct() + additionalPct;

            if (suppressionPct >= SuppressionConfig.PinnedDownThreshold)
                return SuppressionState.PinnedDown;

            if (suppressionPct >= SuppressionConfig.SuppressedThreshold)
                return SuppressionState.Suppressed;

            return SuppressionState.None;
        }

        /// <summary>
        /// Sets suppression value with clamping and UI update.
        ///
        /// Address: 0x1805e76d0
        /// </summary>
        public void SetSuppression(float value)
        {
            // Clamp to valid range
            if (value < 0f)
                value = 0f;
            else if (value > SuppressionConfig.MaxSuppression)
                value = SuppressionConfig.MaxSuppression;

            m_Suppression = value;

            // Update HUD if present
            if (m_HUD != null)
            {
                m_HUD.UpdateSuppression(value * SuppressionConfig.SuppressionToPercentMult, 1000);
            }

            // Trigger skill container update
            this.GetSkillContainer()?.Update();
        }

        /// <summary>
        /// Applies suppression damage to this actor.
        ///
        /// Address: 0x1805ddda0
        ///
        /// Flow:
        /// 1. Check if unit ignores suppression
        /// 2. Apply resistance modifiers
        /// 3. Apply discipline reduction
        /// 4. Update suppression value
        /// 5. Trigger events if suppression increased
        /// </summary>
        /// <param name="value">Raw suppression value to apply</param>
        /// <param name="direct">If true, bypasses some resistances</param>
        /// <param name="suppressor">Entity causing suppression</param>
        /// <param name="skill">Skill causing suppression</param>
        public void ApplySuppression(float value, bool direct, Entity suppressor, Skill skill)
        {
            // Call base implementation first
            base.ApplySuppression(value, direct, suppressor, skill);

            // Get entity properties
            EntityProperties props = this.GetEntityProperties();
            if (props == null) return;

            // Check if unit ignores suppression (flag at +0xEC bit 5)
            if (props.IgnoresSuppression)
                return;

            // For non-direct suppression, check additional immunity
            if (!direct && props.IgnoresIndirectSuppression)
                return;

            float resistanceMult = 1.0f;

            // Apply tile modifiers if taking positive suppression and in cover
            if (value > 0f && this.HasStylesheetPaths())  // proxy for "in valid position"
            {
                var parentContainer = this.ParentContainer;
                var tile = parentContainer?.GetCurrentTile();
                if (tile?.Cover != null)
                {
                    // Cover provides suppression resistance
                    resistanceMult = resistanceMult.AddMult(tile.Cover.SuppressionResistMult);
                }
            }

            float previousSuppression = m_Suppression;
            float clampedResistance = FloatExtensions.Clamped(resistanceMult);

            float finalValue;
            if (value > 0f)
            {
                // Get discipline-based reduction
                float disciplineReduction = props.GetDiscipline() *
                    SuppressionConfig.DisciplineReductionPerPoint;

                float disciplineMult = Math.Max(0f, 1.0f - disciplineReduction);
                float suppressionResist = FloatExtensions.Clamped(props.SuppressionResistMult);

                // Final suppression = value * resistance * discipline * global mult
                finalValue = clampedResistance * value * disciplineMult * suppressionResist;
            }
            else
            {
                // Suppression reduction (healing)
                finalValue = clampedResistance * value;
            }

            // Log suppression
            DevCombatLog.ReportSuppression(skill, this, finalValue, direct);

            // Apply suppression
            SetSuppression(m_Suppression + finalValue);

            // Notify if suppression increased
            if (previousSuppression < m_Suppression)
            {
                TacticalManager.Instance?.InvokeOnSuppressionApplied(
                    this, m_Suppression - previousSuppression, suppressor);
            }
        }

        /// <summary>
        /// Changes suppression and adjusts action points accordingly.
        ///
        /// Address: 0x5DE3B0
        ///
        /// Suppressed units have reduced AP.
        /// </summary>
        public void ChangeSuppressionAndUpdateAP(float delta)
        {
            float oldState = GetSuppressionPct();
            SetSuppression(m_Suppression + delta);
            float newState = GetSuppressionPct();

            // Adjust AP based on state change
            var oldSupState = GetStateFromPct(oldState);
            var newSupState = GetStateFromPct(newState);

            if (oldSupState != newSupState)
            {
                UpdateActionPointsForSuppressionState(newSupState);
            }
        }

        private SuppressionState GetStateFromPct(float pct)
        {
            if (pct >= SuppressionConfig.PinnedDownThreshold)
                return SuppressionState.PinnedDown;
            if (pct >= SuppressionConfig.SuppressedThreshold)
                return SuppressionState.Suppressed;
            return SuppressionState.None;
        }

        // =====================================================================
        // MORALE METHODS
        // =====================================================================

        /// <summary>
        /// Gets raw morale value.
        /// Address: 0x5DF5C0
        /// </summary>
        public float GetMorale()
        {
            return m_Morale;
        }

        /// <summary>
        /// Gets morale as percentage (0.0 - 1.0).
        ///
        /// Address: 0x1805df4a0
        /// </summary>
        public float GetMoralePct()
        {
            float maxMorale = GetMoraleMax();
            return m_Morale / maxMorale;
        }

        /// <summary>
        /// Gets maximum morale value.
        ///
        /// Address: 0x1805df330 / 0x1805df3e0
        /// </summary>
        /// <param name="adjustForHealth">If true, reduces max morale based on HP loss</param>
        public float GetMoraleMax(bool adjustForHealth = false)
        {
            EntityProperties props = this.GetEntityProperties();
            float baseMorale = props?.MoraleBase ?? 100f;
            float moraleMult = FloatExtensions.Clamped(props?.MoraleMult ?? 1f);

            float maxMorale = baseMorale * moraleMult;

            if (adjustForHealth)
            {
                float hpPct = (float)this.Hitpoints / this.HitpointsMax;
                maxMorale *= hpPct;
            }

            return maxMorale;
        }

        /// <summary>
        /// Gets current morale state based on morale percentage.
        ///
        /// Address: 0x1805df4d0
        /// </summary>
        public virtual MoraleState GetMoraleState()
        {
            float moralePct = GetMoralePct();
            int baseState;

            if (moralePct <= 0f)
            {
                // Zero morale - check if this is the selected unit (can't flee if selected)
                if (this.FactionType == FactionType.Player &&
                    TacticalManager.Instance?.SelectedActor == this)
                {
                    baseState = (int)MoraleState.Neutral;  // Don't flee if player-controlled
                }
                else
                {
                    baseState = (int)MoraleState.Fleeing;
                }
            }
            else if (moralePct <= SuppressionConfig.WaveringThreshold)
            {
                baseState = (int)MoraleState.Wavering;
            }
            else
            {
                baseState = (int)MoraleState.Neutral;
            }

            // Apply morale modifier from entity properties
            EntityProperties props = this.GetEntityProperties();
            int moraleModifier = props?.MoraleStateModifier ?? 0;  // +0xC0

            int finalState = baseState + moraleModifier;

            // Clamp to valid range (1-3)
            if (finalState < 1) finalState = 1;
            if (finalState > 3) finalState = 3;

            return (MoraleState)finalState;
        }
    }

    /// <summary>
    /// EntityProperties extensions for suppression/morale stats.
    /// </summary>
    public partial class EntityProperties
    {
        // =====================================================================
        // MORALE STATS (Offsets 0xC4-0xD8)
        // =====================================================================

        /// <summary>Base morale value. Offset: +0xC4</summary>
        public int MoraleBase;

        /// <summary>Morale multiplier. Offset: +0xC8</summary>
        public float MoraleMult = 1.0f;

        /// <summary>Base suppression resistance. Offset: +0xCC</summary>
        public int SuppressionResist;

        /// <summary>Suppression resistance multiplier. Offset: +0xD0</summary>
        public float SuppressionResistMult = 1.0f;

        /// <summary>Morale state modifier (shifts state up/down). Offset: +0xC0</summary>
        public int MoraleStateModifier;

        /// <summary>Discipline value (reduces suppression). Offset: varies</summary>
        public int Discipline;

        /// <summary>Flag: ignores suppression entirely. Offset: +0xEC bit 5</summary>
        public bool IgnoresSuppression;

        /// <summary>Flag: ignores indirect suppression. Offset: +0xEC bit 6</summary>
        public bool IgnoresIndirectSuppression;

        /// <summary>
        /// Gets effective discipline value.
        /// </summary>
        public int GetDiscipline()
        {
            // Discipline reduces suppression impact
            return Discipline;
        }

        /// <summary>
        /// Gets effective suppression from properties.
        ///
        /// Address: 0x18060c780
        /// </summary>
        public float GetSuppression()
        {
            float baseSuppression = SuppressionResist;
            float mult = FloatExtensions.Clamped(SuppressionResistMult);
            return baseSuppression * mult;
        }
    }
}
