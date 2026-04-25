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
    /// Phase B'1 — High-velocity launch diagnostic.
    ///
    /// 6 shots from Hole_01_Geo, all starting AT a depressed surface (bunker or green).
    /// Shots 1–4 use driver velocity (~70 m/s). Shots 5–6 are controls (wedge/putter).
    ///
    /// PASS CRITERIA: All 6 shots complete without exceptions.
    /// Fall-through analysis is done by Architect from the CSV data.
    ///
    /// Focuses on the LAUNCH MOMENT (first 30 sim frames).
    /// Per-step CSVs written to Docs/DIAG/realtest-20260425/Bprime-shot-{1..6}.csv + Bprime-summary.md.
    /// </summary>
    [TestFixture]
    public class HighVelocityLaunchDiagTests
    {
        static Scene         s_HoleScene;
        static string        s_ScenePath;
        static AeroConfig    s_Aero;
        static WindConfig    s_Wind;
        static SurfaceConfig s_SurfCfg;
        static PuttConfig    s_PuttCfg;

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

        static fp3 FindPositionOnSurfaceFiner(SurfaceType target)
            => FindPositionOnSurface(target, 2f);

        static fp3 MakeDriverVelocity(float yawDeg)
        {
            // Driver: ~70 m/s at 12° launch, pointed at yawDeg (0 = +X)
            float yaw   = yawDeg * Mathf.Deg2Rad;
            float pitch = 12f * Mathf.Deg2Rad;
            float spd   = 70f;
            return new fp3(
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                fp.FromFloat(spd * Mathf.Sin(pitch)),
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Sin(yaw)));
        }

        static fp3 MakeWedgeVelocity(float yawDeg)
        {
            // Wedge: ~35 m/s at 40°, control shot that should pass
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
            // Putter: ~5 m/s near-flat, control shot that should pass
            float yaw   = yawDeg * Mathf.Deg2Rad;
            float pitch = 2f * Mathf.Deg2Rad;
            float spd   = 5f;
            return new fp3(
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                fp.FromFloat(spd * Mathf.Sin(pitch)),
                fp.FromFloat(spd * Mathf.Cos(pitch) * Mathf.Sin(yaw)));
        }

        /// <summary>
        /// Runs a shot with full per-step + hit-list logging. Returns total frame count
        /// captured by the DiagPerStepSink (roll + putt phases only; airborne = 0 logged rows).
        /// </summary>
        static (Trajectory traj, int diagFrames) RunDiagShot(int shotNumber, fp3 origin, fp3 velocity)
        {
            string stepPath = Path.Combine(DiagDir, $"Bprime-shot-{shotNumber}.csv");
            string hitsPath = Path.Combine(DiagDir, $"Bprime-shot-{shotNumber}-hits.csv");

            int diagFrames = 0;

            using (var stepW = new StreamWriter(stepPath, append: false))
            using (var hitsW = new StreamWriter(hitsPath, append: false))
            {
                stepW.WriteLine("frame,phase,surface,ballX,ballY,ballZ,groundY2arg,groundY3arg");
                hitsW.WriteLine("frame,worldX,worldZ,preferred,foundPreferred,nHits,hits");

                BallSimulation.DiagPerStepSink = line =>
                {
                    stepW.WriteLine(line);
                    diagFrames++;
                };
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
                return (traj, diagFrames);
            }
        }

        static string s_SummaryPath;

        static void WriteSummaryLine(string line)
        {
            File.AppendAllText(s_SummaryPath, line + "\n");
        }

        static string FormatOrigin(fp3 v)
            => IsZero(v) ? "NOTFOUND" : $"({v.x.ToFloat():F2},{v.y.ToFloat():F2},{v.z.ToFloat():F2})";

        static float MinBallY(Trajectory traj)
        {
            float min = float.MaxValue;
            foreach (var s in traj.samples)
            {
                float y = s.position.y.ToFloat();
                if (y < min) min = y;
            }
            return min == float.MaxValue ? 0f : min;
        }

        // ── Tests ─────────────────────────────────────────────────────────────────────

        [Test, Order(0)]
        public void Bprime_UseSceneProviders_Check()
        {
            s_SummaryPath = Path.Combine(DiagDir, "Bprime-summary.md");
            File.WriteAllText(s_SummaryPath, "# Phase B'1 Summary — High-Velocity Launch Diagnostic\n\n");
            WriteSummaryLine("## Scene Provider Check");

            bool active = SceneProvidersActive();
            string msg  = $"SceneGroundProvider returns non-zero Y: {active}";
            WriteSummaryLine(msg);
            WriteSummaryLine("");
            WriteSummaryLine("## Per-shot results (first 30 diag frames focus)\n");
            WriteSummaryLine("| shot | surface | club | diagFrames | totalTrajFrames | minBallY | termination |");
            WriteSummaryLine("|------|---------|------|-----------|----------------|---------|-------------|");

            Debug.Log($"[B'1] {msg}");
            if (!active)
                Assert.Fail("SceneGroundProvider returns zero Y — scene geometry not loaded.");
        }

        // Shot 1: driver from Bunker_1 aimed straight out (+X) — always-fail case
        [Test, Order(1)]
        public void Bprime_Shot1_Driver_Bunker_StraightOut()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Sand);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Sand);

            string tag = IsZero(origin) ? "NOTFOUND_fallback(0,10,0)" : FormatOrigin(origin);
            Debug.Log($"[B'1] Shot 1 origin: {tag}");
            if (IsZero(origin)) origin = new fp3(fp.Zero, fp.FromInt(10), fp.Zero);

            fp3 vel = MakeDriverVelocity(0f); // aimed +X (straight out)
            var (traj, diagFrames) = RunDiagShot(1, origin, vel);

            WriteSummaryLine($"| 1 | Sand | Driver | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            Debug.Log($"[B'1] Shot 1: diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 1 trajectory null");
        }

        // Shot 2: driver from Bunker_1 aimed toward edge (+Z)
        [Test, Order(2)]
        public void Bprime_Shot2_Driver_Bunker_TowardEdge()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Sand);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Sand);
            if (IsZero(origin)) origin = new fp3(fp.Zero, fp.FromInt(10), fp.Zero);

            fp3 vel = MakeDriverVelocity(90f); // aimed +Z (toward edge perpendicular)
            var (traj, diagFrames) = RunDiagShot(2, origin, vel);

            WriteSummaryLine($"| 2 | Sand | Driver | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            Debug.Log($"[B'1] Shot 2: diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 2 trajectory null");
        }

        // Shot 3: driver from Green_1 aimed toward green edge (+X)
        [Test, Order(3)]
        public void Bprime_Shot3_Driver_Green_TowardEdge()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Green);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Green);
            if (IsZero(origin)) { Assert.Inconclusive("No Green surface found."); return; }

            fp3 vel = MakeDriverVelocity(0f); // aimed +X toward green edge
            var (traj, diagFrames) = RunDiagShot(3, origin, vel);

            WriteSummaryLine($"| 3 | Green | Driver | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            Debug.Log($"[B'1] Shot 3: diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 3 trajectory null");
        }

        // Shot 4: driver from Green_1 aimed toward green center / opposite edge (180°)
        [Test, Order(4)]
        public void Bprime_Shot4_Driver_Green_TowardCenter()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Green);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Green);
            if (IsZero(origin)) { Assert.Inconclusive("No Green surface found."); return; }

            fp3 vel = MakeDriverVelocity(180f); // aimed -X toward opposite side
            var (traj, diagFrames) = RunDiagShot(4, origin, vel);

            WriteSummaryLine($"| 4 | Green | Driver | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            Debug.Log($"[B'1] Shot 4: diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 4 trajectory null");
        }

        // Shot 5: control — wedge from Bunker_1 (should pass per B1 smoke test)
        [Test, Order(5)]
        public void Bprime_Shot5_Wedge_Bunker_Control()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Sand);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Sand);
            if (IsZero(origin)) origin = new fp3(fp.Zero, fp.FromInt(10), fp.Zero);

            fp3 vel = MakeWedgeVelocity(0f);
            var (traj, diagFrames) = RunDiagShot(5, origin, vel);

            WriteSummaryLine($"| 5 | Sand | Wedge(ctrl) | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            Debug.Log($"[B'1] Shot 5 (control): diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 5 trajectory null");
        }

        // Shot 6: control — putter from Green_1 (should pass per B1 smoke test)
        [Test, Order(6)]
        public void Bprime_Shot6_Putter_Green_Control()
        {
            fp3 origin = FindPositionOnSurfaceFiner(SurfaceType.Green);
            if (IsZero(origin)) origin = FindPositionOnSurface(SurfaceType.Green);
            if (IsZero(origin)) { Assert.Inconclusive("No Green surface found."); return; }

            fp3 vel = MakePutterVelocity(0f);
            var (traj, diagFrames) = RunDiagShot(6, origin, vel);

            WriteSummaryLine($"| 6 | Green | Putter(ctrl) | {diagFrames} | {traj.samples.Count} | {MinBallY(traj):F3} | {traj.termination} |");
            WriteSummaryLine("");
            WriteSummaryLine("## Notes");
            WriteSummaryLine("- diagFrames = rows captured by DiagPerStepSink (roll+putt phases only; airborne = 0).");
            WriteSummaryLine("- minBallY = lowest ball Y across entire trajectory. Strongly negative = fall-through.");
            WriteSummaryLine("- If driver shots show diagFrames=0 and strongly negative minBallY, failure is in the airborne integrator.");
            WriteSummaryLine("- See Bprime-shot-N.csv for per-step roll/putt frames and Bprime-shot-N-hits.csv for raycast hit lists.");
            Debug.Log($"[B'1] Shot 6 (control): diagFrames={diagFrames} total={traj.samples.Count} term={traj.termination} minY={MinBallY(traj):F3}");
            Assert.IsNotNull(traj, "Shot 6 trajectory null");
        }

        static bool IsZero(fp3 v) => v.x == fp.Zero && v.y == fp.Zero && v.z == fp.Zero;
    }
}
