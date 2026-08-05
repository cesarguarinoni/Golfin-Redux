using NUnit.Framework;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// K11 club_selection_green_gate — the putter is selectable ONLY on the green, and
    /// non-putter clubs are NOT selectable on the green.
    ///
    /// These cover the pure rule (ClubSelectionBroadcast.IsSelectable) that both selector
    /// paths share — SelectorOverlayWidget.Populate (card enable/disable) and
    /// SelectorOverlayWidget.Scroll (arrow skip). The rule deliberately takes putt-mode as
    /// a parameter instead of re-deriving surface: putt mode is set by §2f
    /// (PutterModeSurfaceController.DecideTargetClub, GREEN-STRICT — GreenCollar counts as
    /// OFF-green), so the gate and the auto-switch can never disagree.
    /// </summary>
    public class ClubSelectionGreenGateTests
    {
        const int Putter = 3;   // PhysicsLabController.PutterIndex (LabClubs.Length - 1)
        const int Driver = 0;
        const int Iron   = 1;
        const int Wedge  = 2;

        // ── On the green (putt mode): putter only ─────────────────────────────

        [Test]
        public void OnGreen_PutterIsSelectable()
        {
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Putter, Putter, inPutterMode: true));
        }

        [Test]
        public void OnGreen_NonPutterClubsAreNotSelectable()
        {
            Assert.IsFalse(ClubSelectionBroadcast.IsSelectable(Driver, Putter, inPutterMode: true));
            Assert.IsFalse(ClubSelectionBroadcast.IsSelectable(Iron,   Putter, inPutterMode: true));
            Assert.IsFalse(ClubSelectionBroadcast.IsSelectable(Wedge,  Putter, inPutterMode: true));
        }

        // ── Off the green: everything except the putter ───────────────────────

        [Test]
        public void OffGreen_PutterIsNotSelectable()
        {
            Assert.IsFalse(ClubSelectionBroadcast.IsSelectable(Putter, Putter, inPutterMode: false));
        }

        [Test]
        public void OffGreen_NonPutterClubsAreSelectable()
        {
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Driver, Putter, inPutterMode: false));
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Iron,   Putter, inPutterMode: false));
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Wedge,  Putter, inPutterMode: false));
        }

        // ── The two modes are exact complements ───────────────────────────────

        [Test]
        public void EveryClubIsSelectableInExactlyOneMode()
        {
            for (int i = 0; i <= Putter; i++)
            {
                bool onGreen  = ClubSelectionBroadcast.IsSelectable(i, Putter, inPutterMode: true);
                bool offGreen = ClubSelectionBroadcast.IsSelectable(i, Putter, inPutterMode: false);
                Assert.AreNotEqual(onGreen, offGreen,
                    $"club {i} must be selectable in exactly one mode (on-green={onGreen}, off-green={offGreen})");
            }
        }

        [Test]
        public void OnGreen_ExactlyOneClubIsSelectable()
        {
            int selectable = 0;
            for (int i = 0; i <= Putter; i++)
                if (ClubSelectionBroadcast.IsSelectable(i, Putter, inPutterMode: true)) selectable++;
            Assert.AreEqual(1, selectable, "on the green only the putter may be selectable");
        }

        // ── Fail-open when nothing has been published ─────────────────────────

        [Test]
        public void UnpublishedPutterIndex_FailsOpen()
        {
            // -1 = no lab controller has published yet. Gating on a putter index we do not
            // know would soft-lock the selector, so every club stays selectable.
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Driver, -1, inPutterMode: false));
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Putter, -1, inPutterMode: false));
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Driver, -1, inPutterMode: true));
            Assert.IsTrue(ClubSelectionBroadcast.IsSelectable(Putter, -1, inPutterMode: true));
        }

        // ── Publisher round-trip ──────────────────────────────────────────────

        [Test]
        public void SetPutterMode_PublishesFlagAndIndex_AndFiresOnChangeOnly()
        {
            bool entered = ClubSelectionBroadcast.InPutterMode;
            int  origIdx = ClubSelectionBroadcast.PutterLabClubIndex;
            int  events  = 0;
            System.Action<bool> handler = _ => events++;
            ClubSelectionBroadcast.OnPutterModeChanged += handler;
            try
            {
                // Force a known starting state without counting that transition.
                ClubSelectionBroadcast.SetPutterMode(false, Putter);
                events = 0;

                ClubSelectionBroadcast.SetPutterMode(true, Putter);
                Assert.IsTrue(ClubSelectionBroadcast.InPutterMode);
                Assert.AreEqual(Putter, ClubSelectionBroadcast.PutterLabClubIndex);
                Assert.AreEqual(1, events);

                // Idempotent re-publish must not re-fire.
                ClubSelectionBroadcast.SetPutterMode(true, Putter);
                Assert.AreEqual(1, events);

                ClubSelectionBroadcast.SetPutterMode(false, Putter);
                Assert.IsFalse(ClubSelectionBroadcast.InPutterMode);
                Assert.AreEqual(2, events);
            }
            finally
            {
                ClubSelectionBroadcast.OnPutterModeChanged -= handler;
                ClubSelectionBroadcast.SetPutterMode(entered, origIdx);
            }
        }
    }
}
