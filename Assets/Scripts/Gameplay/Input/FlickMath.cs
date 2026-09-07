using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.Input
{
    /// <summary>Which band of the cone the flick's aim latched in (miss_grade_duff §3.6).</summary>
    public enum FlickGrade
    {
        /// <summary>At or above the green line — flush contact, no penalty.</summary>
        Pure = 0,
        /// <summary>Gold line up to green — a good strike that cost a little power.</summary>
        Good = 1,
        /// <summary>Red line up to gold — weak contact. The ramp zone.</summary>
        Thin = 2,
        /// <summary>Below the red line — a DUFF. <c>MissPowerMul</c>, topped launch.</summary>
        Duff = 3,
    }

    /// <summary>
    /// The Flick scheme's grade, as a pure function of the latch position.
    ///
    /// <para>WHY IT LIVES IN THE INPUT ASSEMBLY and not beside <c>PendulumMath</c> /
    /// <c>NeedleMath</c> / <c>FreeSwingMath</c> in <c>Golfin.Gameplay.UI</c>: those three schemes
    /// are DRIVERS, and their maths sits next to the driver that calls it. Flick has no driver —
    /// <c>ShotController.TimingPowerMultiplier</c> IS the Flick grader — and
    /// <c>Golfin.Gameplay.UI</c> already references <c>Golfin.Gameplay.Input</c>, so the reverse
    /// reference the UI folder would have required does not exist and cannot be added. This is
    /// therefore the only place BOTH the controller and the pop can read one copy of the bands
    /// from, which is the whole point of the file (SPEC §3.6: "the same three numbers, no
    /// duplicates"). <c>ShotController</c> derives its own <c>isMiss</c> from
    /// <see cref="Grade"/>, so the drawn band, the pop word and the power penalty are one
    /// classification rather than three that happen to agree today.</para>
    /// </summary>
    public static class FlickMath
    {
        /// <summary>Localisation KEYS — never literals. Published by the two-way content
        /// importer, and shared verbatim with the other three schemes (SPEC §3.6: one grade
        /// vocabulary, so a player who has learned one scheme has learned all four).</summary>
        public const string KeyPure = "SHOT_GRADE_PURE";
        public const string KeyGood = "SHOT_GRADE_GOOD";
        public const string KeyThin = "SHOT_GRADE_THIN";
        public const string KeyDuff = "SHOT_GRADE_DUFF";

        public static string GradeKey(FlickGrade g) => g switch
        {
            FlickGrade.Pure => KeyPure,
            FlickGrade.Good => KeyGood,
            FlickGrade.Thin => KeyThin,
            _               => KeyDuff,
        };

        /// <summary>
        /// Grade a latch at slab progress <paramref name="t"/> (0 = cone base, 1 = apex) against
        /// the SAME three band edges the cone is drawn with and the power multiplier is computed
        /// from. Clamped, so an out-of-range sample reads as the nearest band rather than as a
        /// fourth outcome.
        /// </summary>
        public static FlickGrade Grade(float t, in ControlsConfig cfg)
        {
            t = Mathf.Clamp01(t);
            if (t >= cfg.TimingBandGreenY01) return FlickGrade.Pure;
            if (t >= cfg.TimingBandGoldY01)  return FlickGrade.Good;
            if (t >= cfg.TimingBandRedY01)   return FlickGrade.Thin;
            return FlickGrade.Duff;
        }
    }
}
