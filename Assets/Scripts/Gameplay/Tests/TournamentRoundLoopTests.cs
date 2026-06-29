// ─────────────────────────────────────────────────────────────────────────────
// T6 — tournament_round_loop EditMode tests (SPEC §12.2)
//
// Test coverage:
//  1. Stamina depletes per shot
//  2. Stamina carries hole→hole (BeginRound seeds once; no reset between holes)
//  3. Stamina resets on EndRound
//  4. Stamina gate (IsActive=false) → no depletion on solo path
//  5. GameSession.IsTournament / TournamentId cleared on ResetSession
//  6. FireTournamentHoleComplete fires the OnTournamentHoleComplete event
//  7. GameSession.ResetSession calls TournamentRoundContext.EndRound
//
// Deliberately no UnityEngine.UI or live-backend calls — pure state logic.
// Lives in Golfin.Gameplay.Tests (Editor-only asmdef).
// ─────────────────────────────────────────────────────────────────────────────
using NUnit.Framework;
using Golfin.Gameplay.Session;
using Golfin.Tournaments;

namespace Golfin.Gameplay.Tests
{
    [TestFixture]
    public class TournamentRoundLoopTests
    {
        // ── Helper: minimal CharacterSnapshot ────────────────────────────────

        private static CharacterSnapshot MakeSnapshot(string charId = "char_frodo")
        {
            return new CharacterSnapshot(
                characterId:  charId,
                level:        100,
                strength:     40,
                clubControl:  35,
                recovery:     30,
                stamina:      25
            );
        }

        [SetUp]
        public void SetUp()
        {
            // Always start from a clean slate — static state pollutes cross-test.
            TournamentRoundContext.EndRound();
            GameSession.ResetSession();
        }

        [TearDown]
        public void TearDown()
        {
            TournamentRoundContext.EndRound();
            GameSession.ResetSession();
        }

        // ── Test 1: Stamina depletes per shot ─────────────────────────────────

        [Test]
        public void DepleteStamina_ReducesRemainingByOneCostPerCall()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 10f);

            float before = TournamentRoundContext.StaminaEnergyRemaining;
            TournamentRoundContext.DepleteStamina();

            float after = TournamentRoundContext.StaminaEnergyRemaining;
            Assert.AreEqual(before - 10f, after, 0.001f,
                "DepleteStamina must subtract StaminaCostPerShot from StaminaEnergyRemaining.");
        }

        // ── Test 2: Stamina carries hole→hole (no reset between holes) ────────

        [Test]
        public void DepleteStamina_CarriesAcrossHoles_WhenBeginRoundNotCalled()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 5f);

            // Simulate 3 shots on hole 1 → remaining = 85
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();
            float afterHole1 = TournamentRoundContext.StaminaEnergyRemaining;
            Assert.AreEqual(85f, afterHole1, 0.001f,
                "After 3 shots at cost 5, remaining should be 85.");

            // Simulate starting hole 2 — for v1 we do NOT call BeginRound again.
            // Stamina must still carry the depletion.
            Assert.IsTrue(TournamentRoundContext.IsActive,
                "IsActive must stay true between holes — EndRound not called yet.");
            Assert.AreEqual(85f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must NOT reset between holes (only resets on EndRound).");

            // 2 more shots on hole 2 → remaining = 75
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();
            Assert.AreEqual(75f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "After 2 more shots, remaining should be 75.");
        }

        // ── Test 3: Stamina resets on EndRound ───────────────────────────────

        [Test]
        public void EndRound_ResetsStaminaAndClearsIsActive()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 10f);
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();

            Assert.IsTrue(TournamentRoundContext.IsActive, "Pre: should be active.");
            Assert.Less(TournamentRoundContext.StaminaEnergyRemaining,
                        TournamentRoundContext.DefaultStaminaMax,
                        "Pre: stamina should have been depleted.");

            TournamentRoundContext.EndRound();

            Assert.IsFalse(TournamentRoundContext.IsActive,
                "IsActive must be false after EndRound.");
            Assert.AreEqual(TournamentRoundContext.DefaultStaminaMax,
                            TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must reset to DefaultStaminaMax after EndRound.");
        }

        // ── Test 4: Stamina not depleted when IsActive = false (solo path) ────

        [Test]
        public void DepleteStamina_IsNoop_WhenIsActiveFalse()
        {
            // No BeginRound call → IsActive = false.
            Assert.IsFalse(TournamentRoundContext.IsActive, "Pre: should NOT be active.");

            float expected = TournamentRoundContext.StaminaEnergyRemaining;
            TournamentRoundContext.DepleteStamina();  // should be a no-op

            Assert.AreEqual(expected, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "DepleteStamina must be a no-op when IsActive == false (solo path).");
        }

        // ── Test 5: GameSession.IsTournament / TournamentId cleared on ResetSession ──

        [Test]
        public void ResetSession_ClearsTournamentFlags()
        {
            // Arrange: set tournament state
            GameSession.IsTournament = true;
            GameSession.TournamentId = "t1";

            // Act
            GameSession.ResetSession();

            // Assert
            Assert.IsFalse(GameSession.IsTournament,
                "IsTournament must be false after ResetSession.");
            Assert.IsNull(GameSession.TournamentId,
                "TournamentId must be null after ResetSession.");
        }

        // ── Test 6: FireTournamentHoleComplete fires the event ────────────────

        [Test]
        public void FireTournamentHoleComplete_FiresEventWithCorrectArgs()
        {
            int receivedHole    = -1;
            int receivedStrokes = -1;
            int fireCount       = 0;

            System.Action<int, int> listener = (h, s) =>
            {
                receivedHole    = h;
                receivedStrokes = s;
                fireCount++;
            };

            GameSession.OnTournamentHoleComplete += listener;
            try
            {
                GameSession.FireTournamentHoleComplete(3, 12);

                Assert.AreEqual(1,  fireCount,       "Event must fire exactly once.");
                Assert.AreEqual(3,  receivedHole,    "holeNumber arg must be forwarded.");
                Assert.AreEqual(12, receivedStrokes, "totalStrokes arg must be forwarded.");
            }
            finally
            {
                GameSession.OnTournamentHoleComplete -= listener;
            }
        }

        // ── Test 7: ResetSession calls TournamentRoundContext.EndRound ─────────

        [Test]
        public void ResetSession_CallsEndRound()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 5f);
            TournamentRoundContext.DepleteStamina();

            Assert.IsTrue(TournamentRoundContext.IsActive, "Pre: must be active.");

            // ResetSession should internally call EndRound.
            GameSession.ResetSession();

            Assert.IsFalse(TournamentRoundContext.IsActive,
                "ResetSession must call TournamentRoundContext.EndRound, clearing IsActive.");
        }

        // ── Test 8: Stamina floored at 0, does not go negative ────────────────

        [Test]
        public void DepleteStamina_FlooredAtZero()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 60f);

            // Two depletions of 60 each — total would be -20, but floored at 0.
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();

            Assert.AreEqual(0f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must be floored at 0, never negative.");
        }

        // ── Test 9: BeginRound seeds stamina at max (not carrying prior state) ──

        [Test]
        public void BeginRound_SeedsStaminaAtMax()
        {
            var snap = MakeSnapshot();

            // First round with some depletion
            TournamentRoundContext.BeginRound("t1", snap, staminaCostPerShot: 20f);
            TournamentRoundContext.DepleteStamina();
            TournamentRoundContext.DepleteStamina();
            Assert.AreEqual(60f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "Pre: 2 shots at cost 20 → 60 remaining.");

            TournamentRoundContext.EndRound();

            // New round: stamina MUST restart at max.
            TournamentRoundContext.BeginRound("t2", snap, staminaCostPerShot: 10f);
            Assert.AreEqual(TournamentRoundContext.DefaultStaminaMax,
                            TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "BeginRound must seed StaminaEnergyRemaining at DefaultStaminaMax.");
        }
    }
}
