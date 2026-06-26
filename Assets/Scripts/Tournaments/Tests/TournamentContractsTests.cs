// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — TournamentContractsTests
// EditMode compile-gate test (SPEC §5).
// Validates DTO construction, enum exhaustiveness, anti-cheat field presence,
// and the ITournamentBackend seam through StubTournamentBackend.
// No game logic, no Unity play-mode required.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Golfin.Tournaments;

namespace Golfin.Tournaments.Tests
{
    [TestFixture]
    public class TournamentContractsTests
    {
        // ── 1. TournamentDefinition round-trip ──────────────────────────────

        [Test]
        public void TournamentDefinition_ConstructAndRead()
        {
            var holeSet = new[] { "h1", "h2", "h3" };
            var startUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var endUtc   = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

            var def = new TournamentDefinition(
                id:                  "weekly_lomond_9",
                nameKey:             "TOURNAMENT_WEEKLY_LOMOND_9",
                clubId:              "lomond",
                holeSet:             holeSet,
                startUtc:            startUtc,
                endUtc:              endUtc,
                resolveDelayMinutes: 30,
                entryFeeRP:          500L,
                prizeTableId:        "medium",
                botFieldId:          "medium_9hole",
                sponsorKey:          "SPONSOR_WONDERWALL",
                leagueKey:           "LEAGUE_WEEKLY"
            );

            Assert.AreEqual("weekly_lomond_9", def.Id);
            Assert.AreEqual("lomond",          def.ClubId);
            Assert.AreEqual(3,                 def.HoleSet.Count);
            Assert.AreEqual(500L,              def.EntryFeeRP);
            Assert.AreEqual(startUtc,          def.StartUtc);
            Assert.AreEqual(endUtc,            def.EndUtc);
        }

        // ── 2. ShotCommand round-trip ───────────────────────────────────────

        [Test]
        public void ShotCommand_ConstructAndRead()
        {
            var utc = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Utc);
            var cmd = new ShotCommand(
                shotIndex:    0,
                power:        0.82f,
                accuracy:     -0.05f,
                clubId:       "driver_callaway",
                committedUtc: utc
            );

            Assert.AreEqual(0,               cmd.ShotIndex);
            Assert.AreEqual(0.82f,           cmd.Power,    0.0001f);
            Assert.AreEqual(-0.05f,          cmd.Accuracy, 0.0001f);
            Assert.AreEqual("driver_callaway", cmd.ClubId);
            Assert.AreEqual(utc,             cmd.CommittedUtc);
        }

        // ── 3. HoleResult must carry rngSeed + inputLog (SPEC §0.3 / GDD §8) ─

        [Test]
        public void HoleResult_CarriesRngSeedAndInputLog()
        {
            var commands = new List<ShotCommand>
            {
                new ShotCommand(0, 0.9f, 0f, "driver", DateTime.UtcNow),
                new ShotCommand(1, 0.5f, 0.1f, "putter", DateTime.UtcNow),
            };

            var result = new HoleResult(
                holeId:       "h1",
                strokes:      4,
                timeSeconds:  95.5f,
                completedUtc: DateTime.UtcNow,
                rngSeed:      42,
                inputLog:     commands
            );

            // Anti-cheat fields must be present and non-zero/non-null
            Assert.AreEqual(42, result.RngSeed, "HoleResult must carry rngSeed");
            Assert.IsNotNull(result.InputLog,   "HoleResult.InputLog must not be null");
            Assert.AreEqual(2, result.InputLog.Count, "InputLog must carry all submitted shots");
            Assert.AreEqual(4, result.Strokes);
            Assert.AreEqual("h1", result.HoleId);
        }

        [Test]
        public void HoleResult_NullInputLog_BecomesEmptyList()
        {
            // v1 allows empty inputLog; null must be coerced to empty list
            var result = new HoleResult("h1", 3, 60f, DateTime.UtcNow, 99, null!);
            Assert.IsNotNull(result.InputLog, "Null inputLog must be converted to empty list");
            Assert.AreEqual(0, result.InputLog.Count);
        }

        // ── 4. EntryState round-trip ────────────────────────────────────────

        [Test]
        public void EntryState_ConstructAndRead()
        {
            var holes = new List<HoleResult>
            {
                new HoleResult("h1", 3, 55f, DateTime.UtcNow, 1, new List<ShotCommand>()),
            };

            var entry = new EntryState(
                tournamentId: "t1",
                characterId:  "char_frodo",
                perHole:      holes,
                startedUtc:   DateTime.UtcNow.AddHours(-2),
                lastHoleUtc:  DateTime.UtcNow.AddHours(-1),
                status:       EntryStatus.InProgress
            );

            Assert.AreEqual("t1",                entry.TournamentId);
            Assert.AreEqual("char_frodo",        entry.CharacterId);
            Assert.AreEqual(1,                   entry.PerHole.Count);
            Assert.AreEqual(EntryStatus.InProgress, entry.Status);
            Assert.IsNotNull(entry.LastHoleUtc);
        }

        // ── 5. TournamentLeaderboardEntry round-trip ────────────────────────

        [Test]
        public void TournamentLeaderboardEntry_ConstructAndRead()
        {
            var entry = new TournamentLeaderboardEntry
            {
                Rank          = 2,
                IsTie         = true,
                DisplayName   = "FRODO",
                CharacterId   = "char_frodo",
                Level         = 120,
                Strokes       = 33,
                Thru          = 9,
                TimeSeconds   = 540f,
                IsPlayer      = false,
                IsDNF         = false,
                IsProvisional = true,
            };

            Assert.AreEqual(2,       entry.Rank);
            Assert.IsTrue(entry.IsTie);
            Assert.AreEqual(33,      entry.Strokes);
            Assert.AreEqual(9,       entry.Thru);
            Assert.IsTrue(entry.IsProvisional);
        }

        // ── 6. PrizeBand / PrizeTable round-trip ───────────────────────────

        [Test]
        public void PrizeTable_ConstructAndRead()
        {
            var bands = new List<PrizeBand>
            {
                new PrizeBand(1, 1,   5000L, "club_driver_promo"),
                new PrizeBand(2, 3,   2000L, null),
                new PrizeBand(4, 10,   500L, null),
            };
            var table = new PrizeTable("medium", bands);

            Assert.AreEqual("medium", table.PrizeTableId);
            Assert.AreEqual(3,        table.Bands.Count);
            Assert.AreEqual("club_driver_promo", table.Bands[0].ItemRewardId);
            Assert.IsNull(table.Bands[1].ItemRewardId);
        }

        // ── 7. TournamentResult round-trip ──────────────────────────────────

        [Test]
        public void TournamentResult_ConstructAndRead()
        {
            var result = new TournamentResult(
                finalRank:    1,
                isTie:        false,
                prizeRP:      5000L,
                itemRewardId: "club_driver_promo",
                claimed:      false
            );

            Assert.AreEqual(1,                   result.FinalRank);
            Assert.IsFalse(result.IsTie);
            Assert.AreEqual(5000L,               result.PrizeRP);
            Assert.AreEqual("club_driver_promo", result.ItemRewardId);
            Assert.IsFalse(result.Claimed);
        }

        // ── 8. BotFieldConfig + BotCard round-trip ──────────────────────────

        [Test]
        public void BotFieldConfig_ConstructAndRead()
        {
            var weights = new Dictionary<string, float> { ["easy"] = 0.4f, ["medium"] = 0.4f, ["hard"] = 0.2f };
            var config = new BotFieldConfig(
                botFieldId:        "medium_9hole",
                botCount:          24,
                bracketWeights:    weights,
                startOffsetMinSec: 60f,
                startOffsetMaxSec: 3600f,
                perHoleSpreadSec:  120f
            );

            Assert.AreEqual("medium_9hole", config.BotFieldId);
            Assert.AreEqual(24,             config.BotCount);
            Assert.AreEqual(3,              config.BracketWeights.Count);
        }

        [Test]
        public void BotCard_ConstructAndRead()
        {
            var strokes = new[] { 3, 4, 5, 3, 4, 4, 3, 4, 3 };
            var times = new[]
            {
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddHours(3),
                DateTime.UtcNow.AddHours(4),
                DateTime.UtcNow.AddHours(5),
                DateTime.UtcNow.AddHours(6),
                DateTime.UtcNow.AddHours(7),
                DateTime.UtcNow.AddHours(8),
                DateTime.UtcNow.AddHours(9),
            };

            var card = new BotCard(
                botId:                "frodo",
                perHoleStrokes:       strokes,
                totalStrokes:         33,
                startOffsetSeconds:   300f,
                perHoleCompletionUtc: times
            );

            // BotCard.botId must reference fake_players.csv identity (SPEC §4 reuse)
            Assert.AreEqual("frodo",  card.BotId);
            Assert.AreEqual(9,        card.PerHoleStrokes.Count);
            Assert.AreEqual(33,       card.TotalStrokes);
            Assert.AreEqual(9,        card.PerHoleCompletionUtc.Count);
        }

        // ── 9. TournamentState enum exhaustiveness ──────────────────────────

        [Test]
        public void TournamentState_AllCasesCovered()
        {
            // Ensure every declared enum value has a case in the switch below.
            // If a new state is added, this test breaks until the switch is updated.
            var allStates = (TournamentState[])Enum.GetValues(typeof(TournamentState));
            Assert.AreEqual(6, allStates.Length,
                "Expected 6 TournamentState values: Upcoming/Open/Playing/Ending/Closed/Ended. " +
                "Update this assertion if the enum changes.");

            foreach (var state in allStates)
            {
                // The switch must cover every case; default is intentionally absent.
                string label = state switch
                {
                    TournamentState.Upcoming => "UPCOMING",
                    TournamentState.Open     => "OPEN",
                    TournamentState.Playing  => "PLAYING",
                    TournamentState.Ending   => "ENDING",
                    TournamentState.Closed   => "CLOSED",
                    TournamentState.Ended    => "ENDED",
                    _ => throw new Exception($"Uncovered TournamentState: {state}"),
                };
                Assert.IsNotEmpty(label, $"TournamentState.{state} has no label mapping");
            }
        }

        // ── 10. EntryStatus enum exhaustiveness ─────────────────────────────

        [Test]
        public void EntryStatus_AllCasesCovered()
        {
            var allStatuses = (EntryStatus[])Enum.GetValues(typeof(EntryStatus));
            Assert.AreEqual(4, allStatuses.Length,
                "Expected 4 EntryStatus values: NotEntered/InProgress/Finished/DNF.");

            foreach (var status in allStatuses)
            {
                string label = status switch
                {
                    EntryStatus.NotEntered  => "NOT_ENTERED",
                    EntryStatus.InProgress  => "IN_PROGRESS",
                    EntryStatus.Finished    => "FINISHED",
                    EntryStatus.DNF         => "DNF",
                    _ => throw new Exception($"Uncovered EntryStatus: {status}"),
                };
                Assert.IsNotEmpty(label, $"EntryStatus.{status} has no label mapping");
            }
        }

        // ── 11. StubTournamentBackend — seam test ────────────────────────────

        [Test]
        public void StubBackend_ImplementsInterface()
        {
            // Verify the stub compiles and is usable through the ITournamentBackend seam.
            ITournamentBackend backend = new StubTournamentBackend();

            var tournaments = backend.GetTournaments();
            Assert.IsNotNull(tournaments, "GetTournaments must not return null");
            Assert.Greater(tournaments.Count, 0, "Stub must return at least one tournament");

            var def = backend.GetTournament("stub_tournament");
            Assert.IsNotNull(def, "GetTournament must not return null for known id");

            var entry = backend.Register("stub_tournament", 0L, "char_frodo");
            Assert.IsNotNull(entry, "Register must return an EntryState");
            Assert.AreEqual(EntryStatus.InProgress, entry.Status);

            var myEntry = backend.GetMyEntry("stub_tournament");
            Assert.IsNotNull(myEntry, "GetMyEntry must not return null for stub");

            var holeResult = new HoleResult(
                "h1", 4, 60f, DateTime.UtcNow, 123, new List<ShotCommand>()
            );
            var updated = backend.SubmitHoleResult("stub_tournament", holeResult);
            Assert.IsNotNull(updated, "SubmitHoleResult must return an EntryState");

            var board = backend.GetLeaderboard("stub_tournament");
            Assert.IsNotNull(board, "GetLeaderboard must not return null");
            Assert.Greater(board.Count, 0, "Stub board must have at least one entry");

            var results = backend.GetResults("stub_tournament");
            Assert.IsNotNull(results, "GetResults must not return null for stub");

            // ClaimPrize must not throw
            Assert.DoesNotThrow(() => backend.ClaimPrize("stub_tournament"),
                "ClaimPrize must not throw on stub");
        }

        // ── 12. ITournamentClock — TimeProviderClock wraps ITimeProvider ─────

        [Test]
        public void TimeProviderClock_WrapsITimeProvider()
        {
            // Use a fixed-time stub to verify the clock wraps correctly.
            var expected = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
            var stub     = new FixedTimeProvider(expected);
            var clock    = new TimeProviderClock(stub);

            Assert.AreEqual(expected, clock.UtcNow,
                "ITournamentClock.UtcNow must delegate to the wrapped ITimeProvider");
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>Test-only ITimeProvider that returns a fixed time.</summary>
        private sealed class FixedTimeProvider : Golfin.UI.Rankings.ITimeProvider
        {
            private readonly DateTime _utcNow;
            public FixedTimeProvider(DateTime utcNow) { _utcNow = utcNow; }
            public DateTime UtcNow       => _utcNow;
            public bool IsAuthoritative  => true;
        }
    }
}
