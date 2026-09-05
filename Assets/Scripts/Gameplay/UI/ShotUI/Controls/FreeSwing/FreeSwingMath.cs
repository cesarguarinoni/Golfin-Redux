using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>The pop-worthy outcomes of one Free Swing (scheme_freeswing §3.4). Most swings are
    /// <see cref="None"/> — the analyzer chip is always shown, the POP is for the exceptional
    /// cases only.</summary>
    public enum FreeSwingGrade
    {
        /// <summary>Chip only. A perfectly ordinary swing does not deserve a word over the ball.</summary>
        None  = 0,
        /// <summary>Clean impact AND good tempo.</summary>
        Pure  = 1,
        /// <summary>The upswing was too slow to be a swing at all.</summary>
        Duff  = 2,
        /// <summary>Crossed the impact line well LEFT of the club's rest line — the ball goes left.</summary>
        Hook  = 3,
        /// <summary>Crossed well RIGHT — the ball goes right.</summary>
        Slice = 4,
    }

    /// <summary>What the upstroke's shape did to the ball. Reported on the chip's PATH column.</summary>
    public enum FreeSwingPath { Straight = 0, Draw = 1, Fade = 2 }

    /// <summary>What the down:up time ratio did to the power. Reported on the chip's TEMPO column.</summary>
    public enum FreeSwingTempo { Good = 0, Fast = 1, Slow = 2 }

    /// <summary>
    /// Everything the Free Swing scheme decides, as pure functions of numbers.
    ///
    /// <para>STATIC AND MonoBehaviour-FREE, for the third time and the same reason
    /// <c>PendulumMath</c> and <c>NeedleMath</c> are: the verdict is the whole scheme — it is what
    /// turns one continuous drag into a <c>ShotIntent</c> — so it has to be testable without a
    /// scene, a canvas, an EventSystem or a play-mode frame. <see cref="FreeSwingSchemeDriver"/>
    /// is then only wiring: sample the finger, call in here, hand the answer to
    /// <c>ShotController.CommitExternal</c>. This scheme reads FOUR things off one gesture where
    /// the other two read one, so the split matters more here, not less.</para>
    ///
    /// <para>Every knob arrives as a <see cref="ControlsConfig"/>, never read from a static, so a
    /// test can drive the whole table without touching the shipped tuning. Every key is a
    /// <c>FreeSwing*</c> of its own — carry-over 1: nothing is shared with Flick's arrow,
    /// Pendulum's Hz or Needle's sweep, because the three are being A/B'd and a retune of one
    /// must never move another.</para>
    /// </summary>
    public static class FreeSwingMath
    {
        // ── Localisation KEYS — never literals ──────────────────────────────────
        // Published by the two-way content importer. The grade keys HOOK/SLICE already existed
        // (Needle uses the same two words for the same two misses); PURE and DUFF are new.
        public const string KeyPure  = "SHOT_GRADE_PURE";
        public const string KeyDuff  = "SHOT_GRADE_DUFF";
        public const string KeyHook  = "SHOT_GRADE_HOOK";
        public const string KeySlice = "SHOT_GRADE_SLICE";

        /// <summary>The analyzer chip's four column labels.</summary>
        public const string KeyPower  = "SWING_POWER";
        public const string KeyImpact = "SWING_IMPACT";
        public const string KeyPath   = "SWING_PATH";
        public const string KeyTempo  = "SWING_TEMPO";

        /// <summary>The chip's PATH and TEMPO values. Words, so keys.</summary>
        public const string KeyPathStraight = "SWING_PATH_STRAIGHT";
        public const string KeyPathDraw     = "SWING_PATH_DRAW";
        public const string KeyPathFade     = "SWING_PATH_FADE";
        public const string KeyTempoGood    = "SWING_TEMPO_GOOD";
        public const string KeyTempoFast    = "SWING_TEMPO_FAST";
        public const string KeyTempoSlow    = "SWING_TEMPO_SLOW";

        /// <summary>The word beside the impact line in the lane.</summary>
        public const string KeyImpactLine = "SWING_IMPACT_LINE";

        /// <summary>
        /// The chip's two NUMERIC formats. Not localisation keys, and deliberately not
        /// <c>.text</c> literals either: a number is formatted, not translated, and the unit and
        /// the arrowheads are GLYPHS in a format constant so the fidelity linter's
        /// unlocalized-text check has nothing on a view to flag. <c>◀</c>/<c>▶</c> point the way
        /// the club head crossed, which is the way the ball goes.
        /// </summary>
        public const string PowerFormat      = "{0:0}%";
        public const string ImpactFormat     = "{0} {1:0} px";
        public const string ImpactZeroFormat = "{0:0} px";
        public const string ArrowLeft        = "◀";
        public const string ArrowRight       = "▶";

        public static string GradeKey(FreeSwingGrade g) => g switch
        {
            FreeSwingGrade.Pure  => KeyPure,
            FreeSwingGrade.Duff  => KeyDuff,
            FreeSwingGrade.Hook  => KeyHook,
            FreeSwingGrade.Slice => KeySlice,
            _                    => null,
        };

        public static string PathKey(FreeSwingPath p) => p switch
        {
            FreeSwingPath.Draw => KeyPathDraw,
            FreeSwingPath.Fade => KeyPathFade,
            _                  => KeyPathStraight,
        };

        public static string TempoKey(FreeSwingTempo t) => t switch
        {
            FreeSwingTempo.Fast => KeyTempoFast,
            FreeSwingTempo.Slow => KeyTempoSlow,
            _                   => KeyTempoGood,
        };

        /// <summary>
        /// Longest step any time-based measure will accept from one frame (carry-over 9).
        ///
        /// <para>A hitch is not the player's fault, and this scheme measures TWO things in
        /// seconds — the down:up tempo ratio and the upstroke speed that decides a DUFF. An
        /// unclamped 0.4 s frame in the middle of an upswing would report a swing three times
        /// slower than the thumb actually moved and turn a good shot into a duff. The cost of the
        /// clamp is that a genuinely stalled finger reads slightly quicker than it was, which is
        /// the failure that hands the player a shot rather than taking one away.</para>
        /// </summary>
        public const float MaxStepSeconds = 1f / 30f;

        // ── Power ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Pull distance (canvas px, straight down from the touch origin) → power 0..1.2.
        ///
        /// <para>The same SHAPE as <c>ShotController.ComputePower</c>, <c>PendulumMath.Power</c>
        /// and <c>NeedleMath.Power</c> — dead zone, linear to 100%, then a 0.2-wide overpower
        /// ramp — against this scheme's own thresholds, with the putt cap applied here rather
        /// than inferred: a putt's lane draws no 120% tick, so there is nothing on screen to pull
        /// past.</para>
        ///
        /// <para>Read from the PEAK pull, not the live one, everywhere it is used. In this scheme
        /// the finger comes back UP through the whole gesture, so a live reading would fall to
        /// zero on the way to the shot.</para>
        /// </summary>
        public static float Power(float pullPx, in ControlsConfig cfg, bool isPutt)
        {
            float minPull = cfg.FreeSwingMinUsefulPullPx;
            float p100    = cfg.FreeSwingPull100Px;
            float p120    = cfg.FreeSwingPull120Px;

            if (pullPx < minPull) return 0f;

            float span = Mathf.Max(p100 - minPull, 1e-3f);
            if (pullPx <= p100) return Mathf.Clamp01((pullPx - minPull) / span);

            if (isPutt) return 1f;

            float overRange = Mathf.Max(p120 - p100, 1e-3f);
            return Mathf.Min(1f + ((pullPx - p100) / overRange) * 0.2f,
                             Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
        }

        // ── The power shrink (carry-over 2) ─────────────────────────────────────

        /// <summary>
        /// How much every window closes for the power being asked for.
        ///
        /// <para>The scheme's RISK/REWARD, and the reason a 120% pull is a decision rather than a
        /// free upgrade. Free Swing has no timing widget to speed up, so this shrink is the ONLY
        /// cost overpower carries — which makes it the load-bearing number, not a garnish. It
        /// scales the impact window AND the tempo window, and the drawn impact window reads the
        /// same call from the PEAK pull every drag frame, so the green bar visibly closes while
        /// the player is still pulling and the target they watched close is the one they are
        /// graded against.</para>
        /// </summary>
        public static float WindowScaleForPower(float power, in ControlsConfig cfg)
        {
            float t = Mathf.Clamp01(power / Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
            return Mathf.Max(0.05f, Mathf.Lerp(cfg.FreeSwingWindowScaleAtZeroPower,
                                               cfg.FreeSwingWindowScaleAtMaxPower, t));
        }

        // ── Impact ──────────────────────────────────────────────────────────────

        /// <summary>
        /// HALF the clean-impact window, in canvas px either side of the lane centre: Club
        /// Accuracy sets it, power shrinks it.
        ///
        /// <para>Accuracy's job in this scheme is lateral tolerance at the impact line — the same
        /// "error tolerance" job it does as the cone half-angle in Flick and as timing tolerance
        /// in the other two. HALF-width because that is the number a verdict compares
        /// <c>|xI|</c> against; the drawn bar is twice it, and <c>FreeSwingLaneView</c> does that
        /// doubling in one place so "inside the green" and "graded clean" are the same
        /// statement about the same geometry.</para>
        /// </summary>
        public static float ImpactWindowPx(float clubAccuracyNorm01, float power, in ControlsConfig cfg)
            => Mathf.Lerp(cfg.FreeSwingImpactWindowAtAcc0Px,
                          cfg.FreeSwingImpactWindowAtAcc120Px,
                          Mathf.Clamp01(clubAccuracyNorm01)) * WindowScaleForPower(power, cfg);

        /// <summary>
        /// Aim error from WHERE the club head crossed the impact line, in radians.
        ///
        /// <para>SIGN CONVENTION: <paramref name="impactPx"/> is the club head's lateral offset
        /// from the lane centre at the crossing, positive to the player's RIGHT, and a positive
        /// return sends the ball right. That follows from <c>ShotController.AimYawFor</c> —
        /// <c>CameraHeading + finetune × halfCone</c> — which <c>ShotAimParityTests</c> pins as
        /// the single source of truth for where the ball goes. So crossing LEFT of centre
        /// (<c>impactPx &lt; 0</c>) is a negative yaw, a ball left, and a <b>HOOK</b>; crossing
        /// right is a <b>SLICE</b>. Identical to <c>NeedleMath.Grade</c>'s reading of its needle
        /// offset, deliberately: the two schemes must not disagree about which word means which
        /// direction.</para>
        ///
        /// <para>Inside the window the shot is dead straight. Past it the bend ramps with the
        /// miss itself — a hair outside barely bends, the edge of the miss range bends a full
        /// cone half-angle — and past <c>FreeSwingImpactMissPx</c> it is thrown a flat
        /// <c>MissYawGain</c> × half-cone. Flat rather than ramped for the reason Needle's is:
        /// a linear ramp to the edge of the screen would land every truly wild swing in the same
        /// place, which reads as scripted rather than as bad.</para>
        /// </summary>
        public static float ImpactYawRad(float impactPx, float clubAccuracyNorm01, float power,
                                         float halfConeRad, in ControlsConfig cfg)
        {
            float window = ImpactWindowPx(clubAccuracyNorm01, power, cfg);
            float a      = Mathf.Abs(impactPx);
            if (a <= window) return 0f;

            float miss = Mathf.Max(cfg.FreeSwingImpactMissPx, 1e-3f);
            if (a <= miss)
                return (impactPx / miss) * halfConeRad * cfg.FreeSwingYawGain;

            return Mathf.Sign(impactPx) * halfConeRad * cfg.FreeSwingMissYawGain;
        }

        // ── Path (the upstroke's shape) ─────────────────────────────────────────

        /// <summary>
        /// How bowed the upstroke was, in degrees. Positive = bowed to the player's RIGHT.
        ///
        /// <para>THE PATH IS THE CURVE, WHICH IS WHY THE FADE/DRAW TOGGLE IS HIDDEN IN THIS
        /// SCHEME (decision 3). It is measured as the MEAN SIGNED LATERAL OFFSET of the upstroke
        /// samples from the straight line reversal → crossing, turned into an angle by
        /// <c>atan2(meanOffset, upstrokeLength)</c>. An angle, and not raw pixels, because the
        /// same 20px bow means something very different on a 90px flick and on a 500px full
        /// swing; dividing by the stroke's own length is what makes a lay-up and a driver ask for
        /// the same GESTURE rather than the same displacement.</para>
        ///
        /// <para>Offsets from the CHORD, not from vertical: a swing that drifts diagonally but
        /// travels dead straight has zero bow and must not curve. Only the bend away from its own
        /// line counts.</para>
        /// </summary>
        /// <param name="upstroke">The samples strictly between the reversal and the crossing.
        /// Fewer than one leaves nothing to bow, and the answer is 0.</param>
        public static float PathDeg(Vector2 reversal, Vector2 crossing, IReadOnlyList<Vector2> upstroke)
        {
            if (upstroke == null || upstroke.Count == 0) return 0f;

            Vector2 chord = crossing - reversal;
            float   len   = chord.magnitude;
            if (len < 1e-3f) return 0f;

            Vector2 dir = chord / len;
            // Right-hand normal of the direction of travel: rotate dir by -90 degrees. With
            // dir = (0,1) (straight up the screen) this is (1,0), i.e. the player's right — so a
            // positive offset is a bow to the right, which is what the doc comment promises.
            float sum = 0f;
            for (int i = 0; i < upstroke.Count; i++)
            {
                Vector2 v = upstroke[i] - reversal;
                sum += v.x * dir.y - v.y * dir.x;
            }

            float mean = sum / upstroke.Count;
            return Mathf.Atan2(mean, len) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How much bow is ignored, in degrees: Club Control buys tolerance for a shaky thumb.
        ///
        /// <para>A DEAD ZONE AND NOT A SCALE. Nobody drags a perfectly straight line on glass, so
        /// without one every swing would curve a little and "STRAIGHT" would be unreachable —
        /// which is the difference between a scheme that reads the player's intent and one that
        /// reads their tremor. Club Control WIDENS it (a steadier golfer's small wobble is not a
        /// shot shape), which is the opposite direction from the accuracy windows and is the
        /// point: here the stat buys forgiveness of noise, not precision of aim.</para>
        /// </summary>
        public static float PathDeadzoneDeg(float clubControlNorm01, in ControlsConfig cfg)
            => Mathf.Lerp(cfg.FreeSwingPathDeadzoneAtCC0Deg,
                          cfg.FreeSwingPathDeadzoneAtCC120Deg,
                          Mathf.Clamp01(clubControlNorm01));

        /// <summary>
        /// Bow angle → the <c>fadeDraw01</c> the shot pipeline curves the ball with, −1..+1.
        ///
        /// <para>SIGN: <c>+1</c> is the value the flick's handle produces at full RIGHT deflection
        /// (<c>render_fadedraw_curve_overlay.py</c>: <c>fadeDrawInput = +1</c> is a FADE, −1 a
        /// DRAW), so a path bowed right fades and a path bowed left draws. Pinned rather than
        /// guessed because the number goes straight into <c>ShotInputBuilder</c>'s spin tilt,
        /// where a flipped sign would curve every shot the wrong way while every test that only
        /// checked magnitude stayed green.</para>
        ///
        /// <para>PUTTS NEVER CURVE (decision 7) — and it is returned as 0 HERE rather than left
        /// to <c>CommitExternal</c>'s putt clamp, so the chip reads STRAIGHT on a putt instead of
        /// promising a shape the ball will not take.</para>
        /// </summary>
        public static float FadeDraw01(float pathDeg, float clubControlNorm01, bool isPutt,
                                       in ControlsConfig cfg)
        {
            if (isPutt) return 0f;

            float dead = PathDeadzoneDeg(clubControlNorm01, cfg);
            float a    = Mathf.Abs(pathDeg);
            if (a <= dead) return 0f;

            float span = Mathf.Max(cfg.FreeSwingPathFullDeg - dead, 1e-3f);
            return Mathf.Clamp(Mathf.Sign(pathDeg) * (a - dead) / span, -1f, 1f);
        }

        /// <summary>The word the chip's PATH column shows — the same call
        /// <see cref="FadeDraw01"/> makes, so the word and the curve can never disagree.</summary>
        public static FreeSwingPath PathFor(float pathDeg, float clubControlNorm01, bool isPutt,
                                            in ControlsConfig cfg)
        {
            float fd = FadeDraw01(pathDeg, clubControlNorm01, isPutt, cfg);
            if (Mathf.Approximately(fd, 0f)) return FreeSwingPath.Straight;
            return fd > 0f ? FreeSwingPath.Fade : FreeSwingPath.Draw;
        }

        // ── Tempo ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The gesture's rhythm as one number: upswing seconds ÷ backswing seconds.
        ///
        /// <para>A RATIO, NOT A DURATION, so a deliberate slow swing and a quick one are graded
        /// on the same thing they would be graded on at a driving range: the RELATIONSHIP between
        /// the two halves. <c>FreeSwingIdealTempo</c> = 0.5 asks for an upswing half as long as
        /// the backswing.</para>
        /// </summary>
        public static float TempoRatio(float backSeconds, float upSeconds)
            => upSeconds / Mathf.Max(backSeconds, 1e-3f);

        /// <summary>How far off the ideal the rhythm was. Unsigned — <see cref="TempoFor"/>
        /// re-reads the sign to pick the word.</summary>
        public static float TempoError(float tempoRatio, in ControlsConfig cfg)
            => Mathf.Abs(tempoRatio - cfg.FreeSwingIdealTempo);

        /// <summary>The tempo tolerance: Club Control widens it, power shrinks it through the
        /// same <see cref="WindowScaleForPower"/> the impact window uses, so a 120% pull narrows
        /// BOTH halves of the swing at once.</summary>
        public static float TempoWindow(float clubControlNorm01, float power, in ControlsConfig cfg)
            => Mathf.Max(1e-3f,
                   Mathf.Lerp(cfg.FreeSwingTempoWindowAtCC0,
                              cfg.FreeSwingTempoWindowAtCC120,
                              Mathf.Clamp01(clubControlNorm01)) * WindowScaleForPower(power, cfg));

        /// <summary>
        /// Tempo error → the power multiplier. Inside the window costs nothing; one window past
        /// it ramps down to <c>TimingPowerMulGold</c>; beyond that it is a flat
        /// <c>TimingPowerMulRed</c>.
        ///
        /// <para>The two multipliers are SHARED with Flick and the other two schemes on purpose:
        /// "a mistimed shot is worth 90%, a badly mistimed one 70%" is a rule about this game's
        /// shot economy, not about any one control scheme, and four copies would be four places
        /// to keep equal by hand.</para>
        /// </summary>
        public static float TempoMul(float tempoError, float tempoWindow, in ControlsConfig cfg)
        {
            float w = Mathf.Max(tempoWindow, 1e-4f);
            if (tempoError <= w) return 1f;
            if (tempoError <= 2f * w)
                return Mathf.Lerp(1f, cfg.TimingPowerMulGold, (tempoError - w) / w);
            return cfg.TimingPowerMulRed;
        }

        /// <summary>The 0..1 tempo score stamped on the telemetry row as <c>timing01</c> — the
        /// same slot Flick's latch position and Needle's needle offset fill, so one dashboard
        /// column compares all four schemes.</summary>
        public static float Timing01(float tempoError, float tempoWindow)
            => 1f - Mathf.Clamp01(tempoError / Mathf.Max(2f * tempoWindow, 1e-4f));

        /// <summary>The word the chip's TEMPO column shows. GOOD inside the window; outside it
        /// the SIGN picks the word — a ratio below the ideal means the upswing was quick relative
        /// to the backswing, i.e. FAST.</summary>
        public static FreeSwingTempo TempoFor(float tempoRatio, float tempoError, float tempoWindow,
                                              in ControlsConfig cfg)
        {
            if (tempoError <= tempoWindow) return FreeSwingTempo.Good;
            return tempoRatio < cfg.FreeSwingIdealTempo ? FreeSwingTempo.Fast : FreeSwingTempo.Slow;
        }

        // ── Speed / the duff ────────────────────────────────────────────────────

        /// <summary>Upstroke speed in canvas px per second: the path's own length over its own
        /// (dt-clamped) duration. Length along the PATH rather than the straight-line distance,
        /// so a long bowed upswing is correctly not a duff.</summary>
        public static float UpSpeed(float upstrokeLengthPx, float upSeconds)
            => upstrokeLengthPx / Mathf.Max(upSeconds, 1e-3f);

        // ── The verdict ─────────────────────────────────────────────────────────

        /// <summary>Everything one swing produced: what the pipeline needs, and what the chip
        /// shows. One struct so the two can never be computed from different numbers.</summary>
        public readonly struct Verdict
        {
            public readonly FreeSwingGrade Grade;
            public readonly FreeSwingPath  Path;
            public readonly FreeSwingTempo Tempo;

            /// <summary>Radians of aim error, added where the flick's per-pass degradation yaw is.
            /// Positive = the ball's RIGHT (see <see cref="ImpactYawRad"/>).</summary>
            public readonly float ErrorYawRad;
            public readonly float TimingMul;
            public readonly float Timing01;
            public readonly float FadeDraw01;

            // ── What the chip reads, kept because the chip must show the GRADED numbers ──
            public readonly float PowerNormalized;
            public readonly float ImpactPx;
            public readonly float ImpactWindowPx;
            public readonly float PathDeg;
            public readonly float TempoRatio;
            public readonly float TempoError;
            public readonly float TempoWindow;
            public readonly float UpSpeedPxPerSec;

            public Verdict(FreeSwingGrade grade, FreeSwingPath path, FreeSwingTempo tempo,
                           float errorYawRad, float timingMul, float timing01, float fadeDraw01,
                           float powerNormalized, float impactPx, float impactWindowPx,
                           float pathDeg, float tempoRatio, float tempoError, float tempoWindow,
                           float upSpeedPxPerSec)
            {
                Grade = grade; Path = path; Tempo = tempo;
                ErrorYawRad = errorYawRad; TimingMul = timingMul; Timing01 = timing01;
                FadeDraw01 = fadeDraw01;
                PowerNormalized = powerNormalized;
                ImpactPx = impactPx; ImpactWindowPx = impactWindowPx;
                PathDeg = pathDeg;
                TempoRatio = tempoRatio; TempoError = tempoError; TempoWindow = tempoWindow;
                UpSpeedPxPerSec = upSpeedPxPerSec;
            }

            /// <summary>True when the club head crossed inside the drawn green window.</summary>
            public bool ImpactClean => Mathf.Abs(ImpactPx) <= ImpactWindowPx;
        }

        /// <summary>
        /// Grade one swing from the four things the gesture measured.
        ///
        /// <para>PRECEDENCE IS DUFF &gt; HOOK/SLICE &gt; PURE &gt; none, and it is an ordering of
        /// what the player most needs told. A duff is a swing that never happened, so nothing
        /// else about it is worth reading; a wild impact is the next most legible failure; PURE
        /// is the only positive, and it costs BOTH a clean impact and a good tempo, which is what
        /// stops it appearing on every second shot. Everything else gets the chip and no word —
        /// an ordinary swing does not need a banner.</para>
        ///
        /// <para>A DUFF DOUBLES THE IMPACT YAW rather than replacing it, and clamps at the same
        /// ceiling a big miss uses. Doubling keeps the mishit pointing the way the club actually
        /// crossed — a duffed hook still goes left — and the clamp stops the doubling from
        /// throwing it somewhere no miss in this scheme can reach.</para>
        /// </summary>
        public static Verdict Grade(float impactPx, float pathDeg, float tempoRatio,
                                    float upSpeedPxPerSec, float power,
                                    float clubAccuracyNorm01, float clubControlNorm01,
                                    float halfConeRad, bool isPutt, in ControlsConfig cfg)
        {
            float window = ImpactWindowPx(clubAccuracyNorm01, power, cfg);
            float w      = TempoWindow(clubControlNorm01, power, cfg);
            float e      = TempoError(tempoRatio, cfg);
            float yaw    = ImpactYawRad(impactPx, clubAccuracyNorm01, power, halfConeRad, cfg);

            // The DUFF exit. Checked before anything else is decided, because a duff overrides
            // the path (a swing that slow shaped nothing) and the tempo multiplier.
            if (upSpeedPxPerSec < cfg.FreeSwingDuffSpeedPxPerSec)
            {
                float cap = Mathf.Abs(halfConeRad * cfg.FreeSwingMissYawGain);
                return new Verdict(FreeSwingGrade.Duff, FreeSwingPath.Straight,
                                   TempoFor(tempoRatio, e, w, cfg),
                                   Mathf.Clamp(2f * yaw, -cap, cap),
                                   cfg.TimingPowerMulRed, 0f, 0f,
                                   power, impactPx, window, pathDeg, tempoRatio, e, w,
                                   upSpeedPxPerSec);
            }

            var  path  = PathFor(pathDeg, clubControlNorm01, isPutt, cfg);
            var  tempo = TempoFor(tempoRatio, e, w, cfg);
            float fd   = FadeDraw01(pathDeg, clubControlNorm01, isPutt, cfg);
            float mul  = TempoMul(e, w, cfg);
            float t01  = Timing01(e, w);

            bool clean = Mathf.Abs(impactPx) <= window;

            FreeSwingGrade grade;
            if (Mathf.Abs(impactPx) > cfg.FreeSwingImpactMissPx)
                grade = impactPx < 0f ? FreeSwingGrade.Hook : FreeSwingGrade.Slice;
            else if (clean && e <= w)
                grade = FreeSwingGrade.Pure;
            else
                grade = FreeSwingGrade.None;

            return new Verdict(grade, path, tempo, yaw, mul, t01, fd,
                               power, impactPx, window, pathDeg, tempoRatio, e, w, upSpeedPxPerSec);
        }
    }
}
