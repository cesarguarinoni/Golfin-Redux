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
    ///   requires full-size video. Lowering the resolution is NOT the lever (it re-breaks the layout).
    ///
    ///   GPU-saturation guardrails (added 2026-06-09 after a full-res batch wedged the Apple GPU →
    ///   WindowServer userspace_watchdog_timeout → forced reboot; Editor.log + the .ips named Unity +
    ///   AGX workloops). Full-res stays; instead we cap the LOAD so it can't saturate the GPU:
    ///     1. ONE recording per Editor launch (SessionState guard). A 2nd Begin() in the same session is
    ///        REFUSED with a relaunch instruction — relaunching Unity releases the GPU/encoder state.
    ///        The cumulative many-clips-per-session batch is what wedged WindowServer.
    ///     2. Runaway backstop (MaxRecordSeconds, ~30s) — an EditorApplication.update watchdog force-stops
    ///        + exits play mode if a clip runs absurdly long (a hung bot recording forever = its own GPU
    ///        risk). It sits ABOVE the longest legit clip (~19s nav) so it never truncates real footage.
    ///     3. Frame-rate cap during record (Application.targetFrameRate = Fps, vSync off) so the Game
    ///        View doesn't render uncapped — a large GPU-load cut with ZERO change to the recorded look.
    ///   Orchestrator discipline (not enforceable here): only RE-record clips that actually changed; the
    ///   in-chat image-surfacing rule lets Cesar catch issues before a full re-record round.
    ///   If a full-res record STILL wedges the Mac with these guards on, STOP and surface it — do NOT
    ///   silently re-introduce a smaller fabricated resolution.
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

        // Guardrail (2026-06-09): one full-res recording per Editor launch. SessionState
        // survives domain reloads but resets on Editor relaunch — exactly the lifetime we
        // want, since relaunching Unity is what releases the wedged GPU/encoder state.
        const string SessionGuardKey = "LoopV2SmokeBot.RecordedThisEditorSession";

        // Runaway backstop. A clip longer than this is force-stopped — it catches a hung
        // bot that records indefinitely (itself a GPU-saturation risk), NOT normal clips.
        // Set above the longest legit clip (the menu→opponent-turn nav is ~19s) so it never
        // truncates real footage; lower it only if a specific clip family is shorter.
        const int MaxRecordSeconds = 30;

        // Saved render-load settings restored in End().
        static double _recordStartEditorTime;
        static int _savedTargetFps;
        static int _savedVSync;
        static bool _loadOverridden;

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

            // Guardrail 1 — one full-res recording per Editor launch. The 2026-06-09 reboot
            // was a cumulative-load WindowServer watchdog timeout from many full-res clips in
            // one session. Refuse a 2nd recording until Unity is relaunched (releases the GPU
            // encoder). Checked BEFORE clearing RecordVideo so the refusal is explicit.
            if (SessionState.GetBool(SessionGuardKey, false))
            {
                RecordVideo = false;
                Debug.LogError(
                    "[BotVideoRecorder] BLOCKED: a full-res video was already recorded this Unity " +
                    "session. Recording another in the same session risks wedging the Apple GPU / " +
                    "WindowServer watchdog (the 2026-06-09 reboot). RELAUNCH Unity before recording the " +
                    "next clip — that releases the GPU/encoder state. To override after confirming the " +
                    "GPU is idle: GOLFIN > Capture > Reset Video Session Guard.");
                return;
            }

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

                // Guardrail 1 — mark this Editor session as having recorded (survives domain
                // reloads, resets on relaunch). Set only AFTER a successful start so a failed
                // attempt doesn't lock the session out.
                SessionState.SetBool(SessionGuardKey, true);

                // Guardrail 3 — cap the render loop to the capture fps during record. Uncapped
                // Game-View rendering is the dominant GPU draw; capping it cuts load hard with
                // zero change to the recorded look. Restored in End().
                _savedTargetFps = Application.targetFrameRate;
                _savedVSync     = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount   = 0;     // vSync would clamp targetFrameRate to display Hz
                Application.targetFrameRate  = Fps;
                _loadOverridden = true;

                // Guardrail 2 — arm the duration watchdog.
                _recordStartEditorTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += DurationWatchdog;

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
            EditorApplication.update -= DurationWatchdog;   // idempotent

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

            // Guardrail 3 — restore the render-load settings we overrode in Begin().
            if (_loadOverridden)
            {
                Application.targetFrameRate = _savedTargetFps;
                QualitySettings.vSyncCount  = _savedVSync;
                _loadOverridden = false;
            }

            // Defensive: if the Recorder created a "Recording Resolution" entry this run, drop it so it
            // never lingers in the Game View dropdown (and never poisons a later capture). The iPhone-14
            // preset stays selected.
            GameViewSizeUtil.PurgeFabricatedEntries();
        }

        /// <summary>Guardrail 2 — force-stop a clip that exceeds MaxRecordSeconds so the
        /// real-time encoder can't saturate the GPU over a long run (the 2026-06-09
        /// WindowServer-watchdog reboot). Exits play mode after stopping.</summary>
        static void DurationWatchdog()
        {
            if (_controller == null)
            {
                EditorApplication.update -= DurationWatchdog;
                return;
            }
            if (EditorApplication.timeSinceStartup - _recordStartEditorTime < MaxRecordSeconds)
                return;
            Debug.LogWarning(
                $"[BotVideoRecorder] Max clip duration ({MaxRecordSeconds}s) reached — force-stopping " +
                "and exiting play mode to avoid GPU saturation. If the clip needs to be longer, split " +
                "the scenario or shorten it; do NOT raise MaxRecordSeconds without re-checking the " +
                "WindowServer-watchdog risk.");
            End();
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }

        /// <summary>Manual override for the one-recording-per-session guard. Use only after
        /// confirming the GPU is idle (relaunching Unity is the preferred reset).</summary>
        [MenuItem("GOLFIN/Capture/Reset Video Session Guard")]
        public static void ResetSessionGuard()
        {
            SessionState.SetBool(SessionGuardKey, false);
            Debug.Log("[BotVideoRecorder] Video session guard cleared — one more full-res recording " +
                      "allowed this session. Prefer relaunching Unity between clips when possible.");
        }
    }
}
#endif
