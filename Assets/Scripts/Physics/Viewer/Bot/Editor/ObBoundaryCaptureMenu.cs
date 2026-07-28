#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Editor menu launcher for ObBoundaryCaptureBot — three OB boundary capture clips.
    ///
    /// Matches LoopV2SmokeBotMenu pattern exactly:
    ///   • [DidReloadScripts]: register playModeStateChanged once per domain-reload.
    ///   • EnteredPlayMode: if ObBoundaryCaptureBot.Armed, create GO + set runInBackground.
    ///   • ExitingPlayMode: restore DisableSceneReload only.
    ///     BotVideoRecorder.End() is called unconditionally by LoopV2SmokeBotMenu.ExitingPlayMode
    ///     (it fires for ALL bots — we must NOT duplicate that call).
    ///
    /// Uses BotVideoRecorder.ArmDeferred() so Begin() at EnteredPlayMode is a no-op
    /// (DeferredRecord=true, RecordVideo=false). ObBoundaryCaptureBot triggers Begin()
    /// mid-coroutine via reflection after the hole is fully loaded — avoiding Y-flip on Mac/Metal.
    ///
    /// BotVideoRecorder.SessionGuard: one recording per Editor launch. Between clips, run
    ///   GOLFIN > Capture > Reset Video Session Guard
    /// to permit a second recording in the same Editor session.
    ///
    /// Output path: Docs/Specs/Active/ob_boundary_presentation/videos/
    /// </summary>
    [InitializeOnLoad]
    public static class ObBoundaryCaptureMenu
    {
        const string ScenePath   = "Assets/Scenes/ShellScene.unity";
        const string OutputBase  = "Docs/Specs/Active/ob_boundary_presentation/videos";
        const int    MaxSeconds  = 35;

        static ObBoundaryCaptureMenu()
        {
            // Runs on every domain-reload — ensure we subscribe exactly once.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // ── Menu items ─────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/OB Boundary/1 Record BEFORE (git stash active)")]
        public static void RecordBefore()
            => Launch("ob_before", "before_ob_void.mp4",
                "BEFORE — no ObGroundSkirt, no ChaseCamera clamp: bare void visible");

        [MenuItem("GOLFIN/OB Boundary/2 Record AFTER (skirt + clamp live)")]
        public static void RecordAfter()
            => Launch("ob_after", "after_camera_clamp.mp4",
                "AFTER — ObGroundSkirt + SetChaseClamp: green ground plane, camera holds boundary");

        [MenuItem("GOLFIN/OB Boundary/3 Record CONTROL (inbounds normal shot)")]
        public static void RecordControl()
            => Launch("ob_control", "control_normal_chase.mp4",
                "CONTROL — power=0.15 inbounds: normal chase camera, no clamp fires");

        // ── Play mode state machine ────────────────────────────────────────────

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
            if (!ObBoundaryCaptureBot.Armed) return;

            // Enable runInBackground so recording doesn't pause when Unity window loses focus.
            Application.runInBackground = true;

            // BotVideoRecorder.Begin() was already called in Launch() via ArmDeferred().
            // Because DeferredRecord=true at that point it was a no-op — correct.
            // The bot itself triggers Begin() mid-coroutine after the hole loads.
            Debug.Log("[ObBoundaryCaptureMenu] EnteredPlayMode — creating ObBoundaryCaptureBot GO");

            var go = new GameObject("[ObBoundaryCaptureBot]");
            go.AddComponent<ObBoundaryCaptureBot>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        static void ExitingPlayMode()
        {
            // Restore DisableSceneReload (we set it in Launch() to prevent ShellScene being
            // reloaded on domain reload while the scene is playing).
            // BotVideoRecorder.End() is intentionally NOT called here; LoopV2SmokeBotMenu
            // calls it unconditionally and covers all bots including this one.
            bool wasDisabled = SessionState.GetBool("ObBoundaryCapture.DisableSceneReload", false);
            if (wasDisabled)
            {
                EditorSettings.enterPlayModeOptions =
                    EditorSettings.enterPlayModeOptions & ~EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool("ObBoundaryCapture.DisableSceneReload", false);
                Debug.Log("[ObBoundaryCaptureMenu] ExitingPlayMode — restored DisableSceneReload");
            }
        }

        // ── Launch ─────────────────────────────────────────────────────────────

        static void Launch(string scenario, string outputFileName, string description)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ObBoundaryCaptureMenu] Cannot launch while already in play mode.");
                return;
            }

            Debug.Log($"[ObBoundaryCaptureMenu] Launching scenario '{scenario}': {description}");
            Debug.Log($"[ObBoundaryCaptureMenu] Output: {OutputBase}/{outputFileName}");

            // Configure BotVideoRecorder for this clip.
            BotVideoRecorder.MaxRecordSecondsSessionOverride = MaxSeconds;
            // BotVideoRecorder.Begin() appends ".mp4" itself — pass the path WITHOUT extension.
            BotVideoRecorder.CustomOutputPath =
                $"{OutputBase}/{System.IO.Path.GetFileNameWithoutExtension(outputFileName)}";

            // Pipeline_Hardening Rule 4: use CameraInputSettings+TaggedCamera to avoid the
            // Mac/Metal systematic full-video Y-flip produced by GameViewInputSettings when
            // recording begins during active Hole 6 gameplay. The existing Y-FLIP FIX in
            // BotVideoRecorder only guards against single-frame transients (mid-record RT
            // recreation); it cannot fix a systematic flip baked in at record-start.
            // CameraInputSettings reads from the camera's own RT (correctly oriented).
            //
            // ITER-9 FIX: use "ChaseCam" (unique) not "MainCamera" (ambiguous).
            // ShellScene + LabScaffold both carry a "MainCamera"-tagged camera, triggering
            // "More than one camera has the requested target tag 'MainCamera'" warnings and
            // a Y-flipped video.  ObBoundaryCaptureBot step 6b re-tags the ChaseCamera GO
            // to "ChaseCam" before calling Begin() — unique tag → no multi-camera ambiguity
            // → CameraInputSettings injects its own RT → no Metal Y-flip.
            // "ChaseCam" is registered in ProjectSettings/TagManager.asset.
            BotVideoRecorder.UseCameraInput = true;
            BotVideoRecorder.CameraTag = "ChaseCam";

            // ArmDeferred: RecordVideo=false, DeferredRecord=true.
            // This makes Begin() at EnteredPlayMode a no-op.
            // The bot triggers Begin() after the hole is stable to avoid Y-flip.
            BotVideoRecorder.ArmDeferred();

            // Arm the bot with the scenario key.
            ObBoundaryCaptureBot.Armed    = true;
            ObBoundaryCaptureBot.Scenario = scenario;

            // Guard: disable domain-reload on play-mode entry so existing scene state is
            // preserved across the ShellScene single-load (mirrors LoopV2SmokeBotMenu).
            bool alreadyDisabled =
                EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload);
            if (!alreadyDisabled)
            {
                EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool("ObBoundaryCapture.DisableSceneReload", true);
                Debug.Log("[ObBoundaryCaptureMenu] DisableDomainReload set for this session");
            }

            // Open ShellScene (Single) — the bot navigates from there.
            var scene = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ScenePath);
            if (scene == null)
            {
                Debug.LogError(
                    $"[ObBoundaryCaptureMenu] ShellScene not found at '{ScenePath}'. Aborting.");
                ObBoundaryCaptureBot.Armed = false;
                return;
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
