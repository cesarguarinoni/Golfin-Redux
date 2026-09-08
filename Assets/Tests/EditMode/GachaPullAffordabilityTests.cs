// Assets/Tests/EditMode/GachaPullAffordabilityTests.cs
// polish_regressions_0909 R1 — the PULL buttons go dead when the balance cannot pay for them.
//
// WHAT THIS PINS, and why it is a test rather than a screenshot: a pull the player cannot afford
// used to be indistinguishable from one they could. The tap opened the reveal modal, shook the bag
// for the length of a round trip, and closed it again on the server's `insufficient` verdict —
// which reads as the REVEAL BEING CUT OFF a second in, not as a price the player cannot meet
// (Cesar, 2026-09-09). GachaPullFlow.CanAfford is the one predicate both PULL surfaces — the
// banner card's x1/x10 and the Prizes screen's "again" — now ask before they light a button, so
// it is the thing worth pinning: a second copy of this arithmetic is how the two surfaces would
// end up disagreeing about the same balance.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, overrideReferences:false). GachaPullFlow,
// GachaBannerEntry and GachaTicketManager all live in Assembly-CSharp, which an asmdef cannot
// reference, so every production call goes through System.Reflection — the same pattern as
// GachaClientRealPullTests, and for the same reason (feedback_tests_must_target_production_type:
// the seam under test is the SHIPPING one, never a copy of it living in the test file).
// Golfin.Save IS referenced, so SaveDataHost / SaveData / PersistedTicketBalance are normal types.

using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Save;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaPullAffordabilityTests
    {
        // ── Reflection handles ────────────────────────────────────────────────

        private static readonly Type FlowType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPullFlow, Assembly-CSharp");
        private static readonly Type EntryType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerEntry, Assembly-CSharp");
        private static readonly Type TicketManagerType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaTicketManager, Assembly-CSharp");

        private const BindingFlags Statics  = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private GameObject _saveGo;
        private GameObject _ticketGo;

        // ── Fixture ───────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(FlowType,          "GachaPullFlow not found in Assembly-CSharp.");
            Assert.IsNotNull(EntryType,         "GachaBannerEntry not found in Assembly-CSharp.");
            Assert.IsNotNull(TicketManagerType, "GachaTicketManager not found in Assembly-CSharp.");

            ClearLastPull();
            SetTicketManager(null);
            SetSaveHost(null);
        }

        [TearDown]
        public void TearDown()
        {
            ClearLastPull();
            SetTicketManager(null);
            SetSaveHost(null);

            if (_ticketGo != null) UnityEngine.Object.DestroyImmediate(_ticketGo);
            if (_saveGo   != null) UnityEngine.Object.DestroyImmediate(_saveGo);
            _ticketGo = null;
            _saveGo   = null;
        }

        // ── The predicate ─────────────────────────────────────────────────────

        [Test]
        public void CanAfford_WithNoEntry_IsFalse()
        {
            Assert.IsFalse(CanAfford(null, 1), "A pull with no banner cannot be afforded — or made.");
        }

        [Test]
        public void CanAfford_WithNoTicketLedgerInTheScene_IsTrue()
        {
            // The documented degrade: a missing singleton must not lock a player out of a pull they
            // can afford. The server is still there to price it and refuse.
            object entry = MakeEntry(costX1: 50, costX10: 450);
            Assert.IsTrue(CanAfford(entry, 1),  "No ledger must leave x1 live.");
            Assert.IsTrue(CanAfford(entry, 10), "No ledger must leave x10 live.");
        }

        [Test]
        public void CanAfford_WithAnEmptyBalance_RefusesBothPulls()
        {
            InstallLedger(standardBalance: 0);
            object entry = MakeEntry(costX1: 50, costX10: 450);

            Assert.IsFalse(CanAfford(entry, 1),  "0 tickets cannot buy a 50-ticket pull.");
            Assert.IsFalse(CanAfford(entry, 10), "0 tickets cannot buy a 450-ticket pull.");
        }

        [Test]
        public void CanAfford_PricesTheTwoButtonsSeparately()
        {
            // The load-bearing case: a balance that covers x1 but not x10 must leave x1 LIVE.
            // Gating both on the larger price would take away the pull the player can still make.
            InstallLedger(standardBalance: 60);
            object entry = MakeEntry(costX1: 50, costX10: 450);

            Assert.IsTrue (CanAfford(entry, 1),  "60 tickets covers the 50-ticket x1.");
            Assert.IsFalse(CanAfford(entry, 10), "60 tickets does not cover the 450-ticket x10.");
        }

        [Test]
        public void CanAfford_AtExactlyThePrice_IsTrue()
        {
            // GachaTicketManager.CanAfford is >=, and the boundary is the pull that empties the
            // wallet — the one a player is most likely to make.
            InstallLedger(standardBalance: 50);
            object entry = MakeEntry(costX1: 50, costX10: 450);

            Assert.IsTrue(CanAfford(entry, 1), "A balance equal to the price must still buy the pull.");
        }

        [Test]
        public void CanAfford_ReadsTheBannersOwnTicketType()
        {
            // The balance is per ticket type. A banner priced in a type the player holds none of is
            // unaffordable however many Standard tickets are in the wallet.
            InstallLedger(standardBalance: 5000);
            object entry = MakeEntry(costX1: 50, costX10: 450, ticketType: 1);

            Assert.IsFalse(CanAfford(entry, 1),
                "A Standard balance must not pay for a banner priced in another ticket type.");
        }

        // ── PULL AGAIN ────────────────────────────────────────────────────────

        [Test]
        public void CanPullAgain_WithNoPreviousPull_IsFalse()
        {
            InstallLedger(standardBalance: 5000);
            Assert.IsFalse(CanPullAgain(),
                "PULL AGAIN has nothing to repeat until a pull has been made — the button stays dead.");
        }

        [Test]
        public void CanPullAgain_RepricesTheLastPullsOwnCount()
        {
            // x10 was the last pull and the wallet no longer covers it, even though it would
            // comfortably cover an x1. "Again" means the SAME count, so it must be refused.
            InstallLedger(standardBalance: 60);
            SetLastPull(MakeEntry(costX1: 50, costX10: 450), count: 10);
            Assert.IsFalse(CanPullAgain(), "60 tickets cannot repeat a 450-ticket x10.");

            SetLastPull(MakeEntry(costX1: 50, costX10: 450), count: 1);
            Assert.IsTrue(CanPullAgain(), "60 tickets can repeat a 50-ticket x1.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool CanAfford(object entry, int count)
        {
            MethodInfo m = FlowType.GetMethod("CanAfford", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(m, "GachaPullFlow.CanAfford(GachaBannerEntry, int) not found.");
            return (bool)m.Invoke(null, new[] { entry, (object)count });
        }

        private static bool CanPullAgain()
        {
            MethodInfo m = FlowType.GetMethod("CanPullAgain", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(m, "GachaPullFlow.CanPullAgain() not found.");
            return (bool)m.Invoke(null, null);
        }

        private static object MakeEntry(int costX1, int costX10, int ticketType = 0,
                                        string bannerId = "banner_test")
        {
            object entry = Activator.CreateInstance(EntryType);
            EntryType.GetProperty("BannerId").SetValue(entry, bannerId);
            EntryType.GetProperty("CostX1").SetValue(entry, costX1);
            EntryType.GetProperty("CostX10").SetValue(entry, costX10);
            EntryType.GetProperty("TicketType").SetValue(entry, ticketType);
            return entry;
        }

        private static void SetLastPull(object entry, int count)
        {
            FlowType.GetField("_lastEntry", Statics).SetValue(null, entry);
            FlowType.GetField("_lastCount", Statics).SetValue(null, count);
        }

        private static void ClearLastPull() => SetLastPull(null, 1);

        /// <summary>
        /// Stand in for the boot: a SaveDataHost holding one Standard balance, and a
        /// GachaTicketManager pointed at it. Neither Awake runs in EditMode, so both singletons are
        /// installed the way the harness must — through their own private setters, on the real types.
        /// </summary>
        private void InstallLedger(int standardBalance)
        {
            _saveGo = new GameObject("TestSaveDataHost");
            var host = _saveGo.AddComponent<SaveDataHost>();
            var data = new SaveData
            {
                ticketBalances = new List<PersistedTicketBalance>
                {
                    new PersistedTicketBalance { ticketTypeInt = 0, balance = standardBalance },
                },
            };
            typeof(SaveDataHost)
                .GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, data);
            SetSaveHost(host);

            _ticketGo = new GameObject("TestGachaTicketManager");
            var mgr = _ticketGo.AddComponent(TicketManagerType);
            SetTicketManager(mgr);
        }

        private static void SetSaveHost(SaveDataHost host)
            => typeof(SaveDataHost).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                                   .GetSetMethod(nonPublic: true)
                                   .Invoke(null, new object[] { host });

        private static void SetTicketManager(object mgr)
            => TicketManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                                .GetSetMethod(nonPublic: true)
                                .Invoke(null, new[] { mgr });
    }
}
