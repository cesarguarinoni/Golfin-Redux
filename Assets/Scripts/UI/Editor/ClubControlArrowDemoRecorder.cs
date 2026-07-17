#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Reusable "felt gate" rig for Order 732 (club_control_arrow_range_calibration).
    ///
    /// Records a single continuous MP4 of the REAL shot-timing arrow (ShotConeView's
    /// TimingSlabGraphic) oscillating at two ClubControl (CC) values, so the retuned
    /// ArrowSpeedHzPerCC (-0.05) contrast is visible:
    ///   CC=25 (Common cap)  → arrowHz = 3.0 - 25*0.05 = 1.75 Hz (~0.57 s / cycle)
    ///   CC=50 (Supreme cap) → arrowHz = 3.0 - 50*0.05 = 0.50 Hz (~2.00 s / cycle)
    /// The CC=50 arrow sweeps ~3.5× slower than CC=25.
    ///
    /// Drives the REAL Timing loop (NOT FireDebugShot, which bypasses TickArrow):
    ///   PhysicsLabController.SetClub(Driver) + InjectLabBundleForCurrentClub()
    ///   → ShotController.InjectStatBundle(CC bundle)
    ///   → BeginExternalDrag() → SetExternalPower(power>0) → Timing
    ///   → HOLD (Update() ticks TickArrow each frame; the slab oscillates) → CancelExternalDrag().
    ///
    /// Modeled on StaminaLiveMeterDemoRecorder (its own RecorderController — NOT
    /// BotVideoRecorder's one-per-session GPU guard) + PutterConeSmokeCapture (the
    /// LabScaffold boot/find pattern). Records at full iPhone-14 1170×2532 @ 30fps.
    ///
    /// ShotController lives in the Golfin.Gameplay.Input asmdef, which is
    /// autoReferenced:false — so this editor assembly (Assembly-CSharp-Editor) cannot
    /// compile-time reference it. It is driven via reflection instead. All the Stats
    /// types (StatBundle/CharacterStats/ClubStats/BallStats/fp) and PhysicsLabController
    /// ARE autoReferenced, so those are used directly.
    ///
    /// NOTE (MaxTotalPasses=10): TickArrow auto-returns to Idle after 10 full passes.
    /// At CC=25 (1.75 Hz) that is ~5.7 s, so each phase holds < that and the ffmpeg cut
    /// window (written to cut_points.json) stays inside the in-Timing window.
    ///
    /// Usage: GOLFIN > Physics > Record ClubControl Arrow Range Demo (or LaunchDemo()).
    /// Output: Docs/Specs/Completed/club_control_arrow_range_calibration/videos/raw_cc_arrow.mp4
    ///         + cut_points.json (record-start-relative phase offsets for the ffmpeg split).
    /// </summary>
    public static class ClubControlArrowDemoRecorder
    {
        const string LabScenePath = "Assets/Scenes/Physics/LabScaffold.unity";
        const string OutputDir    = "Docs/Specs/Completed/club_control_arrow_range_calibration/videos";
        const string RawStem      = "raw_cc_arrow";
        const string ArmedKey     = "ClubControlArrowDemo.Armed";

        static RecorderController _recorder;

        /// <summary>Time.realtimeSinceStartup captured at StartRecording — the runner
        /// subtracts this to write record-start-relative cut offsets.</summary>
        public static float RecordStartRealtime;

        public static string RawPathNoExt => $"{OutputDir}/{RawStem}";
        public static string CutJsonPath  => $"{OutputDir}/cut_points.json";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Physics/Record ClubControl Arrow Range Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CCArrowDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(LabScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[CCArrowDemo] Armed. Entering play mode — recording will start automatically.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                // Required for reliable play-mode capture (frames render even unfocused).
                Application.runInBackground = true;

                var host = new GameObject("[ClubControlArrowDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<ClubControlArrowDemoRunner>().Begin();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopClip();   // safety net if the coroutine aborted early
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.BotEditor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        public static void StartClip(string fileNoExt)
        {
            // Pin the real device size BEFORE StartRecording (Y-flip fix — lock render
            // state so nothing recreates the Game View RT mid-record).
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--;
                    if (h % 2 != 0) h--;
                    Debug.LogWarning($"[CCArrowDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            QualitySettings.vSyncCount  = 0;    // vSync would clamp targetFrameRate to display Hz
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "CCArrowDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = fileNoExt;   // Recorder appends .mp4

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;   // real-time: video time == wall clock

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            RecordStartRealtime = Time.realtimeSinceStartup;
            Debug.Log($"[CCArrowDemo] Recording started → {fileNoExt}.mp4 ({w}x{h} @ 30fps)");
        }

        public static void StopClip()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording())
                    _recorder.StopRecording();
                Debug.Log($"[CCArrowDemo] Recording stopped → {RawPathNoExt}.mp4");
            }
            catch (Exception e) { Debug.LogWarning($"[CCArrowDemo] StopClip: {e.Message}"); }
            _recorder = null;
        }

        public static void WriteCutJson(float cc25Start, float cc25End, float cc50Start, float cc50End)
        {
            string j =
                "{\n" +
                $"  \"record_stem\": \"{RawStem}\",\n" +
                $"  \"cc25_start\": {cc25Start.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                $"  \"cc25_end\": {cc25End.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                $"  \"cc25_hz\": 1.75,\n" +
                $"  \"cc50_start\": {cc50Start.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                $"  \"cc50_end\": {cc50End.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                $"  \"cc50_hz\": 0.50\n" +
                "}\n";
            File.WriteAllText(CutJsonPath, j);
            Debug.Log($"[CCArrowDemo] Cut points → {CutJsonPath}\n{j}");
        }
    }

    /// <summary>
    /// Runtime bot that drives the two-phase CC arrow capture as a coroutine.
    /// Lives in this editor file (mirrors StaminaMeterDemoRunner) but is AddComponent'd
    /// at play time. Drives the REAL ShotController Timing loop via reflection.
    /// </summary>
    public class ClubControlArrowDemoRunner : MonoBehaviour
    {
        // Reflection handles for the ShotController (asmdef not auto-referenced here).
        object      _sc;
        MethodInfo  _injectStatBundle;
        MethodInfo  _beginExternalDrag;
        MethodInfo  _setExternalPower;
        MethodInfo  _cancelExternalDrag;

        // Per-phase hold seconds. CC=25 held < 10-pass timeout (~5.7 s @ 1.75 Hz).
        const float BootDelay      = 3.5f;
        const float ClubSettle     = 0.5f;
        const float RecorderSettle = 0.3f;
        const float Cc25Hold       = 5.0f;   // ~8.75 passes (< 10) → stays in Timing
        const float PhaseGap       = 0.5f;
        const float Cc50Hold       = 6.0f;   // ~3.0 passes @ 0.5 Hz
        const float TailDelay      = 0.3f;

        public void Begin() => StartCoroutine(Sequence());

        bool ResolveShotController()
        {
            var scType = Type.GetType("Golfin.Gameplay.Input.ShotController, Golfin.Gameplay.Input");
            if (scType == null)
            {
                Debug.LogError("[CCArrowDemo] Could not resolve type Golfin.Gameplay.Input.ShotController.");
                return false;
            }
            _sc = UnityEngine.Object.FindObjectOfType(scType);
            if (_sc == null)
            {
                Debug.LogError("[CCArrowDemo] No ShotController found in the scene (is LabScaffold loaded?).");
                return false;
            }
            const BindingFlags F = BindingFlags.Public | BindingFlags.Instance;
            _injectStatBundle   = scType.GetMethod("InjectStatBundle",   F, null, new[] { typeof(StatBundle) }, null);
            _beginExternalDrag  = scType.GetMethod("BeginExternalDrag",  F, null, Type.EmptyTypes, null);
            _setExternalPower   = scType.GetMethod("SetExternalPower",   F, null, new[] { typeof(float), typeof(float) }, null);
            _cancelExternalDrag = scType.GetMethod("CancelExternalDrag", F, null, Type.EmptyTypes, null);

            if (_injectStatBundle == null || _beginExternalDrag == null ||
                _setExternalPower == null || _cancelExternalDrag == null)
            {
                Debug.LogError("[CCArrowDemo] Missing a ShotController method via reflection: " +
                    $"InjectStatBundle={_injectStatBundle != null} BeginExternalDrag={_beginExternalDrag != null} " +
                    $"SetExternalPower={_setExternalPower != null} CancelExternalDrag={_cancelExternalDrag != null}");
                return false;
            }
            return true;
        }

        void InjectCc(int cc)
        {
            var bundle = new StatBundle(
                ClubStats.DefaultDriver,
                BallStats.Neutral,
                new CharacterStats(0, cc, 0, 0),
                fp.FromFloat(100f), fp.FromFloat(100f));
            _injectStatBundle.Invoke(_sc, new object[] { bundle });
        }

        void EnterTimingHold()
        {
            _beginExternalDrag.Invoke(_sc, null);                       // Idle → Aiming
            _setExternalPower.Invoke(_sc, new object[] { 0.5f, 0f });   // Aiming → Timing (power > 0)
        }

        void ExitTiming() => _cancelExternalDrag.Invoke(_sc, null);     // → Idle (arrow reset)

        IEnumerator Sequence()
        {
            // ── Boot: LabScaffold spins up the shot UI (no title/PLAY gate) ──────
            yield return new WaitForSecondsRealtime(BootDelay);

            var lab = FindObjectOfType<PhysicsLabController>();
            if (lab == null)
            {
                Debug.LogError("[CCArrowDemo] PhysicsLabController not found — abort.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            if (!ResolveShotController())
            {
                EditorApplication.ExitPlaymode();
                yield break;
            }

            // Driver mode (non-putt) so the target arrowHz table applies directly.
            lab.SetClub(0);
            lab.InjectLabBundleForCurrentClub();
            yield return new WaitForSecondsRealtime(ClubSettle);

            // ── Start the continuous recording ──────────────────────────────────
            ClubControlArrowDemoRecorder.StartClip(ClubControlArrowDemoRecorder.RawPathNoExt);
            yield return new WaitForSecondsRealtime(RecorderSettle);
            float t0 = ClubControlArrowDemoRecorder.RecordStartRealtime;

            // ── Phase 1: CC=25 (Common cap) → 1.75 Hz, fast sweep ───────────────
            InjectCc(25);
            EnterTimingHold();
            float cc25Start = Time.realtimeSinceStartup - t0;
            Debug.Log($"[CCArrowDemo] Phase CC=25 begin @ +{cc25Start:F2}s (expect 1.75 Hz).");
            yield return new WaitForSecondsRealtime(Cc25Hold);
            float cc25End = Time.realtimeSinceStartup - t0;
            ExitTiming();
            yield return new WaitForSecondsRealtime(PhaseGap);

            // ── Phase 2: CC=50 (Supreme cap) → 0.50 Hz, slow sweep ──────────────
            InjectCc(50);
            EnterTimingHold();
            float cc50Start = Time.realtimeSinceStartup - t0;
            Debug.Log($"[CCArrowDemo] Phase CC=50 begin @ +{cc50Start:F2}s (expect 0.50 Hz).");
            yield return new WaitForSecondsRealtime(Cc50Hold);
            float cc50End = Time.realtimeSinceStartup - t0;
            ExitTiming();
            yield return new WaitForSecondsRealtime(TailDelay);

            // ── Finish: write cut points, stop, exit ────────────────────────────
            ClubControlArrowDemoRecorder.WriteCutJson(cc25Start, cc25End, cc50Start, cc50End);
            ClubControlArrowDemoRecorder.StopClip();
            yield return new WaitForSecondsRealtime(TailDelay);

            Debug.Log("[CCArrowDemo] ===== DONE ===== exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
