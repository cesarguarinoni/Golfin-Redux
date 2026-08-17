// ─────────────────────────────────────────────────────────────────────────────
// ScheduleRefreshTests — refetch-on-screen-entry (Quick spec tournament_schedule_refresh)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// Same reflection access pattern, and for the same reason, as RemoteScheduleTests
// and TournamentServiceWireupTests: the production types under test
// (ScheduleRefreshThrottle, TournamentService, TournamentSelectionScreenController)
// live in Assembly-CSharp, which an asmdef cannot reference. `Prod` and `Fixtures`
// are the shared helpers declared in RemoteScheduleTests.cs — same namespace, same
// assembly, so they are reused rather than duplicated here.
//
// COVERAGE
//   §1  Throttle    — in-flight guard, cooldown, the five-bounces-one-request case
//                     (acceptance 5), and the failed-fetch-arms-the-cooldown case
//                     (acceptance 4, no retry storm)
//   §2  Vanishing   — SPEC §3.3 at the SCREEN level, not the mapper's: a real
//                     LocalTournamentBackend with a real persisted entry survives a
//                     payload that drops it, and the screen still builds its card
//                     (acceptance 3). A NON-entered row correctly disappears, and
//                     TournamentService.TryGetTournament is what keeps a signup modal
//                     holding that id out of KeyNotFoundException.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Tournaments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Golfin.Tournaments.WireupTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // §1 ScheduleRefreshThrottle — the "may I fetch?" decision
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Reflection wrapper over the Assembly-CSharp <c>ScheduleRefreshThrottle</c>.</summary>
    internal sealed class ThrottleUnderTest
    {
        private static readonly Type T = Prod.Find("Golfin.Tournaments.ScheduleRefreshThrottle");

        private readonly object _it;

        public ThrottleUnderTest(double cooldownSeconds)
            => _it = Activator.CreateInstance(T, new object[] { cooldownSeconds })!;

        public static double DefaultCooldownSeconds =>
            (double)T.GetField("DefaultCooldownSeconds", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        public bool TryBegin(double now)
            => (bool)T.GetMethod("TryBegin")!.Invoke(_it, new object[] { now })!;

        public void Settle(double now)
            => T.GetMethod("Settle")!.Invoke(_it, new object[] { now });

        public void Reset() => T.GetMethod("Reset")!.Invoke(_it, Array.Empty<object>());

        public bool InFlight => (bool)T.GetProperty("InFlight")!.GetValue(_it)!;

        public double SecondsUntilAllowed(double now)
            => (double)T.GetMethod("SecondsUntilAllowed")!.Invoke(_it, new object[] { now })!;
    }

    public class ScheduleRefreshThrottleTests
    {
        private const double Cooldown = 60.0;

        [Test]
        public void TheFirstFetchOfASessionIsAllowedAtTimeZero()
        {
            var t = new ThrottleUnderTest(Cooldown);

            // t=0 is a real value of Time.realtimeSinceStartup at boot. A "last fetched at 0"
            // initial value would make 0 - 0 = 0 < cooldown and block the boot fetch outright.
            Assert.IsTrue(t.TryBegin(0.0), "The boot fetch must not be throttled.");
            Assert.IsTrue(t.InFlight);
        }

        [Test]
        public void ReEntryWhileAFetchIsStillInFlightDoesNotQueueASecond()
        {
            var t = new ThrottleUnderTest(Cooldown);
            Assert.IsTrue(t.TryBegin(0.0));

            // The player left and came back while the request was still on the wire. Even though
            // the cooldown has long expired, there is nothing a second concurrent request adds.
            Assert.IsFalse(t.TryBegin(500.0), "An in-flight fetch must suppress a second one.");
        }

        [Test]
        public void AFetchSettlingReleasesTheInFlightGuard()
        {
            var t = new ThrottleUnderTest(Cooldown);
            t.TryBegin(0.0);
            t.Settle(2.0);

            Assert.IsFalse(t.InFlight);
            Assert.IsTrue(t.TryBegin(2.0 + Cooldown), "Past the cooldown, the next fetch is allowed.");
        }

        [Test]
        public void ReEntryInsideTheCooldownIsAnsweredFromMemory()
        {
            var t = new ThrottleUnderTest(Cooldown);
            t.TryBegin(0.0);
            t.Settle(1.0);

            Assert.IsFalse(t.TryBegin(2.0));
            Assert.IsFalse(t.TryBegin(60.0));
            Assert.IsFalse(t.TryBegin(60.9), "Still 0.1s short of the cooldown.");
            Assert.IsTrue(t.TryBegin(61.0), "Exactly one cooldown after settling, a fetch is due.");
        }

        [Test]
        public void FiveScreenEntriesInTenSecondsProduceExactlyOneRequest()
        {
            // ACCEPTANCE 5. Home → T7 → Home → T7 … five times in ten seconds.
            var t = new ThrottleUnderTest(Cooldown);
            int requests = 0;

            for (int i = 0; i < 5; i++)
            {
                double now = i * 2.0;                       // an entry every two seconds
                if (t.TryBegin(now)) { requests++; t.Settle(now + 0.3); }  // 300ms round trip
            }

            Assert.AreEqual(1, requests,
                "Bouncing between Home and Tournaments must not be one network request per bounce.");
        }

        [Test]
        public void AFailedFetchArmsTheCooldownToo()
        {
            // ACCEPTANCE 4, the half that is easy to miss. In airplane mode UnityWebRequest fails
            // almost immediately, so a cooldown armed only by SUCCESS would let five screen entries
            // fire five requests — the exact retry storm the spec forbids. Settle() is called from a
            // `finally`, so the failure path arms it identically.
            var t = new ThrottleUnderTest(Cooldown);
            int requests = 0;

            for (int i = 0; i < 5; i++)
            {
                double now = i * 2.0;
                if (t.TryBegin(now)) { requests++; t.Settle(now + 0.05); }  // instant offline failure
            }

            Assert.AreEqual(1, requests, "A failed fetch must arm the cooldown, or offline is a storm.");
        }

        [Test]
        public void SettleWithoutABeginIsHarmless()
        {
            var t = new ThrottleUnderTest(Cooldown);
            Assert.DoesNotThrow(() => t.Settle(10.0));
            Assert.IsFalse(t.InFlight);
            Assert.IsFalse(t.TryBegin(11.0), "The stamp still counts — no free fetch.");
        }

        [Test]
        public void ResetReturnsToTheNeverFetchedState()
        {
            var t = new ThrottleUnderTest(Cooldown);
            t.TryBegin(0.0);
            t.Reset();

            Assert.IsFalse(t.InFlight);
            Assert.IsTrue(t.TryBegin(0.0));
        }

        [Test]
        public void SecondsUntilAllowedCountsDownAndFloorsAtZero()
        {
            var t = new ThrottleUnderTest(Cooldown);
            t.TryBegin(0.0);
            t.Settle(0.0);

            Assert.AreEqual(60.0, t.SecondsUntilAllowed(0.0),  0.0001);
            Assert.AreEqual(15.0, t.SecondsUntilAllowed(45.0), 0.0001);
            Assert.AreEqual(0.0,  t.SecondsUntilAllowed(999.0), 0.0001);
        }

        [Test]
        public void ANegativeCooldownIsRejectedAtConstruction()
        {
            // Reflection wraps the real exception in a TargetInvocationException.
            var ex = Assert.Throws<TargetInvocationException>(() => new ThrottleUnderTest(-1.0));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(ex!.InnerException);
        }

        [Test]
        public void TheCooldownLivesInExactlyOneNamedConstant()
        {
            // SPEC §3.1: "one named constant with a comment, not scattered." TournamentService must
            // read its cooldown from the throttle's default rather than declare a second literal that
            // can drift away from it.
            var svc = Prod.Find("Golfin.Tournaments.TournamentService");
            var f   = svc.GetField("ScheduleRefreshCooldownSeconds",
                          BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(f, "TournamentService.ScheduleRefreshCooldownSeconds is the caller-side name.");
            Assert.AreEqual(ThrottleUnderTest.DefaultCooldownSeconds, (double)f!.GetRawConstantValue()!,
                "The service constant and the throttle default must be the same number.");
            Assert.AreEqual(60.0, ThrottleUnderTest.DefaultCooldownSeconds, "60s is the specified value.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2 Disappearance at the SCREEN level (SPEC §3.3, acceptance 2 and 3)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The mapper already has pure tests for <c>MergePreservingEntered</c> (RemoteScheduleTests
    /// §2b). These run the same case one layer up, through a REAL
    /// <see cref="LocalTournamentBackend"/> with a REAL persisted entry — because the thing that
    /// actually breaks is not the merge in isolation, it is
    /// <c>GetTournament(id)</c> throwing <see cref="KeyNotFoundException"/> in the paths the screen
    /// and the signup modal take afterwards.
    /// <para>
    /// With the admin's Activate/Deactivate switch live, a deactivated tournament is simply absent
    /// from the payload — so "the server no longer sends this row" is now routine rather than rare.
    /// </para>
    /// </summary>
    public class VanishedTournamentTests
    {
        private sealed class FixedTestClock : ITournamentClock
        {
            public DateTime UtcNow { get; set; }
            public FixedTestClock(DateTime utcNow) { UtcNow = utcNow; }
        }

        // Inside the window the fixture payload uses (2026-08-09 → 2026-08-25).
        private static readonly DateTime Start = new DateTime(2026, 8, 9,  0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime End   = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Now   = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

        private IReadOnlyDictionary<string, BotFieldConfig> _fields = null!;

        [SetUp]
        public void SetUp() => _fields = Fixtures.BotFields("field_major");

        private static TournamentDefinition Def(string id) =>
            new TournamentDefinition(
                id, "tourn." + id, "kasumigaseki", new[] { "1", "2", "3" },
                Start, End, 30, 0L, id, "field_major", "GOLFIN", "GOLD");

        private static LocalTournamentBackend Backend(
            IReadOnlyList<TournamentDefinition> defs,
            IReadOnlyDictionary<string, PrizeTable> prizes,
            IReadOnlyDictionary<string, BotFieldConfig> fields,
            ITournamentEntryStore store)
            => new LocalTournamentBackend(
                definitions: defs,
                prizeTables: prizes,
                botFields:   fields,
                botGen:      new BotFieldGenerator(new List<FakePlayerRow>(), new List<BotScoreBracketRow>()),
                clock:       new FixedTestClock(Now),
                store:       store,
                rp:          new FakeRewardPointsService(10_000L),
                items:       new FakeItemRewardService(),
                pars:        new FakeHoleParProvider(4));

        /// <summary>
        /// The in-play schedule: one row the player enters, one the admin will deactivate, one that
        /// stays. Mirrors what <c>TournamentService</c> holds when the screen opens.
        /// </summary>
        private static (IReadOnlyList<TournamentDefinition> Defs,
                        IReadOnlyDictionary<string, PrizeTable> Prizes) InPlay(params string[] ids)
        {
            var defs   = new List<TournamentDefinition>();
            var prizes = new Dictionary<string, PrizeTable>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                defs.Add(Def(id));
                prizes[id] = new PrizeTable(id, new[] { new PrizeBand(1, 1, 999L) });
            }
            return (defs, prizes);
        }

        /// <summary>
        /// Runs the production refresh path end to end for a payload that contains only
        /// <paramref name="stillServedIds"/>: map → MergePreservingEntered (with the LIVE backend's
        /// entry lookup, exactly as <c>TournamentService.PreserveEnteredTournaments</c> does) →
        /// recompose the backend over the SAME entry store.
        /// </summary>
        private (LocalTournamentBackend Backend, IReadOnlyDictionary<string, PrizeTable> Prizes) Refetch(
            LocalTournamentBackend current,
            IReadOnlyDictionary<string, PrizeTable> currentPrizes,
            ITournamentEntryStore store,
            params string[] stillServedIds)
        {
            var rows = new string[stillServedIds.Length];
            for (int i = 0; i < stillServedIds.Length; i++)
                rows[i] = Fixtures.Tournament(slug: stillServedIds[i], nameKey: "tourn." + stillServedIds[i]);

            object incoming = Prod.MapJsonRaw(Fixtures.Envelope(rows), _fields)!;

            var (defs, prizes) = Prod.Merge(
                incoming,
                current.GetTournaments(),
                currentPrizes,
                id => current.GetMyEntry(id) != null);

            return (Backend(defs, prizes, _fields, store), prizes);
        }

        // ── Acceptance 3 — an ENTERED tournament survives deactivation ────────

        [Test]
        public void AnEnteredTournamentSurvivesAPayloadThatDropsIt()
        {
            var (defs, prizes) = InPlay("entered_one", "still_there");
            var store   = new InMemoryEntryStore();
            var backend = Backend(defs, prizes, _fields, store);

            backend.Register("entered_one", 0L, "char_ai");
            Assert.IsNotNull(backend.GetMyEntry("entered_one"), "Precondition: the player is entered.");

            LogAssert.ignoreFailingMessages = true;   // the carry-forward warns on purpose
            var (refreshed, refreshedPrizes) = Refetch(backend, prizes, store, "still_there");
            LogAssert.ignoreFailingMessages = false;

            // THE acceptance: no KeyNotFoundException anywhere.
            Assert.DoesNotThrow(() => refreshed.GetTournament("entered_one"),
                "An entered tournament dropped by the server must still resolve — every " +
                "GetTournament(id) after it throws otherwise (signup modal, result modal, round " +
                "handler, SubmitHoleResult mid-round).");

            Assert.IsNotNull(refreshed.GetMyEntry("entered_one"), "The entry itself must survive too.");
            Assert.IsTrue(refreshedPrizes.ContainsKey("entered_one"),
                "Its prize table must be carried across, or GetTopPrizeRP silently reads 0.");
            Assert.AreEqual(999L, refreshedPrizes["entered_one"].Bands[0].RpReward,
                "And it must be the ladder the player registered against, not a server replacement.");
        }

        [Test]
        public void AnEnteredTournamentThatVanishedStillBuildsItsCardOnTheScreen()
        {
            // Acceptance 3's "still listed, still playable" half, at the screen's own layer:
            // this is the exact sequence RebuildCards() runs per definition.
            var (defs, prizes) = InPlay("entered_one", "still_there");
            var store   = new InMemoryEntryStore();
            var backend = Backend(defs, prizes, _fields, store);
            backend.Register("entered_one", 0L, "char_ai");

            LogAssert.ignoreFailingMessages = true;
            var (refreshed, _) = Refetch(backend, prizes, store, "still_there");
            LogAssert.ignoreFailingMessages = false;

            var listed = new List<string>();
            foreach (var d in refreshed.GetTournaments()) listed.Add(d.Id);
            CollectionAssert.Contains(listed, "entered_one", "The card is built from GetTournaments().");

            var def   = refreshed.GetTournament("entered_one");
            var entry = refreshed.GetMyEntry("entered_one");
            var state = refreshed.DeriveState(def, Now);

            Assert.AreEqual(TournamentState.Playing, state);
            Assert.AreEqual("EnteredActive", ScreenCardStateName(state, entry!.Status, nowPastEnd: false),
                "The player must get their playable CONTINUE card back, not a dead row.");
        }

        // ── Acceptance 2 — a NON-entered tournament correctly disappears ──────

        [Test]
        public void ANonEnteredTournamentIsGoneAfterTheServerStopsSendingIt()
        {
            var (defs, prizes) = InPlay("deactivated_one", "still_there");
            var store   = new InMemoryEntryStore();
            var backend = Backend(defs, prizes, _fields, store);

            var (refreshed, _) = Refetch(backend, prizes, store, "still_there");

            var listed = new List<string>();
            foreach (var d in refreshed.GetTournaments()) listed.Add(d.Id);

            CollectionAssert.DoesNotContain(listed, "deactivated_one",
                "Without an entry, an admin deactivation must actually remove the card.");
            CollectionAssert.Contains(listed, "still_there");
        }

        [Test]
        public void GetTournamentThrowsForAnIdThatLeftTheSchedule()
        {
            // Documents the hazard the signup-modal guard exists for: the id a modal is holding is
            // NOT protected by MergePreservingEntered, because the player has not entered yet.
            var (defs, prizes) = InPlay("deactivated_one", "still_there");
            var store   = new InMemoryEntryStore();
            var backend = Backend(defs, prizes, _fields, store);

            var (refreshed, _) = Refetch(backend, prizes, store, "still_there");

            Assert.Throws<KeyNotFoundException>(() => refreshed.GetTournament("deactivated_one"));
        }

        [Test]
        public void TryGetTournamentReturnsNullWhereGetTournamentThrows()
        {
            // The signup modal's guard (SPEC §3.3 ⚠️). Open() and OnConfirm() both go through this,
            // so a modal left open across a refresh that dropped its tournament toasts and closes
            // instead of throwing KeyNotFoundException from inside a button handler.
            var (defs, prizes) = InPlay("deactivated_one", "still_there");
            var store   = new InMemoryEntryStore();
            var (refreshed, _) = Refetch(Backend(defs, prizes, _fields, store), prizes, store, "still_there");

            var service = MakeUnstartedService(refreshed);

            Assert.IsNull(TryGetTournament(service, "deactivated_one"),
                "A vanished id must read as absent, not as an exception in a button handler.");
            Assert.IsNull(TryGetTournament(service, null));
            Assert.IsNull(TryGetTournament(service, string.Empty));
            Assert.IsNotNull(TryGetTournament(service, "still_there"),
                "And the lookup must still find a live tournament, or every signup breaks.");
        }

        // ── Reflection helpers (Assembly-CSharp) ─────────────────────────────

        /// <summary>
        /// A <c>TournamentService</c> with its <c>Backend</c> injected and <c>Awake</c> never run —
        /// the component is added to an INACTIVE GameObject, so no CSV load, no coroutine, no
        /// singleton assignment, and nothing for another test in this Editor session to trip over.
        /// </summary>
        private static Component MakeUnstartedService(ITournamentBackend backend)
        {
            var go = new GameObject("TEST_TournamentService_NoAwake");
            go.SetActive(false);

            var type = Prod.Find("Golfin.Tournaments.TournamentService");
            var comp = go.AddComponent(type);
            Assert.IsNotNull(comp, "AddComponent(TournamentService) returned null.");

            type.GetField("<Backend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(comp, backend);

            return comp!;
        }

        private static TournamentDefinition? TryGetTournament(Component service, string? id)
            => (TournamentDefinition?)service.GetType()
                .GetMethod("TryGetTournament", BindingFlags.Instance | BindingFlags.Public)!
                .Invoke(service, new object?[] { id });

        /// <summary>
        /// <c>TournamentSelectionScreenController.MapCardState</c> — the screen's own mapping, by
        /// name, since the returned enum is nested in Assembly-CSharp.
        /// </summary>
        private static string ScreenCardStateName(
            TournamentState state, EntryStatus entryStatus, bool nowPastEnd)
        {
            var screen = Prod.Find("GolfinRedux.UI.Tournaments.TournamentSelectionScreenController");
            var m = screen.GetMethod("MapCardState", BindingFlags.Public | BindingFlags.Static)!;
            object result = m.Invoke(null, new object[] { state, entryStatus, nowPastEnd })!;
            return Enum.GetName(result.GetType(), result)!;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in GameObject.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go != null && go.name == "TEST_TournamentService_NoAwake")
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
