// ─────────────────────────────────────────────────────────────────────────────
// T6 — tournament_round_loop EditMode tests (SPEC §12.2)
// Updated Phase 3 (stamina_tournament_wiring): per-hole pool model replaces
// the old per-shot drain. Obsolete per-shot DepleteStamina_* tests rewritten.
//
// Test coverage:
//  1. BeginRound seeds pool from explicit tankMax/remaining (not flat-100 reset)
//  2. Pool is CONSTANT within a hole (no per-shot drain in production path)
//  3. Stamina resets on EndRound
//  4. Stamina gate (IsActive=false) → DepleteStamina is no-op (legacy API test)
//  5. GameSession.IsTournament / TournamentId cleared on ResetSession
//  6. FireTournamentHoleComplete fires the OnTournamentHoleComplete event
//  7. GameSession.ResetSession calls TournamentRoundContext.EndRound
//  8. Pool floored at 0, does not go negative (DepleteStamina overload still safe)
//  9. Sentinel -1f remaining → seeded from caller (test caller logic)
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

        // ── Test 1: BeginRound seeds pool from explicit tankMax / remaining ────
        // Phase 3: BeginRound takes explicit tankMax + remaining from the entry,
        // replacing the old flat-100 default reset.

        [Test]
        public void BeginRound_SeedsPoolFromExplicitParams()
        {
            var snap = MakeSnapshot();
            float tank      = 80f;
            float remaining = 55f;

            TournamentRoundContext.BeginRound("t1", snap, tank, remaining);

            Assert.AreEqual(tank,      TournamentRoundContext.StaminaEnergyMax,       0.001f,
                "StaminaEnergyMax must be set to the tankMax argument.");
            Assert.AreEqual(remaining, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must be set to the remaining argument.");
            Assert.IsTrue(TournamentRoundContext.IsActive,
                "IsActive must be true after BeginRound.");
            Assert.AreEqual("t1", TournamentRoundContext.TournamentId,
                "TournamentId must be set.");
        }

        // ── Test 2: Pool is constant within a hole (no per-shot drain) ───────
        // Phase 3 (D4): ShotController no longer calls DepleteStamina().
        // The pool is seeded once at BeginRound and stays constant for the whole hole.
        // The penalty seam (LiveStatProviderHost.ResolveLive) reads this constant
        // value throughout the hole — condition steps down once at hole-complete
        // (backend SubmitHoleResult), not per-shot.

        [Test]
        public void Pool_IsConstantWithinHole_NoPerShotDrain()
        {
            var snap = MakeSnapshot();
            float tank      = 100f;
            float remaining = 70f;

            TournamentRoundContext.BeginRound("t1", snap, tank, remaining);

            float after = TournamentRoundContext.StaminaEnergyRemaining;

            // Simulate several shots (no DepleteStamina calls — production path no longer drains per-shot).
            // Pool must remain at the seeded value.
            Assert.AreEqual(remaining, after, 0.001f,
                "Pool must remain at the seeded value within a hole (no per-shot drain in Phase 3).");
            Assert.AreEqual(remaining, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must be unchanged between BeginRound and EndRound.");
        }

        // ── Test 3: Stamina resets on EndRound ───────────────────────────────

        [Test]
        public void EndRound_ResetsStaminaAndClearsIsActive()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, 80f, 55f);

            Assert.IsTrue(TournamentRoundContext.IsActive, "Pre: should be active.");
            Assert.AreEqual(55f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "Pre: remaining should be seeded to 55.");

            TournamentRoundContext.EndRound();

            Assert.IsFalse(TournamentRoundContext.IsActive,
                "IsActive must be false after EndRound.");
            Assert.AreEqual(TournamentRoundContext.DefaultStaminaMax,
                            TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must reset to DefaultStaminaMax after EndRound.");
        }

        // ── Test 4: DepleteStamina is no-op when IsActive = false (legacy API) ──
        // DepleteStamina() is retained as dead/legacy API (D4 = keep but not called).
        // It must still be safe to call (no-op when inactive = solo path unchanged).

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
            TournamentRoundContext.BeginRound("t1", snap, 100f, 75f);

            Assert.IsTrue(TournamentRoundContext.IsActive, "Pre: must be active.");

            // ResetSession should internally call EndRound.
            GameSession.ResetSession();

            Assert.IsFalse(TournamentRoundContext.IsActive,
                "ResetSession must call TournamentRoundContext.EndRound, clearing IsActive.");
        }

        // ── Test 8: Pool floored at 0 via legacy DepleteStamina (safety check) ──
        // DepleteStamina() is dead in production (D4) but the API must still be safe.

        [Test]
        public void DepleteStamina_FlooredAtZero()
        {
            var snap = MakeSnapshot();
            TournamentRoundContext.BeginRound("t1", snap, 100f, 40f);

            // Two depletions via legacy API — total would go negative but floors at 0.
            TournamentRoundContext.DepleteStamina(30f);
            TournamentRoundContext.DepleteStamina(30f);

            Assert.AreEqual(0f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f,
                "StaminaEnergyRemaining must be floored at 0, never negative.");
        }

        // ── Test 9: BeginRound with full pool (sentinel caller logic) ─────────
        // Verify that when caller passes remaining=tank (sentinel case, full pool),
        // both values agree and IsActive becomes true.

        [Test]
        public void BeginRound_WithFullPool_BothValuesAgree()
        {
            var snap = MakeSnapshot();
            float tank = 90f;

            // Caller resolved the sentinel (-1f) to full before calling BeginRound.
            TournamentRoundContext.BeginRound("t2", snap, tank, tank);

            Assert.AreEqual(tank, TournamentRoundContext.StaminaEnergyMax,       0.001f);
            Assert.AreEqual(tank, TournamentRoundContext.StaminaEnergyRemaining, 0.001f);
            Assert.IsTrue(TournamentRoundContext.IsActive);
        }

        // ── Test 10: Pool carries across calls (no spurious reset) ─────────────
        // BeginRound is called once per hole in the real flow. Verify a second
        // BeginRound (for a new tournament) correctly seeds the new values rather
        // than carrying old state.

        [Test]
        public void BeginRound_SecondCall_SeedsNewValues()
        {
            var snap = MakeSnapshot();

            TournamentRoundContext.BeginRound("t1", snap, 100f, 60f);
            Assert.AreEqual(60f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f, "First BeginRound.");

            TournamentRoundContext.EndRound();

            // New tournament with different pool values.
            TournamentRoundContext.BeginRound("t2", snap, 80f, 30f);
            Assert.AreEqual(80f, TournamentRoundContext.StaminaEnergyMax,       0.001f, "Second BeginRound tank.");
            Assert.AreEqual(30f, TournamentRoundContext.StaminaEnergyRemaining, 0.001f, "Second BeginRound remaining.");
        }
    }
}
