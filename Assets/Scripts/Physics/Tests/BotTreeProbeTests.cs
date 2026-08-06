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

        // ── Tests 7–10: TrySampleTrunkClearAimError (bot_tree_error_recheck) ─────────────────

        // Trunk dead-ahead at x=20 with large scale so it covers all ±6° perturbations.
        // scale=32 → trunkRadius~8m; from origin at yaw=0, ANY sample within ±37° hits it at 20m.
        private const string CsvHugeTrunkAhead =
            "# test — huge trunk blocks all perturbed aim lines within ±6°\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "10.0,0.0,0.0,32.0,default\n";   // placed at 10m to ensure first 0→6m step hits

        // Narrow trunk at x=100, scale=8 → trunkRadius~2m.
        // At d=100m: samples < ~1.15° hit trunk; samples > ~1.15° miss.
        // Used with a mock sampler to control which deltas are blocked vs clear.
        private const string CsvNarrowTrunkAt100 =
            "# test — narrow trunk at 100m; deltas > ~1.15° at carry=120 are clear\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "100.0,0.0,0.0,8.0,default\n";

        // ── Test 7: null provider — first sample accepted, delta within ±max ────────

        [Test]
        public void TrySampleTrunkClearAimError_NullProvider_ReturnsFirstSample()
        {
            // Seed a deterministic sampler. trees==null → helper returns immediately after
            // drawing exactly ONE sample (no LineHasTrunkInWindows call at all).
            var rng = new System.Random(1234);
            int sampleCount = 0;
            System.Func<float, float, float> seededRange = (min, max) =>
            {
                sampleCount++;
                return (float)(min + (max - min) * rng.NextDouble());
            };

            float delta;
            bool result = BotTreeProbe.TrySampleTrunkClearAimError(
                trees: null, ball: BallOrigin, safeYaw: 0f, carry: 80f,
                aimErrorDegMax: 6f, maxTries: 5, sampleRange: seededRange,
                deltaAimDeg: out delta);

            Assert.IsTrue(result,
                "Null provider must return true (treeless no-op — first sample accepted)");
            Assert.AreEqual(1, sampleCount,
                "Null provider must draw exactly ONE sample then return (no more iterations)");
            Assert.That(delta, Is.InRange(-6f, 6f),
                "Accepted delta must be within ±aimErrorDegMax");
        }

        // ── Test 8: straight line clear, all samples clear → returns true ─────────

        [Test]
        public void TrySampleTrunkClearAimError_AllSamplesClear_ReturnsTrue()
        {
            // Trunk is BESIDE the line (CsvTrunkAside: trunk at z=50), so all perturbed
            // lines along +X are clear. Helper should return true on the first sample.
            var trees = BuildProvider(CsvTrunkAside);
            Assert.IsNotNull(trees);

            var rng = new System.Random(7);
            int sampleCount = 0;
            System.Func<float, float, float> seededRange = (min, max) =>
            {
                sampleCount++;
                return (float)(min + (max - min) * rng.NextDouble());
            };

            float delta;
            bool result = BotTreeProbe.TrySampleTrunkClearAimError(
                trees, BallOrigin, safeYaw: 0f, carry: 80f,
                aimErrorDegMax: 6f, maxTries: 5, seededRange, out delta);

            Assert.IsTrue(result,
                "Clear line: helper must return true (first trunk-free sample accepted)");
            Assert.LessOrEqual(sampleCount, 5,
                "Sample count must not exceed maxTries");
            Assert.That(delta, Is.InRange(-6f, 6f),
                "Accepted delta must be within ±aimErrorDegMax");
        }

        // ── Test 9: trunk blocks first two samples; third is clear → returns true ──

        [Test]
        public void TrySampleTrunkClearAimError_TrunkBlocksEarlySamples_ReturnsClearSample()
        {
            // Narrow trunk at x=100, trunkRadius~2m (scale=8).
            // Samples < ~1.15° in absolute value hit the trunk; samples > ~2° are clear.
            // Mock sampler returns: 0.5° (blocked), 0.5° (blocked), 2.5° (clear).
            var trees = BuildProvider(CsvNarrowTrunkAt100);
            Assert.IsNotNull(trees);

            float[] canned = { 0.5f, 0.5f, 2.5f };
            int idx = 0;
            System.Func<float, float, float> mock = (_, __) => canned[idx++];

            float delta;
            bool result = BotTreeProbe.TrySampleTrunkClearAimError(
                trees, BallOrigin, safeYaw: 0f, carry: 120f,
                aimErrorDegMax: 6f, maxTries: 5, mock, out delta);

            Assert.IsTrue(result,
                "Must return true when a clear sample is found within maxTries");
            Assert.AreEqual(3, idx,
                "Exactly 3 samples drawn: two blocked, one clear");
            Assert.AreEqual(2.5f, delta, 0.001f,
                "deltaAimDeg must be the first clear sample (2.5°)");
        }

        // ── Test 10: all samples blocked → returns false, deltaAimDeg == 0 ─────────

        [Test]
        public void TrySampleTrunkClearAimError_AllSamplesBlocked_ReturnsFalseAndZeroDelta()
        {
            // Huge trunk at x=10 with radius~8m covers all ±6° samples at dist=30m.
            var trees = BuildProvider(CsvHugeTrunkAhead);
            Assert.IsNotNull(trees);

            // Mock sampler returns 5 distinct values, all within ±6° — all will be blocked.
            float[] canned = { 5.5f, -5.5f, 4.0f, -4.0f, 3.0f };
            int idx = 0;
            System.Func<float, float, float> mock = (_, __) => canned[idx++];

            float delta;
            bool result = BotTreeProbe.TrySampleTrunkClearAimError(
                trees, BallOrigin, safeYaw: 0f, carry: 30f,
                aimErrorDegMax: 6f, maxTries: 5, mock, out delta);

            Assert.IsFalse(result,
                "Must return false when all maxTries samples are trunk-blocked");
            Assert.AreEqual(0f, delta, 0.001f,
                "deltaAimDeg must be 0 on false return (fall back to pre-2b line)");
            Assert.AreEqual(5, idx,
                "All maxTries (5) samples must have been drawn before giving up");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // canopy_avoidance_v2 — Tests for TrySampleTreeAwareAimError + drift guard
        // ══════════════════════════════════════════════════════════════════════════

        // ── Fake provider helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Provider that always reports no hit. Used for canopy-free / trunk-free baselines.
        /// </summary>
        private class NoHitProvider : ITreeObstacleProvider
        {
            public static readonly NoHitProvider Instance = new NoHitProvider();
            public bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit)
            {
                hit = default;
                return false;
            }
        }

        /// <summary>
        /// Provider that returns a CANOPY hit (IsTrunk=false) when the probed segment
        /// is aimed within <see cref="AngleThreshRad"/> of the +X axis AND the midpoint
        /// height exceeds <see cref="MinHitY"/>. No trunk hits are ever reported.
        ///
        /// This decouples trunk vs canopy cleanly: LineHasTrunkInWindows probes at y=ball.y
        /// (low, ≤ MinHitY) → no hit → trunk-clear. CountCanopyContacts probes at modelled
        /// apex height (high, > MinHitY) → canopy hit when aimed near +X.
        /// </summary>
        private class CanopyInBoreProvider : ITreeObstacleProvider
        {
            public readonly float AngleThreshRad;
            public readonly float MinHitY;

            public CanopyInBoreProvider(float angleThreshDeg, float minHitY = 3f)
            {
                AngleThreshRad = angleThreshDeg * Mathf.Deg2Rad;
                MinHitY        = minHitY;
            }

            public bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit)
            {
                hit = default;
                float midY = (p0.y + p1.y).ToFloat() * 0.5f;
                if (midY <= MinHitY) return false;   // below canopy zone → no hit

                // Horizontal direction of this probe segment.
                float dx    = (p1.x - p0.x).ToFloat();
                float dz    = (p1.z - p0.z).ToFloat();
                float len   = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 0.001f) return false;
                float angle = Mathf.Atan2(Mathf.Abs(dz), Mathf.Abs(dx));  // angle from +X axis

                if (angle < AngleThreshRad)
                {
                    hit.IsTrunk = false;   // canopy hit
                    return true;
                }
                return false;
            }
        }

        // ── Test A: null trees → first sample accepted, single draw, canopyContacts==0 ─

        [Test]
        public void TrySampleTreeAwareAimError_NullTrees_AcceptsFirstSampleCanopy0()
        {
            int draws = 0;
            System.Func<float, float, float> sampler = (_, __) => { draws++; return 7.0f; };

            bool ok = BotTreeProbe.TrySampleTreeAwareAimError(
                trees: null, ball: BallOrigin, safeYaw: 0f,
                carry: 60f, apex: 6f,
                aimErrorDegMax: 10f, maxTries: 5,
                sampleRange: sampler,
                disableCanopyPreference: false,
                out float delta, out int contacts);

            Assert.IsTrue(ok,  "null trees: must return true");
            Assert.AreEqual(7.0f, delta,    0.001f, "null trees: must return first sample value");
            Assert.AreEqual(0,   contacts,          "null trees: canopyContacts must be 0");
            Assert.AreEqual(1,   draws,              "null trees: sampler must be called exactly once");
        }

        // ── Test B: all trunk-clear, canopy-free → returns smallest |delta| (tie-break) ─

        [Test]
        public void TrySampleTreeAwareAimError_AllCanopyFree_ReturnsSmallestAbsDelta()
        {
            // Provider reports no hits at all — every sample is trunk-clear and canopy-free.
            var trees = NoHitProvider.Instance;

            // Three canned samples: |6|=6, |-3|=3, |5|=5 → tie-break picks -3.
            float[] canned = { 6.0f, -3.0f, 5.0f };
            int idx = 0;
            System.Func<float, float, float> sampler = (_, __) => canned[idx++];

            bool ok = BotTreeProbe.TrySampleTreeAwareAimError(
                trees, BallOrigin, safeYaw: 0f,
                carry: 60f, apex: 6f,
                aimErrorDegMax: 8f, maxTries: 3,
                sampleRange: sampler,
                disableCanopyPreference: false,
                out float delta, out int contacts);

            Assert.IsTrue(ok,    "must return true when survivors exist");
            Assert.AreEqual(-3.0f, delta,    0.001f,
                "tie-break: smallest |delta| = |-3|=3, not |6| or |5|");
            Assert.AreEqual(0, contacts,
                "all samples are canopy-free → contacts must be 0");
        }

        // ── Test C: canopy-heavy sample loses to canopy-free despite larger |delta| ─────

        [Test]
        public void TrySampleTreeAwareAimError_CanopyFreePrefersOverHeavy_DespiteLargerDelta()
        {
            // CanopyInBoreProvider: reports canopy when aimed within 5° of +X AND
            // probe height > 3 m. This means:
            //   delta=-2° (yaw≈0): aimed near +X, apex probe at ~3-9 m → canopy contacts>0.
            //   delta=20° (yaw=20°): aimed 20° away, angle>5° → NO canopy contacts.
            // LineHasTrunkInWindows probes at ball.y=1m ≤ 3m → no trunk hits for either.
            var trees = new CanopyInBoreProvider(angleThreshDeg: 5f, minHitY: 3f);
            var ball  = new Vector3(0f, 1f, 0f);  // ball.y=1m < minHitY → trunk-clear

            float[] canned = { -2.0f, 20.0f };
            int idx = 0;
            System.Func<float, float, float> sampler = (_, __) => canned[idx++];

            bool ok = BotTreeProbe.TrySampleTreeAwareAimError(
                trees, ball, safeYaw: 0f,
                carry: 40f, apex: 8f,      // Driver apex=7.92m; probes reach ~7-9m
                aimErrorDegMax: 20f, maxTries: 2,
                sampleRange: sampler,
                disableCanopyPreference: false,
                out float delta, out int contacts);

            Assert.IsTrue(ok, "must return true (both samples are trunk-clear)");
            Assert.AreEqual(20.0f, delta, 0.001f,
                "delta=20° (canopy-free) must win over delta=-2° (canopy-heavy) despite larger |delta|");
            Assert.AreEqual(0, contacts,
                "winning sample (20°) must have 0 canopy contacts");
        }

        // ── Test D: all trunk-blocked → false, deltaAimDeg==0, canopyContacts==-1 ─────

        [Test]
        public void TrySampleTreeAwareAimError_AllTrunkBlocked_ReturnsFalseAndZero()
        {
            // CsvHugeTrunkAhead: huge trunk (scale=8) at x=10 blocks all ±6° samples.
            var trees = BuildProvider(CsvHugeTrunkAhead);
            Assert.IsNotNull(trees, "provider must not be null");

            float[] canned = { 5.5f, -5.5f, 4.0f, -4.0f, 3.0f };
            int idx = 0;
            System.Func<float, float, float> sampler = (_, __) => canned[idx++];

            bool ok = BotTreeProbe.TrySampleTreeAwareAimError(
                trees, BallOrigin, safeYaw: 0f,
                carry: 30f, apex: 6f,
                aimErrorDegMax: 6f, maxTries: 5,
                sampleRange: sampler,
                disableCanopyPreference: false,
                out float delta, out int contacts);

            Assert.IsFalse(ok,
                "must return false when all maxTries samples are trunk-blocked");
            Assert.AreEqual(0f, delta, 0.001f,
                "deltaAimDeg must be 0 on false return (pre-2b line fallback)");
            Assert.AreEqual(-1, contacts,
                "canopyContacts must be -1 on false return");
        }

        // ── Test E: apex drift guard — BotTreeProbe table matches BallSimulation ≤±1.0m ─

        [Test]
        public void ApexDriftGuard_TableMatchesSim()
        {
            // Re-derive apex from BallSimulation.Simulate for clubs 0-2 (putter excluded).
            // Assert that BotTreeProbe.ApexAtFullCarry matches within ±1.0m tolerance.
            // This test fails loudly if the physics sim is retuned without updating the table.
            //
            // Input parameters from SPEC §1 table (ballSpeed m/s, launch °):
            //   0=Driver:  speed=75.0, launch=10.9°, expectedApex=7.92
            //   1=iron7:   speed=70.6, launch=9.2°,  expectedApex=5.29
            //   2=A.Wedge: speed=46.0, launch=24.0°, expectedApex=14.42

            float[] speeds  = { 75.0f, 70.6f, 46.0f };
            float[] launchD = { 10.9f,  9.2f, 24.0f };
            float   tol     = 1.0f;  // SPEC §4.1 drift-guard tolerance

            for (int club = 0; club < 3; club++)
            {
                float speed      = speeds[club];
                float launchRad  = launchD[club] * Mathf.Deg2Rad;
                float horizSpeed = speed * Mathf.Cos(launchRad);
                float vertSpeed  = speed * Mathf.Sin(launchRad);

                var input = new ShotInput(
                    origin:      fp3.Zero,
                    velocity:    new fp3(fp.FromFloat(horizSpeed), fp.FromFloat(vertSpeed), fp.Zero),
                    maxDuration: fp.FromInt(30),
                    spin:        new SpinState(fp3.Zero, fp.Zero));

                var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Default);

                float simApex = 0f;
                foreach (var s in traj.samples)
                    simApex = Mathf.Max(simApex, s.position.y.ToFloat());

                float tableApex = BotTreeProbe.ApexAtFullCarry[club];

                Assert.AreEqual(tableApex, simApex, tol,
                    $"Club {club}: table apex {tableApex:F2}m vs sim apex {simApex:F2}m " +
                    $"(tolerance ±{tol:F1}m). Re-run the calibration harness if this fails.");
            }
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
