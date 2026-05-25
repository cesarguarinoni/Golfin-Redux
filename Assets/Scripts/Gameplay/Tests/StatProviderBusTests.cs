using NUnit.Framework;
using Golfin.Gameplay.Defaults;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode unit tests for StatProviderBus — the static cross-asmdef bus
    /// introduced by live_stat_provider_wiring.
    ///
    /// ISOLATION: StatProviderBus.Resolver is a static field; every test clears it
    /// in [TearDown] to avoid cross-test pollution.
    /// </summary>
    [TestFixture]
    public class StatProviderBusTests
    {
        [TearDown]
        public void TearDown()
        {
            // Always clear the resolver so other tests start clean.
            StatProviderBus.Resolver = null;
        }

        // ── Test 1: No resolver registered → default swing bundle ───────────

        [Test]
        public void Resolve_WithNoResolverRegistered_ReturnsDefaultSwingBundle()
        {
            // Arrange: clear resolver (already done by TearDown, but be explicit).
            StatProviderBus.Resolver = null;

            // Act
            StatBundle result = StatProviderBus.Resolve(isPutt: false);

            // Assert: result is a swing bundle (Club not null, Putter null).
            Assert.IsFalse(result.IsPutt,
                "With no resolver, Resolve(false) must return a swing bundle.");
            Assert.IsTrue(result.Club.HasValue,
                "Default swing bundle must have a Club.");
            Assert.IsFalse(result.Putter.HasValue,
                "Default swing bundle must NOT have a Putter.");
        }

        [Test]
        public void Resolve_WithNoResolverRegistered_ReturnsDefaultPuttBundle()
        {
            // Arrange: clear resolver.
            StatProviderBus.Resolver = null;

            // Act
            StatBundle result = StatProviderBus.Resolve(isPutt: true);

            // Assert: result is a putt bundle (Putter not null, Club null).
            Assert.IsTrue(result.IsPutt,
                "With no resolver, Resolve(true) must return a putt bundle.");
            Assert.IsTrue(result.Putter.HasValue,
                "Default putt bundle must have a Putter.");
            Assert.IsFalse(result.Club.HasValue,
                "Default putt bundle must NOT have a Club.");
        }

        // ── Test 2: Resolver returning null falls through to default ─────────

        [Test]
        public void Resolve_WithResolverReturningNull_FallsThroughToDefault()
        {
            // Arrange: register a resolver that always returns null (partial-state path).
            StatProviderBus.Resolver = isPutt => null;

            // Act
            StatBundle swingResult = StatProviderBus.Resolve(isPutt: false);
            StatBundle puttResult  = StatProviderBus.Resolve(isPutt: true);

            // Assert: both fall through to defaults.
            Assert.IsFalse(swingResult.IsPutt,
                "Null-returning resolver swing path must fall through to default swing bundle.");
            Assert.IsTrue(swingResult.Club.HasValue,
                "Null-resolver fallback swing bundle must have a Club.");

            Assert.IsTrue(puttResult.IsPutt,
                "Null-returning resolver putt path must fall through to default putt bundle.");
            Assert.IsTrue(puttResult.Putter.HasValue,
                "Null-resolver fallback putt bundle must have a Putter.");
        }

        // ── Test 3: Resolver returning a bundle propagates the live bundle ────

        [Test]
        public void Resolve_WithResolverReturningBundle_ReturnsLiveBundle()
        {
            // Arrange: build a recognizable live bundle with non-default values.
            var expectedClubStats = new ClubStats(
                power:           77,
                accuracy:        88,
                lieResistance:   44,
                durability:      55,
                loftDegrees:     fp.FromFloat(12.5f),
                baseVelocityMps: fp.FromFloat(68f),
                baseBackspinRpm: fp.FromFloat(4500f));

            var expectedBundle = new StatBundle(
                expectedClubStats,
                BallStats.Neutral,
                CharacterStats.Neutral,
                fp.FromFloat(80f),
                fp.FromFloat(100f));

            StatProviderBus.Resolver = isPutt => expectedBundle;

            // Act
            StatBundle result = StatProviderBus.Resolve(isPutt: false);

            // Assert: the live bundle is returned verbatim.
            Assert.IsTrue(result.Club.HasValue,
                "Live bundle must have a Club.");
            Assert.AreEqual(77, result.Club.Value.Power,
                "Live bundle Power must match the registered resolver's return value.");
            Assert.AreEqual(88, result.Club.Value.Accuracy,
                "Live bundle Accuracy must match the registered resolver's return value.");
            Assert.IsFalse(result.IsPutt,
                "Live swing bundle must not be a putt.");
        }
    }
}
