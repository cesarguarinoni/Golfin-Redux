namespace Golfin.Gameplay.Config
{
    public struct ControlsConfig
    {
        // Pull thresholds (pixels)
        public float PullStartThresholdPx;
        public float MinUsefulPullPx;
        public float Max100PercentPullPx;
        public float MaxOverpowerPullPx;

        // Flick detection
        public float FlickVelocityThresholdPxPerSec;
        public float FlickAngleDeviationMaxDeg;

        // Cone geometry
        public float ConeHalfAngleAtAcc0Deg;
        public float ConeHalfAngleAtAcc100Deg;

        // Cone visibility
        public float ConeIdleAlpha;
        public float ConeFadeInSeconds;
        public float ConeFadeOutSeconds;

        // Touch detection
        public float BallHitZoneRadiusPx;

        // Targeting line
        public float TargetingLineLengthMeters;

        // Timing arrows
        public float BaseArrowSpeedHzAtCC0;
        public float ArrowSpeedHzPerCC;
        public float MaxCleanPassesAtCC0;   // treat as int at use site
        public float CleanPassesPerCC;
        public float MaxTotalPasses;        // treat as int at use site
        public float DegradationYawDegPerPass;

        // Putt mode
        public float PuttArrowSpeedMultiplier;
        public float PuttBaseVelocityMps;

        // Spin input (player-input lane; see spin_and_shot_shape_wiring SPEC §5.3)
        // Q2 lock: slope=1.5 = sign-flip allowed at spinY=+1 (magScale=-0.5 → true topspin).
        // NOTE(escalation): With slope=1.5, topspin (spinY=+1) goes ~128m SHORTER than CENTER
        // because the flipped Magnus axis pushes the ball DOWN (Magnus lift → drag on topspin).
        // The spec visual gate criterion "Δ carry ≥3m or Δ total ≥8m further" cannot be met
        // with any positive slope in this Magnus-lift model. Escalated to architect: see
        // spin_and_shot_shape_wiring IMPLEMENTER_REPORT.md §Open questions for Architect, item 1.
        public float SpinMagScaleSlope;   // 1.5 = sign-flip allowed at spinY=+1 (Q2 lock)
        public float SpinMaxTiltRad;      // 0.3 ≈ 17° max axis tilt at spinX=±1

        public static readonly ControlsConfig Default = new ControlsConfig
        {
            PullStartThresholdPx           = 30f,
            MinUsefulPullPx                = 40f,
            Max100PercentPullPx            = 300f,
            MaxOverpowerPullPx             = 360f,
            FlickVelocityThresholdPxPerSec = 1500f,
            FlickAngleDeviationMaxDeg      = 30f,
            ConeHalfAngleAtAcc0Deg         = 5f,
            ConeHalfAngleAtAcc100Deg       = 20f,
            ConeIdleAlpha                  = 0.25f,
            ConeFadeInSeconds              = 0.15f,
            ConeFadeOutSeconds             = 0.30f,
            BallHitZoneRadiusPx            = 80f,
            TargetingLineLengthMeters      = 30f,
            BaseArrowSpeedHzAtCC0          = 0.5f,
            ArrowSpeedHzPerCC              = 0.025f,
            MaxCleanPassesAtCC0            = 1f,
            CleanPassesPerCC               = 0.04f,
            MaxTotalPasses                 = 10f,
            DegradationYawDegPerPass       = 2f,
            PuttArrowSpeedMultiplier       = 0.5f,
            PuttBaseVelocityMps            = 5f,
            SpinMagScaleSlope              = 1.5f,
            SpinMaxTiltRad                 = 0.3f,
        };
    }
}
