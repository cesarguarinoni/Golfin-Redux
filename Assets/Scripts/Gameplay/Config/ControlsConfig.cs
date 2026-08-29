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

        /// <summary>
        /// Hard floor applied to the computed swing arrowHz before the putt multiplier (F13).
        /// arrowHz = Base + CC*Slope is a negative-slope line with no natural floor: past
        /// CC = Base/|Slope| it goes negative, the arrow runs backwards, never completes a
        /// pass, and the shot never auto-cancels. Prior to F13 this was "safe" only because
        /// RarityStatCaps happens to cap ClubControl at 50 — a promise made in a different
        /// file. This clamp makes ShotController safe on its own terms.
        /// Set to the calibrated CC-50 arrow speed so it is a no-op across the reachable range.
        /// </summary>
        public float MinArrowSpeedHz;
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

        // Spin selector UX (spin_selector_ux Order 354)
        public float SpinSelectorFloorRadius01;    // min selectable disc radius at spin=-10; default 0.20

        // Aim-line bend (fade_draw_aim_line_bend Order 355)
        public float AimLineDefaultReachPx;  // line length in canvas px at rest / Idle state
        public float AimLineCurveScale;      // k: lateral gain at full handle — tip lateral = k * |finetune| * reachPx

        // Map-view ring radius (map_view_aiming Order 352, iter-22 §6-MODEL)
        public float RingFrac;  // r_p = carry * RingFrac * (p/100) for p∈{80,100,120}

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
            BaseArrowSpeedHzAtCC0          = 2.0f,     // F13 (arrow_speed_retune): 3.0 → 2.0 (mirror controls.csv); low-CC arrow was too fast to time
            ArrowSpeedHzPerCC              = -0.03f,   // F13: −0.05 → −0.03 (mirror controls.csv); moves as a PAIR with the base — CC 0–50 spans 2.0→0.5 Hz, CC-50 end unchanged from F11
            MinArrowSpeedHz                = 0.5f,     // F13: floor = the calibrated CC-50 speed; no-op on reachable CC 0–50, guards CC > 66.7 where the raw line goes negative
            MaxCleanPassesAtCC0            = 1f,
            CleanPassesPerCC               = 0.08f,    // Order 732: 0.04 → 0.08 (mirror controls.csv); CC 0–50 → 1–5 clean passes
            MaxTotalPasses                 = 10f,
            DegradationYawDegPerPass       = 2f,
            PuttArrowSpeedMultiplier       = 0.8f,     // Order 732: 0.5 → 0.8 (mirror controls.csv); avoids compounding into 4 s putt cycles
            PuttBaseVelocityMps            = 5f,
            SpinMagScaleSlope              = 1.5f,
            SpinMaxTiltRad                 = 0.075f,   // D3 trim: ~1/4 of prior 0.3 ≈ 4.3° max sidespin curve
            FadeDrawMaxTiltRad             = 0.3f,     // D1 dominant: 0.3 ≈ 17° max at full handle deflection
            SpinSelectorFloorRadius01      = 0.20f,
            BallSpriteVisualRadiusFrac     = 0.957f,
            AimLineDefaultReachPx          = 500f,   // canvas px at rest (iter-2: increased from 400 for readability)
            AimLineCurveScale              = 0.55f,  // k: full finetune → tip lateral ≈ 0.55 × reachPx (iter-4: increased from 0.35 — Cesar wants a more pronounced, readable bend)
            RingFrac                       = 0.15f,  // map-view ring radius fraction: r_p = carry * RingFrac * (p/100) for p∈{80,100,120}
        };
    }
}
