// Order: beta_telemetry — the MonoBehaviour half: flush clock, FPS sampling, pause/quit.
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Telemetry
{
    /// <summary>
    /// Everything <see cref="TelemetryService"/> deliberately does not do because it is a
    /// plain C# object: own a clock, watch frames, and hear about pause/quit.
    ///
    /// Self-bootstrapping DontDestroyOnLoad host, the same shape as <c>NetCoroutineRunner</c>
    /// and <c>AuthService</c>, so this needs no scene wiring and no prefab.
    ///
    /// FPS sampling is two floats plus a one-second bucket — no list, no allocation per
    /// frame (SPEC §3 rule 5). It only runs while a round is active.
    /// </summary>
    public sealed class TelemetryBehaviour : MonoBehaviour
    {
        private static TelemetryBehaviour _instance;

        public static TelemetryBehaviour Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Golfin.Telemetry]");
                    _instance = go.AddComponent<TelemetryBehaviour>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── FPS accumulators (round-scoped) ───────────────────────────────────────
        private int   _frames;
        private float _elapsed;
        private int   _bucketFrames;
        private float _bucketElapsed;
        private float _worstBucketFps;

        /// <summary>Start (or restart) FPS accumulation for a round.</summary>
        public void ResetFpsSampling()
        {
            _frames = _bucketFrames = 0;
            _elapsed = _bucketElapsed = 0f;
            _worstBucketFps = float.MaxValue;
        }

        /// <summary>Average FPS over the round so far; 0 before any frame was sampled.</summary>
        public float AverageFps => _elapsed > 0f ? _frames / _elapsed : 0f;

        /// <summary>Worst completed one-second bucket; falls back to the average when the
        /// round was shorter than a second.</summary>
        public float LowFps => _worstBucketFps < float.MaxValue ? _worstBucketFps : AverageFps;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            ResetFpsSampling();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (TelemetryService.Instance.RoundActive)
            {
                _frames++;
                _elapsed += dt;

                _bucketFrames++;
                _bucketElapsed += dt;
                if (_bucketElapsed >= 1f)
                {
                    float fps = _bucketFrames / _bucketElapsed;
                    if (fps < _worstBucketFps) _worstBucketFps = fps;
                    _bucketFrames = 0;
                    _bucketElapsed = 0f;
                }
            }

            TelemetryService.Instance.Tick(dt);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            EndSession();
        }

        private void OnApplicationQuit() => EndSession();

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// <c>session_end</c> + a final flush. Best-effort by nature: on iOS the app can be
        /// killed before the request lands, which is exactly why the event is recorded on
        /// PAUSE too and not only on quit.
        /// </summary>
        private void EndSession()
        {
            TelemetryService.Instance.RecordSafe(TelemetryEventNames.SessionEnd, () =>
                new Dictionary<string, object>
                {
                    ["duration_s"] = System.Math.Round(Time.realtimeSinceStartup, 1),
                });

            TelemetryService.Instance.Flush();
        }
    }
}
