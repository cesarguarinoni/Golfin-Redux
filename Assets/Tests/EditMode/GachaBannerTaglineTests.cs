// Assets/Tests/EditMode/GachaBannerTaglineTests.cs
// gacha_banner_tagline §3.3 / §9.4 — the `*…*` accent marker and the `\n` line break.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef). GachaBannerCard lives in Assembly-CSharp, which an
// asmdef cannot reference, so the helper is reached through System.Reflection — the same pattern as
// GachaClientRealPullTests, and for the same reason (feedback_tests_must_target_production_type):
// the seam under test is the SHIPPING FormatTagline, not a copy of it. The type and method lookups
// are asserted non-null first, so a rename fails loudly instead of passing vacuously.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaBannerTaglineTests
    {
        private const string Accent = "#FF2D9B";

        private static readonly Type CardType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCard, Assembly-CSharp");

        private static MethodInfo Format
        {
            get
            {
                Assert.IsNotNull(CardType, "GolfinRedux.UI.Gacha.GachaBannerCard not found in Assembly-CSharp");
                var m = CardType.GetMethod("FormatTagline",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(m, "GachaBannerCard.FormatTagline(string) not found — the marker helper was renamed or removed");
                return m;
            }
        }

        private static string Run(string raw) => (string)Format.Invoke(null, new object[] { raw });

        private static string Pink(string s) => "<color=" + Accent + ">" + s + "</color>";

        // ── The shipped copy ─────────────────────────────────────────────────

        [Test]
        public void MarkedRun_BecomesTheAccentColour_AndBackslashN_IsAHardLineBreak()
        {
            // The 40 standard weeks, exactly as rotations.csv stores them (two-character \n).
            Assert.AreEqual("3× RATE-UP ON\n" + Pink("LEGENDARY") + " GEAR!",
                            Run("3× RATE-UP ON\\n*LEGENDARY* GEAR!"));
        }

        [Test]
        public void JapaneseHook_AccentRunAtTheStart()
        {
            Assert.AreEqual(Pink("レジェンダリー") + "装備\n確率3倍！",
                            Run("*レジェンダリー*装備\\n確率3倍！"));
        }

        [Test]
        public void EventWeek_AccentRunMidLine()
        {
            Assert.AreEqual("SPORTS DAY\n" + Pink("DOUBLE") + " LEGENDARY!",
                            Run("SPORTS DAY\\n*DOUBLE* LEGENDARY!"));
        }

        [Test]
        public void RibbonCopy_WithNoMarker_PassesThroughUntouched()
        {
            Assert.AreEqual("GET BogeyB Drivers & Woods", Run("GET BogeyB Drivers & Woods"));
            Assert.AreEqual("BogeyB ドライバー＆ウッドが登場", Run("BogeyB ドライバー＆ウッドが登場"));
        }

        // ── The marker's edge cases (§9.4: a literal `*` is never visible) ──

        [Test]
        public void UnmatchedMarker_IsStripped_NeverShown()
        {
            Assert.AreEqual("3× RATE-UP ON LEGENDARY GEAR!", Run("3× RATE-UP ON *LEGENDARY GEAR!"));
            Assert.AreEqual("TRAILING", Run("TRAILING*"));
            Assert.AreEqual("LEADING", Run("*LEADING"));
        }

        [Test]
        public void ThreeMarkers_FirstPairIsTheRun_ThirdIsStripped()
        {
            Assert.AreEqual("A" + Pink("B") + "CD", Run("A*B*C*D"));
        }

        [Test]
        public void TwoRuns_BothAccented()
        {
            Assert.AreEqual(Pink("DOUBLE") + " " + Pink("LEGENDARY") + "!", Run("*DOUBLE* *LEGENDARY*!"));
        }

        [Test]
        public void EmptyRun_ProducesNoTag_AndNoAsterisk()
        {
            Assert.AreEqual("", Run("**"));
            Assert.AreEqual("AB", Run("A**B"));
        }

        [Test]
        public void EmptyAndNull_ComeBackEmpty()
        {
            Assert.AreEqual("", Run(""));
            Assert.AreEqual("", (string)Format.Invoke(null, new object[] { null }));
        }

        [Test]
        public void ARealNewline_IsKept_NotDoubled()
        {
            // A row edited in a multi-line admin field could already carry a real newline.
            Assert.AreEqual("A\nB", Run("A\nB"));
        }

        [Test]
        public void Output_NeverContainsAMarker()
        {
            foreach (var raw in new[] { "*", "**", "***", "A*B", "*A*B*", "*A**B*", "*\\n*" })
                StringAssert.DoesNotContain("*", Run(raw), "input: " + raw);
        }
    }
}
