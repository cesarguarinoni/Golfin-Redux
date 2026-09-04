#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Physics.Viewer;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Acceptance capture bot for tree_occlusion_fade (SPEC §5). Drives the REAL player entry path —
    /// ShellScene → splash PLAY → Home PRACTICE PLAY → hole-selection PLAY → Lomond Hole 1 tee —
    /// then plays a shot into the tree line and captures the frames the spec's acceptance list needs.
    ///
    /// Modeled on TeeIdleGlowDemoRecorder (same boot nav) and BotDriver (same production fire path:
    /// ShotController BeginExternalDrag → ramped SetExternalPower → EndExternalDrag).
    ///
    /// Captures (all via the sanctioned CaptureCore.SnapPlayModeSafe — sync, no pause, no
    /// AssetDatabase.Refresh, so the coroutine survives):
    ///   tee_on / tee_off        — §5.3 + §5.8 zero-diff when the corridor is clear
    ///   flight_NNN              — §5.1 + §5.2 + §5.4 window tracks the live ball, consecutive frames
    ///   rest_on / rest_off      — §5.1 + §5.8 at-rest occluded A/B
    ///   map_open                — §5.5 no window in the top-down map view
    ///
    /// Usage: GOLFIN > Physics > Capture Tree Occlude Fade
    /// Output: Docs/Specs/Active/tree_occlusion_fade/screenshots/
    /// </summary>
    public static class TreeOccludeFadeCaptureBot
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "TreeOccludeFadeCapture.Armed";
        public const string OutDir  = "Docs/Specs/Active/tree_occlusion_fade/screenshots";
        public const string LogPath = "Docs/Specs/Active/tree_occlusion_fade/capture_log.txt";

        /// Set true from the orchestrator to release the frozen-occluder hold (see §5 A/B capture).
        public static bool ReleaseHold;

        /// When true the bot records one continuous MP4 of the A/B instead of stopping to hold.
        public static bool RecordMode;
        public const string VideoDir  = "Docs/Specs/Active/tree_occlusion_fade/videos";
        public const string RawStem   = "raw_tree_occlude_fade";
        public static string RawPathNoExt => VideoDir + "/" + RawStem;

        static RecorderController _recorder;

        [MenuItem("GOLFIN/Physics/Record Tree Occlude Fade Video")]
        public static void LaunchVideo()
        {
            RecordMode = true;
            SessionState.SetBool("TreeOccludeFadeCapture.Record", true);
            Launch();
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = System.Reflection.Assembly.Load("Golfin.Physics.Viewer.BotEditor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        /// Pin the device size BEFORE StartRecording. Locking render state here is what keeps the
        /// Game View RT from being recreated mid-record (the Y-flip trigger). Nothing reads an RT
        /// while this is running — no CaptureCore/ScreenCapture calls in record mode.
        public static void StartClip()
        {
            bool pinned = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!pinned)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--;
                    if (h % 2 != 0) h--;
                    Debug.LogWarning($"[OccFadeBot] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }
            Directory.CreateDirectory(VideoDir);
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TreeOccludeFade";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = RawPathNoExt;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[OccFadeBot] Recording → {RawPathNoExt}.mp4 ({w}x{h} @30fps)");
        }

        public static void StopClip()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); }
            catch (Exception e) { Debug.LogWarning("[OccFadeBot] StopClip: " + e.Message); }
            _recorder = null;
            Debug.Log("[OccFadeBot] Recording stopped.");
        }

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Physics/Capture Tree Occlude Fade")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[OccFadeBot] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[OccFadeBot] Armed — entering play mode, driving the real boot path.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            SessionState.SetBool(ArmedKey, false);
            RecordMode = SessionState.GetBool("TreeOccludeFadeCapture.Record", false);
            SessionState.SetBool("TreeOccludeFadeCapture.Record", false);
            Application.runInBackground = true;   // else captures return the wrong/splash frame

            var host = new GameObject("[TreeOccludeFadeCaptureBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TreeOccludeFadeCaptureRunner>().Begin();
        }
    }

    public class TreeOccludeFadeCaptureRunner : MonoBehaviour
    {
        readonly List<string> _log = new List<string>();
        bool _froze;

        public void Begin() => StartCoroutine(Run());

        void L(string s)
        {
            _log.Add($"[{Time.realtimeSinceStartup:F1}s] {s}");
            Debug.Log("[OccFadeBot] " + s);
            Flush();   // write through, so a stall is diagnosable from the file mid-run
        }

        void Flush()
        {
            try { File.WriteAllLines(TreeOccludeFadeCaptureBot.LogPath, _log); } catch { }
        }

        static Type FindType(string full) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

        static Button FindActiveButtonNamed(string goName) =>
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == goName && b.gameObject.activeInHierarchy);

        IEnumerator WaitUntilOrFail(Func<bool> cond, float timeout, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond())
            {
                if (Time.realtimeSinceStartup - t0 > timeout)
                {
                    L($"TIMEOUT waiting for {what} ({timeout:F0}s) — aborting.");
                    Flush();
                    EditorApplication.isPlaying = false;
                    yield break;
                }
                yield return null;
            }
            L($"reached: {what}");
        }

        /// The focus the DRIVER published this frame (what the shader actually cones around),
        /// not a re-derivation — so the bot grades the shipped value.
        static Vector3 Focus()
        {
            var v = Shader.GetGlobalVector("_GolfinOccFadeBall");
            return new Vector3(v.x, v.y, v.z);
        }

        string Snap(string label)
        {
            if (TreeOccludeFadeCaptureBot.RecordMode) { L($"  (record mode — snap {label} skipped)"); return null; }
            string p = null;
            try { p = CaptureCore.SnapPlayModeSafe(label); }
            catch (Exception ex) { L($"  SNAP FAILED {label}: {ex.GetType().Name}: {ex.Message}"); }
            L($"  snap {label} -> {(p ?? "<null>")}");
            Flush();
            return p;
        }

        /// Dump the live globals so the state machine is verifiable from data, not from a human
        /// squinting at a frame (spec's invariant-JSON discipline).
        void DumpGlobals(string tag)
        {
            var ball = Shader.GetGlobalVector("_GolfinOccFadeBall");
            var cam  = Shader.GetGlobalVector("_GolfinOccFadeCam");
            var pars = Shader.GetGlobalVector("_GolfinOccFadeParams");
            float s  = Shader.GetGlobalFloat("_GolfinOccFadeStrength");
            float b  = Shader.GetGlobalFloat("_GolfinOccFadeBias");
            L($"  GLOBALS[{tag}] strength={s:F3} ball=({ball.x:F2},{ball.y:F2},{ball.z:F2}) " +
              $"cam=({cam.x:F2},{cam.y:F2},{cam.z:F2}) params=(cosOuter={pars.x:F4},cosInner={pars.y:F4}," +
              $"cut={pars.z:F2},feather={pars.w:F2}) bias={b:F2} disabled={TreeOccludeFadeDriver.Disabled}");
        }

        /// How many terrain tree instances currently sit inside the fade cone (camera->ball).
        /// This is the objective "is anything actually occluding" number the frames get graded against.
        int TreesInCone(Vector3 camPos, Vector3 focus)
        {
            var terr = Terrain.activeTerrain;
            if (terr == null || terr.terrainData == null) return -1;

            Vector3 toBall = focus - camPos;
            float ballDist = toBall.magnitude;
            if (ballDist < 0.01f) return 0;
            Vector3 dirBall = toBall / ballDist;

            float cosOuter = Mathf.Cos(TreeOccludeFadeDriver.OuterHalfAngleDeg * Mathf.Deg2Rad);
            var td = terr.terrainData;
            Vector3 tp = terr.transform.position;
            int n = 0;
            foreach (var ti in td.treeInstances)
            {
                Vector3 wp = tp + Vector3.Scale(ti.position, td.size);
                Vector3 toF = wp - camPos;
                float d = toF.magnitude;
                if (d < 0.01f) continue;
                // must be nearer than the ball (same gate as the shader) and inside the outer cone
                if (d > ballDist - TreeOccludeFadeDriver.BallDistBiasM) continue;
                if (Vector3.Dot(toF / d, dirBall) < cosOuter) continue;
                n++;
            }
            return n;
        }

        IEnumerator Run()
        {
            // ── 1. Real entry path ────────────────────────────────────────────────
            yield return WaitUntilOrFail(() => FindActiveButtonNamed("StartButton") != null, 60f, "splash PLAY");
            FindActiveButtonNamed("StartButton").onClick.Invoke();

            yield return WaitUntilOrFail(() => FindActiveButtonNamed("PlayButton") != null, 60f, "home PRACTICE PLAY");
            FindActiveButtonNamed("PlayButton").onClick.Invoke();

            Func<Button> holePlay = () =>
                UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(b =>
                    {
                        if (b.name == "PlayButton") return false;
                        var t = b.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                        return t != null && string.Equals(t.text.Trim(), "PLAY", StringComparison.OrdinalIgnoreCase);
                    });
            yield return WaitUntilOrFail(() => holePlay() != null, 90f, "hole-selection PLAY");
            holePlay().onClick.Invoke();

            yield return WaitUntilOrFail(
                () => UnityEngine.Object.FindFirstObjectByType<ChaseCamera>() != null, 180f, "hole tee (ChaseCamera)");

            var chase = UnityEngine.Object.FindFirstObjectByType<ChaseCamera>();
            yield return new WaitForSecondsRealtime(4f);   // let the hole settle + the 0.25s ramp finish

            L($"terrain={(Terrain.activeTerrain != null ? Terrain.activeTerrain.name : "<none>")} " +
              $"trees={(Terrain.activeTerrain != null ? Terrain.activeTerrain.terrainData.treeInstanceCount : 0)}");

            // ── 2. §5.3 / §5.8 — tee frame, kill switch A/B ───────────────────────
            L($"TEE  focus={Focus()}  cam={chase.transform.position}" +
              $"  treesInCone={TreesInCone(chase.transform.position, Focus())}");
            DumpGlobals("tee_on");
            Snap("occfade_tee_on");

            TreeOccludeFadeDriver.Disabled = true;
            yield return new WaitForSecondsRealtime(0.6f);   // let the 0.25 s ramp reach 0
            DumpGlobals("tee_off");
            Snap("occfade_tee_off");

            TreeOccludeFadeDriver.Disabled = false;
            yield return new WaitForSecondsRealtime(0.6f);
            DumpGlobals("tee_back_on");

            // ── 3. Aim into the densest nearby tree cluster, then fire ────────────
            var labType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
            var lab = labType != null ? UnityEngine.Object.FindFirstObjectByType(labType) as MonoBehaviour : null;
            float bestYaw = AimYawTowardTrees(chase.CurrentFocus);
            if (lab != null && !float.IsNaN(bestYaw))
            {
                var m = labType.GetMethod("SetCameraYawRadians", BindingFlags.Public | BindingFlags.Instance);
                if (m != null)
                {
                    try { m.Invoke(lab, new object[] { bestYaw }); L($"aimed yaw={bestYaw * Mathf.Rad2Deg:F1}deg toward tree cluster"); }
                    catch (Exception ex) { L("SetCameraYawRadians warn: " + ex.Message); }
                }
                else L("SetCameraYawRadians not found on PhysicsLabController — firing on the default heading");
            }
            else L($"lab={(lab == null ? "<null>" : lab.name)} bestYaw={bestYaw} — firing on the default heading");

            yield return new WaitForSecondsRealtime(1.5f);

            var shotType = FindType("Golfin.Gameplay.Input.ShotController");
            var shot = shotType != null ? UnityEngine.Object.FindFirstObjectByType(shotType) as MonoBehaviour : null;
            if (shot == null)
            {
                L("ShotController NOT FOUND — cannot play a shot; flight/at-rest captures skipped.");
                Flush();
                yield return new WaitForSecondsRealtime(1f);
                EditorApplication.isPlaying = false;
                yield break;
            }

            // Type.EmptyTypes, not a bare name lookup: BeginExternalDrag is overloaded
            // (control_scheme_seam added BeginExternalDrag(bool ownsTiming)), and a bare
            // lookup throws AmbiguousMatchException. This bot wants the zero-arg flick entry.
            var begin = shotType.GetMethod("BeginExternalDrag", BindingFlags.Public | BindingFlags.Instance,
                                           null, Type.EmptyTypes, null);
            var setP  = shotType.GetMethod("SetExternalPower",  BindingFlags.Public | BindingFlags.Instance);
            var end   = shotType.GetMethod("EndExternalDrag",   BindingFlags.Public | BindingFlags.Instance);

            const float power = 0.62f;   // mid power — lands in the tree line rather than past it
            begin?.Invoke(shot, null);
            float rt = 0f;
            while (rt < 0.85f)
            {
                rt += Time.unscaledDeltaTime;
                setP?.Invoke(shot, new object[] { Mathf.Lerp(0f, power, rt / 0.85f), 0f });
                yield return null;
            }
            setP?.Invoke(shot, new object[] { power, 0f });
            yield return new WaitForSecondsRealtime(0.18f);
            end?.Invoke(shot, new object[] { true });   // bypassFlickGate: guarantee the release fires
            L($"FIRED via production ShotController drag path, power={power:F2}");

            // ── 4. §5.1 / §5.2 / §5.4 — consecutive flight frames ────────────────
            // Consecutive (not sampled) frames are what proves the edge is a gradient and not a cut.
            for (int i = 0; i < 220 && !_froze; i++)
            {
                yield return new WaitForSecondsRealtime(0.08f);
                Vector3 cp = chase.transform.position;
                int inCone = TreesInCone(cp, Focus());
                if (i % 4 == 0) L($"  flight[{i:D2}] focus={Focus()} treesInCone={inCone} " +
                                  $"strength={Shader.GetGlobalFloat("_GolfinOccFadeStrength"):F3}");
                if (inCone > 0 && !_froze)
                {
                    _froze = true;
                    Time.timeScale = 0f;   // freeze the sim but keep rendering + MCP alive (no editor pause)
                    L($"*** FROZE at flight[{i:D2}] treesInCone={inCone} — camera and ball are now static; "
                      + "capture externally with screenshot-game-view ***");
                    L($"    FREEZE cam={cp} focus={Focus()} dist={(Focus() - cp).magnitude:F1}m");
                    DumpGlobals("frozen_occluded");
                }
            }

            if (TreeOccludeFadeCaptureBot.RecordMode)
            {
                // Ball is at rest behind the occluder. Unfreeze (the scene is static now) and let the
                // clip itself BE the A/B: shipped defaults -> kill switch -> wide cone -> defaults.
                Time.timeScale = 1f;
                // Ball is at rest in the tree line. Let it fully settle; no RT reads before StartRecording.
                yield return new WaitForSecondsRealtime(3f);
                L($"VIDEO setup: cam={chase.transform.position} focus={Focus()} dist={(Focus() - chase.transform.position).magnitude:F1}m");

                TreeOccludeFadeCaptureBot.StartClip();
                yield return new WaitForSecondsRealtime(0.5f);

                L("VIDEO t=0.5  PHASE 1 — window ON, shipped defaults (inner 45 / outer 60)");
                DumpGlobals("video_p1_on_default");
                yield return new WaitForSecondsRealtime(4f);

                TreeOccludeFadeDriver.Disabled = true;
                L("VIDEO t=4.5  PHASE 2 — kill switch ON (Disabled=true) => exact pre-change rendering");
                yield return new WaitForSecondsRealtime(1f);
                DumpGlobals("video_p2_killed");
                yield return new WaitForSecondsRealtime(3f);

                TreeOccludeFadeDriver.Disabled = false;
                TreeOccludeFadeDriver.InnerHalfAngleDeg = 10f;
                TreeOccludeFadeDriver.OuterHalfAngleDeg = 16f;
                L("VIDEO t=8.5  PHASE 3 — window ON, OLD narrow cone 10/16 for comparison");
                yield return new WaitForSecondsRealtime(1f);
                DumpGlobals("video_p3_wide");
                yield return new WaitForSecondsRealtime(3f);

                TreeOccludeFadeDriver.InnerHalfAngleDeg = 45f;
                TreeOccludeFadeDriver.OuterHalfAngleDeg = 60f;
                L("VIDEO t=12.5 PHASE 4 — back to shipped defaults (45/60)");
                yield return new WaitForSecondsRealtime(1f);
                DumpGlobals("video_p4_back_to_default");
                yield return new WaitForSecondsRealtime(3f);

                TreeOccludeFadeCaptureBot.StopClip();
                L("VIDEO complete.");
                yield return new WaitForSecondsRealtime(1.5f);
                EditorApplication.isPlaying = false;
                yield break;
            }

            if (_froze)
            {
                L("HOLDING frozen for external capture. Set Time.timeScale=1 to resume.");
                TreeOccludeFadeCaptureBot.ReleaseHold = false;
                while (!TreeOccludeFadeCaptureBot.ReleaseHold) yield return null;
                L("resumed.");
            }

            // ── 5. §5.1 / §5.8 — at-rest occluded A/B ────────────────────────────
            yield return new WaitForSecondsRealtime(3f);
            Vector3 cpos = chase.transform.position;
            int restCone = TreesInCone(cpos, Focus());
            L($"REST focus={Focus()} cam={cpos} treesInCone={restCone}");
            DumpGlobals("rest_on");
            Snap($"occfade_rest_on_cone{restCone}");

            TreeOccludeFadeDriver.Disabled = true;
            yield return new WaitForSecondsRealtime(0.6f);
            DumpGlobals("rest_off");
            Snap($"occfade_rest_off_cone{restCone}");
            TreeOccludeFadeDriver.Disabled = false;
            yield return new WaitForSecondsRealtime(0.6f);

            // ── 6. §5.5 — map view must ramp the window to 0 ─────────────────────
            var mapType = FindType("Golfin.Gameplay.UI.ShotUI.MapViewController");
            var map = mapType != null ? UnityEngine.Object.FindFirstObjectByType(mapType) as MonoBehaviour : null;
            if (map != null)
            {
                var open = mapType.GetMethod("Open", BindingFlags.Public | BindingFlags.Instance)
                        ?? mapType.GetMethod("Toggle", BindingFlags.Public | BindingFlags.Instance);
                if (open != null)
                {
                    bool opened = false;
                    try { open.Invoke(map, null); opened = true; }
                    catch (Exception ex) { L("map open warn: " + ex.Message); }

                    if (opened)
                    {
                        yield return new WaitForSecondsRealtime(1.5f);
                        var isOpen = mapType.GetProperty("IsOpen")?.GetValue(map);
                        L($"MAP opened via {open.Name}() — IsOpen={isOpen}");
                        DumpGlobals("map_open");
                        Snap("occfade_map_open");
                    }
                }
                else L("MapViewController has no public Open()/Toggle() — §5.5 not driven here.");
            }
            else L("MapViewController not found — §5.5 not driven here.");

            L("complete.");
            Flush();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.isPlaying = false;
        }

        /// Pick the aim yaw whose camera->ball corridor will contain the most trees once the ball
        /// has travelled downrange — i.e. aim at the thickest tree line so the shot ends up in it.
        float AimYawTowardTrees(Vector3 ball)
        {
            var terr = Terrain.activeTerrain;
            if (terr == null || terr.terrainData == null) return float.NaN;
            var td = terr.terrainData;
            Vector3 tp = terr.transform.position;

            int bestCount = -1;
            float bestYaw = float.NaN;
            // sample yaw in 5-degree steps over the full circle; score trees 25..90 m out within 12 deg
            for (int deg = 0; deg < 360; deg += 5)
            {
                float yaw = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                int n = 0;
                foreach (var ti in td.treeInstances)
                {
                    Vector3 wp = tp + Vector3.Scale(ti.position, td.size);
                    Vector3 d = wp - ball; d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < 25f || dist > 90f) continue;
                    if (Vector3.Dot(d / dist, dir) < Mathf.Cos(12f * Mathf.Deg2Rad)) continue;
                    n++;
                }
                if (n > bestCount) { bestCount = n; bestYaw = yaw; }
            }
            L($"AimYawTowardTrees: best yaw={bestYaw * Mathf.Rad2Deg:F1}deg with {bestCount} trees in the 25-90m band");
            return bestYaw;
        }
    }
}
#endif
