// map_view_aiming (Order 352) iter-27 — Editor menu launcher for MapViewCaptureDriver.
// Lives in Assets/Scripts/Editor/MapViewCapture/ under Golfin.MapViewCapture.Editor.asmdef.
// Criterion 9: zero edits to Assets/Scripts/Physics/.
//
// iter-27 FIX (gating): DELETED all pre-created-cam scaffolding.
//   - EnsureMapViewCamGO() and its EnteredPlayMode call REMOVED.
//   - _preCreatedCamGO field and ExitingPlayMode destroy REMOVED.
//   - Recorder now uses GameViewInputSettings (captures the full composited Game View,
//     which includes the map cam overlay when it is open and the gameplay scene otherwise).
//   - No camera GO exists before the player/bot opens the map — exact real-player lifecycle.
//
// Root cause of black gameplay (iter-26): "MapViewCam_PreCreated" was created at
// EnteredPlayMode with clearFlags=SolidColor (color=black) and depth=10 OVER the gameplay
// camera. Even with cullingMask=0 a SolidColor camera CLEARS the framebuffer to black
// before any geometry is rendered by lower-depth cams. clearFlags=Depth in iter-26 was
// insufficient — the real fix is NO PRE-CREATED CAM at all.
//
// Recorder: GameViewInputSettings captures the full composited Game View:
//   normal shot view → map open (MapViewCam at depth=10 composites OVER gameplay cam) →
//   aim/drag → SHOOT/close → ball flight. All in ONE continuous clip per SPEC §9.
//
// iter-13 Y-FLIP ROOT LOCK (BotVideoRecorder pattern): lock render state BEFORE
// StartRecording. GameViewInputSettings is the original iter-11 design that correctly
// records both normal play AND the map overlay.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    // [InitializeOnLoad] fires the static ctor after EVERY domain reload — this is what
    // survives the enter-play-mode domain reload that clears all static event subscriptions.
    [InitializeOnLoad]
    public static class MapViewCaptureBotMenu
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const string MenuPath   = "GOLFIN/MapView/Run Real-Input Capture (iter-27)";
        private const int    Fps        = 30;
        private const int    OutputW    = 1170;
        private const int    OutputH    = 2532;
        private const int    MaxSeconds = 90;   // safety cap; full scenario ~55s

        // SessionState keys — survive domain reload; cleared by Begin()
        private const string RecordArmedKey  = "MapViewCapture.RecordVideo";
        private const string OutputPathKey   = "MapViewCapture.VideoOutputPath";
        private const string MaxSecondsKey   = "MapViewCapture.MaxRecordSeconds";

        // ── Recorder state (Editor-side static — reset on domain reload, re-armed from
        //    SessionState on each EnteredPlayMode callback)
        private static RecorderController _controller;
        private static int    _savedTargetFps;
        private static int    _savedVSync;
        private static bool   _renderStateOverridden;
        private static double _recordStartEditorTime;
        private static int    _effectiveMaxSeconds;

        // ── [InitializeOnLoad] static ctor: wire the hook after EVERY domain reload ──
        static MapViewCaptureBotMenu()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // ── Menu item ────────────────────────────────────────────────────────
        [MenuItem(MenuPath)]
        public static void RunRealInputCapture()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MapViewCaptureBotMenu] Already in play mode — exit first.");
                return;
            }
            LaunchProgrammatic(null);
        }

        /// <summary>
        /// Launch programmatically (called by MCP script-execute).
        /// Arms PlayerPrefs for the driver and the SessionState recorder flag,
        /// then enters play mode. The [InitializeOnLoad]-registered playModeStateChanged hook
        /// starts the Recorder at EnteredPlayMode and stops it at ExitingPlayMode.
        /// </summary>
        public static void LaunchProgrammatic(string videoDirOverride = null)
        {
            // Build paths
            string root     = Path.GetDirectoryName(Application.dataPath);
            string videoDir = string.IsNullOrEmpty(videoDirOverride)
                ? Path.GetFullPath(Path.Combine(root, "Docs/Specs/Active/map_view_aiming/videos"))
                : Path.GetFullPath(videoDirOverride);
            string captureDir = Path.GetFullPath(Path.Combine(root,
                "Docs/Specs/Active/map_view_aiming/screenshots/iter30"));

            Directory.CreateDirectory(videoDir);
            Directory.CreateDirectory(captureDir);

            // Output file path (no extension — Recorder appends .mp4)
            // iter-30: tight framing (biased lookAt), validator flag-in-viewport relaxed
            string outputNoExt = Path.Combine(videoDir, "map_view_iter30_raw");

            // Arm the runtime driver via PlayerPrefs (survive domain reload)
            MapViewCaptureDriver.Armed      = true;
            MapViewCaptureDriver.CaptureDir = captureDir;

            // Arm the recorder via SessionState (Editor-side, survive domain reload)
            SessionState.SetBool(RecordArmedKey, true);
            SessionState.SetString(OutputPathKey, outputNoExt);
            SessionState.SetInt(MaxSecondsKey, MaxSeconds);

            Debug.Log($"[MapViewCaptureBotMenu] iter-27 Armed: CaptureDir={captureDir}");
            Debug.Log($"[MapViewCaptureBotMenu] Video output: {outputNoExt}.mp4 @ {OutputW}x{OutputH} {Fps}fps, max={MaxSeconds}s");
            Debug.Log($"[MapViewCaptureBotMenu] RecordArmedKey SET — iter-27: NO pre-created cam, GameViewInputSettings, real-player lifecycle");

            // Open ShellScene if needed
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Equals("ShellScene", StringComparison.OrdinalIgnoreCase))
            {
                string[] guids = AssetDatabase.FindAssets("ShellScene t:Scene");
                foreach (var guid in guids)
                {
                    string sp = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(sp)
                            .Equals("ShellScene", StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sp,
                            UnityEditor.SceneManagement.OpenSceneMode.Single);
                        Debug.Log($"[MapViewCaptureBotMenu] Opened {sp}");
                        break;
                    }
                }
            }

            // B1 fix (iter-8): Select iPhone-14 Game View size BEFORE EnterPlaymode so the
            // Game View is already the correct resolution when the domain reload fires.
            // This prevents a Game View resize mid-recording that would cause a Y-flip on Metal.
            TrySelectIPhone14GameViewSize();

            EditorApplication.EnterPlaymode();
        }

        // ── Play-mode hook: [InitializeOnLoad] ensures this is re-registered after every
        //    domain reload including the one triggered by entering play mode. ─────────────
        // iter-27: EnsureMapViewCamGO() REMOVED — no pre-created camera at all.
        // The map camera is created ONLY when the player opens the map (in MapViewController.Open/
        // BuildRuntimeObjects) and destroyed when it closes (CloseImmediate/DestroyRuntimeObjects).
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    // iter-27: NO EnsureMapViewCamGO() call — real-player lifecycle.
                    StartRecordingIfArmed();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    StopRecording();
                    break;
            }
        }

        // ── Start recording ───────────────────────────────────────────────────
        // iter-27: Uses GameViewInputSettings (NOT CameraInputSettings TaggedCamera).
        // GameViewInputSettings captures the FULL composited Game View:
        //   normal shot view (gameplay cam only) → map open (MapViewCam at depth=10
        //   composites OVER gameplay cam, visible in Game View) → SHOOT/close →
        //   ball flight. All in ONE continuous clip per SPEC §9.
        //
        // Why GameViewInputSettings (not CameraInputSettings):
        //   Without a pre-created cam, CameraInputSettings TaggedCamera "MapViewCam"
        //   would fail at StartRecording() time because no camera with that tag exists
        //   yet — the map camera only lives during map-open. GameViewInputSettings reads
        //   the composited Game View output, so it correctly captures BOTH the normal play
        //   view AND the map overlay (when open) without needing the cam to pre-exist.
        //
        // Key Y-flip prevention: lock render state BEFORE StartRecording (BotVideoRecorder pattern).
        private static void StartRecordingIfArmed()
        {
            if (!SessionState.GetBool(RecordArmedKey, false))
            {
                Debug.Log("[MapViewCaptureBotMenu] EnteredPlayMode: RecordArmedKey=false — not recording.");
                return;
            }

            // Consume the arm flag immediately
            SessionState.SetBool(RecordArmedKey, false);

            string outputNoExt = SessionState.GetString(OutputPathKey, "");
            if (string.IsNullOrEmpty(outputNoExt))
            {
                Debug.LogError("[MapViewCaptureBotMenu] No output path in SessionState — cannot record.");
                return;
            }
            SessionState.SetString(OutputPathKey, "");

            _effectiveMaxSeconds = SessionState.GetInt(MaxSecondsKey, MaxSeconds);
            if (_effectiveMaxSeconds <= 0) _effectiveMaxSeconds = MaxSeconds;
            SessionState.SetInt(MaxSecondsKey, 0);

            Debug.Log($"[MapViewCaptureBotMenu] EnteredPlayMode iter-27: starting GameViewInputSettings recording → {outputNoExt}.mp4 (no pre-created cam)");

            // Ensure output directory exists
            string dir = Path.GetDirectoryName(outputNoExt);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // iter-13 Y-FLIP ROOT LOCK (BotVideoRecorder pattern, commit b369c91f2 + iter-13 hardening):
            // Lock ALL render state BEFORE StartRecording. A mid-record render-state change on Metal
            // causes Unity Recorder's GameView capture to read one frame inverted.
            // We lock here (at EnteredPlayMode, before any gameplay runs) so nothing can change
            // vSync/targetFrameRate while the recorder is active.
            _savedTargetFps = Application.targetFrameRate;
            _savedVSync     = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount   = 0;
            Application.targetFrameRate  = Fps;
            Application.runInBackground  = true;  // iter-13: prevent focus-change render-state disruption
            _renderStateOverridden       = true;
            Debug.Log($"[MapViewCaptureBotMenu] iter-27 render state locked: vSync=0, targetFrameRate={Fps}, runInBackground=true (BEFORE StartRecording — Y-flip prevention)");

            try
            {
                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name         = "MapViewCapture";
                movie.Enabled      = true;
#pragma warning disable CS0618  // OutputFormat is obsolete but still works — same as BotVideoRecorder
                movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
#pragma warning restore CS0618
                // iter-27: GameViewInputSettings — captures the FULL composited Game View.
                // Records both normal gameplay AND the map overlay (MapViewCam depth=10 composites
                // into the Game View when the map is open). No TaggedCamera dependency needed.
                var gvInput = new GameViewInputSettings();
                gvInput.OutputWidth  = OutputW;
                gvInput.OutputHeight = OutputH;
                movie.ImageInputSettings = gvInput;
                movie.AudioInputSettings.PreserveAudio = false;
                movie.OutputFile = outputNoExt;   // Recorder appends .mp4

                var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                settings.AddRecorderSettings(movie);
                settings.SetRecordModeToManual();
                settings.FrameRate          = Fps;
                settings.FrameRatePlayback  = FrameRatePlayback.Variable;  // real-time

                _controller = new RecorderController(settings);
                _controller.PrepareRecording();
                _controller.StartRecording();

                // Arm the duration watchdog
                _recordStartEditorTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += DurationWatchdog;

                Debug.Log($"[MapViewCaptureBotMenu] iter-27 Recording STARTED → {outputNoExt}.mp4 ({OutputW}x{OutputH} @ {Fps}fps, GameViewInputSettings, NO pre-created cam)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapViewCaptureBotMenu] StartRecording failed: {e}");
                RestoreRenderState();
                _controller = null;
            }
        }

        // ── Stop recording ────────────────────────────────────────────────────
        private static void StopRecording()
        {
            EditorApplication.update -= DurationWatchdog;

            if (_controller != null)
            {
                try
                {
                    if (_controller.IsRecording())
                        _controller.StopRecording();
                    Debug.Log("[MapViewCaptureBotMenu] Recording STOPPED.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MapViewCaptureBotMenu] StopRecording: {e.Message}");
                }
                _controller = null;
            }

            RestoreRenderState();
        }

        private static void RestoreRenderState()
        {
            if (_renderStateOverridden)
            {
                Application.targetFrameRate  = _savedTargetFps;
                QualitySettings.vSyncCount   = _savedVSync;
                Application.runInBackground  = false;
                _renderStateOverridden       = false;
                Debug.Log("[MapViewCaptureBotMenu] Render state restored (vSync, targetFrameRate, runInBackground).");
            }
        }

        // ── Duration watchdog (prevents runaway GPU load) ─────────────────────
        private static void DurationWatchdog()
        {
            if (_controller == null)
            {
                EditorApplication.update -= DurationWatchdog;
                return;
            }
            if (EditorApplication.timeSinceStartup - _recordStartEditorTime < _effectiveMaxSeconds)
                return;

            Debug.LogWarning($"[MapViewCaptureBotMenu] Max clip duration ({_effectiveMaxSeconds}s) reached — force-stopping.");
            StopRecording();
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        // ── iPhone-14 Game View size selection (reflection, no Physics dependency) ──
        // Mirrors GameViewSizeUtil.EnsureIPhone14Selected() — duplicated to avoid Physics dep.
        private static void TrySelectIPhone14GameViewSize()
        {
            try
            {
                var edAsm          = typeof(UnityEditor.Editor).Assembly;
                var gameSizesT     = edAsm.GetType("UnityEditor.GameViewSizes");
                var gameSizeT      = edAsm.GetType("UnityEditor.GameViewSize");
                var gameSizeGroupT = edAsm.GetType("UnityEditor.GameViewSizeGroup");
                if (gameSizesT == null || gameSizeT == null || gameSizeGroupT == null)
                {
                    Debug.LogWarning("[MapViewCaptureBotMenu] GameViewSizes types not found — skipping iPhone-14 preset.");
                    return;
                }

                var instance     = gameSizesT.BaseType.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var currentGroup = gameSizesT.GetProperty("currentGroup",
                    BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
                var getSizeCount = gameSizeGroupT.GetMethod("GetTotalCount",
                    BindingFlags.Public | BindingFlags.Instance);
                var getSizeAt    = gameSizeGroupT.GetMethod("GetGameViewSize",
                    BindingFlags.Public | BindingFlags.Instance);

                if (getSizeCount == null || getSizeAt == null) return;

                int count = (int)getSizeCount.Invoke(currentGroup, null);
                var widthProp  = gameSizeT.GetProperty("width",  BindingFlags.Public | BindingFlags.Instance);
                var heightProp = gameSizeT.GetProperty("height", BindingFlags.Public | BindingFlags.Instance);
                var idxProp    = gameSizesT.GetProperty("currentSizeIndex", BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < count; i++)
                {
                    var sz = getSizeAt.Invoke(currentGroup, new object[] { i });
                    if (widthProp == null || heightProp == null) continue;
                    int w = (int)widthProp.GetValue(sz);
                    int h = (int)heightProp.GetValue(sz);
                    if (w == OutputW && h == OutputH)
                    {
                        if (idxProp != null) idxProp.SetValue(instance, i);
                        Debug.Log($"[MapViewCaptureBotMenu] iPhone-14 {OutputW}x{OutputH} selected at index {i}.");
                        return;
                    }
                }
                Debug.LogWarning($"[MapViewCaptureBotMenu] iPhone-14 {OutputW}x{OutputH} not found in Game View sizes; using current.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MapViewCaptureBotMenu] TrySelectIPhone14GameViewSize: {e.Message}");
            }
        }
    }
}
#endif
