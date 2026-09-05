using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>How well the player timed the marker (scheme_pendulum §3.4).</summary>
    public enum PendulumGrade { Just = 0, Good = 1, Miss = 2 }

    /// <summary>
    /// Everything the Pendulum scheme decides, as pure functions of numbers.
    ///
    /// <para>STATIC AND MonoBehaviour-FREE ON PURPOSE. The grade is the whole scheme — it is what
    /// turns a finger into a <c>ShotIntent</c> — so it is the part that has to be testable without
    /// a scene, a canvas, an EventSystem or a play-mode frame. <see cref="PendulumSchemeDriver"/>
    /// is then only wiring: read the finger, call in here, hand the answer to
    /// <c>ShotController.CommitExternal</c>.</para>
    ///
    /// <para>Every knob comes in as a <see cref="ControlsConfig"/>, never read from a static, so a
    /// test can drive the whole table without touching the shipped tuning.</para>
    /// </summary>
    public static class PendulumMath
    {
        /// <summary>Localisation KEYS — never literals. Published by the two-way content importer.</summary>
        public const string KeyJust = "SHOT_GRADE_JUST";
        public const string KeyGood = "SHOT_GRADE_GOOD";
        public const string KeyMiss = "SHOT_GRADE_MISS";

        public static string GradeKey(PendulumGrade g) => g switch
        {
            PendulumGrade.Just => KeyJust,
            PendulumGrade.Good => KeyGood,
            _                  => KeyMiss,
        };

        // ── Power ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Pull distance (canvas px, straight down from the touch origin) → power 0..1.2.
        ///
        /// <para>Same SHAPE as <c>ShotController.ComputePower</c> — dead zone, linear to 100%,
        /// then a 0.2-wide overpower ramp — but against the Pendulum's own lane thresholds, and
        /// with the putt cap applied here rather than inferred, because the lane the player sees
        /// is 320 px tall on a putt and has no 120% tick to pull past.</para>
        /// </summary>
        public static float Power(float pullPx, in ControlsConfig cfg, bool isPutt)
        {
            float minPull = cfg.PendulumMinUsefulPullPx;
            float p100    = cfg.PendulumPull100Px;
            float p120    = cfg.PendulumPull120Px;

            if (pullPx < minPull) return 0f;

            float span = Mathf.Max(p100 - minPull, 1e-3f);
            if (pullPx <= p100) return Mathf.Clamp01((pullPx - minPull) / span);

            // A putt has no overpower: the lane stops at the 100% tick and so does the number.
            if (isPutt) return 1f;

            float overRange = Mathf.Max(p120 - p100, 1e-3f);
            return Mathf.Min(1f + ((pullPx - p100) / overRange) * 0.2f,
                             Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
        }

        // ── Marker speed ────────────────────────────────────────────────────────

        /// <summary>
        /// Marker sweep frequency, in full sinusoid cycles per second.
        ///
        /// <para>ITS OWN LINE, NOT THE FLICK'S. This first reused
        /// <c>BaseArrowSpeedHzAtCC0</c> / <c>ArrowSpeedHzPerCC</c> on the argument that both
        /// schemes ask the identical question. Cesar, watching the first clip: <i>"the horizontal
        /// ball is moving way too fast"</i>. The question is not identical — the flick's arrow
        /// crosses a slab once per pass and is judged at the latch, while this marker crosses the
        /// pip TWICE per cycle and has to be trackable by eye the entire way. Sharing the number
        /// meant Pendulum could not be slowed without slowing the shipping scheme, which is the
        /// one thing this track must never do. <c>PuttArrowSpeedMultiplier</c> IS still shared:
        /// "a putt's timing element is slower than a swing's" is a rule about putting, not about
        /// either scheme.</para>
        ///
        /// <para>Overpower is the one Pendulum-only term: pulling past 100% speeds the marker up,
        /// and Character Strength (as overpower forgiveness) buys most of that back. That is the
        /// risk the 120% pull is supposed to carry — it costs timing, not accuracy.</para>
        /// </summary>
        public static float Hz(float clubControl, float power, float overpowerForgiveness01,
                               bool isPutt, in ControlsConfig cfg)
        {
            float cc   = Mathf.Clamp(clubControl, 0f, 100f);
            float baseHz = Mathf.Max(cfg.PendulumBaseHzAtCC0 + cc * cfg.PendulumHzPerCC,
                                     cfg.PendulumMinHz);

            // Putts never overpower, so they never pay the speed-up either.
            if (isPutt) return baseHz * cfg.PuttArrowSpeedMultiplier;

            float over = Mathf.Max(0f, power - 1f);
            float forgive = Mathf.Clamp01(overpowerForgiveness01);
            return baseHz * (1f + over * cfg.PendulumOverpowerGain * (1f - forgive));
        }

        // ── Accuracy windows ────────────────────────────────────────────────────

        /// <summary>
        /// How much the accuracy windows shrink for the power being asked for (Cesar, 2026-09-05:
        /// "the hitting area should shrink the further the player pulls").
        ///
        /// <para>This is the scheme's RISK/REWARD, and it is the reason a 120% pull is a decision
        /// rather than a free upgrade: overpower already speeds the marker up, and now it narrows
        /// the target as well. A soft lay-up is correspondingly forgiving. The same number drives
        /// the DRAWN bands, so the player watches the green band close as they pull — the cost is
        /// visible before they commit to it, not discovered afterwards.</para>
        /// </summary>
        public static float WindowScaleForPower(float power, in ControlsConfig cfg)
        {
            float t = Mathf.Clamp01(power / Golfin.Gameplay.Input.ShotController.MaxOverpowerNormalized);
            return Mathf.Max(0.05f, Mathf.Lerp(cfg.PendulumWindowScaleAtZeroPower,
                                               cfg.PendulumWindowScaleAtMaxPower, t));
        }

        /// <summary>
        /// The JUST half-window as a fraction of the bar's half-travel: Club Accuracy sets it,
        /// power shrinks it. Accuracy's job in this scheme is timing tolerance — the same "error
        /// tolerance" job it does as the cone half-angle in Flick.
        /// </summary>
        public static float JustWindow01(float clubAccuracyNorm01, float power, in ControlsConfig cfg)
            => Mathf.Lerp(cfg.PendulumJustWindowAtAcc0_01,
                          cfg.PendulumJustWindowAtAcc120_01,
                          Mathf.Clamp01(clubAccuracyNorm01)) * WindowScaleForPower(power, cfg);

        /// <summary>
        /// The GOOD half-window. Fixed by config and shrunk by the same power scale, but clamped
        /// to stay strictly wider than JUST: a GOOD band narrower than the JUST band inside it
        /// would draw as a stripe the player can never land in, and <see cref="Grade"/> would
        /// silently never return Good.
        /// </summary>
        public static float GoodWindow01(float clubAccuracyNorm01, float power, in ControlsConfig cfg)
        {
            float just = JustWindow01(clubAccuracyNorm01, power, cfg);
            return Mathf.Max(cfg.PendulumGoodWindow01 * WindowScaleForPower(power, cfg), just + 0.01f);
        }

        // ── Grade ───────────────────────────────────────────────────────────────

        /// <summary>The scheme's verdict on one release.</summary>
        public readonly struct Verdict
        {
            public readonly PendulumGrade Grade;
            /// <summary>Radians of aim error to add, in the same place the flick's per-pass
            /// degradation yaw is added. Positive = the same direction <c>AimYawFor</c> calls
            /// positive, which is the ball's RIGHT (see <see cref="PendulumMath.Grade"/>).</summary>
            public readonly float ErrorYawRad;
            public readonly float TimingMul;
            public readonly float Timing01;

            public Verdict(PendulumGrade grade, float errorYawRad, float timingMul, float timing01)
            {
                Grade       = grade;
                ErrorYawRad = errorYawRad;
                TimingMul   = timingMul;
                Timing01    = timing01;
            }
        }

        /// <summary>
        /// Grade a release from the marker offset.
        ///
        /// <para>SIGN CONVENTION: <paramref name="m"/> is +1 at the RIGHT end of the bar, and a
        /// positive <c>ErrorYawRad</c> sends the ball right. That follows from
        /// <c>ShotController.AimYawFor</c>, which is <c>CameraHeading + finetune * halfCone</c>
        /// and which <c>ShotAimParityTests</c> pins as the single source of truth for where the
        /// ball goes: a positive finetune (handle pushed right of centre) yaws the aim positively.
        /// So marker-right → +yaw → ball right, and the miss reads the way it looks.</para>
        ///
        /// <para>The GOOD miss is scaled by the marker offset (landing just outside the JUST band
        /// barely bends the shot; landing at the band edge bends it a full cone half-angle), which
        /// is why <paramref name="m"/> and not <c>sign(m)</c> is used there. A MISS is thrown
        /// <c>PendulumMissYawGain</c> times the cone half-angle regardless of how far outside it
        /// landed — past the band the shot is simply bad, and a linear ramp to ±1 would make the
        /// very worst release land in the same place every time, which reads as scripted.</para>
        /// </summary>
        public static Verdict Grade(float m, float clubAccuracyNorm01, float power,
                                    float halfConeRad, in ControlsConfig cfg)
        {
            m = Mathf.Clamp(m, -1f, 1f);
            float just    = JustWindow01(clubAccuracyNorm01, power, cfg);
            float good    = GoodWindow01(clubAccuracyNorm01, power, cfg);
            float timing01 = 1f - Mathf.Abs(m);

            if (Mathf.Abs(m) <= just)
                return new Verdict(PendulumGrade.Just, 0f, 1f, timing01);

            if (Mathf.Abs(m) <= good)
                return new Verdict(PendulumGrade.Good, m * halfConeRad,
                                   cfg.TimingPowerMulGold, timing01);

            return new Verdict(PendulumGrade.Miss,
                               Mathf.Sign(m) * halfConeRad * cfg.PendulumMissYawGain,
                               cfg.TimingPowerMulRed, timing01);
        }

        // ── Marker position ─────────────────────────────────────────────────────

        /// <summary>
        /// Marker offset −1..+1 at a given phase. Sinusoidal, not triangular: the marker is slow
        /// at the ends and fastest through the middle, which is the "pause at the edges" feel of
        /// 白猫GOLF and the reason the centre pip is hard to hit at all.
        /// </summary>
        public static float MarkerAt(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);
    }
}
