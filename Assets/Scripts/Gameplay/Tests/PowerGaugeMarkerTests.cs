using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for power_gauge_target_marker (SPEC §5).
    ///
    /// Two seams under test:
    ///   1. PowerGaugeWidget.ComputeMarkerFrac — pure math: no target, exact carry, half carry,
    ///      beyond overpower reach (pinned + unreachable flag), and club change re-deriving the
    ///      fraction from an unchanged target distance in metres.
    ///   2. ShotController.MapTargetCarryM lifecycle — default -1, survives a fumbled flick,
    ///      cleared by a committed shot.
    ///
    /// NOT under test here (deliberately): the power curve. This feature is a readout —
    /// ComputePower / overpower / ControlsConfig are untouched.
    /// </summary>
    [TestFixture]
    public class PowerGaugeMarkerTests
    {
        private const float kYardsToMeters = 0.9144f;
        private const float kEpsilon       = 0.0005f;

        // ─────────────────────────────────────────────────────────────────────
        // 1. ComputeMarkerFrac — pure math seam
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void NoTarget_YieldsNoMarker()
        {
            float frac = PowerGaugeWidget.ComputeMarkerFrac(-1f, 200f, out bool unreachable);

            Assert.AreEqual(PowerGaugeWidget.MarkerNone, frac,
                "MapTargetCarryM == -1 (no target mapped) must produce the no-marker sentinel.");
            Assert.IsFalse(unreachable, "No target cannot be 'unreachable'.");
            Assert.Less(frac, 0f, "The sentinel must be negative so the graphic skips drawing.");
        }

        [Test]
        public void ZeroOrNegativeCarry_YieldsNoMarker()
        {
            // ClubContext unpopulated / bad data — better no notch than a divide-by-zero notch.
            float frac = PowerGaugeWidget.ComputeMarkerFrac(150f, 0f, out bool unreachable);

            Assert.AreEqual(PowerGaugeWidget.MarkerNone, frac);
            Assert.IsFalse(unreachable);
        }

        [Test]
        public void TargetEqualsClubCarry_IsFullPower()
        {
            float clubCarryYards = 200f;
            float targetM        = clubCarryYards * kYardsToMeters;   // exactly 100% of carry

            float frac = PowerGaugeWidget.ComputeMarkerFrac(targetM, clubCarryYards, out bool unreachable);

            Assert.AreEqual(1f, frac, kEpsilon, "Target at the club's full carry must mark 100%.");
            Assert.IsFalse(unreachable, "Reaching exactly 100% is not over-reach.");
        }

        [Test]
        public void TargetAtHalfCarry_IsHalfPower()
        {
            float clubCarryYards = 200f;
            float targetM        = clubCarryYards * kYardsToMeters * 0.5f;

            float frac = PowerGaugeWidget.ComputeMarkerFrac(targetM, clubCarryYards, out bool unreachable);

            Assert.AreEqual(0.5f, frac, kEpsilon);
            Assert.IsFalse(unreachable);
        }

        [Test]
        public void TargetInOverpowerBand_IsNotFlaggedUnreachable()
        {
            // 110% of carry is reachable — the flick's overpower band runs to 120%.
            float clubCarryYards = 200f;
            float targetM        = clubCarryYards * kYardsToMeters * 1.1f;

            float frac = PowerGaugeWidget.ComputeMarkerFrac(targetM, clubCarryYards, out bool unreachable);

            Assert.AreEqual(1.1f, frac, kEpsilon);
            Assert.IsFalse(unreachable, "Inside the overpower band the target is still reachable.");
        }

        [Test]
        public void TargetBeyondOverpower_PinsAtCeilingAndFlagsUnreachable()
        {
            float clubCarryYards = 200f;
            float targetM        = clubCarryYards * kYardsToMeters * 1.8f;   // way past reach

            float frac = PowerGaugeWidget.ComputeMarkerFrac(targetM, clubCarryYards, out bool unreachable);

            Assert.AreEqual(PowerGaugeWidget.MarkerMaxFrac, frac, kEpsilon,
                "A target past 1.2x carry pins the notch at the overpower ceiling.");
            Assert.IsTrue(unreachable, "Past 1.2x carry the marker must report over-reach (red).");
        }

        [Test]
        public void VeryNearTarget_ClampsToVisibleFloor()
        {
            float frac = PowerGaugeWidget.ComputeMarkerFrac(0.5f, 250f, out bool unreachable);

            Assert.AreEqual(PowerGaugeWidget.MarkerMinFrac, frac, kEpsilon,
                "A tap-in-distance target still needs a drawable notch, not a 0-degree sliver.");
            Assert.IsFalse(unreachable);
        }

        [Test]
        public void ClubChange_MovesMarker_TargetDistanceUnchanged()
        {
            // The whole reason MapTargetCarryM is stored in METRES: swapping clubs re-derives
            // the fraction against the new carry instead of freezing a stale percentage.
            float targetM = 160f;   // ~175yd landing, placed once on the map

            float withDriver = PowerGaugeWidget.ComputeMarkerFrac(targetM, 250f, out bool driverUnreachable);
            float with7Iron  = PowerGaugeWidget.ComputeMarkerFrac(targetM, 160f, out bool ironUnreachable);
            float withWedge  = PowerGaugeWidget.ComputeMarkerFrac(targetM,  90f, out bool wedgeUnreachable);

            Assert.AreEqual(160f / (250f * kYardsToMeters), withDriver, kEpsilon);
            Assert.AreEqual(160f / (160f * kYardsToMeters), with7Iron,  kEpsilon);

            Assert.Less(withDriver, with7Iron,
                "The same landing needs a SMALLER fraction of a longer club's carry.");
            Assert.IsFalse(driverUnreachable);
            Assert.IsFalse(ironUnreachable);

            Assert.AreEqual(PowerGaugeWidget.MarkerMaxFrac, withWedge, kEpsilon,
                "A 160m target is out of a 90yd wedge's reach — pin at the ceiling.");
            Assert.IsTrue(wedgeUnreachable, "...and flag it red.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. ShotController.MapTargetCarryM lifecycle
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void MapTargetCarryM_DefaultsToNoTarget()
        {
            var go = new GameObject("TestShotController");
            try
            {
                var sc = go.AddComponent<ShotController>();
                Assert.AreEqual(-1f, sc.MapTargetCarryM,
                    "A shot with no map session must start markerless.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CommittedShot_ClearsMapTarget()
        {
            var go = new GameObject("TestShotController");
            try
            {
                var sc = go.AddComponent<ShotController>();
                sc.MapTargetCarryM = 137.5f;   // as written by MapViewController.CloseImmediate

                bool fired = false;
                sc.OnShotResolved += (_, __) => fired = true;

                sc.BeginExternalDrag();
                sc.SetExternalPower(0.8f, 0f);
                sc.EndExternalDrag();          // → CommitFlick (no touch samples → gate passes)

                Assert.IsTrue(fired, "Precondition: the shot must actually have committed.");
                Assert.AreEqual(-1f, sc.MapTargetCarryM,
                    "One marker per mapped shot — the target must not survive the stroke.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FumbledFlick_KeepsMapTarget()
        {
            // CompleteShot() -> TransitionToIdle is the SAME path a slow flick / arrow timeout
            // takes. Clearing there would delete the marker the player just placed without
            // ever taking the shot, so the reset lives in CommitFlick instead.
            var go = new GameObject("TestShotController");
            try
            {
                var sc = go.AddComponent<ShotController>();
                sc.MapTargetCarryM = 137.5f;

                sc.CompleteShot();   // TransitionToIdle, no commit

                Assert.AreEqual(137.5f, sc.MapTargetCarryM, kEpsilon,
                    "A reset back to pull-back must not eat the mapped target.");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
