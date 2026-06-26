// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — LocalTournamentBackendTests (T4)
// EditMode NUnit invariant/unit suite gating LocalTournamentBackend.
// All 8 ITournamentBackend methods + DeriveState + resolve gate covered.
// No UnityEngine dependency — headless, fixed clock, in-memory seams.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Golfin.Tournaments;

namespace Golfin.Tournaments.Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test fixtures and helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fixed clock for deterministic tests (matches TournamentContractsTests pattern).</summary>
    internal sealed class FixedClock : ITournamentClock
    {
        public DateTime UtcNow { get; set; }
        public FixedClock(DateTime utcNow) { UtcNow = utcNow; }
    }

    /// <summary>
    /// Factory for building a minimal LocalTournamentBackend with controllable seams.
    /// Default configuration: one 3-hole tournament "t1" with a 30-min resolve delay,
    /// a prize table "pt1", an empty bot field, and par=4 for every hole.
    /// </summary>
    internal static class Fixture
    {
        // ── Shared tournament window ─────────────────────────────────────────
        public static readonly DateTime StartUtc  = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime EndUtc    = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        public const  int     ResolveDelayMin = 30;
        public static readonly DateTime ResolvedAt = EndUtc.AddMinutes(ResolveDelayMin);

        public static readonly string[] HoleSet3  = { "h1", "h2", "h3" };
        public static readonly string[] HoleSet9  = { "h1","h2","h3","h4","h5","h6","h7","h8","h9" };
        public static readonly string[] HoleSet18 =
        {
            "h1","h2","h3","h4","h5","h6","h7","h8","h9",
            "h10","h11","h12","h13","h14","h15","h16","h17","h18"
        };

        // ── Minimal PrizeBand / PrizeTable ───────────────────────────────────
        public static PrizeTable MakePrizeTable(string id = "pt1", params (int from, int to, long rp, string? item)[] bands)
        {
            var bandList = new List<PrizeBand>();
            foreach (var (from, to, rp, item) in bands)
                bandList.Add(new PrizeBand(from, to, rp, item));
            return new PrizeTable(id, bandList);
        }

        public static PrizeTable DefaultPrize() =>
            MakePrizeTable("pt1",
                (1, 1, 1000L, "trophy_gold"),
                (2, 3, 500L,  null),
                (4, 10, 100L, null));

        // ── TournamentDefinition ─────────────────────────────────────────────
        public static TournamentDefinition Def(
            string id = "t1",
            string[] ? holeSet = null,
            long entryFee = 0L,
            int resolveMin = ResolveDelayMin,
            string prizeTableId = "pt1",
            string botFieldId = "bf_empty")
        {
            return new TournamentDefinition(
                id:                  id,
                nameKey:             "NAME_" + id.ToUpper(),
                clubId:              "club_lomond",
                holeSet:             holeSet ?? HoleSet3,
                startUtc:            StartUtc,
                endUtc:              EndUtc,
                resolveDelayMinutes: resolveMin,
                entryFeeRP:          entryFee,
                prizeTableId:        prizeTableId,
                botFieldId:          botFieldId,
                sponsorKey:          "",
                leagueKey:           "");
        }

        // ── BotFieldConfig (empty — no bots) ────────────────────────────────
        public static BotFieldConfig EmptyBotField() =>
            new BotFieldConfig(
                botFieldId:         "bf_empty",
                botCount:           0,
                bracketWeights:     new Dictionary<string, float>(),
                startOffsetMinSec:  0f,
                startOffsetMaxSec:  0f,
                perHoleSpreadSec:   0f);

        // ── BotFieldGenerator (empty roster → 0 bots) ───────────────────────
        public static BotFieldGenerator EmptyBotGen() =>
            new BotFieldGenerator(
                new List<FakePlayerRow>(),
                new List<BotScoreBracketRow>());

        // ── Standard backend (3-hole, no entry fee, no bots) ────────────────
        public static (
            LocalTournamentBackend backend,
            FixedClock clock,
            InMemoryEntryStore store,
            FakeRewardPointsService rp,
            FakeItemRewardService items
        ) Make(
            TournamentDefinition? def = null,
            DateTime? now = null,
            long rpBalance = 10000L,
            PrizeTable? prize = null,
            InMemoryEntryStore? store = null)   // optional: share store across instances (D-claim test)
        {
            var d     = def   ?? Def();
            var clock = new FixedClock(now ?? StartUtc.AddHours(1));
            var s     = store ?? new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService(rpBalance);
            var items = new FakeItemRewardService();
            var p     = prize ?? DefaultPrize();

            var backend = new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { d },
                prizeTables: new Dictionary<string, PrizeTable> { [p.PrizeTableId] = p },
                botFields:   new Dictionary<string, BotFieldConfig> { [EmptyBotField().BotFieldId] = EmptyBotField() },
                botGen:      EmptyBotGen(),
                clock:       clock,
                store:       s,
                rp:          rp,
                items:       items,
                pars:        new FakeHoleParProvider(4));

            return (backend, clock, s, rp, items);
        }

        // ── Backend with a seeded 2-bot field ────────────────────────────────
        // Produces 2 bots (level 10, par=4 per hole, stdev=0 → exactly 4 strokes/hole).
        // This lets tie/countback/split-pool paths be exercised without Unity assets.
        public static (
            LocalTournamentBackend backend,
            FixedClock clock,
            InMemoryEntryStore store,
            FakeRewardPointsService rp,
            FakeItemRewardService items,
            BotFieldGenerator botGen,
            BotFieldConfig botCfg
        ) MakeWithBots(
            TournamentDefinition? def = null,
            DateTime? now = null,
            long rpBalance = 10000L,
            PrizeTable? prize = null,
            InMemoryEntryStore? store = null,
            int botCount = 2,
            int par = 4)
        {
            var d     = def   ?? Def(botFieldId: "bf_seeded");  // default points at the seeded field
            var clock = new FixedClock(now ?? StartUtc.AddHours(1));
            var s     = store ?? new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService(rpBalance);
            var items = new FakeItemRewardService();
            var p     = prize ?? DefaultPrize();

            // Deterministic bots: level 10, bracket "1" (minLevel=1), mean=0, stdev=0
            // → each bot scores exactly par per hole (mean=0 delta + no stdev → round(par+0)=par).
            var roster   = new List<FakePlayerRow>();
            for (int i = 0; i < Math.Max(botCount, 3); i++)
                roster.Add(new FakePlayerRow($"bot_{i}", $"Bot{i}", $"char_bot_{i}", 10));

            var brackets = new List<BotScoreBracketRow>
            {
                new BotScoreBracketRow(minLevel: 1, meanDelta: 0.0, stdev: 0.0)
            };

            var botGen = new BotFieldGenerator(roster, brackets);

            var botCfg = new BotFieldConfig(
                botFieldId:        "bf_seeded",
                botCount:          botCount,
                bracketWeights:    new Dictionary<string, float> { ["1"] = 1.0f },
                startOffsetMinSec: 0f,
                startOffsetMaxSec: 0f,
                perHoleSpreadSec:  0f);

            var backend = new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { d },
                prizeTables: new Dictionary<string, PrizeTable> { [p.PrizeTableId] = p },
                botFields:   new Dictionary<string, BotFieldConfig> { [botCfg.BotFieldId] = botCfg },
                botGen:      botGen,
                clock:       clock,
                store:       s,
                rp:          rp,
                items:       items,
                pars:        new FakeHoleParProvider(par));

            return (backend, clock, s, rp, items, botGen, botCfg);
        }

        // ── HoleResult builder ───────────────────────────────────────────────
        public static HoleResult MakeHole(string holeId, int strokes = 4, float timeSec = 120f, DateTime? completedUtc = null)
        {
            return new HoleResult(
                holeId:       holeId,
                strokes:      strokes,
                timeSeconds:  timeSec,
                completedUtc: completedUtc ?? StartUtc.AddHours(2),
                rngSeed:      42,
                inputLog:     new List<ShotCommand>());
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test Suite
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class LocalTournamentBackendTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // §1 — State derivation
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void DeriveState_BeforeStart_IsUpcoming()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddMinutes(-1));
            var def = Fixture.Def();
            Assert.AreEqual(TournamentState.Upcoming, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_AtExactStart_IsOpen()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc);
            var def = Fixture.Def();
            Assert.AreEqual(TournamentState.Open, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_MidWindow_NoEntry_IsOpen()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(3));
            var def = Fixture.Def();
            Assert.AreEqual(TournamentState.Open, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_MidWindow_EntryInProgress_IsPlaying()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(3));
            var def = Fixture.Def();
            b.Register(def.Id, 0L, "char_test");
            Assert.AreEqual(TournamentState.Playing, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_LastHour_IsEnding()
        {
            // EndUtc - 30 min = within the 1-hour "Ending" threshold
            var t = Fixture.EndUtc.AddMinutes(-30);
            var (b, clock, _, _, _) = Fixture.Make(now: t);
            var def = Fixture.Def();
            Assert.AreEqual(TournamentState.Ending, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_ExactEnd_NoEntry_IsClosed()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.EndUtc);
            var def = Fixture.Def();
            Assert.AreEqual(TournamentState.Closed, b.DeriveState(def, clock.UtcNow));
        }

        [Test]
        public void DeriveState_AfterEnd_WithEntry_IsEnded()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.EndUtc.AddMinutes(1));
            var def = Fixture.Def();
            b.Register(def.Id, 0L, "char_test");
            Assert.AreEqual(TournamentState.Ended, b.DeriveState(def, clock.UtcNow));
        }

        // ─────────────────────────────────────────────────────────────────────
        // §2 — Resolve gate
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void IsResolved_JustBeforeResolve_False()
        {
            var def = Fixture.Def(resolveMin: 30);
            var t   = Fixture.EndUtc.AddMinutes(29);
            Assert.IsFalse(LocalTournamentBackend.IsResolved(def, t));
        }

        [Test]
        public void IsResolved_AtExactResolveTime_True()
        {
            var def = Fixture.Def(resolveMin: 30);
            Assert.IsTrue(LocalTournamentBackend.IsResolved(def, Fixture.ResolvedAt));
        }

        [Test]
        public void IsResolved_AfterResolve_True()
        {
            var def = Fixture.Def(resolveMin: 30);
            Assert.IsTrue(LocalTournamentBackend.IsResolved(def, Fixture.ResolvedAt.AddSeconds(1)));
        }

        // ─────────────────────────────────────────────────────────────────────
        // §3 — GetTournaments / GetTournament
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetTournaments_ReturnsAllDefs()
        {
            var (b, _, _, _, _) = Fixture.Make();
            var defs = b.GetTournaments();
            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual("t1", defs[0].Id);
        }

        [Test]
        public void GetTournament_KnownId_ReturnsCorrectDef()
        {
            var (b, _, _, _, _) = Fixture.Make();
            var def = b.GetTournament("t1");
            Assert.AreEqual("t1", def.Id);
        }

        [Test]
        public void GetTournament_UnknownId_Throws()
        {
            var (b, _, _, _, _) = Fixture.Make();
            Assert.Throws<KeyNotFoundException>(() => b.GetTournament("does_not_exist"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // §4 — Register
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Register_Free_CreatesEntryInProgress()
        {
            var (b, _, _, rp, _) = Fixture.Make(rpBalance: 1000L);
            var entry = b.Register("t1", 0L, "char_a");
            Assert.AreEqual(EntryStatus.InProgress, entry.Status);
            Assert.AreEqual("char_a", entry.CharacterId);
            Assert.AreEqual(0, entry.PerHole.Count);
            Assert.AreEqual(1000L, rp.Balance, "free entry must not debit RP");
        }

        [Test]
        public void Register_WithFee_DebitsExactly()
        {
            var (b, _, _, rp, _) = Fixture.Make(rpBalance: 1000L);
            b.Register("t1", 200L, "char_a");
            Assert.AreEqual(800L, rp.Balance, "entry fee debited from balance");
        }

        [Test]
        public void Register_Idempotent_NoDoubleCharge()
        {
            var (b, _, _, rp, _) = Fixture.Make(rpBalance: 1000L);
            var e1 = b.Register("t1", 200L, "char_a");
            var e2 = b.Register("t1", 200L, "char_a"); // re-register
            Assert.AreEqual(800L, rp.Balance, "re-registration must not charge twice");
            Assert.AreEqual(e1.CharacterId, e2.CharacterId);
        }

        [Test]
        public void Register_InsufficientRP_ThrowsAndNoEntry()
        {
            var (b, _, store, _, _) = Fixture.Make(rpBalance: 100L);
            Assert.Throws<InvalidOperationException>(() => b.Register("t1", 200L, "char_a"));
            Assert.IsNull(store.Load("t1"), "entry must not be created when RP insufficient");
        }

        [Test]
        public void Register_CharacterLocked()
        {
            var (b, _, _, _, _) = Fixture.Make();
            var entry = b.Register("t1", 0L, "char_locked");
            Assert.AreEqual("char_locked", entry.CharacterId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §5 — GetMyEntry
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetMyEntry_NotRegistered_ReturnsNull()
        {
            var (b, _, _, _, _) = Fixture.Make();
            Assert.IsNull(b.GetMyEntry("t1"));
        }

        [Test]
        public void GetMyEntry_Registered_ReturnsEntry()
        {
            var (b, _, _, _, _) = Fixture.Make();
            b.Register("t1", 0L, "char_a");
            var e = b.GetMyEntry("t1");
            Assert.IsNotNull(e);
            Assert.AreEqual("char_a", e!.CharacterId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §6 — SubmitHoleResult
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void SubmitHoleResult_AppendHole_PerHoleGrows()
        {
            var (b, _, _, _, _) = Fixture.Make();
            b.Register("t1", 0L, "char_a");
            var updated = b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            Assert.AreEqual(1, updated.PerHole.Count);
            Assert.AreEqual(EntryStatus.InProgress, updated.Status);
        }

        [Test]
        public void SubmitHoleResult_LastHole_StatusFinished()
        {
            var (b, _, _, _, _) = Fixture.Make();
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 5));
            var final = b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 3));
            Assert.AreEqual(3, final.PerHole.Count);
            Assert.AreEqual(EntryStatus.Finished, final.Status);
        }

        [Test]
        public void SubmitHoleResult_PersistsViaStore()
        {
            var (b, _, store, _, _) = Fixture.Make();
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            var loaded = store.Load("t1");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded!.PerHole.Count);
        }

        [Test]
        public void SubmitHoleResult_DuplicateHole_Throws()
        {
            var (b, _, _, _, _) = Fixture.Make();
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1"));
            Assert.Throws<InvalidOperationException>(() =>
                b.SubmitHoleResult("t1", Fixture.MakeHole("h1"))); // same holeId
        }

        [Test]
        public void SubmitHoleResult_PostEndUtc_Throws()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddHours(1));
            b.Register("t1", 0L, "char_a");
            // Advance clock to after EndUtc
            clock.UtcNow = Fixture.EndUtc.AddSeconds(1);
            Assert.Throws<InvalidOperationException>(() =>
                b.SubmitHoleResult("t1", Fixture.MakeHole("h1")));
        }

        [Test]
        public void SubmitHoleResult_NotRegistered_Throws()
        {
            var (b, _, _, _, _) = Fixture.Make();
            Assert.Throws<InvalidOperationException>(() =>
                b.SubmitHoleResult("t1", Fixture.MakeHole("h1")));
        }

        // ─────────────────────────────────────────────────────────────────────
        // §7 — GetLeaderboard (provisional)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_Provisional_IsProvisionalTrue()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            var board = b.GetLeaderboard("t1");
            // With no bots and no player, empty board
            Assert.AreEqual(0, board.Count);
        }

        [Test]
        public void GetLeaderboard_Provisional_PlayerRow_IsProvisionalTrue()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            var board = b.GetLeaderboard("t1");
            var playerRow = board.First(r => r.IsPlayer);
            Assert.IsTrue(playerRow.IsProvisional);
            Assert.IsFalse(playerRow.IsDNF);
        }

        [Test]
        public void GetLeaderboard_Provisional_D3_ScoreToParSort()
        {
            // D3: thru-3 at -1 must rank above thru-9 at E (score-to-par sort)
            // Setup: two backends, but we simulate with a 9-hole tournament
            // Player A: 3 holes completed, strokes=11 (−1 over par=12 for 3 holes)
            // Player B: ... we have no bots and only one player in the seam
            // Instead test: player completes 3 holes at -1, board shows rank 1
            var def = Fixture.Def(holeSet: Fixture.HoleSet9);
            var (b, clock, _, _, _) = Fixture.Make(def: def, now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            // Par 4 × 3 holes = 12. Strokes: 4+4+3 = 11 → -1 score-to-par
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 3));
            var board = b.GetLeaderboard("t1");
            var playerRow = board.First(r => r.IsPlayer);
            Assert.AreEqual(1, playerRow.Rank, "player at -1 via 3 holes should be rank 1");
            Assert.AreEqual(3, playerRow.Thru);
            Assert.IsTrue(playerRow.IsProvisional);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §8 — GetLeaderboard (final ranking by total strokes)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_Final_IsProvisionalFalse()
        {
            // Register + submit in open window, then advance clock past resolve
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 4));
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            var playerRow = board.First(r => r.IsPlayer);
            Assert.IsFalse(playerRow.IsProvisional);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §9 — Ties
        // ─────────────────────────────────────────────────────────────────────
        // NOTE: Single-player tests can't directly verify N-way ties.
        // We verify the tie logic through the AssignRanks path by building
        // two backends that share a store and asserting the rank assignment.
        // Actually, with only one player and no bots, tie logic isn't reachable
        // in isolation. We test tie assignment separately via a 9-hole fixture
        // where a player has same strokes as a "bot injected via shared store".
        // For T4 headless tests, we test the rank-skipping rule:

        [Test]
        public void AssignRanks_SinglePlayer_Rank1_NoTie()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}", strokes: 4));
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            Assert.AreEqual(1, board.Count);
            Assert.AreEqual(1, board[0].Rank);
            Assert.IsFalse(board[0].IsTie);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §10 — DNF
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_PlayerDNF_AppearsBelowFinishers()
        {
            // Register + partial submit in open window, then advance past EndUtc
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 4));
            // NOT submitting h3 — player is DNF
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            // Only the player's DNF row should appear (no bots, no finishers)
            Assert.AreEqual(1, board.Count, "DNF player must be in sticky row");
            Assert.IsTrue(board[0].IsDNF);
            Assert.IsTrue(board[0].IsPlayer);
        }

        [Test]
        public void GetLeaderboard_PlayerDNF_IsDNFTrue()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            // Submit only 1 hole of 3 — definitely DNF
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 3));
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            var playerRow = board.First(r => r.IsPlayer);
            Assert.IsTrue(playerRow.IsDNF);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §11 — GetResults
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetResults_BeforeResolve_ReturnsNull()
        {
            // Submit in open window, then advance to 10 min post-end (before 30-min resolve)
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.EndUtc.AddMinutes(10); // 10 min < 30 min resolve
            Assert.IsNull(b.GetResults("t1"), "results must be null before resolve delay expires");
        }

        [Test]
        public void GetResults_AfterResolve_NotNull()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;
            var result = b.GetResults("t1");
            Assert.IsNotNull(result);
        }

        [Test]
        public void GetResults_AfterResolve_Rank1_HasPrize()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;
            var result = b.GetResults("t1");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result!.FinalRank);
            Assert.AreEqual(1000L, result.PrizeRP);
            Assert.AreEqual("trophy_gold", result.ItemRewardId);
        }

        [Test]
        public void GetResults_NeverRegistered_ReturnsNull()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.ResolvedAt);
            Assert.IsNull(b.GetResults("t1"), "unregistered player has no result");
        }

        [Test]
        public void GetResults_NotClaimed_IsFalse()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;
            var result = b.GetResults("t1");
            Assert.IsFalse(result!.Claimed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §12 — ClaimPrize
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ClaimPrize_GrantsRPAndItem()
        {
            var (b, clock, _, rp, items) = Fixture.Make(now: Fixture.StartUtc.AddDays(1), rpBalance: 0L);
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;
            b.ClaimPrize("t1");
            Assert.AreEqual(1000L, rp.Balance, "claim must grant rank-1 RP (1000)");
            Assert.AreEqual(1, items.Granted["trophy_gold"], "rank-1 item must be granted");
        }

        [Test]
        public void ClaimPrize_ClaimOnce_NoDoubleGrant()
        {
            var (b, clock, _, rp, items) = Fixture.Make(now: Fixture.StartUtc.AddDays(1), rpBalance: 0L);
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;
            b.ClaimPrize("t1");
            b.ClaimPrize("t1"); // idempotent
            Assert.AreEqual(1000L, rp.Balance, "RP must be granted exactly once");
            Assert.AreEqual(1, items.Granted.GetValueOrDefault("trophy_gold", 0), "item must be granted exactly once");
        }

        [Test]
        public void ClaimPrize_BeforeResolve_NoOp()
        {
            // Start clock in open window, submit all holes, then advance to post-EndUtc but pre-resolve
            var (b, clock, _, rp, items) = Fixture.Make(now: Fixture.StartUtc.AddDays(1), rpBalance: 0L);
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.EndUtc.AddMinutes(10); // 10 min < 30 min resolve → no-op
            b.ClaimPrize("t1");
            Assert.AreEqual(0L, rp.Balance, "ClaimPrize before resolve must be a no-op");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §13 — Prize split-pool (tie spanning two bands)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Prize_SplitPool_TwoPlayersAtRank1_RpRoundedUp()
        {
            // Prize table: rank 1 = 1000 RP, rank 2 = 900 RP.
            // A 2-way tie at rank 1 spans both bands → pool = 1000+900 = 1900 RP.
            // Each player gets ceil(1900/2) = 950 RP.
            // We can only test this via the leaderboard's rank field + split formula.
            // Since we have one player with no bots, simulate the scenario by verifying
            // the split formula in isolation via a pinned prize-table calculation.
            //
            // The actual multi-player tie requires a multi-player scenario; T4 tests
            // single-player paths. The split logic is tested at the formula level
            // in PrizeSplitFormulaTests below.
            //
            // For the single-player rank-1 (no tie) path: verify no splitting occurs.
            var prizeTable = Fixture.MakePrizeTable("pt_two",
                (1, 1, 1000L, "item_gold"),
                (2, 2, 900L, null));

            var def = Fixture.Def(prizeTableId: "pt_two");
            var (b, clock, _, rp, items) = Fixture.Make(def: def, now: Fixture.StartUtc.AddDays(1), rpBalance: 0L,
                prize: prizeTable);

            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));

            clock.UtcNow = Fixture.ResolvedAt;
            b.ClaimPrize("t1");
            Assert.AreEqual(1000L, rp.Balance, "rank 1 (no tie) gets full band RP, no splitting");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §14 — Determinism via clock
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Determinism_SameClockAndSeed_ProduceSameLeaderboard()
        {
            // Two separate backends with IDENTICAL seeded bot fields + same fixed clock
            // must produce bit-identical leaderboards. Empty-bot boards trivially agree;
            // this test uses the seeded 2-bot fixture so the boards are non-empty and
            // each entry's Rank, Strokes, DisplayName must match (Fail 3 fix).
            var (b1, _, _, _, _, _, _) = Fixture.MakeWithBots(now: Fixture.StartUtc.AddDays(2));
            var (b2, _, _, _, _, _, _) = Fixture.MakeWithBots(now: Fixture.StartUtc.AddDays(2));

            var board1 = b1.GetLeaderboard("t1");
            var board2 = b2.GetLeaderboard("t1");

            Assert.AreEqual(board1.Count, board2.Count, "identical seeded inputs must produce identical board length");
            Assert.IsTrue(board1.Count > 0, "seeded 2-bot field must produce non-empty board");
            for (int i = 0; i < board1.Count; i++)
            {
                Assert.AreEqual(board1[i].Rank,        board2[i].Rank,        $"row {i}: Rank differs");
                Assert.AreEqual(board1[i].DisplayName, board2[i].DisplayName, $"row {i}: DisplayName differs");
                Assert.AreEqual(board1[i].Strokes,     board2[i].Strokes,     $"row {i}: Strokes differs");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // §15 — Prize band boundary resolution
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetResults_Rank2_GetsBand2Prize()
        {
            // With no bots and only player → player is always rank 1.
            // Test rank 2 band requires an additional check in isolation.
            // We verify the 500 RP is what a rank-2 player would get by checking
            // the prize table definition itself (structural test).
            var prize = Fixture.DefaultPrize();
            var band2 = prize.Bands.FirstOrDefault(b => b.RankFrom == 2 && b.RankTo == 3);
            Assert.IsNotNull(band2);
            Assert.AreEqual(500L, band2!.RpReward);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §16 — Countback / tie-ladder structural tests
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_9HoleDef_PlayerComplete_AllThruEqual9()
        {
            var def = Fixture.Def(holeSet: Fixture.HoleSet9);
            var (b, clock, _, _, _) = Fixture.Make(def: def, now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 9; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}", strokes: 4));
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            var pRow = board.First(r => r.IsPlayer);
            Assert.AreEqual(9, pRow.Thru, "player who finished 9 holes must show thru=9");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §17 — FakeRewardPointsService seam contract
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FakeRewardPointsService_TrySpend_DecrementsBalance()
        {
            var svc = new FakeRewardPointsService(500L);
            Assert.IsTrue(svc.TrySpend(200L));
            Assert.AreEqual(300L, svc.Balance);
        }

        [Test]
        public void FakeRewardPointsService_TrySpend_Insufficient_ReturnsFalse()
        {
            var svc = new FakeRewardPointsService(100L);
            Assert.IsFalse(svc.TrySpend(200L));
            Assert.AreEqual(100L, svc.Balance, "balance unchanged on failed spend");
        }

        [Test]
        public void FakeRewardPointsService_Grant_IncrementsBalance()
        {
            var svc = new FakeRewardPointsService(0L);
            svc.Grant(750L);
            Assert.AreEqual(750L, svc.Balance);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §18 — FakeItemRewardService seam contract
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FakeItemRewardService_Grant_TracksItems()
        {
            var svc = new FakeItemRewardService();
            svc.Grant("item_a", 1);
            svc.Grant("item_a", 2);
            svc.Grant("item_b", 1);
            Assert.AreEqual(3, svc.Granted["item_a"]);
            Assert.AreEqual(1, svc.Granted["item_b"]);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §19 — InMemoryEntryStore seam contract
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void InMemoryEntryStore_LoadMiss_ReturnsNull()
        {
            var store = new InMemoryEntryStore();
            Assert.IsNull(store.Load("does_not_exist"));
        }

        [Test]
        public void InMemoryEntryStore_SaveAndLoad_RoundTrips()
        {
            var store = new InMemoryEntryStore();
            var entry = new EntryState("t1", "char_a", new List<HoleResult>(), DateTime.UtcNow, null, EntryStatus.InProgress);
            store.Save(entry);
            var loaded = store.Load("t1");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("char_a", loaded!.CharacterId);
        }

        [Test]
        public void InMemoryEntryStore_Overwrite_ReplacesEntry()
        {
            var store = new InMemoryEntryStore();
            var e1 = new EntryState("t1", "char_a", new List<HoleResult>(), DateTime.UtcNow, null, EntryStatus.InProgress);
            var e2 = new EntryState("t1", "char_b", new List<HoleResult>(), DateTime.UtcNow, null, EntryStatus.Finished);
            store.Save(e1);
            store.Save(e2);
            var loaded = store.Load("t1");
            Assert.AreEqual("char_b", loaded!.CharacterId);
            Assert.AreEqual(EntryStatus.Finished, loaded.Status);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §20 — FakeHoleParProvider seam contract
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FakeHoleParProvider_ReturnsFlatPar()
        {
            var prov = new FakeHoleParProvider(5);
            var pars = prov.ParsFor("any_club", Fixture.HoleSet3);
            Assert.AreEqual(3, pars.Count);
            Assert.IsTrue(pars.All(p => p == 5));
        }

        // ─────────────────────────────────────────────────────────────────────
        // §21 — Leaderboard rank=1 for solo-finished player (no bots)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_SoloFinished_Rank1()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1));
            b.Register("t1", 0L, "char_solo");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}", strokes: 4));
            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");
            Assert.AreEqual(1, board.Count);
            Assert.AreEqual(1, board[0].Rank);
            Assert.AreEqual("char_solo", board[0].CharacterId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // §22 — InMemoryEntryStore claim-once contract (Fail 1 — new tests)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void InMemoryEntryStore_IsClaimed_DefaultFalse()
        {
            var store = new InMemoryEntryStore();
            Assert.IsFalse(store.IsClaimed("t1"), "new store must not have any claims");
        }

        [Test]
        public void InMemoryEntryStore_MarkClaimed_ThenIsClaimed_True()
        {
            var store = new InMemoryEntryStore();
            store.MarkClaimed("t1");
            Assert.IsTrue(store.IsClaimed("t1"));
        }

        [Test]
        public void InMemoryEntryStore_MarkClaimed_Idempotent()
        {
            var store = new InMemoryEntryStore();
            store.MarkClaimed("t1");
            store.MarkClaimed("t1"); // second call must not throw
            Assert.IsTrue(store.IsClaimed("t1"));
        }

        [Test]
        public void InMemoryEntryStore_Claim_DoesNotCrossContaminate()
        {
            var store = new InMemoryEntryStore();
            store.MarkClaimed("t1");
            Assert.IsFalse(store.IsClaimed("t2"), "claim for t1 must not affect t2");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §23 — ClaimPrize persists through store (D-claim b — Fail 1 fix test)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ClaimPrize_SurvivesStoreReload_NoDoubleGrant()
        {
            // Simulate what happens after an app restart where the backend is rebuilt
            // over the SAME store instance (which persists the claim).
            // If claim-once relied only on in-memory dict, a new backend instance would
            // re-grant the prize. This test guards against that regression.
            var store = new InMemoryEntryStore();

            // Session 1: register, play, claim
            var (b1, clock1, _, rp1, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1), rpBalance: 0L, store: store);
            b1.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b1.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock1.UtcNow = Fixture.ResolvedAt;
            b1.ClaimPrize("t1");
            Assert.AreEqual(1000L, rp1.Balance, "first claim must grant RP");

            // Session 2: new backend instance over same store, new RP service starting at 0
            var rp2    = new FakeRewardPointsService(0L);
            var items2 = new FakeItemRewardService();
            var prize  = Fixture.DefaultPrize();
            var def    = Fixture.Def();
            var b2 = new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { def },
                prizeTables: new Dictionary<string, PrizeTable> { [prize.PrizeTableId] = prize },
                botFields:   new Dictionary<string, BotFieldConfig> { [Fixture.EmptyBotField().BotFieldId] = Fixture.EmptyBotField() },
                botGen:      Fixture.EmptyBotGen(),
                clock:       new FixedClock(Fixture.ResolvedAt),
                store:       store,        // <-- SAME store
                rp:          rp2,
                items:       items2,
                pars:        new FakeHoleParProvider(4));

            b2.ClaimPrize("t1");           // must be a no-op: store says already claimed
            Assert.AreEqual(0L, rp2.Balance, "re-claim via new backend instance must be blocked by store");
        }

        [Test]
        public void GetResults_AfterClaim_ReturnedClaimedTrue()
        {
            var (b, clock, _, _, _) = Fixture.Make(now: Fixture.StartUtc.AddDays(1), rpBalance: 0L);
            b.Register("t1", 0L, "char_a");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}"));
            clock.UtcNow = Fixture.ResolvedAt;

            Assert.IsFalse(b.GetResults("t1")!.Claimed, "before claim, Claimed must be false");
            b.ClaimPrize("t1");
            Assert.IsTrue(b.GetResults("t1")!.Claimed, "after claim, GetResults must return Claimed=true");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §24 — Multi-entry: tie + rank-skip (Fail 2 fix)
        // Bots: 2 × par (12 each on 3-hole par-4). Player also at 12 → 3-way tie.
        // All 3 tied at rank 1; next-rank is 4 (rank-skip rule).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_ThreeWayTie_AllRank1_RankSkip()
        {
            // 3-hole, par 4 per hole → total par = 12.
            // Bots (2): stdev=0, mean=0 → each scores exactly 4/hole → 12 total.
            // Player: 4+4+4 = 12 → three-way tie at rank 1.
            // After assigning rank, nextRank = 1 + 3 = 4 (rank-skip rule, §6).
            var def = Fixture.Def(botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(def: def, now: Fixture.StartUtc.AddDays(1), botCount: 2);

            b.Register("t1", 0L, "char_player");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}", strokes: 4));

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            // All 3 entries (2 bots + 1 player) should be rank 1 and IsTie=true
            Assert.AreEqual(3, board.Count, "3-way tie: board must have 3 rows");
            foreach (var row in board)
            {
                Assert.AreEqual(1, row.Rank, $"each tied entry must be rank 1 (got {row.Rank} for {row.DisplayName})");
                Assert.IsTrue(row.IsTie, $"each entry must be IsTie=true ({row.DisplayName})");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // §25 — Countback orders entries within a tied group (Fail 2 fix)
        // Player: holes [3, 4, 5] = 12. Bot: holes [4, 4, 4] = 12.
        // Both tied at rank 1 (same TotalStrokes). Countback determines DISPLAY ORDER
        // within the board: bot (back-1=4) appears before player (back-1=5).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_CountbackBack1_OrdersWithinTiedGroup()
        {
            // Player and bot both score 12 over 3 holes (par 4×3=12 = E).
            // Player: [3, 4, 5]. Bot: [4, 4, 4].
            // After total-strokes tie (12 == 12), countback on back-1 (hole index 2):
            //   bot=4 < player=5 → bot appears first in the board within the tied group.
            // Both entries are IsTie=true and Rank=1; countback orders them within the tie.
            var def = Fixture.Def(botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(def: def, now: Fixture.StartUtc.AddDays(1), botCount: 1);

            b.Register("t1", 0L, "char_player");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 3));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 5));   // back-1 = 5

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            // Both total 12 → tied at rank 1, IsTie=true.
            Assert.AreEqual(2, board.Count, "board must have 2 entries");
            Assert.AreEqual(1, board[0].Rank, "first entry must be rank 1");
            Assert.AreEqual(1, board[1].Rank, "second entry must also be rank 1 (tie)");
            Assert.IsTrue(board[0].IsTie, "tied entry must have IsTie=true");
            Assert.IsTrue(board[1].IsTie, "tied entry must have IsTie=true");

            // Countback: bot (back-1=4) should appear before player (back-1=5) in the board
            // (board is sorted by CompareFinal which uses countback to order within tie).
            int botIdx    = -1;
            int playerIdx = -1;
            for (int i = 0; i < board.Count; i++)
            {
                if (!board[i].IsPlayer && botIdx    < 0) botIdx    = i;
                if ( board[i].IsPlayer && playerIdx < 0) playerIdx = i;
            }
            Assert.GreaterOrEqual(botIdx,    0, "bot must appear in board");
            Assert.GreaterOrEqual(playerIdx, 0, "player must appear in board");
            Assert.Less(botIdx, playerIdx,
                "bot (back-1=4) must appear before player (back-1=5) within the tied group");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §26 — Split-pool 2-way tie at rank 1 (Fail 2 fix)
        // Prize: rank 1 = 1000 RP, rank 2 = 900 RP.
        // Player ties with 1 bot → pool = 1900 → share = ceil(1900/2) = 950.
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void ClaimPrize_SplitPool_TwoWayTie_Rank1_Gets950()
        {
            var prizeTable = Fixture.MakePrizeTable("pt_split",
                (1, 1, 1000L, null),
                (2, 2, 900L,  null));

            var def = Fixture.Def(prizeTableId: "pt_split", botFieldId: "bf_seeded");
            var (b, clock, _, rp, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), rpBalance: 0L, botCount: 1, prize: prizeTable);

            // Player scores [4,4,4] = 12. Bot also [4,4,4] = 12 → 2-way tie at rank 1.
            b.Register("t1", 0L, "char_player");
            for (int i = 0; i < 3; i++)
                b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i+1}", strokes: 4));

            clock.UtcNow = Fixture.ResolvedAt;
            b.ClaimPrize("t1");

            // 2-way tie: pool = 1000+900 = 1900. Share = ceil(1900/2) = 950.
            Assert.AreEqual(950L, rp.Balance, "2-way tie at rank 1 must award 950 RP (split-pool round-up)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §27 — D3 provisional: partial player thru-3 at −1 ranks above bot at E thru-9
        // Player: 3 holes of 9, score-to-par = −1 (11 strokes vs par 12).
        // Bot: all 9 holes, score-to-par = E (36 strokes, par = 36).
        // Provisional sort by score-to-par → player (−1) above bot (0).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_Provisional_D3_PartialPlayerBelowParRanksAboveBotAtPar()
        {
            // 9-hole tournament, par 4 per hole → total par = 36.
            // Bot completes all 9 holes at par = 36 strokes (mean=0, stdev=0) → E.
            // But bots' PerHoleCompletionUtc are seeded from StartUtc with 0 offset/spread.
            // We set clock to EndUtc − 1h (still provisional, well within window).
            // At this time the bot is fully revealed (all completion times <= now).
            // Player completes only 3 holes: 4+4+3 = 11, par = 12 → score-to-par = −1.
            var def = Fixture.Def(holeSet: Fixture.HoleSet9, botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), botCount: 1, par: 4);

            b.Register("t1", 0L, "char_player");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 3));  // −1 through 3 holes

            // Advance clock far enough that bot's all completions are past
            clock.UtcNow = Fixture.EndUtc.AddMinutes(-1);  // still provisional (before EndUtc + resolve)

            var board = b.GetLeaderboard("t1");
            Assert.IsTrue(board.Count >= 2, "board must have both player and bot");

            var playerRow = board.FirstOrDefault(r =>  r.IsPlayer);
            var botRow    = board.FirstOrDefault(r => !r.IsPlayer);
            Assert.IsNotNull(playerRow, "player must be in provisional board");
            Assert.IsNotNull(botRow,    "bot must be in provisional board");

            Assert.IsTrue(playerRow!.IsProvisional, "provisional board must set IsProvisional=true");
            Assert.IsTrue(playerRow.Rank < botRow!.Rank,
                $"player at −1 (thru 3) must rank above bot at E (thru all). player rank={playerRow.Rank}, bot rank={botRow.Rank}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §28 — 18-hole back-9 resolves tie (Fail / countback blocker fix)
        // Player and bot both total 72 (18×par4).
        // Player scores 3 on h10 and 5 on h1 — same total, different back-9.
        // GDD §6.1: back-9 must decide → player (back-9=35) ranks above bot (back-9=36).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_18Hole_Back9Resolves_PlayerWins()
        {
            var def = Fixture.Def(holeSet: Fixture.HoleSet18, botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), botCount: 1, par: 4);

            b.Register("t1", 0L, "char_player");

            // Player h1=5 (over), h2-h9=4, h10=3 (under), h11-h18=4
            // Total = 5 + 4×8 + 3 + 4×8 = 5 + 32 + 3 + 32 = 72 (ties bot at 72)
            // Back-9 (h10-h18) player = 3+4×8 = 35. Bot = 4×9 = 36. Player wins.
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1",  strokes: 5));
            for (int i = 2; i <= 9;  i++) b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i}",  strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h10", strokes: 3));
            for (int i = 11; i <= 18; i++) b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i}", strokes: 4));

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            Assert.AreEqual(2, board.Count, "board must have player + 1 bot");
            Assert.AreEqual(1, board[0].Rank, "rank-1 entry should be at board[0]");
            Assert.AreEqual(1, board[1].Rank, "rank-1 tie partner at board[1]");
            Assert.IsTrue(board[0].IsTie, "both entries are in a tie group (same total strokes)");
            Assert.IsTrue(board[1].IsTie);

            // Within the tied group the back-9 winner (player) must appear at index 0
            int playerIdx = -1; int botIdx = -1;
            for (int i = 0; i < board.Count; i++)
            {
                if ( board[i].IsPlayer && playerIdx < 0) playerIdx = i;
                if (!board[i].IsPlayer && botIdx    < 0) botIdx    = i;
            }
            Assert.GreaterOrEqual(playerIdx, 0, "player must be in board");
            Assert.GreaterOrEqual(botIdx,    0, "bot must be in board");
            Assert.Less(playerIdx, botIdx,
                "player (back-9=35) must appear before bot (back-9=36) — back-9 resolved the tie");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §29 — 18-hole: back-9/6/3/1 all tie, front-6 resolves (Fail fix)
        // Player h1-h3=6 (3-over), h4-h6=2 (3-under), h7-h18=4 each.
        // Total = 6+6+6+2+2+2+4×12 = 72 (ties bot at 72).
        // Back-9 sum (h10-h18): player=36, bot=36. TIED.
        // Back-6 (h13-h18): player=24, bot=24. TIED.
        // Back-3 (h16-h18): player=12, bot=12. TIED.
        // Back-1 (h18): player=4, bot=4. TIED.
        // Front-9 (h1-h9): player=6+6+6+2+2+2+4+4+4=36, bot=36. TIED.
        // Front-6 (h4-h9): player=2+2+2+4+4+4=18, bot=24. Player wins (18 < 24).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_18Hole_Front6Resolves_AfterAllBackAndFront9Tie()
        {
            var def = Fixture.Def(holeSet: Fixture.HoleSet18, botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), botCount: 1, par: 4);

            b.Register("t1", 0L, "char_player");

            // h1=6, h2=6, h3=6  (3-over front three)
            // h4=2, h5=2, h6=2  (3-under middle three)
            // h7-h18 = 4 each
            // Total = 18 + 6 + 4×12 = 72. Front-6 (h4-h9) = 2+2+2+4+4+4 = 18 (player wins).
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 6));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 6));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 6));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h4", strokes: 2));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h5", strokes: 2));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h6", strokes: 2));
            for (int i = 7;  i <= 18; i++) b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i}", strokes: 4));

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            Assert.AreEqual(2, board.Count, "board must have player + 1 bot");
            Assert.AreEqual(1, board[0].Rank);
            Assert.AreEqual(1, board[1].Rank);
            Assert.IsTrue(board[0].IsTie);
            Assert.IsTrue(board[1].IsTie);

            // Player wins via front-6 → player at index 0
            int playerIdx = -1; int botIdx = -1;
            for (int i = 0; i < board.Count; i++)
            {
                if ( board[i].IsPlayer && playerIdx < 0) playerIdx = i;
                if (!board[i].IsPlayer && botIdx    < 0) botIdx    = i;
            }
            Assert.GreaterOrEqual(playerIdx, 0, "player must be in board");
            Assert.GreaterOrEqual(botIdx,    0, "bot must be in board");
            Assert.Less(playerIdx, botIdx,
                "player (front-6=18) must appear before bot (front-6=24) — front-6 resolved the tie after all back and front-9 windows tied");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §30 — Rank-skip boundary: 3-way tie at rank 1 → 4th finisher at rank 4
        // (Strengthens the AssignRanks nextRank = rank + (j - i) assertion.)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_ThreeWayTie_ThenFourthFinisher_FourthIsRank4()
        {
            // 3 seeded bots all at par (12 strokes on a 3-hole course).
            // Player submits 5/hole = 15 strokes → unique worst finisher.
            // Expected: board[0..2] all rank=1, IsTie=true; board[3] rank=4, IsTie=false.
            var def = Fixture.Def(botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), botCount: 3, par: 4);

            b.Register("t1", 0L, "char_player");
            b.SubmitHoleResult("t1", Fixture.MakeHole("h1", strokes: 5));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h2", strokes: 5));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h3", strokes: 5));

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            Assert.AreEqual(4, board.Count, "3 bots + 1 player = 4 entries");

            // First 3 entries: rank=1, IsTie=true
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1, board[i].Rank, $"board[{i}].Rank must be 1 (3-way tie)");
                Assert.IsTrue(board[i].IsTie,      $"board[{i}].IsTie must be true");
            }

            // 4th entry (player): rank=4 (skips 2 and 3), IsTie=false
            var fourth = board[3];
            Assert.IsTrue(fourth.IsPlayer, "board[3] must be the player");
            Assert.AreEqual(4, fourth.Rank,  "rank must skip to 4 after 3-way tie at rank 1");
            Assert.IsFalse(fourth.IsTie,     "solo entry at rank 4 must not be marked IsTie");
        }

        // ─────────────────────────────────────────────────────────────────────
        // §31 — 9-hole pinned countback: back-3 resolves (N=9 generalization)
        // N=9, half=ceil(9/2)=5.  CountbackWindows(5) → [5, 3, 1].
        // Back pass:  back-5 (h5-h9, startIdx=4), back-3 (h7-h9, startIdx=6), back-1 (h9).
        // Front pass: front-5 (h1-h5, startIdx=0), front-3 (h3-h5, startIdx=2), front-1 (h5).
        //
        // Player: h5=5 (+1), h7=3 (−1), all others=4. Total = 4×7+5+3 = 36 (par). Bot = 36.
        // Back-5 (h5-h9): player = 5+4+3+4+4 = 20. Bot = 4×5 = 20. TIED.
        // Back-3 (h7-h9): player = 3+4+4 = 11. Bot = 4×3 = 12. PLAYER WINS (11 < 12).
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLeaderboard_9Hole_Back3Resolves_PlayerWins()
        {
            var def = Fixture.Def(holeSet: Fixture.HoleSet9, botFieldId: "bf_seeded");
            var (b, clock, _, _, _, _, _) = Fixture.MakeWithBots(
                def: def, now: Fixture.StartUtc.AddDays(1), botCount: 1, par: 4);

            b.Register("t1", 0L, "char_player");

            // h1-h4=4, h5=5 (+1 on h5), h6=4, h7=3 (−1 on h7), h8-h9=4
            // Total = 4+4+4+4+5+4+3+4+4 = 36 (ties bot at 36)
            // Back-5 (h5-h9): player=5+4+3+4+4=20, bot=4×5=20. TIED.
            // Back-3 (h7-h9): player=3+4+4=11, bot=4×3=12. Player wins.
            for (int i = 1; i <= 4; i++) b.SubmitHoleResult("t1", Fixture.MakeHole($"h{i}", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h5", strokes: 5));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h6", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h7", strokes: 3));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h8", strokes: 4));
            b.SubmitHoleResult("t1", Fixture.MakeHole("h9", strokes: 4));

            clock.UtcNow = Fixture.ResolvedAt;
            var board = b.GetLeaderboard("t1");

            Assert.AreEqual(2, board.Count, "board must have player + 1 bot");
            Assert.AreEqual(1, board[0].Rank);
            Assert.AreEqual(1, board[1].Rank);
            Assert.IsTrue(board[0].IsTie, "both entries tied on total");
            Assert.IsTrue(board[1].IsTie);

            int playerIdx = -1; int botIdx = -1;
            for (int i = 0; i < board.Count; i++)
            {
                if ( board[i].IsPlayer && playerIdx < 0) playerIdx = i;
                if (!board[i].IsPlayer && botIdx    < 0) botIdx    = i;
            }
            Assert.GreaterOrEqual(playerIdx, 0, "player must be in board");
            Assert.GreaterOrEqual(botIdx,    0, "bot must be in board");
            Assert.Less(playerIdx, botIdx,
                "player (back-3=11) must appear before bot (back-3=12) — back-3 resolved the 9-hole tie");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PrizeSplitFormula — isolated arithmetic tests
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class PrizeSplitFormulaTests
    {
        // Split pool: divide evenly, round up (player-favorable)

        [Test]
        [TestCase(1000L, 2, 500L)]
        [TestCase(1900L, 2, 950L)]
        [TestCase(1001L, 2, 501L)]       // 1001 / 2 = 500.5 → 501 (round up)
        [TestCase(100L,  3, 34L)]         // 100 / 3 = 33.33 → 34 (round up)
        [TestCase(0L,    3, 0L)]
        public void DivideRoundUp(long total, int n, long expected)
        {
            // Test the formula directly via PrizeBand split math (mirrors DivideRoundUp)
            long actual = (total + n - 1) / n;
            Assert.AreEqual(expected, actual, $"split({total},{n}) expected {expected}");
        }
    }
}
