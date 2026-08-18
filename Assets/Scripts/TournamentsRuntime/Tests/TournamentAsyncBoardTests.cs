// ─────────────────────────────────────────────────────────────────────────────
// TournamentAsyncBoardTests — tournament_async_board SPEC §5 (EditMode)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (Editor-only)
//
// Same access pattern as RemoteScheduleTests / BackendLeaderboardTests, and for
// the same reason: the production types under test (RemoteTournamentBackend,
// TournamentNetJson, TournamentSubmitQueue, TournamentBackendPolicy and the wire
// DTOs) live in Assembly-CSharp, which an asmdef CANNOT reference. They are
// reached by REFLECTION. Everything asserted is a primitive or a
// Golfin.Tournaments type (TournamentLeaderboardEntry, EntryState, PrizeTable),
// all of which this assembly references directly — so the assertions themselves
// need no casting games.
//
// COVERAGE (SPEC §5 EditMode list, in order)
//   §1  DTO parse of the §1 payloads, incl. player:null, rank:null and the
//       insufficient-funds enter response
//   §2  Snapshot → TournamentLeaderboardEntry mapping is VERBATIM; no client re-rank
//   §3  Sticky-row label: "#N · PRIZE #M" while bots are active and the ranks differ
//   §4  Submit queue: survives a restart (disk), replays FIFO, drops on
//       replayed:true and on 400
//   §5  Register on the remote path never debits IRewardPointsService
//   §6  Provider selection, incl. BotSessionOverride → Local
//   §7  Entry reconcile: server wins per hole, local-only (queued) holes survive
//   §8  Queue DRAIN over a scripted transport — drops on 200, on replayed:true and
//       on 400; keeps and stops on a transient failure
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Tournaments;
using NUnit.Framework;

namespace Golfin.Tournaments.WireupTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // Reflection handles onto the Assembly-CSharp production types
    // ═════════════════════════════════════════════════════════════════════════

    internal static class AsyncProd
    {
        private const BindingFlags AnyStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private const BindingFlags AnyInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        internal static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. It should live in Assembly-CSharp " +
                "(Assets/Scripts/TournamentsRuntime/, no asmdef).");
        }

        internal static readonly Type Json       = Find("Golfin.Tournaments.TournamentNetJson");
        internal static readonly Type BoardDto   = Find("Golfin.Tournaments.TournamentBoardDto");
        internal static readonly Type EnterDto   = Find("Golfin.Tournaments.TournamentEnterResponseDto");
        internal static readonly Type EntryDto   = Find("Golfin.Tournaments.TournamentEntryDto");
        internal static readonly Type Backend    = Find("Golfin.Tournaments.RemoteTournamentBackend");
        internal static readonly Type PlayerRow  = Find("Golfin.Tournaments.TournamentPlayerRow");
        internal static readonly Type Queue      = Find("Golfin.Tournaments.TournamentSubmitQueue");
        internal static readonly Type QueueOp    = Find("Golfin.Tournaments.PendingHoleSubmit");
        internal static readonly Type Policy     = Find("Golfin.Tournaments.TournamentBackendPolicy");

        // ── TournamentNetJson ─────────────────────────────────────────────────

        /// <summary>The generic <c>Read&lt;T&gt;(json, source)</c>, closed over a DTO type.</summary>
        internal static object? Read(Type dtoType, string? json, string source = "test")
        {
            MethodInfo open = Json.GetMethod("Read", AnyStatic)
                              ?? throw new InvalidOperationException("TournamentNetJson.Read not found.");
            return open.MakeGenericMethod(dtoType).Invoke(null, new object?[] { json, source });
        }

        internal static DateTime? ParseUtc(string? value)
            => (DateTime?)Json.GetMethod("ParseUtc", AnyStatic)!.Invoke(null, new object?[] { value });

        /// <summary>Read a public field off any DTO by name, whatever its declared type.</summary>
        internal static object? Field(object dto, string field)
            => dto.GetType().GetField(field)!.GetValue(dto);

        internal static object? Prop(object o, string name)
            => o.GetType().GetProperty(name, AnyInstance)!.GetValue(o);

        // ── RemoteTournamentBackend statics ───────────────────────────────────

        internal static IReadOnlyList<TournamentLeaderboardEntry> MapEntries(object boardDto)
            => (IReadOnlyList<TournamentLeaderboardEntry>)
               Backend.GetMethod("MapEntries", AnyStatic)!.Invoke(null, new[] { boardDto })!;

        /// <summary>The mapped <c>player</c> row, boxed as its struct type.</summary>
        internal static object MapPlayer(object boardDto)
            => Backend.GetMethod("MapPlayer", AnyStatic)!.Invoke(null, new[] { boardDto })!;

        internal static string FormatRankLabel(int? rank, int? prizeRank, bool botsActive)
            => (string)PlayerRow.GetMethod("FormatRankLabel", AnyStatic)!
                   .Invoke(null, new object?[] { rank, prizeRank, botsActive })!;

        internal static string RankLabel(object playerRow)
            => (string)PlayerRow.GetMethod("RankLabel", AnyInstance)!.Invoke(playerRow, null)!;

        internal static TournamentLeaderboardEntry RowEntry(object playerRow)
            => (TournamentLeaderboardEntry)PlayerRow.GetField("Entry")!.GetValue(playerRow)!;

        internal static bool RowHasRow(object playerRow)
            => (bool)PlayerRow.GetField("HasRow")!.GetValue(playerRow)!;

        internal static int? RowRank(object playerRow)
            => (int?)PlayerRow.GetField("Rank")!.GetValue(playerRow);

        internal static int? RowPrizeRank(object playerRow)
            => (int?)PlayerRow.GetField("PrizeRank")!.GetValue(playerRow);

        // ── Policy ────────────────────────────────────────────────────────────

        /// <summary>Returns the enum NAME so the test needs no Assembly-CSharp enum reference.</summary>
        internal static string Choose(bool botOverride, bool signedIn, bool isDemo)
            => Policy.GetMethod("Choose", AnyStatic)!
                   .Invoke(null, new object?[] { botOverride, signedIn, isDemo })!.ToString()!;

        // ── Submit queue ──────────────────────────────────────────────────────

        internal static object NewQueue(IPendingOpsStore store)
            => Activator.CreateInstance(Queue, new object[] { store })!;

        internal static object Enqueue(object queue, string slug, int hole, int strokes)
            => Queue.GetMethod("Enqueue", AnyInstance, null,
                   new[] { typeof(string), typeof(int), typeof(int) }, null)!
               .Invoke(queue, new object[] { slug, hole, strokes })!;

        internal static void LoadQueue(object queue) => Queue.GetMethod("Load", AnyInstance)!.Invoke(queue, null);

        internal static int QueueCount(object queue) => (int)Prop(queue, "Count")!;

        internal static object? Peek(object queue) => Queue.GetMethod("Peek", AnyInstance)!.Invoke(queue, null);

        internal static object? Dequeue(object queue) => Queue.GetMethod("Dequeue", AnyInstance)!.Invoke(queue, null);

        internal static string OpKey(object op)     => (string)QueueOp.GetField("IdempotencyKey")!.GetValue(op)!;
        internal static string OpSlug(object op)    => (string)QueueOp.GetField("Slug")!.GetValue(op)!;
        internal static int    OpHole(object op)    => (int)QueueOp.GetField("HoleNumber")!.GetValue(op)!;
        internal static int    OpStrokes(object op) => (int)QueueOp.GetField("Strokes")!.GetValue(op)!;

        internal static string OpRequestJson(object op)
            => (string)QueueOp.GetMethod("ToRequestJson", AnyInstance)!.Invoke(op, null)!;

        // ── Backend instance ──────────────────────────────────────────────────

        internal static object NewBackend(
            LocalTournamentBackend local,
            ITournamentEntryStore store,
            IRewardPointsService rp,
            IItemRewardService items,
            IReadOnlyDictionary<string, PrizeTable> prizeTables,
            ITournamentClock clock,
            object? queue = null)
        {
            ConstructorInfo ctor = Backend.GetConstructors()[0];
            object backend = ctor.Invoke(new object?[] { local, store, rp, items, prizeTables, clock, queue });

            // Never let a test spawn the ApiClient coroutine host: swallow every fire-and-forget
            // routine instead. The tests that DO exercise a routine pump it themselves.
            Backend.GetProperty("CoroutineRunner")!.SetValue(backend, new Action<IEnumerator>(_ => { }));
            Backend.GetProperty("BalanceRefresh")!.SetValue(backend, new Action(() => { }));
            return backend;
        }

        internal static EntryState RegisterSync(object backend, string id, long fee, string charId)
            => (EntryState)Backend.GetMethod("Register", AnyInstance)!
                   .Invoke(backend, new object?[] { id, fee, charId })!;

        internal static int HoleNumberFor(object backend, string id, string? holeId)
            => (int)Backend.GetMethod("HoleNumberFor", AnyInstance)!.Invoke(backend, new object?[] { id, holeId })!;

        internal static bool ApplyServerEntry(object backend, string id, object entryDto)
            => (bool)Backend.GetMethod("ApplyServerEntry", AnyInstance)!
                   .Invoke(backend, new[] { (object)id, entryDto })!;

        internal static IEnumerator FlushRoutine(object backend, Action<int>? onDone)
            => (IEnumerator)Backend.GetMethod("FlushSubmitQueueRoutine", AnyInstance)!
                   .Invoke(backend, new object?[] { onDone })!;

        internal static EntryState? GetMyEntry(object backend, string id)
            => (EntryState?)Backend.GetMethod("GetMyEntry", AnyInstance)!.Invoke(backend, new object?[] { id });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Fixtures
    // ═════════════════════════════════════════════════════════════════════════

    internal sealed class AsyncClock : ITournamentClock
    {
        public DateTime UtcNow { get; set; }
        public AsyncClock(DateTime utcNow) { UtcNow = utcNow; }
    }

    internal static class AsyncFixture
    {
        internal static readonly DateTime StartUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        internal static readonly DateTime EndUtc   = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        internal const int ResolveDelayMin = 30;

        internal const string Slug = "kasumigaseki_open";
        internal static readonly string[] HoleSet = { "h1", "h2", "h3" };

        internal static TournamentDefinition Def(long entryFee = 250L) => new TournamentDefinition(
            id:                  Slug,
            nameKey:             "TOURN_KASUMI",
            clubId:              "club_lomond",
            holeSet:             HoleSet,
            startUtc:            StartUtc,
            endUtc:              EndUtc,
            resolveDelayMinutes: ResolveDelayMin,
            entryFeeRP:          entryFee,
            prizeTableId:        "pt1",
            botFieldId:          "bf_empty",
            sponsorKey:          "",
            leagueKey:           "");

        internal static PrizeTable Prize() => new PrizeTable("pt1", new List<PrizeBand>
        {
            new PrizeBand(1, 1, 1000L, "trophy_gold"),
            new PrizeBand(2, 3, 500L,  null),
            new PrizeBand(4, 10, 100L, null),
        });

        /// <summary>A LocalTournamentBackend over the one fixture tournament, with no bots.</summary>
        internal static LocalTournamentBackend Local(
            ITournamentEntryStore store, IRewardPointsService rp, IItemRewardService items,
            ITournamentClock clock, long entryFee = 250L)
        {
            var def = Def(entryFee);
            var cfg = new BotFieldConfig("bf_empty", 0, new Dictionary<string, float>(), 0f, 0f, 0f);

            return new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { def },
                prizeTables: new Dictionary<string, PrizeTable> { ["pt1"] = Prize() },
                botFields:   new Dictionary<string, BotFieldConfig> { ["bf_empty"] = cfg },
                botGen:      new BotFieldGenerator(new List<FakePlayerRow>(), new List<BotScoreBracketRow>()),
                clock:       clock,
                store:       store,
                rp:          rp,
                items:       items,
                pars:        new FakeHoleParProvider(4));
        }

        internal static HoleResult Hole(string holeId, int strokes = 4, DateTime? at = null)
            => new HoleResult(holeId, strokes, 120f, at ?? StartUtc.AddHours(1), 0, new List<ShotCommand>());
    }

    /// <summary>The SPEC §1 payloads, verbatim, plus the variants the client must survive.</summary>
    internal static class AsyncPayloads
    {
        /// <summary>SPEC §1 leaderboard example — enveloped, as the disk cache holds it.</summary>
        internal const string Board = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"", ""provisional"": true, ""bots_active"": true,
  ""end_at"": ""2026-08-19T00:00:00+00:00"", ""resolve_delay_minutes"": 60,
  ""entries"": [{""rank"":1,""is_tie"":false,""display_name"":""SMAUG"",""character_id"":""char_olivia"",
               ""level"":232,""strokes"":24,""thru"":6,""score_to_par"":1,
               ""is_player"":false,""is_bot"":false}],
  ""player"": {""rank"":14,""is_tie"":false,""display_name"":""Cratilo"",""character_id"":""char_james"",""level"":12,
             ""strokes"":4,""thru"":1,""score_to_par"":-1,""is_player"":true,""is_bot"":false,
             ""is_dnf"":false,""prize_rank"":3}
}}";

        /// <summary>The SAME payload with the envelope already stripped — what ApiEnvelope hands
        /// over on the live path, as opposed to the raw body the disk cache holds.</summary>
        internal const string BoardUnwrapped = @"{
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"", ""provisional"": true, ""bots_active"": true,
  ""end_at"": ""2026-08-19T00:00:00+00:00"", ""resolve_delay_minutes"": 60,
  ""entries"": [{""rank"":1,""is_tie"":false,""display_name"":""SMAUG"",""character_id"":""char_olivia"",
               ""level"":232,""strokes"":24,""thru"":6,""score_to_par"":1,
               ""is_player"":false,""is_bot"":false}],
  ""player"": {""rank"":14,""is_tie"":false,""display_name"":""Cratilo"",""character_id"":""char_james"",""level"":12,
             ""strokes"":4,""thru"":1,""score_to_par"":-1,""is_player"":true,""is_bot"":false,
             ""is_dnf"":false,""prize_rank"":3}
}";

        /// <summary>Bots retired: the two ranks agree, is_bot is gone from every row.</summary>
        internal const string BoardBotsRetired = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"", ""provisional"": false, ""bots_active"": false,
  ""end_at"": ""2026-08-19T00:00:00+00:00"", ""resolve_delay_minutes"": 60,
  ""entries"": [
    {""rank"":1,""is_tie"":false,""display_name"":""A"",""character_id"":""char_a"",""level"":5,
     ""strokes"":11,""thru"":3,""score_to_par"":-1,""is_player"":false,""is_bot"":false},
    {""rank"":2,""is_tie"":true,""display_name"":""B"",""character_id"":""char_b"",""level"":6,
     ""strokes"":12,""thru"":3,""score_to_par"":0,""is_player"":false,""is_bot"":false},
    {""rank"":2,""is_tie"":true,""display_name"":""Cratilo"",""character_id"":""char_james"",""level"":12,
     ""strokes"":12,""thru"":3,""score_to_par"":0,""is_player"":true,""is_bot"":false},
    {""rank"":4,""is_tie"":false,""display_name"":""D"",""character_id"":""char_d"",""level"":8,
     ""strokes"":13,""thru"":3,""score_to_par"":1,""is_player"":false,""is_bot"":false}
  ],
  ""player"": {""rank"":2,""is_tie"":true,""display_name"":""Cratilo"",""character_id"":""char_james"",""level"":12,
             ""strokes"":12,""thru"":3,""score_to_par"":0,""is_player"":true,""is_bot"":false,
             ""is_dnf"":false,""prize_rank"":2}
}}";

        /// <summary>Entered, nothing submitted: the caller's row exists with a null rank.</summary>
        internal const string BoardPlayerUnranked = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"", ""provisional"": true, ""bots_active"": true,
  ""end_at"": ""2026-08-19T00:00:00+00:00"", ""resolve_delay_minutes"": 60,
  ""entries"": [],
  ""player"": {""rank"":null,""is_tie"":false,""display_name"":""Cratilo"",""character_id"":""char_james"",
             ""level"":12,""strokes"":0,""thru"":0,""score_to_par"":0,""is_player"":true,
             ""is_bot"":false,""is_dnf"":false,""prize_rank"":null}
}}";

        /// <summary>Not entered: player is null and entries is empty.</summary>
        internal const string BoardNoPlayer = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"", ""provisional"": true, ""bots_active"": true,
  ""end_at"": ""2026-08-19T00:00:00+00:00"", ""resolve_delay_minutes"": 60,
  ""entries"": [], ""player"": null
}}";

        internal const string EnterOk = @"{""data"": {""entered"": true, ""already_entered"": false,
  ""entry"": {""character_id"": ""char_james"", ""status"": ""in_progress"", ""holes"": []}}}";

        internal const string EnterAlready = @"{""data"": {""entered"": false, ""already_entered"": true,
  ""entry"": {""character_id"": ""char_james"", ""status"": ""in_progress"", ""holes"": []}}}";

        internal const string EnterInsufficient =
            @"{""data"": {""entered"": false, ""status"": ""insufficient"", ""requested"": 250, ""total_points"": 30}}";

        /// <summary>Cross-device resume: the server has holes 1 and 2.</summary>
        internal const string EntryTwoHoles = @"{""data"": {
  ""character_id"": ""char_james"", ""status"": ""in_progress"", ""best_score"": null,
  ""entered_at"": ""2026-08-01T01:00:00+00:00"", ""submitted_at"": null,
  ""holes"": [
    {""hole_number"": 1, ""strokes"": 5, ""submitted_at"": ""2026-08-01T01:10:00+00:00""},
    {""hole_number"": 2, ""strokes"": 3, ""submitted_at"": ""2026-08-01T01:20:00+00:00""}
  ]
}}";

        /// <summary>GET /entry for a caller who has not entered.</summary>
        internal const string EntryNull = @"{""data"": null}";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  DTO parse
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class TournamentNetDtoParseTests
    {
        [Test]
        public void Parses_the_spec_board_through_the_data_envelope()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;
            Assert.IsNotNull(dto, "The raw {data:…} body is what the disk cache holds — it must parse.");

            Assert.AreEqual(true, AsyncProd.Field(dto, "Provisional"));
            Assert.AreEqual(true, AsyncProd.Field(dto, "BotsActive"));
            Assert.AreEqual(60,   AsyncProd.Field(dto, "ResolveDelayMinutes"));
        }

        [Test]
        public void Parses_an_already_unwrapped_body_identically()
        {
            // The live path hands over a body ApiEnvelope already unwrapped; the disk cache does not.
            // One reader has to survive both, or a cold open and a warm refresh disagree.
            object enveloped = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;
            object bare      = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardUnwrapped)!;

            Assert.AreEqual(AsyncProd.MapEntries(enveloped).Count, AsyncProd.MapEntries(bare).Count);
            Assert.AreEqual(AsyncProd.RowRank(AsyncProd.MapPlayer(enveloped)),
                            AsyncProd.RowRank(AsyncProd.MapPlayer(bare)));
        }

        [Test]
        public void Timestamps_survive_as_raw_strings_not_local_DateTimes()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;

            // If Newtonsoft's DateParseHandling had touched these they would come back as DateTime in
            // the machine's LOCAL zone — two players in different zones would then see two schedules.
            // The reader pins DateParseHandling.None on BOTH the JsonTextReader and the serializer;
            // either one alone leaves a hole this test would catch.
            Assert.IsInstanceOf<string>(AsyncProd.Field(dto, "FetchedAt"));
            Assert.IsInstanceOf<string>(AsyncProd.Field(dto, "EndAt"));
            Assert.AreEqual("2026-08-19T00:00:00+00:00", AsyncProd.Field(dto, "EndAt"));
        }

        [Test]
        public void Entry_timestamps_parse_to_the_same_absolute_instant_regardless_of_offset_spelling()
        {
            DateTime? z      = AsyncProd.ParseUtc("2026-08-01T01:10:00Z");
            DateTime? offset = AsyncProd.ParseUtc("2026-08-01T01:10:00+00:00");
            DateTime? naked  = AsyncProd.ParseUtc("2026-08-01T01:10:00");

            Assert.AreEqual(new DateTime(2026, 8, 1, 1, 10, 0, DateTimeKind.Utc), z);
            Assert.AreEqual(z, offset);
            Assert.AreEqual(z, naked, "A server that drops the offset must still be read as UTC.");
        }

        [Test]
        public void Null_player_is_a_row_less_sticky_not_a_crash()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardNoPlayer)!;
            object row = AsyncProd.MapPlayer(dto);

            Assert.IsFalse(AsyncProd.RowHasRow(row), "player:null means not entered — the sticky row hides.");
            Assert.AreEqual(0, AsyncProd.MapEntries(dto).Count);
        }

        [Test]
        public void Null_rank_survives_as_null_and_renders_as_a_dash()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardPlayerUnranked)!;
            object row = AsyncProd.MapPlayer(dto);

            Assert.IsTrue(AsyncProd.RowHasRow(row), "Entered-but-unranked still has a row.");
            Assert.IsNull(AsyncProd.RowRank(row), "rank:null must not collapse to 0 before the label sees it.");
            Assert.AreEqual("--", AsyncProd.RankLabel(row));
        }

        [Test]
        public void Insufficient_funds_enter_is_a_200_the_client_must_not_read_as_success()
        {
            object dto = AsyncProd.Read(AsyncProd.EnterDto, AsyncPayloads.EnterInsufficient)!;

            Assert.AreEqual(false, AsyncProd.Field(dto, "Entered"));
            Assert.AreEqual(true,  AsyncProd.Prop(dto, "IsInsufficient"),
                "A short balance answers HTTP 200 — branching on the status code alone would enter " +
                "a player who was never charged.");
            Assert.AreEqual(250L, AsyncProd.Field(dto, "Requested"));
            Assert.AreEqual(30L,  AsyncProd.Field(dto, "TotalPoints"));
        }

        [Test]
        public void Already_entered_enter_is_a_success_with_no_second_charge()
        {
            object dto = AsyncProd.Read(AsyncProd.EnterDto, AsyncPayloads.EnterAlready)!;

            Assert.AreEqual(false, AsyncProd.Field(dto, "Entered"));
            Assert.AreEqual(true,  AsyncProd.Field(dto, "AlreadyEntered"));
            Assert.AreEqual(false, AsyncProd.Prop(dto, "IsInsufficient"));
        }

        [Test]
        public void A_null_data_entry_is_null_not_an_exception()
        {
            Assert.IsNull(AsyncProd.Read(AsyncProd.EntryDto, AsyncPayloads.EntryNull),
                "{data:null} on GET /entry means 'not entered', which is a normal answer.");
        }

        [Test]
        public void A_corrupt_body_is_null_not_an_exception()
        {
            Assert.IsNull(AsyncProd.Read(AsyncProd.BoardDto, "{ this is not json"));
            Assert.IsNull(AsyncProd.Read(AsyncProd.BoardDto, ""));
            Assert.IsNull(AsyncProd.Read(AsyncProd.BoardDto, null));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  Mapping is verbatim — no client re-ranking
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class TournamentBoardMappingTests
    {
        [Test]
        public void Every_field_is_copied_from_the_payload_row()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;
            IReadOnlyList<TournamentLeaderboardEntry> rows = AsyncProd.MapEntries(dto);

            Assert.AreEqual(1, rows.Count);
            TournamentLeaderboardEntry r = rows[0];

            Assert.AreEqual(1,             r.Rank);
            Assert.AreEqual(false,         r.IsTie);
            Assert.AreEqual("SMAUG",       r.DisplayName);
            Assert.AreEqual("char_olivia", r.CharacterId);
            Assert.AreEqual(232,           r.Level);
            Assert.AreEqual(24,            r.Strokes);
            Assert.AreEqual(6,             r.Thru);
            Assert.IsFalse(r.IsPlayer);
            Assert.IsFalse(r.IsDNF);
            Assert.IsTrue(r.IsProvisional, "IsProvisional is the payload's `provisional`, board-level.");
        }

        [Test]
        public void TimeSeconds_is_zero_because_the_server_tiebreak_is_submission_order()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;

            Assert.AreEqual(0f, AsyncProd.MapEntries(dto)[0].TimeSeconds,
                "There is no time in the contract and no time column on the board — 0, not a guess.");
        }

        [Test]
        public void Standard_competition_ranking_is_rendered_verbatim_1_2_2_4()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardBotsRetired)!;
            IReadOnlyList<TournamentLeaderboardEntry> rows = AsyncProd.MapEntries(dto);

            // A client that re-ranked would produce 1,2,3,4 — which is exactly what must never happen.
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 4 },
                new[] { rows[0].Rank, rows[1].Rank, rows[2].Rank, rows[3].Rank });
            CollectionAssert.AreEqual(new[] { false, true, true, false },
                new[] { rows[0].IsTie, rows[1].IsTie, rows[2].IsTie, rows[3].IsTie });
        }

        [Test]
        public void Row_order_is_the_payload_order_not_a_client_sort()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardBotsRetired)!;
            IReadOnlyList<TournamentLeaderboardEntry> rows = AsyncProd.MapEntries(dto);

            CollectionAssert.AreEqual(new[] { "A", "B", "Cratilo", "D" },
                new[] { rows[0].DisplayName, rows[1].DisplayName, rows[2].DisplayName, rows[3].DisplayName });
        }

        [Test]
        public void A_final_board_maps_IsProvisional_false_onto_every_row()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardBotsRetired)!;

            foreach (TournamentLeaderboardEntry r in AsyncProd.MapEntries(dto))
                Assert.IsFalse(r.IsProvisional);
        }

        [Test]
        public void The_player_row_carries_both_ranks_off_the_payload()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;
            object row = AsyncProd.MapPlayer(dto);

            Assert.IsTrue(AsyncProd.RowHasRow(row));
            Assert.AreEqual(14, AsyncProd.RowRank(row));
            Assert.AreEqual(3,  AsyncProd.RowPrizeRank(row), "prize_rank is human-only — bots are never paid.");
            Assert.AreEqual("Cratilo", AsyncProd.RowEntry(row).DisplayName);
            Assert.IsTrue(AsyncProd.RowEntry(row).IsPlayer);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3  Sticky-row rank label
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class StickyRankLabelTests
    {
        [Test]
        public void Shows_both_ranks_while_bots_are_active_and_they_differ()
        {
            Assert.AreEqual("#14 · PRIZE #3", AsyncProd.FormatRankLabel(14, 3, botsActive: true));
        }

        [Test]
        public void Reverts_to_the_plain_rank_once_the_bots_retire()
        {
            Assert.AreEqual("14", AsyncProd.FormatRankLabel(14, 3, botsActive: false),
                "Bots retired one-way — the display rank IS the prize rank from then on.");
        }

        [Test]
        public void Shows_the_plain_rank_when_the_two_agree_even_with_bots_active()
        {
            Assert.AreEqual("3", AsyncProd.FormatRankLabel(3, 3, botsActive: true),
                "'#3 · PRIZE #3' would be noise, not information.");
        }

        [Test]
        public void Shows_a_dash_when_unranked()
        {
            Assert.AreEqual("--", AsyncProd.FormatRankLabel(null, null, botsActive: true));
            Assert.AreEqual("--", AsyncProd.FormatRankLabel(null, 3, botsActive: true));
        }

        [Test]
        public void Falls_back_to_the_display_rank_when_the_server_sends_no_prize_rank()
        {
            Assert.AreEqual("14", AsyncProd.FormatRankLabel(14, null, botsActive: true));
        }

        [Test]
        public void The_spec_payload_renders_the_spec_label()
        {
            // End to end from §1's example body, so the format is gated on the real payload rather
            // than on hand-picked arguments.
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.Board)!;
            Assert.AreEqual("#14 · PRIZE #3", AsyncProd.RankLabel(AsyncProd.MapPlayer(dto)));
        }

        [Test]
        public void A_retired_field_payload_renders_the_plain_rank()
        {
            object dto = AsyncProd.Read(AsyncProd.BoardDto, AsyncPayloads.BoardBotsRetired)!;
            Assert.AreEqual("2", AsyncProd.RankLabel(AsyncProd.MapPlayer(dto)));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4  Submit queue
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class TournamentSubmitQueueTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "golfin_submit_queue_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* best effort */ }
        }

        private string TempFile => Path.Combine(_dir, "tournament_pending_holes.json");

        [Test]
        public void Ops_replay_in_strict_FIFO()
        {
            object q = AsyncProd.NewQueue(new InMemoryPendingOpsStore());
            AsyncProd.Enqueue(q, AsyncFixture.Slug, 1, 5);
            AsyncProd.Enqueue(q, AsyncFixture.Slug, 2, 3);
            AsyncProd.Enqueue(q, AsyncFixture.Slug, 3, 4);

            // Order is not cosmetic: the server finishes the entry on the LAST hole and stamps
            // submitted_at, which is the final board's tiebreak.
            Assert.AreEqual(1, AsyncProd.OpHole(AsyncProd.Peek(q)!));
            Assert.AreEqual(1, AsyncProd.OpHole(AsyncProd.Dequeue(q)!));
            Assert.AreEqual(2, AsyncProd.OpHole(AsyncProd.Dequeue(q)!));
            Assert.AreEqual(3, AsyncProd.OpHole(AsyncProd.Dequeue(q)!));
            Assert.AreEqual(0, AsyncProd.QueueCount(q));
            Assert.IsNull(AsyncProd.Dequeue(q));
        }

        [Test]
        public void An_op_survives_a_restart_with_its_idempotency_key_intact()
        {
            var store = new FilePendingOpsStore(TempFile);

            object q1 = AsyncProd.NewQueue(store);
            object op = AsyncProd.Enqueue(q1, AsyncFixture.Slug, 2, 6);
            string key = AsyncProd.OpKey(op);

            Assert.IsTrue(File.Exists(TempFile), "Every mutation persists immediately — " +
                                                 "'enqueue then crash' must not lose a played hole.");

            // "Restart": a brand-new queue over the same file.
            object q2 = AsyncProd.NewQueue(new FilePendingOpsStore(TempFile));
            AsyncProd.LoadQueue(q2);

            Assert.AreEqual(1, AsyncProd.QueueCount(q2));
            object restored = AsyncProd.Peek(q2)!;

            Assert.AreEqual(key, AsyncProd.OpKey(restored),
                "The key is minted ONCE. Regenerating it on replay would defeat the server's " +
                "per-(entry,hole) idempotency and could write a second row.");
            Assert.AreEqual(AsyncFixture.Slug, AsyncProd.OpSlug(restored));
            Assert.AreEqual(2, AsyncProd.OpHole(restored));
            Assert.AreEqual(6, AsyncProd.OpStrokes(restored));
        }

        [Test]
        public void The_request_body_matches_the_server_contract()
        {
            object q  = AsyncProd.NewQueue(new InMemoryPendingOpsStore());
            object op = AsyncProd.Enqueue(q, AsyncFixture.Slug, 3, 7);
            string json = AsyncProd.OpRequestJson(op);

            StringAssert.Contains("\"hole_number\":3", json);
            StringAssert.Contains("\"strokes\":7", json);
            StringAssert.Contains("\"idempotency_key\":\"" + AsyncProd.OpKey(op) + "\"", json);
        }

        [Test]
        public void A_corrupt_queue_file_starts_empty_rather_than_throwing()
        {
            File.WriteAllText(TempFile, "{ not json at all");

            object q = AsyncProd.NewQueue(new FilePendingOpsStore(TempFile));
            AsyncProd.LoadQueue(q);

            Assert.AreEqual(0, AsyncProd.QueueCount(q));
        }

        [Test]
        public void A_key_less_op_on_disk_is_dropped_rather_than_replayed_blind()
        {
            // Without a key the server cannot recognise a replay, so re-sending could write a second
            // row for a hole it already holds. Dropping is the safe half of that trade.
            File.WriteAllText(TempFile,
                "{\"version\":1,\"ops\":[{\"key\":\"\",\"slug\":\"kasumigaseki_open\",\"hole\":1,\"strokes\":4}]}");

            object q = AsyncProd.NewQueue(new FilePendingOpsStore(TempFile));
            AsyncProd.LoadQueue(q);

            Assert.AreEqual(0, AsyncProd.QueueCount(q));
        }

        [Test]
        public void An_unknown_file_version_is_discarded_not_guessed_at()
        {
            File.WriteAllText(TempFile,
                "{\"version\":99,\"ops\":[{\"key\":\"k\",\"slug\":\"kasumigaseki_open\",\"hole\":1,\"strokes\":4}]}");

            object q = AsyncProd.NewQueue(new FilePendingOpsStore(TempFile));
            AsyncProd.LoadQueue(q);

            Assert.AreEqual(0, AsyncProd.QueueCount(q));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §5  Register on the remote path never debits the client ledger
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class RemoteRegisterNoDoubleChargeTests
    {
        private const long StartingBalance = 10_000L;
        private const long Fee             = 250L;

        private (object backend, FakeRewardPointsService rp, InMemoryEntryStore store) Make()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService(StartingBalance);
            var items = new FakeItemRewardService();

            LocalTournamentBackend local = AsyncFixture.Local(store, rp, items, clock, Fee);
            object backend = AsyncProd.NewBackend(
                local, store, rp, items,
                new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() },
                clock,
                AsyncProd.NewQueue(new InMemoryPendingOpsStore()));

            return (backend, rp, store);
        }

        [Test]
        public void Register_does_not_touch_IRewardPointsService_even_when_handed_a_fee()
        {
            var (backend, rp, _) = Make();

            // The SERVER debits entry_fee_pts inside POST /enter, with a deterministic uuid5(user:slug)
            // spend key. A client-side debit on top of that charges the player TWICE for one entry —
            // this is the seam that must stay silent.
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, Fee, "char_james");

            Assert.AreEqual(StartingBalance, rp.Balance,
                $"The remote path must not spend {Fee}RP locally — the server already took it.");
        }

        [Test]
        public void Register_still_mirrors_the_entry_so_the_gameplay_flow_can_read_it_synchronously()
        {
            var (backend, _, store) = Make();

            EntryState entry = AsyncProd.RegisterSync(backend, AsyncFixture.Slug, Fee, "char_james");

            Assert.IsNotNull(entry);
            Assert.AreEqual("char_james", entry.CharacterId);
            Assert.AreEqual(EntryStatus.InProgress, entry.Status);
            Assert.IsNotNull(store.Load(AsyncFixture.Slug), "The mid-round read model is the local store.");
        }

        [Test]
        public void Re_registering_returns_the_existing_entry_and_still_charges_nothing()
        {
            var (backend, rp, _) = Make();

            EntryState first  = AsyncProd.RegisterSync(backend, AsyncFixture.Slug, Fee, "char_james");
            EntryState second = AsyncProd.RegisterSync(backend, AsyncFixture.Slug, Fee, "char_james");

            Assert.AreSame(first, second, "Idempotent: an existing entry short-circuits before the POST.");
            Assert.AreEqual(StartingBalance, rp.Balance);
        }

        [Test]
        public void The_local_backend_by_contrast_DOES_debit_which_is_why_the_remote_path_must_not()
        {
            // The control for the test above. If this ever stops failing to debit, the two paths have
            // converged and the double-charge is back.
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var rp    = new FakeRewardPointsService(StartingBalance);
            LocalTournamentBackend local = AsyncFixture.Local(
                new InMemoryEntryStore(), rp, new FakeItemRewardService(), clock, Fee);

            local.Register(AsyncFixture.Slug, Fee, "char_james");

            Assert.AreEqual(StartingBalance - Fee, rp.Balance);
        }

        [Test]
        public void Hole_ids_map_to_the_1_based_hole_numbers_the_server_validates()
        {
            var (backend, _, _) = Make();

            Assert.AreEqual(1, AsyncProd.HoleNumberFor(backend, AsyncFixture.Slug, "h1"));
            Assert.AreEqual(3, AsyncProd.HoleNumberFor(backend, AsyncFixture.Slug, "h3"));
            Assert.AreEqual(0, AsyncProd.HoleNumberFor(backend, AsyncFixture.Slug, "h9"),
                "A hole outside the set would be a permanent 400 — it must never reach the queue.");
            Assert.AreEqual(0, AsyncProd.HoleNumberFor(backend, "no_such_tournament", "h1"));
        }

        [Test]
        public void SubmitHoleResult_persists_locally_first_and_then_queues_the_hole()
        {
            var (backend, _, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, Fee, "char_james");

            object queue = AsyncProd.Prop(backend, "Queue")!;
            MethodInfo submit = AsyncProd.Backend.GetMethod("SubmitHoleResult")!;
            var updated = (EntryState)submit.Invoke(backend,
                new object[] { AsyncFixture.Slug, AsyncFixture.Hole("h1", 5) })!;

            // Local first: a player who holes out in a tunnel has finished that hole, and nothing
            // about the network may take it back.
            Assert.AreEqual(1, updated.PerHole.Count);
            Assert.AreEqual(1, store.Load(AsyncFixture.Slug)!.PerHole.Count);

            Assert.AreEqual(1, AsyncProd.QueueCount(queue));
            object op = AsyncProd.Peek(queue)!;
            Assert.AreEqual(1, AsyncProd.OpHole(op));
            Assert.AreEqual(5, AsyncProd.OpStrokes(op));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §6  Provider selection
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class TournamentBackendPolicyTests
    {
        [Test]
        public void Bot_session_override_stays_local_even_though_it_reports_signed_in()
        {
            // BotSessionOverride installs a fake identity whose token is a literal placeholder, so an
            // auth check alone would aim entry POSTs at production AND pollute the human-entry count
            // that retires the bot field one-way.
            Assert.AreEqual("Local", AsyncProd.Choose(botOverride: true, signedIn: true, isDemo: false));
        }

        [Test]
        public void A_demo_build_stays_local()
        {
            Assert.AreEqual("Local", AsyncProd.Choose(botOverride: false, signedIn: true, isDemo: true));
        }

        [Test]
        public void A_signed_out_player_stays_local()
        {
            // Every tournament endpoint requires a bearer token, so the server has nothing to tell them.
            Assert.AreEqual("Local", AsyncProd.Choose(botOverride: false, signedIn: false, isDemo: false));
        }

        [Test]
        public void A_signed_in_player_gets_the_shared_server_board()
        {
            Assert.AreEqual("Remote", AsyncProd.Choose(botOverride: false, signedIn: true, isDemo: false));
        }

        [Test]
        public void The_bot_override_wins_over_everything_else()
        {
            foreach (bool signedIn in new[] { true, false })
            foreach (bool demo in new[] { true, false })
                Assert.AreEqual("Local", AsyncProd.Choose(true, signedIn, demo));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §7  Cross-device entry reconcile
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class RemoteEntryReconcileTests
    {
        private (object backend, InMemoryEntryStore store) Make()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(2));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService(10_000L);
            var items = new FakeItemRewardService();

            LocalTournamentBackend local = AsyncFixture.Local(store, rp, items, clock);
            object backend = AsyncProd.NewBackend(
                local, store, rp, items,
                new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() },
                clock,
                AsyncProd.NewQueue(new InMemoryPendingOpsStore()));

            return (backend, store);
        }

        private static object ServerEntry(string json)
            => AsyncProd.Read(AsyncProd.EntryDto, json)!;

        [Test]
        public void Holes_played_on_another_device_appear_locally()
        {
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            Assert.IsTrue(AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug,
                ServerEntry(AsyncPayloads.EntryTwoHoles)));

            EntryState entry = store.Load(AsyncFixture.Slug)!;
            Assert.AreEqual(2, entry.PerHole.Count);
            CollectionAssert.AreEqual(new[] { "h1", "h2" }, new[] { entry.PerHole[0].HoleId, entry.PerHole[1].HoleId });
            CollectionAssert.AreEqual(new[] { 5, 3 },       new[] { entry.PerHole[0].Strokes, entry.PerHole[1].Strokes });
        }

        [Test]
        public void The_server_wins_on_a_hole_both_sides_hold()
        {
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            // Local thinks hole 1 was a 9; the server says 5.
            MethodInfo submit = AsyncProd.Backend.GetMethod("SubmitHoleResult")!;
            submit.Invoke(backend, new object[] { AsyncFixture.Slug, AsyncFixture.Hole("h1", 9) });

            AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug, ServerEntry(AsyncPayloads.EntryTwoHoles));

            EntryState entry = store.Load(AsyncFixture.Slug)!;
            Assert.AreEqual(5, entry.PerHole[0].Strokes, "Server wins on conflict — cross-device resume.");
        }

        [Test]
        public void A_local_only_hole_still_waiting_in_the_queue_is_not_erased()
        {
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            MethodInfo submit = AsyncProd.Backend.GetMethod("SubmitHoleResult")!;
            submit.Invoke(backend, new object[] { AsyncFixture.Slug, AsyncFixture.Hole("h1", 5) });
            submit.Invoke(backend, new object[] { AsyncFixture.Slug, AsyncFixture.Hole("h2", 3) });
            submit.Invoke(backend, new object[] { AsyncFixture.Slug, AsyncFixture.Hole("h3", 4) });

            // The server has only the first two — hole 3 is still queued (airplane mode).
            AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug, ServerEntry(AsyncPayloads.EntryTwoHoles));

            EntryState entry = store.Load(AsyncFixture.Slug)!;
            Assert.AreEqual(3, entry.PerHole.Count,
                "Dropping a hole the server has not seen yet would erase one the player actually played.");
            Assert.AreEqual("h3", entry.PerHole[2].HoleId);
            Assert.AreEqual(4,    entry.PerHole[2].Strokes);
        }

        [Test]
        public void All_holes_present_marks_the_entry_finished()
        {
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            const string threeHoles = @"{""data"": {
              ""character_id"": ""char_james"", ""status"": ""finished"", ""best_score"": 12,
              ""entered_at"": ""2026-08-01T01:00:00+00:00"", ""submitted_at"": ""2026-08-01T01:40:00+00:00"",
              ""holes"": [
                {""hole_number"": 1, ""strokes"": 5, ""submitted_at"": ""2026-08-01T01:10:00+00:00""},
                {""hole_number"": 2, ""strokes"": 3, ""submitted_at"": ""2026-08-01T01:20:00+00:00""},
                {""hole_number"": 3, ""strokes"": 4, ""submitted_at"": ""2026-08-01T01:30:00+00:00""}
              ]}}";

            AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug, ServerEntry(threeHoles));

            Assert.AreEqual(EntryStatus.Finished, store.Load(AsyncFixture.Slug)!.Status);
        }

        [Test]
        public void A_hole_number_outside_the_hole_set_is_ignored_rather_than_indexed()
        {
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            const string bogus = @"{""data"": {
              ""character_id"": ""char_james"", ""status"": ""in_progress"",
              ""holes"": [{""hole_number"": 99, ""strokes"": 4, ""submitted_at"": ""2026-08-01T01:10:00+00:00""}]}}";

            Assert.DoesNotThrow(() =>
                AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug, ServerEntry(bogus)));
            Assert.AreEqual(0, store.Load(AsyncFixture.Slug)!.PerHole.Count);
        }

        [Test]
        public void The_frozen_character_snapshot_survives_a_reconcile()
        {
            // The snapshot is captured at sign-up and the server has no copy, so a reconcile that
            // rebuilt the entry from the payload alone would silently un-freeze the tournament
            // character's stats.
            var (backend, store) = Make();
            AsyncProd.RegisterSync(backend, AsyncFixture.Slug, 250L, "char_james");

            var seeded = new EntryState(
                tournamentId: AsyncFixture.Slug,
                characterId:  "char_james",
                snapshot:     new CharacterSnapshot("char_james", 12, 20, 21, 22, 23),
                perHole:      new List<HoleResult>(),
                startedUtc:   AsyncFixture.StartUtc.AddHours(1),
                lastHoleUtc:  null,
                status:       EntryStatus.InProgress);
            store.Save(seeded);

            AsyncProd.ApplyServerEntry(backend, AsyncFixture.Slug, ServerEntry(AsyncPayloads.EntryTwoHoles));

            CharacterSnapshot? snap = store.Load(AsyncFixture.Slug)!.Snapshot;
            Assert.IsNotNull(snap);
            Assert.AreEqual(20, snap!.Strength);
            Assert.AreEqual(23, snap.Stamina);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §8  Queue drain over a scripted transport
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Answers each request from a scripted list, in order, and records the bodies it was given.
    /// Coroutine-shaped like the real transport so the routine under test is pumped exactly as
    /// <c>ApiClient</c> would pump it — no play mode, no socket.
    /// </summary>
    internal sealed class ScriptedTransport : IHttpTransport
    {
        private readonly Queue<HttpResponse> _script = new Queue<HttpResponse>();

        internal readonly List<string> SentBodies = new List<string>();
        internal readonly List<string> SentUrls   = new List<string>();

        internal ScriptedTransport Then(HttpResponse r) { _script.Enqueue(r); return this; }

        internal int Remaining => _script.Count;

        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            SentUrls.Add(request.Url);
            SentBodies.Add(request.Body ?? string.Empty);

            // A script that runs dry means the routine sent MORE requests than the test expected —
            // surface it as a connection failure so the assertion below fails on the count, loudly.
            HttpResponse response = _script.Count > 0
                ? _script.Dequeue()
                : HttpResponse.ConnectionFailure("scripted transport exhausted");

            onResponse(response);
            yield break;
        }
    }

    /// <summary>No session — every request goes out unauthenticated, which the fake never checks.</summary>
    internal sealed class NoAuth : IAuthTokenProvider
    {
        public bool IsAuthenticated => false;
        public string AccessToken => null!;
        public IEnumerator Refresh(Action<bool> onDone) { onDone(false); yield break; }
    }

    /// <summary>Runs nothing: the tests pump the routines they care about by hand.</summary>
    internal sealed class NullRunner : ICoroutineRunner
    {
        public void Run(IEnumerator routine) { }
    }

    public sealed class TournamentSubmitDrainTests
    {
        private ScriptedTransport _transport = null!;

        [SetUp]
        public void SetUp()
        {
            _transport = new ScriptedTransport();
            var client = new ApiClient(_transport, new NoAuth(), new NullRunner())
            {
                MaxTransientRetries = 0,      // one attempt per op; the retry budget is ApiClient's own test
                RetryDelaySeconds   = 0f,     // never spin on wall-clock inside a pumped routine
                LogRequests         = false
            };
            ApiClient.ConfigureForTest(client);
        }

        [TearDown]
        public void TearDown() => ApiClient.ResetForTest();

        private (object backend, object queue) Make()
        {
            var clock = new AsyncClock(AsyncFixture.StartUtc.AddHours(1));
            var store = new InMemoryEntryStore();
            var rp    = new FakeRewardPointsService(10_000L);
            var items = new FakeItemRewardService();

            object queue = AsyncProd.NewQueue(new InMemoryPendingOpsStore());
            object backend = AsyncProd.NewBackend(
                AsyncFixture.Local(store, rp, items, clock), store, rp, items,
                new Dictionary<string, PrizeTable> { ["pt1"] = AsyncFixture.Prize() },
                clock, queue);

            return (backend, queue);
        }

        /// <summary>Pump the drain to completion and return how many ops left the queue.</summary>
        private static int Drain(object backend)
        {
            int drained = -1;
            IEnumerator routine = AsyncProd.FlushRoutine(backend, n => drained = n);
            while (routine.MoveNext()) { }
            return drained;
        }

        private const string Ok       = @"{""data"": {""replayed"": false, ""hole"": {""hole_number"": 1, ""strokes"": 4}}}";
        private const string Replayed = @"{""data"": {""replayed"": true,  ""hole"": {""hole_number"": 1, ""strokes"": 4}}}";
        private const string Rejected = @"{""detail"": ""hole 9 is not in this tournament's hole set""}";

        [Test]
        public void A_delivered_op_leaves_the_queue()
        {
            var (backend, queue) = Make();
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            _transport.Then(HttpResponse.Status(200, Ok));

            Assert.AreEqual(1, Drain(backend));
            Assert.AreEqual(0, AsyncProd.QueueCount(queue));
        }

        [Test]
        public void Replayed_true_is_a_success_and_drops_the_op()
        {
            // This is what an ambiguous timeout followed by a retry looks like: the server already has
            // the hole. Treating it as a failure would wedge the queue forever on a hole that landed.
            var (backend, queue) = Make();
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            _transport.Then(HttpResponse.Status(200, Replayed));

            Assert.AreEqual(1, Drain(backend));
            Assert.AreEqual(0, AsyncProd.QueueCount(queue));
        }

        [Test]
        public void A_400_is_a_verdict_and_drops_the_op_rather_than_retrying_forever()
        {
            var (backend, queue) = Make();
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            _transport.Then(HttpResponse.Status(400, Rejected));

            Assert.AreEqual(1, Drain(backend));
            Assert.AreEqual(0, AsyncProd.QueueCount(queue),
                "A rejected body is rejected every time — keeping it would block every hole behind it.");
        }

        [Test]
        public void A_transient_failure_keeps_the_op_and_stops_the_drain()
        {
            var (backend, queue) = Make();
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 2, 3);
            _transport.Then(HttpResponse.ConnectionFailure("airplane mode"));

            Assert.AreEqual(0, Drain(backend));
            Assert.AreEqual(2, AsyncProd.QueueCount(queue));
            Assert.AreEqual(1, _transport.SentBodies.Count,
                "The drain stops at the first undelivered op — it must not skip ahead to hole 2.");
        }

        [Test]
        public void The_drain_is_strict_FIFO_and_resumes_where_it_stopped()
        {
            var (backend, queue) = Make();
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 5);
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 2, 3);
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 3, 4);

            // Hole 1 lands, hole 2 does not. Ordering is not cosmetic: the server finishes the entry
            // on the LAST hole and stamps submitted_at, the final board's tiebreak.
            _transport.Then(HttpResponse.Status(200, Ok))
                      .Then(HttpResponse.ConnectionFailure("dropped"));

            Assert.AreEqual(1, Drain(backend));
            Assert.AreEqual(2, AsyncProd.QueueCount(queue));
            Assert.AreEqual(2, AsyncProd.OpHole(AsyncProd.Peek(queue)!));

            StringAssert.Contains("\"hole_number\":1", _transport.SentBodies[0]);
            StringAssert.Contains("\"hole_number\":2", _transport.SentBodies[1]);

            // Reconnect: the rest drains, still in order.
            _transport.Then(HttpResponse.Status(200, Ok)).Then(HttpResponse.Status(200, Ok));
            Assert.AreEqual(2, Drain(backend));
            Assert.AreEqual(0, AsyncProd.QueueCount(queue));

            StringAssert.Contains("\"hole_number\":2", _transport.SentBodies[2]);
            StringAssert.Contains("\"hole_number\":3", _transport.SentBodies[3]);
        }

        [Test]
        public void A_replayed_op_carries_the_SAME_idempotency_key_it_was_minted_with()
        {
            var (backend, queue) = Make();
            object op = AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            string key = AsyncProd.OpKey(op);

            _transport.Then(HttpResponse.ConnectionFailure("dropped"));
            Drain(backend);

            _transport.Then(HttpResponse.Status(200, Replayed));
            Drain(backend);

            Assert.AreEqual(2, _transport.SentBodies.Count);
            foreach (string body in _transport.SentBodies)
                StringAssert.Contains("\"idempotency_key\":\"" + key + "\"", body,
                    "A regenerated key would defeat the server's per-(entry,hole) idempotency.");
        }

        [Test]
        public void The_drain_posts_to_the_submit_hole_endpoint_for_the_right_slug()
        {
            var (backend, _) = Make();
            object queue = AsyncProd.Prop(backend, "Queue")!;
            AsyncProd.Enqueue(queue, AsyncFixture.Slug, 1, 4);
            _transport.Then(HttpResponse.Status(200, Ok));

            Drain(backend);

            Assert.AreEqual(Endpoints.TournamentSubmitHole(AsyncFixture.Slug), _transport.SentUrls[0]);
        }

        [Test]
        public void An_empty_queue_drains_to_zero_without_sending_anything()
        {
            var (backend, _) = Make();

            Assert.AreEqual(0, Drain(backend));
            Assert.AreEqual(0, _transport.SentBodies.Count);
        }
    }
}
