using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2c — HoleSessionDriver + GameSession unit tests.
    ///
    /// ISOLATION NOTE: GameSession is a static class with global state. Every test MUST call
    /// GameSession.ResetForNewHole() in [SetUp] to avoid order-dependent failures.
    /// Tests MUST NOT rely on static event subscription count from prior tests.
    /// </summary>
    public class HoleSessionDriverTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset global state before each test. Also clears any dangling event subscribers
            // by re-creating the static-class fields (static events are cleared by unsubscribing
            // in TearDown, so SetUp just ensures a clean starting count/history).
            GameSession.ResetForNewHole();
        }

        [TearDown]
        public void TearDown()
        {
            // Unsubscribe all listeners attached during tests to avoid cross-test pollution.
            // We do this by replacing events with null — static events can't be mass-cleared
            // so we instead use local delegate variables that go out of scope.
            // (Tests subscribe via local delegates, so they're GC'd when the test ends.)
        }

        // ── Test 1: SetTurn fires OnTurnChanged and updates TurnCount ────────────

        [Test]
        public void GameSession_SetTurn_FiresOnTurnChanged()
        {
            int eventFiredCount = 0;
            System.Action listener = () => eventFiredCount++;
            GameSession.OnTurnChanged += listener;
            try
            {
                GameSession.SetTurn(5);

                Assert.AreEqual(5, GameSession.TurnCount,
                    "TurnCount should be updated to the value passed to SetTurn.");
                Assert.AreEqual(1, eventFiredCount,
                    "OnTurnChanged should fire exactly once when SetTurn is called.");
            }
            finally
            {
                GameSession.OnTurnChanged -= listener;
            }
        }

        // ── Test 2: RecordShot appends to ShotHistory and fires OnHistoryChanged ─

        [Test]
        public void GameSession_RecordShot_AppendsToHistoryAndFiresEvent()
        {
            int eventFiredCount = 0;
            System.Action listener = () => eventFiredCount++;
            GameSession.OnHistoryChanged += listener;
            try
            {
                Assert.AreEqual(0, GameSession.ShotHistory.Count,
                    "ShotHistory should start empty after SetUp reset.");

                var record = HoleSessionDriver.BuildShotRecordStatic(
                    shotNumber: 1,
                    clubLabel: "Driver",
                    origin: Vector3.zero,
                    finalPos: new Vector3(3f, 0f, 4f),
                    terminalState: "AtRest",
                    obReason: null,
                    finalSurface: "Fairway");

                GameSession.RecordShot(record);

                Assert.AreEqual(1, GameSession.ShotHistory.Count,
                    "ShotHistory.Count should be 1 after one RecordShot call.");
                Assert.AreEqual(1, eventFiredCount,
                    "OnHistoryChanged should fire exactly once when RecordShot is called.");
            }
            finally
            {
                GameSession.OnHistoryChanged -= listener;
            }
        }

        // ── Test 3: ResetForNewHole clears history, resets turn, fires both events ─

        [Test]
        public void GameSession_ResetForNewHole_ClearsHistoryAndResetsTurn()
        {
            // Populate some state first.
            GameSession.SetTurn(3);
            var record1 = HoleSessionDriver.BuildShotRecordStatic(1, "Driver", Vector3.zero,
                new Vector3(50f, 0f, 0f), "AtRest", null, "Fairway");
            var record2 = HoleSessionDriver.BuildShotRecordStatic(2, "Iron 7", new Vector3(50f, 0f, 0f),
                new Vector3(80f, 0f, 0f), "AtRest", null, "Green");
            GameSession.RecordShot(record1);
            GameSession.RecordShot(record2);

            Assert.AreEqual(3, GameSession.TurnCount, "Pre-condition: TurnCount should be 3.");
            Assert.AreEqual(2, GameSession.ShotHistory.Count, "Pre-condition: ShotHistory should have 2 entries.");

            int turnEventCount = 0;
            int historyEventCount = 0;
            System.Action turnListener    = () => turnEventCount++;
            System.Action historyListener = () => historyEventCount++;
            GameSession.OnTurnChanged    += turnListener;
            GameSession.OnHistoryChanged += historyListener;
            try
            {
                GameSession.ResetForNewHole();

                Assert.AreEqual(1, GameSession.TurnCount,
                    "TurnCount should be 1 after ResetForNewHole.");
                Assert.AreEqual(0, GameSession.ShotHistory.Count,
                    "ShotHistory should be empty after ResetForNewHole.");
                Assert.AreEqual(1, turnEventCount,
                    "OnTurnChanged should fire exactly once during ResetForNewHole.");
                Assert.AreEqual(1, historyEventCount,
                    "OnHistoryChanged should fire exactly once during ResetForNewHole.");
            }
            finally
            {
                GameSession.OnTurnChanged    -= turnListener;
                GameSession.OnHistoryChanged -= historyListener;
            }
        }

        // ── Test 4: ResetForNewHole fires events even when already in default state ─

        [Test]
        public void GameSession_ResetForNewHole_FiresEventsEvenWhenAlreadyDefault()
        {
            // SetUp already called ResetForNewHole, so we start in default state (TurnCount=1, history empty).
            // Calling ResetForNewHole again should STILL fire both events.
            Assert.AreEqual(1, GameSession.TurnCount, "Pre-condition: already default TurnCount=1.");
            Assert.AreEqual(0, GameSession.ShotHistory.Count, "Pre-condition: already empty history.");

            int turnEventCount = 0;
            int historyEventCount = 0;
            System.Action turnListener    = () => turnEventCount++;
            System.Action historyListener = () => historyEventCount++;
            GameSession.OnTurnChanged    += turnListener;
            GameSession.OnHistoryChanged += historyListener;
            try
            {
                GameSession.ResetForNewHole();

                Assert.AreEqual(1, turnEventCount,
                    "OnTurnChanged must fire even when TurnCount is already 1 — widget re-renders defensively.");
                Assert.AreEqual(1, historyEventCount,
                    "OnHistoryChanged must fire even when history is already empty.");
            }
            finally
            {
                GameSession.OnTurnChanged    -= turnListener;
                GameSession.OnHistoryChanged -= historyListener;
            }
        }

        // ── Test 5: BuildShotRecordStatic computes XZ distance correctly (3-4-5 triangle) ─

        [Test]
        public void ShotRecord_BuildStatic_ComputesXZDistanceCorrectly()
        {
            // 3-4-5 right triangle: dx=3, dz=4 → XZ distance = 5.
            var record = HoleSessionDriver.BuildShotRecordStatic(
                shotNumber: 1,
                clubLabel: "Driver",
                origin: Vector3.zero,
                finalPos: new Vector3(3f, 5f, 4f),   // Y=5 must NOT affect XZ distance
                terminalState: "AtRest",
                obReason: null,
                finalSurface: "Fairway");

            Assert.AreEqual(5.0f, record.DistanceXZMeters, 0.001f,
                "XZ distance for (0,0,0)→(3,5,4) should be 5.0 (3-4-5 right triangle, Y ignored).");
        }

        // ── Test 6: Y-axis difference does not affect XZ distance ────────────────

        [Test]
        public void ShotRecord_BuildStatic_HandlesYDifferenceWithoutAffectingXZDistance()
        {
            // Same XZ components but large Y difference — distance must still be 5.
            var record = HoleSessionDriver.BuildShotRecordStatic(
                shotNumber: 1,
                clubLabel: "Wedge",
                origin: Vector3.zero,
                finalPos: new Vector3(3f, 100f, 4f),
                terminalState: "AtRest",
                obReason: null,
                finalSurface: "Rough");

            Assert.AreEqual(5.0f, record.DistanceXZMeters, 0.001f,
                "XZ distance for (0,0,0)→(3,100,4) should still be 5.0 — Y delta is irrelevant.");
        }

        // ── Test 7: All 8 fields round-trip from constructor args to record ──────

        [Test]
        public void ShotRecord_BuildStatic_PreservesAllFields()
        {
            var origin   = new Vector3(10f, 2f, 5f);
            var finalPos = new Vector3(13f, 3f, 9f);   // dx=3, dz=4, distXZ=5
            const int    shotNumber    = 2;
            const string clubLabel     = "Iron 7";
            const string terminalState = "OB";
            const string obReason      = "Water";
            const string finalSurface  = "Water";

            var record = HoleSessionDriver.BuildShotRecordStatic(
                shotNumber, clubLabel, origin, finalPos, terminalState, obReason, finalSurface);

            Assert.AreEqual(shotNumber,   record.ShotNumber,   "ShotNumber must round-trip.");
            Assert.AreEqual(clubLabel,    record.ClubLabel,    "ClubLabel must round-trip.");
            Assert.AreEqual(origin,       record.OriginPosition, "OriginPosition must round-trip.");
            Assert.AreEqual(finalPos,     record.FinalPosition, "FinalPosition must round-trip.");
            Assert.AreEqual(5.0f,         record.DistanceXZMeters, 0.001f, "DistanceXZMeters computed correctly.");
            Assert.AreEqual(terminalState, record.TerminalState, "TerminalState must round-trip.");
            Assert.AreEqual(obReason,     record.OBReason,     "OBReason must round-trip.");
            Assert.AreEqual(finalSurface, record.FinalSurface, "FinalSurface must round-trip.");
        }
    }
}
