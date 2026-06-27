// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — TournamentAdapterTests
// EditMode NUnit suite verifying the CONTRACTS the three production adapters
// implement (tournament_backend_bootstrap).
//
// Constraint: the concrete adapter types (RewardPointsServiceAdapter,
// ItemRewardServiceAdapter, HoleParProviderAdapter) live in Assembly-CSharp
// (TournamentsRuntime/ has no .asmdef) and are NOT visible from this named
// test asmdef. Tests therefore use the Fake* doubles from Golfin.Tournaments
// to pin the interface contracts, plus local inline stubs where the fakes
// don't cover a spec requirement. Structural adapter checks (implements
// interface, qty<=0 no-op on real SaveDataHost) run via PlayMode script-execute
// in the smoke step.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Golfin.Tournaments.Tests
{
    // ═════════════════════════════════════════════════════════════════════════
    // A. RP "ToInt" overflow-guard contract
    //    Spec §2: "if amt > int.MaxValue log error + clamp; [clamp negatives]"
    //    Mirrored here to pin the spec contract independently of the impl.
    //    (Impl is tested via PlayMode smoke which logs adapter output.)
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public sealed class RpToIntContractTests
    {
        // Local mirror of RewardPointsServiceAdapter.ToInt spec contract.
        private static int ToIntContract(long amt)
        {
            if (amt > int.MaxValue) return int.MaxValue;
            if (amt < 0) return 0;
            return (int)amt;
        }

        [Test] public void NormalValue_PassesThrough()    => Assert.AreEqual(500,           ToIntContract(500L));
        [Test] public void IntMaxValue_IsPreserved()      => Assert.AreEqual(int.MaxValue,  ToIntContract((long)int.MaxValue));
        [Test] public void OverflowByOne_ClampsToIntMax() => Assert.AreEqual(int.MaxValue,  ToIntContract((long)int.MaxValue + 1L));
        [Test] public void LargeOverflow_ClampsToIntMax() => Assert.AreEqual(int.MaxValue,  ToIntContract(long.MaxValue));
        [Test] public void NegativeOne_ClampsToZero()     => Assert.AreEqual(0,             ToIntContract(-1L));
        [Test] public void MinLong_ClampsToZero()         => Assert.AreEqual(0,             ToIntContract(long.MinValue));
        [Test] public void Zero_ReturnsZero()             => Assert.AreEqual(0,             ToIntContract(0L));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // B. IRewardPointsService contract — via FakeRewardPointsService
    //    Tests TrySpend-when-short and balance accounting.
    //    (FakeRewardPointsService: TrySpend returns false when _balance < rp;
    //     Grant + TrySpend throw ArgumentOutOfRangeException for negative values.)
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public sealed class IRewardPointsServiceContractTests
    {
        [Test]
        public void TrySpend_WhenShort_ReturnsFalse()
        {
            var rp = new FakeRewardPointsService(initialBalance: 100L);
            bool result = rp.TrySpend(200L);
            Assert.IsFalse(result, "TrySpend must return false when balance < requested amount");
            Assert.AreEqual(100L, rp.Balance, "Balance must not change on a failed spend");
        }

        [Test]
        public void TrySpend_WhenSufficient_ReturnsTrueAndDeducts()
        {
            var rp = new FakeRewardPointsService(initialBalance: 500L);
            bool result = rp.TrySpend(200L);
            Assert.IsTrue(result);
            Assert.AreEqual(300L, rp.Balance);
        }

        [Test]
        public void TrySpend_ExactBalance_Succeeds()
        {
            var rp = new FakeRewardPointsService(initialBalance: 100L);
            bool result = rp.TrySpend(100L);
            Assert.IsTrue(result);
            Assert.AreEqual(0L, rp.Balance);
        }

        [Test]
        public void Grant_IncreasesBalance()
        {
            var rp = new FakeRewardPointsService(initialBalance: 50L);
            rp.Grant(25L);
            Assert.AreEqual(75L, rp.Balance);
        }

        [Test]
        public void TrySpend_ZeroBalance_ReturnsFalseForNonZeroAmt()
        {
            var rp = new FakeRewardPointsService(initialBalance: 0L);
            Assert.IsFalse(rp.TrySpend(1L));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // C. IItemRewardService contract — via FakeItemRewardService
    //    Tests the accumulation / dict-mutation contract.
    //    Note: FakeItemRewardService.Grant guards null/empty itemId (no-op)
    //    but does NOT guard qty<=0 — the real adapter's qty guard is tested
    //    via PlayMode script-execute smoke.
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public sealed class IItemRewardServiceContractTests
    {
        [Test]
        public void Grant_IncrementsExistingKey()
        {
            var svc = new FakeItemRewardService();
            svc.Grant("trophy_gold", 1);
            svc.Grant("trophy_gold", 2);
            Assert.AreEqual(3, svc.Granted["trophy_gold"],
                "Repeated Grant calls must accumulate into the same key");
        }

        [Test]
        public void Grant_CreatesNewKeyIfMissing()
        {
            var svc = new FakeItemRewardService();
            svc.Grant("new_item_xyz", 5);
            Assert.IsTrue(svc.Granted.ContainsKey("new_item_xyz"),
                "Grant must create the key when it does not exist");
            Assert.AreEqual(5, svc.Granted["new_item_xyz"]);
        }

        [Test]
        public void Grant_NullItemId_IsNoOp()
        {
            var svc = new FakeItemRewardService();
            Assert.DoesNotThrow(() => svc.Grant(null!, 5),
                "Grant with null itemId must not throw");
            Assert.AreEqual(0, svc.Granted.Count,
                "Grant with null itemId must be a no-op (no key created)");
        }

        [Test]
        public void Grant_EmptyItemId_IsNoOp()
        {
            var svc = new FakeItemRewardService();
            Assert.DoesNotThrow(() => svc.Grant("", 5));
            Assert.AreEqual(0, svc.Granted.Count,
                "Grant with empty itemId must be a no-op");
        }

        [Test]
        public void Grant_MultipleItems_TrackedSeparately()
        {
            var svc = new FakeItemRewardService();
            svc.Grant("item_a", 3);
            svc.Grant("item_b", 7);
            Assert.AreEqual(3, svc.Granted["item_a"]);
            Assert.AreEqual(7, svc.Granted["item_b"]);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // D. IHoleParProvider contract — par order + throw-on-unknown
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public sealed class IHoleParProviderContractTests
    {
        [Test]
        public void ParsFor_ReturnsParInOrder()
        {
            var provider = new FakeHoleParProvider(par: 4);
            var holeSet = new List<string> { "1", "2", "3" };
            var pars = provider.ParsFor("kasumigaseki", holeSet);

            Assert.AreEqual(3, pars.Count,
                "ParsFor must return exactly one value per hole in holeSet");
            Assert.AreEqual(4, pars[0]);
            Assert.AreEqual(4, pars[1]);
            Assert.AreEqual(4, pars[2]);
        }

        [Test]
        public void ParsFor_ResultCountMatchesHoleSetCount()
        {
            var provider = new FakeHoleParProvider(par: 5);
            var pars = provider.ParsFor("lomond", new List<string> { "10", "11", "18" });
            Assert.AreEqual(3, pars.Count,
                "ParsFor must return count == holeSet.Count");
        }

        [Test]
        public void ParsFor_EmptyHoleSet_ReturnsEmpty()
        {
            var provider = new FakeHoleParProvider(par: 4);
            var pars = provider.ParsFor("course", new List<string>());
            Assert.AreEqual(0, pars.Count,
                "Empty holeSet must return empty par list, not throw");
        }

        /// <summary>
        /// Real adapter contract: ParsFor MUST throw InvalidOperationException
        /// on an unknown hole id. Verified here via a local strict stub
        /// (FakeHoleParProvider never throws; real adapter does).
        /// </summary>
        [Test]
        public void ParsFor_UnknownHoleId_ThrowsInvalidOperationException()
        {
            var provider = new StrictFakeHoleParProvider(
                knownHoles: new Dictionary<string, int> { { "1", 4 }, { "2", 3 } });

            // Known holes — should succeed
            var pars = provider.ParsFor("course", new List<string> { "1", "2" });
            Assert.AreEqual(2, pars.Count);
            Assert.AreEqual(4, pars[0]);
            Assert.AreEqual(3, pars[1]);

            // Unknown hole — must throw
            var ex = Assert.Throws<InvalidOperationException>(
                () => provider.ParsFor("course", new List<string> { "1", "UNKNOWN_99" }),
                "ParsFor must throw InvalidOperationException on an unknown hole id");
            StringAssert.Contains("UNKNOWN_99", ex.Message,
                "Exception must identify the offending hole id");
        }

        [Test]
        public void ParsFor_VerifiesOrderNotJustCount()
        {
            // Use a strict provider with heterogeneous pars to catch order-swap bugs.
            var provider = new StrictFakeHoleParProvider(new Dictionary<string, int>
            {
                { "1", 4 }, { "2", 5 }, { "3", 3 }
            });
            var pars = provider.ParsFor("course", new List<string> { "3", "1", "2" });
            Assert.AreEqual(3, pars[0], "Hole '3' (par 3) must be first in result");
            Assert.AreEqual(4, pars[1], "Hole '1' (par 4) must be second in result");
            Assert.AreEqual(5, pars[2], "Hole '2' (par 5) must be third in result");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Local test double: strict IHoleParProvider that throws on unknown id.
    // Mirrors the real HoleParProviderAdapter's throw-on-missing-hole contract.
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class StrictFakeHoleParProvider : IHoleParProvider
    {
        private readonly IReadOnlyDictionary<string, int> _knownHoles;

        public StrictFakeHoleParProvider(Dictionary<string, int> knownHoles)
            => _knownHoles = knownHoles;

        public IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet)
        {
            var result = new int[holeSet.Count];
            for (int i = 0; i < holeSet.Count; i++)
            {
                if (!_knownHoles.TryGetValue(holeSet[i], out int par))
                    throw new InvalidOperationException(
                        $"StrictFakeHoleParProvider: unknown hole id '{holeSet[i]}'");
                result[i] = par;
            }
            return result;
        }
    }
}
