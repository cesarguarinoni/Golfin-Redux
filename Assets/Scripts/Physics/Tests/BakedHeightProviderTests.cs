using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// M2 unit tests for <see cref="BakedHeightProvider"/>. Synthetic inputs only.
    /// Spec: SIM_BAKED_DATA_PATH.md M2.3.
    /// </summary>
    [TestFixture]
    public class BakedHeightProviderTests
    {
        private static fp F(float v) => fp.FromFloat(v);

        // ── HeightmapData stub ────────────────────────────────────────────────

        /// <summary>2×2 grid all set to <paramref name="constY"/>. Bilinear interp returns constY everywhere.</summary>
        private static HeightmapData FlatHeightmap(float constY)
        {
            int res = 2;
            int rawY = (int)(constY * 65536f); // Q16.16 raw
            int[] heights = new int[] { rawY, rawY, rawY, rawY };
            return new HeightmapData(
                res,
                F(100f), F(100f),  // sizeX, sizeZ
                F(-50f), F(0f), F(-50f), // originX, originY, originZ
                heights);
        }

        private static Polygon2D Square(float minX, float minZ, float maxX, float maxZ)
        {
            var p = new Polygon2D();
            p.points.Add(new Point2D(minX, minZ));
            p.points.Add(new Point2D(maxX, minZ));
            p.points.Add(new Point2D(maxX, maxZ));
            p.points.Add(new Point2D(minX, maxZ));
            return p;
        }

        // ── 1. Heightmap-only, no zones ───────────────────────────────────────

        [Test]
        public void SampleHeight_NoZones_ReturnsTerrainY()
        {
            var hm   = FlatHeightmap(10.0f);
            var data = new ZoneData { holeId = "EMPTY" };
            var clf  = new BakedZoneClassifier(data);
            var bh   = new BakedHeightProvider(hm, clf);

            Assert.AreEqual(10.0f, bh.SampleHeight(F(0), F(0)).ToFloat(), 1e-3f);
            Assert.AreEqual(10.0f, bh.SampleHeight(F(20), F(-15)).ToFloat(), 1e-3f);
        }

        // ── 2. Bunker zone with offset 0.02 ───────────────────────────────────

        [Test]
        public void SampleHeight_InsideSandZone_AddsZoneOffset()
        {
            var hm   = FlatHeightmap(10.0f);
            var data = new ZoneData { holeId = "SAND" };
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Sand.ToString(),
                yOffsetFromTerrain = 0.02f,
                polygons = { Square(-5, -5, 5, 5) },
            });
            var bh = new BakedHeightProvider(hm, new BakedZoneClassifier(data));

            Assert.AreEqual(10.02f, bh.SampleHeight(F(0), F(0)).ToFloat(),  1e-3f);
            Assert.AreEqual(10.00f, bh.SampleHeight(F(10), F(10)).ToFloat(), 1e-3f);
        }

        // ── 3. Green zone with offset 0.11 ────────────────────────────────────

        [Test]
        public void SampleHeight_InsideGreenZone_AddsGreenOffset()
        {
            var hm   = FlatHeightmap(10.0f);
            var data = new ZoneData { holeId = "GREEN" };
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Green.ToString(),
                yOffsetFromTerrain = 0.11f,
                polygons = { Square(-3, -3, 3, 3) },
            });
            var bh = new BakedHeightProvider(hm, new BakedZoneClassifier(data));

            Assert.AreEqual(10.11f, bh.SampleHeight(F(0), F(0)).ToFloat(),   1e-3f);
            Assert.AreEqual(10.00f, bh.SampleHeight(F(10), F(10)).ToFloat(), 1e-3f);
        }

        // ── 4. Overlapping zones — priority wins ──────────────────────────────

        [Test]
        public void SampleHeight_OverlappingGreenAndSand_GreenOffsetWins()
        {
            var hm   = FlatHeightmap(10.0f);
            var data = new ZoneData { holeId = "OVERLAP" };
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Sand.ToString(),
                yOffsetFromTerrain = 0.02f,
                polygons = { Square(-10, -10, 10, 10) },
            });
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Green.ToString(),
                yOffsetFromTerrain = 0.11f,
                polygons = { Square(-3, -3, 3, 3) },
            });
            var bh = new BakedHeightProvider(hm, new BakedZoneClassifier(data));

            // Inside both zones → Green offset (priority).
            Assert.AreEqual(10.11f, bh.SampleHeight(F(0), F(0)).ToFloat(),  1e-3f);
            // Outside Green, inside Sand → Sand offset.
            Assert.AreEqual(10.02f, bh.SampleHeight(F(7), F(7)).ToFloat(),  1e-3f);
            // Outside both → terrain only.
            Assert.AreEqual(10.00f, bh.SampleHeight(F(20), F(0)).ToFloat(), 1e-3f);
        }

        // ── 5. 3-arg "preferred" override ─────────────────────────────────────

        [Test]
        public void SampleHeight_3Arg_PreferredHigherOffset_PicksPreferred()
        {
            var hm   = FlatHeightmap(10.0f);
            var data = new ZoneData { holeId = "PREFER" };
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Sand.ToString(),
                yOffsetFromTerrain = 0.02f,
                polygons = { Square(-10, -10, 10, 10) },
            });
            // No Green polygon at this XZ, but the green offset is registered.
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Green.ToString(),
                yOffsetFromTerrain = 0.11f,
                polygons = { Square(50, 50, 60, 60) }, // Green far away.
            });
            var bh = new BakedHeightProvider(hm, new BakedZoneClassifier(data));

            // At (0,0): classifier says Sand. Caller prefers Green. We honour it.
            Assert.AreEqual(10.11f,
                bh.SampleHeight(F(0), F(0), SurfaceType.Green).ToFloat(), 1e-3f);

            // At (0,0): classifier says Sand. Caller prefers Fairway (lower, 0).
            // Result must NOT go below the actual Sand classification.
            Assert.AreEqual(10.02f,
                bh.SampleHeight(F(0), F(0), SurfaceType.Fairway).ToFloat(), 1e-3f);
        }

        // ── 6. Null robustness ────────────────────────────────────────────────

        [Test]
        public void SampleHeight_NullClassifier_ReturnsTerrainY()
        {
            var hm = FlatHeightmap(7.5f);
            var bh = new BakedHeightProvider(hm, null);
            Assert.AreEqual(7.5f, bh.SampleHeight(F(0), F(0)).ToFloat(), 1e-3f);
        }

        [Test]
        public void SampleHeight_NullHeightmap_ReturnsZeroPlusOffset()
        {
            var data = new ZoneData { holeId = "NOHM" };
            data.zones.Add(new ZonePolygonGroup
            {
                type = SurfaceType.Green.ToString(),
                yOffsetFromTerrain = 0.11f,
                polygons = { Square(-5, -5, 5, 5) },
            });
            var bh = new BakedHeightProvider(null, new BakedZoneClassifier(data));
            Assert.AreEqual(0.11f, bh.SampleHeight(F(0), F(0)).ToFloat(), 1e-3f);
        }
    }
}
