using NUnit.Framework;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Stage B — GameSession unit tests (5 of 6 new tests; the 6th lives next
    /// to HoleCompleteDriverTests in the Physics.Tests assembly because it
    /// exercises the driver integration too).
    ///
    /// ISOLATION NOTE: GameSession is a static class with global state. Every
    /// test MUST call ResetSession() in [SetUp] so seed fields AND per-hole
    /// fields start from a known baseline; otherwise tests are order-dependent.
    /// </summary>
    public class GameSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            // Full reset including seed fields — defends against cross-test pollution.
            GameSession.ResetSession();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetSession();
        }

        // ── Test 1: SeedSession sets all three seed fields ───────────────────

        [Test]
        public void SeedSession_SetsAllThreeFields()
        {
            GameSession.SeedSession(5, "char_iron7", 2);

            Assert.AreEqual(5,           GameSession.CurrentHoleNumber,
                "CurrentHoleNumber must be set to the holeNumber arg.");
            Assert.AreEqual("char_iron7", GameSession.SelectedCharacterId,
                "SelectedCharacterId must be set to the characterId arg.");
            Assert.AreEqual(2,           GameSession.EquippedBagSlot,
                "EquippedBagSlot must be set to the bagSlot arg.");
        }

        // ── Test 2: ResetForNewHole preserves seed fields ─────────────────────

        [Test]
        public void ResetForNewHole_PreservesSeedFields()
        {
            // Arrange: seed + mutate per-hole state.
            GameSession.SeedSession(7, "char_putter", 3);
            GameSession.SetTurn(4);
            GameSession.RecordShot(new ShotRecord(
                shotNumber:       1,
                clubLabel:        "Driver",
                originPosition:   UnityEngine.Vector3.zero,
                finalPosition:    new UnityEngine.Vector3(100f, 0f, 0f),
                distanceXZMeters: 100f,
                terminalState:    "AtRest",
                obReason:         null,
                finalSurface:     "Fairway"
            ));
            Assert.AreEqual(4, GameSession.TurnCount,    "Pre-condition: turn=4.");
            Assert.AreEqual(1, GameSession.ShotHistory.Count, "Pre-condition: 1 shot recorded.");

            // Act
            GameSession.ResetForNewHole();

            // Assert: per-hole cleared, seed preserved.
            Assert.AreEqual(1,            GameSession.TurnCount,
                "TurnCount must reset to 1.");
            Assert.AreEqual(0,            GameSession.ShotHistory.Count,
                "ShotHistory must be cleared.");
            Assert.AreEqual(7,            GameSession.CurrentHoleNumber,
                "CurrentHoleNumber must be preserved across ResetForNewHole.");
            Assert.AreEqual("char_putter", GameSession.SelectedCharacterId,
                "SelectedCharacterId must be preserved across ResetForNewHole.");
            Assert.AreEqual(3,            GameSession.EquippedBagSlot,
                "EquippedBagSlot must be preserved across ResetForNewHole.");
        }

        // ── Test 3: ResetSession clears all seed fields ──────────────────────

        [Test]
        public void ResetSession_ClearsAllSeedFields()
        {
            // Arrange
            GameSession.SeedSession(9, "char_driver", 4);

            // Act
            GameSession.ResetSession();

            // Assert
            Assert.AreEqual(0,             GameSession.CurrentHoleNumber,
                "CurrentHoleNumber must be cleared to 0.");
            Assert.AreEqual(string.Empty,  GameSession.SelectedCharacterId,
                "SelectedCharacterId must be cleared to empty string.");
            Assert.AreEqual(0,             GameSession.EquippedBagSlot,
                "EquippedBagSlot must be cleared to 0.");
            Assert.AreEqual(1,             GameSession.TurnCount,
                "ResetSession should also reset per-hole TurnCount via ResetForNewHole.");
            Assert.AreEqual(0,             GameSession.ShotHistory.Count,
                "ResetSession should also clear ShotHistory via ResetForNewHole.");
        }

        // ── Test 4: SetCurrentHole updates pointer without clearing seed ─────

        [Test]
        public void SetCurrentHole_UpdatesPointerWithoutClearingSeed()
        {
            // Arrange
            GameSession.SeedSession(5, "char_a", 2);
            GameSession.SetTurn(3); // dirty per-hole state

            // Act: PLAY NEXT — point at hole 6, reset per-hole, keep seed.
            GameSession.SetCurrentHole(6);

            // Assert
            Assert.AreEqual(6,        GameSession.CurrentHoleNumber,
                "CurrentHoleNumber should be updated to the new hole.");
            Assert.AreEqual("char_a", GameSession.SelectedCharacterId,
                "SelectedCharacterId must be preserved across SetCurrentHole.");
            Assert.AreEqual(2,        GameSession.EquippedBagSlot,
                "EquippedBagSlot must be preserved across SetCurrentHole.");
            Assert.AreEqual(1,        GameSession.TurnCount,
                "TurnCount should reset to 1 via ResetForNewHole().");
        }

        // ── Test 5: OnHoleComplete fires with correct payload ────────────────

        [Test]
        public void OnHoleComplete_FiresOnMarkHoleComplete_WithCorrectPayload()
        {
            // Arrange
            HoleCompletionData? received = null;
            int fireCount = 0;
            System.Action<HoleCompletionData> listener = d =>
            {
                received = d;
                fireCount++;
            };
            GameSession.OnHoleComplete += listener;

            try
            {
                var payload = new HoleCompletionData(
                    terminalState:  BallState.InCup,
                    strokes:        4,
                    penaltyStrokes: 1,
                    holeNumber:     7
                );

                // Act
                GameSession.MarkHoleComplete(payload);

                // Assert
                Assert.AreEqual(1, fireCount,
                    "OnHoleComplete must fire exactly once per MarkHoleComplete call.");
                Assert.IsTrue(received.HasValue,
                    "Subscriber should have received the payload.");
                Assert.AreEqual(BallState.InCup, received.Value.TerminalState,
                    "Received TerminalState must match.");
                Assert.AreEqual(4, received.Value.Strokes,
                    "Received Strokes must match.");
                Assert.AreEqual(1, received.Value.PenaltyStrokes,
                    "Received PenaltyStrokes must match.");
                Assert.AreEqual(7, received.Value.HoleNumber,
                    "Received HoleNumber must match.");
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }
    }
}
