using System.Linq;
using NUnit.Framework;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// miss_grade_duff §3.6 — the Flick scheme's own grade, and the ONE vocabulary all four
    /// schemes now speak.
    ///
    /// <para>Flick shipped for a year with no grade pop at all: the player was told what the
    /// slab cost them only by how far the ball went. These tests pin the band boundaries (which
    /// are the same three numbers <c>ShotController.TimingPowerMultiplier</c> ramps between) and
    /// the key table, which is what stops a fifth word — or an old one — reappearing.</para>
    /// </summary>
    [TestFixture]
    public class FlickMathTests
    {
        private ControlsConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ControlsConfig.Default;

        // ── 1. The four bands, at and either side of every edge ──────────────────

        [TestCase(0.00f, FlickGrade.Duff)]
        [TestCase(0.10f, FlickGrade.Duff)]
        [TestCase(0.15f, FlickGrade.Thin)]   // ON the red line = the bottom of the ramp, not the duff
        [TestCase(0.44f, FlickGrade.Thin)]
        [TestCase(0.45f, FlickGrade.Good)]   // ON the gold line
        [TestCase(0.84f, FlickGrade.Good)]
        [TestCase(0.85f, FlickGrade.Pure)]   // ON the green line
        [TestCase(1.00f, FlickGrade.Pure)]
        public void Grade_MapsTheSlabToTheFourBands(float t, FlickGrade expected)
            => Assert.AreEqual(expected, FlickMath.Grade(t, _cfg));

        [Test]
        public void Grade_ReadsTheConfigEdges_NotLiterals()
        {
            // Retune the table and the boundaries must MOVE. A test that only checked the shipped
            // numbers would pass just as happily against three hardcoded constants.
            var cfg = ControlsConfig.Default;
            cfg.TimingBandRedY01   = 0.30f;
            cfg.TimingBandGoldY01  = 0.60f;
            cfg.TimingBandGreenY01 = 0.90f;

            Assert.AreEqual(FlickGrade.Duff, FlickMath.Grade(0.29f, cfg));
            Assert.AreEqual(FlickGrade.Thin, FlickMath.Grade(0.30f, cfg));
            Assert.AreEqual(FlickGrade.Good, FlickMath.Grade(0.60f, cfg));
            Assert.AreEqual(FlickGrade.Pure, FlickMath.Grade(0.90f, cfg));

            // ...and at the SHIPPED table 0.30 is only a THIN, which proves the retune moved it.
            Assert.AreEqual(FlickGrade.Thin, FlickMath.Grade(0.30f, _cfg));
        }

        [Test]
        public void Grade_ClampsOutOfRangeSamples_RatherThanInventingAFifthOutcome()
        {
            Assert.AreEqual(FlickGrade.Duff, FlickMath.Grade(-5f, _cfg));
            Assert.AreEqual(FlickGrade.Pure, FlickMath.Grade( 9f, _cfg));
        }

        // ── 2. One vocabulary, five keys, all four schemes (D7) ──────────────────

        /// <summary>
        /// The whole point of §3.6: every grade every scheme can produce resolves to one of five
        /// keys. Written as an EXPLICIT table rather than by reflecting over the enums — a
        /// reflection-driven version would pass by construction the day somebody adds a grade and
        /// maps it to a new key, which is exactly the drift this is here to catch.
        /// </summary>
        [Test]
        public void GradeKeys_AcrossAllFourSchemes_AreTheFiveUnifiedKeys()
        {
            const string pure = "SHOT_GRADE_PURE";
            const string good = "SHOT_GRADE_GOOD";
            const string thin = "SHOT_GRADE_THIN";
            const string duff = "SHOT_GRADE_DUFF";
            const string hook = "SHOT_GRADE_HOOK";
            const string slice = "SHOT_GRADE_SLICE";

            // Flick
            Assert.AreEqual(pure, FlickMath.GradeKey(FlickGrade.Pure));
            Assert.AreEqual(good, FlickMath.GradeKey(FlickGrade.Good));
            Assert.AreEqual(thin, FlickMath.GradeKey(FlickGrade.Thin));
            Assert.AreEqual(duff, FlickMath.GradeKey(FlickGrade.Duff));

            // Pendulum — the enum still says Just/Miss; the words no longer do.
            Assert.AreEqual(pure, PendulumMath.GradeKey(PendulumGrade.Just));
            Assert.AreEqual(good, PendulumMath.GradeKey(PendulumGrade.Good));
            Assert.AreEqual(duff, PendulumMath.GradeKey(PendulumGrade.Miss));

            // Needle — Perfect -> PURE, Shank -> DUFF.
            Assert.AreEqual(pure,  NeedleMath.GradeKey(NeedleGrade.Perfect));
            Assert.AreEqual(hook,  NeedleMath.GradeKey(NeedleGrade.Hook));
            Assert.AreEqual(slice, NeedleMath.GradeKey(NeedleGrade.Slice));
            Assert.AreEqual(duff,  NeedleMath.GradeKey(NeedleGrade.Shank));

            // Free Swing — already unified; None deliberately has no word.
            Assert.AreEqual(pure,  FreeSwingMath.GradeKey(FreeSwingGrade.Pure));
            Assert.AreEqual(duff,  FreeSwingMath.GradeKey(FreeSwingGrade.Duff));
            Assert.AreEqual(hook,  FreeSwingMath.GradeKey(FreeSwingGrade.Hook));
            Assert.AreEqual(slice, FreeSwingMath.GradeKey(FreeSwingGrade.Slice));
            Assert.IsNull(FreeSwingMath.GradeKey(FreeSwingGrade.None));
        }

        [Test]
        public void TheRetiredVocabulary_IsReferencedByNoScheme()
        {
            // JUST / PERFECT / SHANK / MISS are gone (D7). Asserted over every grade of every
            // scheme rather than by grepping, so it stays true as the enums grow.
            var keys = new[]
            {
                FlickMath.GradeKey(FlickGrade.Pure),  FlickMath.GradeKey(FlickGrade.Good),
                FlickMath.GradeKey(FlickGrade.Thin),  FlickMath.GradeKey(FlickGrade.Duff),
                PendulumMath.GradeKey(PendulumGrade.Just), PendulumMath.GradeKey(PendulumGrade.Good),
                PendulumMath.GradeKey(PendulumGrade.Miss),
                NeedleMath.GradeKey(NeedleGrade.Perfect), NeedleMath.GradeKey(NeedleGrade.Hook),
                NeedleMath.GradeKey(NeedleGrade.Slice),   NeedleMath.GradeKey(NeedleGrade.Shank),
                FreeSwingMath.GradeKey(FreeSwingGrade.Pure),  FreeSwingMath.GradeKey(FreeSwingGrade.Duff),
                FreeSwingMath.GradeKey(FreeSwingGrade.Hook),  FreeSwingMath.GradeKey(FreeSwingGrade.Slice),
            };

            foreach (var retired in new[] { "SHOT_GRADE_JUST", "SHOT_GRADE_PERFECT",
                                            "SHOT_GRADE_SHANK", "SHOT_GRADE_MISS" })
                CollectionAssert.DoesNotContain(keys, retired, $"{retired} was retired by D7");

            // Distinct(): NUnit's subset constraint does not treat a repeated element as one,
            // and six of these fifteen keys legitimately repeat across the four schemes — that
            // repetition IS the unification being asserted.
            CollectionAssert.IsSubsetOf(keys.Distinct().ToArray(), new[]
            {
                "SHOT_GRADE_PURE", "SHOT_GRADE_GOOD", "SHOT_GRADE_THIN",
                "SHOT_GRADE_DUFF", "SHOT_GRADE_HOOK", "SHOT_GRADE_SLICE",
            }, "no scheme may invent a sixth word");
        }
    }
}
