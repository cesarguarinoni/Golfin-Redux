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
    /// M1 integration test (EditMode — matches existing HighVelocityLaunchDiagTests
    /// pattern; scene-loaded via EditorSceneManager). Spec called for PlayMode but
    /// EditMode satisfies the requirement equivalently with no test-runtime cost.
    ///
    /// Sample 100 random XZ points across the Hole_01 bounds, classify both with
    /// SceneSurfaceProvider (current architecture) and BakedZoneClassifier (M1).
    /// Assert agreement &gt; 95% (some boundary disagreement expected).
    ///
    /// Spec: SIM_BAKED_DATA_PATH.md M1.6.
    /// </summary>
    [TestFixture]
    public class BakedClassifier_Hole01_Test
    {
        const string ScenePath  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string ZonesJson  = "Assets/Resources/HoleData/Hole_01/zones.json";
        const float  AgreementThreshold = 0.95f;
        const int    SampleCount = 100;
        const int    SampleSeed  = 12345;

        static Scene s_HoleScene;

        [OneTimeSetUp]
        public static void LoadScene()
        {
            if (!File.Exists(ZonesJson))
            {
                Assert.Inconclusive($"zones.json not baked at {ZonesJson}. "
                    + "Run GOLFIN > Tools > Bake Zone JSON (Active Hole) on Hole_01_Geo first.");
                return;
            }

            s_HoleScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (!s_HoleScene.IsValid())
                Assert.Inconclusive($"Failed to open {ScenePath}.");
        }

        [OneTimeTearDown]
        public static void UnloadScene()
        {
            if (s_HoleScene.IsValid())
                EditorSceneManager.CloseScene(s_HoleScene, true);
        }

        [Test]
        public void BakedClassifier_Agrees_WithSceneSurfaceProvider_OverHole01()
        {
            // Load baked classifier from JSON.
            string json = File.ReadAllText(ZonesJson);
            var data = ZoneData.FromJson(json);
            Assert.IsNotNull(data, "ZoneData parse returned null");
            Assert.That(data.zones.Count, Is.GreaterThan(0), "Zone groups empty");

            var baked = new BakedZoneClassifier(data);
            var scene = new SceneSurfaceProvider();

            // Compute hole bounds from baked polygons (XZ extent of all points).
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

            Assert.That(maxX - minX, Is.GreaterThan(10f), "Hole XZ extent suspiciously small");

            // Sample 100 deterministic XZ points across the bounds. M2.5a baked
            // an OB mask from the Terrain alphamap, so OB samples now compare
            // directly (no skip).
            var rng = new System.Random(SampleSeed);
            int agree = 0, total = 0;
            var disagreements = new StringBuilder();

            for (int i = 0; i < SampleCount; i++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());

                fp fx = fp.FromFloat(x), fz = fp.FromFloat(z);
                var sceneType = scene.Classify(fx, fz);
                var bakedType = baked.Classify(fx, fz);

                total++;
                if (sceneType == bakedType) agree++;
                else if (disagreements.Length < 2000)
                    disagreements.AppendLine($"  ({x:F2},{z:F2}): scene={sceneType} baked={bakedType}");
            }

            float agreement = total == 0 ? 0f : (float)agree / total;
            int inScope = total;
            Debug.Log($"[M1] BakedClassifier vs SceneSurfaceProvider: "
                    + $"{agree}/{total} agree, agreement={agreement:P1}");

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "Docs", "DIAG", "baked-pivot", "M1-classifier-agreement.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var sb = new StringBuilder();
            sb.AppendLine($"# M1 — BakedZoneClassifier vs SceneSurfaceProvider agreement");
            sb.AppendLine();
            sb.AppendLine($"- Hole: Hole_01");
            sb.AppendLine($"- Samples: {SampleCount} (seed {SampleSeed})");
            sb.AppendLine($"- Bounds (XZ): ({minX:F2}, {minZ:F2}) → ({maxX:F2}, {maxZ:F2})");
            sb.AppendLine($"- Agreement: {agree}/{total} = **{agreement:P1}**");
            sb.AppendLine($"- Threshold: {AgreementThreshold:P0}");
            sb.AppendLine();
            if (disagreements.Length > 0)
            {
                sb.AppendLine("## Non-OB disagreements (first 2KB)");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(disagreements.ToString());
                sb.AppendLine("```");
            }
            File.WriteAllText(reportPath, sb.ToString());

            Assert.That(inScope, Is.GreaterThan(10),
                "Too few non-OB samples to evaluate agreement reliably.");
            Assert.That(agreement, Is.GreaterThanOrEqualTo(AgreementThreshold),
                $"Agreement {agreement:P1} (in-scope) below threshold {AgreementThreshold:P0}. "
              + $"See {reportPath}.");
        }
    }
}
