#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Phase B surface-tuning diagnostic harness. Runs 321+ controlled captures through the real
    /// PhysicsLabController shot pipeline, measuring rollout distance per (surface × speed × spin × sample).
    ///
    /// Activated by SurfaceRolloutMenu. Armed via SessionState. Self-destructs after completion.
    /// DO NOT use in production builds.
    ///
    /// Iter-7 changes:
    ///   - Item 1: Uses PhysicsLabController.PlaceBallAtAirborne() for drop geometry (bypass surface snap).
    ///   - Item 2: Reverted Fix #6. Sub-mode 1a uses original spec drop geometry:
    ///             spawn at surfaceY + 3.0m with -30° downward velocity vector.
    ///   - Item 3: Sample jitter: sample_id > 1 offsets spawn XZ by (sampleId-1)*0.10m in +X,
    ///             seeded deterministically so re-runs are reproducible.
    ///   - Item 4: Fixed spin axis: (0,0,1) for +X shots (was (-1,0,0) = anti-parallel to shot,
    ///             which produced zero Magnus force). Cross((0,0,1), (+X_hat)) = (0,1,0) = upward.
    ///   - Item 5: CaptureRealShot now calls PlaceBallAtAirborne(teePos) before firing each shot,
    ///             so GetCurrentOrigin() returns teePos not the previous ball's resting position.
    ///   - Item 6: Sub-mode 1 sweeps Holes 1, 9, 18. Progress key includes hole number.
    ///             sweep.csv gains a source_hole column (col 3).
    /// </summary>
    public sealed class SurfaceRolloutHarness : MonoBehaviour
    {
        // ── Configuration — set by SurfaceRolloutMenu before play mode ────────────────
        [SerializeField] public string _outputDir;
        [SerializeField] public bool   _runSubMode1a = true;
        [SerializeField] public bool   _runSubMode1b = true;
        [SerializeField] public bool   _runSubMode2  = true;
        [SerializeField] public int[]  _holesForSubMode1 = new[] { 1, 9, 18 };   // iter-7: multi-hole 1a+1b
        [SerializeField] public int[]  _holesForSubMode2 = new[] { 1, 9, 18 };

        // ── Constants ─────────────────────────────────────────────────────────────────
        const string ArmedKey       = "SurfaceRolloutHarness.Armed";
        const float  StartupWait    = 8.0f;   // time for LabController + hole providers to fully init
        const float  ShotTimeout    = 30.0f;  // max seconds to wait for OnShotComplete
        const float  BetweenShotWait = 0.3f;  // small pause between shots so SM re-arms
        const float  HoleSwapWait   = 3.0f;   // wait after OnHoleLoaded before discovery

        // §iter-7 Item 2: drop altitude above surface (spec: 3.0m)
        const float DropAltitude = 3.0f;

        static string HoleSceneName(int hole) => $"Hole_{hole:D2}_Geo";

        // ── Sub-mode 1a sweep axes ────────────────────────────────────────────────────
        static readonly SurfaceType[] Surfaces1a = new[]
        {
            SurfaceType.Fairway,
            SurfaceType.Green,
            SurfaceType.GreenCollar,
            SurfaceType.Semirough,
            SurfaceType.Rough,
            SurfaceType.Tee,
            SurfaceType.Sand,
            SurfaceType.BunkerLip,
            SurfaceType.CartPath,
        };

        static readonly float[] Speeds1a = new[] { 3f, 6f, 9f, 12f, 15f, 20f, 25f };   // m/s horizontal
        static readonly float[] Spins1a  = new[] { 500f, 2700f };                        // rpm backspin
        const int Samples1a = 2;

        // §iter-7 Item 3: sample jitter in XZ (meters) per sample_id.
        // sample_id=1: no offset. sample_id=2: +0.10m in +X.
        // This ensures bit-different trajectories across samples while keeping
        // the spawn within the clean-2m-radius area (±0.10m << 2m).
        const float SampleJitterM = 0.10f;

        // §iter-7 Item 4: correct backspin axis for a +X shot.
        // Cross((0,0,1),(1,0,0)) = (0*0-1*0, 1*1-0*0, 0*0-0*1) = (0,1,0) — upward Magnus lift.
        // Previous axis (-1,0,0) was anti-parallel to the +X velocity → Cross((-1,0,0),(1,0,0)) = (0,0,0)
        // → ZERO Magnus force → spin had no effect. (0,0,1) is the Range-preset convention:
        // see ShotPresetCatalog.BackspinAxis and RollAndPuttTuningTests.DriverInput() which fires +Z.)
        static readonly fp3 BackspinAxis1a = new fp3(fp.Zero, fp.Zero, fp.One);

        // ── Sub-mode 1b sweep axes ────────────────────────────────────────────────────
        static readonly SurfaceType[] Surfaces1b = new[]
        {
            SurfaceType.Green,
            SurfaceType.GreenCollar,
            SurfaceType.Fairway,
        };
        static readonly float[] PuttSpeeds = new[] { 0.5f, 1.0f, 1.83f, 2.5f, 3.5f, 5.0f, 7.0f }; // m/s
        const int Samples1b = 3;

        // ── Arm/Disarm ────────────────────────────────────────────────────────────────
        public static bool Armed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        // ── Runtime state ─────────────────────────────────────────────────────────────
        PhysicsLabController _controller;
        bool _subscribed;

        // Used to capture shot result asynchronously from the BallStateMachine event
        bool      _shotComplete;
        ShotResult _lastResult;

        // Progress tracking for resume-skip
        HashSet<string> _completedKeys = new HashSet<string>();

        void Start()
        {
            bool armed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[SurfaceRolloutHarness] Start() — Armed={armed}");
            if (!armed)
            {
                Debug.LogWarning("[SurfaceRolloutHarness] Not armed — destroying self.");
                Destroy(this);
                return;
            }
            Armed = false;
            StartCoroutine(SafeRunSweep());
        }

        IEnumerator SafeRunSweep()
        {
            System.Exception caught = null;
            bool completed = false;
            yield return StartCoroutine(RunSweep(() => completed = true, ex => caught = ex));
            if (caught != null)
                Debug.LogError($"[SurfaceRolloutHarness] Sweep threw exception: {caught}");
            if (!completed)
                Debug.LogError("[SurfaceRolloutHarness] RunSweep did NOT complete normally.");
            // Destroy only THIS component, not the entire LabRoot GameObject.
            Destroy(this);
        }

        IEnumerator RunSweep(Action onDone, Action<Exception> onError)
        {
            // §sweep-fix: Allow Unity to run while not focused so coroutines advance
            Application.runInBackground = true;

            // Force timeScale=1 in case something paused the scene
            if (Time.timeScale < 0.01f)
            {
                Debug.LogWarning($"[SurfaceRolloutHarness] timeScale={Time.timeScale} — forcing to 1.");
                Time.timeScale = 1f;
            }

            Debug.Log($"[SurfaceRolloutHarness] Waiting {StartupWait}s for startup...");
            yield return new WaitForSecondsRealtime(StartupWait);

            _controller = FindObjectOfType<PhysicsLabController>();
            if (_controller == null)
            {
                onError(new Exception("PhysicsLabController not found in scene."));
                yield break;
            }
            Debug.Log($"[SurfaceRolloutHarness] Controller found: {_controller.gameObject.name}");

            // §sweep-fix: Use Instant PlayRate so BallAnimator.SnapToEnd fires immediately in Play(),
            // meaning OnShotComplete fires within 1-2 frames instead of waiting animation duration.
            _controller.SetBallAnimatorPlayRate(float.MaxValue);
            Debug.Log("[SurfaceRolloutHarness] BallAnimator.PlayRate set to Instant (float.MaxValue).");

            // Subscribe to shot complete
            _controller.BallSM.OnShotComplete += OnShotCompleteHandler;
            _subscribed = true;

            // Resolve output directory
            string outDir = string.IsNullOrEmpty(_outputDir)
                ? Path.Combine(Application.dataPath, "../Docs/Specs/Active/phase_b_surface_tuning/captures",
                               DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                : _outputDir;
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);
            Debug.Log($"[SurfaceRolloutHarness] Output dir: {outDir}");

            // Progress log path
            string progressPath = Path.Combine(outDir, "progress.log");

            // Load completed keys from progress.log (resume support)
            if (File.Exists(progressPath))
            {
                foreach (var line in File.ReadAllLines(progressPath))
                {
                    if (line.StartsWith("done:"))
                        _completedKeys.Add(line.Substring(5).Trim());
                }
                Debug.Log($"[SurfaceRolloutHarness] Loaded {_completedKeys.Count} completed keys from progress.log (resume).");
            }

            // CSV writers — opened for append so resume works
            var sweepPath     = Path.Combine(outDir, "sweep.csv");
            var realShotsPath = Path.Combine(outDir, "real_shots.csv");

            bool sweepIsNew     = !File.Exists(sweepPath);
            bool realShotsIsNew = !File.Exists(realShotsPath);

            using (var sweepWriter     = new StreamWriter(sweepPath,     append: true, System.Text.Encoding.UTF8))
            using (var realShotsWriter = new StreamWriter(realShotsPath, append: true, System.Text.Encoding.UTF8))
            using (var progressWriter  = new StreamWriter(progressPath,  append: true, System.Text.Encoding.UTF8))
            {
                // §iter-7 Item 6: sweep.csv now includes source_hole as col 3.
                // New header: mode,source_hole,surface_target,...
                if (sweepIsNew)
                    sweepWriter.WriteLine("mode,source_hole,surface_target,target_v_horizontal_mps,target_spin_rpm,actual_v_at_contact_mps,first_contact_pos_x,first_contact_pos_y,first_contact_pos_z,end_pos_x,end_pos_y,end_pos_z,end_surface,roll_distance_m,bounce_count,sim_duration_s,sample_id,timestamp_iso");

                if (realShotsIsNew)
                    realShotsWriter.WriteLine("hole,shot_variant,tee_pos_x,tee_pos_y,tee_pos_z,first_contact_pos_x,first_contact_pos_y,first_contact_pos_z,end_pos_x,end_pos_y,end_pos_z,end_surface,carry_m,roll_m,total_m,bounce_count,sim_duration_s,timestamp_iso");

                sweepWriter.AutoFlush     = true;
                realShotsWriter.AutoFlush = true;
                progressWriter.AutoFlush  = true;

                int totalCaptures = 0;
                int capturesSinceLog = 0;

                // ── Sub-modes 1a + 1b (on all _holesForSubMode1) ─────────────────────
                if (_runSubMode1a || _runSubMode1b)
                {
                    // Unload any currently-active hole providers before starting the sweep loop
                    _controller.OnHoleUnloaded();
                    yield return null;

                    foreach (int sweepHole in _holesForSubMode1)
                    {
                        string holeScene = HoleSceneName(sweepHole);

                        // Check that this hole scene is loaded additively (pre-loaded by menu)
                        var hs = SceneManager.GetSceneByName(holeScene);
                        if (!hs.IsValid() || !hs.isLoaded)
                        {
                            Debug.LogWarning($"[SurfaceRolloutHarness] Hole {sweepHole} scene '{holeScene}' not loaded — skipping 1a+1b for this hole.");
                            continue;
                        }

                        // Switch controller to this hole's baked providers
                        Debug.Log($"[SurfaceRolloutHarness] Sub-mode 1 sweep: switching to Hole {sweepHole}...");
                        _controller.OnHoleLoaded(holeScene);
                        yield return new WaitForSecondsRealtime(HoleSwapWait);

                        // Discovery: find surface centers on the loaded hole
                        var surfaceCenters = DiscoverSurfaceCenters(_controller, minRadiusM: 2);

                        // Sub-mode 1a
                        if (_runSubMode1a)
                        {
                            Debug.Log($"[SurfaceRolloutHarness] Starting Sub-mode 1a (roll-path sweep) on Hole {sweepHole}...");
                            foreach (var surface in Surfaces1a)
                            {
                                if (!surfaceCenters.ContainsKey(surface))
                                {
                                    Debug.LogWarning($"[SurfaceRolloutHarness] Surface {surface} not found on Hole {sweepHole} — skipping.");
                                    continue;
                                }
                                Vector3 center = surfaceCenters[surface];

                                foreach (float vH in Speeds1a)
                                {
                                    foreach (float spinRpm in Spins1a)
                                    {
                                        for (int s = 1; s <= Samples1a; s++)
                                        {
                                            // §iter-7 Item 6: include sweepHole in progress key
                                            string key = $"1a|hole{sweepHole}|{surface}|{vH:F1}|{spinRpm:F0}|{s}";
                                            if (_completedKeys.Contains(key))
                                            {
                                                totalCaptures++;
                                                continue;
                                            }

                                            progressWriter.WriteLine($"pending: {key}");

                                            yield return StartCoroutine(CaptureRollPath(
                                                surface, center, vH, spinRpm, s,
                                                sweepHole, sweepWriter));

                                            progressWriter.WriteLine($"done:{key}");
                                            _completedKeys.Add(key);
                                            totalCaptures++;
                                            capturesSinceLog++;

                                            if (capturesSinceLog >= 50)
                                            {
                                                Debug.Log($"[SurfaceRolloutHarness] Progress: {totalCaptures} captures complete.");
                                                capturesSinceLog = 0;
                                            }

                                            yield return new WaitForSeconds(BetweenShotWait);
                                        }
                                    }
                                }
                            }
                            Debug.Log($"[SurfaceRolloutHarness] Sub-mode 1a Hole {sweepHole} done. Total so far: {totalCaptures}");
                        }

                        // Sub-mode 1b
                        if (_runSubMode1b)
                        {
                            Debug.Log($"[SurfaceRolloutHarness] Starting Sub-mode 1b (putt-path sweep) on Hole {sweepHole}...");
                            foreach (var surface in Surfaces1b)
                            {
                                if (!surfaceCenters.ContainsKey(surface))
                                {
                                    Debug.LogWarning($"[SurfaceRolloutHarness] Surface {surface} not found for 1b on Hole {sweepHole} — skipping.");
                                    continue;
                                }
                                Vector3 center = surfaceCenters[surface];

                                foreach (float vH in PuttSpeeds)
                                {
                                    for (int s = 1; s <= Samples1b; s++)
                                    {
                                        string key = $"1b|hole{sweepHole}|{surface}|{vH:F2}|{s}";
                                        if (_completedKeys.Contains(key))
                                        {
                                            totalCaptures++;
                                            continue;
                                        }

                                        progressWriter.WriteLine($"pending: {key}");

                                        yield return StartCoroutine(CapturePuttPath(
                                            surface, center, vH, s,
                                            sweepHole, sweepWriter));

                                        progressWriter.WriteLine($"done:{key}");
                                        _completedKeys.Add(key);
                                        totalCaptures++;
                                        capturesSinceLog++;

                                        if (capturesSinceLog >= 50)
                                        {
                                            Debug.Log($"[SurfaceRolloutHarness] Progress: {totalCaptures} captures complete.");
                                            capturesSinceLog = 0;
                                        }

                                        yield return new WaitForSeconds(BetweenShotWait);
                                    }
                                }
                            }
                            Debug.Log($"[SurfaceRolloutHarness] Sub-mode 1b Hole {sweepHole} done. Total so far: {totalCaptures}");
                        }

                        // Unload this hole's providers before moving to the next
                        _controller.OnHoleUnloaded();
                        yield return null;
                    }
                }

                // ── Sub-mode 2 (real driver shots from tees, 3 holes) ─────────────────
                if (_runSubMode2)
                {
                    Debug.Log("[SurfaceRolloutHarness] Starting Sub-mode 2 (real driver shots)...");

                    // Make sure we start in an unloaded state from sub-modes 1a/1b
                    _controller.OnHoleUnloaded();
                    yield return null;

                    foreach (int hole in _holesForSubMode2)
                    {
                        string sceneName = HoleSceneName(hole);

                        var holeScene = SceneManager.GetSceneByName(sceneName);
                        if (!holeScene.IsValid() || !holeScene.isLoaded)
                        {
                            Debug.LogWarning($"[SurfaceRolloutHarness] Scene {sceneName} not loaded — skipping Sub-mode 2 for hole {hole}. Ensure SurfaceRolloutMenu pre-loaded it.");
                            continue;
                        }

                        Debug.Log($"[SurfaceRolloutHarness] Sub-mode 2: switching to {sceneName}...");
                        _controller.OnHoleLoaded(sceneName);
                        yield return new WaitForSecondsRealtime(3.0f);

                        // §iter-7 Item 5: capture teePos AFTER OnHoleLoaded (SetupAtTee runs).
                        // Use CurrentBall.position which is now at the tee.
                        Vector3 teePos = _controller.CurrentBall != null
                            ? _controller.CurrentBall.position
                            : Vector3.zero;

                        Debug.Log($"[SurfaceRolloutHarness] Sub-mode 2: Hole {hole} tee at {teePos:F2}");

                        for (int variant = 0; variant < 2; variant++)
                        {
                            string variantName = variant == 0 ? "straight" : "draw3deg";
                            string key2 = $"2|{hole}|{variantName}";
                            if (_completedKeys.Contains(key2))
                            {
                                totalCaptures++;
                                continue;
                            }

                            progressWriter.WriteLine($"pending: {key2}");
                            yield return StartCoroutine(CaptureRealShot(hole, variantName, teePos, realShotsWriter));
                            progressWriter.WriteLine($"done:{key2}");
                            _completedKeys.Add(key2);
                            totalCaptures++;

                            yield return new WaitForSeconds(BetweenShotWait);
                        }

                        _controller.OnHoleUnloaded();
                        yield return null;

                        Debug.Log($"[SurfaceRolloutHarness] Hole {hole} done. Total so far: {totalCaptures}");
                    }
                    Debug.Log($"[SurfaceRolloutHarness] Sub-mode 2 done. Total: {totalCaptures}");
                }

                // Write README
                WriteReadme(outDir, totalCaptures);
                Debug.Log($"[SurfaceRolloutHarness] All sweeps complete. {totalCaptures} captures. Output: {outDir}");
            }

            _controller.BallSM.OnShotComplete -= OnShotCompleteHandler;
            _subscribed = false;
            onDone();
        }

        void OnDestroy()
        {
            if (_subscribed && _controller != null && _controller.BallSM != null)
            {
                _controller.BallSM.OnShotComplete -= OnShotCompleteHandler;
                _subscribed = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Discovery: find one clean sample position per surface type on the loaded hole
        // ─────────────────────────────────────────────────────────────────────────────

        Dictionary<SurfaceType, Vector3> DiscoverSurfaceCenters(PhysicsLabController controller, float minRadiusM)
        {
            var result = new Dictionary<SurfaceType, Vector3>();
            var surfaceProvider = controller.GetSurfaces() as Golfin.Physics.Runtime.Baked.BakedZoneClassifier;
            if (surfaceProvider == null)
            {
                Debug.LogWarning("[SurfaceRolloutHarness] Surface provider is not BakedZoneClassifier — using placement entries fallback.");
                return DiscoverSurfaceCentersFromPlacementEntries(controller);
            }

            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

            foreach (var entry in controller.PlacementEntries)
            {
                if (entry.WorldPos.x < minX) minX = entry.WorldPos.x;
                if (entry.WorldPos.x > maxX) maxX = entry.WorldPos.x;
                if (entry.WorldPos.z < minZ) minZ = entry.WorldPos.z;
                if (entry.WorldPos.z > maxZ) maxZ = entry.WorldPos.z;
            }

            if (float.IsInfinity(minX))
            {
                minX = -300f; maxX = 300f;
                minZ = -300f; maxZ = 300f;
            }
            else
            {
                minX -= 50f; maxX += 50f;
                minZ -= 50f; maxZ += 50f;
            }

            float step = 2.0f;
            var candidatesByType = new Dictionary<SurfaceType, List<Vector3>>();

            int gridX = Mathf.CeilToInt((maxX - minX) / step);
            int gridZ = Mathf.CeilToInt((maxZ - minZ) / step);
            int totalCells = gridX * gridZ;

            Debug.Log($"[SurfaceRolloutHarness] Discovery: scanning {totalCells} cells ({gridX}×{gridZ}) from ({minX:F0},{minZ:F0}) to ({maxX:F0},{maxZ:F0})");

            var groundProvider = controller.GetGround();

            for (int ix = 0; ix < gridX; ix++)
            {
                float wx = minX + ix * step;
                for (int iz = 0; iz < gridZ; iz++)
                {
                    float wz = minZ + iz * step;
                    var fpX = fp.FromFloat(wx);
                    var fpZ = fp.FromFloat(wz);
                    SurfaceType st = surfaceProvider.Classify(fpX, fpZ);
                    if (st == SurfaceType.OOB || st == SurfaceType.Water) continue;

                    float wy = groundProvider != null
                        ? groundProvider.SampleHeight(fpX, fpZ).ToFloat()
                        : 0f;

                    if (!candidatesByType.ContainsKey(st))
                        candidatesByType[st] = new List<Vector3>();
                    candidatesByType[st].Add(new Vector3(wx, wy, wz));
                }
            }

            float[] offsets = new[] { 0f, minRadiusM, -minRadiusM };
            foreach (var kvp in candidatesByType)
            {
                SurfaceType targetType = kvp.Key;
                var candidates = kvp.Value;

                Vector3 bestCenter = Vector3.zero;
                bool found = false;

                foreach (var pos in candidates)
                {
                    bool clean = true;
                    foreach (float ox in offsets)
                    {
                        foreach (float oz in offsets)
                        {
                            var st = surfaceProvider.Classify(
                                fp.FromFloat(pos.x + ox),
                                fp.FromFloat(pos.z + oz));
                            if (st != targetType) { clean = false; break; }
                        }
                        if (!clean) break;
                    }
                    if (clean)
                    {
                        bestCenter = pos;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    float resolvedY = bestCenter.y;
                    if (UnityEngine.Physics.Raycast(
                        new Vector3(bestCenter.x, 200f, bestCenter.z), Vector3.down,
                        out RaycastHit rcHit, 1000f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        resolvedY = rcHit.point.y;
                    }
                    else
                    {
                        Debug.LogWarning($"[SurfaceRolloutHarness] Discovery: {targetType} — physics Raycast at XZ=({bestCenter.x:F1},{bestCenter.z:F1}) returned no hit. Using groundProvider Y={bestCenter.y:F2}.");
                    }
                    var finalCenter = new Vector3(bestCenter.x, resolvedY, bestCenter.z);
                    result[targetType] = finalCenter;
                    Debug.Log($"[SurfaceRolloutHarness] Discovery: {targetType} center at ({finalCenter.x:F2}, {finalCenter.y:F2}, {finalCenter.z:F2}) [drop spawn Y will be {finalCenter.y + DropAltitude:F2}]");
                }
                else if (candidates.Count > 0)
                {
                    var cand = candidates[0];
                    float resolvedY = cand.y;
                    if (UnityEngine.Physics.Raycast(
                        new Vector3(cand.x, 200f, cand.z), Vector3.down,
                        out RaycastHit rcHit2, 1000f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        resolvedY = rcHit2.point.y;
                    }
                    var fallbackCenter = new Vector3(cand.x, resolvedY, cand.z);
                    result[targetType] = fallbackCenter;
                    Debug.LogWarning($"[SurfaceRolloutHarness] Discovery: {targetType} — no clean 2m-radius center; using first candidate at ({fallbackCenter.x:F2},{fallbackCenter.y:F2},{fallbackCenter.z:F2}). May have surface contamination.");
                }
                else
                {
                    Debug.LogWarning($"[SurfaceRolloutHarness] Discovery: {targetType} not found on this hole — skip.");
                }
            }

            foreach (var st in Surfaces1a)
            {
                if (!result.ContainsKey(st))
                    Debug.LogWarning($"[SurfaceRolloutHarness] Surface {st} absent from this hole — will be skipped in sweep.");
            }

            return result;
        }

        Dictionary<SurfaceType, Vector3> DiscoverSurfaceCentersFromPlacementEntries(PhysicsLabController controller)
        {
            var result = new Dictionary<SurfaceType, Vector3>();
            foreach (var entry in controller.PlacementEntries)
            {
                if (entry.PreferredSurfaceTypeValue == null) continue;
                SurfaceType st = (SurfaceType)entry.PreferredSurfaceTypeValue.Value;
                if (!result.ContainsKey(st))
                    result[st] = entry.WorldPos;
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Capture: Sub-mode 1a — synthetic drop at 30° below horizontal
        // ─────────────────────────────────────────────────────────────────────────────

        IEnumerator CaptureRollPath(SurfaceType surface, Vector3 center,
            float vHorizontal, float spinRpm, int sampleId, int hole, StreamWriter csv)
        {
            // §iter-7 Item 2 (reverts Fix #6): original spec drop geometry.
            // Spawn ball at surfaceY + DropAltitude (3.0m) with -30° downward velocity vector.
            // v_horizontal is the prescribed sweep value; v_vertical = -vH * tan(30°) downward.
            //
            // §iter-7 Item 3: per-sample XZ jitter for sample differentiation.
            // sample_id=1: no offset. sample_id=2: +SampleJitterM in +X.
            // Offset is << 2m clean-radius so we stay on the target surface.
            float jitterX = (sampleId - 1) * SampleJitterM;
            float spawnX  = center.x + jitterX;
            float spawnY  = center.y + DropAltitude;  // 3m above surface
            float spawnZ  = center.z;

            // §iter-7 Item 1: use PlaceBallAtAirborne to bypass surface-snap in GetCurrentOrigin.
            // Without this, HandleShotResolved calls GetCurrentOrigin() which snaps the ball Y
            // back to the surface, defeating the aerial spawn.
            _controller.PlaceBallAtAirborne(new Vector3(spawnX, spawnY, spawnZ));
            yield return null; // let PlaceBallAtAirborne side-effects settle one frame

            // Drop at -30°: v_horizontal in +X, v_vertical = -vH * tan(30°) downward.
            float vX = vHorizontal;                               // horizontal +X
            float vY = -vHorizontal * Mathf.Tan(30f * Mathf.Deg2Rad); // downward component
            float vZ = 0f;

            // §iter-7 Item 4: correct backspin axis for a +X shot.
            // BackspinAxis1a = (0,0,1). Cross((0,0,1),(1,0,0)) = (0,1,0) = upward Magnus lift.
            float rps = spinRpm * 2f * Mathf.PI / 60f;
            var spin = new SpinState(BackspinAxis1a, fp.FromFloat(rps));

            var input = new ShotInput(
                new fp3(fp.FromFloat(spawnX), fp.FromFloat(spawnY), fp.FromFloat(spawnZ)),
                new fp3(fp.FromFloat(vX), fp.FromFloat(vY), fp.FromFloat(vZ)),
                fp.FromInt(60),
                spin);

            _shotComplete = false;
            _controller.HandleShotResolvedForTests(input, BallPhysicsModifiers.Neutral);

            float elapsed = 0f;
            while (!_shotComplete && elapsed < ShotTimeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (!_shotComplete)
            {
                Debug.LogWarning($"[SurfaceRolloutHarness] 1a shot timed out: {surface} v={vHorizontal} spin={spinRpm} s={sampleId}");
                WriteWarningRow(csv, "roll", hole, surface, vHorizontal, spinRpm, sampleId);
                yield break;
            }

            var result   = _lastResult;
            var traj     = _controller.LastTrajectory;

            if (traj == null || traj.terrainHits == null || traj.terrainHits.Count == 0)
            {
                Debug.LogWarning($"[SurfaceRolloutHarness] 1a shot zero-contact: {surface} v={vHorizontal} spin={spinRpm} s={sampleId} — endY={result.EndPosition.y.ToFloat():F1}. Writing warning row.");
                WriteWarningRow(csv, "roll", hole, surface, vHorizontal, spinRpm, sampleId);
                yield break;
            }

            WriteRollRow(csv, "roll", hole, surface, vHorizontal, spinRpm, sampleId, result, traj,
                new Vector3(spawnX, spawnY, spawnZ));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Capture: Sub-mode 1b — putt-path sweep
        // ─────────────────────────────────────────────────────────────────────────────

        IEnumerator CapturePuttPath(SurfaceType surface, Vector3 center,
            float vHorizontal, int sampleId, int hole, StreamWriter csv)
        {
            // §sweep-fix3: PlaceBallAt teleports the visual ball to the surface center so
            // HandleShotResolvedForTests reads the correct origin via GetCurrentOrigin().
            // Place at terrain-snapped center Y for a ground-level putt.
            _controller.PlaceBallAt(center);
            yield return null;

            Vector3 spawnPos = new Vector3(center.x, center.y, center.z);

            var input = new ShotInput(
                new fp3(fp.FromFloat(spawnPos.x), fp.FromFloat(spawnPos.y), fp.FromFloat(spawnPos.z)),
                new fp3(fp.FromFloat(vHorizontal), fp.Zero, fp.Zero),
                fp.FromInt(30));

            _shotComplete = false;
            _controller.HandleShotResolvedForTests(input, BallPhysicsModifiers.Neutral);

            float elapsed = 0f;
            while (!_shotComplete && elapsed < ShotTimeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (!_shotComplete)
            {
                Debug.LogWarning($"[SurfaceRolloutHarness] 1b shot timed out: {surface} v={vHorizontal} s={sampleId}");
                WriteWarningRow(csv, "putt", hole, surface, vHorizontal, 0f, sampleId);
                yield break;
            }

            var result = _lastResult;
            var traj   = _controller.LastTrajectory;
            WriteRollRow(csv, "putt", hole, surface, vHorizontal, 0f, sampleId, result, traj, spawnPos);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Capture: Sub-mode 2 — real driver shots from tee
        // ─────────────────────────────────────────────────────────────────────────────

        IEnumerator CaptureRealShot(int hole, string variantName, Vector3 teePos, StreamWriter csv)
        {
            float angleRad = 10.9f * Mathf.Deg2Rad;
            float speed    = 73.6f;

            // Derive horizontal shot direction from PlacementEntries (Green entries average − teePos)
            Vector3 toGreen = Vector3.right;
            {
                var greenEntries = new List<Vector3>();
                foreach (var entry in _controller.PlacementEntries)
                {
                    if (entry.PreferredSurfaceTypeValue == 1)
                        greenEntries.Add(entry.WorldPos);
                }

                if (greenEntries.Count > 0)
                {
                    Vector3 greenCenter = Vector3.zero;
                    foreach (var gp in greenEntries)
                        greenCenter += gp;
                    greenCenter /= greenEntries.Count;

                    Vector3 raw = greenCenter - teePos;
                    raw.y = 0f;
                    if (raw.sqrMagnitude > 0.001f)
                    {
                        toGreen = raw.normalized;
                        Debug.Log($"[SurfaceRolloutHarness] Sub-mode 2 Hole {hole}: green direction derived from {greenEntries.Count} entries → ({toGreen.x:F3}, 0, {toGreen.z:F3})");
                    }
                    else
                    {
                        Debug.LogWarning($"[SurfaceRolloutHarness] Sub-mode 2 Hole {hole}: teePos and greenCenter are coincident — falling back to +X.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[SurfaceRolloutHarness] Sub-mode 2 Hole {hole}: no Green PlacementEntries found — falling back to +X direction.");
                }
            }

            // For "draw3deg", rotate the XZ direction +3° about world +Y
            if (variantName == "draw3deg")
            {
                float drawRad = 3f * Mathf.Deg2Rad;
                float cosDraw = Mathf.Cos(drawRad);
                float sinDraw = Mathf.Sin(drawRad);
                float newX = toGreen.x * cosDraw - toGreen.z * sinDraw;
                float newZ = toGreen.x * sinDraw + toGreen.z * cosDraw;
                toGreen = new Vector3(newX, 0f, newZ).normalized;
            }

            float vXZ = speed * Mathf.Cos(angleRad);
            float vY  = speed * Mathf.Sin(angleRad);

            // Spin axis: Cross(toGreen, up) — verified against ShotPresetCatalog.driver_hole1.
            Vector3 spinAxis = Vector3.Cross(toGreen, Vector3.up).normalized;
            float rps = 2686f * 2f * Mathf.PI / 60f;
            var   spin = new SpinState(
                new fp3(fp.FromFloat(spinAxis.x), fp.FromFloat(spinAxis.y), fp.FromFloat(spinAxis.z)),
                fp.FromFloat(rps));

            // §iter-7 Item 5: place the ball at teePos via PlaceBallAtAirborne before EACH shot.
            // Without this, after the straight shot the ball is at rest in the fairway.
            // GetCurrentOrigin() would then return the fairway position (not the tee), causing
            // the draw shot to fire from the fairway and producing 2× carry distance.
            // PlaceBallAtAirborne(teePos) arms the single-shot override so GetCurrentOrigin
            // returns teePos Y verbatim on the next HandleShotResolved call.
            _controller.PlaceBallAtAirborne(teePos);
            yield return null;

            var input = new ShotInput(
                new fp3(fp.FromFloat(teePos.x), fp.FromFloat(teePos.y), fp.FromFloat(teePos.z)),
                new fp3(fp.FromFloat(vXZ * toGreen.x), fp.FromFloat(vY), fp.FromFloat(vXZ * toGreen.z)),
                fp.FromInt(60),
                spin);

            _shotComplete = false;
            _controller.HandleShotResolvedForTests(input, BallPhysicsModifiers.Neutral);

            float elapsed = 0f;
            while (!_shotComplete && elapsed < ShotTimeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (!_shotComplete)
            {
                Debug.LogWarning($"[SurfaceRolloutHarness] Sub-mode 2 shot timed out: hole={hole} variant={variantName}");
                WriteRealShotWarningRow(csv, hole, variantName, teePos);
                yield break;
            }

            var result = _lastResult;
            var traj   = _controller.LastTrajectory;
            WriteRealShotRow(csv, hole, variantName, teePos, result, traj);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Shot result handler
        // ─────────────────────────────────────────────────────────────────────────────

        void OnShotCompleteHandler(ShotResult result)
        {
            _lastResult   = result;
            _shotComplete = true;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Metrics helpers
        // ─────────────────────────────────────────────────────────────────────────────

        static float XZDist(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float XZDist(fp3 a, fp3 b)
        {
            float dx = b.x.ToFloat() - a.x.ToFloat();
            float dz = b.z.ToFloat() - a.z.ToFloat();
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Find the first non-stop terrain hit (the actual landing bounce, not the final rest).
        /// Returns zero vector if no terrain hits.
        /// </summary>
        static Vector3 FirstContactPos(Trajectory traj)
        {
            if (traj == null || traj.terrainHits == null) return Vector3.zero;
            foreach (var hit in traj.terrainHits)
            {
                if (!hit.IsStop)
                    return new Vector3(hit.Position.x.ToFloat(), hit.Position.y.ToFloat(), hit.Position.z.ToFloat());
            }
            if (traj.terrainHits.Count > 0)
            {
                var h = traj.terrainHits[0];
                return new Vector3(h.Position.x.ToFloat(), h.Position.y.ToFloat(), h.Position.z.ToFloat());
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Velocity magnitude at the trajectory sample closest to firstContactPos.
        /// </summary>
        static float ActualVAtContact(Trajectory traj, Vector3 firstContact)
        {
            if (traj == null || traj.samples == null || traj.samples.Count == 0) return 0f;
            float bestDist = float.PositiveInfinity;
            float bestVel  = 0f;
            foreach (var sample in traj.samples)
            {
                float sx = sample.position.x.ToFloat();
                float sy = sample.position.y.ToFloat();
                float sz = sample.position.z.ToFloat();
                float d  = Vector3.Distance(new Vector3(sx, sy, sz), firstContact);
                if (d < bestDist)
                {
                    bestDist = d;
                    float vx = sample.velocity.x.ToFloat();
                    float vy = sample.velocity.y.ToFloat();
                    float vz = sample.velocity.z.ToFloat();
                    bestVel = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);
                }
            }
            return bestVel;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // CSV row writers
        // ─────────────────────────────────────────────────────────────────────────────

        void WriteRollRow(StreamWriter w, string mode, int hole, SurfaceType surface,
            float targetVH, float targetSpin, int sampleId,
            ShotResult result, Trajectory traj, Vector3 spawnPos)
        {
            // §sweep-fix4: For putt mode (1b), measure roll_distance from spawnPos to end.
            Vector3 contact;
            bool    hasContact;
            if (mode == "putt")
            {
                contact    = spawnPos;
                hasContact = true;
            }
            else
            {
                contact    = FirstContactPos(traj);
                hasContact = contact != Vector3.zero;
            }

            float actualV   = hasContact ? ActualVAtContact(traj, contact) : 0f;
            Vector3 endPos  = new Vector3(
                result.EndPosition.x.ToFloat(),
                result.EndPosition.y.ToFloat(),
                result.EndPosition.z.ToFloat());
            float rollDist  = hasContact ? XZDist(contact, endPos) : 0f;
            int bounces     = result.BounceCount;
            float simDur    = result.SimDuration.ToFloat();
            string ts       = DateTime.Now.ToString("o");

            if (!hasContact)
                Debug.LogWarning($"[SurfaceRolloutHarness] No terrain hit for {mode} {surface} v={targetVH} spin={targetSpin} s={sampleId} — recording zeros.");

            if (mode == "roll" && hasContact && result.EndSurface != surface)
                Debug.LogWarning($"[SurfaceRolloutHarness] EndSurface mismatch: target={surface} actual={result.EndSurface} (v={targetVH} spin={targetSpin} s={sampleId})");

            // §iter-7 Item 6: source_hole is col 2 (after mode).
            w.WriteLine(
                $"{mode},{hole},{surface}," +
                $"{targetVH:F1},{targetSpin:F0}," +
                $"{actualV:F3}," +
                $"{contact.x:F4},{contact.y:F4},{contact.z:F4}," +
                $"{endPos.x:F4},{endPos.y:F4},{endPos.z:F4}," +
                $"{result.EndSurface}," +
                $"{rollDist:F4}," +
                $"{bounces}," +
                $"{simDur:F4}," +
                $"{sampleId}," +
                $"{ts}");
        }

        void WriteWarningRow(StreamWriter w, string mode, int hole, SurfaceType surface,
            float targetVH, float targetSpin, int sampleId)
        {
            string ts = DateTime.Now.ToString("o");
            // §iter-7 Item 6: source_hole col 2.
            w.WriteLine(
                $"{mode},{hole},{surface}," +
                $"{targetVH:F1},{targetSpin:F0}," +
                $"TIMEOUT,0,0,0,0,0,0,Unknown,0,0,0,{sampleId},{ts}");
        }

        void WriteRealShotRow(StreamWriter w, int hole, string variantName,
            Vector3 teePos, ShotResult result, Trajectory traj)
        {
            Vector3 contact = FirstContactPos(traj);
            Vector3 endPos  = new Vector3(
                result.EndPosition.x.ToFloat(),
                result.EndPosition.y.ToFloat(),
                result.EndPosition.z.ToFloat());

            float carryM = XZDist(teePos, contact);
            float rollM  = XZDist(contact, endPos);
            float totalM = XZDist(teePos, endPos);
            float simDur = result.SimDuration.ToFloat();
            string ts    = DateTime.Now.ToString("o");

            w.WriteLine(
                $"{hole},{variantName}," +
                $"{teePos.x:F4},{teePos.y:F4},{teePos.z:F4}," +
                $"{contact.x:F4},{contact.y:F4},{contact.z:F4}," +
                $"{endPos.x:F4},{endPos.y:F4},{endPos.z:F4}," +
                $"{result.EndSurface}," +
                $"{carryM:F4},{rollM:F4},{totalM:F4}," +
                $"{result.BounceCount}," +
                $"{simDur:F4}," +
                $"{ts}");
        }

        void WriteRealShotWarningRow(StreamWriter w, int hole, string variantName, Vector3 teePos)
        {
            string ts = DateTime.Now.ToString("o");
            w.WriteLine(
                $"{hole},{variantName}," +
                $"{teePos.x:F4},{teePos.y:F4},{teePos.z:F4}," +
                $"TIMEOUT,0,0,0,0,0,0,Unknown,0,0,0,0,0,{ts}");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // README output
        // ─────────────────────────────────────────────────────────────────────────────

        void WriteReadme(string outDir, int totalCaptures)
        {
            string sha = "unknown";
            try
            {
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName               = "git";
                proc.StartInfo.Arguments              = "rev-parse --short HEAD";
                proc.StartInfo.WorkingDirectory       = Application.dataPath;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.UseShellExecute        = false;
                proc.StartInfo.CreateNoWindow         = true;
                proc.Start();
                sha = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
            }
            catch { }

            string holes1Str = string.Join(", ", _holesForSubMode1);
            string readme =
                $"# Surface Rollout Sweep Results\n\n" +
                $"**Timestamp:** {DateTime.Now:o}\n" +
                $"**Harness git SHA:** {sha}\n" +
                $"**Total captures:** {totalCaptures}\n" +
                $"**Holes for Sub-modes 1a+1b:** {holes1Str}\n" +
                $"**Holes for Sub-mode 2:** {string.Join(", ", _holesForSubMode2)}\n\n" +
                $"## Files\n" +
                $"- `sweep.csv` — Sub-modes 1a (roll) + 1b (putt) captures\n" +
                $"- `real_shots.csv` — Sub-mode 2 driver shots from tees\n" +
                $"- `progress.log` — resume checkpoint file\n\n" +
                $"## Config\n" +
                $"- Sub-mode 1a: {Surfaces1a.Length} surfaces × {Speeds1a.Length} speeds × {Spins1a.Length} spins × {Samples1a} samples per hole = {Surfaces1a.Length * Speeds1a.Length * Spins1a.Length * Samples1a} rows/hole\n" +
                $"- Sub-mode 1b: {Surfaces1b.Length} surfaces × {PuttSpeeds.Length} speeds × {Samples1b} samples per hole = {Surfaces1b.Length * PuttSpeeds.Length * Samples1b} rows/hole\n" +
                $"- Sub-mode 2: {_holesForSubMode2.Length} holes × 2 variants = {_holesForSubMode2.Length * 2} rows\n" +
                $"\n## Iter-7 fixes\n" +
                $"- Drop geometry restored: ball spawns at surfaceY+3m with -30° downward velocity.\n" +
                $"- PlaceBallAtAirborne() bypass: GetCurrentOrigin() uses exact spawn Y.\n" +
                $"- Sample jitter: sample_id=2 offsets spawn +{SampleJitterM}m in +X.\n" +
                $"- Spin axis fixed: (0,0,1) for +X shots → upward Magnus lift.\n" +
                $"- Draw shot fix: PlaceBallAtAirborne(teePos) before each real shot.\n" +
                $"- source_hole column added to sweep.csv.\n";

            File.WriteAllText(Path.Combine(outDir, "README.md"), readme);
        }
    }
}
#endif
