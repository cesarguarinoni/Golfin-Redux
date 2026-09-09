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
        //
        // miss_grade_duff (2026-09-07): TimingPowerMulRed is now the multiplier AT
        // TimingBandRedY01, not at 0 — the ramp is RE-BASED to start at the drawn red line.
        // Below that line the flick is not a weak shot, it is a DUFF, and it pays
        // MissPowerMul instead. Nothing at or above the red line changed.
        public float TimingPowerMulRed;    // multiplier at timing01 = TimingBandRedY01
        public float TimingPowerMulGold;   // multiplier at timing01 = TimingBandGoldY01

        // ── The duff (miss_grade_duff §2) ───────────────────────────────────────
        // ONE flat penalty shared by every scheme's miss grade — Pendulum MISS, Needle SHANK,
        // Free Swing DUFF and a Flick latched below TimingBandRedY01 — because "a missed swing
        // tops the ball" is a rule about this game's shot economy, not about any one scheme.
        // Deliberately NOT TimingPowerMulRed: that number keeps its own job as the bottom of the
        // Flick ramp, and a miss has to be able to move without dragging a mistimed-but-real
        // flick down with it.
        // A VELOCITY multiplier, not a distance fraction: carry is super-linear in launch speed,
        // so the number here is nowhere near the fraction of carry it produces. Measured on the
        // shipped driver (329 yd at 100%): 0.20 gave 6.0 yd (1.8%) and 0.40 gives the distance
        // recorded in miss_grade_duff's acceptance run. Cesar took 0.20 -> 0.40 after seeing it.
        public float MissPowerMul;           // flat multiplier on a DUFF (full swing)
        public float PuttMissPowerMul;       // same on the green (D3) — never so low the ball stalls
        public float MissLaunchPitchScale;   // launch pitch = club loft x this on a DUFF; putts unaffected

        /// <summary>Slab progress (0 = cone base, 1 = apex) at the RED band line of the Flick
        /// cone. At or below it the flick is a DUFF. Drawn by <c>ConeBandPalette.BandRedY01</c>
        /// and consumed by <c>ShotController.TimingPowerMultiplier</c> — the same F15 D3 pattern
        /// as the gold and green edges, so the line the player reads and the penalty they pay are
        /// one number.</summary>
        public float TimingBandRedY01;

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

        // ── Reversing the pull cancels the swing (shared by the handle-pull schemes) ──────
        // Cesar, 2026-09-07: "moving the handle up but not flicking should cancel the shot, not
        // allowing to adjust power that way." Pulling back up was a free re-roll — the gauge fell
        // while the COMMITTED power stayed at the peak, so it both lied and let the player re-time
        // the marker for free. A reversal is now a decision: flick and it fires, hold and it dies.
        // Not scheme-prefixed because it is one gesture rule the player learns once.
        public float HandleReverseCancelPx;        // upward travel from the deepest pull that arms it
        public float HandleReverseCancelHoldSec;   // held that long after arming = cancel, not a flick

        // Bot commit precision (bot_scheme_parity §3.2). A bot releases the frame the live marker
        // reaches its sampled offset; the tolerance is how close "reaches" has to be, and the
        // sweep budget is how many full passes it will wait before taking the nearest pass rather
        // than sweeping forever. Config keys and not literals because the difficulty calibration
        // in §5 is a sigma on this same offset — a tolerance loose enough to matter would show up
        // as brackets that no longer hit their target E|ErrorYaw|.
        public float PendulumBotCommitTol01;
        public float PendulumBotMaxWaitSweeps;   // treat as int at use site, as MaxTotalPasses is

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

        /// <summary>Bot tap precision — the Needle counterpart of
        /// <see cref="PendulumBotCommitTol01"/>. No sweep budget: this needle crosses ONCE, so a
        /// bot that missed its offset has already shanked and there is nothing to wait for.</summary>
        public float NeedleBotCommitTol01;

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
        // and the upstroke DURATION above which the swing is a DUFF rather than a swing.
        public float FreeSwingIdealTempo;
        public float FreeSwingTempoWindowAtCC0;
        public float FreeSwingTempoWindowAtCC120;
        public float FreeSwingDuffSeconds;

        // Power shrinks BOTH windows, from the PEAK pull and on the DRAWN bar too. Free Swing has
        // no timing widget to speed up, so this is the ONLY cost a 120% pull carries.
        public float FreeSwingWindowScaleAtZeroPower;
        public float FreeSwingWindowScaleAtMaxPower;

        // How long the analyzer chip stays up after the shot, and how many finger samples the
        // driver's OWN ring buffer keeps (never ShotController.PushTouchSample — that ring is
        // Flick's gate).
        public float FreeSwingAnalyzerSeconds;
        public float FreeSwingSampleWindow;      // treat as int at use site, as MaxTotalPasses is

        // ── Shot-view layout (shot_view_layout §3.1) ────────────────────────────
        // WHERE THE SHOT VIEW SITS, not how a shot is computed. The 3D camera pins the ball to
        // the 2D CentralBall widget (PhysicsLabController.GetAimBallViewportY -> SolveAimCameraPose),
        // so the ball anchor below IS the camera pitch: drop the widget and the camera tilts up,
        // which is the whole of "more sky, more fairway" (Figma In-Game - Shot Tests 14153:4602).
        //
        // PER SCHEME, because the schemes do not draw the same thing below the ball -- but all
        // four now share 0.38. Flick sat at 0.5 while its cone was a scene-authored 1009px, which
        // would have put the base off the bottom; flick_shot_view D1/D2 cuts the cone to
        // FlickConeHeightPx so the base lands on BottomBaselinePx and Flick joins the framing.
        public float BallAnchorViewportY_Flick;
        public float BallAnchorViewportY_Pendulum;
        public float BallAnchorViewportY_Needle;
        public float BallAnchorViewportY_FreeSwing;

        // ── Flick cone geometry (flick_shot_view D2/D3/D5) ─────────────────────
        // THE CONE IS FLICK'S LANE. Every one of these used to be a serialized number on
        // ShotConeView/ConeMeshGraphic/TimingSlabGraphic/PutterTrackGraphic, which is why the ball
        // could not move: four objects had to agree and only the scene knew the numbers. They live
        // here now, so 641 = BottomBaselinePx line - ball(0.38) - the apex gap is ONE edit and the
        // D6 clamp can read the same depth the cone is drawn at.
        //
        // The handle rest is a FRACTION, not px: the drag maps the whole cone height to power
        // (ClubHandleDragger.ProcessDrag), so an absolute rest would change what "touching the
        // club" reads as the moment the cone is re-cut.
        //
        // Its VALUE is pull parity, chosen by Cesar on 2026-09-07: 0.6818 x 792 = 540px from rest
        // to 100%, which is exactly PendulumPull100Px / FreeSwingPull100Px, so a thumb travels the
        // same distance in every scheme. Both ends of that trade are real — the same formula means
        // a shorter pull starts with more power already dialled in (32% the instant the club is
        // touched, against 22% at 0.778 and 17% on the old 1160px cone). If the cone is re-cut,
        // re-derive this as 540/newHeight or the parity silently lapses.
        //
        // FlickConeApexGapPx and FlickPutterTrackTopBelowBallPx are both 0, and that is not an
        // oversight. The cone's apex sits ON the ball —
        // how the scene has always shipped (height 1160, base at ball-1160) and what Cesar asked
        // for explicitly on 2026-09-07: "the cone's top point should reach the ball, not leave
        // empty space". The putt track follows the same rule by the same instruction, and there the
        // live runtime had ALREADY been snapping the top onto the ball
        // (PhysicsLabController.AlignPutterTrackToBall) — so 0 is what the game does, now said out
        // loud. Both keys exist so the gap is a number somebody can see and change rather than an
        // accident of one anchoredPosition.
        public float FlickConeHeightPx;
        public float FlickHandleStartY01;
        public float FlickPutterTrackHeightPx;
        public float FlickPutterTrackTopBelowBallPx;
        public float FlickConeApexGapPx;

        // ── Flick pull thresholds (flick_pull_mapping D1/D2) ───────────────────
        // FINGER TRAVEL FROM THE CLUB'S REST, exactly as the other three schemes measure it, and
        // the reason FlickHandleStartY01 above is what it is. Flick used to read power off the
        // cone BASE (power = 1 - handleY/height), so the club read 31.8% the instant it was
        // touched and there was no 120% at all -- the handle ran out of cone at 1.0. Cesar,
        // 2026-09-07: "way too high".
        //
        // These two ARE Pendulum's and Free Swing's numbers (540 / 648), so a thumb travels the
        // same distance in all four schemes, and their own keys so a Flick retune cannot move an
        // A/B partner. MinUsefulPullPx (40) is REUSED as the dead zone rather than copied a fifth
        // time -- the same thumb slop in either path.
        //
        // THE INVARIANT THAT TIES THEM TO THE CONE: FlickHandleStartY01 x FlickConeHeightPx ==
        // FlickPull120Px (0.8182 x 792 = 648.0). That is what makes the BASE 120% and the club
        // rest 648px above it. Change one of the three and the other two must move with it;
        // ControlsConfigTests asserts the identity so they cannot drift apart silently.
        public float FlickPull100Px;
        public float FlickPull120Px;

        // ONE bottom baseline shared by the action buttons' bottom edge, both selector overlays
        // and the pull lane's end, so they cannot drift apart the way 96 (buttons) and the lane's
        // derived end already had. Raised at runtime to safeAreaBottom + 60 on a device whose
        // inset is deeper, which is what keeps the 120% flick clear of the home gesture.
        public float BottomBaselinePx;

        // Centre of the round power gauge, 0 = bottom. It used to sit at 0.50 — dead on the aim
        // bar's row — so a full-power pull was read through the widget covering it.
        public float PowerGaugeViewportY;

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
            BaseArrowSpeedHzAtCC0          = 1.0f,     // F17 (flick_arrow_speed_retune): 2.0 → 1.0 (mirror controls.csv); starter Commons (CC 6–7) ran at ~1.8 Hz, too fast to time. F13: 3.0 → 2.0
            ArrowSpeedHzPerCC              = -0.012f,  // F17: −0.03 → −0.012 (mirror controls.csv); moves as a PAIR with the base — keeps F13's 2.5× ladder shape, CC 0–50 spans 1.0→0.4 Hz. F13: −0.05 → −0.03
            MinArrowSpeedHz                = 0.4f,     // F17: 0.5 → 0.4 = the new calibrated CC-50 speed; no-op on reachable CC 0–50, guards CC > 83.3 where the raw line goes negative. F13: introduced at 0.5 (guard was CC > 66.7)
            MaxCleanPassesAtCC0            = 1f,
            CleanPassesPerCC               = 0.08f,    // Order 732: 0.04 → 0.08 (mirror controls.csv); CC 0–50 → 1–5 clean passes
            MaxTotalPasses                 = 10f,
            DegradationYawDegPerPass       = 2f,
            TimingBandGoldY01              = 0.45f,   // F15: was ConeBandPalette.BandGoldY01 (same value)
            TimingBandGreenY01             = 0.85f,   // F15: was ConeBandPalette.BandGreenY01 (same value)
            TimingPowerMulRed              = 0.70f,   // F15: flick ON THE RED LINE = 70% power (re-based by miss_grade_duff)
            TimingPowerMulGold             = 0.90f,   // F15: flick on the gold line = 90% power
            TimingBandRedY01               = 0.15f,   // miss_grade_duff: below this the flick is a DUFF
            MissPowerMul                   = 0.40f,   // miss_grade_duff: 0.20 -> 0.40 (Cesar, 2026-09-07) after seeing the 6 yd duff
            PuttMissPowerMul               = 0.30f,   // miss_grade_duff D3
            MissLaunchPitchScale           = 0.35f,   // miss_grade_duff: a topped ball flies low and flat
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
            PendulumPull100Px              = 540f,   // 300 -> 380 (2026-09-05): the longer pill needs the
                                                     // ticks LOW in it, and a tick only moves down honestly
                                                     // if the pull it represents gets longer.
                                                     // 380 -> 540 (shot_view_layout): with the ball at 0.38
                                                     // the lane has 160px more room, and the lane END is what
                                                     // has to land on BottomBaselinePx
            PendulumPull120Px              = 648f,   // 360 -> 456 -> 648, keeping the node's 1.2x tick spacing
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
            HandleReverseCancelPx          = 60f,
            HandleReverseCancelHoldSec     = 0.12f,
            PendulumBotCommitTol01         = 0.03f,  // bot_scheme_parity §3.2
            PendulumBotMaxWaitSweeps       = 2f,

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
            NeedleBotCommitTol01         = 0.03f,  // bot_scheme_parity §3.2

            // scheme_freeswing §3.5 seed values — mirror controls.csv (F13 two-mirror rule).
            FreeSwingMinUsefulPullPx        = 40f,
            FreeSwingPull100Px              = 540f,  // seeded equal to PENDULUM: the two lane schemes
            FreeSwingPull120Px              = 648f,  // share a lane end on the baseline (shot_view_layout
                                                     // D4). Needle keeps 380/456 -- its pull is a RING
                                                     // around the ball, not a lane, and 648 would clip
                                                     // off both sides of a 1170-wide canvas (D5)
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
            FreeSwingDuffSeconds            = 0.50f, // SECONDS, not px/s — see controls.csv
            FreeSwingWindowScaleAtZeroPower = 1.35f,
            FreeSwingWindowScaleAtMaxPower  = 0.55f,
            FreeSwingAnalyzerSeconds        = 1.5f,
            FreeSwingSampleWindow           = 90f,

            // shot_view_layout §3.1 seed values — mirror controls.csv (F13 two-mirror rule).
            BallAnchorViewportY_Flick       = 0.38f,  // flick_shot_view D1: 0.5 -> 0.38, the cone
                                                      // was cut to 641 so the base still fits
            BallAnchorViewportY_Pendulum    = 0.38f,  // 62% from the top, the Figma ball position
            BallAnchorViewportY_Needle      = 0.38f,
            BallAnchorViewportY_FreeSwing   = 0.38f,

            // flick_shot_view §3.1 seed values -- mirror controls.csv (F13 two-mirror rule).
            FlickConeHeightPx               = 792f,   // 1096 - 304 - 0, base on the baseline
            FlickHandleStartY01             = 0.8182f,// flick_pull_mapping D2: 0.6818 -> 0.8182
                                                      // = 648/792, so the BASE is 120% and the
                                                      // club rests 648px above it (0% at touch)
            FlickPutterTrackHeightPx        = 792f,   // top ON the ball, bottom on 1096
            FlickPutterTrackTopBelowBallPx  = 0f,     // as the live runtime already placed it
            FlickConeApexGapPx              = 0f,     // the apex sits ON the ball, as it shipped
            FlickPull100Px                  = 540f,   // = PendulumPull100Px / FreeSwingPull100Px
            FlickPull120Px                  = 648f,   // = 1.2 x FlickPull100Px = the cone's base
            BottomBaselinePx                = 170f,   // was 96 on the buttons alone
            PowerGaugeViewportY             = 0.70f,  // 30% from the top, clear of the aim bar
        };
    }
}
