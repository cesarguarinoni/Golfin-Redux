using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>How well the player timed the tap (scheme_needle §1.4).</summary>
    public enum NeedleGrade
    {
        Perfect = 0,
        /// <summary>Tapped EARLY — the needle was still left of the top. The ball goes left.</summary>
        Hook    = 1,
        /// <summary>Tapped LATE — the needle was already right of the top. The ball goes right.</summary>
        Slice   = 2,
        /// <summary>Never tapped: the needle ran off the right end of the arc.</summary>
        Shank   = 3,
    }

    /// <summary>
    /// Everything the Needle ("Tap Timing") scheme decides, as pure functions of numbers.
    ///
    /// <para>STATIC AND MonoBehaviour-FREE, for the same reason <c>PendulumMath</c> is: the grade
    /// is the whole scheme — it is what turns a finger into a <c>ShotIntent</c> — so it has to be
    /// testable without a scene, a canvas, an EventSystem or a play-mode frame.
    /// <see cref="NeedleSchemeDriver"/> is then only wiring.</para>
    ///
    /// <para>Every knob arrives as a <see cref="ControlsConfig"/>, never read from a static, so a
    /// test can drive the whole table without touching the shipped tuning.</para>
    /// </summary>
    public static class NeedleMath
    {
        /// <summary>Localisation KEYS — never literals. Published by the two-way content importer.</summary>
        public const string KeyPerfect = "SHOT_GRADE_PERFECT";
        public const string KeyHook    = "SHOT_GRADE_HOOK";
        public const string KeySlice   = "SHOT_GRADE_SLICE";
        public const string KeyShank   = "SHOT_GRADE_SHANK";
        /// <summary>The "TAP!" prompt under the arc. Also a key, for the same reason.</summary>
        public const string KeyTapHint = "SHOT_TAP_HINT";

        public static string GradeKey(NeedleGrade g) => g switch
        {
            NeedleGrade.Perfect => KeyPerfect,
            NeedleGrade.Hook    => KeyHook,
            NeedleGrade.Slice   => KeySlice,
            _                   => KeyShank,
        };

        /// <summary>
        /// The arc's angular half-sweep, in degrees. The needle travels from −1 (the arc's left
        /// end) to +1 (its right end), so <c>n</c> maps to <c>n × 90°</c> of rotation off the top
        /// — which is what lets every window in this file be written as a fraction of 90° and
        /// drawn, unconverted, as an angular zone width.
        /// </summary>
        public const float ArcHalfSweepDeg = 90f;

        // ── Power ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Pull distance (canvas px, straight down from the touch origin) → power 0..1.2.
        ///
        /// <para>The same SHAPE as <c>ShotController.ComputePower</c> and <c>PendulumMath.Power</c>
        /// — dead zone, linear to 100%, then a 0.2-wide overpower ramp — against this scheme's own
        /// thresholds, with the putt cap applied here rather than inferred: a putt draws no 120%
        /// ring and no overpower crescent, so there is nothing on screen to pull past.</para>
        /// </summary>
        public static float Power(float pullPx, in ControlsConfig cfg, bool isPutt)
        {
            float minPull = cfg.NeedleMinUsefulPullPx;
            float p100    = cfg.NeedlePull100Px;
            float p120    = cfg.NeedlePull120Px;

            if (pullPx < minPull) return 0f;

            float span = Mathf.Max(p100 - minPull, 1e-3f);
            if (pullPx <= p100) return Mathf.Clamp01((pullPx - minPull) / span);

            if (isPutt) return 1f;

            float overRange = Mathf.Max(p120 - p100, 1e-3f);
            return Mathf.Min(1f + ((pullPx - p100) / overRange) * 0.2f,
                             Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
        }

        // ── Needle speed ────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds for the needle to cross the arc ONCE, left end to right end.
        ///
        /// <para>SECONDS, NOT Hz, AND ITS OWN LINE. Seconds because this needle makes exactly one
        /// pass and then the swing is over, so the number the player experiences is "how long do I
        /// have to react" — stating it that way is what makes Cesar's "trackable by eye" an
        /// assertion (<c>SweepSeconds(cc=0) ≥ 1.0</c>) rather than an opinion. Its own line because
        /// the Pendulum's Hz already had to be halved off the flick's arrow, and a shared number
        /// means one scheme cannot be retuned without moving the other two — which is exactly what
        /// the A/B must never do. <c>PuttArrowSpeedMultiplier</c> IS shared: "a putt's timing
        /// element is slower than a swing's" is a rule about putting, not about any one scheme.</para>
        ///
        /// <para>Overpower is the scheme's one speed-up: pulling past 100% shortens the sweep, and
        /// Character Strength (as overpower forgiveness) buys most of it back. That is the risk a
        /// 120% pull carries — it costs TIME, and (through
        /// <see cref="WindowScaleForPower"/>) target width, but never accuracy directly.</para>
        /// </summary>
        public static float SweepSeconds(float clubControl, float power, float overpowerForgiveness01,
                                         bool isPutt, in ControlsConfig cfg)
        {
            float cc      = Mathf.Clamp(clubControl, 0f, 100f);
            float baseSec = Mathf.Max(cfg.NeedleSweepSecAtCC0 + cc * cfg.NeedleSweepSecPerCC,
                                      cfg.NeedleMinSweepSec);

            // Putts never overpower, so they never pay the speed-up either — they only take the
            // shared putt slowdown. Dividing by a multiplier < 1 LENGTHENS the sweep.
            if (isPutt) return baseSec / Mathf.Max(cfg.PuttArrowSpeedMultiplier, 1e-3f);

            float over    = Mathf.Max(0f, power - 1f);
            float forgive = Mathf.Clamp01(overpowerForgiveness01);
            return baseSec / Mathf.Max(1f + over * cfg.NeedleOverpowerGain * (1f - forgive), 1e-3f);
        }

        // ── Accuracy windows ────────────────────────────────────────────────────

        /// <summary>
        /// How much the zones shrink for the power being asked for — the Pendulum carry-over
        /// Cesar asked to have from the first build rather than after a review round.
        ///
        /// <para>This is the scheme's RISK/REWARD and the reason a 120% pull is a decision rather
        /// than a free upgrade: overpower already shortens the sweep, and it narrows the target as
        /// well. The same number drives the DRAWN zones from the PEAK power, so the blue zone
        /// closes while the player is still pulling — the cost is visible before they commit to
        /// it, and the window they are graded against is the one they watched close.</para>
        /// </summary>
        public static float WindowScaleForPower(float power, in ControlsConfig cfg)
        {
            float t = Mathf.Clamp01(power / Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
            return Mathf.Max(0.05f, Mathf.Lerp(cfg.NeedleWindowScaleAtZeroPower,
                                               cfg.NeedleWindowScaleAtMaxPower, t));
        }

        /// <summary>
        /// The PERFECT half-window, as a fraction of the arc's 90° half-sweep: Club Accuracy sets
        /// it, power shrinks it. Accuracy's job in this scheme is timing tolerance — the same
        /// "error tolerance" job it does as the cone half-angle in Flick.
        /// </summary>
        public static float PerfectZone01(float clubAccuracyNorm01, float power, in ControlsConfig cfg)
            => Mathf.Lerp(cfg.NeedlePerfectZoneAtAcc0_01,
                          cfg.NeedlePerfectZoneAtAcc120_01,
                          Mathf.Clamp01(clubAccuracyNorm01)) * WindowScaleForPower(power, cfg);

        /// <summary>
        /// The GOOD (small hook/slice) half-window. Fixed by config and shrunk by the same power
        /// scale, but clamped to stay strictly wider than PERFECT: an amber zone narrower than the
        /// blue one inside it would draw as a stripe nobody can land in, and <see cref="Grade"/>
        /// would silently never return a small Hook or Slice at all.
        /// </summary>
        public static float GoodZone01(float clubAccuracyNorm01, float power, in ControlsConfig cfg)
        {
            float perfect = PerfectZone01(clubAccuracyNorm01, power, cfg);
            return Mathf.Max(cfg.NeedleGoodZone01 * WindowScaleForPower(power, cfg), perfect + 0.02f);
        }

        // ── Grade ───────────────────────────────────────────────────────────────

        /// <summary>The scheme's verdict on one tap.</summary>
        public readonly struct Verdict
        {
            public readonly NeedleGrade Grade;
            /// <summary>Radians of aim error, added where the flick's per-pass degradation yaw is.
            /// Positive = the direction <c>AimYawFor</c> calls positive, which is the ball's RIGHT
            /// (see <see cref="NeedleMath.Grade"/>).</summary>
            public readonly float ErrorYawRad;
            public readonly float TimingMul;
            public readonly float Timing01;

            public Verdict(NeedleGrade grade, float errorYawRad, float timingMul, float timing01)
            {
                Grade       = grade;
                ErrorYawRad = errorYawRad;
                TimingMul   = timingMul;
                Timing01    = timing01;
            }
        }

        /// <summary>
        /// Grade a tap from the needle offset <paramref name="n"/> ∈ [−1, +1], 0 = the top of
        /// the arc.
        ///
        /// <para>SIGN CONVENTION: <paramref name="n"/> is +1 at the RIGHT end of the arc, and a
        /// positive <c>ErrorYawRad</c> sends the ball right. That follows from
        /// <c>ShotController.AimYawFor</c> — <c>CameraHeading + finetune × halfCone</c> — which
        /// <c>ShotAimParityTests</c> pins as the single source of truth for where the ball goes: a
        /// positive finetune yaws the aim positively, which is the ball's right. So needle LEFT of
        /// the top (tapped early, n &lt; 0) → negative yaw → ball left → <b>HOOK</b>, and needle
        /// right (late) → <b>SLICE</b>. The miss reads the way it looks.</para>
        ///
        /// <para>A small hook/slice is scaled by <paramref name="n"/> itself, not by its sign:
        /// landing just outside the blue barely bends the shot, landing at the amber edge bends it
        /// a full cone half-angle. A big one is thrown a flat <c>NeedleMissYawGain</c> × half-cone
        /// however far outside it landed — past the amber the tap is simply bad, and a linear ramp
        /// to ±1 would make the very worst tap land in the same place every time, which reads as
        /// scripted.</para>
        /// </summary>
        public static Verdict Grade(float n, float clubAccuracyNorm01, float power,
                                    float halfConeRad, in ControlsConfig cfg)
        {
            n = Mathf.Clamp(n, -1f, 1f);
            float perfect  = PerfectZone01(clubAccuracyNorm01, power, cfg);
            float good     = GoodZone01(clubAccuracyNorm01, power, cfg);
            float timing01 = 1f - Mathf.Abs(n);

            if (Mathf.Abs(n) <= perfect)
                return new Verdict(NeedleGrade.Perfect, 0f, 1f, timing01);

            NeedleGrade side = n < 0f ? NeedleGrade.Hook : NeedleGrade.Slice;

            if (Mathf.Abs(n) <= good)
                return new Verdict(side, n * halfConeRad * cfg.NeedleYawGain,
                                   Mathf.Lerp(1f, cfg.TimingPowerMulGold,
                                              Mathf.Clamp01(Mathf.Abs(n) / Mathf.Max(good, 1e-4f))),
                                   timing01);

            return new Verdict(side, Mathf.Sign(n) * halfConeRad * cfg.NeedleMissYawGain,
                               cfg.TimingPowerMulGold, timing01);
        }

        /// <summary>
        /// The verdict for a swing nobody tapped: the needle ran off the right end.
        ///
        /// <para>A SHANK is thrown the same width as a big slice but pays the RED power multiplier
        /// and scores <c>timing01 = 0</c> — the worst outcome the scheme can produce, which is what
        /// makes "just do not tap" a losing strategy rather than a safe one. It is +yaw (right) by
        /// construction, because the needle was at the right end when the swing timed out.</para>
        /// </summary>
        public static Verdict Shank(float halfConeRad, in ControlsConfig cfg)
            => new Verdict(NeedleGrade.Shank, halfConeRad * cfg.NeedleMissYawGain,
                           cfg.TimingPowerMulRed, 0f);
    }
}
