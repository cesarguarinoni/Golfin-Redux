using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 4 surface-interaction tests. All 29 tests (21 prior + 8 here) must pass.
    /// </summary>
    public class SurfaceTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static ShotInput IronInput()
        {
            // 7-iron: 52.5 m/s, 16.3°, 7097 rpm backspin — same as AerodynamicsTests
            float angleRad = 16.3f * (float)System.Math.PI / 180f;
            float speed    = 52.5f;
            var   origin   = fp3.Zero;
            var   velocity = new fp3(fp.Zero,
                fp.FromDouble(speed * System.Math.Sin(angleRad)),
                fp.FromDouble(speed * System.Math.Cos(angleRad)));
            float rps  = 7097f * 2f * (float)System.Math.PI / 60f;
            var   spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            return new ShotInput(origin, velocity, fp.FromInt(30), spin);
        }

        private static SurfaceConfig AllFairwayConfig()
        {
            // Override all surfaces to Fairway coefficients so every surface behaves identically.
            var cfg = SurfaceConfig.Default;
            var fairway = cfg[SurfaceType.Fairway];
            for (int i = 0; i < cfg.Coefficients.Length; i++)
                cfg.Coefficients[i] = fairway;
            return cfg;
        }

        // ── Test 1: Bit-exact gate ────────────────────────────────────────────────

        /// <summary>
        /// Run a 7-iron through all four overloads with equivalent physics.
        /// The airborne hit position (first ground contact) must be bit-exact across all four.
        /// This guards against accidental changes to the airborne path in the Phase 4 overload.
        /// </summary>
        [Test]
        public void Surface_Phase3Overloads_BitExact()
        {
            var input  = IronInput();
            var ground = new FlatGround(fp.Zero);
            var aero   = AeroConfig.Default;
            var wind   = WindConfig.Calm;

            // Phase 1–3 overloads — terminate at HitGround.
            var t1 = BallSimulation.Simulate(input, ground);
            var t2 = BallSimulation.Simulate(input, ground, aero);
            var t3 = BallSimulation.Simulate(input, ground, aero, wind);

            // Phase 4 overload with stub providers.
            var t4 = BallSimulation.Simulate(input, ground, aero, wind,
                new ConstantSurfaceProvider(SurfaceType.Fairway), SurfaceConfig.Default);

            // Phase 1–3 must still terminate at HitGround.
            Assert.AreEqual(TerminationReason.HitGround, t1.termination, "t1 termination");
            Assert.AreEqual(TerminationReason.HitGround, t2.termination, "t2 termination");
            Assert.AreEqual(TerminationReason.HitGround, t3.termination, "t3 termination");

            // Phase 4's first terrain hit position must equal Phase 3's final position (bit-exact).
            Assert.IsTrue(t4.terrainHits.Count > 0, "Phase 4 must record at least one terrain hit");
            var firstHit = t4.terrainHits[0];

            Assert.AreEqual(t3.finalPosition.x.raw, firstHit.Position.x.raw,
                "Phase4 first hit X vs Phase3 finalPosition X (bit-exact)");
            Assert.AreEqual(t3.finalPosition.z.raw, firstHit.Position.z.raw,
                "Phase4 first hit Z vs Phase3 finalPosition Z (bit-exact)");

            // Phase 4 must have continued past HitGround.
            Assert.AreNotEqual(TerminationReason.HitGround, t4.termination,
                "Phase 4 must not terminate at HitGround — it should bounce/roll to BallStopped");
        }

        // ── Test 2: Backspin checks on green ─────────────────────────────────────

        [Test]
        public void Surface_Bounce_OnGreenWithBackspin_Checks()
        {
            // Ball dropped from 30m onto Green with 5000 rpm backspin.
            float rps  = 5000f * 2f * (float)System.Math.PI / 60f;
            var   spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            var   input = new ShotInput(
                new fp3(fp.Zero, fp.FromInt(30), fp.Zero),
                fp3.Zero, fp.FromInt(30), spin);

            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Default,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default);

            // Ball must come to rest (not bounce forever) and terminate as BallStopped.
            Assert.AreEqual(TerminationReason.BallStopped, traj.termination,
                "Backspin ball on green must terminate as BallStopped");
            float stopDist = System.Math.Abs(traj.finalPosition.z.ToFloat());
            Assert.Less(stopDist, 20f, $"Ball with backspin should stop within 20m of drop; got {stopDist:F2}m");
        }

        // ── Test 3: Cart path high restitution ───────────────────────────────────

        [Test]
        public void Surface_Bounce_OnCartPath_HighRestitution()
        {
            // Ball dropped from 10m onto CartPath (Cr = 0.70).
            var input = new ShotInput(
                new fp3(fp.Zero, fp.FromInt(10), fp.Zero),
                fp3.Zero, fp.FromInt(30), new SpinState(fp3.Zero, fp.Zero));

            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Vacuum,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.CartPath), SurfaceConfig.Default);

            // With Cr=0.70 and no air drag: impact vy ≈ 14.0 m/s, bounce vy ≈ 9.8 m/s,
            // peak = vy²/(2g) = 9.8²/19.61 ≈ 4.9m. Assert ≥4.0m (vs Fairway Cr=0.50→2.5m).
            Assert.IsTrue(traj.terrainHits.Count > 0, "Must record bounce hit");
            float bounceVy = traj.terrainHits[0].VelocityOut.y.ToFloat();
            Assert.Greater(bounceVy, 0f, "Bounce velocity must be upward");

            float peakH = bounceVy * bounceVy / (2f * 9.80665f);
            Assert.Greater(peakH, 4.0f,
                $"First bounce peak ≥4.0m (CartPath Cr=0.70 from 10m drop); got {peakH:F2}m");
        }

        // ── Test 4: Roll stops on flat fairway ───────────────────────────────────

        [Test]
        public void Surface_Roll_StopsOnFlatFairway()
        {
            // Ball 1cm above ground with 3 m/s horizontal — impacts immediately, rolls to stop.
            // Starting exactly at ground level doesn't trigger the airborne hit detector
            // (requires pos.y > groundY), so we use a 1cm offset.
            var input = new ShotInput(
                new fp3(fp.Zero, fp.FromFloat(0.01f), fp.Zero),
                new fp3(fp.Zero, fp.Zero, fp.FromFloat(3f)),
                fp.FromInt(60),
                new SpinState(fp3.Zero, fp.Zero));

            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Vacuum,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.Fairway), SurfaceConfig.Default);

            // v0=3, rolling_resistance=0.18 → stop distance ≈ 3/0.18 ≈ 17m
            float dist = traj.finalPosition.z.ToFloat();
            Assert.Less(dist, 35f, $"Ball should stop within 35m; got {dist:F2}m");
            Assert.AreEqual(TerminationReason.BallStopped, traj.termination, "Must terminate as BallStopped");
        }

        // ── Test 5: Roll accelerates downslope ───────────────────────────────────

        [Test]
        public void Surface_Roll_AcceleratesDownSlope()
        {
            // Synthetic 3×3 heightmap representing a 10° slope falling in +Z direction.
            // Heights: row 0 (z=0) is high, row 2 (z=end) is low.
            // Slope angle 10°: dh/dz = -tan(10°) ≈ -0.1763
            float slopeSize = 50f; // 50m × 50m patch
            float tanSlope  = (float)System.Math.Tan(10.0 * System.Math.PI / 180.0);
            int   res       = 3;
            // Q16.16 height values: h(z) = -tanSlope * z_world
            // z_world ranges from 0 to slopeSize across rows 0..res-1
            float cellZ = slopeSize / (res - 1);
            var heights = new int[res * res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float zWorld = iz * cellZ;
                float hVal   = -tanSlope * zWorld; // meters, Q16.16 raw
                heights[iz * res + ix] = (int)System.Math.Round(hVal * 65536.0);
            }

            var slopeMap = new HeightmapData(res,
                fp.FromFloat(slopeSize), fp.FromFloat(slopeSize),
                fp.Zero, fp.Zero, fp.Zero, heights);

            // Ball starts at rest at (0, groundH, 0) — top of slope.
            fp startY = slopeMap.SampleHeight(fp.Zero, fp.Zero);
            // Start slightly above ground so airborne hit detection fires.
            var inputPos = new fp3(fp.Zero, startY + fp.FromFloat(0.01f), fp.Zero);
            var input = new ShotInput(
                inputPos, fp3.Zero, fp.FromInt(60),
                new SpinState(fp3.Zero, fp.Zero));

            var traj = BallSimulation.Simulate(input, slopeMap, AeroConfig.Vacuum,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.Fairway), SurfaceConfig.Default);

            float zDisplacement = traj.finalPosition.z.ToFloat();
            Assert.Greater(zDisplacement, 5f,
                $"Ball should roll downhill ≥5m; got {zDisplacement:F2}m");
            Assert.AreEqual(TerminationReason.BallStopped, traj.termination, "Must terminate as BallStopped");
        }

        // ── Test 6: Water terminates sim ─────────────────────────────────────────

        [Test]
        public void Surface_Water_TerminatesSim()
        {
            var input = new ShotInput(
                new fp3(fp.Zero, fp.FromInt(10), fp.Zero),
                fp3.Zero, fp.FromInt(30),
                new SpinState(fp3.Zero, fp.Zero));

            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Vacuum,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.Water), SurfaceConfig.Default);

            Assert.AreEqual(TerminationReason.HitWater, traj.termination, "Must terminate with HitWater");
            Assert.AreEqual(1, traj.terrainHits.Count, "Exactly one TerrainHit for water contact");
            Assert.AreEqual(SurfaceType.Water, traj.terrainHits[0].Surface, "TerrainHit surface must be Water");
            Assert.IsTrue(traj.terrainHits[0].IsStop, "Water hit must be marked as stop");
        }

        // ── Test 7: Max bounces capped ───────────────────────────────────────────

        [Test]
        public void Surface_MaxBounces_Capped()
        {
            // Synthetic high-restitution surface (Cr=0.95) that would bounce forever.
            var cfg = SurfaceConfig.Default;
            for (int i = 0; i < cfg.Coefficients.Length; i++)
            {
                var c = cfg.Coefficients[i];
                c.Restitution     = fp.FromFloat(0.95f);
                c.TangentFriction = fp.Zero;
                c.StopSpeed       = fp.FromFloat(0.001f); // nearly zero so it never stops naturally
                cfg.Coefficients[i] = c;
            }

            var input = new ShotInput(
                new fp3(fp.Zero, fp.FromInt(20), fp.Zero),
                fp3.Zero, fp.FromInt(60), // generous duration
                new SpinState(fp3.Zero, fp.Zero));

            var startTime = System.DateTime.UtcNow;
            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero), AeroConfig.Vacuum,
                WindConfig.Calm, new ConstantSurfaceProvider(SurfaceType.Fairway), cfg);
            double elapsed = (System.DateTime.UtcNow - startTime).TotalSeconds;

            Assert.AreEqual(TerminationReason.MaxBouncesExceeded, traj.termination,
                "Must terminate with MaxBouncesExceeded, not infinite loop");
            Assert.Less(elapsed, 5.0, $"Test must complete in <5s; took {elapsed:F2}s");
        }

        // ── Test 8: Bilinear interpolation sub-cell precision ─────────────────────

        [Test]
        public void Surface_Heightmap_BilinearInterpolation_SubCellPrecision()
        {
            // 3×3 heightmap with known values.
            // Grid: resolution=3, size=2×2 m (cell = 1 m), origin=(0,0,0)
            // heights[z,x]: h(0,0)=0, h(0,1)=1, h(0,2)=2, h(1,0)=1, h(1,1)=2, h(1,2)=3...
            // Pattern: h = x + z (in grid coords)
            int res = 3;
            var heights = new int[res * res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                heights[iz * res + ix] = (int)System.Math.Round((ix + iz) * 65536.0);

            var hmap = new HeightmapData(res,
                fp.FromFloat(2f), fp.FromFloat(2f), // 2m × 2m
                fp.Zero, fp.Zero, fp.Zero,
                heights);

            // At cell centers: h(0,0)=0, h(1m,0)=1, h(0,1m)=1, h(1m,1m)=2 (in world coords, gx=gz=1)
            fp h00 = hmap.SampleHeight(fp.Zero, fp.Zero);
            Assert.AreEqual(0.0, h00.ToDouble(), 1e-4, "h(0,0) must be 0");

            fp h10 = hmap.SampleHeight(fp.FromFloat(1f), fp.Zero);
            Assert.AreEqual(1.0, h10.ToDouble(), 1e-4, "h(1,0) must be 1");

            fp h01 = hmap.SampleHeight(fp.Zero, fp.FromFloat(1f));
            Assert.AreEqual(1.0, h01.ToDouble(), 1e-4, "h(0,1) must be 1");

            fp h11 = hmap.SampleHeight(fp.FromFloat(1f), fp.FromFloat(1f));
            Assert.AreEqual(2.0, h11.ToDouble(), 1e-4, "h(1,1) must be 2");

            // Midpoint between (0,0) and (1m,0): expected = 0.5
            fp hMid = hmap.SampleHeight(fp.FromFloat(0.5f), fp.Zero);
            Assert.AreEqual(0.5, hMid.ToDouble(), 1e-4, "Bilinear midpoint (0.5,0) must be 0.5");

            // Midpoint between (0,0) and (0,1m): expected = 0.5
            fp hMidZ = hmap.SampleHeight(fp.Zero, fp.FromFloat(0.5f));
            Assert.AreEqual(0.5, hMidZ.ToDouble(), 1e-4, "Bilinear midpoint (0,0.5) must be 0.5");

            // Center of all four: expected = 1.0
            fp hCenter = hmap.SampleHeight(fp.FromFloat(0.5f), fp.FromFloat(0.5f));
            Assert.AreEqual(1.0, hCenter.ToDouble(), 1e-4, "Bilinear center (0.5,0.5) must be 1.0");
        }
    }
}
