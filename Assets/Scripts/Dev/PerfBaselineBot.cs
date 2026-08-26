// PerfBaselineBot — the Phase 0b measurement harness (PERF_OPTIMIZATION_PLAN §8).
//
// ── IT CANNOT SHIP ──────────────────────────────────────────────────────────────────────
// Gated on GOLFIN_TESTBUILD. iOS-Full.asset — the profile the store pipeline builds via
// Tools/unity-build-ios.sh → CIBuild.BuildIOS → fastlane — carries ZERO scripting defines, so
// neither UNITY_EDITOR nor GOLFIN_TESTBUILD is defined there and this file compiles to nothing.
// Verified both directions: PerfBaselineBot appears in Builds/iOS-Dev/Il2CppOutputProject/
// .../Golfin.DevHarness_CodeGen.c and is absent from Builds/iOS-Full/Il2CppOutputProject.
//
// ── WHAT PHASE 0 GOT WRONG THAT THIS FIXES ──────────────────────────────────────────────
// §9.4 showed single captures are not evidence: the same hole and the same bot produced
// 5,483 vs 4,043 batches because camera yaw and tree-LOD selection drift run to run, and the
// device throttled 36% without anything recording that it had. So:
//   • YAW IS PINNED. The first run on a hole records the yaw it lands on; every later run
//     replays it. Same pose, therefore comparable geometry counts.
//   • THERMAL STATE IS RECORDED, from iOS itself (NSProcessInfo, via GolfinThermal.m). A run
//     that was not Nominal is identifiable after the fact instead of being silently averaged in.
//   • STATS COME OFF THE DEVICE. ProfilerRecorder samples on the player and the medians are
//     logged to the device console, so a capture needs no Editor, no profiler socket and no
//     window focus — all three of which cost hours in Phase 0.
//
// ── EXPERIMENTS ARE RUNTIME-ONLY, SO "REVERTED" IS STRUCTURAL ───────────────────────────
// Every toggle in §B of the Phase 0b brief is applied here at runtime — Camera.enabled,
// UniversalRenderPipelineAsset.shadowCascadeCount/shadowDistance, Terrain.basemapDistance/
// drawInstanced, ScriptableRendererFeature.SetActive, QualitySettings.maximumLODLevel. No
// asset is edited, so nothing can leak into a commit, and each run starts from a fresh process
// which means the revert is guaranteed rather than remembered. One build covers all of it.
//
// ── SCHEDULE ────────────────────────────────────────────────────────────────────────────
// One job per launch, 3 consecutive launches per job (the brief's 3-run median), then the
// cursor advances. The operator controls cooling by choosing when to relaunch. Every run logs
// "JOB idx=… run=… hole=… exp=…" so the log is self-describing and a wedged run cannot be
// silently mislabelled.
#if UNITY_EDITOR || GOLFIN_TESTBUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Dev
{
    public class PerfBaselineBot : MonoBehaviour
    {
        // ── Schedule ────────────────────────────────────────────────────────────────────
        struct Job
        {
            public int hole; public string exp; public bool midflight; public string label;
            public Job(int h, string e, bool mf, string l) { hole = h; exp = e; midflight = mf; label = l; }
        }

        static readonly Job[] Schedule =
        {
            // A — cooled baselines (Phase 0's H08/H06 were throttled)
            new Job(8, "none", false, "A1_h08_tee_baseline"),
            new Job(6, "none", false, "A2_h06_tee_baseline"),
            new Job(8, "none", true,  "A3_h08_midflight_baseline"),
            // B — experiments, one at a time, Hole 08
            new Job(8, "a",    false, "Ba_shellcam_off"),
            new Job(8, "b",    false, "Bb_cascades1_dist40"),
            new Job(8, "c",    false, "Bc_basemap100_instanced"),
            new Job(8, "d",    false, "Bd_decalfeature_off"),
            new Job(8, "e",    true,  "Be_maxLOD1_midflight"),
            new Job(8, "ad",   false, "Bad_shellcam_off_plus_decal_off"),
        };

        const int  RunsPerJob      = 3;
        const string JobIdxKey     = "PerfBot.JobIdx";
        const string RunIdxKey     = "PerfBot.RunIdx";
        const string YawKeyPrefix  = "PerfBot.Yaw.h";

        const float SettleSeconds    = 6f;    // "after the tee-idle glow settles"
        const float SampleSeconds    = 4f;    // window the 60-frame medians are taken over
        const float PoseHoldSeconds  = 45f;   // leaves room for an optional Editor-side capture

        // ── iOS thermal state (see Assets/Plugins/iOS/GolfinThermal.m) ──────────────────
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int GolfinGetThermalState();
        static string ThermalState()
        {
            try
            {
                switch (GolfinGetThermalState())
                {
                    case 0: return "Nominal";
                    case 1: return "Fair";
                    case 2: return "Serious";
                    case 3: return "Critical";
                    default: return "unavailable";
                }
            }
            catch (Exception e) { return "err:" + e.GetType().Name; }
        }
#else
        static string ThermalState() => "n/a-editor";
#endif

#if UNITY_EDITOR
        const string ArmedKey = "PerfBaselineBot.Armed";
        public static bool EditorArmed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        [UnityEditor.MenuItem("GOLFIN/Perf/Arm Perf Baseline Bot (next Play)")]
        static void ArmMenu()
        {
            EditorArmed = true;
            Debug.Log("[PerfBot] ARMED — next Play-mode session runs the current job. Clears on Editor restart.");
        }

        [UnityEditor.MenuItem("GOLFIN/Perf/Reset Schedule to job 0")]
        static void ResetScheduleMenu()
        {
            PlayerPrefs.SetInt(JobIdxKey, 0); PlayerPrefs.SetInt(RunIdxKey, 0); PlayerPrefs.Save();
            Debug.Log("[PerfBot] schedule reset → job 0 run 0.");
        }

        [UnityEditor.MenuItem("GOLFIN/Perf/Clear Pinned Yaws")]
        static void ClearYawMenu()
        {
            for (int h = 1; h <= 18; h++) PlayerPrefs.DeleteKey(YawKeyPrefix + h.ToString("00"));
            PlayerPrefs.Save();
            Debug.Log("[PerfBot] pinned yaws cleared — the next run per hole re-pins.");
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
#if UNITY_EDITOR
            if (!EditorArmed) return;
#endif
            var go = new GameObject("~PerfBaselineBot");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfBaselineBot>();
        }

        static void Mark(string message) => Debug.Log("[PerfBot] " + message);

        // ── Profiler counters, sampled ON DEVICE ────────────────────────────────────────
        // Names as they appear in ProfilerRecorderDescription; resolved by scanning the
        // available handles so a rename shows up as "MISSING" in the log instead of a zero.
        static readonly string[] Counters =
        {
            "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "CPU Total Frame Time",
            "Batches Count", "SetPass Calls Count", "Draw Calls Count",
            "Triangles Count", "Vertices Count", "Shadow Casters Count",
            "System Used Memory", "Total Reserved Memory", "GC Allocated In Frame",
        };

        readonly Dictionary<string, ProfilerRecorder> _rec = new Dictionary<string, ProfilerRecorder>();

        void StartRecorders()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var byName = new Dictionary<string, ProfilerRecorderHandle>();
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (!byName.ContainsKey(d.Name)) byName[d.Name] = h;
            }
            var missing = new List<string>();
            foreach (var name in Counters)
            {
                if (byName.TryGetValue(name, out var h))
                {
                    var r = new ProfilerRecorder(h, 60, ProfilerRecorderOptions.Default);
                    r.Start();
                    _rec[name] = r;
                }
                else missing.Add(name);
            }
            Mark($"RECORDERS started={_rec.Count} missing=[{string.Join(", ", missing)}] available={handles.Count}");
        }

        void StopRecorders()
        {
            foreach (var kv in _rec) { try { kv.Value.Dispose(); } catch { } }
            _rec.Clear();
        }

        double Median(string name)
        {
            if (!_rec.TryGetValue(name, out var r) || !r.Valid || r.Count == 0) return double.NaN;
            int n = r.Count;
            var vals = new List<long>(n);
            for (int i = 0; i < n; i++) vals.Add(r.GetSample(i).Value);
            vals.Sort();
            return vals[vals.Count / 2];
        }

        /// The whole point: one line, in the device log, with everything needed to fill a row.
        void LogStats(string label, int hole, string exp, int jobIdx, int runIdx)
        {
            double NS(string k) => Median(k) / 1e6;   // ns → ms
            double frameMs = NS("CPU Total Frame Time");
            double fps = frameMs > 0.0001 ? 1000.0 / frameMs : double.NaN;

            Mark($"STATS job={jobIdx} run={runIdx} label={label} hole={hole} exp={exp} " +
                 $"thermal={ThermalState()} " +
                 $"fps={fps:F1} frameMs={frameMs:F2} " +
                 $"mainMs={NS("CPU Main Thread Frame Time"):F2} renderMs={NS("CPU Render Thread Frame Time"):F2} " +
                 $"batches={Median("Batches Count"):F0} setpass={Median("SetPass Calls Count"):F0} " +
                 $"draws={Median("Draw Calls Count"):F0} tris={Median("Triangles Count"):F0} " +
                 $"verts={Median("Vertices Count"):F0} shadowCasters={Median("Shadow Casters Count"):F0} " +
                 $"sysMemMB={Median("System Used Memory") / 1048576.0:F1} " +
                 $"reservedMB={Median("Total Reserved Memory") / 1048576.0:F1} " +
                 $"gcPerFrameB={Median("GC Allocated In Frame"):F0}");
        }

        // ── Run ─────────────────────────────────────────────────────────────────────────
        IEnumerator Start()
        {
            // OPERATOR OVERRIDE. PlayerPrefs on a device cannot be written from the Mac, so the
            // auto-advancing cursor is the only control — and re-reaching an earlier job costs a
            // full lap of the schedule. A one-shot override file in the app's Documents container
            // (writable via `devicectl device copy to --domain-type appDataContainer`) lets the
            // Mac name the exact job. Format: "<jobIdx>" or "<jobIdx> <runIdx>". Consumed and
            // deleted on read, so a stale file cannot silently pin the schedule.
            int jobIdx = Mathf.Clamp(PlayerPrefs.GetInt(JobIdxKey, 0), 0, Schedule.Length - 1);
            int runIdx = PlayerPrefs.GetInt(RunIdxKey, 0);
            bool overridden = false;
            try
            {
                var ovr = System.IO.Path.Combine(Application.persistentDataPath, "perfbot", "job.txt");
                if (System.IO.File.Exists(ovr))
                {
                    var parts = System.IO.File.ReadAllText(ovr).Trim()
                                  .Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1 && int.TryParse(parts[0], out int j))
                    {
                        jobIdx = Mathf.Clamp(j, 0, Schedule.Length - 1);
                        runIdx = (parts.Length >= 2 && int.TryParse(parts[1], out int r)) ? Mathf.Clamp(r, 0, RunsPerJob - 1) : 0;
                        overridden = true;
                    }
                    System.IO.File.Delete(ovr);
                }
            }
            catch (Exception e) { Mark("WARN job override read failed: " + e.Message); }

            if (runIdx < 0 || runIdx >= RunsPerJob) runIdx = 0;
            var job = Schedule[jobIdx];

            // Advance immediately: a wedged run must not trap the schedule on one job forever.
            int nextRun = runIdx + 1, nextJob = jobIdx;
            if (nextRun >= RunsPerJob) { nextRun = 0; nextJob = (jobIdx + 1) % Schedule.Length; }
            PlayerPrefs.SetInt(RunIdxKey, nextRun);
            PlayerPrefs.SetInt(JobIdxKey, nextJob);
            PlayerPrefs.Save();
            if (overridden) Mark($"JOB OVERRIDE from job.txt → job={jobIdx} run={runIdx}");

            Mark($"JOB idx={jobIdx} run={runIdx}/{RunsPerJob} label={job.label} hole={job.hole} " +
                 $"exp={job.exp} midflight={job.midflight} thermalAtBoot={ThermalState()} " +
                 $"version={Application.version} device={SystemInfo.deviceModel}");

            yield return new WaitForSecondsRealtime(2f);

            yield return NavigateToHome(90f);
            if (CurrentScreen() != "Home")
            { Mark($"ABORT never reached Home (screen={CurrentScreen()})"); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);

            UnlockHole(job.hole);
            SnapCarouselToMode("practice");
            yield return null;
            var play = FindModeCardPlayButton("practice");
            if (play == null) { Mark("ABORT no active practice PLAY button"); yield break; }
            play.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);

            yield return WaitForScreen("HoleSelection", 20f);
            if (CurrentScreen() != "HoleSelection")
            { Mark($"ABORT never reached HoleSelection (screen={CurrentScreen()})"); yield break; }
            yield return new WaitForSecondsRealtime(2f);

            TapHoleCard(job.hole);
            yield return new WaitForSecondsRealtime(1.5f);
            if (!SeedAndLoad(job.hole)) { Mark("ABORT seed/BeginGameplayLoad failed"); yield break; }

            yield return WaitForSceneLoaded("LabScaffold", 60f);
            yield return WaitForSceneLoaded($"Hole_{job.hole:00}_Geo", 60f);
            yield return new WaitForSecondsRealtime(SettleSeconds);

            // ── Pin the pose BEFORE applying any experiment, so geometry is comparable ──
            ApplyPinnedYaw(job.hole);
            yield return new WaitForSecondsRealtime(1.5f);

            // ── Apply the experiment ───────────────────────────────────────────────────
            ApplyExperiment(job.exp);
            yield return new WaitForSecondsRealtime(2f);

            var terr = Terrain.activeTerrain;
            Mark($"TEE hole={job.hole} terrain={(terr != null ? terr.name : "<none>")} " +
                 $"terrainTrees={(terr != null && terr.terrainData != null ? terr.terrainData.treeInstanceCount : -1)} " +
                 $"thermal={ThermalState()}");

            if (job.midflight)
            {
                Mark("SHOT firing driver for the mid-flight sample");
                yield return FireDriverShot(1.0f);
                yield return new WaitForSecondsRealtime(1.2f);   // into the flight, before apex
            }

            // ── Sample ─────────────────────────────────────────────────────────────────
            StartRecorders();
            yield return new WaitForSecondsRealtime(SampleSeconds);
            LogStats(job.label, job.hole, job.exp, jobIdx, runIdx);

            // A number without a frame is not evidence. Experiment (d) disabled the decal
            // feature at runtime and rendered the terrain BLACK — a broken frame draws less
            // work and therefore reads as a "win". Every measurement now carries a PNG so a
            // visually broken run is caught instead of being reported as an optimisation.
            yield return CaptureFrame($"{job.label}_run{runIdx}");
            Mark($"POSE_READY job={jobIdx} run={runIdx} label={job.label} — holding {PoseHoldSeconds:F0}s");
            yield return new WaitForSecondsRealtime(PoseHoldSeconds);
            LogStats(job.label + "_late", job.hole, job.exp, jobIdx, runIdx);
            StopRecorders();
            Mark($"JOB_DONE idx={jobIdx} run={runIdx} label={job.label} thermal={ThermalState()}");

            // Soak so a thermal reading is still available if the operator wants one.
            float soaked = 0f;
            while (true)
            {
                yield return new WaitForSecondsRealtime(30f);
                soaked += 30f;
                Mark($"SOAK t={soaked:F0}s thermal={ThermalState()} frames={Time.frameCount}");
            }
        }

        void OnDestroy() => StopRecorders();

        /// Save the sampled frame next to the numbers, into the app's Documents container so the
        /// Mac can pull it with `devicectl device copy from --domain-type appDataContainer`.
        ///
        /// WHY THIS EXISTS: experiment (d) disabled the decal renderer feature at runtime and
        /// rendered the terrain BLACK. A broken frame draws LESS work, so it reads as a large
        /// "win" in every counter — and it was only caught because a human looked at the phone.
        /// A number without a frame is not evidence; every measurement now carries a PNG.
        ///
        /// Uses CaptureScreenshotAsTexture (synchronous, after WaitForEndOfFrame) — CLAUDE.md
        /// bans ScreenCapture.CaptureScreenshot(path) outright.
        IEnumerator CaptureFrame(string label)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "perfbot");
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, label + ".png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Mark($"FRAME saved {label}.png ({tex.width}x{tex.height})");
            }
            catch (Exception e) { Mark("WARN frame capture failed: " + e.Message); }
            finally { if (tex != null) UnityEngine.Object.Destroy(tex); }
        }

        // ── Pose pinning ────────────────────────────────────────────────────────────────

        static void ApplyPinnedYaw(int hole)
        {
            try
            {
                var labType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
                if (labType == null) { Mark("WARN PhysicsLabController not found — yaw NOT pinned"); return; }
                var lab = UnityEngine.Object.FindFirstObjectByType(labType) as MonoBehaviour;
                if (lab == null) { Mark("WARN no PhysicsLabController instance — yaw NOT pinned"); return; }

                var yawField = labType.GetField("_cameraYaw", BindingFlags.NonPublic | BindingFlags.Instance);
                var setYaw   = labType.GetMethod("SetCameraYawRadians",
                                   BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (yawField == null || setYaw == null)
                { Mark("WARN _cameraYaw/SetCameraYawRadians not found — yaw NOT pinned"); return; }

                string key = YawKeyPrefix + hole.ToString("00");
                if (PlayerPrefs.HasKey(key))
                {
                    float pinned = PlayerPrefs.GetFloat(key);
                    setYaw.Invoke(lab, new object[] { pinned });
                    Mark($"YAW replayed hole={hole} yaw={pinned:F5} rad ({pinned * Mathf.Rad2Deg:F2} deg)");
                }
                else
                {
                    float landed = (float)yawField.GetValue(lab);
                    PlayerPrefs.SetFloat(key, landed); PlayerPrefs.Save();
                    Mark($"YAW pinned hole={hole} yaw={landed:F5} rad ({landed * Mathf.Rad2Deg:F2} deg) — first run, recorded");
                }
            }
            catch (Exception e) { Mark("WARN yaw pin failed: " + e.Message); }
        }

        /// Fires through the PRODUCTION drag path — BeginExternalDrag → ramp SetExternalPower →
        /// EndExternalDrag → CommitFlick — NOT PhysicsLabController.Fire(ShotPreset), which is a
        /// lab test seam. Same sequence BotDriver.FireDriverShot uses, reached by reflection
        /// because Golfin.DevHarness does not reference Golfin.Gameplay.Input.
        IEnumerator FireDriverShot(float power01)
        {
            var labType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
            var lab = labType != null ? UnityEngine.Object.FindFirstObjectByType(labType) as MonoBehaviour : null;
            if (lab != null)
            {
                try { labType.GetMethod("SetClub", BindingFlags.Public | BindingFlags.Instance)?.Invoke(lab, new object[] { 0 }); }
                catch (Exception e) { Mark("WARN SetClub failed: " + e.Message); }
            }
            else Mark("WARN no PhysicsLabController — firing anyway via ShotController");

            var scType = FindType("Golfin.Gameplay.Input.ShotController");
            var sc = scType != null ? UnityEngine.Object.FindFirstObjectByType(scType) as MonoBehaviour : null;
            if (sc == null) { Mark("WARN no ShotController — mid-flight sample will be a TEE sample"); yield break; }

            var stateProp = scType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            var begin = scType.GetMethod("BeginExternalDrag", BindingFlags.Public | BindingFlags.Instance);
            var setP  = scType.GetMethod("SetExternalPower",  BindingFlags.Public | BindingFlags.Instance);
            var end   = scType.GetMethod("EndExternalDrag",   BindingFlags.Public | BindingFlags.Instance);
            if (begin == null || setP == null || end == null)
            { Mark("WARN ShotController drag API not found — mid-flight sample will be a TEE sample"); yield break; }

            // Idle gate: firing before the controller is ready silently no-ops.
            float w = 0f;
            while (w < 4f)
            {
                var s = stateProp?.GetValue(sc)?.ToString();
                if (s == null || s == "Idle") break;
                yield return null; w += Time.unscaledDeltaTime;
            }

            // Reflection does NOT apply C# default arguments: EndExternalDrag(bool bypassFlickGate
            // = false) has ONE parameter, so a zero-arg Invoke throws TargetParameterCountException
            // — which killed the coroutine before sampling on the first attempt. Build every args
            // array from the real parameter count, and never let an Invoke abort the run: a failed
            // shot should degrade to a tee sample, not lose the whole measurement.
            object[] EndArgs() => end.GetParameters().Length == 0 ? null : new object[] { false };

            bool ok = true;
            try { begin.Invoke(sc, null); }
            catch (Exception e) { Mark("WARN BeginExternalDrag failed: " + e.Message); ok = false; }

            if (ok)
            {
                const float ramp = 0.85f;
                float t = 0f;
                while (t < ramp)
                {
                    t += Time.unscaledDeltaTime;
                    try { setP.Invoke(sc, new object[] { Mathf.Lerp(0f, power01, t / ramp), 0f }); }
                    catch (Exception e) { Mark("WARN SetExternalPower failed: " + e.Message); ok = false; break; }
                    yield return null;
                }
            }

            if (ok)
            {
                try { setP.Invoke(sc, new object[] { power01, 0f }); }
                catch (Exception e) { Mark("WARN SetExternalPower(final) failed: " + e.Message); ok = false; }
            }
            yield return new WaitForSecondsRealtime(0.18f);
            if (ok)
            {
                try { end.Invoke(sc, EndArgs()); Mark($"SHOT fired via production drag path (power={power01:F2})"); }
                catch (Exception e) { Mark("WARN EndExternalDrag failed: " + e.Message); ok = false; }
            }
            if (!ok) Mark("SHOT FAILED — this run's sample is a TEE sample, not mid-flight. Label accordingly.");
        }

        // ── Experiments (runtime only — never an asset edit) ────────────────────────────

        static void ApplyExperiment(string exp)
        {
            switch (exp)
            {
                case "none": Mark("EXP none (baseline)"); break;
                case "a":    ExpShellCameraOff(); break;
                case "b":    ExpShadowDiet(); break;
                case "c":    ExpTerrainBasemap(); break;
                case "d":    ExpDecalFeatureOff(); break;
                case "e":    ExpMaxLod1(); break;
                case "ad":   ExpShellCameraOff(); ExpDecalFeatureOff(); break;
                default:     Mark("WARN unknown experiment '" + exp + "' — running as baseline"); break;
            }
        }

        static void ExpShellCameraOff()
        {
            int off = 0;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (cam.gameObject.scene.name == "ShellScene") { cam.enabled = false; off++; }
            Mark($"EXP a — ShellScene cameras disabled: {off}");
        }

        static void ExpShadowDiet()
        {
            try
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (rp == null) { Mark("WARN no currentRenderPipeline"); return; }
                var t = rp.GetType();
                var cascades = t.GetProperty("shadowCascadeCount", BindingFlags.Public | BindingFlags.Instance);
                var dist     = t.GetProperty("shadowDistance",     BindingFlags.Public | BindingFlags.Instance);
                string before = $"cascades={cascades?.GetValue(rp)} dist={dist?.GetValue(rp)}";
                cascades?.SetValue(rp, 1);
                dist?.SetValue(rp, 40f);
                Mark($"EXP b — shadow diet: {before} → cascades={cascades?.GetValue(rp)} dist={dist?.GetValue(rp)}");
            }
            catch (Exception e) { Mark("WARN exp b failed: " + e.Message); }
        }

        static void ExpTerrainBasemap()
        {
            int n = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Mark($"EXP c — {t.name} before: basemapDistance={t.basemapDistance} drawInstanced={t.drawInstanced}");
                t.basemapDistance = 100f;
                t.drawInstanced = true;
                n++;
            }
            Mark($"EXP c — terrains modified: {n} (basemapDistance=100, drawInstanced=true)");
        }

        static void ExpDecalFeatureOff()
        {
            try
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (rp == null) { Mark("WARN no currentRenderPipeline"); return; }
                var listField = rp.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (listField == null) { Mark("WARN m_RendererDataList not found"); return; }
                var datas = listField.GetValue(rp) as Array;
                if (datas == null) { Mark("WARN renderer data list null"); return; }

                int disabled = 0;
                foreach (var data in datas)
                {
                    if (data == null) continue;
                    var featProp = data.GetType().GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.Instance);
                    var feats = featProp?.GetValue(data) as System.Collections.IList;
                    if (feats == null) continue;
                    foreach (var f in feats)
                    {
                        if (f == null) continue;
                        if (!f.GetType().Name.Contains("Decal")) continue;
                        f.GetType().GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance)
                                   ?.Invoke(f, new object[] { false });
                        disabled++;
                        Mark($"EXP d — disabled renderer feature: {f.GetType().Name}");
                    }
                    data.GetType().GetMethod("SetDirty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?.Invoke(data, null);
                }
                Mark($"EXP d — decal features disabled: {disabled}");
            }
            catch (Exception e) { Mark("WARN exp d failed: " + e.Message); }
        }

        static void ExpMaxLod1()
        {
            int before = QualitySettings.maximumLODLevel;
            QualitySettings.maximumLODLevel = 1;
            Mark($"EXP e — maximumLODLevel {before} → {QualitySettings.maximumLODLevel}");
        }

        // ── Navigation (ported from Golfin.Physics.Viewer.Bot.BotDriver — see git history;
        //    that file lives under Assets/Scripts/Physics/, which CLAUDE.md bans editing) ───

        IEnumerator NavigateToHome(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                string cur = CurrentScreen();
                if (cur == "Home") yield break;
                if (cur == "Splash" || cur == "Logo") break;
                yield return new WaitForSecondsRealtime(0.5f); elapsed += 0.5f;
            }
            while (elapsed < timeoutSeconds && CurrentScreen() == "Logo")
            { yield return new WaitForSecondsRealtime(0.5f); elapsed += 0.5f; }

            if (CurrentScreen() == "Splash")
            {
                yield return new WaitForSecondsRealtime(0.5f);
                var start = FindActiveButtonNamed("StartButton");
                if (start != null) { start.onClick.Invoke(); Mark("SPLASH StartButton pressed"); }
                else Mark("WARN StartButton not found on Splash");
                elapsed += 1f;
            }
            while (elapsed < timeoutSeconds)
            {
                if (CurrentScreen() == "Home") yield break;
                yield return new WaitForSecondsRealtime(0.5f); elapsed += 0.5f;
            }
        }

        IEnumerator WaitForScreen(string screen, float timeoutSeconds)
        {
            float t = 0f;
            while (t < timeoutSeconds && CurrentScreen() != screen)
            { yield return new WaitForSecondsRealtime(0.25f); t += 0.25f; }
        }

        IEnumerator WaitForSceneLoaded(string sceneName, float timeoutSeconds)
        {
            float t = 0f;
            while (t < timeoutSeconds)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name == sceneName)
                    { Mark($"SCENE {sceneName} loaded after {t:F1}s"); yield break; }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Mark($"WARN scene {sceneName} never loaded within {timeoutSeconds:F0}s");
        }

        static Button FindActiveButtonNamed(string name)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.gameObject.name == name && b.isActiveAndEnabled) return b;
            return null;
        }

        static void SnapCarouselToMode(string modeId)
        {
            try
            {
                Type carouselType = FindType("GolfinRedux.UI.ModeSelect.ModeCarouselController");
                if (carouselType == null) { Mark("WARN ModeCarouselController not found"); return; }
                var carousel = UnityEngine.Object.FindFirstObjectByType(carouselType) as MonoBehaviour;
                if (carousel == null) { Mark("WARN no ModeCarouselController in scene"); return; }

                var cardsField     = carouselType.GetField("_allCards", BindingFlags.NonPublic | BindingFlags.Instance);
                var centeredField  = carouselType.GetField("_centeredVirtualIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                var dataCountField = carouselType.GetField("_dataCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cardsField == null || centeredField == null || dataCountField == null)
                { Mark("WARN carousel private fields not found"); return; }

                var allCards = cardsField.GetValue(carousel) as System.Collections.IList;
                if (allCards == null || allCards.Count == 0) { Mark("WARN _allCards empty"); return; }

                Type cardType = FindType("GolfinRedux.UI.ModeSelect.ModeCardController");
                var modeIdProp = cardType?.GetProperty("ModeId", BindingFlags.Public | BindingFlags.Instance);
                int dataCount = (int)dataCountField.GetValue(carousel);

                int target = -1;
                for (int i = 0; i < allCards.Count; i++)
                {
                    var card = allCards[i] as MonoBehaviour;
                    if (card == null) continue;
                    string id = modeIdProp?.GetValue(card) as string;
                    if (id == null || !id.Equals(modeId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (i >= dataCount && i < 2 * dataCount) { target = i; break; }
                    if (target < 0) target = i;
                }
                if (target < 0) { Mark($"WARN no card with modeId='{modeId}'"); return; }

                centeredField.SetValue(carousel, target);
                carouselType.GetMethod("ApplyCardStates", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(carousel, null);
            }
            catch (Exception e) { Mark("WARN carousel snap failed: " + e.Message); }
        }

        static Button FindModeCardPlayButton(string modeId)
        {
            try
            {
                Type cardType = FindType("GolfinRedux.UI.ModeSelect.ModeCardController");
                if (cardType == null) { Mark("WARN ModeCardController not found"); return null; }
                var modeIdProp = cardType.GetProperty("ModeId", BindingFlags.Public | BindingFlags.Instance);
                var playField  = cardType.GetField("playButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playField == null) { Mark("WARN playButton field not found"); return null; }

                foreach (var card in UnityEngine.Object.FindObjectsByType(cardType, FindObjectsSortMode.None))
                {
                    string id = modeIdProp?.GetValue(card) as string;
                    if (id == null || !id.Equals(modeId, StringComparison.OrdinalIgnoreCase)) continue;
                    var btn = playField.GetValue(card) as Button;
                    if (btn != null && btn.gameObject.activeInHierarchy && btn.isActiveAndEnabled) return btn;
                }
                return null;
            }
            catch (Exception e) { Mark("WARN find play button failed: " + e.Message); return null; }
        }

        static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { t = asm.GetType(fullName); if (t != null) return t; }
            return null;
        }

        static string CurrentScreen()
        {
            try
            {
                var smType = FindType("GolfinRedux.UI.ScreenManager") ?? FindType("ScreenManager");
                if (smType == null) return "<no ScreenManager>";
                var inst = smType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (inst == null) return "<no instance>";
                return smType.GetProperty("CurrentScreen", BindingFlags.Public | BindingFlags.Instance)
                             ?.GetValue(inst)?.ToString() ?? "<null>";
            }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        static void UnlockHole(int hole)
        {
            try
            {
                var svcType = FindType("GolfinRedux.UI.HoleSelection.HoleProgressionService");
                if (svcType == null) { Mark("WARN HoleProgressionService not found"); return; }
                var inst = svcType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                svcType.GetMethod("SetUnlockedOverride", new[] { typeof(int), typeof(bool) })
                       ?.Invoke(inst, new object[] { hole, true });
            }
            catch (Exception e) { Mark("WARN unlock failed: " + e.Message); }
        }

        static void TapHoleCard(int hole)
        {
            try
            {
                var cardType = FindType("GolfinRedux.UI.HoleSelection.HoleCardController");
                if (cardType == null) { Mark("WARN HoleCardController not found"); return; }
                var holeNumProp = cardType.GetProperty("HoleNumber");
                foreach (var card in UnityEngine.Object.FindObjectsByType(cardType, FindObjectsSortMode.None))
                {
                    if ((int)(holeNumProp?.GetValue(card) ?? 0) != hole) continue;
                    var go = ((Component)card).gameObject;
                    Button tap = null;
                    foreach (var b in go.GetComponentsInChildren<Button>(true))
                        if (b.gameObject.name.Contains("CardTapButton") || b.gameObject.name.Contains("TapButton")) { tap = b; break; }
                    if (tap == null) tap = go.GetComponentInChildren<Button>();
                    if (tap != null) { tap.onClick.Invoke(); Mark($"CARDTAP hole={hole}"); }
                    return;
                }
                Mark($"WARN no HoleCardController for hole {hole}");
            }
            catch (Exception e) { Mark("WARN card tap failed: " + e.Message); }
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gsType = FindType("Golfin.Gameplay.Session.GameSession") ?? FindType("GameSession");
                if (gsType == null) { Mark("WARN GameSession not found"); return false; }
                gsType.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);

                string charId = "";
                var cmType = FindType("CharacterManager");
                if (cmType != null)
                {
                    var cmInst = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (cmInst != null) charId = (string)(cmType.GetMethod("GetSelectedCharacterId")?.Invoke(cmInst, null) ?? "");
                }
                int bagSlot = 0;
                var bmType = FindType("BagManager");
                if (bmType != null)
                {
                    var bmInst = bmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (bmInst != null) bagSlot = (int)(bmType.GetProperty("EquippedBagSlot")?.GetValue(bmInst) ?? 0);
                }
                gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { hole, charId, bagSlot });

                var loaderType = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader") ?? FindType("GameplaySceneLoader");
                if (loaderType == null) { Mark("WARN GameplaySceneLoader not found"); return false; }
                var loaderInst = loaderType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (loaderInst == null) { Mark("WARN GameplaySceneLoader.Instance null"); return false; }

                MethodInfo begin = loaderType.GetMethods().FirstOrDefault(m => m.Name == "BeginGameplayLoad");
                if (begin == null) { Mark("WARN BeginGameplayLoad not found"); return false; }
                var pars = begin.GetParameters();
                begin.Invoke(loaderInst, pars.Length == 1 ? new object[] { hole } : new object[] { hole, null });
                Mark($"LOAD BeginGameplayLoad({hole})");
                return true;
            }
            catch (Exception e) { Mark("WARN seed/load failed: " + e.Message); return false; }
        }
    }
}
#endif
