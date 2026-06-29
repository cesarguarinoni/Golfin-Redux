#nullable enable
// ─────────────────────────────────────────────────────────────────────────────
// StaminaConfig — pure value struct holding tunables parsed from stamina_economy.csv
// No UnityEngine.Resources / IO dependency; only System types.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Golfin.Core.Stamina
{
    public readonly struct StaminaConfig
    {
        public readonly float DrainPerHole;
        public readonly float TankBase;
        public readonly float TankPerStaminaPoint;
        public readonly float RegenBasePerHour;
        public readonly float RegenPerRecoveryPoint;
        public readonly float ComfortThresholdPct;   // 0..1
        public readonly float FloorPenalty;          // 0..1
        public readonly float PenaltyCurveExp;
        public readonly float MeterHighPct;          // 0..1
        public readonly float MeterMidPct;           // 0..1
        public readonly float LowConditionFlagPct;   // 0..1
        public readonly IReadOnlyCollection<string> DegradedStats; // e.g. {"Strength","ClubControl"}

        private StaminaConfig(
            float drainPerHole,
            float tankBase,
            float tankPerStaminaPoint,
            float regenBasePerHour,
            float regenPerRecoveryPoint,
            float comfortThresholdPct,
            float floorPenalty,
            float penaltyCurveExp,
            float meterHighPct,
            float meterMidPct,
            float lowConditionFlagPct,
            IReadOnlyCollection<string> degradedStats)
        {
            DrainPerHole           = drainPerHole;
            TankBase               = tankBase;
            TankPerStaminaPoint    = tankPerStaminaPoint;
            RegenBasePerHour       = regenBasePerHour;
            RegenPerRecoveryPoint  = regenPerRecoveryPoint;
            ComfortThresholdPct    = comfortThresholdPct;
            FloorPenalty           = floorPenalty;
            PenaltyCurveExp        = penaltyCurveExp;
            MeterHighPct           = meterHighPct;
            MeterMidPct            = meterMidPct;
            LowConditionFlagPct    = lowConditionFlagPct;
            DegradedStats          = degradedStats;
        }

        /// <summary>
        /// Parse a two-column CSV text (key,value[,notes...]).
        /// Header row is detected and skipped. Blank lines and rows with no key are ignored.
        /// The 'degraded_stats' value is a semicolon-separated list.
        /// Pure — no IO.
        /// </summary>
        public static StaminaConfig Parse(string csvText)
        {
            if (csvText == null) throw new ArgumentNullException(nameof(csvText));

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool headerSkipped = false;
            foreach (var rawLine in csvText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // Skip header row (first non-blank, non-comment line that starts with "key")
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    if (line.StartsWith("key", StringComparison.OrdinalIgnoreCase))
                        continue; // skip the CSV header
                }

                // Split only on the first two commas; notes column is ignored
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                string val = parts[1].Trim();
                if (key.Length == 0) continue;

                values[key] = val;
            }

            float GetFloat(string key, float fallback = 0f)
            {
                if (values.TryGetValue(key, out var raw) &&
                    float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                    return result;
                return fallback;
            }

            IReadOnlyCollection<string> GetStatList(string key)
            {
                if (values.TryGetValue(key, out var raw) && raw.Length > 0)
                    return raw.Split(';')
                              .Select(s => s.Trim())
                              .Where(s => s.Length > 0)
                              .ToArray();
                return Array.Empty<string>();
            }

            return new StaminaConfig(
                drainPerHole:           GetFloat("drain_per_hole"),
                tankBase:               GetFloat("tank_base"),
                tankPerStaminaPoint:    GetFloat("tank_per_stamina_point"),
                regenBasePerHour:       GetFloat("regen_base_per_hour"),
                regenPerRecoveryPoint:  GetFloat("regen_per_recovery_point"),
                comfortThresholdPct:    GetFloat("comfort_threshold_pct"),
                floorPenalty:           GetFloat("floor_penalty"),
                penaltyCurveExp:        GetFloat("penalty_curve_exp"),
                meterHighPct:           GetFloat("meter_high_pct"),
                meterMidPct:            GetFloat("meter_mid_pct"),
                lowConditionFlagPct:    GetFloat("low_condition_flag_pct"),
                degradedStats:          GetStatList("degraded_stats")
            );
        }
    }
}
