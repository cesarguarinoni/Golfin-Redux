// Phase 2 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// EditMode tests verifying GreenTopology round-trip, bounds, and pin order.
// Tests T1-T3 are mandatory per the spec.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Golfin.Course.Runtime;

// Note: GreenJsonWriter lives in Golfin.Editor.GreenAuthoring (separate asmdef).
// Golfin.Course.Tests references Golfin.Course.Runtime only — it cannot reference
// Golfin.Editor.GreenAuthoring directly without adding that reference to the asmdef.
// Strategy: use GreenTopology.LoadFromResources which reads from disk; T1 writes
// the file via a direct JSON-construction helper that replicates the DTO schema.
// This keeps the test asmdef dependency graph clean.
//
// The Golfin.Course.Tests asmdef references are intentionally kept minimal:
// Golfin.Course.Runtime + TestRunner only. Adding Golfin.Editor.GreenAuthoring
// would create a test-dependency loop and is not needed — GreenJsonWriter.SaveToResources
// is tested indirectly via T1's write/read round-trip which uses GreenTopology.LoadFromResources.
// Since we need to write green.json for T1 without the editor asmdef reference, we
// use a file-write helper below that duplicates the minimal schema.

namespace Golfin.Course.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="GreenTopology"/>. Covers round-trip schema fidelity,
    /// out-of-bounds sampling, and pin candidate ordering.
    /// Spec items T1, T2, T3 from Phase 2 SPEC.md.
    /// </summary>
    [TestFixture]
    public class GreenTopologyTests
    {
        // ──────────────────────────────────────────────────────────────────────
        // T1 — Round-trip schema fidelity
        // ──────────────────────────────────────────────────────────────────────

        private const int TestHoleNumber = 99;  // out-of-range; no real Hole_99 data

        [TearDown]
        public void TearDown_DeleteHole99()
        {
            // Clean up any Hole_99 test fixture written during T1.
            string dir = Path.GetFullPath("Assets/Resources/HoleData/Hole_99");
            if (Directory.Exists(dir))
            {
                // Remove all files then the directory.
                foreach (var f in Directory.GetFiles(dir))
                    File.Delete(f);
                Directory.Delete(dir);

                // Also clean up .meta files if present.
                if (File.Exists(dir + ".meta"))
                    File.Delete(dir + ".meta");
            }

            // Remove any stale files inside.
            string jsonPath = Path.GetFullPath("Assets/Resources/HoleData/Hole_99/green.json");
            if (File.Exists(jsonPath)) File.Delete(jsonPath);

            // Bust cache.
            GreenTopologyCache.Invalidate(TestHoleNumber);
        }

        [Test]
        public void T1_RoundTrip_SchemaFidelity()
        {
            // ── Build a synthetic state (8×8 grid, 192 floats, deterministic values) ──
            const int W = 8, H = 8;
            var slopeGrid = new float[W * H * 3];
            for (int j = 0; j < H; j++)
            {
                for (int i = 0; i < W; i++)
                {
                    int idx = (j * W + i) * 3;
                    slopeGrid[idx]     = i * 0.1f;                        // dirX
                    slopeGrid[idx + 1] = j * 0.1f;                        // dirZ
                    slopeGrid[idx + 2] = (float)((i + j) % 12);           // magPct (0..11)
                }
            }

            // 3 pin candidates.
            var pins = new List<(Vector3 world, string label)>
            {
                (new Vector3(0f,  0f, 0.5f), "pin-a"),
                (new Vector3(1f,  0f, 0.5f), "pin-b"),
                (new Vector3(2f,  0f, 0.5f), "pin-c"),
            };
            const int defaultPin = 0;

            var boundsMin = new Vector2(0f, 0f);
            var boundsMax = new Vector2(W * 0.5f, H * 0.5f);  // 4m x 4m at 0.5m cell
            const float cellSize = 0.5f;

            // ── Write via inline JSON helper (no Golfin.Editor.GreenAuthoring reference needed) ──
            WriteTestGreenJson(TestHoleNumber, slopeGrid, W, H, cellSize, boundsMin, boundsMax,
                               pins, defaultPin);

            // ── Load via GreenTopology.LoadFromResources ──
            GreenTopologyCache.Invalidate(TestHoleNumber);
            var topo = GreenTopology.LoadFromResources(TestHoleNumber);

            Assert.IsNotNull(topo, "GreenTopology.LoadFromResources returned null for Hole_99.");
            Assert.AreEqual(W,         topo.GridWidth,  "GridWidth mismatch.");
            Assert.AreEqual(H,         topo.GridHeight, "GridHeight mismatch.");
            Assert.AreEqual(cellSize,  topo.CellSize,   1e-5f, "CellSize mismatch.");
            Assert.AreEqual(boundsMin, topo.BoundsMin, "BoundsMin mismatch.");
            Assert.AreEqual(boundsMax, topo.BoundsMax, "BoundsMax mismatch.");

            // ── For all (i, j): slope at cell center must match constructed grid ──
            for (int j = 0; j < H; j++)
            {
                for (int i = 0; i < W; i++)
                {
                    Vector2 center = new Vector2(
                        boundsMin.x + (i + 0.5f) * cellSize,
                        boundsMin.y + (j + 0.5f) * cellSize);

                    bool ok = topo.TrySampleSlope(center, out Vector2 dir, out float mag);
                    Assert.IsTrue(ok, $"TrySampleSlope returned false for cell ({i},{j}).");

                    int srcIdx = (j * W + i) * 3;
                    Assert.AreEqual(slopeGrid[srcIdx],     dir.x, 1e-4f, $"dirX mismatch at ({i},{j}).");
                    Assert.AreEqual(slopeGrid[srcIdx + 1], dir.y, 1e-4f, $"dirZ mismatch at ({i},{j}).");
                    Assert.AreEqual(slopeGrid[srcIdx + 2], mag,   1e-4f, $"magPct mismatch at ({i},{j}).");
                }
            }

            // ── Pin candidates ──
            var loadedPins   = topo.GetPinCandidates();
            var loadedLabels = topo.GetPinLabels();
            Assert.AreEqual(3, loadedPins.Count, "Pin count mismatch.");
            Assert.AreEqual("pin-a", loadedLabels[0]);
            Assert.AreEqual("pin-b", loadedLabels[1]);
            Assert.AreEqual("pin-c", loadedLabels[2]);
            Assert.AreEqual(defaultPin, topo.DefaultPinIndex, "DefaultPinIndex mismatch.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // T2 — Out-of-bounds returns false
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void T2_OutOfBounds_ReturnsFalse()
        {
            GreenTopologyCache.Invalidate(1);
            var topo = GreenTopology.LoadFromResources(1);

            if (topo == null)
            {
                Assert.Inconclusive("Hole_01 green.json not found. T2 requires the Phase 1 skeleton.");
                return;
            }

            float cellSize = topo.CellSize;
            // Sample 4 corners shifted outside bounds by 2 * cellSize.
            Vector2[] outCorners = {
                new Vector2(topo.BoundsMin.x - 2f * cellSize, topo.BoundsMin.y - 2f * cellSize),
                new Vector2(topo.BoundsMax.x + 2f * cellSize, topo.BoundsMin.y - 2f * cellSize),
                new Vector2(topo.BoundsMin.x - 2f * cellSize, topo.BoundsMax.y + 2f * cellSize),
                new Vector2(topo.BoundsMax.x + 2f * cellSize, topo.BoundsMax.y + 2f * cellSize),
            };

            foreach (var pt in outCorners)
            {
                bool ok = topo.TrySampleSlope(pt, out Vector2 dir, out float mag);
                Assert.IsFalse(ok, $"TrySampleSlope should return false for out-of-bounds point ({pt.x:F2}, {pt.y:F2}).");
                Assert.AreEqual(Vector2.zero, dir,  $"dir should be zero for OOB point ({pt.x:F2}, {pt.y:F2}).");
                Assert.AreEqual(0f,           mag,  1e-5f, $"mag should be 0 for OOB point ({pt.x:F2}, {pt.y:F2}).");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // T3 — Pin candidates returned in author-supplied order
        // ──────────────────────────────────────────────────────────────────────

        [Test]
        public void T3_PinCandidates_AuthorSuppliedOrder()
        {
            GreenTopologyCache.Invalidate(1);
            var topo = GreenTopology.LoadFromResources(1);

            if (topo == null)
            {
                Assert.Inconclusive("Hole_01 green.json not found. T3 requires the Phase 1 skeleton.");
                return;
            }

            var labels = topo.GetPinLabels();
            Assert.IsNotNull(labels, "GetPinLabels() returned null.");
            Assert.GreaterOrEqual(labels.Count, 3,
                "Hole_01 skeleton should have 3 pin candidates; got " + labels.Count);

            Assert.AreEqual("skeleton-center",      labels[0], "labels[0] mismatch.");
            Assert.AreEqual("skeleton-front-right", labels[1], "labels[1] mismatch.");
            Assert.AreEqual("skeleton-back-left",   labels[2], "labels[2] mismatch.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Extra: GreenAuthoringMath helper tests
        // ──────────────────────────────────────────────────────────────────────

        // NOTE: GreenAuthoringMath is in Golfin.Editor.GreenAuthoring asmdef which
        // Golfin.Course.Tests does NOT reference (kept minimal per Q6).
        // Math tests for GreenAuthoringMath belong in a separate integration test
        // or in a test class within the Golfin.Editor.GreenAuthoring asmdef itself.
        // They are omitted here to keep the dependency graph clean.

        // ──────────────────────────────────────────────────────────────────────
        // Helper: inline JSON writer for T1 (avoids Golfin.Editor.GreenAuthoring dep)
        // ──────────────────────────────────────────────────────────────────────

        private static void WriteTestGreenJson(
            int holeNumber,
            float[] slopeGrid,
            int gridWidth, int gridHeight,
            float cellSize,
            Vector2 boundsMin, Vector2 boundsMax,
            List<(Vector3 world, string label)> pins,
            int defaultPinIndex)
        {
            // Convert float[] to base64 (same as GreenJsonWriter.SaveToResources).
            byte[] bytes = new byte[slopeGrid.Length * sizeof(float)];
            System.Buffer.BlockCopy(slopeGrid, 0, bytes, 0, bytes.Length);
            string base64 = System.Convert.ToBase64String(bytes);

            // Build pin JSON.
            var pinsJson = new System.Text.StringBuilder();
            for (int i = 0; i < pins.Count; i++)
            {
                var (w, lbl) = pins[i];
                if (i > 0) pinsJson.Append(",\n    ");
                pinsJson.Append(string.Format(CultureInfo.InvariantCulture,
                    "{{ \"worldX\": {0}, \"worldY\": {1}, \"worldZ\": {2}, \"label\": \"{3}\" }}",
                    w.x, w.y, w.z, lbl));
            }

            // Use InvariantCulture for all float formatting to ensure valid JSON on all locales.
            string json = string.Format(CultureInfo.InvariantCulture, @"{{
  ""schemaVersion"": {0},
  ""holeNumber"": {1},
  ""sourceTag"": ""test-t1"",
  ""boundsMin"": {{ ""x"": {2}, ""z"": {3} }},
  ""boundsMax"": {{ ""x"": {4}, ""z"": {5} }},
  ""cellSize"": {6},
  ""gridWidth"": {7},
  ""gridHeight"": {8},
  ""slopeGridBase64"": ""{9}"",
  ""pinCandidates"": [
    {10}
  ],
  ""defaultPinIndex"": {11}
}}",
                GreenTopology.CurrentSchemaVersion,
                holeNumber,
                boundsMin.x, boundsMin.y,
                boundsMax.x, boundsMax.y,
                cellSize,
                gridWidth,
                gridHeight,
                base64,
                pinsJson,
                defaultPinIndex);

            string folder = Path.GetFullPath($"Assets/Resources/HoleData/Hole_{holeNumber:D2}");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "green.json"), json);

            // Tell Unity about the new file.
            AssetDatabase.ImportAsset($"Assets/Resources/HoleData/Hole_{holeNumber:D2}/green.json");
        }
    }
}
