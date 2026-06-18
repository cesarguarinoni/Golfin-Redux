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
        public float SpinMaxTiltRad;      // TRIM value (D3, fade_draw_core_wiring): ~1/4 of original 0.3 → 0.075 ≈ 4.3°

        // Fade/Draw shaping (fade_draw_core_wiring Order 356, D1–D5)
        public float FadeDrawMaxTiltRad;  // dominant curve term: max tilt when handle at ±1 (propose = old SpinMaxTiltRad = 0.3)
        public float AimNudgeRangeRad;    // aim yaw nudge at full handle deflection (Straight mode), propose ~3deg = 0.052rad

        // Spin selector UX (spin_selector_ux Order 354)
        public float SpinSelectorFloorRadius01;    // min selectable disc radius at spin=-10; default 0.20

        // Aim-line bend (fade_draw_aim_line_bend Order 355)
        public float AimLineDefaultReachPx;  // line length in canvas px at rest / Idle state
        public float AimLineCurveScale;      // k: lateral gain at full handle — tip lateral = k * |finetune| * reachPx

        /// <summary>
        /// Fraction of the BallImage RectTransform half-width that equals the visible
        /// painted ball edge (accounting for sprite alpha padding).
        ///
        /// The ball sprite (200×200 ASTC_6x6) has transparent padding: the painted
        /// circle edge lands at approximately 95.7% of the RectTransform half-width
        /// (empirically measured: reviewer observed ~287 canvas-px visible radius out of
        /// 300 canvas-px RectTransform half-width).
        ///
        /// Used by SpinPanelWidget to cap the HIGH disc's un-dimmed hole to the visible
        /// painted ball edge rather than the RectTransform border.
        /// Tunable via controls.csv (key: BallSpriteVisualRadiusFrac).
        /// </summary>
        public float BallSpriteVisualRadiusFrac;   // 0.957 = visible ball edge / RT half-width

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
            BaseArrowSpeedHzAtCC0          = 3.0f,
            ArrowSpeedHzPerCC              = -0.025f,
            MaxCleanPassesAtCC0            = 1f,
            CleanPassesPerCC               = 0.04f,
            MaxTotalPasses                 = 10f,
            DegradationYawDegPerPass       = 2f,
            PuttArrowSpeedMultiplier       = 0.5f,
            PuttBaseVelocityMps            = 5f,
            SpinMagScaleSlope              = 1.5f,
            SpinMaxTiltRad                 = 0.075f,   // D3 trim: ~1/4 of prior 0.3 ≈ 4.3° max sidespin curve
            FadeDrawMaxTiltRad             = 0.3f,     // D1 dominant: 0.3 ≈ 17° max at full handle deflection
            AimNudgeRangeRad               = 0.0524f,  // D4 Straight mode aim nudge: ~3° full deflection
            SpinSelectorFloorRadius01      = 0.20f,
            BallSpriteVisualRadiusFrac     = 0.957f,
            AimLineDefaultReachPx          = 500f,   // canvas px at rest (iter-2: increased from 400 for readability)
            AimLineCurveScale              = 0.55f,  // k: full finetune → tip lateral ≈ 0.55 × reachPx (iter-4: increased from 0.35 — Cesar wants a more pronounced, readable bend)
        };
    }
}
