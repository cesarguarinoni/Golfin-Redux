// Assets/Tests/EditMode/NoticePageSliderTests.cs
//
// notice_panel_slide §2 — the two pure seams of the Home notice slider.
//
// WHAT IS BEING PINNED. Not "does the box slide" — that is a video. These are the two arithmetic
// decisions a hand-rolled swipe gets wrong, and both were live bugs in the component this replaces:
//
//   1. WHAT A RELEASE MEANS. Distance OR speed commits, and a page count of 1 (the usual case —
//      one published notice) can NEVER commit, however hard it is thrown. Without that last
//      clause a single-notice panel "pages" to itself and the dots strip, which hides at n <= 1,
//      has nothing to show for it.
//   2. THE WRAP. C#'s % keeps the dividend's sign, so stepping back off page 0 lands on -1 and
//      indexes off the front of the notice list — a crash on the player's first right-swipe.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. NoticePageSlider lives in Assembly-CSharp, which a named
// assembly cannot reference, so the seams are reached by reflection — the same pattern
// GachaCarouselLoopTests next door uses against GachaCarouselController.WrapOnRing.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class NoticePageSliderTests
    {
        private static readonly Type SliderType =
            Type.GetType("Golfin.UI.Home.NoticePageSlider, Assembly-CSharp");

        private const float CommitDistance = 60f;   // NoticePageSlider.DefaultCommitDistance
        private const float CommitVelocity = 800f;  // NoticePageSlider.DefaultCommitVelocity

        private static MethodInfo Method(string name)
        {
            Assert.NotNull(SliderType, "NoticePageSlider not found in Assembly-CSharp");
            MethodInfo m = SliderType.GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(m, $"NoticePageSlider.{name} seam is missing");
            return m;
        }

        private static int Resolve(float delta, float velocity, int pageCount)
            => (int)Method("ResolveRelease").Invoke(null,
                   new object[] { delta, velocity, pageCount, CommitDistance, CommitVelocity })!;

        private static int Wrap(int current, int step, int count)
            => (int)Method("WrapIndex").Invoke(null, new object[] { current, step, count })!;

        // ── 1 · the defaults the seam is documented with ─────────────────────

        [Test]
        public void Defaults_MatchTheArchitectValues()
        {
            Assert.AreEqual(CommitDistance,
                (float)SliderType.GetField("DefaultCommitDistance",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                    .GetValue(null)!);
            Assert.AreEqual(CommitVelocity,
                (float)SliderType.GetField("DefaultCommitVelocity",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                    .GetValue(null)!);
        }

        // ── 2 · ResolveRelease: the distance path ───────────────────────────

        [Test]
        public void DragLeftPastThreshold_CommitsToNext()
        {
            Assert.AreEqual(1, Resolve(-60f, 0f, 3), "exactly at the threshold commits");
            Assert.AreEqual(1, Resolve(-400f, 0f, 3));
        }

        [Test]
        public void DragRightPastThreshold_CommitsToPrevious()
        {
            Assert.AreEqual(-1, Resolve(60f, 0f, 3));
            Assert.AreEqual(-1, Resolve(400f, 0f, 3));
        }

        [Test]
        public void ShortSlowDrag_SnapsHome()
        {
            Assert.AreEqual(0, Resolve(-59.9f, 0f, 3));
            Assert.AreEqual(0, Resolve(59.9f, 0f, 3));
            Assert.AreEqual(0, Resolve(0f, 0f, 3));
        }

        // ── 3 · ResolveRelease: the velocity path (a flick) ─────────────────

        [Test]
        public void FastFlickUnderTheDistance_StillCommits()
        {
            // 20 px of travel — nowhere near 60 — thrown at 1200 px/s.
            Assert.AreEqual(1, Resolve(-20f, -1200f, 3));
            Assert.AreEqual(-1, Resolve(20f, 1200f, 3));
        }

        [Test]
        public void FlickAtExactlyTheVelocityThreshold_Commits()
        {
            Assert.AreEqual(1, Resolve(-5f, -CommitVelocity, 3));
            Assert.AreEqual(-1, Resolve(5f, CommitVelocity, 3));
        }

        [Test]
        public void SlowRelease_DoesNotCommitOnVelocity()
        {
            Assert.AreEqual(0, Resolve(-10f, -799f, 3));
            Assert.AreEqual(0, Resolve(10f, 799f, 3));
        }

        // ── 4 · the clause that makes a single notice safe ──────────────────

        [Test]
        public void OnePageOrNone_NeverCommits()
        {
            foreach (int count in new[] { 0, 1 })
            {
                Assert.AreEqual(0, Resolve(-500f, -5000f, count), $"count={count}, hard left");
                Assert.AreEqual(0, Resolve(500f, 5000f, count), $"count={count}, hard right");
            }
        }

        [Test]
        public void TwoPages_CommitInBothDirections()
        {
            Assert.AreEqual(1, Resolve(-120f, 0f, 2));
            Assert.AreEqual(-1, Resolve(120f, 0f, 2));
        }

        // ── 5 · WrapIndex: cyclic in both directions ────────────────────────

        [Test]
        public void Next_WrapsPastTheLastPage()
        {
            Assert.AreEqual(1, Wrap(0, 1, 3));
            Assert.AreEqual(2, Wrap(1, 1, 3));
            Assert.AreEqual(0, Wrap(2, 1, 3));
        }

        [Test]
        public void Previous_WrapsOffTheFront_NotToMinusOne()
        {
            Assert.AreEqual(2, Wrap(0, -1, 3), "page 0 back is the LAST page, never -1");
            Assert.AreEqual(0, Wrap(1, -1, 3));
            Assert.AreEqual(1, Wrap(2, -1, 3));
        }

        [Test]
        public void TwoPages_EitherDirectionShowsTheOtherPage()
        {
            Assert.AreEqual(1, Wrap(0, 1, 2));
            Assert.AreEqual(1, Wrap(0, -1, 2));
            Assert.AreEqual(0, Wrap(1, 1, 2));
            Assert.AreEqual(0, Wrap(1, -1, 2));
        }

        [Test]
        public void SinglePage_StaysOnItself()
        {
            Assert.AreEqual(0, Wrap(0, 1, 1));
            Assert.AreEqual(0, Wrap(0, -1, 1));
        }

        [Test]
        public void ZeroPages_IsZero_NotADivideByZero()
        {
            Assert.AreEqual(0, Wrap(0, 1, 0));
            Assert.AreEqual(0, Wrap(0, -1, 0));
        }

        // ── 6 · the two seams agree: a commit always lands in range ─────────

        [Test]
        public void EveryCommit_LandsInRange()
        {
            for (int count = 2; count <= 5; count++)
            {
                for (int current = 0; current < count; current++)
                {
                    foreach (float delta in new[] { -200f, 200f })
                    {
                        int step = Resolve(delta, 0f, count);
                        Assert.AreNotEqual(0, step, $"count={count} should commit at {delta}px");
                        int landed = Wrap(current, step, count);
                        Assert.GreaterOrEqual(landed, 0, $"count={count} current={current}");
                        Assert.Less(landed, count, $"count={count} current={current}");
                    }
                }
            }
        }
    }
}
