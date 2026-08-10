#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// K10 ob_recovery_fixes — launcher for ObRecoveryCaptureBot (one boundary-OB recovery clip).
    /// Mirrors ObBoundaryCaptureMenu exactly: deferred, Y-flip-safe recording via the ChaseCam
    /// TaggedCamera path; the bot triggers Begin() mid-coroutine after the hole is stable.
    /// BotVideoRecorder.End() is called unconditionally by LoopV2SmokeBotMenu.ExitingPlayMode.
    ///
    /// Between clips, run GOLFIN > Capture > Reset Video Session Guard to allow a second recording.
    /// Output: Docs/Specs/Completed/ob_recovery_fixes/videos/ob_recovery_after.mp4
    /// </summary>
    [InitializeOnLoad]
    public static class ObRecoveryCaptureMenu
    {
        const string ScenePath  = "Assets/Scenes/ShellScene.unity";
        const string OutputBase = "Docs/Specs/Completed/ob_recovery_fixes/videos";
        const int    MaxSeconds = 30;

        static ObRecoveryCaptureMenu()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("GOLFIN/OB Recovery/Record boundary-OB recovery (Hole 6)")]
        public static void RecordRecovery()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ObRecoveryCaptureMenu] Cannot launch while already in play mode.");
                return;
            }

            Debug.Log($"[ObRecoveryCaptureMenu] Launching. Output: {OutputBase}/ob_recovery_after.mp4");

            BotVideoRecorder.MaxRecordSecondsSessionOverride = MaxSeconds;
            BotVideoRecorder.CustomOutputPath = $"{OutputBase}/ob_recovery_after_hud";
            // GameView source → captures the full Overlay HUD (TURN counter, aim cone, ball,
            // buttons) so the re-tee + forward-aim + drag are all legible. The 2026-06-16
            // Y-FLIP FIX (lock render-pipeline state BEFORE StartRecording) makes GameView
            // upright on Mac/Metal — the ChaseCam TaggedCamera workaround is no longer needed
            // for orientation and it drops the HUD, which we want for this recovery clip.
            BotVideoRecorder.UseCameraInput   = false;
            BotVideoRecorder.ArmDeferred();

            ObRecoveryCaptureBot.Armed = true;

            bool alreadyDisabled =
                EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload);
            if (!alreadyDisabled)
            {
                EditorSettings.enterPlayModeOptions |= EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool("ObRecoveryCapture.DisableSceneReload", true);
            }

            var scene = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ScenePath);
            if (scene == null)
            {
                Debug.LogError($"[ObRecoveryCaptureMenu] ShellScene not found at '{ScenePath}'. Aborting.");
                ObRecoveryCaptureBot.Armed = false;
                return;
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (!ObRecoveryCaptureBot.Armed) return;
                    Application.runInBackground = true;
                    var go = new GameObject("[ObRecoveryCaptureBot]");
                    go.AddComponent<ObRecoveryCaptureBot>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    Debug.Log("[ObRecoveryCaptureMenu] EnteredPlayMode — bot GO created");
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    bool wasDisabled = SessionState.GetBool("ObRecoveryCapture.DisableSceneReload", false);
                    if (wasDisabled)
                    {
                        EditorSettings.enterPlayModeOptions =
                            EditorSettings.enterPlayModeOptions & ~EnterPlayModeOptions.DisableDomainReload;
                        SessionState.SetBool("ObRecoveryCapture.DisableSceneReload", false);
                    }
                    break;
            }
        }
    }
}
#endif
