// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — MapCardStateTests
// EditMode unit tests for the pure TournamentCardStateMapper.Map() function
// (SPEC §4, 7-row table from SPEC §2).
//
// TournamentSelectionScreenController.MapCardState() delegates to this mapper,
// so removing TournamentCardStateMapper.Map makes both the tests AND the
// selection screen fail — zero circular-gate risk (Lesson: feedback_tests_must_
// target_production_type).
// ─────────────────────────────────────────────────────────────────────────────
using NUnit.Framework;
using Golfin.Tournaments;

namespace Golfin.Tournaments.Tests
{
    [TestFixture]
    public class MapCardStateTests
    {
        // Convenience wrapper — parameter names match TournamentCardStateMapper.Map
        // so named-argument calls compile without ambiguity.
        private static TournamentCardState Map(
            TournamentState state,
            EntryStatus entryStatus,
            bool nowPastEnd)
            => TournamentCardStateMapper.Map(state, entryStatus, nowPastEnd);

        // ── Row 1: Upcoming state → Upcoming ─────────────────────────────────
        [Test]
        public void Row1_Upcoming_NoEntry_ReturnsUpcoming()
        {
            Assert.AreEqual(TournamentCardState.Upcoming,
                Map(TournamentState.Upcoming, EntryStatus.NotEntered, nowPastEnd: false));
        }

        [Test]
        public void Row1_Upcoming_WithEntry_StillReturnsUpcoming()
        {
            // Upcoming state wins regardless of entry status
            Assert.AreEqual(TournamentCardState.Upcoming,
                Map(TournamentState.Upcoming, EntryStatus.InProgress, nowPastEnd: false));
        }

        // ── Row 2: entry InProgress + in window → EnteredActive ──────────────
        [Test]
        public void Row2_InProgress_Playing_InWindow_ReturnsEnteredActive()
        {
            Assert.AreEqual(TournamentCardState.EnteredActive,
                Map(TournamentState.Playing, EntryStatus.InProgress, nowPastEnd: false));
        }

        [Test]
        public void Row2_InProgress_Open_InWindow_ReturnsEnteredActive()
        {
            // Open state + InProgress entry (edge: entered during Open window)
            Assert.AreEqual(TournamentCardState.EnteredActive,
                Map(TournamentState.Open, EntryStatus.InProgress, nowPastEnd: false));
        }

        // ── Row 3: entry Finished (any state) → EnteredFinished ──────────────
        [Test]
        public void Row3_Finished_InWindow_ReturnsEnteredFinished()
        {
            Assert.AreEqual(TournamentCardState.EnteredFinished,
                Map(TournamentState.Open, EntryStatus.Finished, nowPastEnd: false));
        }

        [Test]
        public void Row3_Finished_Ended_ReturnsEnteredFinished()
        {
            Assert.AreEqual(TournamentCardState.EnteredFinished,
                Map(TournamentState.Ended, EntryStatus.Finished, nowPastEnd: true));
        }

        // ── Row 4: any entry + nowPastEnd → EnteredFinished ──────────────────
        [Test]
        public void Row4_InProgress_PastEnd_ReturnsEnteredFinished()
        {
            Assert.AreEqual(TournamentCardState.EnteredFinished,
                Map(TournamentState.Ended, EntryStatus.InProgress, nowPastEnd: true));
        }

        [Test]
        public void Row4_DNF_PastEnd_ReturnsEnteredFinished()
        {
            Assert.AreEqual(TournamentCardState.EnteredFinished,
                Map(TournamentState.Ended, EntryStatus.DNF, nowPastEnd: true));
        }

        // ── Row 5: no entry + Ending → Ending ────────────────────────────────
        [Test]
        public void Row5_NoEntry_Ending_ReturnsEnding()
        {
            Assert.AreEqual(TournamentCardState.Ending,
                Map(TournamentState.Ending, EntryStatus.NotEntered, nowPastEnd: false));
        }

        // ── Row 6: no entry + Open → Open ────────────────────────────────────
        [Test]
        public void Row6_NoEntry_Open_ReturnsOpen()
        {
            Assert.AreEqual(TournamentCardState.Open,
                Map(TournamentState.Open, EntryStatus.NotEntered, nowPastEnd: false));
        }

        // ── Row 7: no entry + nowPastEnd (Closed/Ended) → Ended ──────────────
        [Test]
        public void Row7_NoEntry_Closed_PastEnd_ReturnsEnded()
        {
            Assert.AreEqual(TournamentCardState.Ended,
                Map(TournamentState.Closed, EntryStatus.NotEntered, nowPastEnd: true));
        }

        [Test]
        public void Row7_NoEntry_Ended_PastEnd_ReturnsEnded()
        {
            Assert.AreEqual(TournamentCardState.Ended,
                Map(TournamentState.Ended, EntryStatus.NotEntered, nowPastEnd: true));
        }
    }
}
