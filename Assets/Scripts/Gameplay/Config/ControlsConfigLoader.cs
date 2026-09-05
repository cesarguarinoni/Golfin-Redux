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
                    // scheme_needle §3.5
                    case "NeedleMinUsefulPullPx":          cfg.NeedleMinUsefulPullPx          = val; break;
                    case "NeedlePull80Px":                 cfg.NeedlePull80Px                 = val; break;
                    case "NeedlePull100Px":                cfg.NeedlePull100Px                = val; break;
                    case "NeedlePull120Px":                cfg.NeedlePull120Px                = val; break;
                    case "NeedleOverpowerGain":            cfg.NeedleOverpowerGain            = val; break;
                    case "NeedlePerfectZoneAtAcc0_01":     cfg.NeedlePerfectZoneAtAcc0_01     = val; break;
                    case "NeedlePerfectZoneAtAcc120_01":   cfg.NeedlePerfectZoneAtAcc120_01   = val; break;
                    case "NeedleGoodZone01":               cfg.NeedleGoodZone01               = val; break;
                    case "NeedleYawGain":                  cfg.NeedleYawGain                  = val; break;
                    case "NeedleMissYawGain":              cfg.NeedleMissYawGain              = val; break;
                    case "NeedleCurveHalfWidthPx":         cfg.NeedleCurveHalfWidthPx         = val; break;
                    case "NeedleSweepSecAtCC0":            cfg.NeedleSweepSecAtCC0            = val; break;
                    case "NeedleSweepSecPerCC":            cfg.NeedleSweepSecPerCC            = val; break;
                    case "NeedleMinSweepSec":              cfg.NeedleMinSweepSec              = val; break;
                    case "NeedleWindowScaleAtZeroPower":   cfg.NeedleWindowScaleAtZeroPower   = val; break;
                    case "NeedleWindowScaleAtMaxPower":    cfg.NeedleWindowScaleAtMaxPower    = val; break;
                    // scheme_freeswing §3.5
                    case "FreeSwingMinUsefulPullPx":        cfg.FreeSwingMinUsefulPullPx        = val; break;
                    case "FreeSwingPull100Px":              cfg.FreeSwingPull100Px              = val; break;
                    case "FreeSwingPull120Px":              cfg.FreeSwingPull120Px              = val; break;
                    case "FreeSwingFollowThroughPx":        cfg.FreeSwingFollowThroughPx        = val; break;
                    case "FreeSwingReversalSlopPx":         cfg.FreeSwingReversalSlopPx         = val; break;
                    case "FreeSwingImpactWindowAtAcc0Px":   cfg.FreeSwingImpactWindowAtAcc0Px   = val; break;
                    case "FreeSwingImpactWindowAtAcc120Px": cfg.FreeSwingImpactWindowAtAcc120Px = val; break;
                    case "FreeSwingImpactMissPx":           cfg.FreeSwingImpactMissPx           = val; break;
                    case "FreeSwingYawGain":                cfg.FreeSwingYawGain                = val; break;
                    case "FreeSwingMissYawGain":            cfg.FreeSwingMissYawGain            = val; break;
                    case "FreeSwingPathDeadzoneAtCC0Deg":   cfg.FreeSwingPathDeadzoneAtCC0Deg   = val; break;
                    case "FreeSwingPathDeadzoneAtCC120Deg": cfg.FreeSwingPathDeadzoneAtCC120Deg = val; break;
                    case "FreeSwingPathFullDeg":            cfg.FreeSwingPathFullDeg            = val; break;
                    case "FreeSwingIdealTempo":             cfg.FreeSwingIdealTempo             = val; break;
                    case "FreeSwingTempoWindowAtCC0":       cfg.FreeSwingTempoWindowAtCC0       = val; break;
                    case "FreeSwingTempoWindowAtCC120":     cfg.FreeSwingTempoWindowAtCC120     = val; break;
                    case "FreeSwingDuffSpeedPxPerSec":      cfg.FreeSwingDuffSpeedPxPerSec      = val; break;
                    case "FreeSwingWindowScaleAtZeroPower": cfg.FreeSwingWindowScaleAtZeroPower = val; break;
                    case "FreeSwingWindowScaleAtMaxPower":  cfg.FreeSwingWindowScaleAtMaxPower  = val; break;
                    case "FreeSwingAnalyzerSeconds":        cfg.FreeSwingAnalyzerSeconds        = val; break;
                    case "FreeSwingSampleWindow":           cfg.FreeSwingSampleWindow           = val; break;
                    default: matched = false; break;
                }

                if (!matched)
                    Debug.LogWarning($"[ControlsConfigLoader] Unknown key '{key}' in controls.csv — skipped");
            }
            return cfg;
        }
    }
}
