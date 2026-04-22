using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

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
                    case "spin_decay_rate":       cfg.SpinDecayRate        = fp.FromFloat(val); break;
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

        public static WindConfig LoadWindConfig()
        {
            var cfg = WindConfig.Calm;
            var ta = Resources.Load<TextAsset>("Physics/wind");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/wind.csv not found — using calm defaults");
                return cfg;
            }

            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();

                if (key == "seed")
                {
                    if (uint.TryParse(parts[1].Trim(),
                            System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out uint seed))
                        cfg.Seed = seed;
                    continue;
                }

                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float val)) continue;

                switch (key)
                {
                    case "base_x":               cfg.BaseVelocity      = new Golfin.Physics.Math.fp3(fp.FromFloat(val), cfg.BaseVelocity.y, cfg.BaseVelocity.z); break;
                    case "base_y":               cfg.BaseVelocity      = new Golfin.Physics.Math.fp3(cfg.BaseVelocity.x, fp.FromFloat(val), cfg.BaseVelocity.z); break;
                    case "base_z":               cfg.BaseVelocity      = new Golfin.Physics.Math.fp3(cfg.BaseVelocity.x, cfg.BaseVelocity.y, fp.FromFloat(val)); break;
                    case "gust_amplitude":       cfg.GustAmplitude     = fp.FromFloat(val); break;
                    case "gust_frequency":       cfg.GustFrequency     = fp.FromFloat(val); break;
                    case "altitude_factor":      cfg.AltitudeFactor    = fp.FromFloat(val); break;
                    case "altitude_ref_meters":  cfg.AltitudeRefMeters = fp.FromFloat(val); break;
                }
            }
            return cfg;
        }

        public static SurfaceConfig LoadSurfaceConfig()
        {
            var cfg = SurfaceConfig.Default;
            var ta = Resources.Load<TextAsset>("Physics/surfaces");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/surfaces.csv not found — using defaults");
                return cfg;
            }

            bool headerSkipped = false;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                string name = parts[0].Trim();
                if (!System.Enum.TryParse<SurfaceType>(name, out var st))
                {
                    Debug.LogWarning($"[PhysicsConfigLoader] surfaces.csv: unknown surface '{name}' — skipped");
                    continue;
                }

                if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float restitution)) continue;
                if (!float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float friction)) continue;
                if (!float.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float rolling)) continue;
                if (!float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float stopSpeed)) continue;

                cfg.Coefficients[(int)st] = new SurfaceCoefficients
                {
                    Restitution       = fp.FromFloat(restitution),
                    TangentFriction   = fp.FromFloat(friction),
                    RollingResistance = fp.FromFloat(rolling),
                    StopSpeed         = fp.FromFloat(stopSpeed),
                };
            }
            return cfg;
        }

        public static PuttConfig LoadPuttConfig()
        {
            var cfg = PuttConfig.Default;
            var ta = Resources.Load<TextAsset>("Physics/putt");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/putt.csv not found — using defaults");
                return cfg;
            }

            bool headerSkipped = false;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0].Trim();
                if (!System.Enum.TryParse<SurfaceType>(name, out var st))
                {
                    Debug.LogWarning($"[PhysicsConfigLoader] putt.csv: unknown surface '{name}' — skipped");
                    continue;
                }

                if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float rolling)) continue;

                float stopSpeed = 0.04f;
                if (parts.Length >= 3)
                    float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out stopSpeed);

                cfg.Coefficients[(int)st] = new SurfaceCoefficients
                {
                    Restitution       = fp.Zero,
                    TangentFriction   = fp.One,
                    RollingResistance = fp.FromFloat(rolling),
                    StopSpeed         = fp.FromFloat(stopSpeed),
                };
            }
            return cfg;
        }

        public static StatCoefficients LoadStatCoefficients()
        {
            var cfg = StatCoefficients.Default;
            var ta = Resources.Load<TextAsset>("Physics/stats");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/stats.csv not found — using defaults");
                return cfg;
            }

            bool headerSkipped = false;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float val)) continue;

                switch (key)
                {
                    case "club_power_per_point":        cfg.ClubPowerPerPoint          = fp.FromFloat(val); break;
                    case "club_accuracy_per_point":     cfg.ClubAccuracyPerPoint       = fp.FromFloat(val); break;
                    case "club_lie_resistance_per_point": cfg.ClubLieResistancePerPoint = fp.FromFloat(val); break;
                    case "ball_power_per_point":        cfg.BallPowerPerPoint          = fp.FromFloat(val); break;
                    case "ball_rebound_per_point":      cfg.BallReboundPerPoint        = fp.FromFloat(val); break;
                    case "ball_wind_cut_per_point":     cfg.BallWindCutPerPoint        = fp.FromFloat(val); break;
                    case "ball_roll_per_point":         cfg.BallRollPerPoint           = fp.FromFloat(val); break;
                    case "ball_spin_per_point":         cfg.BallSpinPerPoint           = fp.FromFloat(val); break;
                    case "char_strength_per_point":     cfg.CharStrengthPerPoint       = fp.FromFloat(val); break;
                    case "char_club_control_per_point": cfg.CharClubControlPerPoint    = fp.FromFloat(val); break;
                    case "putter_control_per_point":    cfg.PutterControlPerPoint      = fp.FromFloat(val); break;
                    case "putter_accuracy_per_point":   cfg.PutterAccuracyPerPoint     = fp.FromFloat(val); break;
                    case "putter_weight_per_point":     cfg.PutterWeightPerPoint       = fp.FromFloat(val); break;
                    case "stamina_floor_fraction":      cfg.StaminaFloorFraction       = fp.FromFloat(val); break;
                }
            }
            return cfg;
        }

        public static StatCaps LoadStatCaps()
        {
            var caps = StatCaps.Default;
            var ta = Resources.Load<TextAsset>("Physics/stat_caps");
            if (ta == null)
            {
                Debug.LogWarning("[PhysicsConfigLoader] Physics/stat_caps.csv not found — using defaults");
                return caps;
            }

            bool headerSkipped = false;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float val)) continue;

                switch (key)
                {
                    case "velocity_multiplier_max":          caps.VelocityMultiplierMax         = fp.FromFloat(val); break;
                    case "aim_cone_reduction_max":           caps.AimConeReductionMax           = fp.FromFloat(val); break;
                    case "lie_resistance_max":               caps.LieResistanceMax              = fp.FromFloat(val); break;
                    case "overpower_forgiveness_max":        caps.OverpowerForgivenessMax       = fp.FromFloat(val); break;
                    case "stamina_cap_multiplier_max":       caps.StaminaCapMultiplierMax       = fp.FromFloat(val); break;
                    case "putter_off_center_forgiveness_max": caps.PutterOffCenterForgivenessMax = fp.FromFloat(val); break;
                    case "rebound_multiplier_max":           caps.ReboundMultiplierMax          = fp.FromFloat(val); break;
                    case "rebound_multiplier_min":           caps.ReboundMultiplierMin          = fp.FromFloat(val); break;
                    case "roll_multiplier_max":              caps.RollMultiplierMax             = fp.FromFloat(val); break;
                    case "roll_multiplier_min":              caps.RollMultiplierMin             = fp.FromFloat(val); break;
                    case "wind_cut_max":                     caps.WindCutMax                    = fp.FromFloat(val); break;
                }
            }
            return caps;
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
