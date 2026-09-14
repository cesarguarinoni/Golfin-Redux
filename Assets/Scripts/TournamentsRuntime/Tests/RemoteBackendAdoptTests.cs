// ─────────────────────────────────────────────────────────────────────────────
// RemoteBackendAdoptTests — finished_tournament_leaderboard_route F2 (EditMode)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (Editor-only). RemoteTournamentBackend lives in
// Assembly-CSharp, reached by REFLECTION through the AsyncProd helpers, exactly like
// TournamentAsyncBoardTests.
//
// The defect: TournamentService builds ONE RemoteTournamentBackend per session and reuses it
// across schedule swaps (on purpose — board snapshots and the submit queue live in it), but
// the wrapper kept delegating to the LocalTournamentBackend it was BUILT with. Every Apply()
// composes a new local backend, so a signed-in player never saw a schedule that landed after
// sign-in. RemoteTournamentBackend.Adopt(local, prizeTables) is the seam the service now uses;
// these tests pin what it must and must not change.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Economy;
using Golfin.Tournaments;
using NUnit.Framework;

namespace Golfin.Tournaments.WireupTests
{
    public sealed class RemoteBackendAdoptTests
    {
        private const BindingFlags AnyInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const string SecondSlug = "hirono_invitational";

        private static TournamentDefinition SecondDef() => new TournamentDefinition(
            id:                  SecondSlug,
            nameKey:             "TOURN_HIRONO",
            clubId:              "club_hirono",
            holeSet:             AsyncFixture.HoleSet,
            startUtc:            AsyncFixture.StartUtc.AddDays(10),
            endUtc:              AsyncFixture.EndUtc.AddDays(10),
            resolveDelayMinutes: AsyncFixture.ResolveDelayMin,
            entryFeeRP:          0L,
            prizeTableId:        "pt2",
            botFieldId:          "bf_empty",
            sponsorKey:          "",
            leagueKey:           "");

        /// <summary>A local backend over the fixture tournament plus a second one — what a schedule
        /// refetch composes after the wrapper already exists.</summary>
        private static LocalTournamentBackend LocalWithTwo(
            ITournamentEntryStore store, IRewardPointsService rp, IItemRewardService items, ITournamentClock clock,
            IReadOnlyDictionary<string, PrizeTable> prizes)
        {
            var cfg = new BotFieldConfig("bf_empty", 0, new Dictionary<string, float>(), 0f, 0f, 0f);
            return new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { AsyncFixture.Def(0L), SecondDef() },
                prizeTables: prizes,
                botFields:   new Dictionary<string, BotFieldConfig> { ["bf_empty"] = cfg },
                botGen:      new BotFieldGenerator(new List<FakePlayerRow>(), new List<BotScoreBracketRow>()),
                clock:       clock,
                store:       store,
                rp:          rp,
                items:       items,
                pars:        new FakeHoleParProvider(4));
        }

        private static IReadOnlyList<TournamentDefinition> Tournaments(object backend)
            => (IReadOnlyList<TournamentDefinition>)AsyncProd.Backend.GetMethod("GetTournaments", AnyInstance)!
                   .Invoke(backend, null)!;

        private static void Adopt(object backend, LocalTournamentBackend local, IReadOnlyDictionary<string, PrizeTable> prizes)
            => AsyncProd.Backend.GetMethod("Adopt", AnyInstance)!.Invoke(backend, new object[] { local, prizes });

        private static object LocalOf(object backend)
            => AsyncProd.Backend.GetProperty("Local", AnyInstance)!.GetValue(backend)!;

        private static object QueueOf(object backend)
            => AsyncProd.Backend.GetProperty("Queue", AnyInstance)!.GetValue(backend)!;

        [Test]
        public void Adopt_makes_the_wrapper_serve_the_newly_composed_schedule()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService();
            var items = new FakeItemRewardService();
            var prizesA = new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() };

            LocalTournamentBackend first = AsyncFixture.Local(store, rp, items, clock, 0L);
            object backend = AsyncProd.NewBackend(first, store, rp, items, prizesA, clock,
                                                  AsyncProd.NewQueue(new InMemoryPendingOpsStore()));

            // As built: the wrapper serves the one fixture tournament.
            Assert.AreEqual(1, Tournaments(backend).Count, "sanity: the wrapper starts on the first schedule");
            Assert.AreSame(first, LocalOf(backend));

            // A refetch lands: the service composes a NEW local backend over two tournaments.
            var prizesB = new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize(), ["pt2"] = AsyncFixture.Prize() };
            LocalTournamentBackend second = LocalWithTwo(store, rp, items, clock, prizesB);

            // The defect, stated: before Adopt the wrapper still answers from the first schedule.
            Assert.AreEqual(1, Tournaments(backend).Count,
                "the wrapper does not see a new schedule on its own — that is why the service must Adopt");

            Adopt(backend, second, prizesB);

            Assert.AreSame(second, LocalOf(backend), "Adopt re-points the wrapper at the new local backend");
            Assert.AreEqual(2, Tournaments(backend).Count, "the new schedule's tournaments are served");
            bool found = false;
            foreach (var d in Tournaments(backend)) if (d.Id == SecondSlug) found = true;
            Assert.IsTrue(found, $"'{SecondSlug}' from the new schedule is reachable through the wrapper");
        }

        [Test]
        public void Adopt_keeps_the_session_state_the_wrapper_exists_for()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService();
            var items = new FakeItemRewardService();
            var prizes = new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() };

            object backend = AsyncProd.NewBackend(AsyncFixture.Local(store, rp, items, clock, 0L),
                                                  store, rp, items, prizes, clock,
                                                  AsyncProd.NewQueue(new InMemoryPendingOpsStore()));
            object queueBefore = QueueOf(backend);

            // An entry registered before the swap is still there after it: the store is shared,
            // and the wrapper's own queue instance is untouched.
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 0L, "char_james");
            Adopt(backend, LocalWithTwo(store, rp, items, clock, prizes), prizes);

            Assert.AreSame(queueBefore, QueueOf(backend), "the submit queue survives a schedule swap");
            Assert.IsNotNull(AsyncProd.GetMyEntry(backend, AsyncFixture.Slug), "the entry made before the swap is still served");
        }

        [Test]
        public void Adopt_refuses_null()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService();
            var items = new FakeItemRewardService();
            var prizes = new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() };
            object backend = AsyncProd.NewBackend(AsyncFixture.Local(store, rp, items, clock, 0L),
                                                  store, rp, items, prizes, clock,
                                                  AsyncProd.NewQueue(new InMemoryPendingOpsStore()));

            var ex = Assert.Throws<TargetInvocationException>(() => Adopt(backend, null!, prizes));
            Assert.IsInstanceOf<ArgumentNullException>(ex!.InnerException);
        }
    }
}
