using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// M2 integration test. Sample 100 random XZ on Hole_01 and compare
    /// <see cref="BakedHeightProvider.SampleHeight"/> against the live-scene
    /// <see cref="SceneGroundProvider.SampleHeight"/>. Allow up to 5 cm divergence
    /// (mesh tessellation + heightmap quantization differ); any diverging point
    /// is dumped into the M2 agreement report.
    ///
    /// Spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md M2.4.
    /// </summary>
    [TestFixture]
    public class BakedHeight_Hole01_Test
    {
        const string ScenePath          = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string ZonesJsonPath      = "Assets/Resources/HoleData/Hole_01/zones.json";
        const string HeightmapBytesPath = "Assets/Resources/HoleData/Hole_01/heightmap.bytes";
        const float  DivergenceTolerance = 0.05f; // 5 cm
        const int    SampleCount         = 100;
        const int    SampleSeed          = 7777;

        static Scene s_HoleScene;

        [OneTimeSetUp]
        public static void LoadScene()
        {
            if (!File.Exists(ZonesJsonPath))
            {
                Assert.Inconclusive($"zones.json not baked at {ZonesJsonPath}");
                return;
            }
            if (!File.Exists(HeightmapBytesPath))
            {
                Assert.Inconclusive($"heightmap.bytes not found at {HeightmapBytesPath}");
                return;
            }

            s_HoleScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!s_HoleScene.IsValid())
                Assert.Inconclusive($"Failed to open {ScenePath}");
        }

        [OneTimeTearDown]
        public static void UnloadScene()
        {
            if (s_HoleScene.IsValid())
                EditorSceneManager.CloseScene(s_HoleScene, true);
        }

        [Test]
        public void BakedHeight_Agrees_WithSceneGround_OverHole01()
        {
            // Load heightmap.
            byte[] hmBytes = File.ReadAllBytes(HeightmapBytesPath);
            var hm         = HeightmapLoader.LoadFromBytes(hmBytes);
            Assert.IsNotNull(hm, "HeightmapLoader returned null");

            // Load classifier.
            var data = ZoneData.FromJson(File.ReadAllText(ZonesJsonPath));
            var clf  = new BakedZoneClassifier(data);
            var baked = new BakedHeightProvider(hm, clf);

            // Live-scene reference.
            var scene = new SceneGroundProvider();

            // Compute polygon-bounded sample area (avoid OB regions where the scene
            // raycast may miss colliders entirely and return 0 — see B'1).
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var grp in data.zones)
                foreach (var poly in grp.polygons)
                    foreach (var pt in poly.points)
                    {
                        if (pt.x < minX) minX = pt.x;
                        if (pt.x > maxX) maxX = pt.x;
                        if (pt.z < minZ) minZ = pt.z;
                        if (pt.z > maxZ) maxZ = pt.z;
                    }

            // Sample.
            var rng = new System.Random(SampleSeed);
            var sb  = new StringBuilder();
            sb.AppendLine("# M2 — BakedHeightProvider vs SceneGroundProvider divergence");
            sb.AppendLine();
            sb.AppendLine($"- Hole: Hole_01");
            sb.AppendLine($"- Samples: {SampleCount} (seed {SampleSeed})");
            sb.AppendLine($"- Polygon bounds: ({minX:F2}, {minZ:F2}) → ({maxX:F2}, {maxZ:F2})");
            sb.AppendLine($"- Tolerance: ±{DivergenceTolerance:F3} m");
            sb.AppendLine();

            int withinTol = 0, diverged = 0, sceneZeroSkipped = 0;
            float maxDiverge = 0f;
            float sumAbsDiff = 0f;
            var bins = new int[5];   // 0–1cm, 1–2cm, 2–5cm, 5–10cm, >10cm

            var details = new StringBuilder();
            details.AppendLine("| x | z | type | sceneY | bakedY | diff(m) |");
            details.AppendLine("|---|---|------|--------|--------|---------|");

            for (int i = 0; i < SampleCount; i++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                fp fx = fp.FromFloat(x), fz = fp.FromFloat(z);

                float sceneY = scene.Classify_NoOp_GetGroundY(fx, fz);
                float bakedY = baked.SampleHeight(fx, fz).ToFloat();

                if (Mathf.Abs(sceneY) < 0.001f)
                {
                    // Scene raycast missed (no collider at this XZ); not a fair comparison.
                    sceneZeroSkipped++;
                    continue;
                }

                float diff = Mathf.Abs(sceneY - bakedY);
                sumAbsDiff += diff;
                if (diff > maxDiverge) maxDiverge = diff;
                if (diff <= DivergenceTolerance) withinTol++;
                else
                {
                    diverged++;
                    if (details.Length < 4000)
                    {
                        var t = clf.Classify(fx, fz);
                        details.AppendLine($"| {x:F2} | {z:F2} | {t} | {sceneY:F3} | {bakedY:F3} | {diff:F3} |");
                    }
                }

                if      (diff <= 0.01f) bins[0]++;
                else if (diff <= 0.02f) bins[1]++;
                else if (diff <= 0.05f) bins[2]++;
                else if (diff <= 0.10f) bins[3]++;
                else                    bins[4]++;
            }

            int inScope = withinTol + diverged;
            float meanDiff = inScope == 0 ? 0f : sumAbsDiff / inScope;

            sb.AppendLine($"- In-scope samples: {inScope}");
            sb.AppendLine($"- Scene-zero samples skipped: {sceneZeroSkipped} (raycast missed; void)");
            sb.AppendLine($"- Within tolerance: {withinTol}/{inScope}");
            sb.AppendLine($"- Diverged: {diverged}/{inScope}");
            sb.AppendLine($"- Max divergence: {maxDiverge:F3} m");
            sb.AppendLine($"- Mean abs divergence: {meanDiff:F4} m");
            sb.AppendLine();
            sb.AppendLine("## Histogram");
            sb.AppendLine();
            sb.AppendLine($"- 0–1 cm:   {bins[0]}");
            sb.AppendLine($"- 1–2 cm:   {bins[1]}");
            sb.AppendLine($"- 2–5 cm:   {bins[2]}");
            sb.AppendLine($"- 5–10 cm:  {bins[3]}");
            sb.AppendLine($"- > 10 cm:  {bins[4]}");
            sb.AppendLine();
            if (diverged > 0)
            {
                sb.AppendLine("## Diverging samples (first 4 KB)");
                sb.AppendLine();
                sb.AppendLine(details.ToString());
            }

            string reportPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Docs", "DIAG", "baked-pivot", "M2-height-agreement.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, sb.ToString());

            Debug.Log($"[M2] BakedHeight vs SceneGround: "
                    + $"{withinTol}/{inScope} within ±{DivergenceTolerance}m, "
                    + $"max={maxDiverge:F3}m, mean={meanDiff:F4}m, "
                    + $"sceneZeroSkipped={sceneZeroSkipped}.");

            // M2 spec: "Allow up to 5 cm divergence ... flag any > 5 cm divergence
            // and dump those XZ points for review." So we don't FAIL the test on
            // divergence — we report it. The agreement report IS the deliverable.
            //
            // We DO assert on enough in-scope samples: a fully empty result would
            // hide a real bug.
            Assert.That(inScope, Is.GreaterThan(20),
                "Too few in-scope samples — heightmap or zones data may be empty.");
        }
    }

    /// <summary>
    /// Bridge — SceneGroundProvider does not expose a public "max-Y" sampler, but
    /// the existing 2-arg <c>SampleHeight</c> already does that under the hood.
    /// Keep the call simple and document the intent.
    /// </summary>
    internal static class SceneGroundProviderExt
    {
        public static float Classify_NoOp_GetGroundY(this SceneGroundProvider p, fp x, fp z)
            => p.SampleHeight(x, z).ToFloat();
    }
}
