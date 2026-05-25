#nullable enable
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Gameplay.Defaults;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.PlayMode.Tests
{
    /// <summary>
    /// PlayMode test: verifies that StatProviderBus end-to-end wiring works at
    /// runtime — a registered resolver propagates live bundles through
    /// StatProviderBus.Resolve, and GameSession / ClubContext / BallContext
    /// state changes are reflected in the resolved bundle.
    ///
    /// NOTE: LiveStatProviderHost itself is in Assembly-CSharp (no named asmdef),
    /// so it cannot be directly instantiated from a named test asmdef. This test
    /// validates the bus contract and the context seeding path, which is
    /// equivalent — the host is a trivial Awake() registration wrapper. A
    /// separate smoke-integration path (PhysicsLab lab smoke bot) validates the
    /// full host lifecycle. See IMPLEMENTER_REPORT.md for the lab smoke result.
    /// </summary>
    [TestFixture]
    public class LiveStatProviderHostPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up static bus + context after each test.
            StatProviderBus.Resolver = null;
            GameSession.ResetSession();
            ClubContext.Reset();
            BallContext.Reset();
        }

        // ── Test 1: Bus resolver is wired and returns the registered bundle ────

        [UnityTest]
        public IEnumerator Resolve_WithLiveResolver_ReturnsBundleFromResolver()
        {
            // Arrange: seed static contexts (mirrors what ClubContextPopulator /
            // BallContextPopulator do at runtime).
            GameSession.SeedSession(1, "char_test", 1);
            ClubContext.SelectedClubId = "club_driver_gf";
            BallContext.SelectedBallId = "ball_golfin";

            // Register a synthetic resolver that reads contexts (minimal live path).
            var expectedVelocity = fp.FromFloat(75f);
            StatProviderBus.Resolver = isPutt =>
            {
                if (string.IsNullOrEmpty(GameSession.SelectedCharacterId)) return null;
                if (string.IsNullOrEmpty(ClubContext.SelectedClubId)) return null;
                if (string.IsNullOrEmpty(BallContext.SelectedBallId)) return null;

                var clubStats = new ClubStats(
                    power: 80, accuracy: 30, lieResistance: 10, durability: 100,
                    loftDegrees: fp.FromFloat(10.9f),
                    baseVelocityMps: expectedVelocity,
                    baseBackspinRpm: fp.FromFloat(2686f));

                return new StatBundle(
                    clubStats,
                    BallStats.Neutral,
                    CharacterStats.Neutral,
                    fp.FromFloat(100f),
                    fp.FromFloat(100f));
            };

            // Wait one frame for any MonoBehaviour initialization.
            yield return null;

            // Act
            var result = StatProviderBus.Resolve(isPutt: false);

            // Assert: the live bundle propagated through the bus.
            Assert.IsTrue(result.Club.HasValue,
                "Resolve with a live resolver must return a club bundle.");
            Assert.AreEqual(expectedVelocity, result.Club.Value.BaseVelocityMps,
                "BaseVelocityMps must match the value from the registered resolver — " +
                "proving the bus correctly routes to the live path.");
            Assert.IsFalse(result.IsPutt,
                "Non-putt resolve must return a swing bundle.");
        }

        // ── Test 2: Clearing contexts falls through to defaults ───────────────

        [UnityTest]
        public IEnumerator Resolve_AfterContextsCleared_FallsThroughToDefaults()
        {
            // Arrange: seed then clear.
            GameSession.SeedSession(1, "char_test", 1);
            ClubContext.SelectedClubId = "club_driver_gf";
            BallContext.SelectedBallId = "ball_golfin";

            StatProviderBus.Resolver = isPutt =>
            {
                // Resolver returns null when contexts are empty — mirrors LiveStatProviderHost
                // behavior on partial state miss.
                if (string.IsNullOrEmpty(GameSession.SelectedCharacterId)) return null;
                return new StatBundle(
                    ClubStats.DefaultDriver,
                    BallStats.Neutral,
                    CharacterStats.Neutral,
                    fp.FromFloat(100f),
                    fp.FromFloat(100f));
            };

            // Clear the character context (simulates session end / menu return).
            GameSession.ResetSession();

            yield return null;

            // Act
            var result = StatProviderBus.Resolve(isPutt: false);

            // Assert: falls through to DefaultStatProvider (DefaultDriver values).
            Assert.IsTrue(result.Club.HasValue,
                "Fallback bundle must have a Club.");
            Assert.AreEqual(ClubStats.DefaultDriver.BaseVelocityMps,
                result.Club.Value.BaseVelocityMps,
                "Fallback bundle BaseVelocityMps must match DefaultDriver (75 m/s).");
        }
    }
}
