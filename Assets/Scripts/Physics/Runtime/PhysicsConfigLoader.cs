using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads physics tuning CSVs from Resources/Physics/. Lives in the Runtime assembly
    /// so it can call Resources.Load; the returned structs (AeroConfig, ClubSpec) are pure Core types.
    /// </summary>
    public static class PhysicsConfigLoader
    {
        public static AeroConfig LoadAeroConfig()
        {
            var cfg = AeroConfig.Default;
            var ta = Resources.Load<TextAsset>("Physics/aero");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/aero.csv not found — using defaults");
                return cfg;
            }

            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float val)) continue;

                switch (key)
                {
                    case "air_density":           cfg.AirDensity           = fp.FromFloat(val); break;
                    case "ball_mass":             cfg.BallMass             = fp.FromFloat(val); break;
                    case "ball_cross_section":    cfg.BallCrossSection     = fp.FromFloat(val); break;
                    case "ball_radius":           cfg.BallRadius           = fp.FromFloat(val); break;
                    case "drag_coefficient":      cfg.DragCoefficient      = fp.FromFloat(val); break;
                    case "lift_coefficient_base": cfg.LiftCoefficientBase  = fp.FromFloat(val); break;
                    case "spin_rate_reference":   cfg.SpinRateReference    = fp.FromFloat(val); break;
                    case "lift_max_multiplier":   cfg.LiftMaxMultiplier    = fp.FromFloat(val); break;
                    case "use_drag_lut":          cfg.UseDragLut           = (val != 0f);       break;
                    case "use_lift_lut":          cfg.UseLiftLut           = (val != 0f);       break;
                }
            }

            cfg.DragLut = LoadDragLut();
            cfg.LiftLut = LoadLiftLut();
            return cfg;
        }

        public static CoefficientLut LoadDragLut()
        {
            return LoadLut("Physics/aero_drag_lut", "speed_mps", "cd");
        }

        public static CoefficientLut LoadLiftLut()
        {
            return LoadLut("Physics/aero_lift_lut", "spin_parameter", "cl");
        }

        // Parses a two-column CSV (x, y, optional notes). Returns default(CoefficientLut) on failure.
        private static CoefficientLut LoadLut(string resourcePath, string xHeader, string yHeader)
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogWarning($"[PhysicsConfigLoader] {resourcePath}.csv not found — LUT disabled");
                return default;
            }

            var xs = new List<fp>();
            var ys = new List<fp>();
            bool headerSkipped = false;

            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                if (!float.TryParse(parts[0].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float y)) continue;

                xs.Add(fp.FromFloat(x));
                ys.Add(fp.FromFloat(y));
            }

            if (xs.Count < 2)
            {
                Debug.LogWarning($"[PhysicsConfigLoader] {resourcePath}.csv has fewer than 2 valid rows — LUT disabled");
                return default;
            }

            return new CoefficientLut(xs.ToArray(), ys.ToArray());
        }

        public static List<ClubSpec> LoadClubSpecs()
        {
            var result = new List<ClubSpec>();
            var ta = Resources.Load<TextAsset>("Physics/clubs");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/clubs.csv not found");
                return result;
            }

            bool headerSkipped = false;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float speed)) continue;
                if (!float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float angle)) continue;
                if (!float.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float spin)) continue;
                if (!float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float carry)) continue;

                result.Add(new ClubSpec
                {
                    Id              = parts[0].Trim().Trim('"'),
                    BallSpeedMps    = fp.FromFloat(speed),
                    LaunchAngleDeg  = fp.FromFloat(angle),
                    SpinRateRpm     = fp.FromFloat(spin),
                    ExpectedCarryYd = fp.FromFloat(carry),
                });
            }
            return result;
        }
    }
}
