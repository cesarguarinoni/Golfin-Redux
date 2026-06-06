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
    /// capture pipeline records at 30 fps without the per-frame encode stall that
    /// capped the dump approach at ~8 fps.
    ///
    /// Driven entirely from the editor by LoopV2SmokeBotMenu's playModeStateChanged hook:
    /// Begin() at EnteredPlayMode, End() at ExitingPlayMode. Active only when the
    /// RecordVideo flag is armed (SessionState; cleared on Begin so it never leaks).
    ///
    /// Game-View size handling (the important part):
    ///   Unity Recorder's GameViewInput resizes the Game View to its output resolution by creating /
    ///   selecting a custom "Recording Resolution" GameViewSize entry. Letting it do that at any size
    ///   other than the device size mis-lays-out the UI Canvas (the bottom nav bar / menu break) and
    ///   the Recorder then captures the broken frame. So BEFORE recording we explicitly select the real
    ///   iPhone-14 1170×2532 device preset via <see cref="GameViewSizeUtil.EnsureIPhone14Selected"/>
    ///   and record at exactly that size — the UI lays out identically to normal play, and because the
    ///   requested output size already equals the current render size the Recorder's resize is a no-op
    ///   (no fabricated entry is created). End() purges any fabricated entry defensively.
    ///
    ///   We record at FULL 1170×2532. An earlier build capped this to 540p as a macOS "kernel-panic
    ///   mitigation"; that cap is removed — it was the source of the broken-layout captures and Cesar
    ///   requires full-size video. If a full-res record genuinely kernel-panics the Mac, STOP and
    ///   surface it — do NOT silently re-introduce a smaller fabricated resolution.
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

        // 30 fps (not 60): real-time recording at 1170×2532 keeps the encoder load modest.
        const int Fps = 30;

        /// <summary>SessionState-armed flag — set by the launcher before play mode entry.</summary>
        public static bool RecordVideo
        {
            get => SessionState.GetBool(RecordKey, false);
            set => SessionState.SetBool(RecordKey, value);
        }

        /// <summary>Arm a recording. Call before entering play mode.</summary>
        public static void Arm() => RecordVideo = true;

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

                // Select the REAL iPhone-14 1170×2532 device preset (and purge any fabricated
                // "Recording Resolution" entry) so the menu lays out exactly as in normal play.
                bool selected = GameViewSizeUtil.EnsureIPhone14Selected();

                // Record at full device resolution. Use the live render size when the select worked
                // (it now equals 1170×2532); fall back to the constants otherwise.
                int w, h;
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (selected && cw == GameViewSizeUtil.IPhone14Width && ch == GameViewSizeUtil.IPhone14Height)
                {
                    w = GameViewSizeUtil.IPhone14Width;
                    h = GameViewSizeUtil.IPhone14Height;
                }
                else
                {
                    Debug.LogWarning($"[BotVideoRecorder] Could not confirm the iPhone-14 1170×2532 Game View size " +
                                     $"(selected={selected}, render={cw}x{ch}). Recording at the current render size; " +
                                     $"the UI may not match normal play.");
                    w = Mathf.Max(2, (int)cw);
                    h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--;   // H.264 requires even dimensions
                    if (h % 2 != 0) h--;
                }

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
                    ", \"size\": \"iphone14-1170x2532-full\"" +
                    "}");

                Debug.Log($"[BotVideoRecorder] Recording started → {dir}/raw.mp4 ({w}x{h} @ {Fps}fps). " +
                          "Game View pinned to the iPhone-14 1170×2532 device preset — UI lays out as in normal play.");
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
            if (_controller != null)
            {
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

            // Defensive: if the Recorder created a "Recording Resolution" entry this run, drop it so it
            // never lingers in the Game View dropdown (and never poisons a later capture). The iPhone-14
            // preset stays selected.
            GameViewSizeUtil.PurgeFabricatedEntries();
        }
    }
}
#endif
