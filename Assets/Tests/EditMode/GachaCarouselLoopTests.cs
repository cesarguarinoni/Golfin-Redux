// Assets/Tests/EditMode/GachaCarouselLoopTests.cs
//
// The Gacha banner carousel wraps: swiping past the last banner continues onto the first.
//
// WHAT IS ACTUALLY BEING PINNED. Not "does it look endless" — two arithmetic properties that
// together ARE the loop, and that a hand-rolled version of this gets wrong in exactly two ways:
//
//   1. every card is drawn at its NEAREST copy, so the last banner sits one slot to the LEFT of
//      the first rather than N-1 slots to the right. Get this wrong and the wrap "works" but the
//      whole strip flies across the screen to do it.
//   2. the snap index is a POSITIVE modulo of an unbounded scroll. C#'s % keeps the sign of the
//      dividend, so a player who swipes left off banner 0 lands on index -1 and indexes off the
//      front of the list — the crash this test exists to prevent.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. GachaCarouselController lives in Assembly-CSharp, which a
// named assembly cannot reference, so the seams are reached by reflection — the same pattern
// GachaStage2Tests next door uses for FormatCountdown, and against the same production type.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaCarouselLoopTests
    {
        private static readonly Type CarouselType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaCarouselController, Assembly-CSharp");

        private static float Wrap(float delta, int count, float spacing)
        {
            Assert.NotNull(CarouselType, "GachaCarouselController not found in Assembly-CSharp");
            MethodInfo m = CarouselType.GetMethod("WrapOnRing",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(m, "GachaCarouselController.WrapOnRing seam is missing");
            return (float)m.Invoke(null, new object[] { delta, count, spacing })!;
        }

        private static int Mod(int a, int n)
        {
            MethodInfo m = CarouselType.GetMethod("Mod",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(m, "GachaCarouselController.Mod seam is missing");
            return (int)m.Invoke(null, new object[] { a, n })!;
        }

        const float Spacing = 800f;

        // ── 1 · nearest copy ─────────────────────────────────────────────────

        [Test]
        public void CentredCard_StaysAtZero()
            => Assert.AreEqual(0f, Wrap(0f, 3, Spacing), 0.001f);

        [Test]
        public void Neighbour_IsOneSlotAway_NotWrapped()
        {
            Assert.AreEqual( Spacing, Wrap( Spacing, 3, Spacing), 0.001f);
            Assert.AreEqual(-Spacing, Wrap(-Spacing, 3, Spacing), 0.001f);
        }

        [Test]
        public void LastCard_SitsOneSlotLeftOfTheFirst_NotTwoSlotsRight()
        {
            // Three banners, banner 2 while banner 0 is centred: its absolute slot is +1600, but
            // the ring's answer is -800 — one card's width to the LEFT. This is the assertion that
            // separates a wrap from a long slide back across the strip.
            Assert.AreEqual(-Spacing, Wrap(2f * Spacing, 3, Spacing), 0.001f);
        }

        [Test]
        public void ScrollPastTheEnd_ComesBackToTheStart()
        {
            // A player who has swiped a full turn and a bit: card 0 seen from scroll 3*800+800.
            Assert.AreEqual(-Spacing, Wrap(-4f * Spacing, 3, Spacing), 0.001f);
        }

        [Test]
        public void ManyTurnsOut_ReducesToTheSameNeighbour()
        {
            // Ten laps of the ring must land exactly where zero laps did, or the strip drifts.
            Assert.AreEqual(Wrap(Spacing, 4, Spacing),
                            Wrap(Spacing + 10f * 4f * Spacing, 4, Spacing), 0.01f);
        }

        [Test]
        public void EveryReduction_StaysWithinHalfASpan()
        {
            const int count = 5;
            float half = count * Spacing * 0.5f;
            for (int i = -37; i <= 37; i++)
            {
                float w = Wrap(i * 137f, count, Spacing);
                Assert.LessOrEqual(Math.Abs(w), half + 0.001f,
                    $"delta {i * 137f} reduced to {w}, outside +/-{half}");
            }
        }

        // ── the two shapes that are NOT a ring ───────────────────────────────

        [Test]
        public void SingleBanner_DoesNotWrap()
        {
            // One card is not a ring: with a span of one slot every position collapses onto the
            // centre and a swipe would move nothing. Left alone, the caller's non-wrapping clamp
            // handles it exactly as it did before the loop existed.
            Assert.AreEqual(1234f, Wrap(1234f, 1, Spacing), 0.001f);
            Assert.AreEqual(1234f, Wrap(1234f, 0, Spacing), 0.001f);
        }

        [Test]
        public void NonPositiveSpacing_DoesNotWrap()
        {
            // Guards a divide-by-zero span. A carousel whose spacing has not been authored yet
            // must not send every card to the same pixel.
            Assert.AreEqual(500f, Wrap(500f, 4, 0f),  0.001f);
            Assert.AreEqual(500f, Wrap(500f, 4, -8f), 0.001f);
        }

        // ── 2 · positive modulo ──────────────────────────────────────────────

        [Test]
        public void SwipingLeftOffTheFirstBanner_LandsOnTheLast()
        {
            // The bug this pins: C# gives -1 % 3 == -1, which indexes off the front of the list.
            Assert.AreEqual(2, Mod(-1, 3));
            Assert.AreEqual(1, Mod(-2, 3));
            Assert.AreEqual(0, Mod(-3, 3));
        }

        [Test]
        public void SwipingRightOffTheLastBanner_LandsOnTheFirst()
        {
            Assert.AreEqual(0, Mod(3, 3));
            Assert.AreEqual(1, Mod(4, 3));
        }

        [Test]
        public void Mod_IsAlwaysAValidIndex()
        {
            for (int n = 1; n <= 6; n++)
                for (int a = -50; a <= 50; a++)
                {
                    int m = Mod(a, n);
                    Assert.GreaterOrEqual(m, 0, $"Mod({a},{n}) = {m}");
                    Assert.Less(m, n, $"Mod({a},{n}) = {m}");
                }
        }

        [Test]
        public void Mod_WithNoBanners_IsZeroNotADivideByZero()
            => Assert.AreEqual(0, Mod(-7, 0));
    }
}
