using System;
using NUnit.Framework;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// selector_carousel §3 — the pure ring arithmetic behind the 4-slot selector.
    ///
    /// <para>The load-bearing cases are the K11 green-gate ones: a snap must NEVER come to rest
    /// on a club the player is not allowed to hit with. Off the green that is the putter, so the
    /// ring has to walk past it in whichever direction the finger was travelling; on the green it
    /// is everything EXCEPT the putter, so every slide and fling has to come home to the putter.
    /// <see cref="ClubSelectionGreenGateTests"/> covers the rule itself; this covers the ring
    /// honouring it.</para>
    ///
    /// <para>NOTE on what ResolveSnapTarget returns: a VIRTUAL index, not a bag index — the ring
    /// coordinate to scroll to, whose item is <c>Mod(result, n)</c>. It can be negative or ≥ n,
    /// and that is the point: coming off item 0 with a downward fling resolves to −1 (item n−1
    /// arriving from below) rather than +(n−1), which would drag the whole stack the long way
    /// round. Assertions below therefore check the ITEM, and check the raw virtual index only
    /// where the direction of travel is what is under test.</para>
    /// </summary>
    public class SelectorCarouselMathTests
    {
        // A realistic bag: 8 clubs, putter last (index 7).
        const int N       = 8;
        const int Putter  = 7;

        static readonly Func<int, bool> OffGreen = i => i != Putter;   // everything but the putter
        static readonly Func<int, bool> OnGreen  = i => i == Putter;   // putter only
        static readonly Func<int, bool> All      = i => true;
        static readonly Func<int, bool> None     = i => false;

        // ── Mod ───────────────────────────────────────────────────────────────

        [Test]
        public void Mod_WrapsNegativesForward()
        {
            Assert.AreEqual(7, SelectorCarouselMath.Mod(-1, 8));
            Assert.AreEqual(0, SelectorCarouselMath.Mod(-8, 8));
            Assert.AreEqual(6, SelectorCarouselMath.Mod(-10, 8));
        }

        [Test]
        public void Mod_WrapsAtN()
        {
            Assert.AreEqual(0, SelectorCarouselMath.Mod(8, 8));
            Assert.AreEqual(1, SelectorCarouselMath.Mod(9, 8));
            Assert.AreEqual(3, SelectorCarouselMath.Mod(3, 8));
        }

        [Test]
        public void Mod_EmptyRingIsZeroNotDivideByZero()
        {
            Assert.AreEqual(0, SelectorCarouselMath.Mod(3, 0));
            Assert.AreEqual(0, SelectorCarouselMath.Mod(-3, -1));
        }

        // ── SlotY ─────────────────────────────────────────────────────────────

        [Test]
        public void SlotY_FocusSlotSitsOnTheMarginAtRest()
        {
            Assert.AreEqual(36f, SelectorCarouselMath.SlotY(0, 2.0f, 274f, 36f), 1e-4f);
        }

        [Test]
        public void SlotY_HalfwayBetweenSlotsIsHalfAPitchUp()
        {
            Assert.AreEqual(36f + 137f, SelectorCarouselMath.SlotY(1, 2.5f, 274f, 36f), 1e-4f);
        }

        [Test]
        public void SlotY_AdjacentSlotsAreExactlyOnePitchApart()
        {
            float a = SelectorCarouselMath.SlotY(1, 3.25f, 274f, 36f);
            float b = SelectorCarouselMath.SlotY(2, 3.25f, 274f, 36f);
            Assert.AreEqual(274f, b - a, 1e-4f);
        }

        [Test]
        public void SlotY_BufferSlotBelowIsOffTheBottomOfTheViewport()
        {
            // j = -1 at rest: bottom edge one pitch below the focus slot, i.e. clipped away.
            Assert.AreEqual(36f - 274f, SelectorCarouselMath.SlotY(-1, 5f, 274f, 36f), 1e-4f);
        }

        // ── EaseOutCubic ──────────────────────────────────────────────────────

        [Test]
        public void EaseOutCubic_PinsBothEndsAndClampsOutside()
        {
            Assert.AreEqual(0f, SelectorCarouselMath.EaseOutCubic(0f),    1e-5f);
            Assert.AreEqual(1f, SelectorCarouselMath.EaseOutCubic(1f),    1e-5f);
            Assert.AreEqual(0f, SelectorCarouselMath.EaseOutCubic(-3f),   1e-5f);
            Assert.AreEqual(1f, SelectorCarouselMath.EaseOutCubic(9f),    1e-5f);
            Assert.AreEqual(0.875f, SelectorCarouselMath.EaseOutCubic(0.5f), 1e-5f);  // fast out of the gate
        }

        // ── SnapDuration ──────────────────────────────────────────────────────

        [Test]
        public void SnapDuration_ClampsToMinAndMax()
        {
            // 0.12 + 0*0.06 = 0.12 → floored at the 0.15 minimum
            Assert.AreEqual(0.15f, SelectorCarouselMath.SnapDuration(0f, 0.12f, 0.06f, 0.15f, 0.35f), 1e-5f);
            // 0.12 + 9*0.06 = 0.66 → capped at 0.35
            Assert.AreEqual(0.35f, SelectorCarouselMath.SnapDuration(9f, 0.12f, 0.06f, 0.15f, 0.35f), 1e-5f);
        }

        [Test]
        public void SnapDuration_ScalesInsideTheBandAndIgnoresTravelSign()
        {
            Assert.AreEqual(0.30f, SelectorCarouselMath.SnapDuration( 3f, 0.12f, 0.06f, 0.15f, 0.35f), 1e-5f);
            Assert.AreEqual(0.30f, SelectorCarouselMath.SnapDuration(-3f, 0.12f, 0.06f, 0.15f, 0.35f), 1e-5f);
        }

        // ── ResolveSnapTarget — fling ─────────────────────────────────────────

        [Test]
        public void ResolveSnapTarget_FlingIsClampedToThreeItems()
        {
            int up = SelectorCarouselMath.ResolveSnapTarget(
                0f, velocityItemsPerSec: 100f, lookaheadSec: 0.12f, maxFling: 3,
                n: N, wrap: true, selectable: All, fallback: 0);
            Assert.AreEqual(3, up, "a hard flick up may not advance more than maxFling items");

            int down = SelectorCarouselMath.ResolveSnapTarget(
                0f, velocityItemsPerSec: -100f, lookaheadSec: 0.12f, maxFling: 3,
                n: N, wrap: true, selectable: All, fallback: 0);
            Assert.AreEqual(-3, down);
        }

        [Test]
        public void ResolveSnapTarget_SlowReleaseSnapsToTheNearestSlot()
        {
            Assert.AreEqual(2, SelectorCarouselMath.ResolveSnapTarget(
                2.2f, 0f, 0.12f, 3, N, true, All, 2));
            Assert.AreEqual(3, SelectorCarouselMath.ResolveSnapTarget(
                2.7f, 0f, 0.12f, 3, N, true, All, 2));
        }

        [Test]
        public void ResolveSnapTarget_WrapsBackwardsThroughZeroAsANegativeVirtualIndex()
        {
            int v = SelectorCarouselMath.ResolveSnapTarget(
                0f, velocityItemsPerSec: -10f, lookaheadSec: 0.12f, maxFling: 3,
                n: N, wrap: true, selectable: All, fallback: 0);
            Assert.AreEqual(-1, v, "the ring must come the short way round, not +7");
            Assert.AreEqual(N - 1, SelectorCarouselMath.Mod(v, N));
        }

        // ── ResolveSnapTarget — K11 green gate ────────────────────────────────

        [Test]
        public void ResolveSnapTarget_StationaryOnAGatedPutterWalksForwardOne()
        {
            int v = SelectorCarouselMath.ResolveSnapTarget(
                Putter, velocityItemsPerSec: 0f, lookaheadSec: 0.12f, maxFling: 3,
                n: N, wrap: true, selectable: OffGreen, fallback: Putter);
            Assert.AreEqual(Putter + 1, v);
            Assert.AreEqual(0, SelectorCarouselMath.Mod(v, N), "must land on the club after the putter");
        }

        [Test]
        public void ResolveSnapTarget_TravellingDownOntoAGatedPutterWalksPastIt()
        {
            // Released mid-slide heading down; the projected landing IS the putter.
            int v = SelectorCarouselMath.ResolveSnapTarget(
                7.4f, velocityItemsPerSec: -5f, lookaheadSec: 0.12f, maxFling: 3,
                n: N, wrap: true, selectable: OffGreen, fallback: Putter);
            Assert.AreEqual(Putter - 1, v, "keeps going the way the finger was going, never reverses onto the gate");
        }

        [Test]
        public void ResolveSnapTarget_NeverRestsOnTheGatedPutterFromAnywhere()
        {
            for (float scroll = -2f; scroll <= 10f; scroll += 0.25f)
            foreach (float vel in new[] { -40f, -8f, -1f, 0f, 1f, 8f, 40f })
            {
                int v = SelectorCarouselMath.ResolveSnapTarget(
                    scroll, vel, 0.12f, 3, N, true, OffGreen, 0);
                Assert.AreNotEqual(Putter, SelectorCarouselMath.Mod(v, N),
                    $"snapped onto the gated putter from scroll={scroll} vel={vel}");
            }
        }

        [Test]
        public void ResolveSnapTarget_PutterModeAlwaysComesHomeToThePutter()
        {
            for (float scroll = -2f; scroll <= 10f; scroll += 0.25f)
            foreach (float vel in new[] { -40f, -8f, -1f, 0f, 1f, 8f, 40f })
            {
                int v = SelectorCarouselMath.ResolveSnapTarget(
                    scroll, vel, 0.12f, 3, N, true, OnGreen, Putter);
                Assert.AreEqual(Putter, SelectorCarouselMath.Mod(v, N),
                    $"left the putter from scroll={scroll} vel={vel}");
            }
            // From a standing start three slots away it resolves to the putter's own index.
            Assert.AreEqual(Putter, SelectorCarouselMath.ResolveSnapTarget(
                4f, 0f, 0.12f, 3, N, true, OnGreen, Putter));
        }

        // ── ResolveSnapTarget — short, non-wrapping stacks ────────────────────

        [Test]
        public void ResolveSnapTarget_NonWrappingStackStaysInRange()
        {
            const int n = 3;
            for (float scroll = -1.5f; scroll <= 4f; scroll += 0.25f)
            foreach (float vel in new[] { -40f, -5f, 0f, 5f, 40f })
            {
                int v = SelectorCarouselMath.ResolveSnapTarget(
                    scroll, vel, 0.12f, 3, n, wrap: false, selectable: All, fallback: 0);
                Assert.GreaterOrEqual(v, 0,     $"ran off the bottom from scroll={scroll} vel={vel}");
                Assert.LessOrEqual(v, n - 1,    $"ran off the top from scroll={scroll} vel={vel}");
            }
        }

        [Test]
        public void ResolveSnapTarget_NonWrappingWalkTurnsBackInwardAtAnEnd()
        {
            // 3 items, only item 0 selectable, finger travelling up off the top end.
            int v = SelectorCarouselMath.ResolveSnapTarget(
                2f, velocityItemsPerSec: 20f, lookaheadSec: 0.12f, maxFling: 3,
                n: 3, wrap: false, selectable: i => i == 0, fallback: 2);
            Assert.AreEqual(0, v);
        }

        // ── ResolveSnapTarget — degenerate inputs ─────────────────────────────

        [Test]
        public void ResolveSnapTarget_NothingSelectableReturnsFallback()
        {
            Assert.AreEqual(42, SelectorCarouselMath.ResolveSnapTarget(
                3f, 5f, 0.12f, 3, N, true, None, fallback: 42));
        }

        [Test]
        public void ResolveSnapTarget_EmptyRingReturnsFallback()
        {
            Assert.AreEqual(-9, SelectorCarouselMath.ResolveSnapTarget(
                0f, 0f, 0.12f, 3, n: 0, wrap: true, selectable: All, fallback: -9));
        }
    }
}
