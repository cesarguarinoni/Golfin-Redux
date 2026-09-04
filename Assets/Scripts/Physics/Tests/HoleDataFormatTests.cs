using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;


namespace Golfin.Physics.Tests
{
    /// <summary>
    /// build_size_diet Phase 2 — the parity gate for the two hole-data formats, as tests rather
    /// than as a one-off console line from the converter.
    ///
    /// The converter already refused any hole whose round trip differed, but a converter runs
    /// once. These run every time: they are what catches the next person who "optimises" the
    /// delta scheme, or a Unity upgrade whose DeflateStream reads differently, or a hole
    /// re-baked by a tool that forgot the new writer.
    ///
    /// Nothing here tolerates approximation. The heightmap is Q16.16 fixed point read by an
    /// fp-deterministic simulation, so the assertion is int-for-int equality, not a tolerance.
    /// </summary>
    public class HoleDataFormatTests
    {
        const string ResourcesRoot = "Assets/Resources/HoleData";

        // ------------------------------------------------------------------ //
        // GHM1 / GHM2 — synthetic
        // ------------------------------------------------------------------ //

        /// <summary>A GHM1 file written the way PhysicsHeightmapBaker used to write one.</summary>
        static byte[] WriteGhm1(int res, float sx, float sz, float px, float py, float pz, int[] heights)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)'G'); bw.Write((byte)'H'); bw.Write((byte)'M'); bw.Write((byte)'1');
                bw.Write(1);
                bw.Write(res);
                bw.Write(sx); bw.Write(sz);
                bw.Write(px); bw.Write(py); bw.Write(pz);
                bw.Write(1);
                for (int i = 0; i < heights.Length; i++) bw.Write(heights[i]);
                bw.Flush();
                return ms.ToArray();
            }
        }

        static int[] SyntheticHeights(int res)
        {
            // Deliberately NOT smooth everywhere: a ramp (tiny deltas, the common case), a step
            // (one huge delta per row) and negatives (below the terrain origin). A codec that
            // only ever sees gentle terrain can hide a sign or overflow bug for a long time.
            var h = new int[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    h[y * res + x] = (x < res / 2)
                        ? (y * 977 + x * 131) - 40000
                        : (y * 977 + x * 131) + 5_000_000;
            return h;
        }

        [Test]
        public void Ghm1_AndGhm2_DecodeToTheSameInts_8x8()
        {
            const int res = 8;
            var heights = SyntheticHeights(res);

            var ghm1 = WriteGhm1(res, 100f, 200f, -1.5f, 0.25f, 3.75f, heights);
            Assert.IsTrue(HeightmapLoader.TryDecode(ghm1, out var a), "GHM1 did not decode.");
            CollectionAssert.AreEqual(heights, a.heights, "GHM1 decode changed the samples.");

            var ghm2 = HeightmapLoader.EncodeGhm2(a);
            Assert.AreEqual((byte)'2', ghm2[3], "EncodeGhm2 did not stamp the GHM2 magic.");
            Assert.IsTrue(HeightmapLoader.TryDecode(ghm2, out var b), "GHM2 did not decode.");

            CollectionAssert.AreEqual(heights, b.heights, "GHM2 round trip changed the samples.");
            Assert.AreEqual(a.res,   b.res);
            Assert.AreEqual(a.sizeX, b.sizeX);
            Assert.AreEqual(a.sizeZ, b.sizeZ);
            Assert.AreEqual(a.posX,  b.posX);
            Assert.AreEqual(a.posY,  b.posY);
            Assert.AreEqual(a.posZ,  b.posZ);
        }

        [Test]
        public void Ghm1_And_Ghm2_ProduceIdenticalHeightmapData_8x8()
        {
            const int res = 8;
            var heights = SyntheticHeights(res);
            var ghm1 = WriteGhm1(res, 100f, 200f, -1.5f, 0.25f, 3.75f, heights);
            HeightmapLoader.TryDecode(ghm1, out var d);
            var ghm2 = HeightmapLoader.EncodeGhm2(d);

            var h1 = HeightmapLoader.LoadFromBytes(ghm1);
            var h2 = HeightmapLoader.LoadFromBytes(ghm2);
            Assert.IsNotNull(h1); Assert.IsNotNull(h2);

            // Sample through the PUBLIC surface the simulation actually uses, so this would fail
            // even if the ints matched but the header were mis-read.
            for (int i = 0; i <= 16; i++)
            {
                var x = Golfin.Physics.Math.fp.FromFloat(i * 6f);
                var z = Golfin.Physics.Math.fp.FromFloat(i * 11f);
                Assert.AreEqual(h1.SampleHeight(x, z).raw, h2.SampleHeight(x, z).raw,
                    $"SampleHeight differs at sample {i}.");
            }
        }

        [Test]
        public void Ghm2_RejectsTruncatedPayload()
        {
            const int res = 8;
            var heights = SyntheticHeights(res);
            var ghm1 = WriteGhm1(res, 1f, 1f, 0f, 0f, 0f, heights);
            HeightmapLoader.TryDecode(ghm1, out var d);
            var ghm2 = HeightmapLoader.EncodeGhm2(d);
            var cut = ghm2.Take(ghm2.Length - 8).ToArray();

            LogAssert.ignoreFailingMessages = true;   // the loader logs the reason; the API contract is "false"
            Assert.IsFalse(HeightmapLoader.TryDecode(cut, out _), "A truncated GHM2 must not decode.");
            LogAssert.ignoreFailingMessages = false;
        }

        // ------------------------------------------------------------------ //
        // GHM2 — the SHIPPED holes
        // ------------------------------------------------------------------ //

        [Test]
        public void EveryShippedHeightmap_DecodesAndRoundTripsBitIdentically()
        {
            var files = Directory.Exists(ResourcesRoot)
                ? Directory.GetFiles(ResourcesRoot, "heightmap.bytes", SearchOption.AllDirectories).OrderBy(p => p).ToArray()
                : Array.Empty<string>();
            if (files.Length == 0) Assert.Inconclusive($"No heightmap.bytes under {ResourcesRoot}.");

            foreach (var path in files)
            {
                var bytes = File.ReadAllBytes(path);
                Assert.IsTrue(HeightmapLoader.TryDecode(bytes, out var a), $"{path} did not decode.");
                Assert.AreEqual(a.res * a.res, a.heights.Length, $"{path}: sample count != res².");

                var re = HeightmapLoader.EncodeGhm2(a);
                Assert.IsTrue(HeightmapLoader.TryDecode(re, out var b), $"{path}: re-encoded GHM2 did not decode.");
                CollectionAssert.AreEqual(a.heights, b.heights, $"{path}: GHM2 round trip is not lossless.");
            }
        }

        // ------------------------------------------------------------------ //
        // zones — the SHIPPED holes
        // ------------------------------------------------------------------ //

        [Test]
        public void EveryShippedZonesAsset_ParsesToNonEmptyZoneData()
        {
            var files = ZoneFiles();
            if (files.Length == 0) Assert.Inconclusive($"No zones asset under {ResourcesRoot}.");

            foreach (var path in files)
            {
                string json = HoleDataIO.DecodeZonesText(File.ReadAllBytes(path));
                Assert.IsNotNull(json, $"{path}: zones did not decode to text.");
                var data = ZoneData.FromJson(json);
                Assert.IsNotNull(data, $"{path}: ZoneData.FromJson returned null.");
                Assert.IsNotNull(data.zones, $"{path}: ZoneData.zones is null.");
                Assert.Greater(data.zones.Count, 0, $"{path}: ZoneData has no zone groups.");
            }
        }

        [Test]
        public void ZonesGzipRoundTrip_PreservesEveryZoneDataField_AllHoles()
        {
            var files = ZoneFiles();
            if (files.Length == 0) Assert.Inconclusive($"No zones asset under {ResourcesRoot}.");

            foreach (var path in files)
            {
                string json = HoleDataIO.DecodeZonesText(File.ReadAllBytes(path));
                var original = ZoneData.FromJson(json);

                // The exact transform the converter applied: minify -> gzip -> gunzip -> parse.
                string back = HoleDataIO.DecodeZonesText(HoleDataIO.EncodeZones(HoleDataIO.MinifyJson(json)));
                var round = ZoneData.FromJson(back);

                // Same comparison the converter gated on — not a re-implementation that could
                // agree with a bug.
                string diff = HoleDataIO.ZoneDataDiff(original, round);
                Assert.IsNull(diff, $"{path}: {diff}");
            }
        }

        [Test]
        public void DecodeZonesText_ReadsPlainJsonAsWellAsGzip()
        {
            const string json = "{\"holeId\":\"Hole_99\",\"zones\":[]}";
            var plain = new System.Text.UTF8Encoding(false).GetBytes(json);

            Assert.IsFalse(HoleDataIO.IsGzip(plain));
            Assert.AreEqual(json, HoleDataIO.DecodeZonesText(plain), "Plain JSON must pass through unchanged.");

            var gz = HoleDataIO.EncodeZones(json);
            Assert.IsTrue(HoleDataIO.IsGzip(gz), "EncodeZones must emit a gzip member.");
            Assert.AreEqual(json, HoleDataIO.DecodeZonesText(gz), "gzip round trip changed the text.");
        }

        [Test]
        public void MinifyJson_StripsWhitespaceOutsideStringsAndNothingElse()
        {
            const string pretty = "{\n  \"a\" : 1,\n  \"s\" : \"keep  me\\n and \\\" this\",\n  \"n\" : [ 1, 2 ]\n}";
            const string want   = "{\"a\":1,\"s\":\"keep  me\\n and \\\" this\",\"n\":[1,2]}";
            Assert.AreEqual(want, HoleDataIO.MinifyJson(pretty));
        }

        static string[] ZoneFiles()
        {
            if (!Directory.Exists(ResourcesRoot)) return Array.Empty<string>();
            var list = new List<string>();
            list.AddRange(Directory.GetFiles(ResourcesRoot, "zones.bytes", SearchOption.AllDirectories));
            list.AddRange(Directory.GetFiles(ResourcesRoot, "zones.json", SearchOption.AllDirectories));
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
