using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.UI.HUD;

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
