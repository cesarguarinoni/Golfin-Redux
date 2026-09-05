using UnityEngine;

namespace Golfin.Gameplay.Config
{
    public static class ControlsConfigLoader
    {
        public static ControlsConfig Load()
        {
            var cfg = ControlsConfig.Default;
            var ta = Resources.Load<TextAsset>("Gameplay/controls");
            if (ta == null)
            {
                Debug.LogWarning("[ControlsConfigLoader] Gameplay/controls.csv not found — using defaults");
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
                        out float val))
                    continue;

                bool matched = true;
                switch (key)
                {
                    case "PullStartThresholdPx":           cfg.PullStartThresholdPx           = val; break;
                    case "MinUsefulPullPx":                cfg.MinUsefulPullPx                = val; break;
                    case "Max100PercentPullPx":            cfg.Max100PercentPullPx            = val; break;
                    case "MaxOverpowerPullPx":             cfg.MaxOverpowerPullPx             = val; break;
                    case "FlickVelocityThresholdPxPerSec": cfg.FlickVelocityThresholdPxPerSec = val; break;
                    case "FlickAngleDeviationMaxDeg":      cfg.FlickAngleDeviationMaxDeg      = val; break;
                    case "ConeHalfAngleAtAcc0Deg":         cfg.ConeHalfAngleAtAcc0Deg         = val; break;
                    case "ConeHalfAngleAtAcc100Deg":       cfg.ConeHalfAngleAtAcc100Deg       = val; break;
                    case "ConeIdleAlpha":                  cfg.ConeIdleAlpha                  = val; break;
                    case "ConeFadeInSeconds":              cfg.ConeFadeInSeconds              = val; break;
                    case "ConeFadeOutSeconds":             cfg.ConeFadeOutSeconds             = val; break;
                    case "BallHitZoneRadiusPx":            cfg.BallHitZoneRadiusPx            = val; break;
                    case "TargetingLineLengthMeters":      cfg.TargetingLineLengthMeters      = val; break;
                    case "BaseArrowSpeedHzAtCC0":          cfg.BaseArrowSpeedHzAtCC0          = val; break;
                    case "ArrowSpeedHzPerCC":              cfg.ArrowSpeedHzPerCC              = val; break;
                    case "MinArrowSpeedHz":                cfg.MinArrowSpeedHz                = val; break;
                    case "MaxCleanPassesAtCC0":            cfg.MaxCleanPassesAtCC0            = val; break;
                    case "CleanPassesPerCC":               cfg.CleanPassesPerCC               = val; break;
                    case "MaxTotalPasses":                 cfg.MaxTotalPasses                 = val; break;
                    case "DegradationYawDegPerPass":       cfg.DegradationYawDegPerPass       = val; break;
                    case "TimingBandGoldY01":              cfg.TimingBandGoldY01              = val; break;
                    case "TimingBandGreenY01":             cfg.TimingBandGreenY01             = val; break;
                    case "TimingPowerMulRed":              cfg.TimingPowerMulRed              = val; break;
                    case "TimingPowerMulGold":             cfg.TimingPowerMulGold             = val; break;
                    case "PuttArrowSpeedMultiplier":       cfg.PuttArrowSpeedMultiplier       = val; break;
                    case "PuttBaseVelocityMps":            cfg.PuttBaseVelocityMps            = val; break;
                    case "SpinMagScaleSlope":              cfg.SpinMagScaleSlope              = val; break;
                    case "SpinMaxTiltRad":                 cfg.SpinMaxTiltRad                 = val; break;
                    case "FadeDrawMaxTiltRad":             cfg.FadeDrawMaxTiltRad             = val; break;
                    case "SpinSelectorFloorRadius01":      cfg.SpinSelectorFloorRadius01      = val; break;
                    case "BallSpriteVisualRadiusFrac":     cfg.BallSpriteVisualRadiusFrac     = val; break;
                    case "AimLineDefaultReachPx":          cfg.AimLineDefaultReachPx          = val; break;
                    case "AimLineCurveScale":              cfg.AimLineCurveScale              = val; break;
                    // scheme_pendulum §3.6
                    case "PendulumMinUsefulPullPx":        cfg.PendulumMinUsefulPullPx        = val; break;
                    case "PendulumPull100Px":              cfg.PendulumPull100Px              = val; break;
                    case "PendulumPull120Px":              cfg.PendulumPull120Px              = val; break;
                    case "PendulumOverpowerGain":          cfg.PendulumOverpowerGain          = val; break;
                    case "PendulumJustWindowAtAcc0_01":    cfg.PendulumJustWindowAtAcc0_01    = val; break;
                    case "PendulumJustWindowAtAcc120_01":  cfg.PendulumJustWindowAtAcc120_01  = val; break;
                    case "PendulumGoodWindow01":           cfg.PendulumGoodWindow01           = val; break;
                    case "PendulumMissYawGain":            cfg.PendulumMissYawGain            = val; break;
                    case "PendulumCurveHalfWidthPx":       cfg.PendulumCurveHalfWidthPx       = val; break;
                    case "PendulumMaxSweeps":              cfg.PendulumMaxSweeps              = val; break;
                    case "PendulumBaseHzAtCC0":            cfg.PendulumBaseHzAtCC0            = val; break;
                    case "PendulumHzPerCC":                cfg.PendulumHzPerCC                = val; break;
                    case "PendulumMinHz":                  cfg.PendulumMinHz                  = val; break;
                    case "PendulumWindowScaleAtZeroPower": cfg.PendulumWindowScaleAtZeroPower = val; break;
                    case "PendulumWindowScaleAtMaxPower":  cfg.PendulumWindowScaleAtMaxPower  = val; break;
                    default: matched = false; break;
                }

                if (!matched)
                    Debug.LogWarning($"[ControlsConfigLoader] Unknown key '{key}' in controls.csv — skipped");
            }
            return cfg;
        }
    }
}
