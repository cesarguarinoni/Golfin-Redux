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

        // Timing bands + off-time power penalty (shot_timing_power, F15)
        // Band edges are shared by the drawn cone bands (ConeBandPalette), the timing slab
        // colour (ShotConeView.SlabColorFromProgress) and the power multiplier
        // (ShotController.TimingPowerMultiplier) so the colour the player reads and the
        // penalty they pay can never drift apart (D3).
        public float TimingBandGoldY01;    // slab progress at the gold band line (0 = cone base, 1 = apex)
        public float TimingBandGreenY01;   // slab progress at the green band line; at/above it = full power

        // Power multiplier at the two band edges (D2). timing01 >= green always yields 1.0.
        public float TimingPowerMulRed;    // multiplier at timing01 = 0 (bottom of the cone)
        public float TimingPowerMulGold;   // multiplier at timing01 = TimingBandGoldY01

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

        // ── Pendulum scheme (scheme_pendulum §3.6) ──────────────────────────────
        // Deliberately a SEPARATE set from the flick's MinUsefulPullPx / Max100PercentPullPx /
        // MaxOverpowerPullPx even though three of them seed to the same numbers: the two schemes
        // are being A/B'd against each other, so a retune of one that silently moved the other
        // would invalidate the comparison. They are also different UNITS in practice — the flick
        // measures a pull against the drawn cone's height, the pendulum against its own lane.
        public float PendulumMinUsefulPullPx;
        public float PendulumPull100Px;
        public float PendulumPull120Px;
        public float PendulumOverpowerGain;
        public float PendulumJustWindowAtAcc0_01;
        public float PendulumJustWindowAtAcc120_01;
        public float PendulumGoodWindow01;
        public float PendulumMissYawGain;
        public float PendulumCurveHalfWidthPx;
        public float PendulumMaxSweeps;          // treat as int at use site, as MaxTotalPasses is

        // Marker speed. Originally the Pendulum REUSED the flick's arrow line
        // (BaseArrowSpeedHzAtCC0 / ArrowSpeedHzPerCC / MinArrowSpeedHz) on the argument that both
        // schemes ask the same question. Cesar watching the first clip: "the horizontal ball is
        // moving way too fast" — a full sweep at 1.82 Hz is 0.55 s, which is not readable. The two
        // schemes turn out NOT to want the same number: the flick's arrow crosses a slab once per
        // pass, the pendulum's marker crosses the pip TWICE per cycle and has to be trackable by
        // eye the whole way. Its own line, so slowing it can never move the flick.
        public float PendulumBaseHzAtCC0;
        public float PendulumHzPerCC;            // negative: higher Club Control = slower marker
        public float PendulumMinHz;              // floor, same guard MinArrowSpeedHz gives the arrow

        // Power shrinks the target (Cesar, 2026-09-05: "the hitting area should shrink the further
        // the player pulls"). This is the scheme's risk/reward: a soft lay-up is forgiving, a
        // 120% pull is a needle. Applied to BOTH accuracy windows and to the drawn bands, from the
        // same number, so the green band the player is watching narrows as they pull.
        public float PendulumWindowScaleAtZeroPower;   // multiplier at power 0
        public float PendulumWindowScaleAtMaxPower;    // multiplier at MaxOverpowerNormalized

        // ── Needle scheme / "Tap Timing" (scheme_needle §3.5) ───────────────────
        // A THIRD set of pull thresholds, seeded to the Pendulum's own numbers. The three
        // schemes are being A/B'd against each other, so a retune of one must never move the
        // others — the same argument that gave Pendulum its own copy of the flick's thresholds.
        // NeedlePull80Px is the one with no counterpart: this scheme draws a ring at 80% as well,
        // and NeedlePowerCircleView places all three rings at HandleRestBelowBall + these, i.e.
        // where the club head LANDS at that power.
        public float NeedleMinUsefulPullPx;
        public float NeedlePull80Px;
        public float NeedlePull100Px;
        public float NeedlePull120Px;
        public float NeedleOverpowerGain;

        // The accuracy windows, as fractions of the arc's 90 degree half-sweep. |n| <= Perfect is
        // a PERFECT; |n| <= Good is a small HOOK/SLICE; past that is a big one.
        public float NeedlePerfectZoneAtAcc0_01;
        public float NeedlePerfectZoneAtAcc120_01;
        public float NeedleGoodZone01;
        public float NeedleYawGain;
        public float NeedleMissYawGain;
        public float NeedleCurveHalfWidthPx;

        // Needle speed, in SECONDS PER SWEEP rather than Hz. The needle crosses the arc ONCE and
        // then the swing is over, so "how long do I have to react" is the question the number
        // answers, and stating it in seconds is what makes "trackable by eye" checkable. Its own
        // line, never the flick's arrow or the Pendulum's Hz: sharing meant one scheme could not
        // be retuned without moving another, which is the whole point of the A/B.
        public float NeedleSweepSecAtCC0;
        public float NeedleSweepSecPerCC;      // positive: higher Club Control = slower, easier
        public float NeedleMinSweepSec;        // floor, so a retune to a negative slope cannot invert the sweep

        // Power shrinks the target, from the PEAK pull and on the DRAWN zones too, so the player
        // watches the blue zone close as they pull (the Pendulum carry-over Cesar asked for).
        public float NeedleWindowScaleAtZeroPower;
        public float NeedleWindowScaleAtMaxPower;

        // ── Free Swing scheme (scheme_freeswing §3.5) ───────────────────────────
        // A FOURTH set of pull thresholds, seeded to the Pendulum's and the Needle's own numbers
        // so the pull feels the same in all three on day one. Its own keys for the third time and
        // the same reason: the schemes are being A/B'd, and a retune of one must never move
        // another. FollowThroughPx and ReversalSlopPx have no counterpart anywhere — this is the
        // only scheme whose gesture continues PAST the impact line, and the only one that has to
        // tell a genuine second backswing from a thumb wobbling at the bottom of the first.
        public float FreeSwingMinUsefulPullPx;
        public float FreeSwingPull100Px;
        public float FreeSwingPull120Px;
        public float FreeSwingFollowThroughPx;
        public float FreeSwingReversalSlopPx;

        // Impact: HALF the clean window in canvas px either side of the lane centre, lerped by
        // Club Accuracy and shrunk by power. FreeSwingLaneView draws the green bar at twice this,
        // from the PEAK pull, so the target the player watched close is the graded one.
        public float FreeSwingImpactWindowAtAcc0Px;
        public float FreeSwingImpactWindowAtAcc120Px;
        public float FreeSwingImpactMissPx;
        public float FreeSwingYawGain;
        public float FreeSwingMissYawGain;

        // Path: how bowed the upstroke has to be before it shapes the shot at all, and how bowed
        // it has to be for a full fade/draw. Club Control WIDENS the dead zone — here the stat
        // buys forgiveness of thumb noise, not precision of aim.
        public float FreeSwingPathDeadzoneAtCC0Deg;
        public float FreeSwingPathDeadzoneAtCC120Deg;
        public float FreeSwingPathFullDeg;

        // Tempo: the upswing:backswing seconds ratio the swing is graded against, its tolerance,
        // and the upstroke speed below which the swing is a DUFF rather than a swing.
        public float FreeSwingIdealTempo;
        public float FreeSwingTempoWindowAtCC0;
        public float FreeSwingTempoWindowAtCC120;
        public float FreeSwingDuffSpeedPxPerSec;

        // Power shrinks BOTH windows, from the PEAK pull and on the DRAWN bar too. Free Swing has
        // no timing widget to speed up, so this is the ONLY cost a 120% pull carries.
        public float FreeSwingWindowScaleAtZeroPower;
        public float FreeSwingWindowScaleAtMaxPower;

        // How long the analyzer chip stays up after the shot, and how many finger samples the
        // driver's OWN ring buffer keeps (never ShotController.PushTouchSample — that ring is
        // Flick's gate).
        public float FreeSwingAnalyzerSeconds;
        public float FreeSwingSampleWindow;      // treat as int at use site, as MaxTotalPasses is

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
            TimingBandGoldY01              = 0.45f,   // F15: was ConeBandPalette.BandGoldY01 (same value)
            TimingBandGreenY01             = 0.85f,   // F15: was ConeBandPalette.BandGreenY01 (same value)
            TimingPowerMulRed              = 0.70f,   // F15: flick at the very bottom of the cone = 70% power
            TimingPowerMulGold             = 0.90f,   // F15: flick on the gold line = 90% power
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

            // scheme_pendulum §3.6 seed values — mirror controls.csv (F13 two-mirror rule).
            PendulumMinUsefulPullPx        = 40f,
            PendulumPull100Px              = 380f,   // 300 -> 380 (2026-09-05): the longer pill needs the
                                                     // ticks LOW in it, and a tick only moves down honestly
                                                     // if the pull it represents gets longer
            PendulumPull120Px              = 456f,   // 360 -> 456, keeping the node's 1.2x tick spacing
            PendulumOverpowerGain          = 1.0f,
            PendulumJustWindowAtAcc0_01    = 0.08f,
            PendulumJustWindowAtAcc120_01  = 0.20f,
            PendulumGoodWindow01           = 0.45f,  // Figma BandGood is 0.40 (288px); see csv note
            PendulumMissYawGain            = 1.5f,
            PendulumCurveHalfWidthPx       = 150f,
            PendulumMaxSweeps              = 10f,
            PendulumBaseHzAtCC0            = 1.0f,    // was the flick's 2.0 — halved after Cesar's first-clip review
            PendulumHzPerCC                = -0.015f, // half the flick's slope, so the CC ladder keeps the same shape
            PendulumMinHz                  = 0.35f,
            PendulumWindowScaleAtZeroPower = 1.35f,
            PendulumWindowScaleAtMaxPower  = 0.55f,

            // scheme_needle §3.5 seed values — mirror controls.csv (F13 two-mirror rule).
            NeedleMinUsefulPullPx        = 40f,
            NeedlePull80Px               = 304f,   // 0.8 x Pull100Px — the 80% ring
            NeedlePull100Px              = 380f,   // seeded equal to PendulumPull100Px: the pull
            NeedlePull120Px              = 456f,   // must feel the same in both schemes on day one
            NeedleOverpowerGain          = 1.0f,
            NeedlePerfectZoneAtAcc0_01   = 0.08f,
            NeedlePerfectZoneAtAcc120_01 = 0.20f,
            NeedleGoodZone01             = 0.40f,  // Figma ZoneGood measures 37.82 deg = 0.420 of 90
            NeedleYawGain                = 1.0f,
            NeedleMissYawGain            = 1.5f,
            NeedleCurveHalfWidthPx       = 150f,
            NeedleSweepSecAtCC0          = 1.2f,
            NeedleSweepSecPerCC          = 0.006f,
            NeedleMinSweepSec            = 0.8f,
            NeedleWindowScaleAtZeroPower = 1.35f,
            NeedleWindowScaleAtMaxPower  = 0.55f,

            // scheme_freeswing §3.5 seed values — mirror controls.csv (F13 two-mirror rule).
            FreeSwingMinUsefulPullPx        = 40f,
            FreeSwingPull100Px              = 380f,  // seeded equal to Pendulum/Needle: the pull
            FreeSwingPull120Px              = 456f,  // must feel the same in all three on day one
            FreeSwingFollowThroughPx        = 160f,  // node: the lane's top edge, 160px above the ball
            FreeSwingReversalSlopPx         = 24f,
            FreeSwingImpactWindowAtAcc0Px   = 22f,
            FreeSwingImpactWindowAtAcc120Px = 60f,
            FreeSwingImpactMissPx           = 140f,
            FreeSwingYawGain                = 1.0f,
            FreeSwingMissYawGain            = 1.5f,
            FreeSwingPathDeadzoneAtCC0Deg   = 6f,
            FreeSwingPathDeadzoneAtCC120Deg = 12f,
            FreeSwingPathFullDeg            = 30f,
            FreeSwingIdealTempo             = 0.5f,  // an upswing half as long as the backswing
            FreeSwingTempoWindowAtCC0       = 0.25f,
            FreeSwingTempoWindowAtCC120     = 0.45f,
            FreeSwingDuffSpeedPxPerSec      = 900f,
            FreeSwingWindowScaleAtZeroPower = 1.35f,
            FreeSwingWindowScaleAtMaxPower  = 0.55f,
            FreeSwingAnalyzerSeconds        = 1.5f,
            FreeSwingSampleWindow           = 90f,
        };
    }
}
