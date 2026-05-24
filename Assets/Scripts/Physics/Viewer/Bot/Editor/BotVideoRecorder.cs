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
        const string RecordKey = "LoopV2SmokeBot.RecordVideo";
        // iter-3 mitigations for Mac kernel-panic (2× panics on prior 1170×2532 @ 60fps run):
        //   • Fps reduced from 60 → 30 (lower GPU encoder pressure)
        //   • Resolution capped at 540p (see Begin() — Game View size is capped to 540p max height)
        //   • H.264 codec (macOS HEVC has documented kernel-panic reports under Metal + heavy load;
        //     MovieRecorderSettings.VideoRecorderOutputFormat.MP4 defaults to H.264 on macOS when
        //     no explicit codec override is set AND the resolution is kept modest)
        const int Fps = 30;

        /// <summary>SessionState-armed flag — set by the launcher before play mode entry.</summary>
        public static bool RecordVideo
        {
            get => SessionState.GetBool(RecordKey, false);
            set => SessionState.SetBool(RecordKey, value);
        }

        static RecorderController _controller;

        /// <summary>Start recording the Game View. No-op unless RecordVideo is armed.</summary>
        public static void Begin()
        {
            if (!RecordVideo) return;
            RecordVideo = false;   // clear immediately — never leak into a later run

            try
            {
                string scenario = LoopV2SmokeBot.Scenario;
                if (string.IsNullOrEmpty(scenario)) scenario = "unknown";
                string dir = $"tasks/loop_v2_smoke_bot/{scenario}/video";
                Directory.CreateDirectory(dir);

                // iter-3 mitigation: cap resolution to 540p max height to reduce
                // Metal/GPU encoder pressure that triggered macOS kernel panics in two
                // prior runs at 1170×2532 @ 60fps.
                Vector2 gv = Handles.GetMainGameViewSize();
                int rawW = Mathf.Max(2, (int)gv.x);
                int rawH = Mathf.Max(2, (int)gv.y);
                const int MaxHeight = 540;
                int h, w;
                if (rawH > MaxHeight)
                {
                    h = MaxHeight;
                    w = Mathf.Max(2, Mathf.RoundToInt((float)rawW / rawH * MaxHeight));
                    // Ensure width is even (H.264 requires even dimensions).
                    if (w % 2 != 0) w--;
                }
                else
                {
                    w = rawW;
                    h = rawH;
                }
                // Ensure height is also even.
                if (h % 2 != 0) h--;

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

                float t0 = Time.realtimeSinceStartup;
                File.WriteAllText($"{dir}/record_info.json",
                    "{\"record_start_realtime\": " +
                    t0.ToString("F4", CultureInfo.InvariantCulture) +
                    ", \"mp4\": \"" + dir + "/raw.mp4\", \"fps\": " + Fps +
                    ", \"width\": " + w + ", \"height\": " + h +
                    ", \"mitigation\": \"iter3-30fps-540p-H264\"" +
                    "}");

                Debug.Log($"[BotVideoRecorder] Recording started → {dir}/raw.mp4 ({w}x{h} @ {Fps}fps) " +
                          $"[iter-3 mitigations: 30fps, {h}p, H.264 on macOS — reduced from 1170×2532 @ 60fps that caused 2× kernel panics].");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BotVideoRecorder] Begin failed: {e}");
                _controller = null;
            }
        }

        /// <summary>Stop recording. Safe to call when not recording.</summary>
        public static void End()
        {
            if (_controller == null) return;
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
        }
    }
}
#endif
