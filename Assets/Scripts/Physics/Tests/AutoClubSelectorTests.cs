using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// auto_club_selection: unit tests for AutoClubSelector.SelectBestClub.
    /// Pure logic — no scene setup, no ClubContext statics, no [SetUp] beyond the fixtures.
    /// Mirrors PutterModeSurfaceControllerTests in style.
    /// </summary>
    public class AutoClubSelectorTests
    {
        // LabClubs indices (PhysicsLabController.LabClubs): 0=Driver/Wood, 1=Iron, 2=Wedge, 3=Putter.
        const int LabDriver = 0;
        const int LabIron   = 1;
        const int LabWedge  = 2;
        const int LabPutter = 3;

        static ClubEntry Club(string id, int distanceYd, int labIdx, bool isDriver = false) => new ClubEntry
        {
            ClubId       = id,
            TypeLabel    = id.ToUpperInvariant(),
            Distance     = distanceYd,
            LabClubIndex = labIdx,
            IsDriver     = isDriver,
        };

        /// <summary>Default bag: Driver 250, Wood 220, Iron 150, P.Wedge 100, Putter 10.</summary>
        static List<ClubEntry> FullBag() => new List<ClubEntry>
        {
            /* 0 */ Club("club_driver_gf",      250, LabDriver, isDriver: true),
            /* 1 */ Club("club_wood_gf",        220, LabDriver),
            /* 2 */ Club("club_iron7_mireo",    150, LabIron),
            /* 3 */ Club("club_pwedge_royal",   100, LabWedge),
            /* 4 */ Club("club_putter_golfinx",  10, LabPutter),
        };

        /// <summary>Same bag with the driver removed (index 0 is now the Wood).</summary>
        static List<ClubEntry> BagWithoutDriver()
        {
            var bag = FullBag();
            bag.RemoveAt(0);
            return bag;
        }

        static float Yards(float yd) => yd / AutoClubSelector.YardsPerMeter;   // yards → meters

        // ── 1. Tee ────────────────────────────────────────────────────────────

        [Test]
        public void TeeShot_PicksTheDriver_RegardlessOfDistance()
        {
            var bag = FullBag();
            int idx = AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: true, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(0, idx);
            Assert.IsTrue(bag[idx].IsDriver, "Tee shot must select the driver entry.");
        }

        [Test]
        public void TeeShot_DriverNotAtIndexZero_StillFindsTheDriver()
        {
            var bag = new List<ClubEntry>
            {
                Club("club_iron7_mireo", 150, LabIron),
                Club("club_wood_gf",     220, LabDriver),
                Club("club_driver_gf",   250, LabDriver, isDriver: true),
            };
            Assert.AreEqual(2, AutoClubSelector.SelectBestClub(
                Yards(200f), isTeeShot: true, inPutterMode: false, bag, LabPutter));
        }

        [Test]
        public void TeeShot_NoDriverInBag_FallsThroughToDistanceRule()
        {
            var bag = BagWithoutDriver();   // Wood 220, Iron 150, Wedge 100, Putter 10
            // 120yd → shortest club that reaches is the Iron (150), NOT the Wood (220).
            int idx = AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: true, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(1, idx);
            Assert.AreEqual("club_iron7_mireo", bag[idx].ClubId);
        }

        // ── 2. Off the tee: never the driver ──────────────────────────────────

        [Test]
        public void OffTee_NeverReturnsTheDriver_EvenAtDriverDistance()
        {
            var bag = FullBag();
            int idx = AutoClubSelector.SelectBestClub(
                Yards(245f), isTeeShot: false, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(1, idx, "245yd is inside driver range but off the tee the Wood must win.");
            Assert.IsFalse(bag[idx].IsDriver);
        }

        [Test]
        public void OffTee_OvershootsEveryClub_PicksLongestNonDriver()
        {
            var bag = FullBag();
            // 400yd is beyond every club including the driver.
            int idx = AutoClubSelector.SelectBestClub(
                Yards(400f), isTeeShot: false, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(1, idx, "Nothing reaches → longest NON-driver (Wood 220), not the Driver.");
            Assert.IsFalse(bag[idx].IsDriver);
        }

        // ── 3. Shortest club that still reaches ───────────────────────────────

        [Test]
        public void OffTee_PicksShortestClubThatReaches()
        {
            var bag = FullBag();
            // 120yd: Wedge (100) is short; Iron (150) is the shortest that reaches.
            Assert.AreEqual(2, AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: false, inPutterMode: false, bag, LabPutter));
        }

        [Test]
        public void OffTee_ExactMatchOnClubDistance_PicksThatClub()
        {
            var bag = FullBag();
            // Distance >= distYd is inclusive, so exactly 100yd picks the 100yd wedge.
            Assert.AreEqual(3, AutoClubSelector.SelectBestClub(
                Yards(100f), isTeeShot: false, inPutterMode: false, bag, LabPutter));
        }

        [Test]
        public void OffTee_ShortShot_PicksTheWedge_NotThePutter()
        {
            var bag = FullBag();
            // 5yd chip: the Putter (10) is the only club that "reaches", but it is excluded,
            // so the shortest eligible club wins.
            int idx = AutoClubSelector.SelectBestClub(
                Yards(5f), isTeeShot: false, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(3, idx);
            Assert.AreNotEqual(LabPutter, bag[idx].LabClubIndex, "The putter must never be auto-picked off the green.");
        }

        [Test]
        public void OffTee_TieOnDistance_LowestBagIndexWins()
        {
            var bag = new List<ClubEntry>
            {
                Club("club_iron7_a", 150, LabIron),
                Club("club_iron7_b", 150, LabIron),
            };
            Assert.AreEqual(0, AutoClubSelector.SelectBestClub(
                Yards(150f), isTeeShot: false, inPutterMode: false, bag, LabPutter));
        }

        [Test]
        public void OffTee_TieOnLongest_LowestBagIndexWins()
        {
            var bag = new List<ClubEntry>
            {
                Club("club_wood_a", 220, LabDriver),
                Club("club_wood_b", 220, LabDriver),
            };
            // 400yd → nothing reaches → longest, tie broken by lowest index.
            Assert.AreEqual(0, AutoClubSelector.SelectBestClub(
                Yards(400f), isTeeShot: false, inPutterMode: false, bag, LabPutter));
        }

        // ── 4. Meters → yards conversion ──────────────────────────────────────

        [Test]
        public void Distance_IsConvertedMetersToYards()
        {
            // 100m = 109.361yd. A meters-as-yards bug would pick the 100yd club.
            var bag = new List<ClubEntry>
            {
                /* 0 */ Club("club_100yd", 100, LabWedge),
                /* 1 */ Club("club_110yd", 110, LabIron),
                /* 2 */ Club("club_150yd", 150, LabIron),
            };
            int idx = AutoClubSelector.SelectBestClub(
                100f, isTeeShot: false, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(1, idx, "100m = 109.4yd → the 110yd club, not the 100yd club.");
        }

        [Test]
        public void YardsPerMeter_MatchesTheProjectConstant()
        {
            Assert.AreEqual(1.09361f, AutoClubSelector.YardsPerMeter, 1e-6f);
        }

        // ── 5. Green / §2f deference ──────────────────────────────────────────

        [Test]
        public void InPutterMode_ReturnsNoChange()
        {
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(8f), isTeeShot: false, inPutterMode: true, FullBag(), LabPutter));
        }

        [Test]
        public void InPutterMode_OnTee_StillReturnsNoChange()
        {
            // Putter mode outranks the tee rule — §2f always wins.
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(400f), isTeeShot: true, inPutterMode: true, FullBag(), LabPutter));
        }

        // ── 6. Degenerate bags ────────────────────────────────────────────────

        [Test]
        public void EmptyBag_ReturnsNoChange()
        {
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: false, inPutterMode: false, new List<ClubEntry>(), LabPutter));
        }

        [Test]
        public void NullBag_ReturnsNoChange()
        {
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: false, inPutterMode: false, null, LabPutter));
        }

        [Test]
        public void EmptyBag_OnTee_ReturnsNoChange()
        {
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: true, inPutterMode: false, new List<ClubEntry>(), LabPutter));
        }

        [Test]
        public void BagOfDriverAndPutterOnly_OffTee_ReturnsNoChange()
        {
            var bag = new List<ClubEntry>
            {
                Club("club_driver_gf",     250, LabDriver, isDriver: true),
                Club("club_putter_golfinx", 10, LabPutter),
            };
            Assert.AreEqual(-1, AutoClubSelector.SelectBestClub(
                Yards(120f), isTeeShot: false, inPutterMode: false, bag, LabPutter),
                "No eligible candidate (driver excluded, putter excluded) → leave selection alone.");
        }

        [Test]
        public void BagOfDriverAndPutterOnly_OnTee_StillPicksTheDriver()
        {
            var bag = new List<ClubEntry>
            {
                Club("club_driver_gf",     250, LabDriver, isDriver: true),
                Club("club_putter_golfinx", 10, LabPutter),
            };
            Assert.AreEqual(0, AutoClubSelector.SelectBestClub(
                Yards(300f), isTeeShot: true, inPutterMode: false, bag, LabPutter));
        }

        [Test]
        public void WoodIsEligibleOffTee_EvenThoughItSharesTheDriverLabIndex()
        {
            var bag = FullBag();
            int idx = AutoClubSelector.SelectBestClub(
                Yards(200f), isTeeShot: false, inPutterMode: false, bag, LabPutter);
            Assert.AreEqual(1, idx);
            Assert.AreEqual(LabDriver, bag[idx].LabClubIndex,
                "The Wood shares lab index 0 with the Driver — exclusion must key off IsDriver, not the lab index.");
        }
    }
}
