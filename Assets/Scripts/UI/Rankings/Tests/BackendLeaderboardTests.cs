// ─────────────────────────────────────────────────────────────────────────────
// BackendLeaderboardTests — leaderboard_backend SPEC §7 (EditMode)
//
// ASSEMBLY: Golfin.UI.Rankings.Tests (Editor-only, references Core + Save)
//
// Same access pattern as BannerPolicyTests / RemoteScheduleTests, and for the same
// reason: the production types under test live in Assembly-CSharp
// (Assets/Scripts/UI/Rankings/, no asmdef), which an asmdef CANNOT reference. They
// are reached by REFLECTION. Everything asserted is either a primitive or a
// Golfin.UI.Rankings.Core type (LeaderboardEntry, LeaderboardPeriod), both of which
// this assembly references directly — so the assertions need no casting games.
//
// COVERAGE (SPEC §7 EditMode list, in order)
//   §1  DTO parse of the §1 payload, incl. null character_id and null period_end_utc
//   §2  Mapping is VERBATIM — rank/is_tie are never recomputed client-side
//   §3  Countdown end-time math survives a ±10 min device clock skew
//   §4  Disk cache round-trip; a corrupt cache file → null → empty board + refresh
//   §5  Provider selection: bot override → LocalFake, signed-in → Backend
//   §6  Character-sync payload + throttle (SPEC §5)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Golfin.UI.Rankings;

namespace Golfin.UI.Rankings.Tests
{
    /// <summary>Reflection handles onto the Assembly-CSharp leaderboard types.</summary>
    internal static class BoardProd
    {
        private static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. It should live in Assembly-CSharp " +
                "(Assets/Scripts/UI/Rankings/, no asmdef).");
        }

        internal static readonly Type Provider   = Find("Golfin.UI.Rankings.BackendLeaderboardProvider");
        internal static readonly Type DiskCache  = Find("Golfin.UI.Rankings.LeaderboardDiskCache");
        internal static readonly Type Policy     = Find("Golfin.UI.Rankings.LeaderboardProviderPolicy");
        internal static readonly Type SyncPolicy = Find("Golfin.UI.Rankings.GolfinCharacterSyncPolicy");

        private const BindingFlags AnyStatic =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static object? Call(Type t, string method, params object?[] args)
        {
            var m = t.GetMethod(method, AnyStatic)
                    ?? throw new InvalidOperationException($"{t.Name}.{method} not found.");
            return m.Invoke(null, args);
        }

        // ── Provider statics ──────────────────────────────────────────────────

        /// <summary>The private <c>Deserialize(json, source)</c>. Returns the DTO, or null.</summary>
        internal static object? Deserialize(string? json) => Call(Provider, "Deserialize", json, "test");

        /// <summary>The private <c>MapEntries(dto)</c>, already typed as the Core struct.</summary>
        internal static IReadOnlyList<LeaderboardEntry> MapEntries(object dto)
            => (IReadOnlyList<LeaderboardEntry>)Call(Provider, "MapEntries", dto)!;

        /// <summary>The private <c>MapPlayer(dto)</c> — nullable, so a missing player stays visible.</summary>
        internal static LeaderboardEntry? MapPlayer(object dto)
            => (LeaderboardEntry?)Call(Provider, "MapPlayer", dto);

        internal static DateTime AdjustedPeriodEnd(string? periodEndUtc, string? fetchedAt, DateTime localNowAtFetch)
            => (DateTime)Call(Provider, "AdjustedPeriodEnd", periodEndUtc, fetchedAt, localNowAtFetch)!;

        internal static DateTime? ParseUtc(string? value) => (DateTime?)Call(Provider, "ParseUtc", value);

        internal static string WirePeriod(LeaderboardPeriod period)
            => (string)Call(Provider, "WirePeriod", period)!;

        /// <summary>Read a public field off the DTO by name, whatever its declared type.</summary>
        internal static object? DtoField(object dto, string field)
            => dto.GetType().GetField(field)!.GetValue(dto);

        // ── Disk cache ────────────────────────────────────────────────────────

        internal static string CachePath(LeaderboardPeriod p) => (string)Call(DiskCache, "CachePath", p)!;
        internal static string CacheFileName(LeaderboardPeriod p) => (string)Call(DiskCache, "CacheFileName", p)!;
        internal static string? ReadCache(LeaderboardPeriod p) => (string?)Call(DiskCache, "ReadCache", p);
        internal static void WriteCache(LeaderboardPeriod p, string json) => Call(DiskCache, "WriteCache", p, json);
        internal static void ClearCache(LeaderboardPeriod p) => Call(DiskCache, "ClearCache", p);

        // ── Policies ──────────────────────────────────────────────────────────

        /// <summary>Returns the enum NAME so the test needs no Assembly-CSharp enum reference.</summary>
        internal static string Choose(bool botOverride, bool signedIn)
            => Call(Policy, "Choose", botOverride, signedIn)!.ToString()!;

        internal static string? BuildPayload(string? characterId, int level)
            => (string?)Call(SyncPolicy, "BuildPayload", characterId, level);

        internal static bool ShouldSend(string? payload, string? lastSent)
            => (bool)Call(SyncPolicy, "ShouldSend", payload, lastSent)!;

        // ── Live provider ─────────────────────────────────────────────────────

        /// <summary>Construct the real provider against a fixed clock (it reads the disk cache at ctor).</summary>
        internal static ILeaderboardProvider NewProvider(ITimeProvider time)
        {
            var ctor = Provider.GetConstructor(new[] { typeof(ITimeProvider) })
                       ?? throw new InvalidOperationException("BackendLeaderboardProvider(ITimeProvider) not found.");
            return (ILeaderboardProvider)ctor.Invoke(new object?[] { time });
        }
    }

    /// <summary>Fixed clock, so the skew arithmetic under test is the only variable.</summary>
    internal sealed class FixedTime : ITimeProvider
    {
        public FixedTime(DateTime utcNow) { UtcNow = utcNow; }
        public DateTime UtcNow { get; }
        public bool IsAuthoritative => true;
    }

    /// <summary>The SPEC §1 payload, verbatim, plus the variants the client must survive.</summary>
    internal static class Payloads
    {
        /// <summary>SPEC §1 example, character_id present on both rows.</summary>
        internal const string SpecExample = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"",
  ""period"": ""daily"",
  ""period_end_utc"": ""2026-08-19T00:00:00+00:00"",
  ""entries"": [
    {""rank"": 1, ""is_tie"": false, ""display_name"": ""SMAUG"", ""character_id"": ""char_olivia"",
     ""level"": 232, ""score"": 312, ""is_player"": false},
    {""rank"": 2, ""is_tie"": true, ""display_name"": ""Cratilo"", ""character_id"": ""char_james"",
     ""level"": 12, ""score"": 220, ""is_player"": true}
  ],
  ""player"": {""rank"": 2, ""is_tie"": true, ""display_name"": ""Cratilo"",
             ""character_id"": ""char_james"", ""level"": 12, ""score"": 220}
}}";

        /// <summary>
        /// The SAME payload with the <c>{"data": …}</c> envelope already stripped — what
        /// <c>ApiEnvelope</c> hands over on the live path, as opposed to the raw body the disk cache
        /// holds. Written out in full rather than derived from <see cref="SpecExample"/> by string
        /// surgery, which is its own source of bugs.
        /// </summary>
        internal const string SpecExampleUnwrapped = @"{
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"",
  ""period"": ""daily"",
  ""period_end_utc"": ""2026-08-19T00:00:00+00:00"",
  ""entries"": [
    {""rank"": 1, ""is_tie"": false, ""display_name"": ""SMAUG"", ""character_id"": ""char_olivia"",
     ""level"": 232, ""score"": 312, ""is_player"": false},
    {""rank"": 2, ""is_tie"": true, ""display_name"": ""Cratilo"", ""character_id"": ""char_james"",
     ""level"": 12, ""score"": 220, ""is_player"": true}
  ],
  ""player"": {""rank"": 2, ""is_tie"": true, ""display_name"": ""Cratilo"",
             ""character_id"": ""char_james"", ""level"": 12, ""score"": 220}
}";

        /// <summary>A PLAYLIFE-only user (null character_id) and the historic board (null period_end_utc).</summary>
        internal const string NullsEverywhere = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"",
  ""period"": ""historic"",
  ""period_end_utc"": null,
  ""entries"": [
    {""rank"": 1, ""is_tie"": false, ""display_name"": ""NEVERSYNCED"", ""character_id"": null,
     ""level"": 0, ""score"": 900000, ""is_player"": false}
  ],
  ""player"": {""rank"": 4181, ""is_tie"": false, ""display_name"": ""Cratilo"",
             ""character_id"": null, ""level"": 1, ""score"": 0}
}}";

        /// <summary>
        /// Standard competition ranking straight off the server: 1,2,2,4. A client that re-ranked would
        /// produce 1,2,3,4 — which is exactly what §2 asserts never happens.
        /// </summary>
        internal const string TiedRanks = @"{""data"": {
  ""fetched_at"": ""2026-08-18T05:30:00+00:00"",
  ""period"": ""weekly"",
  ""period_end_utc"": ""2026-08-24T00:00:00+00:00"",
  ""entries"": [
    {""rank"": 1, ""is_tie"": false, ""display_name"": ""A"", ""character_id"": ""char_a"", ""level"": 5, ""score"": 500, ""is_player"": false},
    {""rank"": 2, ""is_tie"": true,  ""display_name"": ""B"", ""character_id"": ""char_b"", ""level"": 6, ""score"": 400, ""is_player"": false},
    {""rank"": 2, ""is_tie"": true,  ""display_name"": ""C"", ""character_id"": ""char_c"", ""level"": 7, ""score"": 400, ""is_player"": false},
    {""rank"": 4, ""is_tie"": false, ""display_name"": ""D"", ""character_id"": ""char_d"", ""level"": 8, ""score"": 300, ""is_player"": false}
  ],
  ""player"": {""rank"": 4, ""is_tie"": false, ""display_name"": ""D"", ""character_id"": ""char_d"", ""level"": 8, ""score"": 300}
}}";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  DTO parse
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LeaderboardDtoParseTests
    {
        [Test]
        public void Parses_the_spec_payload_through_the_data_envelope()
        {
            object dto = BoardProd.Deserialize(Payloads.SpecExample)!;
            Assert.IsNotNull(dto, "The raw {data:…} body is what the disk cache holds — it must parse.");

            Assert.AreEqual("2026-08-18T05:30:00+00:00", BoardProd.DtoField(dto, "FetchedAt"));
            Assert.AreEqual("daily", BoardProd.DtoField(dto, "Period"));
            Assert.AreEqual("2026-08-19T00:00:00+00:00", BoardProd.DtoField(dto, "PeriodEndUtc"));
        }

        [Test]
        public void Parses_an_already_unwrapped_body_too()
        {
            // The live path hands over a body ApiEnvelope already unwrapped; the disk cache does not.
            // One reader has to survive both, or a cold open and a warm refresh disagree.
            object? dto = BoardProd.Deserialize(Payloads.SpecExampleUnwrapped);
            Assert.IsNotNull(dto, "An unwrapped payload must parse identically to the enveloped one.");
            Assert.AreEqual("daily", BoardProd.DtoField(dto!, "Period"));

            // …and identically means identically: the same rows come out either way.
            Assert.AreEqual(BoardProd.MapEntries(BoardProd.Deserialize(Payloads.SpecExample)!).Count,
                            BoardProd.MapEntries(dto!).Count);
        }

        [Test]
        public void Timestamps_survive_as_raw_strings_not_local_DateTimes()
        {
            object dto = BoardProd.Deserialize(Payloads.SpecExample)!;

            // If Newtonsoft's DateParseHandling had touched these they would come back as DateTime in
            // the machine's LOCAL zone — two players in different zones would then see two countdowns.
            Assert.IsInstanceOf<string>(BoardProd.DtoField(dto, "FetchedAt"));
            Assert.IsInstanceOf<string>(BoardProd.DtoField(dto, "PeriodEndUtc"));
        }

        [Test]
        public void Null_character_id_becomes_an_empty_string_not_a_crash()
        {
            object dto = BoardProd.Deserialize(Payloads.NullsEverywhere)!;
            IReadOnlyList<LeaderboardEntry> entries = BoardProd.MapEntries(dto);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(string.Empty, entries[0].CharacterId,
                "A null character_id is normal (PLAYLIFE-only users). Empty string is what the widgets " +
                "already treat as 'use the default portrait'.");
        }

        [Test]
        public void Null_period_end_utc_yields_MaxValue_so_the_countdown_blanks()
        {
            DateTime end = BoardProd.AdjustedPeriodEnd(null, "2026-08-18T05:30:00+00:00",
                                                       new DateTime(2026, 8, 18, 5, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual(DateTime.MaxValue, end,
                "Historic never resets — UpdateCountdownLabel blanks the label on DateTime.MaxValue.");
        }

        [Test]
        public void A_corrupt_body_is_null_not_an_exception()
        {
            Assert.IsNull(BoardProd.Deserialize("{ this is not json"));
            Assert.IsNull(BoardProd.Deserialize(""));
            Assert.IsNull(BoardProd.Deserialize(null));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  Mapping is verbatim — the server owns the ranking
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LeaderboardMappingTests
    {
        [Test]
        public void Every_field_is_copied_verbatim()
        {
            object dto = BoardProd.Deserialize(Payloads.SpecExample)!;
            IReadOnlyList<LeaderboardEntry> entries = BoardProd.MapEntries(dto);

            Assert.AreEqual(2, entries.Count);

            LeaderboardEntry first = entries[0];
            Assert.AreEqual(1,             first.Rank);
            Assert.AreEqual(false,         first.IsTie);
            Assert.AreEqual("SMAUG",       first.DisplayName);
            Assert.AreEqual("char_olivia", first.CharacterId);
            Assert.AreEqual(232,           first.Level);
            Assert.AreEqual(312L,          first.Score);
            Assert.AreEqual(false,         first.IsPlayer);

            LeaderboardEntry second = entries[1];
            Assert.AreEqual(2,            second.Rank);
            Assert.AreEqual(true,         second.IsTie);
            Assert.AreEqual("char_james", second.CharacterId);
            Assert.AreEqual(220L,         second.Score);
            Assert.AreEqual(true,         second.IsPlayer,
                "is_player marks the caller's row inside the top slice.");
        }

        [Test]
        public void Server_ties_are_rendered_as_sent_never_recomputed()
        {
            object dto = BoardProd.Deserialize(Payloads.TiedRanks)!;
            IReadOnlyList<LeaderboardEntry> entries = BoardProd.MapEntries(dto);

            CollectionAssert.AreEqual(new[] { 1, 2, 2, 4 }, Ranks(entries),
                "Standard competition ranking (1,2,2,4) arrives computed. A client-side re-rank would " +
                "produce 1,2,3,4 and silently disagree with the server.");

            CollectionAssert.AreEqual(new[] { false, true, true, false }, Ties(entries),
                "is_tie drives the 'T2' prefix and is the server's call, not a score-frequency count here.");
        }

        [Test]
        public void Order_is_the_payload_order_not_a_client_sort()
        {
            object dto = BoardProd.Deserialize(Payloads.TiedRanks)!;
            IReadOnlyList<LeaderboardEntry> entries = BoardProd.MapEntries(dto);

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, Names(entries),
                "entries arrives already sorted; re-sorting would reorder equal-score rows arbitrarily.");
        }

        [Test]
        public void Player_object_is_mapped_and_always_flagged_as_the_player()
        {
            object dto = BoardProd.Deserialize(Payloads.NullsEverywhere)!;
            LeaderboardEntry? player = BoardProd.MapPlayer(dto);

            Assert.IsTrue(player.HasValue, "player is ALWAYS present, even at score 0 outside the slice.");
            Assert.AreEqual(4181, player!.Value.Rank, "A rank far outside the top 100 still pins correctly.");
            Assert.AreEqual(0L,   player.Value.Score);
            Assert.IsTrue(player.Value.IsPlayer,
                "The player object carries no is_player field — it is the caller by definition.");
        }

        [Test]
        public void Wire_period_names_match_the_endpoint_spelling()
        {
            Assert.AreEqual("daily",    BoardProd.WirePeriod(LeaderboardPeriod.Daily));
            Assert.AreEqual("weekly",   BoardProd.WirePeriod(LeaderboardPeriod.Weekly));
            Assert.AreEqual("monthly",  BoardProd.WirePeriod(LeaderboardPeriod.Monthly));
            Assert.AreEqual("historic", BoardProd.WirePeriod(LeaderboardPeriod.Historic));
        }

        private static int[] Ranks(IReadOnlyList<LeaderboardEntry> e)
        {
            var r = new int[e.Count];
            for (int i = 0; i < e.Count; i++) r[i] = e[i].Rank;
            return r;
        }

        private static bool[] Ties(IReadOnlyList<LeaderboardEntry> e)
        {
            var r = new bool[e.Count];
            for (int i = 0; i < e.Count; i++) r[i] = e[i].IsTie;
            return r;
        }

        private static string[] Names(IReadOnlyList<LeaderboardEntry> e)
        {
            var r = new string[e.Count];
            for (int i = 0; i < e.Count; i++) r[i] = e[i].DisplayName;
            return r;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3  Countdown math under device clock skew
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LeaderboardCountdownTests
    {
        private const string FetchedAt    = "2026-08-18T05:30:00+00:00";
        private const string PeriodEndUtc = "2026-08-19T00:00:00+00:00";

        /// <summary>The server's truth: 18h30m left when the board was computed.</summary>
        private static readonly TimeSpan ServerRemaining = TimeSpan.FromHours(18) + TimeSpan.FromMinutes(30);

        [TestCase(0,   TestName = "Countdown_matches_the_server_when_the_device_clock_is_correct")]
        [TestCase(10,  TestName = "Countdown_matches_the_server_when_the_device_runs_10min_FAST")]
        [TestCase(-10, TestName = "Countdown_matches_the_server_when_the_device_runs_10min_SLOW")]
        public void Countdown_ignores_device_clock_skew(int skewMinutes)
        {
            // The device believes "now" is the fetch instant ± the skew.
            DateTime serverNow = DateTime.Parse(FetchedAt).ToUniversalTime();
            DateTime deviceNow = serverNow.AddMinutes(skewMinutes);

            DateTime adjustedEnd = BoardProd.AdjustedPeriodEnd(PeriodEndUtc, FetchedAt, deviceNow);

            // UpdateCountdownLabel does exactly this subtraction, against the same device clock.
            TimeSpan shown = adjustedEnd - deviceNow;

            Assert.AreEqual(ServerRemaining, shown,
                $"A device {skewMinutes} minutes off must still show the server's remaining time — the " +
                "shift applied at fetch cancels the error the subtraction reintroduces.");
        }

        [Test]
        public void A_missing_fetched_at_falls_back_to_the_raw_period_end()
        {
            DateTime end = BoardProd.AdjustedPeriodEnd(PeriodEndUtc, null, DateTime.UtcNow);
            Assert.AreEqual(DateTime.Parse(PeriodEndUtc).ToUniversalTime(), end,
                "With no server reference the timestamp is trusted as-is rather than skewed by garbage.");
        }

        [Test]
        public void An_unparseable_period_end_blanks_the_countdown_rather_than_throwing()
        {
            Assert.AreEqual(DateTime.MaxValue, BoardProd.AdjustedPeriodEnd("not a date", FetchedAt, DateTime.UtcNow));
        }

        [Test]
        public void Offsets_and_Z_forms_normalise_to_the_same_instant()
        {
            DateTime? withOffset = BoardProd.ParseUtc("2026-08-19T00:00:00+00:00");
            DateTime? withZ      = BoardProd.ParseUtc("2026-08-19T00:00:00Z");
            DateTime? bare       = BoardProd.ParseUtc("2026-08-19T00:00:00");

            Assert.IsTrue(withOffset.HasValue && withZ.HasValue && bare.HasValue);
            Assert.AreEqual(withOffset!.Value, withZ!.Value);
            Assert.AreEqual(withOffset.Value, bare!.Value,
                "AssumeUniversal covers a server that drops the offset — never the machine's local zone.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4  Disk cache round-trip
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LeaderboardDiskCacheTests
    {
        /// <summary>Historic is the least-used board, so a dev's real cache is least likely to be
        /// disturbed — and it is restored in TearDown regardless.</summary>
        private const LeaderboardPeriod Period = LeaderboardPeriod.Historic;

        private string? _savedOriginal;

        [SetUp]
        public void SaveTheDevelopersRealCache()
        {
            _savedOriginal = BoardProd.ReadCache(Period);
            BoardProd.ClearCache(Period);
        }

        [TearDown]
        public void RestoreIt()
        {
            BoardProd.ClearCache(Period);
            if (_savedOriginal != null) BoardProd.WriteCache(Period, _savedOriginal);
        }

        [Test]
        public void Cache_file_is_named_per_period()
        {
            Assert.AreEqual("leaderboard_daily.json",    BoardProd.CacheFileName(LeaderboardPeriod.Daily));
            Assert.AreEqual("leaderboard_historic.json", BoardProd.CacheFileName(LeaderboardPeriod.Historic));
            Assert.AreNotEqual(BoardProd.CachePath(LeaderboardPeriod.Daily),
                               BoardProd.CachePath(LeaderboardPeriod.Weekly),
                               "One file per period — a weekly fetch must never overwrite the daily board.");
        }

        [Test]
        public void Round_trips_the_raw_body_byte_for_byte()
        {
            BoardProd.WriteCache(Period, Payloads.NullsEverywhere);

            Assert.AreEqual(Payloads.NullsEverywhere, BoardProd.ReadCache(Period),
                "The RAW body is cached, not a mapped view, so a later build that reads more fields " +
                "can still use a cache this one wrote.");
        }

        [Test]
        public void Writing_leaves_no_tmp_file_behind()
        {
            BoardProd.WriteCache(Period, Payloads.NullsEverywhere);
            Assert.IsFalse(File.Exists(BoardProd.CachePath(Period) + ".tmp"),
                "The .tmp is replaced into place; a leftover means the atomic write did not complete.");
        }

        [Test]
        public void Overwriting_an_existing_cache_replaces_it_atomically()
        {
            BoardProd.WriteCache(Period, Payloads.SpecExample);
            BoardProd.WriteCache(Period, Payloads.NullsEverywhere);

            Assert.AreEqual(Payloads.NullsEverywhere, BoardProd.ReadCache(Period),
                "File.Replace path — the second write must land, not be rejected because the file existed.");
        }

        [Test]
        public void Missing_cache_reads_as_null_not_an_exception()
        {
            BoardProd.ClearCache(Period);
            Assert.IsNull(BoardProd.ReadCache(Period));
        }

        [Test]
        public void A_cached_board_is_on_screen_before_any_fetch()
        {
            BoardProd.WriteCache(Period, Payloads.NullsEverywhere);

            // Construction alone loads the cache — this is the airplane-mode open (SPEC §7 manual).
            ILeaderboardProvider provider = BoardProd.NewProvider(
                new FixedTime(new DateTime(2026, 8, 18, 5, 30, 0, DateTimeKind.Utc)));

            IReadOnlyList<LeaderboardEntry> board = provider.GetRanking(Period);
            Assert.AreEqual(1, board.Count, "The disk-cached board must render with no network at all.");
            Assert.AreEqual("NEVERSYNCED", board[0].DisplayName);

            LeaderboardEntry player = provider.GetPlayerEntry(Period);
            Assert.AreEqual(4181, player.Rank, "The cached player row pins at its cached rank.");
            Assert.IsTrue(player.IsPlayer);
        }

        [Test]
        public void A_corrupt_cache_file_yields_an_empty_board_not_a_broken_screen()
        {
            BoardProd.WriteCache(Period, "{ truncated mid-writ");

            ILeaderboardProvider provider = BoardProd.NewProvider(
                new FixedTime(new DateTime(2026, 8, 18, 5, 30, 0, DateTimeKind.Utc)));

            Assert.AreEqual(0, provider.GetRanking(Period).Count,
                "Unparseable cache → no snapshot → empty board, which the screen's refresh then fills.");
            Assert.AreEqual(DateTime.MaxValue, provider.GetPeriodEndUtc(Period),
                "No snapshot means no countdown target — the label blanks rather than showing garbage.");

            // And the pinned row still renders rather than throwing.
            Assert.IsTrue(provider.GetPlayerEntry(Period).IsPlayer);
        }

        [Test]
        public void Historic_has_no_countdown_even_from_a_good_cache()
        {
            BoardProd.WriteCache(Period, Payloads.NullsEverywhere);

            ILeaderboardProvider provider = BoardProd.NewProvider(
                new FixedTime(new DateTime(2026, 8, 18, 5, 30, 0, DateTimeKind.Utc)));

            Assert.AreEqual(DateTime.MaxValue, provider.GetPeriodEndUtc(Period),
                "period_end_utc is null for historic — SPEC §1.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §5  Provider selection (SPEC §4)
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LeaderboardProviderSelectionTests
    {
        [Test]
        public void Signed_in_players_read_the_backend()
        {
            Assert.AreEqual("Backend", BoardProd.Choose(botOverride: false, signedIn: true));
        }

        [Test]
        public void Signed_out_players_stay_on_the_local_fakes()
        {
            Assert.AreEqual("LocalFake", BoardProd.Choose(botOverride: false, signedIn: false),
                "Every leaderboard endpoint requires a bearer token — the backend has nothing to say.");
        }

        [Test]
        public void A_bot_run_NEVER_reaches_production_even_though_it_looks_signed_in()
        {
            // BotSessionOverride installs a fake session, so signedIn is TRUE during a bot run. An auth
            // check alone would aim requests at prod with BOT_SESSION_OVERRIDE_NOT_A_REAL_TOKEN.
            Assert.AreEqual("LocalFake", BoardProd.Choose(botOverride: true, signedIn: true),
                "The override must be checked FIRST — bots are offline by design (SPEC §4).");

            Assert.AreEqual("LocalFake", BoardProd.Choose(botOverride: true, signedIn: false));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §6  Character sync payload + throttle (SPEC §5)
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class GolfinCharacterSyncPolicyTests
    {
        [Test]
        public void Payload_is_the_two_fields_the_endpoint_expects()
        {
            string payload = BoardProd.BuildPayload("char_james", 12)!;

            Assert.IsTrue(payload.Contains("\"character_id\":\"char_james\""), payload);
            Assert.IsTrue(payload.Contains("\"level\":12"), payload);
        }

        [Test]
        public void No_character_selected_means_nothing_to_send()
        {
            Assert.IsNull(BoardProd.BuildPayload(null, 12));
            Assert.IsNull(BoardProd.BuildPayload("", 12));
            Assert.IsNull(BoardProd.BuildPayload("   ", 12),
                "An empty character_id is the pre-roster state and a guaranteed 400.");
        }

        [Test]
        public void An_unloaded_level_never_reaches_the_wire_as_zero()
        {
            Assert.IsTrue(BoardProd.BuildPayload("char_james", 0)!.Contains("\"level\":1"),
                "Server clamps to 1-999; clamping the floor here keeps an unloaded 0 off the wire.");
        }

        [Test]
        public void An_identical_payload_is_not_re_sent()
        {
            string payload = BoardProd.BuildPayload("char_james", 12)!;

            Assert.IsTrue(BoardProd.ShouldSend(payload, null), "First push of the session always sends.");
            Assert.IsFalse(BoardProd.ShouldSend(payload, payload),
                "OnCharacterSelected fires on every carousel commit — resending the same state is noise.");
        }

        [Test]
        public void A_level_up_changes_the_payload_and_therefore_sends()
        {
            string before = BoardProd.BuildPayload("char_james", 12)!;
            string after  = BoardProd.BuildPayload("char_james", 13)!;

            Assert.AreNotEqual(before, after);
            Assert.IsTrue(BoardProd.ShouldSend(after, before));
        }

        [Test]
        public void A_character_switch_changes_the_payload_and_therefore_sends()
        {
            string before = BoardProd.BuildPayload("char_james", 12)!;
            string after  = BoardProd.BuildPayload("char_olivia", 12)!;

            Assert.AreNotEqual(before, after);
            Assert.IsTrue(BoardProd.ShouldSend(after, before));
        }

        [Test]
        public void A_null_payload_never_sends()
        {
            Assert.IsFalse(BoardProd.ShouldSend(null, null));
            Assert.IsFalse(BoardProd.ShouldSend("", "anything"));
        }
    }
}
