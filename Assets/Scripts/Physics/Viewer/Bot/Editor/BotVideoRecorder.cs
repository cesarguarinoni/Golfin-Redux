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
        const int    Fps       = 60;

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

                Vector2 gv = Handles.GetMainGameViewSize();
                int w = Mathf.Max(2, (int)gv.x);
                int h = Mathf.Max(2, (int)gv.y);

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
                    ", \"mp4\": \"" + dir + "/raw.mp4\", \"fps\": " + Fps + "}");

                Debug.Log($"[BotVideoRecorder] Recording started → {dir}/raw.mp4 ({w}x{h} @ {Fps}fps).");
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
