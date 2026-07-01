#nullable enable
// ─────────────────────────────────────────────────────────────────────────────
// StaminaModel — pure static helper for stamina drain / regen / penalty math.
// No UnityEngine dependency; no mutable state beyond the configuration slot.
// Call Configure(config) once at boot before any other method.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Core.Stamina
{
    public enum MeterColorState { High, Mid, Low }   // blue / yellow / red

    public static class StaminaModel
    {
        private static StaminaConfig _config;
        private static bool _configured;

        // ── Configuration ─────────────────────────────────────────────────────

        public static void Configure(StaminaConfig config)
        {
            _config = config;
            _configured = true;
        }

        public static bool IsConfigured => _configured;

        // ── Guard ─────────────────────────────────────────────────────────────

        private static void EnsureConfigured()
        {
            if (!_configured)
                throw new InvalidOperationException(
                    "[StaminaModel] Not configured. Call StaminaModel.Configure(config) before using any method.");
        }

        // ── Core formulas ──────────────────────────────────────────────────────

        /// <summary>Tank size in Condition points.</summary>
        public static int MaxCondition(int staminaStat)
        {
            EnsureConfigured();
            return (int)Math.Round(_config.TankBase + staminaStat * _config.TankPerStaminaPoint);
        }

        /// <summary>Flat Condition cost per hole completion.</summary>
        public static float DrainForHole()
        {
            EnsureConfigured();
            return _config.DrainPerHole;
        }

        /// <summary>Current Condition as a 0..1 fraction. Guards against MaxCondition==0.</summary>
        public static float ConditionPct(float condition, int staminaStat)
        {
            EnsureConfigured();
            int maxCond = MaxCondition(staminaStat);
            if (maxCond <= 0) return 0f;
            return Math.Max(0f, Math.Min(1f, condition / maxCond));
        }

        /// <summary>Condition points per real hour at the given Recovery stat.</summary>
        public static float RegenPerHour(int recoveryStat)
        {
            EnsureConfigured();
            return _config.RegenBasePerHour + recoveryStat * _config.RegenPerRecoveryPoint;
        }

        /// <summary>Condition points gained over a real elapsed span. Always >= 0.</summary>
        public static float RegenForElapsed(int recoveryStat, TimeSpan elapsed)
        {
            EnsureConfigured();
            if (elapsed.TotalHours <= 0d) return 0f;
            return Math.Max(0f, RegenPerHour(recoveryStat) * (float)elapsed.TotalHours);
        }

        /// <summary>
        /// Read-only projection of live Condition energy for display purposes.
        /// NEVER call AccrueRegen or any persisting method — display only.
        ///
        /// When hasTimestamp is false (conditionUpdatedUtc == default) returns
        /// currentEnergy unchanged (treat as fresh/full, no elapsed time known).
        ///
        /// simElapsed should be (DateTime.UtcNow − conditionUpdatedUtc) plus any
        /// demo-accelerator virtual hours. The result is clamped to maxEnergy.
        /// </summary>
        /// <param name="currentEnergy">pcd.currentStaminaEnergy</param>
        /// <param name="maxEnergy">pcd.maxStaminaEnergy</param>
        /// <param name="recovery">pcd.currentRecovery</param>
        /// <param name="hasTimestamp">false when conditionUpdatedUtc == default(DateTime)</param>
        /// <param name="simElapsed">Total simulated elapsed time (real + demo virtual hours)</param>
        public static float LiveDisplayEnergy(
            float currentEnergy,
            float maxEnergy,
            int   recovery,
            bool  hasTimestamp,
            TimeSpan simElapsed)
        {
            EnsureConfigured();
            if (!hasTimestamp) return currentEnergy;
            float gain = RegenForElapsed(recovery, simElapsed);
            return Math.Min(maxEnergy, currentEnergy + gain);
        }

        /// <summary>
        /// Stat penalty multiplier (0..FloorPenalty) for the given Condition %.
        /// Returns 0 at or above the comfort threshold; FloorPenalty at 0 %.
        /// </summary>
        public static float PenaltyFor(float conditionPct)
        {
            EnsureConfigured();
            if (conditionPct >= _config.ComfortThresholdPct) return 0f;

            float t = (_config.ComfortThresholdPct - conditionPct) / _config.ComfortThresholdPct;
            t = Math.Max(0f, Math.Min(1f, t));   // clamp01
            return _config.FloorPenalty * (float)Math.Pow(t, _config.PenaltyCurveExp);
        }

        /// <summary>
        /// Effective (post-penalty) value for a degraded stat.
        /// For non-degraded stats callers should skip this; IsDegraded is the gate.
        /// </summary>
        public static int EffectiveStat(int baseStat, float conditionPct)
        {
            EnsureConfigured();
            float penalty = PenaltyFor(conditionPct);
            return (int)Math.Round(baseStat * (1f - penalty));
        }

        /// <summary>True if the stat degrades under low Condition. Case-insensitive.</summary>
        public static bool IsDegraded(string statName)
        {
            EnsureConfigured();
            if (statName == null) return false;
            foreach (var s in _config.DegradedStats)
            {
                if (string.Equals(s, statName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Color state for the Stamina meter (High=blue, Mid=yellow, Low=red).</summary>
        public static MeterColorState MeterState(float conditionPct)
        {
            EnsureConfigured();
            if (conditionPct >= _config.MeterHighPct) return MeterColorState.High;
            if (conditionPct >= _config.MeterMidPct)  return MeterColorState.Mid;
            return MeterColorState.Low;
        }

        /// <summary>True when Condition is low enough to show the portrait alarm icon.</summary>
        public static bool IsLowConditionFlag(float conditionPct)
        {
            EnsureConfigured();
            return conditionPct < _config.LowConditionFlagPct;
        }

        // ── Test-support reset (Editor only) ──────────────────────────────────
        // Allows unit tests to reset between fixtures. Public so the test asmdef
        // (a separate assembly) can call it. Stripped from player builds by
        // UNITY_EDITOR guard — does not appear in shipped code.
#if UNITY_EDITOR
        public static void ResetForTests()
        {
            _configured = false;
            _config = default;
        }
#endif
    }
}
