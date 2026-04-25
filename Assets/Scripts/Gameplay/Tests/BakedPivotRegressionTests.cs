using System.Collections.Generic;
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
    /// Canonical regression suite for the baked-data sim pivot
    /// (spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md).
    ///
    /// Three tests × 8 cardinal directions each. Invariant at every trajectory
    /// sample: ball.Y >= ground.SampleHeight(ball.x, ball.z) - 0.05.
    ///
    /// M0 wired this to SceneGroundProvider/SceneSurfaceProvider (current
    /// architecture) — expected FAIL on at least one direction.
    /// M3 rewires sim AND invariant to BakedHeightProvider/BakedZoneClassifier
    /// — expected PASS on all 24 directions.
    /// </summary>
    [TestFixture]
    public class BakedPivotRegressionTests
    {
        const float InvariantTolerance = 0.05f;

        const string ZonesJsonPath      = "Assets/Resources/HoleData/Hole_01/zones.json";
        const string HeightmapBytesPath = "Tools/UHoleGeo/output/lomond-country-club/export/hole-01/heightmap.bytes";

        static Scene         s_HoleScene;
        static AeroConfig    s_Aero;
        static WindConfig    s_Wind;
        static SurfaceConfig s_SurfCfg;
        static PuttConfig    s_PuttCfg;

        // M3: baked providers shared across tests (read-only after setup).
        static BakedZoneClassifier s_Classifier;
        static BakedHeightProvider s_Ground;

        static string DiagDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Docs", "DIAG", "baked-pivot"));

        [OneTimeSetUp]
        public static void LoadScene()
        {
            Directory.CreateDirectory(DiagDir);

            s_Aero    = PhysicsConfigLoader.LoadAeroConfig();
            s_Wind    = PhysicsConfigLoader.LoadWindConfig();
            s_SurfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            s_PuttCfg = PhysicsConfigLoader.LoadPuttConfig();

            string[] guids = AssetDatabase.FindAssets("t:Scene Hole_01_Geo");
            if (guids.Length == 0) { Assert.Inconclusive("Hole_01_Geo scene not found."); return; }

            string scenePath = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.Contains("Video")) { scenePath = p; break; }
            }
            if (scenePath == null) { Assert.Inconclusive("Cannot resolve Hole_01_Geo path."); return; }

            s_HoleScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!s_HoleScene.IsValid()) Assert.Inconclusive("OpenScene returned invalid scene.");

            // M3: load baked providers. Failure here renders the test inconclusive
            // (we can't validate the architecture if the data isn't baked).
            if (!File.Exists(ZonesJsonPath))
            {
                Assert.Inconclusive($"zones.json not baked at {ZonesJsonPath}. "
                    + "Run GOLFIN > Tools > Bake Zone JSON (Active Hole) on Hole_01_Geo first.");
                return;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string hmPath = Path.Combine(projectRoot, HeightmapBytesPath);
            if (!File.Exists(hmPath))
            {
                Assert.Inconclusive($"heightmap.bytes not baked at {hmPath}.");
                return;
            }

            var data = ZoneData.FromJson(File.ReadAllText(ZonesJsonPath));
            s_Classifier = new BakedZoneClassifier(data);
            var hm = HeightmapLoader.LoadFromBytes(File.ReadAllBytes(hmPath));
            if (hm == null) Assert.Inconclusive($"Heightmap parse failed for {hmPath}.");
            s_Ground = new BakedHeightProvider(hm, s_Classifier);
        }

        [OneTimeTearDown]
        public static void UnloadScene()
        {
            if (s_HoleScene.IsValid())
                EditorSceneManager.CloseScene(s_HoleScene, true);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Find GO by exact name within the hole scene; null if not present.</summary>
        static GameObject FindByName(string name)
        {
            if (!s_HoleScene.IsValid()) return null;
            foreach (var root in s_HoleScene.GetRootGameObjects())
            {
                GameObject found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject FindRecursive(Transform t, string name)
        {
            if (t.name == name) return t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindRecursive(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// World-space XZ centroid of a GO, derived from the combined renderer
        /// bounds of itself + all children. Falls back to transform.position if
        /// no renderer is found.
        /// </summary>
        static Vector3 CentroidXZ(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return go.transform.position;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.center;
        }

        static fp3 MakeDriverVelocity(float yawDeg)
        {
            float yaw   = yawDeg * Mathf.Deg2Rad;
            float pitch = 12f * Mathf.Deg2Rad;
            float spd   = 70f;
            return new fp3(
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                fp.FromFloat(spd * Mathf.Sin(pitch)),
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Sin(yaw)));
        }

        static fp3 MakePutterVelocity(float yawDeg)
        {
            float yaw   = yawDeg * Mathf.Deg2Rad;
            float pitch = 2f * Mathf.Deg2Rad;
            float spd   = 5f;
            return new fp3(
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                fp.FromFloat(spd * Mathf.Sin(pitch)),
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Sin(yaw)));
        }

        /// <summary>Direction yaws: N=0, NE=45, E=90, SE=135, S=180, SW=225, W=270, NW=315.</summary>
        static readonly float[] s_CardinalYaws = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        static readonly string[] s_CardinalLabels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        struct DirectionResult
        {
            public string label;
            public float yawDeg;
            public bool pass;
            public int violatingFrame;
            public float ballY;
            public float groundY;
            public float minBallY;
            public TerminationReason termination;
            public int sampleCount;
        }

        /// <summary>
        /// Run a single shot and check the invariant per-sample. Returns the first
        /// violation (or pass=true if all samples satisfy it).
        /// </summary>
        static DirectionResult RunAndCheck(Vector3 originWorld, float originY, fp3 velocity,
                                           float yawDeg, string label,
                                           IGroundProvider ground, ISurfaceProvider surfaces)
        {
            var ball0 = new fp3(
                fp.FromFloat(originWorld.x),
                fp.FromFloat(originY + 0.02f),
                fp.FromFloat(originWorld.z));

            var input = new ShotInput(ball0, velocity, fp.FromInt(60));
            var traj  = BallSimulation.Simulate(input, ground, s_Aero, s_Wind,
                                                surfaces, s_SurfCfg, s_PuttCfg,
                                                BallPhysicsModifiers.Neutral);

            var result = new DirectionResult
            {
                label           = label,
                yawDeg          = yawDeg,
                pass            = true,
                violatingFrame  = -1,
                ballY           = 0f,
                groundY         = 0f,
                minBallY        = float.MaxValue,
                termination     = traj.termination,
                sampleCount     = traj.samples.Count,
            };

            for (int i = 0; i < traj.samples.Count; i++)
            {
                var s = traj.samples[i];
                float bx = s.position.x.ToFloat();
                float by = s.position.y.ToFloat();
                float bz = s.position.z.ToFloat();
                if (by < result.minBallY) result.minBallY = by;

                float groundY = ground.SampleHeight(
                    fp.FromFloat(bx), fp.FromFloat(bz)).ToFloat();

                if (by < groundY - InvariantTolerance)
                {
                    result.pass           = false;
                    result.violatingFrame = i;
                    result.ballY          = by;
                    result.groundY        = groundY;
                    break;
                }
            }

            if (result.minBallY == float.MaxValue) result.minBallY = 0f;
            return result;
        }

        /// <summary>
        /// Shared driver for the 3 tests. Returns (passCount, failCount) and writes a
        /// per-test markdown report to M0-regression-&lt;label&gt;.md.
        /// </summary>
        static (int pass, int fail) Run8Directions(string testLabel, GameObject originGo,
                                                    System.Func<float, fp3> velFn)
        {
            Assert.IsNotNull(originGo, $"{testLabel}: origin GameObject not found in scene.");

            Vector3 centroid = CentroidXZ(originGo);

            // M3: sim AND invariant both use the baked providers loaded in OneTimeSetUp.
            // Snap origin Y to the baked surface (the visible mesh top via IDW).
            float groundY = s_Ground.SampleHeight(
                fp.FromFloat(centroid.x), fp.FromFloat(centroid.z)).ToFloat();
            if (Mathf.Abs(groundY) < 0.01f) groundY = centroid.y;

            var results = new List<DirectionResult>(8);
            for (int i = 0; i < s_CardinalYaws.Length; i++)
            {
                var r = RunAndCheck(centroid, groundY, velFn(s_CardinalYaws[i]),
                                    s_CardinalYaws[i], s_CardinalLabels[i],
                                    s_Ground, s_Classifier);
                results.Add(r);
            }

            int passCount = 0, failCount = 0;
            foreach (var r in results) { if (r.pass) passCount++; else failCount++; }

            WriteReport(testLabel, originGo.name, centroid, groundY, results);
            return (passCount, failCount);
        }

        static void WriteReport(string testLabel, string originName, Vector3 centroid,
                                float groundY, List<DirectionResult> results)
        {
            string path = Path.Combine(DiagDir, $"M0-regression-{testLabel}.md");
            var sb = new StringBuilder();
            sb.AppendLine($"# M0 Regression — {testLabel}");
            sb.AppendLine();
            sb.AppendLine($"- Origin GO: `{originName}`");
            sb.AppendLine($"- Centroid (world XZ): ({centroid.x:F3}, {centroid.z:F3})");
            sb.AppendLine($"- Ground Y at centroid (BakedHeightProvider): {groundY:F3}");
            sb.AppendLine($"- Invariant tolerance: {InvariantTolerance:F3} m");
            sb.AppendLine($"- Provider: BakedHeightProvider + BakedZoneClassifier (M3)");
            sb.AppendLine();
            sb.AppendLine("| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |");
            sb.AppendLine("|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|");
            foreach (var r in results)
            {
                sb.AppendLine($"| {r.label} | {r.yawDeg:F0} | {(r.pass ? "PASS" : "FAIL")} "
                            + $"| {(r.violatingFrame < 0 ? "-" : r.violatingFrame.ToString())} "
                            + $"| {r.ballY:F3} | {r.groundY:F3} | {r.minBallY:F3} "
                            + $"| {r.sampleCount} | {r.termination} |");
            }
            File.WriteAllText(path, sb.ToString());
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void RegressionTest_DriverFromBunker_DoesNotFallThrough()
        {
            var origin = FindByName("Bunker_1");
            Assert.IsNotNull(origin, "Bunker_1 not found in Hole_01_Geo.");

            var (pass, fail) = Run8Directions("DriverFromBunker", origin, MakeDriverVelocity);
            Debug.Log($"[M0] DriverFromBunker: {pass}/8 PASS, {fail}/8 FAIL.");
            Assert.AreEqual(0, fail,
                $"DriverFromBunker: {fail}/8 directions fell through under baked architecture "
              + "(ball.Y dropped >0.05m below BakedHeightProvider.SampleHeight). "
              + "See Docs/DIAG/baked-pivot/M0-regression-DriverFromBunker.md.");
        }

        [Test]
        public void RegressionTest_PutterFromGreen_StaysOnGreen()
        {
            var origin = FindByName("Green_1");
            Assert.IsNotNull(origin, "Green_1 not found in Hole_01_Geo.");

            var (pass, fail) = Run8Directions("PutterFromGreen", origin, MakePutterVelocity);
            Debug.Log($"[M0] PutterFromGreen: {pass}/8 PASS, {fail}/8 FAIL.");
            Assert.AreEqual(0, fail,
                $"PutterFromGreen: {fail}/8 directions fell through. "
              + "See Docs/DIAG/baked-pivot/M0-regression-PutterFromGreen.md.");
        }

        [Test]
        public void RegressionTest_DriverFromGreen_StaysOnGreen()
        {
            var origin = FindByName("Green_1");
            Assert.IsNotNull(origin, "Green_1 not found in Hole_01_Geo.");

            var (pass, fail) = Run8Directions("DriverFromGreen", origin, MakeDriverVelocity);
            Debug.Log($"[M0] DriverFromGreen: {pass}/8 PASS, {fail}/8 FAIL.");
            Assert.AreEqual(0, fail,
                $"DriverFromGreen: {fail}/8 directions fell through under baked architecture. "
              + "See Docs/DIAG/baked-pivot/M0-regression-DriverFromGreen.md.");
        }
    }
}
