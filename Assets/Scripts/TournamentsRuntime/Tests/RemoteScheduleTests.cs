// ─────────────────────────────────────────────────────────────────────────────
// RemoteScheduleTests — the server-schedule wiring (tournaments_unity_wiring §6.10)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// Same access pattern as TournamentServiceWireupTests, and for the same reason:
// the production types under test (TournamentScheduleMapper, TournamentArtPolicy,
// TournamentDisplayName) live in Assembly-CSharp, which an asmdef cannot reference.
// They are reached by REFLECTION; every value asserted is either a primitive or a
// Golfin.Tournaments type (TournamentDefinition, PrizeTable, BotFieldConfig), so
// the assertions themselves need no casting games.
//
// COVERAGE
//   §1  Mapper           — hole_set expansion, absolute-UTC parsing, title/banner,
//                          synthesized prizeTableId, wholesale-or-nothing
//   §2  Bad server data  — dangling bot_field_id / empty ladder / bad window are
//                          DROPPED individually, siblings survive
//   §3  Host allowlist   — accept/reject table
//   §4  Cache key        — derivation is stable, URL-sensitive, extension-preserving
//   §5  Name ladder      — localize(NameKey) → TitleJa (JP only) → Title → Id
//   §6  Integrity        — CheckReferentialIntegrity over mapped server data
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
    /// <summary>Reflection handles onto the Assembly-CSharp production types.</summary>
    internal static class Prod
    {
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

        internal static readonly Type Mapper      = Find("Golfin.Tournaments.TournamentScheduleMapper");
        internal static readonly Type ArtPolicy   = Find("Golfin.Tournaments.TournamentArtPolicy");
        internal static readonly Type DisplayName = Find("Golfin.Tournaments.TournamentDisplayName");

        internal static object? Invoke(Type t, string method, params object?[] args)
        {
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"{t.Name}.{method} not found.");
            return m.Invoke(null, args);
        }

        internal static string AllowedPrefix =>
            (string)ArtPolicy.GetField("AllowedArtPrefix", BindingFlags.Public | BindingFlags.Static)!
                .GetRawConstantValue()!;

        // ── Typed wrappers ────────────────────────────────────────────────────

        internal static bool IsAllowed(string? url)
            => (bool)Invoke(ArtPolicy, "IsAllowed", url)!;

        internal static string CacheFileName(string url)
            => (string)Invoke(ArtPolicy, "CacheFileName", url)!;

        internal static string ResolveName(string? nameKey, string? title, string? id)
        {
            var m = DisplayName.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(string), typeof(string) }, null)!;
            return (string)m.Invoke(null, new object?[] { nameKey, title, id })!;
        }

        /// <summary>The four-part ladder: localize(nameKey) → titleJa (JP only) → title → id.</summary>
        internal static string ResolveName(string? nameKey, string? title, string? titleJa, string? id)
        {
            var m = DisplayName.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, null)!;
            return (string)m.Invoke(null, new object?[] { nameKey, title, titleJa, id })!;
        }

        /// <summary>The definition overload — proves the ladder is fed TitleJa off a real DTO.</summary>
        internal static string ResolveName(TournamentDefinition? def)
        {
            var m = DisplayName.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(TournamentDefinition) }, null)!;
            return (string)m.Invoke(null, new object?[] { def })!;
        }

        /// <summary>
        /// Reaches the private <c>Deserialize</c> and returns the RAW <c>start_at</c> string as it sits
        /// on the DTO — the only way to prove Newtonsoft never touched it. Reflection ignores
        /// accessibility, so this needs no production visibility change.
        /// </summary>
        internal static string? RawStartAt(string json)
        {
            var m = Mapper.GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("TournamentScheduleMapper.Deserialize not found.");
            object? dto = m.Invoke(null, new object?[] { json });
            if (dto == null) return null;

            var list = (System.Collections.IList?)dto.GetType().GetField("Tournaments")!.GetValue(dto);
            if (list == null || list.Count == 0) return null;

            object first = list[0]!;
            return (string?)first.GetType().GetField("StartAt")!.GetValue(first);
        }

        /// <summary>MergePreservingEntered, with the entry predicate supplied by the test.</summary>
        internal static (IReadOnlyList<TournamentDefinition> Defs,
                         IReadOnlyDictionary<string, PrizeTable> Prizes) Merge(
            object incomingSchedule,
            IReadOnlyList<TournamentDefinition> current,
            IReadOnlyDictionary<string, PrizeTable> currentPrizes,
            Func<string, bool> isEntered)
        {
            var m = Mapper.GetMethod("MergePreservingEntered", BindingFlags.Public | BindingFlags.Static)!;
            object merged = m.Invoke(null, new object?[] { incomingSchedule, current, currentPrizes, isEntered })!;

            var t = merged.GetType();
            return ((IReadOnlyList<TournamentDefinition>)t.GetProperty("Definitions")!.GetValue(merged)!,
                    (IReadOnlyDictionary<string, PrizeTable>)t.GetProperty("PrizeTables")!.GetValue(merged)!);
        }

        /// <summary>The raw TournamentSchedule object, for feeding straight back into Merge.</summary>
        internal static object? MapJsonRaw(string json, IReadOnlyDictionary<string, BotFieldConfig> botFields)
        {
            var m = Mapper.GetMethod("TryMapJson", BindingFlags.Public | BindingFlags.Static)!;
            return m.Invoke(null, new object?[] { json, botFields, "test" });
        }

        /// <summary>TryMapJson → (definitions, prizeTables), or null when the payload is unusable.</summary>
        internal static (IReadOnlyList<TournamentDefinition> Defs,
                         IReadOnlyDictionary<string, PrizeTable> Prizes)? MapJson(
            string json, IReadOnlyDictionary<string, BotFieldConfig> botFields)
        {
            var m = Mapper.GetMethod("TryMapJson", BindingFlags.Public | BindingFlags.Static)!;
            object? schedule = m.Invoke(null, new object?[] { json, botFields, "test" });
            if (schedule == null) return null;

            var t = schedule.GetType();
            var defs   = (IReadOnlyList<TournamentDefinition>)t.GetProperty("Definitions")!.GetValue(schedule)!;
            var prizes = (IReadOnlyDictionary<string, PrizeTable>)t.GetProperty("PrizeTables")!.GetValue(schedule)!;
            return (defs, prizes);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Shared fixtures
    // ═════════════════════════════════════════════════════════════════════════
    internal static class Fixtures
    {
        internal static IReadOnlyDictionary<string, BotFieldConfig> BotFields(params string[] ids)
        {
            var d = new Dictionary<string, BotFieldConfig>(StringComparer.Ordinal);
            foreach (var id in ids)
                d[id] = new BotFieldConfig(id, 10, new Dictionary<string, float> { { "1", 1f } }, 0f, 0f, 0f);
            return d;
        }

        /// <summary>One well-formed tournament, with every field overridable by the caller.</summary>
        internal static string Tournament(
            string slug        = "kasumigaseki_open",
            string? title      = "Kasumigaseki Open",
            string? titleJa    = null,
            string? nameKey    = "tourn.kasumigaseki",
            string courseId    = "kasumigaseki",
            string holeSet     = "1-18",
            string startAt     = "2026-08-09T00:00:00+00:00",
            string endAt       = "2026-08-25T00:00:00+00:00",
            string botFieldId  = "field_major",
            string? bannerUrl  = null,
            string bands       = "[{\"rank_from\":1,\"rank_to\":1,\"rp_reward\":2000,\"item_reward_id\":\"trophy_major\"}]")
        {
            string J(string? s) => s == null ? "null" : "\"" + s + "\"";
            return "{"
                 + $"\"slug\":{J(slug)},\"title\":{J(title)},\"title_ja\":{J(titleJa)},\"name_key\":{J(nameKey)},"
                 + $"\"course_id\":{J(courseId)},\"hole_set\":{J(holeSet)},"
                 + $"\"start_at\":{J(startAt)},\"end_at\":{J(endAt)},"
                 + "\"resolve_delay_minutes\":30,\"entry_fee_pts\":10,"
                 + $"\"bot_field_id\":{J(botFieldId)},\"sponsor_name\":\"PUMA\",\"league_key\":\"DIAMOND\","
                 + $"\"banner_url\":{J(bannerUrl)},\"bot_seed\":1001,"
                 + $"\"prize_bands\":{bands}"
                 + "}";
        }

        /// <summary>Wraps tournaments in the same {"data":{…}} envelope the server sends.</summary>
        internal static string Envelope(params string[] tournaments)
            => "{\"data\":{\"fetched_at\":\"2026-08-14T03:00:00Z\",\"tournaments\":["
             + string.Join(",", tournaments) + "]}}";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1 Mapper — the happy path
    // ═════════════════════════════════════════════════════════════════════════
    public class ScheduleMapperTests
    {
        private IReadOnlyDictionary<string, BotFieldConfig> _fields = null!;

        [SetUp]
        public void SetUp() => _fields = Fixtures.BotFields("field_major", "field_small");

        [Test]
        public void MapsEveryFieldOntoTheDefinition()
        {
            var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament()), _fields);

            Assert.IsNotNull(mapped, "A well-formed payload must map.");
            Assert.AreEqual(1, mapped!.Value.Defs.Count);

            var def = mapped.Value.Defs[0];
            Assert.AreEqual("kasumigaseki_open", def.Id,      "Id must be the slug, not the uuid.");
            Assert.AreEqual("tourn.kasumigaseki", def.NameKey);
            Assert.AreEqual("Kasumigaseki Open",  def.Title,  "title must reach the DTO for the name ladder.");
            Assert.AreEqual("kasumigaseki",       def.ClubId, "course_id → ClubId.");
            Assert.AreEqual(30,   def.ResolveDelayMinutes);
            Assert.AreEqual(10L,  def.EntryFeeRP);
            Assert.AreEqual("field_major", def.BotFieldId);
            Assert.AreEqual("PUMA",        def.SponsorKey);
            Assert.AreEqual("DIAMOND",     def.LeagueKey);
        }

        [Test]
        public void ExpandsHoleSetThroughTheCsvLoaderRule()
        {
            var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament(holeSet: "1-18")), _fields);
            Assert.AreEqual(18, mapped!.Value.Defs[0].HoleSet.Count, "\"1-18\" must expand to 18 hole ids.");
            Assert.AreEqual("1",  mapped.Value.Defs[0].HoleSet[0]);
            Assert.AreEqual("18", mapped.Value.Defs[0].HoleSet[17]);

            var commas = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament(holeSet: "1,4,7")), _fields);
            Assert.AreEqual(3, commas!.Value.Defs[0].HoleSet.Count, "Comma form must expand too.");
        }

        [Test]
        public void DtoTimestampsAreNeverTouchedByNewtonsoft()
        {
            // THIS is the guard on DateParseHandling.None, and it bites on ANY host.
            //
            // The instant-equality test below does NOT guard it: Newtonsoft's default
            // DateParseHandling.DateTime + RoundtripKind is instant-preserving, so on a UTC build
            // agent the round trip comes back character-identical and the assertion passes with the
            // fix removed — green in CI while the exact regression it exists to catch ships. It only
            // bit here because this machine is UTC+7.
            //
            // Byte-identity of the DTO string is machine-independent: with the fix removed the reader
            // rewrites "+00:00" to "Z" even on a UTC host, and to "08/25/2026 07:00:00" on UTC+7.
            foreach (string start in new[]
                     { "2026-08-09T00:00:00+00:00", "2026-08-09T00:00:00Z",
                       "2026-08-09T00:00:00",       "2026-08-09T09:00:00+09:00" })
            {
                string json = Fixtures.Envelope(Fixtures.Tournament(startAt: start));
                Assert.AreEqual(start, Prod.RawStartAt(json),
                    $"start_at must reach the DTO as the exact characters the server sent. " +
                    $"A rewritten '{start}' means Newtonsoft parsed the date at the reader level " +
                    "and DateParseHandling.None has been lost.");
            }
        }

        [Test]
        public void ParsesTimestampsAsAbsoluteUtc_NeverLocal()
        {
            // Same instant, three spellings: explicit +00:00, Z, and a naked timestamp.
            // A local-time conversion would shift the naked one by the machine's offset and give
            // players in different timezones different schedules.
            foreach (string start in new[]
                     { "2026-08-09T00:00:00+00:00", "2026-08-09T00:00:00Z", "2026-08-09T00:00:00" })
            {
                var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament(startAt: start)), _fields);
                var def = mapped!.Value.Defs[0];

                Assert.AreEqual(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), def.StartUtc,
                    $"'{start}' must parse to the same absolute UTC instant.");
                Assert.AreEqual(DateTimeKind.Utc, def.StartUtc.Kind, $"'{start}' must be Kind=Utc.");
            }

            // An offset that is NOT zero must be converted, not ignored.
            var jst = Prod.MapJson(
                Fixtures.Envelope(Fixtures.Tournament(startAt: "2026-08-09T09:00:00+09:00")), _fields);
            Assert.AreEqual(new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc), jst!.Value.Defs[0].StartUtc,
                "A +09:00 timestamp must be converted to UTC, not read as wall-clock.");
        }

        [Test]
        public void SynthesizesPrizeTableIdFromTheSlug()
        {
            var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament()), _fields);
            var def = mapped!.Value.Defs[0];

            Assert.AreEqual("kasumigaseki_open", def.PrizeTableId,
                "Server bands are per-tournament, so each gets a one-entry table keyed by its own slug.");
            Assert.IsTrue(mapped.Value.Prizes.ContainsKey("kasumigaseki_open"));

            var table = mapped.Value.Prizes["kasumigaseki_open"];
            Assert.AreEqual(1, table.Bands.Count);
            Assert.AreEqual(2000L, table.Bands[0].RpReward);
            Assert.AreEqual("trophy_major", table.Bands[0].ItemRewardId);
        }

        [Test]
        public void SortsPrizeBandsByRankFrom()
        {
            string bands = "[{\"rank_from\":11,\"rank_to\":50,\"rp_reward\":100},"
                         + "{\"rank_from\":1,\"rank_to\":1,\"rp_reward\":2000},"
                         + "{\"rank_from\":2,\"rank_to\":10,\"rp_reward\":500}]";

            var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament(bands: bands)), _fields);
            var ladder = mapped!.Value.Prizes["kasumigaseki_open"].Bands;

            Assert.AreEqual(3, ladder.Count);
            Assert.AreEqual(1,  ladder[0].RankFrom);
            Assert.AreEqual(2,  ladder[1].RankFrom);
            Assert.AreEqual(11, ladder[2].RankFrom);
        }

        [Test]
        public void KeepsAllowlistedBannerUrlAndNullsAnythingElse()
        {
            string good = Prod.AllowedPrefix + "puma_summer_slam-a1b2c3.jpg";
            var ok = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament(bannerUrl: good)), _fields);
            Assert.AreEqual(good, ok!.Value.Defs[0].BannerUrl);

            LogAssert.ignoreFailingMessages = true;  // the refusal warning is expected
            var bad = Prod.MapJson(
                Fixtures.Envelope(Fixtures.Tournament(bannerUrl: "https://evil.example.com/art.jpg")), _fields);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsNull(bad!.Value.Defs[0].BannerUrl,
                "A refused URL must be indistinguishable from no URL downstream.");
            Assert.AreEqual("kasumigaseki_open", bad.Value.Defs[0].Id,
                "Refusing the art must NOT drop the tournament — it falls back to bundled art.");
        }

        [Test]
        public void CsvRowsCarryNoTitleOrBanner()
        {
            // The positional ctor still works untouched, and the three appended fields default to null.
            var def = new TournamentDefinition(
                "id", "key", "club", new[] { "1" },
                DateTime.UtcNow, DateTime.UtcNow.AddDays(1),
                30, 0L, "table", "field_major", "GOLFIN", "GOLD");

            Assert.IsNull(def.Title,     "A CSV row has no server title.");
            Assert.IsNull(def.BannerUrl, "A CSV row never carries remote art.");
            Assert.IsNull(def.TitleJa,   "A CSV row has no server Japanese title.");
        }

        [Test]
        public void MapsTitleJaOntoTheDefinition()
        {
            var mapped = Prod.MapJson(
                Fixtures.Envelope(Fixtures.Tournament(titleJa: "霞ヶ関オープン")), _fields);

            Assert.AreEqual("霞ヶ関オープン", mapped!.Value.Defs[0].TitleJa,
                "title_ja must reach the DTO, or the JP rung of the name ladder can never fire.");
        }

        [Test]
        public void TitleJaIsTrimmedAndBlankBecomesNull()
        {
            // Same treatment as title: an operator who saved a field of spaces has not set a name,
            // and a null TitleJa is what makes a JP player fall through to Title.
            var padded = Prod.MapJson(
                Fixtures.Envelope(Fixtures.Tournament(titleJa: "  霞ヶ関オープン  ")), _fields);
            Assert.AreEqual("霞ヶ関オープン", padded!.Value.Defs[0].TitleJa);

            foreach (string? blank in new string?[] { null, "", "   " })
            {
                var mapped = Prod.MapJson(
                    Fixtures.Envelope(Fixtures.Tournament(titleJa: blank)), _fields);
                Assert.IsNull(mapped!.Value.Defs[0].TitleJa, $"'{blank ?? "null"}' must map to null.");
            }
        }

        [Test]
        public void TitleJaIsNeverInterpretedByTheJsonReader()
        {
            // title_ja is a plain string. A value that LOOKS like a date must survive verbatim —
            // the same DateParseHandling.None guard DtoTimestampsAreNeverTouchedByNewtonsoft pins
            // for the timestamps, asserted on the field this change adds.
            var mapped = Prod.MapJson(
                Fixtures.Envelope(Fixtures.Tournament(titleJa: "2026-08-25T00:00:00+00:00")), _fields);

            Assert.AreEqual("2026-08-25T00:00:00+00:00", mapped!.Value.Defs[0].TitleJa,
                "A date-shaped title_ja must arrive as the exact characters the server sent.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2 Bad server data is dropped individually, never silently, never partially
    // ═════════════════════════════════════════════════════════════════════════
    public class ScheduleMapperBadDataTests
    {
        private IReadOnlyDictionary<string, BotFieldConfig> _fields = null!;

        [SetUp]
        public void SetUp() => _fields = Fixtures.BotFields("field_major");

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        /// <summary>
        /// Every test in this class drives production code that logs an error ON PURPOSE — that
        /// loud, local failure IS the behaviour under test (SPEC §3 D5). Unity's test framework
        /// resets this flag AFTER user [SetUp] runs, so it has to be set inside the test body.
        /// </summary>
        private static void ExpectErrorLogs() => LogAssert.ignoreFailingMessages = true;

        [Test]
        public void DropsTournamentWithDanglingBotFieldId_AndKeepsSiblings()
        {
            ExpectErrorLogs();
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "good_one"),
                Fixtures.Tournament(slug: "broken_one", botFieldId: "field_nonexistent"));

            var mapped = Prod.MapJson(json, _fields);

            Assert.AreEqual(1, mapped!.Value.Defs.Count, "Only the broken tournament may be dropped.");
            Assert.AreEqual("good_one", mapped.Value.Defs[0].Id);
        }

        [Test]
        public void DropsTournamentWithEmptyPrizeLadder()
        {
            ExpectErrorLogs();
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "good_one"),
                Fixtures.Tournament(slug: "no_prizes", bands: "[]"));

            var mapped = Prod.MapJson(json, _fields);
            Assert.AreEqual(1, mapped!.Value.Defs.Count);
            Assert.AreEqual("good_one", mapped.Value.Defs[0].Id);
        }

        [Test]
        public void DropsTournamentWithUnparseableWindow()
        {
            ExpectErrorLogs();
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "good_one"),
                Fixtures.Tournament(slug: "bad_dates", startAt: "not-a-date"));

            var mapped = Prod.MapJson(json, _fields);
            Assert.AreEqual(1, mapped!.Value.Defs.Count);
            Assert.AreEqual("good_one", mapped.Value.Defs[0].Id);
        }

        [Test]
        public void ReturnsNullWhenNothingUsableSurvives()
        {
            ExpectErrorLogs();
            // Wholesale or not at all: an all-bad payload must leave the caller's existing
            // schedule alone rather than replacing it with an empty screen.
            string json = Fixtures.Envelope(Fixtures.Tournament(botFieldId: "field_nonexistent"));
            Assert.IsNull(Prod.MapJson(json, _fields));
        }

        [Test]
        public void ReturnsNullOnMalformedOrEmptyJson()
        {
            ExpectErrorLogs();
            Assert.IsNull(Prod.MapJson("{ this is not json", _fields));
            Assert.IsNull(Prod.MapJson("{\"data\":{}}", _fields), "Missing 'tournaments' array.");
            Assert.IsNull(Prod.MapJson("", _fields));
        }

        [Test]
        public void ReturnsNullOnAnEmptyTournamentsArray()
        {
            ExpectErrorLogs();
            // A 200 with an empty schedule must NOT blank the screen — the caller keeps whatever
            // source it already had. Correct today, but nothing pinned it.
            Assert.IsNull(
                Prod.MapJson("{\"data\":{\"fetched_at\":\"2026-08-14T03:00:00Z\",\"tournaments\":[]}}", _fields));
        }

        [Test]
        public void DropsTournamentWithAnEmptyHoleSet()
        {
            ExpectErrorLogs();
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "good_one"),
                Fixtures.Tournament(slug: "no_holes", holeSet: ""));

            var mapped = Prod.MapJson(json, _fields);
            Assert.AreEqual(1, mapped!.Value.Defs.Count,
                "ExpandHoleSet returns empty for an empty value; a zero-hole tournament would " +
                "otherwise render '· 0 Holes' and still be enterable.");
            Assert.AreEqual("good_one", mapped.Value.Defs[0].Id);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2b Mid-entry preservation (SPEC §4.2 + D3 amended) — acceptance 9 / B1
    // ═════════════════════════════════════════════════════════════════════════
    public class MidEntryPreservationTests
    {
        private IReadOnlyDictionary<string, BotFieldConfig> _fields = null!;

        [SetUp]
        public void SetUp() => _fields = Fixtures.BotFields("field_major");

        /// <summary>The schedule the player is currently playing against.</summary>
        private static (IReadOnlyList<TournamentDefinition>, IReadOnlyDictionary<string, PrizeTable>)
            InPlay(params (string Id, long Fee)[] rows)
        {
            var defs   = new List<TournamentDefinition>();
            var prizes = new Dictionary<string, PrizeTable>(StringComparer.Ordinal);
            foreach (var (id, fee) in rows)
            {
                defs.Add(new TournamentDefinition(
                    id, "tourn." + id, "lomond", new[] { "1", "2" },
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                    30, fee, id, "field_major", "GOLFIN", "GOLD"));
                prizes[id] = new PrizeTable(id, new[] { new PrizeBand(1, 1, 999L) });
            }
            return (defs, prizes);
        }

        [Test]
        public void EnteredTournamentStillInThePayloadKeepsItsInPlayDefinitionAndLadder()
        {
            var (current, currentPrizes) = InPlay(("kasumigaseki_open", 10L));

            // The server has since changed the fee and the prize.
            string json = Fixtures.Envelope(Fixtures.Tournament(slug: "kasumigaseki_open"));
            object incoming = Prod.MapJsonRaw(json, _fields)!;

            var (defs, prizes) = Prod.Merge(incoming, current, currentPrizes, id => true);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual(10L, defs[0].EntryFeeRP,
                "The player registered against the in-play fee; the server's must be deferred.");
            Assert.AreEqual(999L, prizes["kasumigaseki_open"].Bands[0].RpReward,
                "The ladder must travel WITH the definition — swapping prizes while keeping the " +
                "definition is exactly the mid-session mutation this guard exists to prevent.");
        }

        [Test]
        public void EnteredTournamentDroppedByTheServerIsCarriedForward()
        {
            // B1. The server no longer sends 'gone_from_server' at all (deleted, or kind-flipped),
            // but the player holds a persisted entry in it. Losing it makes GetTournament(id) throw
            // KeyNotFoundException in the signup modal, the result modal, the round handler and
            // SubmitHoleResult — a mid-round hole submission takes the app down.
            var (current, currentPrizes) = InPlay(("gone_from_server", 10L), ("still_there", 0L));

            string json = Fixtures.Envelope(Fixtures.Tournament(slug: "still_there"));
            object incoming = Prod.MapJsonRaw(json, _fields)!;

            LogAssert.ignoreFailingMessages = true;   // the carry-forward warns on purpose
            var (defs, prizes) = Prod.Merge(
                incoming, current, currentPrizes, id => id == "gone_from_server");
            LogAssert.ignoreFailingMessages = false;

            var ids = new List<string>();
            foreach (var d in defs) ids.Add(d.Id);

            CollectionAssert.Contains(ids, "gone_from_server",
                "An ENTERED tournament the server dropped must survive the swap — vanishing is the " +
                "maximal change, and every GetTournament(id) after it throws.");
            CollectionAssert.Contains(ids, "still_there");
            Assert.IsTrue(prizes.ContainsKey("gone_from_server"),
                "Its prize table must be carried across too, or GetTopPrizeRP silently reads 0.");
        }

        [Test]
        public void NonEnteredTournamentDroppedByTheServerIsNotResurrected()
        {
            // The mirror of the case above: without an entry, the server dropping a tournament is
            // just a schedule change and must be honoured.
            var (current, currentPrizes) = InPlay(("dropped", 10L), ("still_there", 0L));

            string json = Fixtures.Envelope(Fixtures.Tournament(slug: "still_there"));
            object incoming = Prod.MapJsonRaw(json, _fields)!;

            var (defs, _) = Prod.Merge(incoming, current, currentPrizes, id => false);

            Assert.AreEqual(1, defs.Count);
            Assert.AreEqual("still_there", defs[0].Id);
        }

        [Test]
        public void NonEnteredTournamentTakesTheIncomingVersion()
        {
            var (current, currentPrizes) = InPlay(("kasumigaseki_open", 10L));
            string json = Fixtures.Envelope(Fixtures.Tournament(slug: "kasumigaseki_open"));
            object incoming = Prod.MapJsonRaw(json, _fields)!;

            var (defs, prizes) = Prod.Merge(incoming, current, currentPrizes, id => false);

            Assert.AreEqual(10L, defs[0].EntryFeeRP);  // fixture fee, from the server payload
            Assert.AreEqual(2000L, prizes["kasumigaseki_open"].Bands[0].RpReward,
                "With no entry, the server's ladder wins.");
            Assert.AreEqual("Kasumigaseki Open", defs[0].Title,
                "The server definition carries Title; the in-play CSV-shaped one does not.");
        }

        [Test]
        public void NoEntriesMeansAStraightPassthrough()
        {
            var (current, currentPrizes) = InPlay(("a", 0L));
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "x"), Fixtures.Tournament(slug: "y"));
            object incoming = Prod.MapJsonRaw(json, _fields)!;

            var (defs, _) = Prod.Merge(incoming, current, currentPrizes, id => false);
            Assert.AreEqual(2, defs.Count, "Nothing entered → the server schedule replaces wholesale.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3 Host allowlist — the security control (SPEC §5.2)
    // ═════════════════════════════════════════════════════════════════════════
    public class ArtHostAllowlistTests
    {
        private static string P => Prod.AllowedPrefix;

        [Test]
        public void AcceptsOnlyTheProjectStorageBucketPath()
        {
            Assert.IsTrue(Prod.IsAllowed(P + "puma-a1b2.jpg"));
            Assert.IsTrue(Prod.IsAllowed(P + "nested/path/art.png"));
        }

        [Test]
        public void RejectsEverythingElse()
        {
            var rejected = new (string Url, string Why)[]
            {
                (null!,                                                        "null"),
                ("",                                                           "empty"),
                ("   ",                                                        "whitespace"),
                (P,                                                            "prefix with no object name"),
                ("http" + P.Substring(5) + "art.jpg",                          "http, not https"),
                ("https://evil.example.com/art.jpg",                           "foreign host"),
                ("https://wmszyghwwkaptgqdunel.supabase.co.evil.com/storage/v1/object/public/tournament-art/a.jpg",
                                                                               "host suffix attack"),
                ("https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/other-bucket/a.jpg",
                                                                               "right host, wrong bucket"),
                ("https://wmszyghwwkaptgqdunel.supabase.co/a.jpg",             "right host, no bucket path"),
                ("//wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/a.jpg",
                                                                               "scheme-relative"),
                ("https://wmszyghwwkaptgqdunel.supabase.co@evil.com/storage/v1/object/public/tournament-art/a.jpg",
                                                                               "userinfo — real host is evil.com"),
                ("https://wmszyghwwkaptgqdunel.supabase.co:8443/storage/v1/object/public/tournament-art/a.jpg",
                                                                               "non-default port"),
            };

            foreach (var (url, why) in rejected)
                Assert.IsFalse(Prod.IsAllowed(url), $"Must reject ({why}): '{url}'");
        }

        [Test]
        public void RejectsDotSegmentsThatWalkOutOfTheBucket()
        {
            // The bypass a raw StartsWith cannot see. Every one of these has the allowlisted prefix
            // as a literal string prefix, but System.Uri — which is what UnityWebRequest builds
            // before the socket opens — normalizes them to a path OUTSIDE the bucket. The check has
            // to run on the same normalized form the HTTP stack will actually request.
            var traversals = new[]
            {
                P + "../../../../../rest/v1/rpc/anything",
                P + "../../../../../../auth/v1/admin/users",
                P + "..%2F..%2F..%2F..%2F..%2Frest/v1/rpc/x",
                P + "%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/rest/v1/rpc/x",
                P + "sub/../../../../../../rest/v1/rpc/x",
            };

            foreach (string url in traversals)
            {
                Assert.IsFalse(Prod.IsAllowed(url), $"Must reject traversal: '{url}'");

                // And prove the claim: the normalized path really does leave the bucket.
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    Assert.IsFalse(
                        uri.AbsolutePath.StartsWith("/storage/v1/object/public/tournament-art/", StringComparison.Ordinal)
                        && uri.AbsolutePath.IndexOf("..", StringComparison.Ordinal) < 0
                        && uri.AbsolutePath.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) < 0,
                        $"'{url}' normalizes to '{uri.AbsolutePath}' — if that were still inside the " +
                        "bucket with no dot segments left, this test would be asserting nothing.");
            }
        }

        [Test]
        public void AllowsANestedObjectNameThatIsNotATraversal()
        {
            // Guard against over-tightening: a legitimate nested key must still pass.
            Assert.IsTrue(Prod.IsAllowed(P + "2026/08/puma_summer_slam-a1b2c3.jpg"));
        }

        [Test]
        public void PrefixPinsSchemeHostAndBucketPath()
        {
            // A regression guard on the constant itself — relaxing any of the three parts is the
            // failure mode that turns banner_url into an arbitrary content channel.
            Assert.IsTrue(P.StartsWith("https://", StringComparison.Ordinal), "Scheme must be pinned to https.");
            Assert.IsTrue(P.Contains("wmszyghwwkaptgqdunel.supabase.co"),     "Host must be pinned.");
            Assert.IsTrue(P.EndsWith("/tournament-art/", StringComparison.Ordinal), "Bucket path must be pinned.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4 Cache key derivation (SPEC §5.5)
    // ═════════════════════════════════════════════════════════════════════════
    public class ArtCacheKeyTests
    {
        private static string Url(string leaf) => Prod.AllowedPrefix + leaf;

        [Test]
        public void IsDeterministicAndSixteenHexPlusExtension()
        {
            string a = Prod.CacheFileName(Url("puma-a1b2c3.jpg"));
            string b = Prod.CacheFileName(Url("puma-a1b2c3.jpg"));

            Assert.AreEqual(a, b, "Same URL must always produce the same cache file.");
            Assert.AreEqual(".jpg", System.IO.Path.GetExtension(a));

            string stem = System.IO.Path.GetFileNameWithoutExtension(a);
            Assert.AreEqual(16, stem.Length, "16 hex chars = first 8 bytes of sha256.");
            foreach (char c in stem)
                Assert.IsTrue("0123456789abcdef".IndexOf(c) >= 0, $"Non-hex char '{c}' in '{stem}'.");
        }

        [Test]
        public void DifferentUrlsProduceDifferentKeys()
        {
            // The dashboard writes content-hashed immutable names, so new art = new URL = new key.
            // This is what makes "nothing to invalidate" true.
            Assert.AreNotEqual(
                Prod.CacheFileName(Url("puma-aaaa.jpg")),
                Prod.CacheFileName(Url("puma-bbbb.jpg")));
        }

        [Test]
        public void PreservesKnownExtensionsAndFallsBackOtherwise()
        {
            Assert.AreEqual(".png", System.IO.Path.GetExtension(Prod.CacheFileName(Url("a.png"))));
            Assert.AreEqual(".jpg", System.IO.Path.GetExtension(Prod.CacheFileName(Url("a.JPG"))),
                "Extension is lower-cased.");
            Assert.AreEqual(".jpg", System.IO.Path.GetExtension(Prod.CacheFileName(Url("a.jpg?token=x"))),
                "Query string must not leak into the file name.");
            Assert.AreEqual(".img", System.IO.Path.GetExtension(Prod.CacheFileName(Url("no-extension"))));
        }

        [Test]
        public void KeyContainsNoPathSeparators()
        {
            string key = Prod.CacheFileName(Url("nested/deep/art.jpg"));
            Assert.IsFalse(key.Contains("/"), "A key with a separator would escape the cache directory.");
            Assert.IsFalse(key.Contains("\\"));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4b Schedule disk cache — write / read / re-map round trip (SPEC §4.2)
    // ═════════════════════════════════════════════════════════════════════════
    public class ScheduleCacheRoundTripTests
    {
        private static Type Source => Prod.Find("Golfin.Tournaments.RemoteTournamentSource");

        private static string CachePath =>
            (string)Source.GetProperty("CachePath", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        private static void Write(string json) => Prod.Invoke(Source, "WriteCache", json);
        private static string? Read()          => (string?)Prod.Invoke(Source, "ReadCache");
        private static void Clear()            => Prod.Invoke(Source, "ClearCache");

        private string? _saved;

        [SetUp]
        public void SetUp() => _saved = Read();     // never clobber a real cached schedule

        [TearDown]
        public void TearDown()
        {
            Clear();
            if (_saved != null) Write(_saved);
        }

        [Test]
        public void RawBodySurvivesWriteReadAndRemapsIdentically()
        {
            var fields = Fixtures.BotFields("field_major");
            string body = Fixtures.Envelope(
                Fixtures.Tournament(slug: "a"), Fixtures.Tournament(slug: "b"));

            Write(body);

            Assert.AreEqual(body, Read(), "The RAW body must round trip byte-for-byte.");

            // And it must still be mappable off disk — this is the boot path when the network is down.
            var mapped = Prod.MapJson(Read()!, fields);
            Assert.IsNotNull(mapped);
            Assert.AreEqual(2, mapped!.Value.Defs.Count);
        }

        [Test]
        public void WriteIsAtomicAndLeavesNoStagingFileBehind()
        {
            // The .tmp → File.Replace pattern (from PendingOpsStore) exists so a kill mid-write
            // leaves the PREVIOUS good cache intact rather than a truncated file that fails to parse
            // on next boot. After a completed write there must be no staging file left over.
            Write(Fixtures.Envelope(Fixtures.Tournament(slug: "first")));
            Write(Fixtures.Envelope(Fixtures.Tournament(slug: "second")));

            Assert.IsFalse(System.IO.File.Exists(CachePath + ".tmp"),
                "A completed write must not leave a .tmp behind.");
            StringAssert.Contains("second", Read(), "The second write must have replaced the first.");
        }

        [Test]
        public void ReadReturnsNullWhenThereIsNoCache()
        {
            Clear();
            Assert.IsNull(Read(), "A first-ever launch must report no cache, not an empty string.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §5 Display-name ladder (SPEC §4.3; JP rung from tournament_title_ja §3.3)
    // ═════════════════════════════════════════════════════════════════════════
    public class DisplayNameLadderTests
    {
        private static FieldInfo TextMapField =>
            typeof(LocalizationManager).GetField("_textMap", BindingFlags.NonPublic | BindingFlags.Static)!;

        private object?  _savedTextMap;
        private Language _savedLanguage;

        [SetUp]
        public void SetUp()
        {
            _savedTextMap  = TextMapField.GetValue(null);
            _savedLanguage = LocalizationManager.CurrentLanguage;
        }

        [TearDown]
        public void TearDown()
        {
            // Language is global static state; leaving a test's JP switch behind would silently
            // re-language every later test in the run. Restored through Initialize rather than
            // SetLanguage so it does NOT fire OnLanguageChanged at whatever UI is alive in the
            // editor — TournamentSelectionScreenController now subscribes to that event.
            LocalizationManager.Initialize(
                ScriptableObject.CreateInstance<LocalizationTextTable>(), _savedLanguage);
            TextMapField.SetValue(null, _savedTextMap);
        }

        private static void InstallLocalization(params (string Key, string English)[] rows)
            => InstallLocalization(Language.English, rows);

        /// <summary>
        /// Installs a table and pins the language. <c>Initialize</c> assigns
        /// <c>CurrentLanguage</c> directly, so this sets the language without firing
        /// <c>OnLanguageChanged</c> at whatever UI happens to be alive in the editor.
        /// </summary>
        private static void InstallLocalization(
            Language language, params (string Key, string English)[] rows)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            foreach (var (key, english) in rows)
                table.rows.Add(new LocalizedTextRow { key = key, english = english, japanese = "" });
            LocalizationManager.Initialize(table, language);
        }

        /// <summary>A table whose rows carry BOTH languages — a real translation pair.</summary>
        private static void InstallBilingual(
            Language language, params (string Key, string English, string Japanese)[] rows)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            foreach (var (key, english, japanese) in rows)
                table.rows.Add(new LocalizedTextRow { key = key, english = english, japanese = japanese });
            LocalizationManager.Initialize(table, language);
        }

        [Test]
        public void Rung1_LocalizedNameWinsWhenTheKeyResolves()
        {
            InstallLocalization(("tourn.kasumigaseki", "Kasumigaseki Open"));

            Assert.AreEqual("Kasumigaseki Open",
                Prod.ResolveName("tourn.kasumigaseki", "SERVER TITLE", "kasumigaseki_open"),
                "A shipped localization entry outranks the server title.");
        }

        [Test]
        public void Rung3_FallsBackToServerTitleWhenTheKeyEchoesBack()
        {
            // THE HEADLINE CASE: a dashboard-created brand tournament has no localization key in
            // this build, so LocalizationManager.Get returns the key verbatim. Without the ladder
            // the card would read "tourn.puma_summer_slam".
            InstallLocalization(("tourn.kasumigaseki", "Kasumigaseki Open"));

            Assert.AreEqual("PUMA Summer Slam",
                Prod.ResolveName("tourn.puma_summer_slam", "PUMA Summer Slam", "puma_summer_slam"));
        }

        [Test]
        public void Rung3_FallsBackToServerTitleWhenThereIsNoKeyAtAll()
        {
            InstallLocalization();
            Assert.AreEqual("PUMA Summer Slam", Prod.ResolveName(null, "PUMA Summer Slam", "puma_summer_slam"));
            Assert.AreEqual("PUMA Summer Slam", Prod.ResolveName("",   "PUMA Summer Slam", "puma_summer_slam"));
        }

        [Test]
        public void Rung4_FallsBackToTheSlugWhenNothingElseExists()
        {
            InstallLocalization();
            Assert.AreEqual("puma_summer_slam", Prod.ResolveName(null, null, "puma_summer_slam"));
            Assert.AreEqual("puma_summer_slam", Prod.ResolveName("tourn.missing", "   ", "puma_summer_slam"));
        }

        [Test]
        public void NeverRendersARawLocalizationKey()
        {
            InstallLocalization();
            string rendered = Prod.ResolveName("tourn.puma_summer_slam", "PUMA Summer Slam", "puma_summer_slam");
            Assert.AreNotEqual("tourn.puma_summer_slam", rendered, "The raw key must never reach a player.");
        }

        [Test]
        public void NullKeyDoesNotThrow()
        {
            // LocalizationManager.Get(null) would hit Dictionary.TryGetValue(null) → ArgumentNullException.
            InstallLocalization(("tourn.x", "X"));
            Assert.DoesNotThrow(() => Prod.ResolveName(null, "Title", "id"));
        }

        // ── The JP rung (tournament_title_ja §5) ──────────────────────────────

        [Test]
        public void Rung2_JapanesePlayerSeesTitleJa_EnglishPlayerSeesTitle()
        {
            // Acceptance 1. A dashboard-created tournament with both titles and NO key.
            InstallLocalization(Language.Japanese);
            Assert.AreEqual("プーマ サマースラム",
                Prod.ResolveName(null, "PUMA Summer Slam", "プーマ サマースラム", "puma_summer_slam"),
                "A JP player must get the operator's Japanese title.");

            InstallLocalization(Language.English);
            Assert.AreEqual("PUMA Summer Slam",
                Prod.ResolveName(null, "PUMA Summer Slam", "プーマ サマースラム", "puma_summer_slam"),
                "An EN player must get the English title, never the Japanese one.");
        }

        [Test]
        public void Rung1_ResolvingKeyStillWinsInBothLanguages()
        {
            // Acceptance 2. A shipped key is a real translation PAIR — both languages get a proper
            // name from it — so it outranks an operator's single-language string in both.
            InstallBilingual(Language.English, ("tourn.kasumigaseki", "Kasumigaseki Open", "霞ヶ関オープン"));
            Assert.AreEqual("Kasumigaseki Open",
                Prod.ResolveName("tourn.kasumigaseki", "SERVER TITLE", "サーバータイトル", "kasumigaseki_open"));

            InstallBilingual(Language.Japanese, ("tourn.kasumigaseki", "Kasumigaseki Open", "霞ヶ関オープン"));
            Assert.AreEqual("霞ヶ関オープン",
                Prod.ResolveName("tourn.kasumigaseki", "SERVER TITLE", "サーバータイトル", "kasumigaseki_open"),
                "The key outranks title_ja: it is a translation pair, not one operator's string.");
        }

        [Test]
        public void SeededKeyOnlyRowsAreUnchangedInBothLanguages()
        {
            // Acceptance 3. The six shipped CSV rows carry a key and no server titles; adding the
            // JP rung must not move them.
            InstallBilingual(Language.English, ("tourn.lomond", "Lomond Championship", "ローモンド選手権"));
            Assert.AreEqual("Lomond Championship",
                Prod.ResolveName("tourn.lomond", null, null, "lomond_championship"));

            InstallBilingual(Language.Japanese, ("tourn.lomond", "Lomond Championship", "ローモンド選手権"));
            Assert.AreEqual("ローモンド選手権",
                Prod.ResolveName("tourn.lomond", null, null, "lomond_championship"));
        }

        [Test]
        public void EnglishPlayerFallsToTheSlugRatherThanEverShowingTitleJa()
        {
            // Acceptance 4 — the case the whole "JP only" rule exists for. With title empty, the
            // English ladder must SKIP title_ja entirely and land on the slug. A rung that merely
            // preferred title would pass a naive test here and still leak Japanese to EN players.
            InstallLocalization(Language.English);

            foreach (string? emptyTitle in new string?[] { null, "", "   " })
            {
                string rendered = Prod.ResolveName(null, emptyTitle, "プーマ サマースラム", "puma_summer_slam");
                Assert.AreEqual("puma_summer_slam", rendered,
                    $"With title='{emptyTitle ?? "null"}' an EN player must see the slug.");
                Assert.AreNotEqual("プーマ サマースラム", rendered,
                    "The Japanese string must NEVER reach an English player.");
            }

            // And with a non-resolving key in play, which is the shape a dashboard row actually has.
            Assert.AreEqual("puma_summer_slam",
                Prod.ResolveName("tourn.puma_summer_slam", null, "プーマ サマースラム", "puma_summer_slam"));
        }

        [Test]
        public void JapanesePlayerWithNoTitleJaFallsToTheEnglishTitle()
        {
            // Intended, not a gap: one name is better than a slug.
            InstallLocalization(Language.Japanese);

            foreach (string? blank in new string?[] { null, "", "   " })
                Assert.AreEqual("PUMA Summer Slam",
                    Prod.ResolveName(null, "PUMA Summer Slam", blank, "puma_summer_slam"),
                    $"titleJa='{blank ?? "null"}' must fall through to Title, not to the slug.");
        }

        [Test]
        public void TitleJaIsTrimmedWhenRendered()
        {
            InstallLocalization(Language.Japanese);
            Assert.AreEqual("プーマ サマースラム",
                Prod.ResolveName(null, "PUMA Summer Slam", "  プーマ サマースラム  ", "puma_summer_slam"));
        }

        [Test]
        public void TheDefinitionOverloadFeedsTitleJaIntoTheLadder()
        {
            // The raw-parts ladder can be perfect while Resolve(def) still drops TitleJa on the
            // floor — and Resolve(def) is what all three UI surfaces actually call.
            var def = new TournamentDefinition(
                "puma_summer_slam", "tourn.puma_summer_slam", "lomond", new[] { "1" },
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc),
                30, 0L, "table", "field_major", "PUMA", "GOLD",
                title: "PUMA Summer Slam", bannerUrl: null, titleJa: "プーマ サマースラム");

            InstallLocalization(Language.Japanese);
            Assert.AreEqual("プーマ サマースラム", Prod.ResolveName(def));

            InstallLocalization(Language.English);
            Assert.AreEqual("PUMA Summer Slam", Prod.ResolveName(def));
        }

        [Test]
        public void CsvExportedDashboardTournamentRendersItsNameNotItsSlug()
        {
            // Acceptance 5, the display half. The loader half — that LoadTournaments feeds the raw
            // nameKey column into Title — is pinned on the REAL loader in
            // TournamentCsvLoaderTests.LoadTournaments_RealLoader_MirrorsNameKeyIntoTitle. This is
            // the shape that produces: the dashboard's CSV export writes `nameKey ?? title` into
            // the one name column, so a panel-named tournament arrives with the display name
            // sitting in nameKey, resolving against nothing.
            InstallLocalization(("tourn.lomond", "Lomond Championship"));

            Assert.AreEqual("Cesar Championship",
                Prod.ResolveName("Cesar Championship", "Cesar Championship", null, "cesar_championship"),
                "Offline, a dashboard-named tournament must show its name, not 'cesar_championship'.");

            // And a row whose key DOES resolve is untouched — rung 1 wins before Title is read.
            Assert.AreEqual("Lomond Championship",
                Prod.ResolveName("tourn.lomond", "tourn.lomond", null, "lomond_championship"),
                "Mirroring nameKey into Title must not change a row whose key resolves.");
        }

        [Test]
        public void ThreePartOverloadStillWorksAndBehavesAsIfTitleJaWereNull()
        {
            // Kept for every existing caller; a JP player on it must simply fall to Title.
            InstallLocalization(Language.Japanese);
            Assert.AreEqual("PUMA Summer Slam", Prod.ResolveName(null, "PUMA Summer Slam", "puma_summer_slam"));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §6 Referential integrity over MAPPED SERVER DATA (not just CSV)
    // ═════════════════════════════════════════════════════════════════════════
    public class ServerDataIntegrityTests
    {
        [Test]
        public void MappedServerDataPassesCheckReferentialIntegrity()
        {
            var fields = Fixtures.BotFields("field_major", "field_small");
            string json = Fixtures.Envelope(
                Fixtures.Tournament(slug: "a", botFieldId: "field_major"),
                Fixtures.Tournament(slug: "b", botFieldId: "field_small"));

            var mapped = Prod.MapJson(json, fields);
            Assert.IsNotNull(mapped);

            Assert.IsTrue(
                TournamentCsvLoader.CheckReferentialIntegrity(mapped!.Value.Defs, mapped.Value.Prizes, fields),
                "Every mapped tournament must resolve its synthesized prize table and its bot field.");
        }

        [Test]
        public void EverySurvivingTournamentHasItsOwnPrizeTable()
        {
            var fields = Fixtures.BotFields("field_major");
            LogAssert.ignoreFailingMessages = true;
            var mapped = Prod.MapJson(
                Fixtures.Envelope(
                    Fixtures.Tournament(slug: "a"),
                    Fixtures.Tournament(slug: "dropped", botFieldId: "nope"),
                    Fixtures.Tournament(slug: "c")),
                fields);
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(2, mapped!.Value.Defs.Count);
            foreach (var def in mapped.Value.Defs)
            {
                Assert.IsTrue(mapped.Value.Prizes.ContainsKey(def.PrizeTableId),
                    $"'{def.Id}' points at prize table '{def.PrizeTableId}' which was not built.");
                Assert.AreEqual(def.Id, def.PrizeTableId, "The table is keyed by the tournament's own slug.");
            }
        }
    }
}
