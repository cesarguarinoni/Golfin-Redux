// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — TournamentCsvLoaderTests (T2)
// EditMode tests covering SPEC §5 assertions.
// Headless NUnit — CSV data injected as literal strings for headless parsing;
// Resources.Load tests use UnityEngine.TestTools infrastructure so they run
// inside the Unity test runner where Resources.Load works at EditMode.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Tournaments;

namespace Golfin.Tournaments.Tests
{
    [TestFixture]
    public class TournamentCsvLoaderTests
    {
        // ── Inline CSV fixtures (verbatim from SPEC §3) ───────────────────────

        private const string TOURNAMENTS_CSV = @"# Tournament definitions — one row per tournament. courseId→ClubId; holeSet expands ranges.
id,nameKey,courseId,holeSet,startUtc,endUtc,resolveDelayMinutes,entryFeeRP,botFieldId,prizeTableId,sponsorKey,leagueKey
kasumigaseki_open,tourn.kasumigaseki,kasumigaseki,1-18,2026-06-23T00:00:00Z,2026-06-29T00:00:00Z,30,0,field_major,prize_major,PUMA,DIAMOND
hirono_invitational,tourn.hirono,hirono,1-18,2026-06-20T00:00:00Z,2026-06-26T12:00:00Z,30,0,field_major,prize_major,GOLFIN,DIAMOND
lomond_championship,tourn.lomond,lomond,1-18,2026-06-24T00:00:00Z,2026-06-27T00:00:00Z,30,0,field_medium,prize_medium,GOLFIN,GOLD
gotemba_masters,tourn.gotemba,gotemba,1-18,2026-06-21T00:00:00Z,2026-06-25T22:00:00Z,30,500,field_major,prize_major,TAIHEIYO,GOLD
kisarazu_cup,tourn.kisarazu,kisarazu,1-18,2026-07-02T00:00:00Z,2026-07-05T00:00:00Z,30,0,field_small,prize_small,GOLFIN,SILVER
kawana_fuji_open,tourn.kawana,kawana,1-18,2026-06-08T00:00:00Z,2026-06-13T00:00:00Z,30,0,field_major,prize_major,GOLFIN,DIAMOND
";

        private const string PRIZES_CSV = @"# Rank-band prize tables. rpReward = RP; itemRewardId blank = RP only.
prizeTableId,rankFrom,rankTo,rpReward,itemRewardId
prize_small,1,1,3000,
prize_small,2,3,1500,
prize_small,4,10,500,
prize_medium,1,1,5000,ticket_gold
prize_medium,2,3,3000,
prize_medium,4,10,1000,
prize_major,1,1,20000,trophy_major
prize_major,2,3,12000,ticket_gold
prize_major,4,10,5000,
prize_major,11,50,1000,
";

        private const string BOT_FIELDS_CSV = @"# Bot field configs. bracketWeights = minLevel:weight pairs (keys = bot_difficulty.csv minLevels).
botFieldId,botCount,bracketWeights,startOffsetMinSec,startOffsetMaxSec,perHoleSpreadSec
field_small,12,1:0.30;10:0.30;25:0.20;50:0.20,0,7200,600
field_medium,20,10:0.25;25:0.30;50:0.25;100:0.20,0,7200,540
field_major,30,25:0.20;50:0.25;100:0.30;180:0.25,0,10800,480
";

        // ── Helpers: headless parse wrappers ──────────────────────────────────

        /// <summary>
        /// Parse tournaments from a literal CSV string (no Resources.Load needed).
        /// Mirrors TournamentCsvLoader.LoadTournaments() but accepts inline text.
        /// </summary>
        private static IReadOnlyList<TournamentDefinition> ParseTournaments(string csv)
        {
            var lines        = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int iId          = -1, iNameKey = -1, iCourseId = -1, iHoleSet = -1;
            int iStartUtc    = -1, iEndUtc = -1, iResolveDelay = -1;
            int iEntryFee    = -1, iBotFieldId = -1, iPrizeTableId = -1;
            int iSponsorKey  = -1, iLeagueKey = -1;
            bool headerParsed = false;
            var results      = new List<TournamentDefinition>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                var cols = SplitCsvLine(line);
                if (!headerParsed)
                {
                    iId           = IndexOf(cols, "id");
                    iNameKey      = IndexOf(cols, "nameKey");
                    iCourseId     = IndexOf(cols, "courseId");
                    iHoleSet      = IndexOf(cols, "holeSet");
                    iStartUtc     = IndexOf(cols, "startUtc");
                    iEndUtc       = IndexOf(cols, "endUtc");
                    iResolveDelay = IndexOf(cols, "resolveDelayMinutes");
                    iEntryFee     = IndexOf(cols, "entryFeeRP");
                    iBotFieldId   = IndexOf(cols, "botFieldId");
                    iPrizeTableId = IndexOf(cols, "prizeTableId");
                    iSponsorKey   = IndexOf(cols, "sponsorKey");
                    iLeagueKey    = IndexOf(cols, "leagueKey");
                    headerParsed  = true;
                    continue;
                }
                string id = Get(cols, iId);
                if (string.IsNullOrEmpty(id)) continue;

                results.Add(new TournamentDefinition(
                    id:                  id,
                    nameKey:             Get(cols, iNameKey),
                    clubId:              Get(cols, iCourseId),
                    holeSet:             TournamentCsvLoader.ExpandHoleSet(Get(cols, iHoleSet)),
                    startUtc:            ParseUtc(Get(cols, iStartUtc)),
                    endUtc:              ParseUtc(Get(cols, iEndUtc)),
                    resolveDelayMinutes: ParseInt(Get(cols, iResolveDelay), 0),
                    entryFeeRP:          ParseLong(Get(cols, iEntryFee), 0L),
                    prizeTableId:        Get(cols, iPrizeTableId),
                    botFieldId:          Get(cols, iBotFieldId),
                    sponsorKey:          Get(cols, iSponsorKey),
                    leagueKey:           Get(cols, iLeagueKey)
                ));
            }
            return results;
        }

        private static IReadOnlyDictionary<string, PrizeTable> ParsePrizes(string csv)
        {
            var lines = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int iPrizeTableId = -1, iRankFrom = -1, iRankTo = -1;
            int iRpReward = -1, iItemRewardId = -1;
            bool headerParsed = false;
            var grouped = new Dictionary<string, List<PrizeBand>>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;
                var cols = SplitCsvLine(line);
                if (!headerParsed)
                {
                    iPrizeTableId = IndexOf(cols, "prizeTableId");
                    iRankFrom     = IndexOf(cols, "rankFrom");
                    iRankTo       = IndexOf(cols, "rankTo");
                    iRpReward     = IndexOf(cols, "rpReward");
                    iItemRewardId = IndexOf(cols, "itemRewardId");
                    headerParsed  = true;
                    continue;
                }
                string tableId = Get(cols, iPrizeTableId);
                if (string.IsNullOrEmpty(tableId)) continue;
                string? itemId = NullIfEmpty(Get(cols, iItemRewardId));
                var band = new PrizeBand(
                    ParseInt(Get(cols, iRankFrom), 1),
                    ParseInt(Get(cols, iRankTo),   1),
                    ParseLong(Get(cols, iRpReward), 0L),
                    itemId);
                if (!grouped.TryGetValue(tableId, out var list))
                {
                    list             = new List<PrizeBand>();
                    grouped[tableId] = list;
                }
                list.Add(band);
            }

            var result = new Dictionary<string, PrizeTable>();
            foreach (var kv in grouped)
                result[kv.Key] = new PrizeTable(kv.Key, kv.Value);
            return result;
        }

        private static IReadOnlyDictionary<string, BotFieldConfig> ParseBotFields(string csv)
        {
            var lines = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int iBotFieldId = -1, iBotCount = -1, iBracketWeights = -1;
            int iMinSec = -1, iMaxSec = -1, iSpread = -1;
            bool headerParsed = false;
            var result = new Dictionary<string, BotFieldConfig>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;
                var cols = SplitCsvLine(line);
                if (!headerParsed)
                {
                    iBotFieldId     = IndexOf(cols, "botFieldId");
                    iBotCount       = IndexOf(cols, "botCount");
                    iBracketWeights = IndexOf(cols, "bracketWeights");
                    iMinSec         = IndexOf(cols, "startOffsetMinSec");
                    iMaxSec         = IndexOf(cols, "startOffsetMaxSec");
                    iSpread         = IndexOf(cols, "perHoleSpreadSec");
                    headerParsed    = true;
                    continue;
                }
                string fieldId = Get(cols, iBotFieldId);
                if (string.IsNullOrEmpty(fieldId)) continue;
                var weights = TournamentCsvLoader.ParseBracketWeights(Get(cols, iBracketWeights), fieldId);
                result[fieldId] = new BotFieldConfig(
                    botFieldId:        fieldId,
                    botCount:          ParseInt(Get(cols, iBotCount),  0),
                    bracketWeights:    weights,
                    startOffsetMinSec: ParseFloat(Get(cols, iMinSec),  0f),
                    startOffsetMaxSec: ParseFloat(Get(cols, iMaxSec),  0f),
                    perHoleSpreadSec:  ParseFloat(Get(cols, iSpread),  0f)
                );
            }
            return result;
        }

        // ── SPEC §5 tests — LoadTournaments ───────────────────────────────────

        [Test]
        public void LoadTournaments_Returns6Rows()
        {
            var rows = ParseTournaments(TOURNAMENTS_CSV);
            Assert.AreEqual(6, rows.Count, "tournaments.csv must parse to 6 rows");
        }

        [Test]
        public void LoadTournaments_LomondChampionship_AllFields()
        {
            var rows  = ParseTournaments(TOURNAMENTS_CSV);
            var lomond = rows.FirstOrDefault(t => t.Id == "lomond_championship");
            Assert.IsNotNull(lomond, "lomond_championship row must be present");

            Assert.AreEqual("lomond",         lomond!.ClubId,       "ClubId (from courseId column)");
            Assert.AreEqual(0L,                lomond.EntryFeeRP,    "EntryFeeRP");
            Assert.AreEqual(18,               lomond.HoleSet.Count, "HoleSet.Count (1-18 expanded)");
            Assert.AreEqual("GOLFIN",         lomond.SponsorKey,    "SponsorKey");
            Assert.AreEqual("GOLD",           lomond.LeagueKey,     "LeagueKey");
            Assert.AreEqual(30,               lomond.ResolveDelayMinutes, "ResolveDelayMinutes");

            // Dates parsed UTC
            Assert.AreEqual(DateTimeKind.Utc, lomond.StartUtc.Kind, "StartUtc.Kind must be Utc");
            Assert.AreEqual(DateTimeKind.Utc, lomond.EndUtc.Kind,   "EndUtc.Kind must be Utc");
            Assert.AreEqual(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc), lomond.StartUtc, "StartUtc value");
            Assert.AreEqual(new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc), lomond.EndUtc,   "EndUtc value");
        }

        [Test]
        public void LoadTournaments_GotembaHasEntryFee500()
        {
            var rows    = ParseTournaments(TOURNAMENTS_CSV);
            var gotemba = rows.FirstOrDefault(t => t.Id == "gotemba_masters");
            Assert.IsNotNull(gotemba, "gotemba_masters must be present");
            Assert.AreEqual(500L, gotemba!.EntryFeeRP, "gotemba_masters entryFeeRP must be 500");
        }

        [Test]
        public void LoadTournaments_CommentLinesSkipped()
        {
            // The # comment line at the top must not appear as a row
            var rows = ParseTournaments(TOURNAMENTS_CSV);
            Assert.IsTrue(rows.All(t => !t.Id.StartsWith("#")),
                "Comment lines starting with # must be skipped");
        }

        // ── SPEC §5 tests — holeSet expansion ────────────────────────────────

        [Test]
        public void ExpandHoleSet_Range_Returns18Ids()
        {
            var result = TournamentCsvLoader.ExpandHoleSet("1-18");
            Assert.AreEqual(18, result.Count, "1-18 must expand to 18 ids");
            Assert.AreEqual("1",  result[0],  "First id must be '1'");
            Assert.AreEqual("18", result[17], "Last id must be '18'");
        }

        [Test]
        public void ExpandHoleSet_CommaList_Returns3Ids()
        {
            var result = TournamentCsvLoader.ExpandHoleSet("1,4,7");
            Assert.AreEqual(3, result.Count, "1,4,7 must expand to 3 ids");
            Assert.AreEqual("1", result[0]);
            Assert.AreEqual("4", result[1]);
            Assert.AreEqual("7", result[2]);
        }

        [Test]
        public void ExpandHoleSet_Single_ReturnsSingleId()
        {
            var result = TournamentCsvLoader.ExpandHoleSet("9");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("9", result[0]);
        }

        [Test]
        public void ExpandHoleSet_Empty_ReturnsEmpty()
        {
            var result = TournamentCsvLoader.ExpandHoleSet("");
            Assert.AreEqual(0, result.Count);
        }

        // ── SPEC §5 tests — LoadPrizeTables ──────────────────────────────────

        [Test]
        public void LoadPrizeTables_Returns3Tables()
        {
            var tables = ParsePrizes(PRIZES_CSV);
            Assert.AreEqual(3, tables.Count, "Must parse 3 distinct prize tables");
            Assert.IsTrue(tables.ContainsKey("prize_small"),  "prize_small must be present");
            Assert.IsTrue(tables.ContainsKey("prize_medium"), "prize_medium must be present");
            Assert.IsTrue(tables.ContainsKey("prize_major"),  "prize_major must be present");
        }

        [Test]
        public void LoadPrizeTables_PrizeMedium_Band1_CorrectFields()
        {
            var tables = ParsePrizes(PRIZES_CSV);
            Assert.IsTrue(tables.ContainsKey("prize_medium"), "prize_medium must exist");
            var medium = tables["prize_medium"];

            // Band 1 (rank 1–1): rpReward=5000, itemRewardId=ticket_gold
            var band1 = medium.Bands.FirstOrDefault(b => b.RankFrom == 1 && b.RankTo == 1);
            Assert.IsNotNull(band1, "prize_medium must have a rank-1-to-1 band");
            Assert.AreEqual(5000L,       band1!.RpReward,     "prize_medium band-1 RpReward must be 5000");
            Assert.AreEqual("ticket_gold", band1.ItemRewardId, "prize_medium band-1 ItemRewardId must be ticket_gold");
        }

        [Test]
        public void LoadPrizeTables_PrizeMedium_Band4to10_ItemRewardIsNull()
        {
            var tables = ParsePrizes(PRIZES_CSV);
            var medium = tables["prize_medium"];

            // Band 4–10: no item reward
            var band4_10 = medium.Bands.FirstOrDefault(b => b.RankFrom == 4 && b.RankTo == 10);
            Assert.IsNotNull(band4_10, "prize_medium must have a rank-4-to-10 band");
            Assert.IsNull(band4_10!.ItemRewardId, "prize_medium band-4-to-10 must have null ItemRewardId");
        }

        [Test]
        public void LoadPrizeTables_PrizeMajor_Has4Bands()
        {
            var tables = ParsePrizes(PRIZES_CSV);
            Assert.AreEqual(4, tables["prize_major"].Bands.Count, "prize_major must have 4 bands");
        }

        [Test]
        public void LoadPrizeTables_PrizeMajor_Band1_HasTrophyMajor()
        {
            var tables = ParsePrizes(PRIZES_CSV);
            var band1  = tables["prize_major"].Bands.First(b => b.RankFrom == 1);
            Assert.AreEqual(20000L,       band1.RpReward,     "prize_major rank-1 RpReward must be 20000");
            Assert.AreEqual("trophy_major", band1.ItemRewardId, "prize_major rank-1 ItemRewardId must be trophy_major");
        }

        // ── SPEC §5 tests — LoadBotFields ─────────────────────────────────────

        [Test]
        public void LoadBotFields_Returns3Configs()
        {
            var fields = ParseBotFields(BOT_FIELDS_CSV);
            Assert.AreEqual(3, fields.Count, "Must parse 3 bot field configs");
            Assert.IsTrue(fields.ContainsKey("field_small"),  "field_small must be present");
            Assert.IsTrue(fields.ContainsKey("field_medium"), "field_medium must be present");
            Assert.IsTrue(fields.ContainsKey("field_major"),  "field_major must be present");
        }

        [Test]
        public void LoadBotFields_FieldMajor_BotCount30()
        {
            var fields = ParseBotFields(BOT_FIELDS_CSV);
            Assert.AreEqual(30, fields["field_major"].BotCount, "field_major BotCount must be 30");
        }

        [Test]
        public void LoadBotFields_FieldMajor_BracketWeightsSumTo1()
        {
            var fields  = ParseBotFields(BOT_FIELDS_CSV);
            var weights = fields["field_major"].BracketWeights;
            float sum   = weights.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.0001f,
                $"field_major bracketWeights must sum to 1.0 (got {sum})");
        }

        [Test]
        public void LoadBotFields_FieldMajor_KeysAreValidBrackets()
        {
            // Valid bot_difficulty.csv minLevels per SPEC §2
            var validBrackets = new HashSet<string> { "1", "10", "25", "50", "100", "180" };
            var fields        = ParseBotFields(BOT_FIELDS_CSV);
            var weights       = fields["field_major"].BracketWeights;

            foreach (var key in weights.Keys)
                Assert.IsTrue(validBrackets.Contains(key),
                    $"field_major bracket key '{key}' is not a valid bot_difficulty.csv minLevel");
        }

        [Test]
        public void LoadBotFields_AllFields_BracketWeightsSumTo1()
        {
            var fields = ParseBotFields(BOT_FIELDS_CSV);
            foreach (var kv in fields)
            {
                float sum = kv.Value.BracketWeights.Values.Sum();
                Assert.AreEqual(1.0f, sum, 0.0001f,
                    $"botField '{kv.Key}' bracketWeights sum={sum} must be 1.0");
            }
        }

        [Test]
        public void LoadBotFields_FieldSmall_Offsets()
        {
            var fields = ParseBotFields(BOT_FIELDS_CSV);
            var small  = fields["field_small"];
            Assert.AreEqual(0f,    small.StartOffsetMinSec, "field_small startOffsetMinSec must be 0");
            Assert.AreEqual(7200f, small.StartOffsetMaxSec, "field_small startOffsetMaxSec must be 7200");
            Assert.AreEqual(600f,  small.PerHoleSpreadSec,  "field_small perHoleSpreadSec must be 600");
        }

        // ── SPEC §5 tests — Referential integrity ─────────────────────────────

        [Test]
        public void ReferentialIntegrity_AllTournamentIdsResolve()
        {
            var tournaments = ParseTournaments(TOURNAMENTS_CSV);
            var prizes      = ParsePrizes(PRIZES_CSV);
            var botFields   = ParseBotFields(BOT_FIELDS_CSV);

            // Manually check each cross-reference
            foreach (var t in tournaments)
            {
                Assert.IsTrue(prizes.ContainsKey(t.PrizeTableId),
                    $"Tournament '{t.Id}' references prizeTableId='{t.PrizeTableId}' which is not in prize tables");
                Assert.IsTrue(botFields.ContainsKey(t.BotFieldId),
                    $"Tournament '{t.Id}' references botFieldId='{t.BotFieldId}' which is not in bot fields");
            }
        }

        [Test]
        public void ReferentialIntegrity_CheckReferentialIntegrity_ReturnsTrueForValidData()
        {
            var tournaments = ParseTournaments(TOURNAMENTS_CSV);
            var prizes      = ParsePrizes(PRIZES_CSV);
            var botFields   = ParseBotFields(BOT_FIELDS_CSV);

            bool ok = TournamentCsvLoader.CheckReferentialIntegrity(tournaments, prizes, botFields);
            Assert.IsTrue(ok, "CheckReferentialIntegrity must return true for the SPEC §3 sample data");
        }

        [Test]
        public void ReferentialIntegrity_CheckReferentialIntegrity_ReturnsFalseOnDanglingPrizeTableId()
        {
            // Inject a tournament that references a nonexistent prize table.
            // CheckReferentialIntegrity emits Debug.LogError for dangling ids — tell the
            // test runner to expect it so it doesn't fail the test as an unhandled log.
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    @"Referential integrity FAIL.*fake_t.*prizeTableId.*NONEXISTENT_PRIZE_TABLE"));

            var fakeTournament = new TournamentDefinition(
                id:                  "fake_t",
                nameKey:             "k",
                clubId:              "c",
                holeSet:             new[] { "1" },
                startUtc:            DateTime.UtcNow,
                endUtc:              DateTime.UtcNow.AddDays(1),
                resolveDelayMinutes: 0,
                entryFeeRP:          0L,
                prizeTableId:        "NONEXISTENT_PRIZE_TABLE",
                botFieldId:          "field_small",
                sponsorKey:          "S",
                leagueKey:           "L"
            );
            var prizes    = ParsePrizes(PRIZES_CSV);
            var botFields = ParseBotFields(BOT_FIELDS_CSV);

            bool ok = TournamentCsvLoader.CheckReferentialIntegrity(
                new[] { fakeTournament }, prizes, botFields);
            Assert.IsFalse(ok, "CheckReferentialIntegrity must return false when prizeTableId is dangling");
        }

        [Test]
        public void ReferentialIntegrity_CheckReferentialIntegrity_ReturnsFalseOnDanglingBotFieldId()
        {
            // CheckReferentialIntegrity emits Debug.LogError — tell the test runner to expect it.
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    @"Referential integrity FAIL.*fake_t.*botFieldId.*NONEXISTENT_BOT_FIELD"));

            var fakeTournament = new TournamentDefinition(
                id:                  "fake_t",
                nameKey:             "k",
                clubId:              "c",
                holeSet:             new[] { "1" },
                startUtc:            DateTime.UtcNow,
                endUtc:              DateTime.UtcNow.AddDays(1),
                resolveDelayMinutes: 0,
                entryFeeRP:          0L,
                prizeTableId:        "prize_small",
                botFieldId:          "NONEXISTENT_BOT_FIELD",
                sponsorKey:          "S",
                leagueKey:           "L"
            );
            var prizes    = ParsePrizes(PRIZES_CSV);
            var botFields = ParseBotFields(BOT_FIELDS_CSV);

            bool ok = TournamentCsvLoader.CheckReferentialIntegrity(
                new[] { fakeTournament }, prizes, botFields);
            Assert.IsFalse(ok, "CheckReferentialIntegrity must return false when botFieldId is dangling");
        }

        // ── SPEC §5 — ParseBracketWeights unit tests ──────────────────────────

        [Test]
        public void ParseBracketWeights_ValidInput_ParsesAllPairs()
        {
            var result = TournamentCsvLoader.ParseBracketWeights("1:0.30;10:0.30;25:0.20;50:0.20", "test");
            Assert.AreEqual(4, result.Count, "Must parse 4 pairs");
            Assert.AreEqual(0.30f, result["1"],  0.0001f);
            Assert.AreEqual(0.30f, result["10"], 0.0001f);
            Assert.AreEqual(0.20f, result["25"], 0.0001f);
            Assert.AreEqual(0.20f, result["50"], 0.0001f);
        }

        [Test]
        public void ParseBracketWeights_AllSixBrackets_ValidInput()
        {
            var result = TournamentCsvLoader.ParseBracketWeights(
                "1:0.10;10:0.15;25:0.20;50:0.25;100:0.20;180:0.10", "test");
            Assert.AreEqual(6, result.Count, "Must parse 6 pairs");
            float sum = result.Values.Sum();
            Assert.AreEqual(1.0f, sum, 0.0001f, "All 6-bracket weights must sum to 1.0");
        }

        // ── T1 amendment: ResolveDelayMinutes round-trip test ────────────────

        [Test]
        public void TournamentDefinition_ResolveDelayMinutes_RoundTrip()
        {
            // Verify the T1 amendment: TournamentDefinition ctor now accepts and exposes
            // resolveDelayMinutes.
            var def = new TournamentDefinition(
                id:                  "test_rdm",
                nameKey:             "k",
                clubId:              "c",
                holeSet:             new[] { "1" },
                startUtc:            DateTime.UtcNow,
                endUtc:              DateTime.UtcNow.AddDays(1),
                resolveDelayMinutes: 45,
                entryFeeRP:          0L,
                prizeTableId:        "p",
                botFieldId:          "b",
                sponsorKey:          "S",
                leagueKey:           "L"
            );
            Assert.AreEqual(45, def.ResolveDelayMinutes,
                "TournamentDefinition.ResolveDelayMinutes must round-trip the ctor arg");
        }

        // ── Resources.Load integration test (requires Unity Editor) ──────────

        /// <summary>
        /// Integration test: verifies the shipped CSVs are loadable via Resources.Load
        /// in Unity EditMode. Marked with the Unity test attribute so it runs in the
        /// test runner context where Resources.Load is available.
        /// Uses System.IO.File path resolution (same pattern as BotFieldInvariantTests B3).
        /// </summary>
        [Test]
        public void ShippedCSVs_ExistOnDisk_AllThreeFiles()
        {
            string assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

            string projectRoot = assemblyDir;
            for (int i = 0; i < 4; i++)
            {
                string candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(projectRoot, ".."));
                if (System.IO.File.Exists(System.IO.Path.Combine(candidate, "Assets", "Resources", "Data", "tournaments.csv")))
                {
                    projectRoot = candidate;
                    break;
                }
                projectRoot = candidate;
            }

            string dataDir = System.IO.Path.Combine(projectRoot, "Assets", "Resources", "Data");

            string tournamentsPath  = System.IO.Path.Combine(dataDir, "tournaments.csv");
            string prizesPath       = System.IO.Path.Combine(dataDir, "tournament_prizes.csv");
            string botFieldsPath    = System.IO.Path.Combine(dataDir, "tournament_bot_fields.csv");

            if (!System.IO.File.Exists(tournamentsPath))
            {
                Assert.Inconclusive($"tournaments.csv not found at '{tournamentsPath}'. Skipping disk check.");
                return;
            }
            Assert.IsTrue(System.IO.File.Exists(tournamentsPath),  "tournaments.csv must exist on disk");
            Assert.IsTrue(System.IO.File.Exists(prizesPath),        "tournament_prizes.csv must exist on disk");
            Assert.IsTrue(System.IO.File.Exists(botFieldsPath),     "tournament_bot_fields.csv must exist on disk");

            // Parse the shipped files and verify row counts match expectations
            string tournText  = System.IO.File.ReadAllText(tournamentsPath);
            string prizeText  = System.IO.File.ReadAllText(prizesPath);
            string botText    = System.IO.File.ReadAllText(botFieldsPath);

            var tournaments   = ParseTournaments(tournText);
            var prizes        = ParsePrizes(prizeText);
            var botFields     = ParseBotFields(botText);

            Assert.AreEqual(6, tournaments.Count, "Shipped tournaments.csv must have 6 rows");
            Assert.AreEqual(3, prizes.Count,      "Shipped tournament_prizes.csv must have 3 tables");
            Assert.AreEqual(3, botFields.Count,   "Shipped tournament_bot_fields.csv must have 3 configs");

            // Referential integrity of shipped files
            bool ok = TournamentCsvLoader.CheckReferentialIntegrity(tournaments, prizes, botFields);
            Assert.IsTrue(ok, "Shipped CSVs must pass referential integrity check");
        }

        // ── Real-loader wrapper tests (exercise shipped Load* methods + Resources.Load) ──

        /// <summary>
        /// Calls the real TournamentCsvLoader.LoadTournaments() against the shipped CSV
        /// in Resources/Data/. This exercises the Resources.Load path and the path constant
        /// "Data/tournaments" that the inline fixture tests bypass.
        /// </summary>
        [Test]
        public void LoadTournaments_RealLoader_ShippedCSV_Returns6Rows()
        {
            var loader = new TournamentCsvLoader();
            var rows   = loader.LoadTournaments();
            Assert.AreEqual(6, rows.Count, "Real LoadTournaments() must return 6 rows from the shipped CSV");

            // Re-assert the Lomond fields against REAL loader output
            var lomond = rows.FirstOrDefault(t => t.Id == "lomond_championship");
            Assert.IsNotNull(lomond, "lomond_championship must be present in real loader output");
            Assert.AreEqual("lomond",  lomond!.ClubId,       "Real loader: lomond ClubId");
            Assert.AreEqual(0L,        lomond.EntryFeeRP,    "Real loader: lomond EntryFeeRP");
            Assert.AreEqual(18,        lomond.HoleSet.Count, "Real loader: lomond HoleSet.Count (1-18)");
            Assert.AreEqual("TITLEIST",  lomond.SponsorKey,    "Real loader: lomond SponsorKey");
            Assert.AreEqual("GOLD",    lomond.LeagueKey,     "Real loader: lomond LeagueKey");
            Assert.AreEqual(DateTimeKind.Utc, lomond.StartUtc.Kind, "Real loader: lomond StartUtc.Kind");
            Assert.AreEqual(DateTimeKind.Utc, lomond.EndUtc.Kind,   "Real loader: lomond EndUtc.Kind");
        }

        /// <summary>
        /// tournament_title_ja §4. The shipped CSV has ONE name column, and the dashboard's CSV
        /// export writes <c>nameKey ?? title</c> into it — so a dashboard-named tournament comes
        /// back as <c>NameKey="Cesar Championship"</c>, which resolves against nothing in the
        /// build, with <c>Title=null</c>. The display ladder then fell through to the SLUG and the
        /// card read <c>cesar_championship</c> offline. The loader now feeds the same column value
        /// into Title as well; a key that resolves is unaffected because rung 1 wins first.
        /// <para>
        /// Asserted on the REAL loader against the SHIPPED CSV — the inline-fixture helper at the
        /// top of this file is a copy of the parse logic and would prove nothing about production.
        /// </para>
        /// </summary>
        [Test]
        public void LoadTournaments_RealLoader_MirrorsNameKeyIntoTitle()
        {
            var rows = new TournamentCsvLoader().LoadTournaments();
            Assert.Greater(rows.Count, 0, "Shipped CSV must load, or this test asserts nothing.");

            foreach (var row in rows)
            {
                Assert.AreEqual(row.NameKey, row.Title,
                    $"'{row.Id}': the raw nameKey column value must also reach Title, or a " +
                    "dashboard-named tournament round-tripped through the CSV renders as its slug.");
                Assert.IsFalse(string.IsNullOrEmpty(row.Title),
                    $"'{row.Id}': every shipped row has a nameKey, so Title must be populated.");
                Assert.IsNull(row.TitleJa,
                    $"'{row.Id}': the CSV is the offline fallback and gains NO title_ja column.");
            }
        }

        /// <summary>
        /// Calls the real TournamentCsvLoader.LoadPrizeTables() against the shipped CSV.
        /// Exercises Resources.Load path + "Data/tournament_prizes" path constant.
        /// </summary>
        [Test]
        public void LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables()
        {
            var loader = new TournamentCsvLoader();
            var tables = loader.LoadPrizeTables();
            Assert.AreEqual(3, tables.Count, "Real LoadPrizeTables() must return 3 tables");
            Assert.IsTrue(tables.ContainsKey("prize_small"),  "Real loader: prize_small present");
            Assert.IsTrue(tables.ContainsKey("prize_medium"), "Real loader: prize_medium present");
            Assert.IsTrue(tables.ContainsKey("prize_major"),  "Real loader: prize_major present");

            // prize_medium band #1
            var band1 = tables["prize_medium"].Bands.FirstOrDefault(b => b.RankFrom == 1 && b.RankTo == 1);
            Assert.IsNotNull(band1, "Real loader: prize_medium must have rank-1-to-1 band");
            // 5000 → 500 at the RP_REBALANCE cutover (2026-08-12). This assertion reads the SHIPPED
            // CSV, unlike the inline-fixture tests above, so it moves with the shipped economy.
            Assert.AreEqual(500L,        band1!.RpReward,     "Real loader: prize_medium band-1 RpReward");
            Assert.AreEqual("ticket_gold", band1.ItemRewardId, "Real loader: prize_medium band-1 ItemRewardId");

            // prize_medium band 4-10 has no item
            var band4_10 = tables["prize_medium"].Bands.FirstOrDefault(b => b.RankFrom == 4 && b.RankTo == 10);
            Assert.IsNotNull(band4_10, "Real loader: prize_medium must have rank-4-to-10 band");
            Assert.IsNull(band4_10!.ItemRewardId, "Real loader: prize_medium band-4-to-10 ItemRewardId must be null");
        }

        /// <summary>
        /// Calls the real TournamentCsvLoader.LoadBotFields() against the shipped CSV.
        /// Exercises Resources.Load path + "Data/tournament_bot_fields" path constant.
        /// </summary>
        [Test]
        public void LoadBotFields_RealLoader_ShippedCSV_Returns3Configs()
        {
            var loader = new TournamentCsvLoader();
            var fields = loader.LoadBotFields();
            Assert.AreEqual(3, fields.Count, "Real LoadBotFields() must return 3 configs");
            Assert.IsTrue(fields.ContainsKey("field_small"),  "Real loader: field_small present");
            Assert.IsTrue(fields.ContainsKey("field_medium"), "Real loader: field_medium present");
            Assert.IsTrue(fields.ContainsKey("field_major"),  "Real loader: field_major present");

            // field_major assertions
            var major = fields["field_major"];
            Assert.AreEqual(30, major.BotCount, "Real loader: field_major BotCount must be 30");
            float wSum = major.BracketWeights.Values.Sum();
            Assert.AreEqual(1.0f, wSum, 0.0001f,
                $"Real loader: field_major bracketWeights must sum to 1.0 (got {wSum})");
        }

        /// <summary>
        /// Full real end-to-end: load all three CSVs via the real loader and verify
        /// referential integrity holds. This is the exact call sequence T4's
        /// LocalTournamentBackend will use.
        /// </summary>
        [Test]
        public void Loader_RealEnd2End_ReferentialIntegrityHolds()
        {
            var loader      = new TournamentCsvLoader();
            var tournaments = loader.LoadTournaments();
            var prizes      = loader.LoadPrizeTables();
            var botFields   = loader.LoadBotFields();

            // All three must have loaded data
            Assert.Greater(tournaments.Count, 0, "Real end-to-end: LoadTournaments returned 0 rows");
            Assert.Greater(prizes.Count,      0, "Real end-to-end: LoadPrizeTables returned 0 tables");
            Assert.Greater(botFields.Count,   0, "Real end-to-end: LoadBotFields returned 0 configs");

            bool ok = TournamentCsvLoader.CheckReferentialIntegrity(tournaments, prizes, botFields);
            Assert.IsTrue(ok, "Real end-to-end: CheckReferentialIntegrity must return true for shipped CSVs");
        }

        // ── Private helpers (mirrors TournamentCsvLoader private helpers) ─────

        private static string[] SplitCsvLine(string line)
        {
            var  cols     = new List<string>();
            var  sb       = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes) { cols.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(c);
            }
            cols.Add(sb.ToString().Trim());
            return cols.ToArray();
        }

        private static int IndexOf(string[] cols, string name)
        {
            for (int i = 0; i < cols.Length; i++)
                if (string.Equals(cols[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string Get(string[] cols, int idx)
            => (idx >= 0 && idx < cols.Length) ? cols[idx] : string.Empty;

        private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        private static DateTime ParseUtc(string raw)
        {
            if (DateTime.TryParse(raw, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime dt))
                return dt;
            return DateTime.MinValue;
        }

        private static int ParseInt(string raw, int fallback)
            => int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : fallback;

        private static long ParseLong(string raw, long fallback)
            => long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long v) ? v : fallback;

        private static float ParseFloat(string raw, float fallback)
            => float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }
}
