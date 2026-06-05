#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Drives the Unity Recorder (com.unity.recorder) to capture a smooth, full-framerate
    /// MP4 of a smoke-bot run. Replaces the old PNG-dump recorder — the Recorder's async
    /// capture pipeline records at 30-60 fps without the per-frame encode stall that
    /// capped the dump approach at ~8 fps.
    ///
    /// Driven entirely from the editor by LoopV2SmokeBotMenu's playModeStateChanged hook:
    /// Begin() at EnteredPlayMode, End() at ExitingPlayMode. Active only when the
    /// RecordVideo flag is armed (SessionState; cleared on Begin so it never leaks).
    ///
    /// Capture profiles (see <see cref="CaptureProfile"/>):
    ///   • MenuNative     — records at the Game View's native render resolution (e.g. the
    ///                      iPhone 14 1170×2532 custom size). The output size equals the
    ///                      current Game View size, so the Recorder's internal
    ///                      GameViewSize.SetCustomSize is a no-op (it early-returns when the
    ///                      requested size already matches) — the Game View is NOT resized and
    ///                      menu/UI layouts keep their exact pixels. This is the default.
    ///   • GameplayCapped — caps recorded height at 540p to reduce Metal/GPU encoder pressure
    ///                      that caused two macOS kernel panics on prior 1170×2532 @ 60fps
    ///                      gameplay runs. This DOES resize the Game View while recording; the
    ///                      view is restored to its original resolution in End() so a capped
    ///                      gameplay run never leaves the view stuck at 540p (which would
    ///                      otherwise poison a subsequent MenuNative capture into recording —
    ///                      and breaking — the UI at 540p).
    ///
    /// Output (project-root-relative):
    ///   tasks/loop_v2_smoke_bot/&lt;scenario&gt;/video/raw.mp4
    ///   tasks/loop_v2_smoke_bot/&lt;scenario&gt;/video/record_info.json
    ///     — the bot-clock Time.realtimeSinceStartup at record start, so
    ///       Docs/Scripts/build_bot_video.py can sync ffmpeg captions to history.log.
    ///
    /// Recorded in real-time mode (CapFrameRate off) so video time == the bot's
    /// real-time clock — caption sync is then a single offset subtraction.
    /// </summary>
    public static class BotVideoRecorder
    {
        const string RecordKey  = "LoopV2SmokeBot.RecordVideo";
        const string ProfileKey = "LoopV2SmokeBot.RecordProfile";

        // Fps reduced from 60 → 30 (lower GPU encoder pressure) after 2× macOS kernel panics
        // on a prior 1170×2532 @ 60fps run. H.264 (MP4 default on macOS) is kept for the same reason.
        const int Fps = 30;

        // GameplayCapped profile only: cap recorded height to keep the Metal/GPU encoder load modest.
        const int GameplayMaxHeight = 540;

        /// <summary>How the recorder chooses its output resolution. See class doc for the trade-off.</summary>
        public enum CaptureProfile
        {
            /// <summary>
            /// Record at the Game View's native render resolution (no resize). Use for menu / UI
            /// captures — keeps the iPhone 14 (or whatever) custom view exactly as authored.
            /// </summary>
            MenuNative = 0,

            /// <summary>
            /// Cap recorded height at 540p (kernel-panic mitigation). Resizes the Game View while
            /// recording, then restores it in End(). Use for 3D gameplay captures.
            /// </summary>
            GameplayCapped = 1,
        }

        /// <summary>SessionState-armed flag — set by the launcher before play mode entry.</summary>
        public static bool RecordVideo
        {
            get => SessionState.GetBool(RecordKey, false);
            set => SessionState.SetBool(RecordKey, value);
        }

        /// <summary>
        /// SessionState-armed capture profile. Defaults to <see cref="CaptureProfile.MenuNative"/> so
        /// any recording that doesn't explicitly opt into the gameplay cap keeps the Game View intact.
        /// </summary>
        public static CaptureProfile Profile
        {
            get => (CaptureProfile)SessionState.GetInt(ProfileKey, (int)CaptureProfile.MenuNative);
            set => SessionState.SetInt(ProfileKey, (int)value);
        }

        /// <summary>Arm a recording with an explicit profile. Call before entering play mode.</summary>
        public static void Arm(CaptureProfile profile)
        {
            RecordVideo = true;
            Profile     = profile;
        }

        static RecorderController _controller;

        // Snapshot of the Game View render resolution taken before recording, so the GameplayCapped
        // profile (or an odd-size even-clamp) can restore it in End() instead of leaving the view resized.
        static bool _restoreResolution;
        static uint _savedWidth;
        static uint _savedHeight;

        /// <summary>Start recording the Game View. No-op unless RecordVideo is armed.</summary>
        public static void Begin()
        {
            if (!RecordVideo) return;
            RecordVideo = false;                  // clear immediately — never leak into a later run
            CaptureProfile profile = Profile;
            Profile = CaptureProfile.MenuNative;  // reset to the safe default so the profile never leaks

            _restoreResolution = false;

            try
            {
                string scenario = LoopV2SmokeBot.Scenario;
                if (string.IsNullOrEmpty(scenario)) scenario = "unknown";
                string dir = $"tasks/loop_v2_smoke_bot/{scenario}/video";
                Directory.CreateDirectory(dir);

                // Snapshot the live Game View render resolution BEFORE the Recorder touches it.
                PlayModeWindow.GetRenderingResolution(out _savedWidth, out _savedHeight);
                int rawW = Mathf.Max(2, (int)_savedWidth);
                int rawH = Mathf.Max(2, (int)_savedHeight);

                int w, h;
                if (profile == CaptureProfile.GameplayCapped && rawH > GameplayMaxHeight)
                {
                    // Downscale proportionally to the 540p height cap (kernel-panic mitigation).
                    h = GameplayMaxHeight;
                    w = Mathf.Max(2, Mathf.RoundToInt((float)rawW / rawH * GameplayMaxHeight));
                    _restoreResolution = true;   // we are about to resize the view; undo it in End()
                }
                else
                {
                    // MenuNative (or already ≤ cap): record at the native size. Output == current
                    // render size, so the Recorder's SetCustomSize is a no-op → no resize → UI intact.
                    w = rawW;
                    h = rawH;
                }
                // H.264 requires even dimensions. Clamping an odd native size by 1px would itself
                // resize the view, so flag a restore when that happens too.
                if (w % 2 != 0) { w--; _restoreResolution = true; }
                if (h % 2 != 0) { h--; _restoreResolution = true; }

                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name         = "BotVideo";
                movie.Enabled      = true;
                movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
                movie.ImageInputSettings = new GameViewInputSettings
                {
                    OutputWidth  = w,
                    OutputHeight = h,
                };
                movie.AudioInputSettings.PreserveAudio = false;
                movie.OutputFile = $"{dir}/raw";   // Recorder appends the .mp4 extension

                var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                settings.AddRecorderSettings(movie);
                settings.SetRecordModeToManual();
                settings.FrameRate = Fps;
                // Variable playback = real-time recording: video time == the bot's
                // real-time clock, so history.log captions sync with one offset.
                // (Constant playback drives Time.captureFramerate and stretches the
                // video to game-time, which desyncs realtime-stamped captions.)
                settings.FrameRatePlayback = FrameRatePlayback.Variable;

                _controller = new RecorderController(settings);
                _controller.PrepareRecording();
                _controller.StartRecording();

                string mitigation = profile == CaptureProfile.GameplayCapped
                    ? $"gameplay-capped-{Fps}fps-{h}p-H264"
                    : $"menu-native-{Fps}fps-{w}x{h}-H264";

                float t0 = Time.realtimeSinceStartup;
                File.WriteAllText($"{dir}/record_info.json",
                    "{\"record_start_realtime\": " +
                    t0.ToString("F4", CultureInfo.InvariantCulture) +
                    ", \"mp4\": \"" + dir + "/raw.mp4\", \"fps\": " + Fps +
                    ", \"width\": " + w + ", \"height\": " + h +
                    ", \"profile\": \"" + profile + "\"" +
                    ", \"mitigation\": \"" + mitigation + "\"" +
                    "}");

                if (profile == CaptureProfile.MenuNative)
                {
                    Debug.Log($"[BotVideoRecorder] Recording started → {dir}/raw.mp4 ({w}x{h} @ {Fps}fps, profile=MenuNative). " +
                              "Native Game View resolution — no resize, menu/UI layout preserved.");
                }
                else
                {
                    Debug.Log($"[BotVideoRecorder] Recording started → {dir}/raw.mp4 ({w}x{h} @ {Fps}fps, profile=GameplayCapped). " +
                              $"Capped to {h}p for macOS kernel-panic safety; Game View will be restored to {_savedWidth}x{_savedHeight} on stop.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BotVideoRecorder] Begin failed: {e}");
                _controller = null;
                RestoreResolutionIfNeeded();
            }
        }

        /// <summary>Stop recording. Safe to call when not recording.</summary>
        public static void End()
        {
            if (_controller == null)
            {
                // A Begin() that armed a restore but never spun up a controller still needs cleanup.
                RestoreResolutionIfNeeded();
                return;
            }
            try
            {
                if (_controller.IsRecording())
                    _controller.StopRecording();
                Debug.Log("[BotVideoRecorder] Recording stopped.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BotVideoRecorder] End: {e.Message}");
            }
            _controller = null;
            RestoreResolutionIfNeeded();
        }

        /// <summary>
        /// Put the Game View back to the resolution it had before recording. Best-effort: the
        /// GameplayCapped profile resizes the view to 540p and the Recorder never restores it, so
        /// without this a capped run would leave the editor — and any later menu capture — at 540p.
        /// </summary>
        static void RestoreResolutionIfNeeded()
        {
            if (!_restoreResolution) return;
            _restoreResolution = false;
            try
            {
                PlayModeWindow.SetCustomRenderingResolution(_savedWidth, _savedHeight, "Restored (BotVideoRecorder)");
                Debug.Log($"[BotVideoRecorder] Restored Game View resolution to {_savedWidth}x{_savedHeight}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BotVideoRecorder] Resolution restore failed: {e.Message}");
            }
        }
    }
}
#endif
