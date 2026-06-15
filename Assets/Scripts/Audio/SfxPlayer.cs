using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Audio.Events;

namespace Golfin.Audio
{
    /// <summary>
    /// Listens to SfxBus.OnPlay and resolves SfxId → AudioClip → AudioManager.PlaySFX.
    ///
    /// Lifecycle rules:
    ///   - Attached to the same GameObject as AudioManager (persists via DDOL).
    ///   - Subscribe in OnEnable, unsubscribe in OnDisable — guards against double-subscribe.
    ///   - Clip object refs are wired via the inspector (SfxLibrary ScriptableObject) so
    ///     clips are not loaded via Resources.Load (avoids build bloat).
    ///   - Per-event tunable data (baseVolume, velocityGateMin, etc.) comes from sfx.csv
    ///     loaded at Start() from Resources/Data/sfx.csv.
    /// </summary>
    [RequireComponent(typeof(AudioManager))]
    public class SfxPlayer : MonoBehaviour, ISfxGates
    {
        [Header("Clip Library")]
        [Tooltip("ScriptableObject holding the SfxId → AudioClip mappings.")]
        [SerializeField] private SfxLibrary _library;

        // ── Parsed CSV data ────────────────────────────────────────────────────

        private struct SfxData
        {
            public float baseVolume;
            public float velocityGateMin;  // minimum velocity magnitude for landing SFX
            public float playRateCap;      // suppress if BallAnimator.PlayRate > this
            public float minIntervalSec;   // min seconds between repeated landing SFX
        }

        private Dictionary<SfxId, SfxData> _data = new Dictionary<SfxId, SfxData>();

        // ── Subscription guard ─────────────────────────────────────────────────

        private bool _subscribed;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Start()
        {
            LoadCsvData();
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                SfxBus.OnPlay += HandlePlay;
                _subscribed = true;
            }
            // Register gate provider so BallAudioEmitter (Golfin.Physics.Viewer) can
            // query per-event thresholds without a direct cross-assembly reference.
            SfxBus.Gates = this;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                SfxBus.OnPlay -= HandlePlay;
                _subscribed = false;
            }
            // Clear gate provider so dangling references don't survive scene reloads.
            if (SfxBus.Gates == (ISfxGates)this)
                SfxBus.Gates = null;
        }

        // ── Bus handler ────────────────────────────────────────────────────────

        private void HandlePlay(SfxId id)
        {
            if (_library == null)
            {
                Debug.LogWarning($"[SfxPlayer] SfxLibrary not assigned — cannot play {id}.");
                return;
            }

            AudioClip clip = _library.GetClip(id);
            if (clip == null)
            {
                Debug.LogWarning($"[SfxPlayer] No clip mapped for SfxId={id}.");
                return;
            }

            float vol = GetBaseVolume(id);
            AudioManager.Instance?.PlaySFX(clip, vol);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private float GetBaseVolume(SfxId id)
        {
            if (_data.TryGetValue(id, out var d)) return d.baseVolume;
            return 1f;
        }

        /// <summary>
        /// Used by BallAudioEmitter to check per-event tunable data before publishing.
        /// Returns true if a landing SFX should be suppressed based on velocity magnitude.
        /// </summary>
        public bool ShouldSuppressLanding(SfxId id, float velocityMagnitude)
        {
            if (!_data.TryGetValue(id, out var d)) return false;
            return velocityMagnitude < d.velocityGateMin;
        }

        /// <summary>
        /// Returns the playRateCap for an SfxId. BallAudioEmitter uses this to suppress
        /// per-bounce sounds at very high PlayRate (Instant mode).
        /// </summary>
        public float GetPlayRateCap(SfxId id)
        {
            if (_data.TryGetValue(id, out var d)) return d.playRateCap;
            return 4f;
        }

        /// <summary>
        /// Returns the minimum interval (seconds) between landing SFX of this id.
        /// </summary>
        public float GetMinInterval(SfxId id)
        {
            if (_data.TryGetValue(id, out var d)) return d.minIntervalSec;
            return 0f;
        }

        // ── CSV loader ─────────────────────────────────────────────────────────

        private void LoadCsvData()
        {
            var csv = Resources.Load<TextAsset>("Data/sfx");
            if (csv == null)
            {
                Debug.LogWarning("[SfxPlayer] Assets/Resources/Data/sfx.csv not found — using default volumes.");
                return;
            }

            _data.Clear();
            var lines = csv.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            bool firstLine = true;
            foreach (var line in lines)
            {
                if (firstLine) { firstLine = false; continue; } // skip header
                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                if (!Enum.TryParse<SfxId>(parts[0].Trim(), out var id)) continue;

                var d = new SfxData
                {
                    baseVolume     = TryParseFloat(parts[1], 1f),
                    // parts[2] = loop (unused at runtime — AudioManager handles looping)
                    velocityGateMin = TryParseFloat(parts[3], 0f),
                    playRateCap     = TryParseFloat(parts[4], 4f),
                    minIntervalSec  = TryParseFloat(parts[5], 0f)
                };
                _data[id] = d;
            }

            Debug.Log($"[SfxPlayer] Loaded {_data.Count} SFX data entries from sfx.csv.");
        }

        private static float TryParseFloat(string s, float fallback)
        {
            s = s.Trim();
            return float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }
    }
}
