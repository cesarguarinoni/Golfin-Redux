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
using Golfin.Physics.Viewer.Editor;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Canonical regression suite for the baked-data sim pivot
    /// (spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md).
    ///
    /// 24 per-direction fixtures (3 origin/club combinations × 8 cardinal
    /// directions). Invariant at every trajectory sample:
    ///   ball.Y &gt;= ground.SampleHeight(ball.x, ball.z) - 0.05
    /// flagged only after <see cref="SustainedFrameThreshold"/> consecutive
    /// sub-ground frames (anti-overshoot).
    ///
    /// 4 fixtures are currently marked Ignore pending the airborne ground-level
    /// detection fix — see <c>Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md</c>.
    /// The remaining 20 must continue to PASS unconditionally.
    /// </summary>
    [TestFixture]
    public class BakedPivotRegressionTests
    {
        const float InvariantTolerance = 0.05f;
        // "Fall through" is sustained sub-ground, not a single-frame integration
        // overshoot. The 240Hz airborne integrator can briefly clip ground Y
        // for one step on rapid descent; 3 consecutive frames = 12.5 ms =
        // unambiguous fall-through, not a step-boundary artifact.
        const int   SustainedFrameThreshold = 3;
        // Bunker test launches from the polygon EDGE in the shot direction so
        // the ball starts above the rim instead of behind it.
        const float BunkerEdgeOffsetMeters  = 1.5f;

        // build_size_diet Phase 2: the hole FOLDER — HoleDataIO picks zones.bytes (gzip) or a
        // legacy zones.json, so this fixture is indifferent to which one the tree carries.
        const string HoleFolder         = "Assets/Resources/HoleData/lomond-country-club/Hole_01";
        const string HeightmapBytesPath = "Assets/Resources/HoleData/lomond-country-club/Hole_01/heightmap.bytes";

        // Ignore reason linked from the 4 known-failing TestCases below.
        const string IgnoreReason = "Known-failing — see Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md (M3.5).";

        static Scene         s_HoleScene;
        static AeroConfig    s_Aero;
        static WindConfig    s_Wind;
        static SurfaceConfig s_SurfCfg;
        static PuttConfig    s_PuttCfg;

        static BakedZoneClassifier s_Classifier;
        static BakedHeightProvider s_Ground;

        // Aggregated per-fixture results for reporting.
        static readonly Dictionary<string, List<DirectionResult>> s_Results
            = new Dictionary<string, List<DirectionResult>>();

        static string DiagDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Docs", "DIAG", "baked-pivot"));

        // ── Setup / teardown ──────────────────────────────────────────────────

        [OneTimeSetUp]
        public static void LoadScene()
        {
            // Pre-clean + staged-scene window — see RealHoleTerrainTests.GlobalSetup for the
            // full rationale (hole_scene_leftover_v3). Same defect, smaller blast radius.
            int preCleaned = CaptureSceneSetup.CloseStagedHoleScenes("BakedPivotRegressionTests/pre-clean");
            if (preCleaned > 0)
                Debug.Log($"[BakedPivotRegressionTests] Pre-clean closed {preCleaned} leftover staged "
                        + "hole scene(s) from a previous run.");
            s_HoleScene = default;
            CaptureSceneSetup.BeginStagedSceneWindow("BakedPivotRegressionTests");

            Directory.CreateDirectory(DiagDir);
            s_Results.Clear();

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

            if (HoleDataIO.ZonesDiskPath(HoleFolder) == null)
            {
                Assert.Inconclusive($"no zones.bytes / zones.json in {HoleFolder}. "
                    + "Run GOLFIN > Tools > Bake Zone JSON (Active Hole) on Hole_01_Geo first.");
                return;
            }
            if (!File.Exists(HeightmapBytesPath))
            {
                Assert.Inconclusive($"heightmap.bytes not baked at {HeightmapBytesPath}.");
                return;
            }

            var data = ZoneData.FromJson(HoleDataIO.LoadZonesTextFromDisk(HoleFolder));
            s_Classifier = new BakedZoneClassifier(data);
            var hm = HeightmapLoader.LoadFromBytes(File.ReadAllBytes(HeightmapBytesPath));
            if (hm == null) Assert.Inconclusive($"Heightmap parse failed for {HeightmapBytesPath}.");
            s_Ground = new BakedHeightProvider(hm, s_Classifier);
        }

        [OneTimeTearDown]
        public static void UnloadScene()
        {
            // Write per-fixture reports from the aggregated results dict.
            foreach (var kv in s_Results)
                WriteReport(kv.Key, kv.Value);

            // Scan-based close, NOT `if (s_HoleScene.IsValid())` — a domain reload wipes the
            // static while the scene stays open, and the old teardown then closed nothing and
            // reported success (hole_scene_leftover_v3).
            int closed = CaptureSceneSetup.CloseStagedHoleScenes("BakedPivotRegressionTests/teardown");
            Debug.Log($"[BakedPivotRegressionTests] Teardown closed {closed} staged hole scene(s); "
                    + $"{SceneManager.sceneCount} scene(s) remain open.");
            s_HoleScene = default;
            CaptureSceneSetup.EndStagedSceneWindow();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

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

        /// <summary>Wedge: ~35 m/s @ 40° pitch — clean rim clearance from bunker edges.</summary>
        static fp3 MakeWedgeVelocity(float yawDeg)
        {
            float yaw   = yawDeg * Mathf.Deg2Rad;
            float pitch = 40f * Mathf.Deg2Rad;
            float spd   = 35f;
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

        struct DirectionResult
        {
            public string label;
            public float  yawDeg;
            public bool   pass;
            public int    violatingFrame;
            public float  ballY;
            public float  groundY;
            public float  minBallY;
            public TerminationReason termination;
            public int    sampleCount;
        }

        static DirectionResult RunOneShot(Vector3 originWorld, float originY, fp3 velocity,
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

            int subGroundStreak = 0;
            int subGroundStart  = -1;
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
                    if (subGroundStreak == 0) subGroundStart = i;
                    subGroundStreak++;
                    if (subGroundStreak >= SustainedFrameThreshold)
                    {
                        result.pass           = false;
                        result.violatingFrame = subGroundStart;
                        result.ballY          = by;
                        result.groundY        = groundY;
                        break;
                    }
                }
                else
                {
                    subGroundStreak = 0;
                    subGroundStart  = -1;
                }
            }

            if (result.minBallY == float.MaxValue) result.minBallY = 0f;
            return result;
        }

        /// <summary>
        /// Shared per-direction test runner. Locates the origin GO, computes the
        /// (possibly edge-offset) launch position, fires the shot, stores the
        /// result for the per-fixture report, and asserts the direction passed.
        /// </summary>
        static void RunDirection(string fixtureName, string originGoName,
                                 System.Func<float, fp3> velFn,
                                 string label, float yawDeg, float edgeOffset = 0f)
        {
            var go = FindByName(originGoName);
            Assert.IsNotNull(go, $"{fixtureName} {label}: '{originGoName}' not found.");
            Vector3 centroid = CentroidXZ(go);

            float yawRad = yawDeg * Mathf.Deg2Rad;
            Vector3 origin = edgeOffset > 0f
                ? new Vector3(
                      centroid.x + Mathf.Cos(yawRad) * edgeOffset,
                      centroid.y,
                      centroid.z + Mathf.Sin(yawRad) * edgeOffset)
                : centroid;

            float originY = s_Ground.SampleHeight(
                fp.FromFloat(origin.x), fp.FromFloat(origin.z)).ToFloat();
            if (Mathf.Abs(originY) < 0.01f) originY = origin.y;

            var r = RunOneShot(origin, originY, velFn(yawDeg),
                               yawDeg, label, s_Ground, s_Classifier);

            if (!s_Results.TryGetValue(fixtureName, out var list))
            {
                list = new List<DirectionResult>();
                s_Results[fixtureName] = list;
            }
            list.Add(r);

            Assert.IsTrue(r.pass,
                $"{fixtureName} {label}: ball.Y dropped >0.05m below "
              + $"BakedHeightProvider.SampleHeight for {SustainedFrameThreshold}+ consecutive frames "
              + $"starting at frame {r.violatingFrame} (ballY={r.ballY:F3}, groundY={r.groundY:F3}). "
              + $"See Docs/DIAG/baked-pivot/M0-regression-{fixtureName}.md.");
        }

        static void WriteReport(string fixtureName, List<DirectionResult> results)
        {
            string path = Path.Combine(DiagDir, $"M0-regression-{fixtureName}.md");
            var sb = new StringBuilder();
            sb.AppendLine($"# Regression — {fixtureName}");
            sb.AppendLine();
            sb.AppendLine($"- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)");
            sb.AppendLine($"- Invariant tolerance: {InvariantTolerance:F3} m");
            sb.AppendLine($"- Sustained-frame threshold: {SustainedFrameThreshold}");
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

        // ── Tests: WedgeFromBunkerEdge (8 directions, 2 ignored) ──────────────

        [TestCase("N",   0f)]
        [TestCase("NE",  45f)]
        [TestCase("E",   90f)]
        [TestCase("SE",  135f)]
        [TestCase("S",   180f)]
        [TestCase("SW",  225f)]
        [TestCase("W",   270f)]
        [TestCase("NW",  315f)]
        public void RegressionTest_WedgeFromBunkerEdge_DoesNotFallThrough(string label, float yawDeg)
        {
            RunDirection("WedgeFromBunkerEdge", "Bunker_1", MakeWedgeVelocity,
                         label, yawDeg, BunkerEdgeOffsetMeters);
        }

        // ── Tests: PutterFromGreen (8 directions, none ignored) ───────────────

        [TestCase("N",   0f)]
        [TestCase("NE",  45f)]
        [TestCase("E",   90f)]
        [TestCase("SE",  135f)]
        [TestCase("S",   180f)]
        [TestCase("SW",  225f)]
        [TestCase("W",   270f)]
        [TestCase("NW",  315f)]
        public void RegressionTest_PutterFromGreen_StaysOnGreen(string label, float yawDeg)
        {
            RunDirection("PutterFromGreen", "Green_1", MakePutterVelocity,
                         label, yawDeg);
        }

        // ── Tests: DriverFromGreen (8 directions, 2 ignored) ──────────────────

        [TestCase("N",   0f)]
        [TestCase("NE",  45f)]
        [TestCase("E",   90f)]
        [TestCase("SE",  135f)]
        [TestCase("S",   180f)]
        [TestCase("SW",  225f)]
        [TestCase("W",   270f)]
        [TestCase("NW",  315f)]
        public void RegressionTest_DriverFromGreen_StaysOnGreen(string label, float yawDeg)
        {
            RunDirection("DriverFromGreen", "Green_1", MakeDriverVelocity,
                         label, yawDeg);
        }
    }
}
