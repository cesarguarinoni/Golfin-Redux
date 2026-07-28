#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Editor menu launcher for ZoneBakeAfterClipBot — §6 "after" gameplay clips.
    ///
    /// Two clips:
    ///   1. H15 Fairway — real tee shot lands in the fairway ring (z &lt; 55.4 = green boundary).
    ///                     zones.json Fairway poly now present → IsPutt=False, non-putter HUD.
    ///                     (The inverted case; matters most.)
    ///   2. H14 Green   — tee + approach shots until ball reaches the green.
    ///                     zones.json Green poly now present → IsPutt=True, putter auto-engages.
    ///
    /// CRITICAL capture mode: UseCameraInput=false (GameView).
    ///   CameraInputSettings (camera-source) drops the URP Overlay HUD — the club widget that
    ///   shows "Putter" vs "Driver" would be INVISIBLE in the recording.  GameView capture
    ///   preserves the full HUD.  (reference_gameplay_capture_gameview_not_camera_urp.md)
    ///
    /// Y-flip prevention: Arm() → Begin() at EnteredPlayMode (ShellScene already open and stable).
    ///   Prior ArmDeferred() pattern (Begin() mid-coroutine at t≈26s) produced 0-byte MP4s on
    ///   Mac/Metal: the Metal compositor stops updating the CAMetalLayer backbuffer once Unity is
    ///   backgrounded, so GameViewInputSettings captures no frames.  Starting at EnteredPlayMode
    ///   (while Metal is still in foreground transition mode) produces a valid clip.
    ///   Y-flip risk is low because ShellScene is pre-opened before play mode entry, so no
    ///   significant render-settings change occurs at that moment.
    ///   (reference_botvideorecorder_yflip_fix.md / feedback_record_bot_video_full_size.md)
    ///
    /// Session guard: one recording per Editor launch.  Between H15 and H14 clips:
    ///   GOLFIN > Capture > Reset Video Session Guard
    ///
    /// Exactly ONE call to BotVideoRecorder.End() per session — from LoopV2SmokeBotMenu
    /// (fires unconditionally for ALL bots). Do NOT call End() here.
    ///
    /// Output: Docs/Specs/Active/zone_bake_completeness/videos/
    /// </summary>
    [InitializeOnLoad]
    public static class ZoneBakeAfterClipMenu
    {
        const string ScenePath  = "Assets/Scenes/ShellScene.unity";
        const string OutputBase = "Docs/Specs/Active/zone_bake_completeness/videos";

        static ZoneBakeAfterClipMenu()
        {
            // Ensure we subscribe exactly once per domain-reload.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // ── Menu items ──────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Zone Bake/1 Record H15 Fairway (AFTER)")]
        public static void RecordH15()
            => Launch("h15_fairway", "h15_after_raw", maxSeconds: 55,
                "H15 fairway-ring shot — IsPutt=False expected (zones.json Fairway now present)");

        [MenuItem("GOLFIN/Zone Bake/2 Record H14 Green Putt (AFTER)")]
        public static void RecordH14()
            => Launch("h14_green", "h14_after_raw", maxSeconds: 260,
                "H14 green approach — IsPutt=True expected (zones.json Green now present)");

        // ── Play-mode state machine ─────────────────────────────────────────────

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    EnteredPlayMode();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ExitingPlayMode();
                    break;
            }
        }

        static void EnteredPlayMode()
        {
            if (!ZoneBakeAfterClipBot.Armed) return;

            // runInBackground: recording must not pause when Unity window loses focus.
            Application.runInBackground = true;

            Debug.Log("[ZoneBakeAfterClipMenu] EnteredPlayMode — creating ZoneBakeAfterClipBot GO");

            var go = new GameObject("[ZoneBakeAfterClipBot]");
            go.AddComponent<ZoneBakeAfterClipBot>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            // Start GameView recording NOW (at play mode entry, while Metal compositor is still
            // active for the Game View window). Starting recording mid-coroutine (ArmDeferred) on
            // Mac/Metal causes 0-byte output because the CAMetalLayer backbuffer stops updating
            // once Unity is backgrounded. Arm() set RecordVideo=true in Launch(); Begin() consumes
            // it here. ZoneBakeAfterClipBot.StartRecording() still calls Begin() but the session
            // guard blocks the redundant call — it's benign.
            BotVideoRecorder.Begin();
        }

        static void ExitingPlayMode()
        {
            // Restore DisableSceneReload we set in Launch().
            // BotVideoRecorder.End() is intentionally NOT called here.
            // LoopV2SmokeBotMenu.ExitingPlayMode() calls it unconditionally for all bots.
            bool wasDisabled = SessionState.GetBool("ZoneBakeAfterClip.DisableSceneReload", false);
            if (wasDisabled)
            {
                EditorSettings.enterPlayModeOptions =
                    EditorSettings.enterPlayModeOptions & ~EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool("ZoneBakeAfterClip.DisableSceneReload", false);
                Debug.Log("[ZoneBakeAfterClipMenu] ExitingPlayMode — restored DisableSceneReload");
            }
        }

        // ── Launch ──────────────────────────────────────────────────────────────

        static void Launch(string scenario, string outputFileName, int maxSeconds, string description)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ZoneBakeAfterClipMenu] Cannot launch while in play mode.");
                return;
            }

            Debug.Log($"[ZoneBakeAfterClipMenu] Launching scenario '{scenario}': {description}");
            Debug.Log($"[ZoneBakeAfterClipMenu] Output: {OutputBase}/{outputFileName}.mp4");

            // Configure BotVideoRecorder for this clip.
            BotVideoRecorder.MaxRecordSecondsSessionOverride = maxSeconds;
            // Pass path WITHOUT extension — BotVideoRecorder.Begin() appends ".mp4".
            BotVideoRecorder.CustomOutputPath = $"{OutputBase}/{outputFileName}";

            // CRITICAL: GameView mode (UseCameraInput=false) — required to capture the
            // URP Overlay HUD (club widget showing Putter/Driver).
            // CameraInputSettings (UseCameraInput=true) renders the game camera's RT
            // directly and bypasses the URP Overlay canvas → HUD invisible in recording.
            // (Contrast: ObBoundaryCaptureMenu uses UseCameraInput=true for ChaseCam OB
            //  clips where HUD is not the evidence; here the HUD IS the evidence.)
            BotVideoRecorder.UseCameraInput = false;
            // CameraTag is irrelevant when UseCameraInput=false; clear defensively.
            BotVideoRecorder.CameraTag = string.Empty;

            // Arm: sets RecordVideo=true immediately. Begin() is called at EnteredPlayMode so
            // Mac/Metal's CAMetalLayer backbuffer is still active and GameView captures frames.
            // ArmDeferred() caused 0-byte MP4s: Metal stops updating the backbuffer once Unity
            // is backgrounded, so late Begin() (mid-coroutine at t≈26s) captured nothing.
            BotVideoRecorder.Arm();

            // Arm the bot.
            ZoneBakeAfterClipBot.Armed    = true;
            ZoneBakeAfterClipBot.Scenario = scenario;

            // DisableDomainReload: prevents ShellScene being reloaded on domain reload
            // while the scene is open in play mode, preserving static fields.
            bool alreadyDisabled =
                EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload);
            if (!alreadyDisabled)
            {
                EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool("ZoneBakeAfterClip.DisableSceneReload", true);
                Debug.Log("[ZoneBakeAfterClipMenu] DisableDomainReload set for this session");
            }

            // Open ShellScene (Single) — bot navigates from the Title screen.
            var scene = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ScenePath);
            if (scene == null)
            {
                Debug.LogError(
                    $"[ZoneBakeAfterClipMenu] ShellScene not found at '{ScenePath}'. Aborting.");
                ZoneBakeAfterClipBot.Armed = false;
                return;
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
