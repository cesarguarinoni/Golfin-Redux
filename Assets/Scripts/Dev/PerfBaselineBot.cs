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
using Golfin.Gameplay.UI.Quality;

using Golfin.Gameplay.UI.Controls.Bot;

namespace Golfin.Dev
{
    public class PerfBaselineBot : MonoBehaviour
    {
        // ── Schedule ────────────────────────────────────────────────────────────────────
        struct Job
        {
            public int hole; public string exp; public bool midflight; public string label;
            public bool teardown;   // perf_phase1_free_wins: drive the quit path and assert the restore
            public string tier;     // quality_tiers: "low"|"mid"|"high"|"auto"|null (null = leave as resolved)
            public float endurance; // quality_tiers: >0 = hold the pose this long, logging every 30 s
            public Job(int h, string e, bool mf, string l, bool td = false, string tr = null, float end = 0f)
            { hole = h; exp = e; midflight = mf; label = l; teardown = td; tier = tr; endurance = end; }
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
            // P — perf_phase1_free_wins acceptance (indices 9+; 0-8 are frozen so the Phase 0b
            // logs stay readable). exp="none" here does NOT mean "stock": every Phase 1 change is
            // baked into the build, so these ARE the after numbers. H01 had no job before —
            // Phase 1 normalises its 5000/50/5 tree distances, so it needs one.
            new Job(8, "none", false, "P1_h08_tee_after"),
            new Job(1, "none", false, "P1_h01_tee_after"),
            new Job(6, "none", false, "P1_h06_tee_after"),
            new Job(8, "none", true,  "P1_h08_midflight_after"),
            // Teardown gate: loads a hole, quits through the REAL in-game-settings widgets, and
            // asserts the shell camera + light come back. Writes teardown_invariants.json.
            new Job(8, "none", false, "P1_teardown", true),

            // T — quality_tiers (9a) acceptance. Indices 0-13 are FROZEN so the Phase 0b/Phase 1
            // logs stay readable; these start at 14 exactly as the spec pins them. exp="none"
            // throughout: a tier is not an experiment, it is the shipping configuration, applied
            // through QualityTierService before the hole loads so the URP asset swap is already
            // settled by the time anything is sampled.
            new Job(8, "none", false, "T_h08_tee_low",  false, "low"),      // 14
            new Job(8, "none", false, "T_h08_tee_mid",  false, "mid"),      // 15
            new Job(8, "none", false, "T_h08_tee_high", false, "high"),     // 16
            new Job(6, "none", false, "T_h06_tee_low",  false, "low"),      // 17
            new Job(6, "none", false, "T_h06_tee_mid",  false, "mid"),      // 18
            new Job(6, "none", false, "T_h06_tee_high", false, "high"),     // 19

            // Endurance. H06 is the hole Phase 1 could NOT hold (40.7 fps after 45 s at Serious),
            // so it is the one that answers "do static tiers make the thermal governor unnecessary".
            // 5 minutes, fps + thermal every 30 s.
            new Job(6, "none", false, "T_h06_endurance_high", false, "high", 300f),   // 20
            new Job(6, "none", false, "T_h06_endurance_mid",  false, "mid",  300f),   // 21
            new Job(6, "none", false, "T_h06_endurance_low",  false, "low",  300f),   // 22

            // H01 per tier. The spec's §7 job list stops at H08/H06, but the acceptance checklist
            // asks for an H01 tee row per tier as well — added here so that table is runnable
            // rather than hand-measured.
            new Job(1, "none", false, "T_h01_tee_low",  false, "low"),      // 23
            new Job(1, "none", false, "T_h01_tee_mid",  false, "mid"),      // 24
            new Job(1, "none", false, "T_h01_tee_high", false, "high"),     // 25
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

        /// <summary>
        /// The bot is OPT-IN on device as well as in the Editor.
        ///
        /// It used to spawn unconditionally in any GOLFIN_TESTBUILD player, which meant every
        /// launch of a dev build was hijacked: it drove the menus, picked a hole and held a pinned
        /// tee pose, so a human could not play the build they had just been handed. On device the
        /// arm signal is the presence of the job-override file the Mac-side runner already writes
        /// before every automated launch:
        ///
        ///     Documents/perfbot/job.txt        (devicectl device copy to --domain-type appDataContainer)
        ///
        /// Start() consumes and deletes that file, so exactly one launch is automated per push and
        /// the next launch belongs to whoever is holding the phone. Automation is unaffected —
        /// runjob.sh writes job.txt every time.
        /// </summary>
        static bool DeviceArmed()
        {
            try
            {
                var f = System.IO.Path.Combine(Application.persistentDataPath, "perfbot", "job.txt");
                if (System.IO.File.Exists(f)) return true;
                Debug.Log("[PerfBot] not armed — no Documents/perfbot/job.txt. The app is yours; " +
                          "push a job file to automate a launch.");
                return false;
            }
            catch (Exception e)
            {
                Debug.Log("[PerfBot] not armed — arm check failed: " + e.Message);
                return false;   // never hijack the app because the check itself broke
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
#if UNITY_EDITOR
            if (!EditorArmed) return;
#else
            if (!DeviceArmed()) return;
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
            string tierOverride = null;
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
                    // quality_tiers §7: "tier=low|mid|high|auto" anywhere in the file re-tiers the run
                    // WITHOUT needing a schedule entry — so a one-off "same job, other tier" A/B costs
                    // a file write instead of a rebuild. Positional, order-independent, optional.
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("tier=", StringComparison.OrdinalIgnoreCase))
                            tierOverride = part.Substring(5).Trim().ToLowerInvariant();
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
                 $"exp={job.exp} midflight={job.midflight} tier={job.tier ?? "<resolved>"} " +
                 $"endurance={job.endurance:F0}s thermalAtBoot={ThermalState()} " +
                 $"version={Application.version} device={SystemInfo.deviceModel}");

            // quality_tiers §7: BEFORE the hole loads. SetOverride swaps the URP asset immediately,
            // so render scale / shadows / HDR are already the tier's by the time any content streams
            // in — and the 30 fps cap on Low is in force for the whole navigation, not just the pose.
            ApplyTier(tierOverride ?? job.tier);

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
            // PIN THE SKY. SkyRandomizer rolls one preset per RUN, seeded from RoundSeed which
            // self-seeds from Random.Range on first access — so every launch got a different sun.
            // Phase 0b pinned the camera yaw but not this, which meant frames from different runs
            // were lit by different suns: 'Noon (Cloudy)' at 74.5 deg elevation in one run and
            // 'Morning' at 20.2 deg in the next. Long raking morning shadows read as "dark patches
            // on the fairway" that come and go between builds. A frame comparison is only evidence
            // if the sun is the same, so the seed is fixed here.
            PinSky();
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

            if (job.teardown)
            {
                yield return RunTeardownGate(job.hole);
                Mark($"JOB_DONE idx={jobIdx} run={runIdx} label={job.label} thermal={ThermalState()}");
                yield break;
            }

            if (job.endurance > 0f)
            {
                yield return RunEndurance(job, jobIdx, runIdx);
                Mark($"JOB_DONE idx={jobIdx} run={runIdx} label={job.label} thermal={ThermalState()}");
                yield break;
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

        /// <summary>
        /// Fixes SkyRandomizer's per-run roll to a constant so every measurement and every frame
        /// is lit identically. Logs the resulting preset so a run whose sky drifted is obvious.
        /// </summary>
        const int PinnedSkySeed = 20260826;
        static void PinSky()
        {
            try
            {
                var t = FindType("Golfin.Gameplay.Environment.SkyRandomizer") ?? FindType("SkyRandomizer");
                if (t == null) { Mark("WARN SkyRandomizer type not found — sky NOT pinned"); return; }
                t.GetMethod("EndRun", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                var setSeed = t.GetMethod("SetRoundSeed", BindingFlags.Public | BindingFlags.Static);
                if (setSeed == null) { Mark("WARN SkyRandomizer.SetRoundSeed missing — sky NOT pinned"); return; }
                setSeed.Invoke(null, new object[] { PinnedSkySeed });
                Mark($"SKY pinned RoundSeed={PinnedSkySeed}");
            }
            catch (Exception e) { Mark("WARN sky pin failed: " + e.Message); }
        }

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

        /// Fires through THE ONE DOOR EVERY BOT SWINGS THROUGH — BotSwing.PlayPerfect
        /// (bot_scheme_parity §3.5) — NOT PhysicsLabController.Fire(ShotPreset), which is a lab
        /// test seam, and no longer a hand-rolled BeginExternalDrag→ramp→EndExternalDrag either.
        ///
        /// ForceFlick, deliberately: perf baseline compares against build 2699 numbers; scheme UI
        /// cost is measured by scheme_evaluation. A baseline whose swing changed shape because a
        /// control-scheme preference moved would be comparing two different workloads and calling
        /// the difference a regression.
        IEnumerator FireDriverShot(float power01)
        {
            var labType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
            var lab = labType != null ? UnityEngine.Object.FindFirstObjectByType(labType) as MonoBehaviour : null;
            if (lab != null)
            {
                try { labType.GetMethod("SetClub", BindingFlags.Public | BindingFlags.Instance)?.Invoke(lab, new object[] { 0 }); }
                catch (Exception e) { Mark("WARN SetClub failed: " + e.Message); }
            }
            else Mark("WARN no PhysicsLabController — firing anyway via BotSwing");

            // Resolve() finds the ShotController for us: Golfin.DevHarness does not reference
            // Golfin.Gameplay.Input and so cannot name that type at all, which is why every step
            // of this shot used to be a reflection call.
            var ctx = BotExecutionContext.Resolve();
            if (!ctx.HasShot) { Mark("WARN no ShotController — mid-flight sample will be a TEE sample"); yield break; }

            yield return BotSwing.PlayPerfect(power01, aimYawRad: 0f, isPutt: false, ctx,
                                              new BotSwingOptions { ForceFlick = true });

            Mark($"SHOT fired via BotSwing.PlayPerfect(ForceFlick) (power={power01:F2})");
        }

        // ── Experiments (runtime only — never an asset edit) ────────────────────────────

        /// <summary>
        /// quality_tiers §7 — pin the run to a tier. null/empty leaves whatever the device resolved
        /// to, which is what jobs 0-13 want (they predate tiers and must stay comparable).
        /// "auto" explicitly CLEARS a previously pinned override, so a Low run cannot leak into the
        /// next launch through PlayerPrefs.
        /// </summary>
        static void ApplyTier(string tier)
        {
            if (string.IsNullOrEmpty(tier))
            {
                Mark($"TIER left as resolved: {QualityTierService.Current} " +
                     $"(source={(QualityTierService.IsOverride ? "override" : "auto")})");
                return;
            }

            int pref;
            switch (tier.ToLowerInvariant())
            {
                case "low":  pref = (int)QualityTier.Low;  break;
                case "mid":  pref = (int)QualityTier.Mid;  break;
                case "high": pref = (int)QualityTier.High; break;
                case "auto": pref = QualityTierService.AutoPref; break;
                default:
                    Mark($"WARN unknown tier '{tier}' — leaving as resolved ({QualityTierService.Current})");
                    return;
            }

            QualityTierService.SetOverride(pref);
            Mark($"TIER applied={QualityTierService.Current} pref={pref} " +
                 $"qualityLevel={QualitySettings.GetQualityLevel()} targetFrameRate={Application.targetFrameRate} " +
                 $"maxLOD={QualitySettings.maximumLODLevel} aniso={QualitySettings.anisotropicFiltering}");
        }

        /// <summary>
        /// quality_tiers §7 — the 5-minute endurance hold. Phase 1 put every COOLED pose at 60 fps;
        /// H08 then fell to 47.5 and H06 to 40.7 once the device reached thermal Serious. A cooled
        /// table therefore cannot answer whether static tiers are enough, and this can: one sample
        /// every 30 s, each with its thermal state, for the whole five minutes.
        ///
        /// Recorders run for the WHOLE hold rather than being restarted per sample, so each line is
        /// a rolling 60-frame median at that moment — the same statistic every other row in the
        /// report uses.
        /// </summary>
        IEnumerator RunEndurance(Job job, int jobIdx, int runIdx)
        {
            StartRecorders();
            yield return new WaitForSecondsRealtime(SampleSeconds);

            LogStats($"{job.label}_t000s", job.hole, job.exp, jobIdx, runIdx);
            yield return CaptureFrame($"{job.label}_run{runIdx}_t000s");

            const float StepSeconds = 30f;
            int steps = Mathf.RoundToInt(job.endurance / StepSeconds);
            for (int i = 1; i <= steps; i++)
            {
                yield return new WaitForSecondsRealtime(StepSeconds);
                float t = i * StepSeconds;
                LogStats($"{job.label}_t{t:000}s", job.hole, job.exp, jobIdx, runIdx);
                Mark($"ENDURANCE label={job.label} t={t:F0}s thermal={ThermalState()} frames={Time.frameCount}");
            }

            // A number without a frame is not evidence — same rule as the cooled samples. The LAST
            // frame is the one that matters here: it is the one drawn while throttled.
            yield return CaptureFrame($"{job.label}_run{runIdx}_t{job.endurance:000}s");
            StopRecorders();
        }

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

        // ── perf_phase1_free_wins teardown gate ─────────────────────────────────────────
        /// <summary>Live state of the two things §1 switches off, read straight off the objects.</summary>
        static void ShellState(out bool camFound, out bool camEnabled, out bool lightFound, out bool lightEnabled)
        {
            camFound = camEnabled = lightFound = lightEnabled = false;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && c.gameObject.scene.name == "ShellScene") { camFound = true; camEnabled = c.enabled; break; }
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l != null && l.type == LightType.Directional && l.gameObject.scene.name == "ShellScene")
                { lightFound = true; lightEnabled = l.enabled; break; }
        }

        /// <summary>
        /// Drives the player's own quit path — the in-game settings QUIT button and its confirm —
        /// then asserts the ShellScene camera and directional light are back on. This is the §1
        /// OnDestroy restore, checked on the device instead of by eye.
        /// </summary>
        IEnumerator RunTeardownGate(int hole)
        {
            var results = new List<string>();
            Action<string,bool,string> assert = (name, ok, detail) =>
            {
                results.Add($"{{\"assert\":\"{name}\",\"verdict\":\"{(ok ? "PASS" : "FAIL")}\",\"detail\":\"{detail}\"}}");
                Mark($"TEARDOWN {(ok ? "PASS" : "FAIL")} {name} — {detail}");
            };

            bool cf, ce, lf, le;
            ShellState(out cf, out ce, out lf, out le);
            assert("in_hole_shell_camera_disabled", cf && !ce, $"found={cf} enabled={ce}");
            assert("in_hole_shell_light_disabled",  lf && !le, $"found={lf} enabled={le}");

            // (i) quit mid-hole, through the REAL widgets.
            var modalType = FindType("Golfin.UI.Modals.InGameSettingsModalController")
                         ?? FindType("InGameSettingsModalController");
            bool clicked = false;
            if (modalType != null)
            {
                var modal = UnityEngine.Object.FindObjectsByType(modalType, FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                if (modal != null)
                {
                    var F = BindingFlags.NonPublic | BindingFlags.Instance;
                    var qb = modalType.GetField("quitButton", F)?.GetValue(modal) as UnityEngine.UI.Button;
                    var cb = modalType.GetField("confirmQuitButton", F)?.GetValue(modal) as UnityEngine.UI.Button;
                    if (qb != null) { qb.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.8f); }
                    if (cb != null) { cb.onClick.Invoke(); clicked = true; }
                }
            }
            assert("quit_driven_via_real_widget", clicked, clicked
                ? "InGameSettingsModalController.confirmQuitButton.onClick invoked"
                : "could not resolve the quit widgets");

            if (clicked)
            {
                float t0 = 0f;
                while (t0 < 30f && CurrentScreen() != "Home") { t0 += Time.unscaledDeltaTime; yield return null; }
                yield return new WaitForSecondsRealtime(2.5f);

                assert("returned_to_home", CurrentScreen() == "Home", "screen=" + CurrentScreen());

                ShellState(out cf, out ce, out lf, out le);
                assert("shell_camera_re_enabled", cf && ce, $"found={cf} enabled={ce}");
                assert("shell_light_re_enabled",  lf && le, $"found={lf} enabled={le}");

                bool labGone = !UnityEngine.SceneManagement.SceneManager.GetSceneByName("LabScaffold").isLoaded;
                assert("labscaffold_unloaded", labGone, "LabScaffold loaded=" + (!labGone));

                yield return CaptureFrame("P1_teardown_home_after_quit");

                // (iii) start a second hole — the Next-Hole case — and assert we switch off again.
                UnlockHole(hole);
                SnapCarouselToMode("practice");
                yield return null;
                var play = FindModeCardPlayButton("practice");
                if (play != null)
                {
                    play.onClick.Invoke();
                    yield return WaitForScreen("HoleSelection", 20f);
                    yield return new WaitForSecondsRealtime(1.5f);
                    TapHoleCard(hole);
                    yield return new WaitForSecondsRealtime(1.5f);
                    SeedAndLoad(hole);
                    yield return WaitForSceneLoaded($"Hole_{hole:00}_Geo", 60f);
                    yield return new WaitForSecondsRealtime(SettleSeconds);
                    ShellState(out cf, out ce, out lf, out le);
                    assert("second_hole_shell_camera_disabled_again", cf && !ce, $"found={cf} enabled={ce}");
                    yield return CaptureFrame("P1_teardown_second_hole");
                }
                else assert("second_hole_started", false, "no practice PLAY button on the second pass");
            }

            int fails = results.Count(r => r.Contains("\"FAIL\""));
            string json = "{\"gate\":\"perf_phase1_teardown\",\"fails\":" + fails +
                          ",\"assertions\":[" + string.Join(",", results) + "]}";
            try
            {
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "perfbot");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "teardown_invariants.json"), json);
            }
            catch (Exception e) { Mark("WARN teardown json write failed: " + e.Message); }
            Mark("TEARDOWN_JSON " + json);
            Mark($"TEARDOWN_RESULT fails={fails}");
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
