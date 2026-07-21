// tree_aware_bot (Order 351) — EditMode unit tests for BotTreeProbe
// Assembly: Golfin.Physics.Tests (already references Golfin.Physics.Viewer via asmdef)
// Run via: Unity Test Runner → EditMode → filter "BotTreeProbe"
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    [TestFixture]
    public class BotTreeProbeTests
    {
        // ── helpers ────────────────────────────────────────────────────────────────
        // CSV format matches TreeObstacleLoader.LoadInstancesFromText:
        //   # optional comment
        //   worldX,worldZ,baseY,scale,profileName   <- header (skipped)
        //   <float>,<float>,<float>,<float>,<name>  <- data rows
        //
        // Default profile: trunkRadius=0.25m, trunkHeight=3.0m, canopyRadius=3m, canopyTop=9m.
        // Scale=8 → trunkRadius~2m, giving a robust target for 6m-step marching.

        private static ITreeObstacleProvider BuildProvider(string csvText)
        {
            var instances = TreeObstacleLoader.LoadInstancesFromText(csvText);
            return TreeObstacleProvider.Create(instances);
        }

        // Trunk dead-ahead at x=20, z=0.  scale=8 → trunkRadius~2m so a 6m step hits it.
        private const string CsvTrunkAhead =
            "# test\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "20.0,0.0,0.0,8.0,default\n";

        // Trunk far to the side (x=0, z=50) — perpendicular to +X aim line, never intersects.
        private const string CsvTrunkAside =
            "# test\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "0.0,50.0,0.0,1.0,default\n";

        // Trunk squarely in the apex band (x=40, z=0) for dist=80.
        // With nearEnd=35 and landStart=45, the step [36,42] is fully inside [35,45] → skipped.
        // The step [42,48] is outside the apex band check (dEnd=48 > landStart=45) but
        // the trunk centre at x=40 is not crossed by [42,48] → no hit.
        private const string CsvTrunkApex =
            "# test\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "40.0,0.0,0.0,1.0,default\n";

        // Ball at origin (y=1 puts us within default trunk height [0,3m]).
        private static readonly Vector3 BallOrigin = new Vector3(0f, 1f, 0f);

        // ── Test 1: clear line — no trunk on the path ──────────────────────────────

        [Test]
        public void LineHasTrunkInWindows_ClearLine_ReturnsFalse()
        {
            var trees = BuildProvider(CsvTrunkAside);
            Assert.IsNotNull(trees, "Provider must not be null for side-placed trunk");

            // Aiming along +X, trunk at (0,1,50) — completely off to the side.
            bool hit = BotTreeProbe.LineHasTrunkInWindows(trees, BallOrigin, yaw: 0f, dist: 80f);
            Assert.IsFalse(hit, "No trunk hit expected on a line clear of any trunk");
        }

        // ── Test 2: trunk directly ahead — detect it and find safe re-aim ─────────

        [Test]
        public void TryFindTrunkClearAim_TrunkOnLine_FindsSafeAim()
        {
            var trees = BuildProvider(CsvTrunkAhead);
            Assert.IsNotNull(trees, "Provider must not be null for ahead-placed trunk");

            bool result = BotTreeProbe.TryFindTrunkClearAim(
                trees, surfaces: null,
                ball: BallOrigin, aimYaw: 0f, targetDist: 60f,
                out float safeYaw, out float safeDist);

            Assert.IsTrue(result,
                "TryFindTrunkClearAim should return true when trunk blocks the direct line");
            Assert.IsTrue(safeDist >= 10f,
                "Safe dist must be >= LayupMinDistM (10m)");
        }

        // ── Test 3: trunk only in apex band — fly-over, should NOT be detected ────

        [Test]
        public void LineHasTrunkInWindows_ApexBandTrunk_ReturnsFalse()
        {
            var trees = BuildProvider(CsvTrunkApex);
            Assert.IsNotNull(trees, "Provider must not be null for apex-placed trunk");

            // dist=80 → nearEnd=35, landStart=45. Trunk centre at x=40 lives in [35,45].
            // Step [36,42]: d=36 >= nearEnd=35 AND dEnd=42 <= landStart=45 → skipped.
            // Step [42,48]: ends at 48 > 45 → not skipped, but segment [42,48] is past the
            //               trunk (centre at 40, radius 0.25 → edge at 40.25 < 42). No hit.
            bool hit = BotTreeProbe.LineHasTrunkInWindows(trees, BallOrigin, yaw: 0f, dist: 80f);
            Assert.IsFalse(hit,
                "Trunk in apex band should be fly-over skipped — ball is above it at that point");
        }

        // ── Test 4: null provider — must be a pure no-op ──────────────────────────

        [Test]
        public void TryFindTrunkClearAim_NullProvider_ReturnsFalse()
        {
            bool result = BotTreeProbe.TryFindTrunkClearAim(
                trees: null, surfaces: null,
                ball: BallOrigin, aimYaw: 0f, targetDist: 50f,
                out float _, out float _);

            Assert.IsFalse(result,
                "Null provider must return false immediately (no-op on treeless holes)");
        }

        // ── Test 5: water landing — IsPlayableLanding must reject it ──────────────

        [Test]
        public void IsPlayableLanding_WaterSurface_ReturnsFalse()
        {
            // ConstantSurfaceProvider classifies every point as the given surface type.
            var waterSurfaces = new ConstantSurfaceProvider(SurfaceType.Water);

            bool playable = BotTreeProbe.IsPlayableLanding(
                waterSurfaces, BallOrigin, yaw: 0f, dist: 30f);

            Assert.IsFalse(playable,
                "Water surface must be rejected by IsPlayableLanding (IsAvoid=true)");
        }

        // ── Test 6: carry-vs-cup regression (Order 351 §9 iter-2 fix) ─────────────
        // A trunk placed at x=265m (inside the carry landing window 252-287m) must be
        // detected when the probe receives carry=287m, but NOT when it receives
        // cup_dist=417m (where 265m is deep in the apex band [35,382]).
        // This locks the §9 fix: the probe must receive the club's carry, not the cup dist.

        private const string CsvTrunkAt265 =
            "# test — trunk at carry landing zone (iter-2 §9 regression)\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "265.0,0.0,0.0,8.0,default\n";  // scale=8 → trunkRadius~2m, hits 6m probe step

        [Test]
        public void TryFindTrunkClearAim_CarryLengthTarget_FiresOnCarryNotCup()
        {
            var trees = BuildProvider(CsvTrunkAt265);
            Assert.IsNotNull(trees, "Provider must not be null");

            const float carry   = 287f;    // driver's actual carry (modelled landing)
            const float cupDist = 417f;    // full tee-to-cup distance (Par 5 Hole 8)
            // Aim along +X (yaw=0). Trunk at x=265 is in the landing window [252,287].

            // --- With carry as the target: probe MUST detect the trunk ---
            bool hitOnCarry = BotTreeProbe.LineHasTrunkInWindows(
                trees, BallOrigin, yaw: 0f, dist: carry);
            Assert.IsTrue(hitOnCarry,
                "Trunk at x=265 must be in landing window [252,287] when carry=287 — probe must fire");

            // --- With cup_dist as the target: trunk is inside apex band [35,382], must NOT be detected ---
            bool hitOnCupDist = BotTreeProbe.LineHasTrunkInWindows(
                trees, BallOrigin, yaw: 0f, dist: cupDist);
            Assert.IsFalse(hitOnCupDist,
                "Trunk at x=265 must be inside apex band [35,382] when dist=417 — probe must NOT fire (iter-1 bug)");
        }
    }
}
