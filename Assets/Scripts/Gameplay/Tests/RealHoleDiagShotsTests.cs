using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Phase A3 — Real-scene diagnostic shots.
    ///
    /// Fires 4 scripted shots against Hole_01_Geo loaded additively.
    /// Per-step CSVs are written to Docs/DIAG/realtest-20260425/ for Architect review.
    ///
    /// PASS CRITERIA: All 4 shots run without exceptions.
    /// Fall-through analysis is done by Architect from the CSV data.
    /// </summary>
    [TestFixture]
    public class RealHoleDiagShotsTests
    {
        // Static so all test instances share scene + configs (NUnit creates new instance per test).
        static Scene        s_HoleScene;
        static string       s_ScenePath;
        static AeroConfig   s_Aero;
        static WindConfig   s_Wind;
        static SurfaceConfig s_SurfCfg;
        static PuttConfig   s_PuttCfg;

        static string DiagDir => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Docs", "DIAG", "realtest-20260425"));

        [OneTimeSetUp]
        public static void LoadScene()
        {
            Directory.CreateDirectory(DiagDir);

            s_Aero    = PhysicsConfigLoader.LoadAeroConfig();
            s_Wind    = PhysicsConfigLoader.LoadWindConfig();
            s_SurfCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            s_PuttCfg = PhysicsConfigLoader.LoadPuttConfig();

            string[] guids = AssetDatabase.FindAssets("t:Scene Hole_01_Geo");
            if (guids.Length == 0) { Assert.Inconclusive("Hole_01_Geo not found."); return; }

            s_ScenePath = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.Contains("Video")) { s_ScenePath = p; break; }
            }
            if (s_ScenePath == null) { Assert.Inconclusive("Cannot resolve Hole_01_Geo path."); return; }

            s_HoleScene = EditorSceneManager.OpenScene(s_ScenePath, OpenSceneMode.Additive);
            if (!s_HoleScene.IsValid()) Assert.Inconclusive("OpenScene returned invalid scene.");
        }

        [OneTimeTearDown]
        public static void UnloadScene()
        {
            BallSimulation.DiagPerStepEnabled = false;
            BallSimulation.DiagPerStepSink    = null;
            SceneGroundProvider.DiagHitSink   = null;
            if (s_HoleScene.IsValid())
                EditorSceneManager.CloseScene(s_HoleScene, true);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        static bool SceneProvidersActive()
        {
            var ground = new SceneGroundProvider();
            for (float x = -300f; x <= 300f; x += 40f)
            for (float z = -300f; z <= 300f; z += 40f)
            {
                float y = ground.SampleHeight(fp.FromFloat(x), fp.FromFloat(z)).ToFloat();
                if (Mathf.Abs(y) > 0.1f) return true;
            }
            return false;
        }

        static fp3 FindPositionOnSurface(SurfaceType target, float step = 5f)
        {
            var surfaces = new SceneSurfaceProvider();
            var ground   = new SceneGroundProvider();
            for (float x = -300f; x <= 300f; x += step)
            for (float z = -300f; z <= 300f; z += step)
            {
                fp fx = fp.FromFloat(x), fz = fp.FromFloat(z);
                if (surfaces.Classify(fx, fz) == target)
                {
                    float y = ground.SampleHeight(fx, fz).ToFloat();
                    if (Mathf.Abs(y) > 0.01f)
                        return new fp3(fx, fp.FromFloat(y + 0.02f), fz);
                }
            }
            return fp3.Zero;
        }

        static Trajectory RunDiagShot(int shotNumber, fp3 origin, fp3 velocity)
        {
            string stepPath = Path.Combine(DiagDir, $"A3-shot-{shotNumber}.csv");
            string hitsPath = Path.Combine(DiagDir, $"A3-shot-{shotNumber}-hits.csv");

            using (var stepW = new StreamWriter(stepPath, append: false))
            using (var hitsW = new StreamWriter(hitsPath, append: false))
            {
                stepW.WriteLine("frame,phase,surface,ballX,ballY,ballZ,groundY2arg,groundY3arg");
                hitsW.WriteLine("frame,worldX,worldZ,preferred,foundPreferred,nHits,hits");

                BallSimulation.DiagPerStepSink  = line => stepW.WriteLine(line);
                SceneGroundProvider.DiagHitSink = line => hitsW.WriteLine(line);
                BallSimulation.DiagStepFrame    = 0;
                BallSimulation.DiagPerStepEnabled = true;

                var ground   = new SceneGroundProvider();
                var surfaces = new SceneSurfaceProvider();
                var input    = new ShotInput(origin, velocity, fp.FromInt(60));
                var traj     = BallSimulation.Simulate(input, ground, s_Aero, s_Wind,
                                   surfaces, s_SurfCfg, s_PuttCfg, BallPhysicsModifiers.Neutral);

                BallSimulation.DiagPerStepEnabled = false;
                BallSimulation.DiagPerStepSink    = null;
                SceneGroundProvider.DiagHitSink   = null;
                return traj;
            }
        }

        static void AppendToSummary(string line)
        {
            string path = Path.Combine(DiagDir, "A3-summary.md");
            File.AppendAllText(path, line + "\n");
        }

        // ── Tests ─────────────────────────────────────────────────────────────────────

        [Test, Order(0)]
        public void A3_UseSceneProviders_Check()
        {
            bool active = SceneProvidersActive();
            string msg = $"_useSceneProviders equivalent: {active}";
            File.WriteAllText(Path.Combine(DiagDir, "A3-summary.md"), "# A3 Summary\n\n");
            AppendToSummary(msg);
            Debug.Log($"[A3] {msg}");
            if (!active)
                Assert.Fail("SceneGroundProvider returns zero Y at all sampled positions — scene geometry not loaded.");
        }

        [Test, Order(1)]
        public void A3_Shot1_Rough()
        {
            fp3 origin = FindPositionOnSurface(SurfaceType.Rough);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Semirough);
            AppendToSummary($"Shot 1 origin: {(IsZero(origin) ? "FALLBACK(0,10,0)" : $"{origin.x.ToFloat():F2},{origin.y.ToFloat():F2},{origin.z.ToFloat():F2}")}");
            if (IsZero(origin)) origin = new fp3(fp.Zero, fp.FromInt(10), fp.Zero);

            float spd = 30f, pitch = 20f * Mathf.Deg2Rad;
            var vel = new fp3(fp.FromFloat(spd * Mathf.Cos(pitch)), fp.FromFloat(spd * Mathf.Sin(pitch)), fp.Zero);
            var traj = RunDiagShot(1, origin, vel);

            AppendToSummary($"Shot 1: {traj.termination}, {traj.samples.Count} frames");
            Assert.IsNotNull(traj, "Shot 1 trajectory null");
        }

        [Test, Order(2)]
        public void A3_Shot2_Putt_Green()
        {
            fp3 origin = FindPositionOnSurface(SurfaceType.Green);
            if (IsZero(origin)) { Assert.Inconclusive("No Green surface found in search area."); return; }

            AppendToSummary($"Shot 2 origin: {origin.x.ToFloat():F2},{origin.y.ToFloat():F2},{origin.z.ToFloat():F2}");
            var vel = new fp3(fp.FromFloat(5f), fp.Zero, fp.Zero);
            var traj = RunDiagShot(2, origin, vel);

            AppendToSummary($"Shot 2: {traj.termination}, {traj.samples.Count} frames");
            Assert.IsNotNull(traj, "Shot 2 trajectory null");
        }

        [Test, Order(3)]
        public void A3_Shot3_Approach_Green()
        {
            fp3 greenPos = FindPositionOnSurface(SurfaceType.Green);
            if (IsZero(greenPos)) { Assert.Inconclusive("No Green surface found."); return; }

            fp3 origin = FindPositionOnSurface(SurfaceType.Fairway);
            if (IsZero(origin))
                origin = new fp3(greenPos.x + fp.FromFloat(60f), greenPos.y + fp.FromFloat(2f), greenPos.z);

            float dx  = (greenPos.x - origin.x).ToFloat();
            float dz  = (greenPos.z - origin.z).ToFloat();
            float yaw = Mathf.Atan2(dz, dx);
            float spd = 40f, pitch = 40f * Mathf.Deg2Rad;
            var vel = new fp3(
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                fp.FromFloat(spd * Mathf.Sin(pitch)),
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Sin(yaw)));

            AppendToSummary($"Shot 3 origin: {origin.x.ToFloat():F2},{origin.y.ToFloat():F2},{origin.z.ToFloat():F2}");
            var traj = RunDiagShot(3, origin, vel);
            AppendToSummary($"Shot 3: {traj.termination}, {traj.samples.Count} frames");
            Assert.IsNotNull(traj, "Shot 3 trajectory null");
        }

        [Test, Order(4)]
        public void A3_Shot4_Fairway()
        {
            fp3 origin = FindPositionOnSurface(SurfaceType.Fairway);
            if (IsZero(origin)) { Assert.Inconclusive("No Fairway surface found."); return; }

            AppendToSummary($"Shot 4 origin: {origin.x.ToFloat():F2},{origin.y.ToFloat():F2},{origin.z.ToFloat():F2}");
            float spd = 70f, pitch = 12f * Mathf.Deg2Rad;
            var vel = new fp3(fp.FromFloat(spd * Mathf.Cos(pitch)), fp.FromFloat(spd * Mathf.Sin(pitch)), fp.Zero);
            var traj = RunDiagShot(4, origin, vel);
            AppendToSummary($"Shot 4: {traj.termination}, {traj.samples.Count} frames");
            Assert.IsNotNull(traj, "Shot 4 trajectory null");
        }

        // fp3 has no == operator; use component comparison against Zero sentinel.
        static bool IsZero(fp3 v) => v.x == fp.Zero && v.y == fp.Zero && v.z == fp.Zero;
    }
}
