#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Optional companion to LoopV2SmokeBot: dumps Game View frames during a bot run so
    /// the run can be assembled into a captioned demo video by an external ffmpeg script
    /// (Docs/Scripts/build_bot_video.py).
    ///
    /// This is the LEAN replacement for the removed BotVideoRecorder (BOT_FRAMEWORK §8).
    /// It ONLY dumps PNG frames via the sanctioned CaptureCore.SnapPlayModeSafe path —
    /// no MediaEncoder, no in-game caption canvas. All encoding and captioning happens
    /// post-hoc in ffmpeg, so the video pipeline is data-driven and reusable: captions
    /// are derived from the bot's history.log timestamps.
    ///
    /// Lifecycle: injected by LoopV2SmokeBotMenu at EnteredPlayMode, but ONLY when the
    /// RecordVideo flag is armed (SessionState). Like LoopV2SmokeBot.Armed, the flag
    /// clears itself on Start so it never carries into an unrelated later run.
    ///
    /// Output:
    ///   - frames  → Docs/Diagnostics/_capture/botframe_NNNNN_*.png
    ///   - manifest → tasks/loop_v2_smoke_bot/&lt;scenario&gt;/video/frames_manifest.csv
    ///     (per-frame Time.realtimeSinceStartup — the SAME clock BotDriver.LogStep uses,
    ///      so captions sync exactly).
    ///
    /// Never placed in a scene manually and never saved to disk — same zero-contamination
    /// contract as the bot host.
    /// </summary>
    public class BotFrameRecorder : MonoBehaviour
    {
        const string RecordKey  = "LoopV2SmokeBot.RecordVideo";
        const float  StartDelay = 0.30f;   // let the first rendered frame settle
        const int    MaxFrames  = 6000;    // safety cap

        /// <summary>SessionState-armed flag — set by the launcher before play mode entry.</summary>
        public static bool RecordVideo
        {
            get => UnityEditor.SessionState.GetBool(RecordKey, false);
            set => UnityEditor.SessionState.SetBool(RecordKey, value);
        }

        readonly List<float> _frameTimes = new List<float>();
        bool _running;
        bool _manifestWritten;

        void Start()
        {
            if (!RecordVideo)
            {
                Destroy(gameObject);
                return;
            }
            // Clear immediately so the flag never leaks into a later non-recorded run.
            RecordVideo = false;

            ClearOldFrames();
            _running = true;
            StartCoroutine(CaptureLoop());
            Debug.Log("[BotFrameRecorder] Armed — dumping Game View frames via CaptureCore.");
        }

        IEnumerator CaptureLoop()
        {
            yield return new WaitForSecondsRealtime(StartDelay);
            int frame = 0;
            int fails = 0;
            while (_running && frame < MaxFrames)
            {
                // CaptureScreenshotAsTexture (inside SnapPlayModeSafe) only produces a
                // valid texture at end-of-frame — yield WaitForEndOfFrame first, or the
                // texture comes back null and no PNG is written.
                yield return new WaitForEndOfFrame();

                float t = Time.realtimeSinceStartup;
                string path = null;
                try { path = CaptureCore.SnapPlayModeSafe($"botframe_{frame:D5}"); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BotFrameRecorder] frame {frame}: {e.Message}");
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    // Only count + timestamp frames that actually produced a file, so the
                    // manifest stays 1:1 with the PNGs on disk.
                    _frameTimes.Add(t);
                    frame++;
                    fails = 0;
                }
                else
                {
                    fails++;
                    if (fails == 1 || fails % 30 == 0)
                        Debug.LogWarning($"[BotFrameRecorder] capture produced no file (fail #{fails})");
                    yield return new WaitForSecondsRealtime(0.02f);
                }
            }
        }

        // Play mode exiting fires OnApplicationQuit; OnDisable is the belt-and-suspenders.
        void OnApplicationQuit() => WriteManifest();
        void OnDisable()         => WriteManifest();

        void WriteManifest()
        {
            if (_manifestWritten) return;
            _manifestWritten = true;
            _running = false;

            string scenario = LoopV2SmokeBot.Scenario;
            if (string.IsNullOrEmpty(scenario)) scenario = "unknown";
            string dir = $"tasks/loop_v2_smoke_bot/{scenario}/video";

            try
            {
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("frame,realtime");
                for (int i = 0; i < _frameTimes.Count; i++)
                    sb.AppendLine($"{i},{_frameTimes[i].ToString("F4", CultureInfo.InvariantCulture)}");

                string path = $"{dir}/frames_manifest.csv";
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[BotFrameRecorder] Wrote {_frameTimes.Count} frame timestamps → {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BotFrameRecorder] WriteManifest failed: {e.Message}");
            }
        }

        /// <summary>Delete stale botframe_*.png so a new run never mixes with an old one.</summary>
        static void ClearOldFrames()
        {
            try
            {
                string capDir = CaptureCore.OutDir;
                if (Directory.Exists(capDir))
                    foreach (var f in Directory.GetFiles(capDir, "botframe_*.png"))
                        File.Delete(f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BotFrameRecorder] ClearOldFrames: {e.Message}");
            }
        }
    }
}
#endif
