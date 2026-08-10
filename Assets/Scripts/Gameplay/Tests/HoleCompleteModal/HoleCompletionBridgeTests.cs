using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;

namespace Golfin.HoleCompleteModal.Tests
{
    // ── Fake BallStateMachine provider ────────────────────────────────────────

    /// <summary>
    /// Minimal ISurfaceProvider for creating a headless BallStateMachine in tests.
    /// </summary>
    class ConstantSurfaceProvider : ISurfaceProvider
    {
        readonly SurfaceType _type;
        public ConstantSurfaceProvider(SurfaceType t = SurfaceType.Green) { _type = t; }
        public SurfaceType Classify(fp worldX, fp worldZ) => _type;
    }

    // ── Bridge test helper ────────────────────────────────────────────────────

    static class BridgeTestHelper
    {
        public static ShotResult MakeShotResult(BallState terminal)
            => new ShotResult(
                terminal,
                obReason:      null,
                startPosition: fp3.Zero,
                endPosition:   fp3.Zero,
                startSurface:  SurfaceType.Green,
                endSurface:    SurfaceType.Green,
                simDuration:   fp.Zero,
                bounceCount:   0);
    }

    // ── Test class ─────────────────────────────────────────────────────────────

    public class HoleCompletionBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            GameSession.ResetSession();
            HoleContext.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetSession();
            HoleContext.Reset();
        }

        // ── Test 8: InCup triggers MarkHoleComplete ───────────────────────────

        [Test]
        public void Bridge_InCupTriggersMarkHoleComplete()
        {
            // Arrange
            GameSession.SeedSession(1, "char_a", 0);
            HoleContext.Par = 5;  // Hole 1 par

            int fireCount = 0;
            HoleCompletionData? received = null;
            System.Action<HoleCompletionData> listener = d => { fireCount++; received = d; };
            GameSession.OnHoleComplete += listener;

            try
            {
                // Build a fake bridge backed by a manual ShotResult dispatch.
                // We test the logic by invoking HandleShot directly via reflection.
                var bridgeGO = new GameObject("Bridge");
                var bridge   = bridgeGO.AddComponent<HoleCompletionBridge>();
                bridge.ResetFiredFlag(); // ensure clean state

                // Invoke HandleShot via reflection.
                var handleShot = typeof(HoleCompletionBridge)
                    .GetMethod("HandleShot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(handleShot, "HandleShot method should exist on HoleCompletionBridge");

                var shotResult = BridgeTestHelper.MakeShotResult(BallState.InCup);
                handleShot.Invoke(bridge, new object[] { shotResult });

                // Assert
                Assert.AreEqual(1, fireCount,
                    "MarkHoleComplete must fire exactly once for InCup.");
                Assert.IsTrue(received.HasValue, "A payload must be received.");
                Assert.AreEqual(BallState.InCup, received.Value.TerminalState,
                    "Terminal state must be InCup.");

                Object.DestroyImmediate(bridgeGO);
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }

        // ── Test 9: Stroke cap triggers FAILED (opt-in only) ─────────────────

        /// <summary>
        /// The Missions path: a mode that explicitly opts in still gets the FAILED
        /// end-condition. Cesar 2026-08-10 — the machinery is preserved for Missions,
        /// only the always-on behaviour was removed.
        /// </summary>
        [Test]
        public void Bridge_StrokeCapTriggersFailed()
        {
            // Arrange
            GameSession.SeedSession(1, "char_a", 0);
            HoleContext.Par = 5;
            GameSession.StrokeCapEnabled = true;   // Missions opt-in
            int cap = HoleContext.Par + 5; // = 10
            GameSession.SetTurn(cap);

            int fireCount = 0;
            HoleCompletionData? received = null;
            System.Action<HoleCompletionData> listener = d => { fireCount++; received = d; };
            GameSession.OnHoleComplete += listener;

            try
            {
                var bridgeGO = new GameObject("Bridge");
                var bridge   = bridgeGO.AddComponent<HoleCompletionBridge>();
                bridge.ResetFiredFlag();

                var handleShot = typeof(HoleCompletionBridge)
                    .GetMethod("HandleShot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Fire AtRest at stroke cap.
                var shotResult = BridgeTestHelper.MakeShotResult(BallState.AtRest);
                handleShot.Invoke(bridge, new object[] { shotResult });

                Assert.AreEqual(1, fireCount,
                    "MarkHoleComplete must fire once when TurnCount >= par+5 and terminal=AtRest.");
                Assert.IsTrue(received.HasValue);
                Assert.AreEqual(BallState.AtRest, received.Value.TerminalState,
                    "Terminal state must be AtRest for FAILED proxy.");

                Object.DestroyImmediate(bridgeGO);
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }

        // ── Test 10: AtRest below cap does not fire ───────────────────────────

        [Test]
        public void Bridge_AtRestBelowCapNoFire()
        {
            // Arrange
            GameSession.SeedSession(1, "char_a", 0);
            HoleContext.Par = 5;
            int belowCap = HoleContext.Par + 4; // = 9, below cap of 10
            GameSession.SetTurn(belowCap);

            int fireCount = 0;
            System.Action<HoleCompletionData> listener = _ => fireCount++;
            GameSession.OnHoleComplete += listener;

            try
            {
                var bridgeGO = new GameObject("Bridge");
                var bridge   = bridgeGO.AddComponent<HoleCompletionBridge>();
                bridge.ResetFiredFlag();

                var handleShot = typeof(HoleCompletionBridge)
                    .GetMethod("HandleShot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Fire AtRest below cap — should be no-op.
                var shotResult = BridgeTestHelper.MakeShotResult(BallState.AtRest);
                handleShot.Invoke(bridge, new object[] { shotResult });

                Assert.AreEqual(0, fireCount,
                    "MarkHoleComplete must NOT fire when TurnCount < par+5 and terminal=AtRest.");

                Object.DestroyImmediate(bridgeGO);
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }

        // ── Test 11: no fail condition in any shipping mode ───────────────────

        /// <summary>
        /// Regression guard (Cesar 2026-08-10). Practice showed a FAILED screen on shot
        /// 10 of Hole 1 (par 5 + always-on cap of 5) even though no shipping mode has a
        /// fail condition. Well past the old cap, with StrokeCapEnabled at its default,
        /// the bridge must stay silent — InCup is the only end-condition.
        /// </summary>
        [Test]
        public void Bridge_StrokeCapDisabledByDefault_NoFireWellPastCap()
        {
            // Arrange — Practice session, no opt-in. Deliberately do NOT touch
            // GameSession.StrokeCapEnabled: the default is the thing under test.
            GameSession.SeedSession(1, "char_a", 0);
            HoleContext.Par = 5;
            Assert.IsFalse(GameSession.StrokeCapEnabled,
                "StrokeCapEnabled must default to false — no shipping mode has a fail condition.");

            GameSession.SetTurn(HoleContext.Par + 20); // 25 strokes: far beyond the old par+5 cap

            int fireCount = 0;
            System.Action<HoleCompletionData> listener = _ => fireCount++;
            GameSession.OnHoleComplete += listener;

            try
            {
                var bridgeGO = new GameObject("Bridge");
                var bridge   = bridgeGO.AddComponent<HoleCompletionBridge>();
                bridge.ResetFiredFlag();

                var handleShot = typeof(HoleCompletionBridge)
                    .GetMethod("HandleShot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var shotResult = BridgeTestHelper.MakeShotResult(BallState.AtRest);
                handleShot.Invoke(bridge, new object[] { shotResult });

                Assert.AreEqual(0, fireCount,
                    "No FAILED modal may fire in Practice/Tournament/1v1 — the stroke cap is opt-in.");

                // The cup still ends the hole normally.
                bridge.ResetFiredFlag();
                handleShot.Invoke(bridge,
                    new object[] { BridgeTestHelper.MakeShotResult(BallState.InCup) });
                Assert.AreEqual(1, fireCount,
                    "InCup must still complete the hole with the cap disabled.");

                Object.DestroyImmediate(bridgeGO);
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }

        // ── Test 12: Missions supplies its own cap magnitude ──────────────────

        /// <summary>
        /// GameSession.StrokeCapOverPar overrides the value serialized on the bridge, so
        /// Missions can ship a tighter (or looser) limit than the component default of 5
        /// without touching the scene.
        /// </summary>
        [Test]
        public void Bridge_SessionStrokeCapOverParOverridesSerializedDefault()
        {
            GameSession.SeedSession(1, "char_a", 0);
            HoleContext.Par = 5;
            GameSession.StrokeCapEnabled = true;
            GameSession.StrokeCapOverPar = 2;      // mission limit: par + 2 = 7

            int fireCount = 0;
            HoleCompletionData? received = null;
            System.Action<HoleCompletionData> listener = d => { fireCount++; received = d; };
            GameSession.OnHoleComplete += listener;

            try
            {
                var bridgeGO = new GameObject("Bridge");
                var bridge   = bridgeGO.AddComponent<HoleCompletionBridge>();
                bridge.ResetFiredFlag();

                var handleShot = typeof(HoleCompletionBridge)
                    .GetMethod("HandleShot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Stroke 6 — under the mission cap of 7, and under the serialized 10.
                GameSession.SetTurn(6);
                handleShot.Invoke(bridge, new object[] { BridgeTestHelper.MakeShotResult(BallState.AtRest) });
                Assert.AreEqual(0, fireCount, "Below the mission cap must not fire.");

                // Stroke 7 — at the mission cap, still below the serialized default of 10.
                GameSession.SetTurn(7);
                handleShot.Invoke(bridge, new object[] { BridgeTestHelper.MakeShotResult(BallState.AtRest) });
                Assert.AreEqual(1, fireCount,
                    "The session override (par+2) must win over the serialized default (par+5).");
                Assert.IsTrue(received.HasValue);
                Assert.AreEqual(BallState.AtRest, received.Value.TerminalState,
                    "Terminal state must be AtRest for the FAILED proxy.");

                Object.DestroyImmediate(bridgeGO);
            }
            finally
            {
                GameSession.OnHoleComplete -= listener;
            }
        }
    }
}
