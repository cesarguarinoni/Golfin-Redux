using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Session;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2e — Next-shot handoff unit tests.
    /// Tests cover: ShotRecord constructor overloads, OBDropResolver, AimRotationHelper,
    /// and HoleSessionDriver.ComputeNextTurn. All tests are static-helper / value-type only —
    /// no scene required, no GameSession static-state mutation.
    /// </summary>
    public class NextShotHandoffTests
    {
        // ── Test 1: ShotRecord 8-arg ctor defaults PenaltyStrokes to 0 ────────────

        [Test]
        public void ShotRecord_EightArgCtor_DefaultsPenaltyStrokesToZero()
        {
            var record = new ShotRecord(
                1, "Driver",
                Vector3.zero, new Vector3(50f, 0f, 0f),
                50f,
                "AtRest", null, "Fairway");

            Assert.AreEqual(0, record.PenaltyStrokes,
                "8-arg ShotRecord ctor must default PenaltyStrokes to 0.");
        }

        // ── Test 2: ShotRecord 9-arg ctor sets PenaltyStrokes and round-trips all fields ──

        [Test]
        public void ShotRecord_NineArgCtor_SetsPenaltyStrokes()
        {
            var origin   = new Vector3(5f, 0f, 5f);
            var finalPos = new Vector3(15f, 0f, 5f);
            var record = new ShotRecord(
                2, "Iron 7",
                origin, finalPos,
                10f,
                "OB", "Water", "Water",
                1);

            Assert.AreEqual(1,        record.PenaltyStrokes, "PenaltyStrokes should be 1.");
            Assert.AreEqual(2,        record.ShotNumber,     "ShotNumber must round-trip.");
            Assert.AreEqual("Iron 7", record.ClubLabel,      "ClubLabel must round-trip.");
            Assert.AreEqual(origin,   record.OriginPosition, "OriginPosition must round-trip.");
            Assert.AreEqual(finalPos, record.FinalPosition,  "FinalPosition must round-trip.");
            Assert.AreEqual(10f,      record.DistanceXZMeters, 0.001f, "DistanceXZMeters must round-trip.");
            Assert.AreEqual("OB",     record.TerminalState,  "TerminalState must round-trip.");
            Assert.AreEqual("Water",  record.OBReason,       "OBReason must round-trip.");
            Assert.AreEqual("Water",  record.FinalSurface,   "FinalSurface must round-trip.");
        }

        // ── Test 3: OBDropResolver finds last safe fairway hit before water ──────

        [Test]
        public void OBDropResolver_FindsLastSafeFairwayHitBeforeWater()
        {
            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(10f, 0f, 0f), SurfaceType.Fairway),
                MakeHit(new Vector3(20f, 0f, 0f), SurfaceType.Fairway),
                MakeHit(new Vector3(30f, 0f, 0f), SurfaceType.Water),
            };
            var trajectory = MakeTrajectory(hits);
            var fallback   = new Vector3(0f, 0f, 0f);

            Vector3 result = OBDropResolver.Resolve(trajectory, fallback);

            Assert.AreEqual(new Vector3(20f, 0f, 0f), result,
                "Drop should be at the last fairway hit before water (20,0,0).");
        }

        // ── Test 4: OBDropResolver skips Water and OOB hits ──────────────────────

        [Test]
        public void OBDropResolver_SkipsWaterAndOOBHits()
        {
            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(5f, 0f, 0f),  SurfaceType.Rough),
                MakeHit(new Vector3(15f, 0f, 0f), SurfaceType.Water),
                MakeHit(new Vector3(25f, 0f, 0f), SurfaceType.OOB),
            };
            var trajectory = MakeTrajectory(hits);
            var fallback   = new Vector3(0f, 0f, 0f);

            Vector3 result = OBDropResolver.Resolve(trajectory, fallback);

            Assert.AreEqual(new Vector3(5f, 0f, 0f), result,
                "Drop should skip OOB and Water, landing on Rough hit at (5,0,0).");
        }

        // ── Test 5: OBDropResolver falls back to origin when no safe hit ─────────

        [Test]
        public void OBDropResolver_FallsBackToOriginWhenNoSafeHit()
        {
            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(10f, 0f, 0f), SurfaceType.Water),
                MakeHit(new Vector3(20f, 0f, 0f), SurfaceType.Water),
            };
            var trajectory = MakeTrajectory(hits);
            var fallback   = new Vector3(0f, 0f, 0f);

            Vector3 result = OBDropResolver.Resolve(trajectory, fallback);

            Assert.AreEqual(new Vector3(0f, 0f, 0f), result,
                "Drop should fall back to origin when all hits are Water.");
        }

        // ── Test 6: OBDropResolver handles null trajectory ────────────────────────

        [Test]
        public void OBDropResolver_NullTrajectoryReturnsOrigin()
        {
            var fallback = new Vector3(5f, 2f, 7f);

            Vector3 result = OBDropResolver.Resolve(null, fallback);

            Assert.AreEqual(fallback, result,
                "Resolve(null, fallback) should return fallback unchanged.");
        }

        // ── Test 6b: K10 boundary OB is stroke-and-distance (drop at previous origin) ──

        [Test]
        public void OBDropResolver_ResolveByRule_BoundaryDropsAtPreviousOrigin()
        {
            // A boundary OB (isWater=false) must ignore any safe terrain hits and drop at
            // the previous shot origin — stroke and distance. First-shot boundary OB → tee.
            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(20f, 0f, 0f), SurfaceType.Fairway),
                MakeHit(new Vector3(40f, 0f, 0f), SurfaceType.OOB),
            };
            var trajectory = MakeTrajectory(hits);
            var tee        = new Vector3(3f, 1f, 7f);

            Vector3 result = OBDropResolver.ResolveByRule(trajectory, tee, isWater: false);

            Assert.AreEqual(tee, result,
                "Boundary OB must drop at the previous shot origin (stroke and distance), " +
                "not at the last in-bounds terrain hit.");
        }

        // ── Test 6c: K10 water OB still uses the last-dry-touch resolver ──────────

        [Test]
        public void OBDropResolver_ResolveByRule_WaterDelegatesToResolve()
        {
            // Water (isWater=true) must be byte-identical to Resolve(): last safe hit before water.
            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(10f, 0f, 0f), SurfaceType.Fairway),
                MakeHit(new Vector3(20f, 0f, 0f), SurfaceType.Fairway),
                MakeHit(new Vector3(30f, 0f, 0f), SurfaceType.Water),
            };
            var trajectory = MakeTrajectory(hits);
            var origin     = new Vector3(0f, 0f, 0f);

            Vector3 byRule  = OBDropResolver.ResolveByRule(trajectory, origin, isWater: true);
            Vector3 resolve = OBDropResolver.Resolve(trajectory, origin);

            Assert.AreEqual(resolve, byRule,
                "Water OB must delegate to Resolve() (last dry touch), unchanged from §2e.");
            Assert.AreEqual(new Vector3(20f, 0f, 0f), byRule,
                "Water drop should be the last fairway hit before the water hazard.");
        }

        // ── Tests 6d–6f: water drops at the hazard margin, not behind it ──────────

        /// <summary>Everything at or beyond boundaryX is water; before it is fairway.</summary>
        sealed class WaterBeyondXProvider : ISurfaceProvider
        {
            readonly float _boundaryX;
            public WaterBeyondXProvider(float boundaryX) { _boundaryX = boundaryX; }
            public SurfaceType Classify(fp worldX, fp worldZ)
                => worldX.ToFloat() >= _boundaryX ? SurfaceType.Water : SurfaceType.Fairway;
        }

        // Flight from x=0 out to x=100 in 1 m steps; water starts at x=60.
        // One land bounce is recorded far back at x=20 — the old resolver's answer.
        static Trajectory WaterCarryTrajectory()
        {
            var samples = new List<TrajectorySample>();
            for (int i = 0; i <= 100; i++)
                samples.Add(new TrajectorySample(
                    fp.FromFloat(i * 0.05f),
                    new fp3(fp.FromFloat(i), fp.FromFloat(10f), fp.Zero),
                    fp3.Zero));

            var hits = new List<TerrainHit>
            {
                MakeHit(new Vector3(20f, 0f, 0f), SurfaceType.Fairway),   // early bounce, then carried
                MakeHit(new Vector3(85f, 0f, 0f), SurfaceType.Water),     // splash
            };
            return new Trajectory(samples, new fp3(fp.FromFloat(85f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.HitWater, hits);
        }

        [Test]
        public void OBDropResolver_Water_DropsAtHazardMargin_NotTheLastBounce()
        {
            var traj     = WaterCarryTrajectory();
            var provider = new WaterBeyondXProvider(60f);
            var origin   = new Vector3(0f, 0f, 0f);

            Vector3 drop = OBDropResolver.ResolveByRule(traj, origin, isWater: true, provider: provider);

            // Margin is at x=60; the last dry SAMPLE is x=59 (crossing lies between 59 and 60).
            Assert.AreEqual(59f, drop.x, 1.01f,
                "Water drop must be at the hazard margin the ball last crossed (x≈59–60), " +
                "NOT the last land bounce at x=20 that the terrain-hit scan returns.");

            // Guard the actual defect: strictly ahead of the old answer, still short of the water.
            Assert.Greater(drop.x, 20f, "Must not fall back to the far-behind last bounce (x=20).");
            Assert.Less(drop.x, 60f,    "Must never be dropped inside the hazard / nearer the hole.");
        }

        [Test]
        public void OBDropResolver_Water_NoProvider_FallsBackToLegacyLastDryTouch()
        {
            var traj   = WaterCarryTrajectory();
            var origin = new Vector3(0f, 0f, 0f);

            // Flat-ground / no-hole sessions have no classifier — behaviour must be unchanged.
            Vector3 drop = OBDropResolver.ResolveByRule(traj, origin, isWater: true, provider: null);

            Assert.AreEqual(new Vector3(20f, 0f, 0f), drop,
                "Without a classifier the legacy last-dry-touch scan must still be used.");
        }

        [Test]
        public void OBDropResolver_Water_FlightNeverOverLand_FallsBackToOrigin()
        {
            // Teed off already inside the hazard: every sample is over water.
            var samples = new List<TrajectorySample>();
            for (int i = 0; i <= 10; i++)
                samples.Add(new TrajectorySample(
                    fp.FromFloat(i * 0.05f),
                    new fp3(fp.FromFloat(70f + i), fp.FromFloat(5f), fp.Zero),
                    fp3.Zero));
            var traj = new Trajectory(samples, new fp3(fp.FromFloat(80f), fp.Zero, fp.Zero),
                fp3.Zero, fp.One, TerminationReason.HitWater, new List<TerrainHit>());

            var origin = new Vector3(65f, 0f, 0f);
            Vector3 drop = OBDropResolver.ResolveByRule(traj, origin, isWater: true,
                provider: new WaterBeyondXProvider(60f));

            Assert.AreEqual(origin, drop,
                "A flight that was never over land must fall back to the previous shot origin.");
        }

        // ── Test 7: AimRotationHelper points toward pin ────────────────────────────

        [Test]
        public void AimRotationHelper_PointsTowardPin()
        {
            var ballPos = new Vector3(0f, 0f, 0f);
            var pinPos  = new Vector3(10f, 0f, 10f);   // 45° in XZ plane
            float fallbackYaw = 0f;

            float yaw = AimRotationHelper.ComputeYawTowardPin(ballPos, pinPos, fallbackYaw);

            Assert.AreEqual(Mathf.PI / 4f, yaw, 1e-4f,
                "Yaw toward (10,0,10) from origin should be π/4 radians.");
        }

        // ── Test 8: AimRotationHelper falls back when pin is unset ────────────────

        [Test]
        public void AimRotationHelper_FallsBackWhenPinUnset()
        {
            var ballPos      = new Vector3(5f, 0f, 5f);
            var pinPos       = Vector3.zero;   // unset sentinel
            float fallback   = 1.5f;

            float yaw = AimRotationHelper.ComputeYawTowardPin(ballPos, pinPos, fallback);

            Assert.AreEqual(fallback, yaw,
                "When pinPos is Vector3.zero, ComputeYawTowardPin should return fallbackYaw.");
        }

        // ── Test 9: HoleSessionDriver.ComputeNextTurn advances by 1+penalty ──────

        [Test]
        public void HoleSessionDriver_ComputeNextTurn_AdvancesByOnePlusPenalty()
        {
            Assert.AreEqual(4, HoleSessionDriver.ComputeNextTurn(3, 0),
                "No penalty: turn 3 → 4 (advance by 1).");
            Assert.AreEqual(5, HoleSessionDriver.ComputeNextTurn(3, 1),
                "OB penalty: turn 3 → 5 (advance by 1 + 1 penalty).");
            Assert.AreEqual(4, HoleSessionDriver.ComputeNextTurn(3, -1),
                "Negative penalty clamped to 0: turn 3 → 4.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        static TerrainHit MakeHit(Vector3 pos, SurfaceType surface)
        {
            return new TerrainHit(
                fp.Zero,
                new fp3(fp.FromFloat(pos.x), fp.FromFloat(pos.y), fp.FromFloat(pos.z)),
                fp3.Zero,
                fp3.Zero,
                surface,
                false);
        }

        static Trajectory MakeTrajectory(List<TerrainHit> hits)
        {
            return new Trajectory(
                new List<TrajectorySample>(),
                fp3.Zero,
                fp3.Zero,
                fp.Zero,
                TerminationReason.HitWater,
                hits);
        }
    }
}
